# SPEC 09 — Doctors: CRUD y vínculo con el usuario

> **Status:** Approved
> **Depends on:** SPEC 06, SPEC 07, SPEC 08
> **Date:** 2026-10-26
> **Objective:** Crear el aggregate `Doctor` con su persistencia y exponer su alta, edición, activación, desactivación y consulta, además del vínculo de un usuario `Doctor` con su ficha de doctor.

## Por qué este spec existe

Es el primer módulo clínico. Appointments (fila 12) y Consultations (fila 15) necesitan doctores existentes y activos, y Consultations necesita que el usuario `Doctor` tenga su `DoctorId` en el token.
`User.LinkDoctor` existe desde SPEC 06, pero nadie lo llama: un usuario `Doctor` creado en SPEC 08 queda con `DoctorId = null`.
El vínculo se hace desde el módulo Users, porque un handler solo escribe en su propio módulo (`ARCHITECTURE.md`, regla 3).
Este spec cierra la pregunta que `DOMAIN.md` dejaba abierta: `DoctorId` **sigue siendo opcional** para un usuario con rol `Doctor`.

## Scope

**In:**

- Aggregate `Doctor` en `Dispensario.Domain/Doctors/` con `Create`, `Update`, `Deactivate` y `Activate`, y sus tests de dominio.
- Persistencia: `DoctorConfiguration` (tabla `Doctors`, índice único de `LicenseNumber`), `DbSet<Doctor>` en `IApplicationDbContext` y `DispensarioDbContext`, e índice único en `Users.DoctorId`.
- Commands en `Dispensario.Application/Doctors/Commands/`: `CreateDoctor`, `UpdateDoctor`, `DeactivateDoctor` y `ActivateDoctor`, con validadores y handlers.
- Queries en `Dispensario.Application/Doctors/Queries/`: `GetDoctors` (paginada, con `search`, `specialty` e `isActive`) y `GetDoctorById`.
- `DoctorDto` con el `UserId` del usuario vinculado (o `null`).
- Command `LinkDoctor` en `Dispensario.Application/Users/Commands/LinkDoctor/` y su endpoint `POST /api/users/{id}/link-doctor`.
- Endpoints `POST /api/doctors`, `PUT /api/doctors/{id}`, `POST /api/doctors/{id}/deactivate` y `POST /api/doctors/{id}/activate` (`AdminOnly`), y `GET /api/doctors` y `GET /api/doctors/{id}` (cualquier usuario autenticado).
- Tests unitarios de dominio, persistencia, validadores y handlers, y tests de integración de los endpoints.
- Documentación: sección **Endpoints** del `README.md`, `DOMAIN.md`, `APPLICATION.md`, `INFRASTRUCTURE.md`, `API.md`, `CLAUDE.md` y `docs/PLAN.md`.

**Out of scope (for future specs):**

- Horario semanal (`WeeklySchedule`, `SetWeeklySchedule`, `IsAvailableAt`) y la tabla `DoctorSchedules`: fila 10.
- Desvincular un usuario de su doctor (`UnlinkDoctor`) y cambiar el vínculo a otro doctor.
- Vincular al crear el doctor o al crear el usuario (`userId` en `POST /api/doctors` o `doctorId` en `POST /api/users`).
- Hacer obligatorio `DoctorId` para los usuarios con rol `Doctor`.
- Que un doctor edite su propia ficha.
- Domain events en Doctors (`DoctorCreated`, `DoctorDeactivated`…): no hay consumidores.
- Efectos de desactivar un doctor sobre su usuario vinculado o sobre citas futuras (aún no hay citas).
- Catálogo de especialidades: `Specialty` es texto libre en v1.
- Borrado de doctores (solo se desactivan).
- Migraciones: siguen con `EnsureCreated` hasta la fila 17.

## Data model

### Aggregate `Doctor` (`Dispensario.Domain/Doctors/`)

```
Doctors/
  DoctorId.cs   (ya existe; cambia su comentario)
  Doctor.cs     (nuevo)
```

