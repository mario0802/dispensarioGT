# Guía de la capa Application

Proyecto: `src/Services/Dispensario/Dispensario.Application`.
Orquesta los casos de uso con **CQRS** usando los contratos de `BuildingBlocks.CQRS`.

## 1. Estructura
```
Abstractions/
  IApplicationDbContext.cs   ICurrentUser.cs   IPasswordHasher.cs   IJwtTokenGenerator.cs   IClinicTimeZone.cs
Common/
  LikePatterns.cs            # patrones de EF.Functions.Like con comodines escapados
  ClinicOptions.cs  ClinicTimeZone.cs   # zona horaria del dispensario (Clinic:TimeZone) y su conversión local ↔ UTC
<Module>/
  Commands/<Feature>/ <Feature>Command.cs  <Feature>Handler.cs
  Queries/<Feature>/  <Feature>Query.cs    <Feature>Handler.cs
  Dtos/               <Sustantivo>Dto.cs
  EventHandlers/      <Evento>Handler.cs
DependencyInjection.cs
```

## 2. Abstracciones
> **Estado:** `IApplicationDbContext` existe desde la fila 04; cada módulo añade su `DbSet<T>` al crear su aggregate (hoy `Users`, `Doctors`, `Patients` y `Appointments`). `ICurrentUser`, `IPasswordHasher` e `IJwtTokenGenerator` ya están en su forma final (fila 07): `ICurrentUser` lo implementa `CurrentUser` con los claims del JWT, y todas sus propiedades son `null` en una petición anónima o sin `HttpContext`. `IClinicTimeZone` llega con la fila 12. Abajo, la forma final.

```csharp
public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    DbSet<Doctor> Doctors { get; }
    DbSet<Patient> Patients { get; }
    DbSet<Appointment> Appointments { get; }
    DbSet<Consultation> Consultations { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface ICurrentUser
{
    UserId? UserId { get; }
    Role? Role { get; }
    DoctorId? DoctorId { get; }
    bool IsAuthenticated { get; }
}

public interface IPasswordHasher { string Hash(string password); bool Verify(string hash, string password); }
public interface IJwtTokenGenerator { (string Token, DateTime ExpiresAtUtc) Generate(User user); }

public interface IClinicTimeZone
{
    DateTime ToUtc(DateTime local);   // hora local sin offset → UTC; una hora inexistente (salto de DST) → BadRequestException
    DateTime ToLocal(DateTime utc);   // UTC → hora local (Kind = Unspecified)
    DateOnly Today { get; }           // fecha local de hoy según TimeProvider
}
```

`IClinicTimeZone` (fila 12) lo implementa `ClinicTimeZone` (`Common/`) con `TimeZoneInfo` y la zona de `ClinicOptions` (sección `Clinic`, `TimeZone` con un ID IANA; por defecto `America/Guayaquil`). Se registra como singleton en `AddApplication`, que valida la zona al arrancar: si no existe, la aplicación no arranca (`Clinic:TimeZone is not a valid time zone.`). Una hora ambigua se toma como hora estándar.

### Módulo Users: autenticación
La autenticación es parte del módulo Users (los endpoints viven en `Api/Endpoints/Auth/`, con la ruta `/api/auth`):
| Caso de uso | Ubicación | Reglas |
|---|---|---|
| `LoginCommand` → `LoginResult(AccessToken, TokenType, ExpiresAtUtc)` | `Users/Commands/Login/` | Es un command (aunque no escribe) para pasar por `ValidationBehavior`. Email inexistente, contraseña incorrecta o usuario inactivo → `UnauthorizedException("Invalid credentials.")`, el mismo mensaje en los tres casos. El email se normaliza con `Email.Create`, así que no distingue mayúsculas |
| `GetCurrentUserQuery` → `CurrentUserDto` | `Users/Queries/GetCurrentUser/` | Lee el usuario de la BD por `ICurrentUser.UserId`: sin usuario autenticado → 401, inexistente → `NotFoundException`, inactivo → `UnauthorizedException("User is inactive.")` |

