# Guía de la capa Application

Proyecto: `src/Services/Dispensario/Dispensario.Application`.
Orquesta los casos de uso con **CQRS** usando los contratos de `BuildingBlocks.CQRS`.

## 1. Estructura
```
Abstractions/
  IApplicationDbContext.cs   ICurrentUser.cs   IPasswordHasher.cs   IJwtTokenGenerator.cs
Common/
  LikePatterns.cs            # patrones de EF.Functions.Like con comodines escapados
<Module>/
  Commands/<Feature>/ <Feature>Command.cs  <Feature>Handler.cs
  Queries/<Feature>/  <Feature>Query.cs    <Feature>Handler.cs
  Dtos/               <Sustantivo>Dto.cs
  EventHandlers/      <Evento>Handler.cs
DependencyInjection.cs
```

## 2. Abstracciones
> **Estado:** `IApplicationDbContext` existe desde la fila 04; cada módulo añade su `DbSet<T>` al crear su aggregate (hoy `Users` y `Doctors`). `ICurrentUser`, `IPasswordHasher` e `IJwtTokenGenerator` ya están en su forma final (fila 07): `ICurrentUser` lo implementa `CurrentUser` con los claims del JWT, y todas sus propiedades son `null` en una petición anónima o sin `HttpContext`. Abajo, la forma final.

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
```

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

- Validadores: matrícula máx. 30, especialidad máx. 100, email obligatorio, duración (si viene) entre 10 y 120 y múltiplo de 5.
- `DoctorDto` incluye `UserId` (usuario vinculado o `null`). Lo calcula `DoctorProjections.ToDto(doctors, users)` con una subconsulta sobre `Users` (lectura entre módulos); Mapster no la expresa, por eso no hay `DoctorsMappingConfig`.
- `LikePatterns` (`Common/`): `Contains` y `Equal` escapan `%`, `_` y `\` para `EF.Functions.Like` con `LikePatterns.Escape`. Lo usan `GetUsers` y `GetDoctors`.

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

        // Regla de unicidad: necesita la BD, así que se valida aquí y no en el dominio
        if (await db.Patients.AnyAsync(p => p.Document == document, ct))
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
**ScheduleAppointment** (y Reschedule):
1. El paciente existe y está activo; si no → `NotFoundException` o `BadRequestException`.
2. El doctor existe y está activo.
3. `EndUtc = StartUtc + doctor.AppointmentDurationMinutes`.
4. Al convertir a la hora local del dispensario (`Clinic:TimeZone`), el rango cae dentro de un bloque del `WeeklySchedule` y está alineado a la duración del slot; si no → `BadRequestException`.
5. No hay cruce con citas del doctor ni del paciente en estado `Scheduled` o `Confirmed`; si lo hay → `ConflictException`.
6. No se agenda en el pasado.

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
    return services;
}
```