```csharp
public sealed class Doctor : AggregateRoot<DoctorId>
{
    public const int LicenseNumberMaxLength = 30;
    public const int SpecialtyMaxLength = 100;
    public const int MinAppointmentDurationMinutes = 10;
    public const int MaxAppointmentDurationMinutes = 120;
    public const int AppointmentDurationStepMinutes = 5;
    public const int DefaultAppointmentDurationMinutes = 30;

    public FullName FullName { get; private set; }
    public string LicenseNumber { get; private set; }      // Trim + ToUpperInvariant; único
    public string Specialty { get; private set; }          // Trim; texto libre
    public Email Email { get; private set; }               // obligatorio, no único
    public PhoneNumber? Phone { get; private set; }        // opcional
    public int AppointmentDurationMinutes { get; private set; }
    public bool IsActive { get; private set; }             // true al crear

    public static Doctor Create(FullName fullName, string licenseNumber, string specialty,
        Email email, PhoneNumber? phone, int appointmentDurationMinutes);
    public void Update(FullName fullName, string licenseNumber, string specialty,
        Email email, PhoneNumber? phone, int appointmentDurationMinutes);   // phone null borra el teléfono
    public void Deactivate();
    public void Activate();
}
```

Reglas de dominio (`DomainException` → 400):

| Regla | Mensaje |
| --- | --- |
| `LicenseNumber` vacío tras el `Trim` | `License number is required.` |
| `LicenseNumber` de más de 30 caracteres | `License number must be at most 30 characters.` |
| `Specialty` vacía tras el `Trim` | `Specialty is required.` |
| `Specialty` de más de 100 caracteres | `Specialty must be at most 100 characters.` |
| Duración fuera de 10–120 o no múltiplo de 5 | `Appointment duration must be between 10 and 120 minutes and a multiple of 5.` |
| `Deactivate` sobre un inactivo | `Doctor is already inactive.` |
| `Activate` sobre un activo | `Doctor is already active.` |

- `Create` y `Update` aplican las mismas reglas y la misma normalización.
- `Update` también se permite sobre un doctor inactivo.
- El dominio no conoce el valor por defecto de la petición: el 30 lo aplica el handler de alta con `Doctor.DefaultAppointmentDurationMinutes`.
- Sin domain events.

### Persistencia (`Dispensario.Infrastructure/Data/`)

`Configurations/Doctors/DoctorConfiguration.cs` (nuevo), tabla `Doctors`:

| Columna | Origen | Notas |
| --- | --- | --- |
| `Id` | `DoctorId` | conversión por convención |
| `FullName_FirstName`, `FullName_LastName` | `ComplexProperty` | `FullName.MaxLength`, requeridas |
| `LicenseNumber` | `string` | `Doctor.LicenseNumberMaxLength`, requerida, **índice único** |
| `Specialty` | `string` | `Doctor.SpecialtyMaxLength`, requerida |
| `Email` | `HasConversion` | `Email.MaxLength`, requerida, sin índice |
| `Phone` | `HasConversion` | `PhoneNumber.MaxLength`, nullable |
| `AppointmentDurationMinutes` | `int` | requerida |
| `IsActive` | `bool` | requerida |
| auditoría | `IAuditable` | como en `Users` |

Cambios en código existente:

- `IApplicationDbContext` y `DispensarioDbContext` añaden `DbSet<Doctor> Doctors`.
- `UserConfiguration`: `builder.HasIndex(u => u.DoctorId).IsUnique()`. SQLite admite varios `NULL` en un índice único, así que no hace falta filtro. Sigue sin FK: los módulos se relacionan solo por ID. Se actualiza el comentario de la línea `DoctorId`.
- El esquema cambia: hay que **borrar `dispensario.db`** local antes de `dotnet run`.

### `Dispensario.Application/Doctors/` (namespaces iguales a las carpetas)

```
Doctors/
  Commands/
    CreateDoctor/      CreateDoctorCommand.cs  CreateDoctorHandler.cs
    UpdateDoctor/      UpdateDoctorCommand.cs  UpdateDoctorHandler.cs
    DeactivateDoctor/  DeactivateDoctorCommand.cs  DeactivateDoctorHandler.cs
    ActivateDoctor/    ActivateDoctorCommand.cs  ActivateDoctorHandler.cs
  Queries/
    GetDoctors/        GetDoctorsQuery.cs  GetDoctorsHandler.cs
    GetDoctorById/     GetDoctorByIdQuery.cs  GetDoctorByIdHandler.cs
  Dtos/                DoctorDto.cs  DoctorProjections.cs
Users/
  Commands/
    LinkDoctor/        LinkDoctorCommand.cs  LinkDoctorHandler.cs
```