`UsersMappingConfig` (`Users/Dtos/`) mapea `User → CurrentUserDto` y `User → UserDto` (IDs a `Guid`, VOs a sus valores; `UserDto` añade `CreatedAtUtc`).

### Módulo Users: gestión de usuarios
Usan los métodos que ya tiene `User` (`Create`, `ChangePassword`, `UpdateProfile`, `Deactivate`, `Activate`). Las reglas que necesitan la BD o el usuario actual van en los handlers:
| Caso de uso | Ubicación | Reglas |
|---|---|---|
| `RegisterUserCommand` → `RegisterUserResult(Id)` | `Users/Commands/RegisterUser/` | Email (normalizado a minúsculas) o identificación ya usados → `ConflictException`. Lo usan el autoregistro (el endpoint fija `Role.Receptionist`) y el alta por el Admin (cualquier rol) |
| `ChangePasswordCommand` | `Users/Commands/ChangePassword/` | Usuario actual: sin `UserId` → 401, inexistente → 404, inactivo → 401. Contraseña actual incorrecta → `BadRequestException("Current password is incorrect.")` (400, no 401). La nueva debe ser distinta de la actual |
| `ResetUserPasswordCommand` | `Users/Commands/ResetUserPassword/` | Sobre el propio usuario → `BadRequestException` (debe usar `ChangePassword`, que exige la actual). Funciona también sobre inactivos |
| `UpdateUserProfileCommand` / `UpdateCurrentUserProfileCommand` | `Users/Commands/UpdateUserProfile/`, `UpdateCurrentUserProfile/` | Nombre y teléfono (`null` borra el teléfono). La versión del usuario actual aplica las mismas comprobaciones 401/404/401 que `ChangePassword` |
| `DeactivateUserCommand` | `Users/Commands/DeactivateUser/` | Sobre sí mismo → `BadRequestException`. Último Admin activo → `ConflictException`. Ya inactivo → `DomainException` |
| `ActivateUserCommand` | `Users/Commands/ActivateUser/` | Ya activo → `DomainException` |
| `LinkDoctorCommand(UserId, DoctorId)` | `Users/Commands/LinkDoctor/` | Lee `Doctors` y solo escribe en `Users`. Usuario o doctor inexistentes → `NotFoundException`. Doctor inactivo → `BadRequestException("Doctor is inactive.")`. Doctor ya vinculado a otro usuario → `ConflictException` (respaldado por el índice único de `Users.DoctorId`). Rol distinto de `Doctor` o usuario ya vinculado → `DomainException`. El claim `doctor_id` llega con el siguiente login |
| `GetUsersQuery` → `PaginatedResult<UserDto>` | `Users/Queries/GetUsers/` | `PageIndex < 0` o `PageSize` fuera de 1–100 → `BadRequestException` (las queries no pasan por `ValidationBehavior`). `Search` con `EF.Functions.Like` (insensible a mayúsculas en ASCII, comodines escapados) en nombre, apellido, email e identificación; filtros `Role` e `IsActive`; orden por apellido, nombre e ID; `ProjectToType<UserDto>` |
| `GetUserByIdQuery` → `UserDto` | `Users/Queries/GetUserById/` | Inexistente → `NotFoundException` |

- `PasswordRules` (`Users/Validation/`): extensión `ValidPassword()` de FluentValidation, de 8 a 72 caracteres (límite de bcrypt) con al menos una letra y un dígito. La usan el registro, el cambio y el reseteo.
- `Email` se guarda con `HasConversion`: para usarlo como texto en `EF.Functions.Like` se escribe `(string)(object)u.Email`, que EF traduce a la columna. `string.Contains` en SQLite genera `instr`, que sí distingue mayúsculas.
- Un owned type (`Identification`) no se compara entero en LINQ: se comparan `Type` y `Number` por separado.