```csharp
public sealed record CreateDoctorCommand(
    string FirstName, string LastName,
    string LicenseNumber, string Specialty,
    string Email, string? Phone,
    int? AppointmentDurationMinutes) : ICommand<CreateDoctorResult>;   // null → 30
public sealed record CreateDoctorResult(Guid Id);

public sealed record UpdateDoctorCommand(
    Guid DoctorId,
    string FirstName, string LastName,
    string LicenseNumber, string Specialty,
    string Email, string? Phone,
    int? AppointmentDurationMinutes) : ICommand;                      // null → conserva la actual

public sealed record DeactivateDoctorCommand(Guid DoctorId) : ICommand;
public sealed record ActivateDoctorCommand(Guid DoctorId) : ICommand;

public sealed record GetDoctorsQuery(PaginationRequest Pagination, string? Search, string? Specialty, bool? IsActive)
    : IQuery<PaginatedResult<DoctorDto>>;
public sealed record GetDoctorByIdQuery(Guid DoctorId) : IQuery<DoctorDto>;

public sealed record DoctorDto(
    Guid Id, string FirstName, string LastName,
    string LicenseNumber, string Specialty,
    string Email, string? Phone,
    int AppointmentDurationMinutes, bool IsActive,
    Guid? UserId, DateTime CreatedAtUtc);

public sealed record LinkDoctorCommand(Guid UserId, Guid DoctorId) : ICommand;
```

- `DoctorProjections.ToDto(this IQueryable<Doctor> doctors, IQueryable<User> users)`: proyección LINQ que comparten las dos queries. `UserId` sale de una subconsulta sobre `users` por `DoctorId` (lectura entre módulos, permitida). Mapster no puede expresar esa subconsulta, por eso no hay `DoctorsMappingConfig`.

### Validadores (en el archivo de cada command)

| Command | Reglas |
| --- | --- |
| `CreateDoctorCommand` | `FirstName`/`LastName`: `NotEmpty`, `MaximumLength(FullName.MaxLength)`. `LicenseNumber`: `NotEmpty`, `MaximumLength(Doctor.LicenseNumberMaxLength)`. `Specialty`: `NotEmpty`, `MaximumLength(Doctor.SpecialtyMaxLength)`. `Email`: `NotEmpty`, `MaximumLength(Email.MaxLength)`, `EmailAddress`. `Phone`: `MaximumLength(PhoneNumber.MaxLength)` si no es null. `AppointmentDurationMinutes`: si no es null, `InclusiveBetween(10, 120)` y múltiplo de 5 |
| `UpdateDoctorCommand` | `DoctorId`: `NotEmpty`. El resto como en `CreateDoctorCommand` |
| `DeactivateDoctorCommand` / `ActivateDoctorCommand` | `DoctorId`: `NotEmpty` |
| `LinkDoctorCommand` | `UserId` y `DoctorId`: `NotEmpty` |

- Las longitudes se validan con los valores sin recortar; el dominio las vuelve a comprobar tras el `Trim`.
- El formato del teléfono lo valida el VO `PhoneNumber` (`DomainException` → 400).

### Handlers

Todos reciben `IApplicationDbContext`. Cada command hace un único `SaveChangesAsync`.