### Módulo Doctors
Usan los métodos de `Doctor` (`Create`, `Update`, `Deactivate`, `Activate`). Escritura `AdminOnly`; lecturas para cualquier usuario autenticado:
| Caso de uso | Ubicación | Reglas |
|---|---|---|
| `CreateDoctorCommand` → `CreateDoctorResult(Id)` | `Doctors/Commands/CreateDoctor/` | `AppointmentDurationMinutes` null → `Doctor.DefaultAppointmentDurationMinutes` (30). Matrícula (ya normalizada por el aggregate) repetida → `ConflictException` |
| `UpdateDoctorCommand` | `Doctors/Commands/UpdateDoctor/` | Inexistente → `NotFoundException`. `AppointmentDurationMinutes` null conserva la actual. Matrícula de otro doctor → `ConflictException`. Vale sobre doctores inactivos |
| `DeactivateDoctorCommand` / `ActivateDoctorCommand` | `Doctors/Commands/DeactivateDoctor/`, `ActivateDoctor/` | Ya en ese estado → `DomainException`. No tocan al usuario vinculado |
| `GetDoctorsQuery` → `PaginatedResult<DoctorDto>` | `Doctors/Queries/GetDoctors/` | Paginación validada como en `GetUsers`. `Search` con `EF.Functions.Like` en nombre, apellido, matrícula y especialidad; `Specialty` igualdad sin distinguir mayúsculas (LIKE sin comodines); filtro `IsActive`; orden por apellido, nombre e ID |
| `GetDoctorByIdQuery` → `DoctorDto` | `Doctors/Queries/GetDoctorById/` | Inexistente → `NotFoundException` |
| `SetDoctorScheduleCommand(DoctorId, Blocks)` | `Doctors/Commands/SetDoctorSchedule/` | Recibe `ICurrentUser`. Un `Doctor` sobre otra ficha (o sin `DoctorId`) → `ForbiddenException`, **antes** de leer la BD (no revela qué IDs existen). Inexistente → `NotFoundException`. Parsea las horas `"HH:mm"` y llama a `SetWeeklySchedule`: múltiplo de 5, inicio < fin, duración mínima, solapes y doctor inactivo → `DomainException`. Reemplaza el horario entero |
| `GetDoctorScheduleQuery` → `DoctorScheduleDto(DoctorId, Blocks)` | `Doctors/Queries/GetDoctorSchedule/` | Inexistente → `NotFoundException`. Bloques de lunes a domingo y por hora de inicio (orden en memoria), horas `"HH:mm"`; también para doctores inactivos |