| Handler | Comportamiento, en orden |
| --- | --- |
| `CreateDoctorHandler` | Crea los VOs y el `Doctor` con `AppointmentDurationMinutes ?? Doctor.DefaultAppointmentDurationMinutes`. Si existe otro doctor con el `LicenseNumber` ya normalizado → `ConflictException("Doctor with license number {licenseNumber} already exists.")`. Guarda y devuelve el ID |
| `UpdateDoctorHandler` | No existe → `NotFoundException(nameof(Doctor), id)`. `Update(...)` con `AppointmentDurationMinutes ?? doctor.AppointmentDurationMinutes`. Si otro doctor (ID distinto) tiene la matrícula normalizada → `ConflictException` con el mismo mensaje. Guarda |
| `DeactivateDoctorHandler` | No existe → `NotFoundException`. `Deactivate()` (inactivo → 400 de dominio) y guarda. No toca al usuario vinculado |
| `ActivateDoctorHandler` | No existe → `NotFoundException`. `Activate()` (activo → 400 de dominio) y guarda |
| `GetDoctorsHandler` | `PageIndex < 0` o `PageSize` fuera de 1–100 → `BadRequestException`. `AsNoTracking`; filtros opcionales: `IsActive`; `Specialty` (recortada; igualdad sin distinguir mayúsculas con `EF.Functions.Like` y comodines escapados); `Search` (recortado; contiene, con `EF.Functions.Like`, en nombre, apellido, matrícula o especialidad). Orden por apellido, nombre e ID. Proyecta con `DoctorProjections.ToDto` y devuelve `PaginatedResult<DoctorDto>` |
| `GetDoctorByIdHandler` | `AsNoTracking`; no existe → `NotFoundException`; si no, `DoctorDto` |
| `LinkDoctorHandler` | Usuario no existe → `NotFoundException(nameof(User), id)`. Doctor no existe → `NotFoundException(nameof(Doctor), id)`. Doctor inactivo → `BadRequestException("Doctor is inactive.")`. Otro usuario ya tiene ese `DoctorId` → `ConflictException("Doctor {doctorId} is already linked to another user.")`. `user.LinkDoctor(doctorId)` (rol distinto de `Doctor` o usuario ya vinculado → 400 de dominio) y guarda |

- `LinkDoctorHandler` lee `Doctors` pero solo escribe en `Users`.
- El índice único de `Users.DoctorId` respalda la regla del 409 ante dos peticiones simultáneas.
- El claim `doctor_id` solo aparece en los tokens emitidos después del vínculo: el usuario debe volver a hacer login.
- El escape de comodines de `LIKE` se reutiliza del de `GetUsersHandler`. Si se mueve a un helper compartido, va en `Dispensario.Application/Common/` y `GetUsersHandler` pasa a usarlo.

### `Dispensario.Api/Endpoints/`

```
Doctors/
  CreateDoctorEndpoint.cs  GetDoctorsEndpoint.cs  GetDoctorByIdEndpoint.cs
  UpdateDoctorEndpoint.cs  DeactivateDoctorEndpoint.cs  ActivateDoctorEndpoint.cs
Users/
  LinkDoctorEndpoint.cs
```

| Ruta | Request → Response | Autorización | Respuestas |
| --- | --- | --- | --- |
| `POST /api/doctors` | `CreateDoctorRequest(FirstName, LastName, LicenseNumber, Specialty, Email, Phone?, AppointmentDurationMinutes?)` → `CreateDoctorResponse(Guid Id)` | `AdminOnly` | 201 (`Location: /api/doctors/{id}`), 400, 401, 403, 409 |
| `GET /api/doctors` | `[AsParameters] GetDoctorsRequest(int PageIndex = 0, int PageSize = 10, string? Search, string? Specialty, bool? IsActive)` → `PaginatedResult<DoctorResponse>` | `RequireAuthorization()` | 200, 400, 401 |
| `GET /api/doctors/{id:guid}` | — → `DoctorResponse` (campos de `DoctorDto`) | `RequireAuthorization()` | 200, 401, 404 |
| `PUT /api/doctors/{id:guid}` | `UpdateDoctorRequest` (los mismos campos que el alta) | `AdminOnly` | 204, 400, 401, 403, 404, 409 |
| `POST /api/doctors/{id:guid}/deactivate` | — | `AdminOnly` | 204, 400, 401, 403, 404 |
| `POST /api/doctors/{id:guid}/activate` | — | `AdminOnly` | 204, 400, 401, 403, 404 |
| `POST /api/users/{id:guid}/link-doctor` | `LinkDoctorRequest(Guid DoctorId)` | `AdminOnly` | 204, 400, 401, 403, 404, 409 |

- Los endpoints de `/api/doctors` llevan `WithTags("Doctors")`; el de vínculo, `WithTags("Users")`. Todos con `WithName` (igual al nombre del command o query) y `WithSummary`.

### Tests

| Archivo | Qué prueba |
| --- | --- |
| `tests/Dispensario.Domain.UnitTests/Doctors/DoctorTests.cs` | `Create` normaliza la matrícula (`" mp-123 "` → `"MP-123"`) y recorta la especialidad; queda activo. Matrícula o especialidad vacías o demasiado largas → `DomainException`. Duraciones 5, 125 y 32 fallan; 10, 30 y 120 pasan. `Update` cambia todos los campos y `phone` null borra el teléfono. `Deactivate` y `Activate` cambian el estado y fallan si se repiten. Sin domain events |
| `tests/Dispensario.Application.UnitTests/Common/TestDoctors.cs` | Builder de doctores de prueba (como `TestUsers`) |
| `tests/Dispensario.Application.UnitTests/Persistence/Doctors/DoctorPersistenceTests.cs` | Ida y vuelta de un `Doctor` con todos sus campos y auditoría; matrícula repetida → `DbUpdateException`; dos `User` con el mismo `DoctorId` → `DbUpdateException`; varios `User` con `DoctorId` null se guardan |
| `tests/Dispensario.Application.UnitTests/Doctors/CreateDoctorCommandValidatorTests.cs` | Nombre vacío, email inválido, matrícula de 31 caracteres, especialidad de 101 caracteres y duraciones 5, 125 y 32 fallan; duración null y un comando correcto pasan. Incluye `UpdateDoctorCommandValidator`: `DoctorId` vacío falla |
| `tests/Dispensario.Application.UnitTests/Doctors/CreateDoctorHandlerTests.cs` | Crea el doctor con matrícula normalizada; sin duración → 30; matrícula repetida con otras mayúsculas → `ConflictException` |
| `tests/Dispensario.Application.UnitTests/Doctors/UpdateDoctorHandlerTests.cs` | Cambia los campos; sin duración conserva la actual; mantener su propia matrícula no da conflicto; matrícula de otro doctor → `ConflictException`; inexistente → `NotFoundException`; edita un doctor inactivo |
| `tests/Dispensario.Application.UnitTests/Doctors/DeactivateDoctorHandlerTests.cs` | Desactiva; ya inactivo → `DomainException`; inexistente → `NotFoundException`; el usuario vinculado sigue activo y vinculado. Incluye `ActivateDoctorHandler`: activa; ya activo → `DomainException`; inexistente → `NotFoundException` |
| `tests/Dispensario.Application.UnitTests/Doctors/GetDoctorsHandlerTests.cs` | Paginación (`Count` total y tamaño de página); filtro `IsActive`; `Specialty` sin distinguir mayúsculas y sin coincidencias parciales; `Search` por apellido, matrícula y especialidad sin distinguir mayúsculas; `%` en `search` se trata como literal; orden por apellido; `UserId` del usuario vinculado o null; `PageSize = 0` o `101` → `BadRequestException` |
| `tests/Dispensario.Application.UnitTests/Doctors/GetDoctorByIdHandlerTests.cs` | Devuelve el `DoctorDto` con `UserId` y `CreatedAtUtc`; inexistente → `NotFoundException` |
| `tests/Dispensario.Application.UnitTests/Users/LinkDoctorHandlerTests.cs` | Vincula un usuario `Doctor` con un doctor activo; usuario o doctor inexistentes → `NotFoundException`; doctor inactivo → `BadRequestException`; doctor ya vinculado a otro usuario → `ConflictException`; usuario `Receptionist` → `DomainException`; usuario ya vinculado → `DomainException` |
| `tests/Dispensario.Api.IntegrationTests/Doctors/DoctorsEndpointsTests.cs` | Como Admin: alta → 201 con `Location` y `GET /api/doctors/{id}` lo devuelve con `appointmentDurationMinutes = 30` y `userId = null`; matrícula repetida → 409; duración 32 → 400 con errores de validación; `GET /api/doctors?specialty=…` lo lista; `PUT` → 204; desactivar → 204 y `?isActive=false` lo lista; activar → 204; `GET` de un ID inexistente → 404. Como `Receptionist` y `Doctor`: `GET` → 200 y los de escritura → 403. Sin token → 401 |
| `tests/Dispensario.Api.IntegrationTests/Users/LinkDoctorEndpointTests.cs` | Como Admin: vincular un usuario `Doctor` → 204; `GET /api/users/{id}` y `GET /api/doctors/{id}` muestran el vínculo; tras un nuevo login, `GET /api/auth/me` devuelve el `doctorId`. Doctor ya vinculado a otro → 409; usuario `Receptionist` → 400; doctor inactivo → 400; doctor inexistente → 404. Como `Receptionist` → 403 |