- Validadores: matrícula máx. 30, especialidad máx. 100, email obligatorio, duración (si viene) entre 10 y 120 y múltiplo de 5. `SetDoctorScheduleCommandValidator` solo comprueba la forma: `Blocks` no null (vacía vale), `DayOfWeek` del enum y horas `^([01][0-9]|2[0-3]):[0-5][0-9]$`; las reglas del horario son del dominio.
- `UpdateDoctorCommand` falla con `DomainException` si la nueva duración es mayor que algún bloque del horario (regla de `Doctor.Update`).
- `DoctorDto` incluye `UserId` (usuario vinculado o `null`). Lo calcula `DoctorProjections.ToDto(doctors, users)` con una subconsulta sobre `Users` (lectura entre módulos); Mapster no la expresa, por eso no hay `DoctorsMappingConfig`.
- `LikePatterns` (`Common/`): `Contains` y `Equal` escapan `%`, `_` y `\` para `EF.Functions.Like` con `LikePatterns.Escape`. Lo usan `GetUsers`, `GetDoctors` y `GetPatients`.

### Módulo Patients
Usan los métodos de `Patient` (`Create`, `Update`, `Deactivate`, `Activate`). Escritura con la política `Reception` (Admin y Receptionist); lecturas para cualquier usuario autenticado:
| Caso de uso | Ubicación | Reglas |
|---|---|---|
| `CreatePatientCommand` → `CreatePatientResult(Id)` | `Patients/Commands/CreatePatient/` | Recibe `TimeProvider` y pasa la fecha UTC de hoy al dominio (nacimiento futuro → `DomainException`). Documento (tipo + número recortado) repetido → `ConflictException` |
| `UpdatePatientCommand` | `Patients/Commands/UpdatePatient/` | Inexistente → `NotFoundException`. Reemplaza todo, incluido el documento; un opcional null borra el valor. Documento de otro paciente → `ConflictException`. Vale sobre pacientes inactivos |
| `DeactivatePatientCommand` / `ActivatePatientCommand` | `Patients/Commands/DeactivatePatient/`, `ActivatePatient/` | Ya en ese estado → `DomainException` |
| `GetPatientsQuery` → `PaginatedResult<PatientDto>` | `Patients/Queries/GetPatients/` | Paginación validada como en `GetUsers`. `Search` con `EF.Functions.Like` en nombre, apellido y número de documento; filtro `IsActive`; orden por apellido, nombre e ID; `ProjectToType<PatientDto>` |
| `GetPatientByIdQuery` → `PatientDto` | `Patients/Queries/GetPatientById/` | Inexistente → `NotFoundException` |
| `GetPatientByDocumentQuery(DocumentType, DocumentNumber)` → `PatientDto` | `Patients/Queries/GetPatientByDocument/` | Número en blanco → `BadRequestException`. Igualdad exacta por tipo y número recortado (distingue mayúsculas, como el índice único). Inexistente → `NotFoundException`. Devuelve también pacientes inactivos |

- `BloodTypeNotation` (`Patients/`): traduce la notación clínica (`"A+"`, `"AB-"`…) ↔ `BloodType`. Commands, DTOs y requests llevan el texto; el dominio y la BD, el enum. Los validadores rechazan un valor fuera de los ocho con `ValidationErrors`.
- `PatientData` (`Patients/Commands/`, interno): construye los VOs (documento, nombre, email, teléfono, dirección) y traduce el tipo de sangre a partir de los campos de `CreatePatient` y `UpdatePatient`.
- `AddressInput` (en el archivo de `CreatePatientCommand`) es el objeto de dirección de los commands; `AddressInputValidator` lo valida (calle y ciudad obligatorias, máximos de `Address`).
- `PatientsMappingConfig` (Mapster): `Patient` → `PatientDto`, con `Address` → `AddressDto` o `null` y `BloodType` → notación clínica. `BloodTypeNotation.ToNotation` no se traduce a SQL: EF lo evalúa en cliente en la proyección final.

### Módulo Appointments
Agenda la recepción (política `Reception`); leen todos los autenticados, pero un `Doctor` solo ve sus citas. Las horas de la Api son locales del dispensario y la BD guarda UTC:
| Caso de uso | Ubicación | Reglas |
|---|---|---|
| `ScheduleAppointmentCommand(PatientId, DoctorId, StartLocal, Reason)` → `ScheduleAppointmentResult(Id)` | `Appointments/Commands/ScheduleAppointment/` | Recibe `IClinicTimeZone` y `TimeProvider`. Lee `Patients` y `Doctors` y solo escribe en `Appointments`. Orden de comprobaciones en §6 |
| `GetAppointmentsQuery(Pagination, DoctorId?, PatientId?, Status?, From?, To?)` → `PaginatedResult<AppointmentDto>` | `Appointments/Queries/GetAppointments/` | Recibe `ICurrentUser`. Un `Doctor` sin `DoctorId` → `ForbiddenException("Your user is not linked to a doctor.")`; con un `DoctorId` ajeno en el filtro → `ForbiddenException("You can only view your own appointments.")`; sin filtro se filtra por el suyo. Todo antes de leer la BD. Paginación validada como en `GetUsers`; `From > To` → `BadRequestException`. `From`/`To` son fechas locales inclusive: se convierten a límites UTC `[From 00:00, To + 1 día 00:00)`. Orden por `StartUtc` e ID |
| `GetAppointmentByIdQuery` → `AppointmentDto` | `Appointments/Queries/GetAppointmentById/` | `Doctor` sin `DoctorId` → `ForbiddenException`. Inexistente → `NotFoundException`. Cita de otro doctor → `ForbiddenException` (el 404 va antes: hay que leer la cita para saber de quién es) |

- `ScheduleAppointmentCommandValidator`: IDs no vacíos; `StartLocal` no vacío y con `Kind == Unspecified` (`Start time must be a local time without offset.`), porque System.Text.Json solo deja `Unspecified` cuando el texto no trae `Z` ni offset; `Reason` no vacío y máx. 500.
- `AppointmentDto` lleva `PatientFullName` y `DoctorFullName` (subconsultas sobre `Patients` y `Doctors` en `AppointmentProjections.ToDto`, sin Mapster), las horas UTC y las locales. `TimeZoneInfo` no se traduce a SQL: `ToDto` deja `StartLocal`/`EndLocal` vacías y `WithLocalTimes(clinicTimeZone)` las rellena en memoria tras leer la página (máximo 100 filas).

## 3. Plantilla de un command
```csharp
// CreatePatientCommand.cs
namespace Dispensario.Application.Patients.Commands.CreatePatient;