- Convenciones de `TESTING.md`: `Method_Scenario_ExpectedResult`, Arrange / Act / Assert y Shouldly.

## Implementation plan

1. Crear `Doctor` en `Dispensario.Domain/Doctors/` y actualizar el comentario de `DoctorId.cs`, con `DoctorTests`. Verificar: `dotnet test` en verde.
2. Crear `DoctorConfiguration`, añadir `DbSet<Doctor>` a `IApplicationDbContext` y `DispensarioDbContext`, y el índice único de `Users.DoctorId` en `UserConfiguration`. Crear `TestDoctors` y `DoctorPersistenceTests`. Verificar: `dotnet test` en verde y, tras borrar `dispensario.db`, `dotnet run` arranca.
3. Crear `CreateDoctor` (command, validador, handler y endpoint), con `CreateDoctorCommandValidatorTests`, `CreateDoctorHandlerTests` y los casos de alta de `DoctorsEndpointsTests`. Verificar: `dotnet test` en verde y alta desde Scalar.
4. Crear `DoctorDto`, `DoctorProjections`, `GetDoctorById` y `GetDoctors` (queries, handlers y endpoints), con `GetDoctorByIdHandlerTests`, `GetDoctorsHandlerTests` y sus casos de integración. Verificar: `dotnet test` en verde.
5. Crear `UpdateDoctor` (command, validador, handler y endpoint), con `UpdateDoctorHandlerTests`, los casos de `UpdateDoctorCommandValidator` y sus casos de integración. Verificar: `dotnet test` en verde.
6. Crear `DeactivateDoctor` y `ActivateDoctor` (commands, validadores, handlers y endpoints), con `DeactivateDoctorHandlerTests` y sus casos de integración, incluidos los 403 por rol. Verificar: `dotnet test` en verde.
7. Crear `LinkDoctor` en Users (command, validador, handler y `LinkDoctorEndpoint`), con `LinkDoctorHandlerTests` y `LinkDoctorEndpointTests`. Verificar: `dotnet test` en verde.
8. Actualizar la documentación:
   - `README.md` sección **Endpoints**: filas nuevas en la tabla general, apartado **Doctors** con cada ruta y `POST /api/users/{id}/link-doctor` en **Users**: acceso, ejemplo de petición y respuesta, y tabla de códigos de error.
   - `docs/guides/DOMAIN.md` §4 Doctors: `Email` obligatorio y `Phone?` opcional, normalización y máximo de `LicenseNumber`, máximo de `Specialty`, duración de 10 a 120 múltiplo de 5 con 30 por defecto en el alta, `Activate()`, sin eventos y `Schedules` desde la fila 10. §4 Users: `DoctorId` sigue opcional con rol `Doctor` y se asigna con `POST /api/users/{id}/link-doctor`. §5: carpeta `Doctors/` con `Doctor.cs`.
   - `docs/guides/APPLICATION.md` §2: estado de `IApplicationDbContext` (hoy `Users` y `Doctors`), tabla "Módulo Doctors" con los casos de uso y `LinkDoctorCommand` en la tabla de gestión de usuarios.
   - `docs/guides/INFRASTRUCTURE.md`: tabla `Doctors` e índice único de `Users.DoctorId` (sin FK).
   - `docs/guides/API.md` §1: `Endpoints/Doctors/` y `LinkDoctorEndpoint` en `Users/`.
   - `CLAUDE.md`: "Estado actual" con el CRUD de doctores (feature 09); "Detalles no obvios" con el vínculo por `/api/users/{id}/link-doctor`, el nuevo login necesario para el claim `doctor_id` y la duración por defecto de 30 minutos.
   - `docs/PLAN.md`: la fila 09 apunta a `specs/09-doctors-crud.md`.

## Acceptance criteria

- [ ] `dotnet build Dispensario.slnx` termina sin errores y sin warnings nuevos respecto a SPEC 08.
- [ ] `dotnet test Dispensario.slnx` termina con éxito e incluye los tests de este spec y los anteriores.
- [ ] `User.cs` no cambia; `UserConfiguration.cs` solo añade el índice único de `DoctorId` y cambia su comentario.
- [ ] Tras borrar `dispensario.db`, `dotnet run` crea la tabla `Doctors` con el índice único de `LicenseNumber` y el índice único de `Users.DoctorId`.
- [ ] `POST /api/doctors` como Admin devuelve 201 con `Location: /api/doctors/{id}`; sin `appointmentDurationMinutes` el doctor queda con 30.
- [ ] La matrícula se guarda recortada y en mayúsculas, y una matrícula ya usada (también con otras mayúsculas) devuelve 409 en el alta y en la edición.
- [ ] Una duración fuera de 10–120 o no múltiplo de 5 devuelve 400 con errores de validación en el alta y en la edición.
- [ ] Sin `email`, con un email inválido, sin matrícula o sin especialidad, el alta devuelve 400.
- [ ] `PUT /api/doctors/{id}` responde 204; sin `appointmentDurationMinutes` conserva la duración actual y un `phone` null borra el teléfono.
- [ ] `GET /api/doctors` devuelve `pageIndex`, `pageSize`, `count` y `data`, filtra por `search`, `specialty` e `isActive`, y responde 400 con `pageSize` fuera de 1–100.
- [ ] `GET /api/doctors/{id}` devuelve el doctor con `userId` (o `null`) y `createdAtUtc`, y 404 si no existe.
- [ ] `POST /api/doctors/{id}/deactivate` y `/activate` responden 204, y 400 si el doctor ya está en ese estado; el usuario vinculado no cambia.
- [ ] `POST /api/users/{id}/link-doctor` responde 204 y, tras un nuevo login, `GET /api/auth/me` devuelve el `doctorId`.
- [ ] Vincular un doctor ya vinculado a otro usuario devuelve 409; un usuario sin rol `Doctor`, un usuario ya vinculado o un doctor inactivo devuelven 400; un usuario o doctor inexistente devuelve 404.
- [ ] Los endpoints de escritura de doctores y el de vínculo responden 403 a `Receptionist` y `Doctor`; `GET /api/doctors` y `GET /api/doctors/{id}` responden 200 a cualquier rol; todos responden 401 sin token.
- [ ] Todos los errores salen como `application/problem+json`.
- [ ] Todas las rutas nuevas aparecen en `/openapi/v1.json` con su tag, nombre y resumen.
- [ ] La sección **Endpoints** del `README.md` documenta las siete rutas nuevas con acceso, petición, respuesta y códigos de error.
- [ ] `DOMAIN.md`, `APPLICATION.md`, `INFRASTRUCTURE.md`, `API.md` y `CLAUDE.md` describen el módulo Doctors y el vínculo, y `docs/PLAN.md` fila 09 apunta a `specs/09-doctors-crud.md`.

## Decisions