public sealed record CreatePatientCommand(
    DocumentType DocumentType, string DocumentNumber,
    string FirstName, string LastName, DateOnly BirthDate, Gender Gender,
    string? Email, string? Phone) : ICommand<CreatePatientResult>;

public sealed record CreatePatientResult(Guid Id);

public sealed class CreatePatientCommandValidator : AbstractValidator<CreatePatientCommand>
{
    public CreatePatientCommandValidator()
    {
        RuleFor(x => x.DocumentNumber).NotEmpty().MaximumLength(20);
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        // ...
    }
}

// CreatePatientHandler.cs
/// <summary>Registra un paciente nuevo; el documento debe ser único.</summary>
public sealed class CreatePatientHandler(IApplicationDbContext db)
    : ICommandHandler<CreatePatientCommand, CreatePatientResult>
{
    public async Task<CreatePatientResult> Handle(CreatePatientCommand command, CancellationToken ct)
    {
        var document = Document.Create(command.DocumentType, command.DocumentNumber);

        // Regla de unicidad: necesita la BD, así que se valida aquí y no en el dominio.
        // Un owned type no se compara entero en LINQ: se comparan Type y Number por separado
        if (await db.Patients.AnyAsync(p => p.Document.Type == document.Type && p.Document.Number == document.Number, ct))
            throw new ConflictException($"Patient with document {document} already exists.");

        var patient = Patient.Create(document, /* ... */);
        db.Patients.Add(patient);
        await db.SaveChangesAsync(ct);

        return new CreatePatientResult(patient.Id.Value);
    }
}
```

## 4. Plantilla de una query
```csharp
public sealed record GetPatientsQuery(PaginationRequest Pagination, string? Search)
    : IQuery<PaginatedResult<PatientDto>>;