- **Yes:** el vínculo se hace con `POST /api/users/{id}/link-doctor` en el módulo Users. El handler lee `Doctors` y solo escribe en `Users` (`ARCHITECTURE.md`, regla 3). Decisión del usuario.
- **No:** `userId` opcional en `POST /api/doctors` con un domain event que vincule en Users. Los errores del vínculo aparecerían dentro del alta del doctor. Descartado por el usuario.
- **Yes:** `DoctorId` sigue opcional para los usuarios con rol `Doctor`. No cambia el alta de usuarios de SPEC 08; las features clínicas rechazarán al usuario sin vínculo. Decisión del usuario.
- **Yes:** el vínculo exige un doctor activo (400) y libre (409), además de las reglas de `User.LinkDoctor` (rol `Doctor` y sin vínculo previo, 400). Decisión del usuario.
- **No:** exigir un usuario activo para vincular. No lo pidió el usuario: un usuario inactivo no puede hacer login igualmente.
- **No:** `UnlinkDoctor`. Fuera de alcance; un error de vínculo se corrige en un spec futuro.
- **Yes:** índice único en `Users.DoctorId`, sin FK. Respalda el 409 y mantiene la relación entre módulos solo por ID.
- **Yes:** escritura de doctores `AdminOnly`; lecturas para cualquier usuario autenticado. Recepción y doctores necesitan leerlos para agendar. Decisión del usuario.
- **Yes:** `Deactivate` y `Activate`, simétrico con Users. Decisión del usuario.
- **Yes:** desactivar un doctor no toca a su usuario vinculado. Un handler no modifica otro módulo; el Admin desactiva el usuario aparte si hace falta. Decisión del usuario.
- **Yes:** `Email` obligatorio y no único; `Phone` opcional. El email del doctor es independiente del email de login. Decisión del usuario.
- **Yes:** matrícula con `Trim` y `ToUpperInvariant`, máximo 30, única y editable (409 si choca). Decisión del usuario.
- **Yes:** la normalización de la matrícula vive en el dominio; el handler comprueba la unicidad con el valor ya normalizado del aggregate.
- **Yes:** `Specialty` texto libre, máximo 100. Decisión del usuario.
- **Yes:** `AppointmentDurationMinutes` por doctor, entre 10 y 120 y múltiplo de 5, con 30 por defecto en el alta. Decisión del usuario.
- **No:** duración fija de 30 sin campo. Descartado por el usuario: algún especialista puede necesitar otra duración.
- **Yes:** en `PUT` la duración es opcional y, si falta, se conserva la actual. Decisión del usuario.
- **Yes:** `Update` se permite sobre doctores inactivos, igual que la edición de perfil de usuarios.
- **Yes:** `GetDoctors` paginado como `GetUsers`, con `search` (nombre, apellido, matrícula, especialidad), `specialty` (igualdad sin mayúsculas) e `isActive`. Decisión del usuario.
- **Yes:** `DoctorDto` incluye el `UserId` vinculado, leído de `Users` con una subconsulta. Sirve para ver qué doctores no tienen cuenta. Decisión del usuario.
- **Yes:** proyección LINQ manual (`DoctorProjections`) en lugar de Mapster, porque la subconsulta no se puede expresar en un `TypeAdapterConfig`.
- **No:** domain events en Doctors. Nadie reacciona hoy (`DOMAIN.md` §2 regla 7). Decisión del usuario.
- **No:** horario semanal en este spec. Va en la fila 10, que añade `WeeklySchedule` y su regla frente a la duración.

## Risks

| Risk | Mitigation |
| --- | --- |
| La subconsulta de `UserId` compara IDs tipados (`u.DoctorId == d.Id`, uno nullable) y puede no traducirse a SQL | El paso 4 lo comprueba con `GetDoctorsHandlerTests` sobre SQLite. Si falla, se compara por `Value` o con `EF.Property<Guid?>`; nunca en memoria. |
| La fila 10 introduce la regla "cada bloque dura al menos `AppointmentDurationMinutes`": cambiar la duración con un horario ya cargado podría romperla | Fuera de alcance aquí (aún no hay horario). La fila 10 decide si `Update` valida contra el horario. |
| Un usuario vinculado no tiene el claim `doctor_id` hasta su siguiente login | Documentado en `CLAUDE.md` y `README.md`; el token dura 60 minutos. |
| `LIKE` en SQLite solo ignora mayúsculas en ASCII: "Pediatría" y "PEDIATRÍA" no coinciden en `specialty` | Aceptado en v1, igual que en `GetUsers`. |
| Matrícula repetida en dos altas simultáneas | El índice único lanza `DbUpdateException` → 500. Muy improbable con un solo Admin; aceptado en v1. |
| El esquema cambia y una `dispensario.db` local antigua no tiene la tabla `Doctors` | Borrar `dispensario.db` antes de `dotnet run` (`EnsureCreated` hasta la fila 17). |
| Un doctor desactivado conserva su usuario activo y vinculado | Decisión del usuario. Las features de agenda y consulta comprobarán que el doctor está activo. |

## What is **not** in this spec

- Horario semanal y disponibilidad (fila 10).
- Desvincular o cambiar el vínculo, y vincular al crear el doctor o el usuario.
- `DoctorId` obligatorio para los usuarios `Doctor`.
- Edición de la propia ficha por el doctor.
- Domain events en Doctors.
- Efectos de desactivar un doctor sobre su usuario o sus citas.
- Catálogo de especialidades.
- Borrado de doctores y migraciones.

Cada uno de ellos, si llega, va en su propio spec.