public sealed class GetPatientsHandler(IApplicationDbContext db)
    : IQueryHandler<GetPatientsQuery, PaginatedResult<PatientDto>>
{
    public async Task<PaginatedResult<PatientDto>> Handle(GetPatientsQuery query, CancellationToken ct)
    {
        var source = db.Patients.AsNoTracking();
        // ...filtro por Search en nombre o documento
        var total = await source.LongCountAsync(ct);
        var items = await source
            .OrderBy(p => p.FullName.LastName)
            .Skip(query.Pagination.PageIndex * query.Pagination.PageSize)
            .Take(query.Pagination.PageSize)
            .ProjectToType<PatientDto>()          // Mapster
            .ToListAsync(ct);
        return new PaginatedResult<PatientDto>(query.Pagination.PageIndex, query.Pagination.PageSize, total, items);
    }
}
```

## 5. Reglas
| Regla | Detalle |
|---|---|
| Un handler, un módulo de escritura | Puede leer otros DbSets, pero solo modifica aggregates de su módulo |
| Excepciones | `NotFoundException(nameof(Patient), id)`, `ConflictException` (duplicados/cruces), `BadRequestException`, `ForbiddenException`; las reglas de dominio lanzan `DomainException` |
| Validación | FluentValidation solo para forma y formato; `ValidationBehavior` aplica a los commands |
| Queries | Siempre `AsNoTracking()` y proyección a DTO; nunca devolver entidades |
| Tiempo | Se inyecta `TimeProvider` y se usa `GetUtcNow().UtcDateTime` |
| Transacciones | Un `SaveChangesAsync` por command. Los domain events se despachan **después** de guardar (ver INFRASTRUCTURE.md) |
| Mapeo | Mapster: `TypeAdapterConfig` por módulo en `<Module>/Dtos/<Module>MappingConfig.cs` (`IRegister`) |

## 6. Reglas de negocio con varios aggregates
**ScheduleAppointment** (fila 12; Reschedule reutilizará las mismas reglas), en este orden:
1. El paciente existe (`NotFoundException`) y está activo (`BadRequestException("Patient is inactive.")`).
2. El doctor existe (`NotFoundException`) y está activo (`BadRequestException("Doctor is inactive.")`).
3. `StartUtc = IClinicTimeZone.ToUtc(StartLocal)`, `EndUtc = StartUtc + doctor.AppointmentDurationMinutes` y `localEnd = StartLocal + duración`.
4. `Appointment.Create(..., nowUtc)`: cita en el pasado o motivo inválido → `DomainException`.
5. `doctor.IsAvailableAt(StartLocal, localEnd)` (el rango local cabe entero en un `ScheduleBlock`); si no → `BadRequestException("The doctor is not available at that time.")`.
6. Alineación **desde el inicio del bloque** que contiene la cita: `(inicio local − bloque.StartTime)` es múltiplo de la duración; si no → `BadRequestException("Start time must align with the doctor's appointment slots.")`. Con un bloque `09:10–12:10` y 30 minutos valen `09:10`, `09:40`…
7. No hay cruce (`StartUtc < endUtc && startUtc < EndUtc`) con citas `Scheduled` o `Confirmed` del doctor (`ConflictException("The doctor already has an appointment at that time.")`) ni, después, del paciente (`ConflictException("The patient already has an appointment at that time.")`). Una cita contigua no es cruce.
8. Un único `SaveChangesAsync`. Dos altas simultáneas podrían pasar los dos la comprobación de cruces: aceptado en v1.

**StartConsultation**: el usuario actual es `Doctor` y `ICurrentUser.DoctorId` es el doctor de la consulta. Si se envía `AppointmentId`, la cita debe ser de ese doctor y paciente y estar `Scheduled` o `Confirmed`. Solo puede haber una consulta `Open` por cita.

## 7. DependencyInjection
```csharp
public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
{
    var assembly = typeof(DependencyInjection).Assembly;
    services.AddMediatR(cfg =>
    {
        // Solo se asigna si hay clave: una cadena vacía no es una licencia válida
        var licenseKey = configuration["MediatR:LicenseKey"];
        if (!string.IsNullOrWhiteSpace(licenseKey))
            cfg.LicenseKey = licenseKey;

        cfg.RegisterServicesFromAssembly(assembly);
        cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
        cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
    });
    services.AddValidatorsFromAssembly(assembly);
    TypeAdapterConfig.GlobalSettings.Scan(assembly);

    // Con una zona inexistente la aplicación no arranca (fila 12)
    services.AddOptions<ClinicOptions>()
        .Bind(configuration.GetSection(ClinicOptions.SectionName))
        .Validate(o => o.TimeZone is not null && TimeZoneInfo.TryFindSystemTimeZoneById(o.TimeZone, out _),
            "Clinic:TimeZone is not a valid time zone.")
        .ValidateOnStart();
    services.AddSingleton<IClinicTimeZone, ClinicTimeZone>();
    return services;
}
```
