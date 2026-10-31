# SPEC 12 — Appointments: agendar

> **Status:** Implemented
> **Depends on:** SPEC 07, SPEC 09, SPEC 10, SPEC 11
> **Date:** 2026-10-27
> **Objective:** Crear el aggregate `Appointment` y exponer el agendamiento de citas en hora local del dispensario, validando horario, alineación a slots y cruces, junto con su listado filtrado y su detalle.

## Por qué este spec existe

Es la primera feature de la Fase 3: las consultas (fila 15) y el ciclo de vida de la cita (fila 13) parten de una cita agendada.
Cierra la pregunta abierta de la zona horaria: la Api recibe y devuelve horas locales del dispensario (`Clinic:TimeZone`) y la base de datos guarda UTC.
La conversión vive en un servicio de Application (`IClinicTimeZone`) que reutilizará el cálculo de slots disponibles (fila 14).
Las reglas que dependen de otros aggregates (paciente y doctor activos, horario, cruces) se validan en el handler, como ya prevé `APPLICATION.md` §6.

## Scope

**In:**

- Aggregate `Appointment`, ID tipado `AppointmentId` y enum `AppointmentStatus` completo (`Scheduled`, `Confirmed`, `Completed`, `Cancelled`, `NoShow`) en `Dispensario.Domain/Appointments/`, con sus tests de dominio.
- Persistencia: `AppointmentConfiguration` (tabla `Appointments` con índices (`DoctorId`, `StartUtc`) y (`PatientId`, `StartUtc`)) y `DbSet<Appointment>` en `IApplicationDbContext` y `DispensarioDbContext`.
- Zona horaria del dispensario: `ClinicOptions` (`Clinic:TimeZone`), `IClinicTimeZone` y su implementación `ClinicTimeZone`, registrados en `AddApplication` con validación al arrancar; sección `Clinic` en `appsettings.json`.
- Command `ScheduleAppointment` (validador y handler) en `Dispensario.Application/Appointments/Commands/`.
- Queries `GetAppointments` (paginada y filtrada) y `GetAppointmentById` en `Dispensario.Application/Appointments/Queries/`.
- `AppointmentDto` con nombres de doctor y paciente, y horas en UTC y en local.
- Endpoints `POST /api/appointments` (`Reception`), `GET /api/appointments` y `GET /api/appointments/{id}` (cualquier usuario autenticado; un `Doctor` solo ve sus citas).
- Tests unitarios de dominio, persistencia, zona horaria, validador y handlers, y tests de integración de los endpoints.
- Documentación: sección **Endpoints** del `README.md`, `DOMAIN.md`, `APPLICATION.md`, `INFRASTRUCTURE.md`, `API.md`, `CLAUDE.md` y `docs/PLAN.md`.

**Out of scope (for future specs):**

- Transiciones de estado: `Confirm`, `Reschedule`, `Cancel`, `MarkNoShow` y `Complete` (fila 13). El enum existe, pero este spec solo crea citas `Scheduled`.
- `CancellationReason` y su columna (fila 13).
- Cálculo de slots disponibles (`GET /api/doctors/{id}/available-slots`, fila 14).
- Domain events de Appointments (`AppointmentScheduledDomainEvent`…): no hay consumidores.
- Que un `Doctor` agende citas (incluso en su propia agenda).
- Horizonte máximo para agendar (por ejemplo, no más de 90 días vista).
- Bloqueos de agenda (vacaciones, feriados) y excepciones por fecha.
- Usar la fecha local del dispensario en `Patient.Create`/`Update` (hoy usan la fecha UTC; spec 11).
- Exclusión estricta de cruces bajo concurrencia (dos altas simultáneas).
- Migraciones: siguen con `EnsureCreated` hasta la fila 17.

## Data model

### Aggregate `Appointment` (`Dispensario.Domain/Appointments/`)

```
Appointments/
  Appointment.cs  AppointmentId.cs  AppointmentStatus.cs
```

```csharp
public readonly record struct AppointmentId(Guid Value) : IStronglyTypedId
{
    public static AppointmentId New() => new(Guid.CreateVersion7());
}

public enum AppointmentStatus { Scheduled, Confirmed, Completed, Cancelled, NoShow }

public sealed class Appointment : AggregateRoot<AppointmentId>
{
    public const int ReasonMaxLength = 500;

    public PatientId PatientId { get; private set; }
    public DoctorId DoctorId { get; private set; }
    public DateTime StartUtc { get; private set; }
    public DateTime EndUtc { get; private set; }
    public string Reason { get; private set; }              // Trim; obligatorio
    public AppointmentStatus Status { get; private set; }   // Scheduled al crear

    public static Appointment Create(PatientId patientId, DoctorId doctorId,
        DateTime startUtc, DateTime endUtc, string reason, DateTime nowUtc);
}
```

Reglas de `Create` (`DomainException` → 400), en este orden:

| Regla | Mensaje |
| --- | --- |
| `startUtc >= endUtc` | `Appointment start must be before its end.` |
| `startUtc <= nowUtc` | `Appointment cannot be scheduled in the past.` |
| `reason` en blanco tras el `Trim` | `Reason is required.` |
| `reason` de más de 500 caracteres tras el `Trim` | `Reason must be at most 500 characters.` |

- `nowUtc` lo pasa el handler con `TimeProvider`: el dominio no conoce la hora actual (`DOMAIN.md` §2 regla 8), igual que `today` en `Patient`.
- `Create` deja la cita en `Scheduled`.
- Solo se relaciona con `Patient` y `Doctor` por ID tipado: sin navegaciones.
- Sin domain events.

### Persistencia (`Dispensario.Infrastructure/Data/Configurations/Appointments/AppointmentConfiguration.cs`, nuevo)

Tabla `Appointments`:

| Columna | Origen | Notas |
| --- | --- | --- |
| `Id` | `AppointmentId` | conversión por convención |
| `PatientId` | `PatientId` | conversión por convención, requerida, **sin FK** |
| `DoctorId` | `DoctorId` | conversión por convención, requerida, **sin FK** |
| `StartUtc`, `EndUtc` | `DateTime` | requeridas; se leen con `Kind = Utc` por la convención existente |
| `Reason` | `string` | `Appointment.ReasonMaxLength`, requerida |
| `Status` | `HasConversion<string>()` | requerida |
| auditoría | `IAuditable` | como en el resto de tablas |

- Índices no únicos: (`DoctorId`, `StartUtc`) y (`PatientId`, `StartUtc`), como en `INFRASTRUCTURE.md`.
- Sin FK hacia `Patients` ni `Doctors`: los módulos se relacionan solo por ID, igual que `Users.DoctorId`.
- Cambios en código existente: `IApplicationDbContext` y `DispensarioDbContext` añaden `DbSet<Appointment> Appointments`.
- El esquema cambia: hay que **borrar `dispensario.db`** local antes de `dotnet run`.

### Zona horaria del dispensario (`Dispensario.Application`)

```
Abstractions/IClinicTimeZone.cs
Common/ClinicOptions.cs  Common/ClinicTimeZone.cs
```

```csharp
public sealed class ClinicOptions
{
    public const string SectionName = "Clinic";
    public string TimeZone { get; init; } = "America/Guayaquil";   // ID IANA
}

public interface IClinicTimeZone
{
    DateTime ToUtc(DateTime local);    // local sin Kind (Unspecified) → UTC (Kind = Utc)
    DateTime ToLocal(DateTime utc);    // UTC → local del dispensario (Kind = Unspecified)
    DateOnly Today { get; }            // fecha local de hoy según TimeProvider
}
```

- `ClinicTimeZone` recibe `IOptions<ClinicOptions>` y `TimeProvider`, y resuelve la zona con `TimeZoneInfo.FindSystemTimeZoneById`.
- `ToUtc` con una hora local inexistente (salto de horario de verano) → `BadRequestException("Start time does not exist in the clinic time zone.")`. Una hora ambigua se resuelve como hora estándar (comportamiento de `TimeZoneInfo`).
- Registro en `AddApplication`: `AddOptions<ClinicOptions>().Bind(configuration.GetSection("Clinic"))`, `.Validate(...)` con `TimeZoneInfo.TryFindSystemTimeZoneById` (mensaje `Clinic:TimeZone is not a valid time zone.`) y `.ValidateOnStart()`. `IClinicTimeZone` → `ClinicTimeZone` como singleton.
- `Dispensario.Application.csproj` añade la referencia a `Microsoft.Extensions.Options.ConfigurationExtensions` (versión central 10.x en `Directory.Packages.props`) para `Bind`.
- `appsettings.json` añade `"Clinic": { "TimeZone": "America/Guayaquil" }`.
- `America/Guayaquil` es UTC−5 todo el año, sin horario de verano.

### `Dispensario.Application/Appointments/` (namespaces iguales a las carpetas)

```
Appointments/
  Commands/
    ScheduleAppointment/   ScheduleAppointmentCommand.cs  ScheduleAppointmentHandler.cs
  Queries/
    GetAppointments/       GetAppointmentsQuery.cs  GetAppointmentsHandler.cs
    GetAppointmentById/    GetAppointmentByIdQuery.cs  GetAppointmentByIdHandler.cs
  Dtos/                    AppointmentDto.cs  AppointmentProjections.cs
```

```csharp
public sealed record ScheduleAppointmentCommand(
    Guid PatientId, Guid DoctorId,
    DateTime StartLocal,          // hora local del dispensario, sin offset
    string Reason) : ICommand<ScheduleAppointmentResult>;
public sealed record ScheduleAppointmentResult(Guid Id);

public sealed record GetAppointmentsQuery(
    PaginationRequest Pagination,
    Guid? DoctorId, Guid? PatientId, AppointmentStatus? Status,
    DateOnly? From, DateOnly? To) : IQuery<PaginatedResult<AppointmentDto>>;   // fechas locales, inclusive
public sealed record GetAppointmentByIdQuery(Guid AppointmentId) : IQuery<AppointmentDto>;

public sealed record AppointmentDto(
    Guid Id,
    Guid PatientId, string PatientFullName,
    Guid DoctorId, string DoctorFullName,
    DateTime StartUtc, DateTime EndUtc,
    DateTime StartLocal, DateTime EndLocal,
    string Reason, AppointmentStatus Status, DateTime CreatedAtUtc);
```

- `AppointmentProjections.ToDto(this IQueryable<Appointment>, IQueryable<Patient>, IQueryable<Doctor>)`: proyección manual con subconsultas sobre `Patients` y `Doctors` para los nombres (`"FirstName LastName"`), sin Mapster, igual que `DoctorProjections`. `StartLocal` y `EndLocal` salen vacíos de la proyección y los rellena el handler en memoria con `IClinicTimeZone.ToLocal`, tras leer la página (máximo 100 filas).
- `StartLocal`/`EndLocal` se serializan sin offset (`"2026-11-03T09:30:00"`); `StartUtc`/`EndUtc`, con `Z`.

### Validador (`ScheduleAppointmentCommandValidator`, en el archivo del command)

| Campo | Reglas |
| --- | --- |
| `PatientId`, `DoctorId` | `NotEmpty` |
| `StartLocal` | `NotEmpty`; `Kind == DateTimeKind.Unspecified` con el mensaje `Start time must be a local time without offset.` |
| `Reason` | `NotEmpty`, `MaximumLength(Appointment.ReasonMaxLength)` |

- System.Text.Json deja `Kind = Unspecified` solo cuando el texto no trae `Z` ni offset: así se detecta un `startLocal` con offset.
- El pasado, la longitud tras el `Trim`, el horario y la alineación no son del validador.

### Handlers

| Handler | Comportamiento, en orden |
| --- | --- |
| `ScheduleAppointmentHandler` | Recibe `IApplicationDbContext`, `IClinicTimeZone` y `TimeProvider`. 1) Paciente no existe → `NotFoundException(nameof(Patient), id)`; inactivo → `BadRequestException("Patient is inactive.")`. 2) Doctor no existe → `NotFoundException(nameof(Doctor), id)`; inactivo → `BadRequestException("Doctor is inactive.")`. 3) `startUtc = ToUtc(StartLocal)`, `endUtc = startUtc + AppointmentDurationMinutes`, `localEnd = StartLocal + AppointmentDurationMinutes`. 4) `Appointment.Create(..., nowUtc)` (pasado y motivo → 400 de dominio). 5) `doctor.IsAvailableAt(StartLocal, localEnd)` es `false` → `BadRequestException("The doctor is not available at that time.")`. 6) El bloque del horario que contiene la cita cumple `(inicio local − bloque.StartTime)` múltiplo de `AppointmentDurationMinutes` (en minutos exactos, sin segundos); si no → `BadRequestException("Start time must align with the doctor's appointment slots.")`. 7) Otra cita del doctor en `Scheduled` o `Confirmed` con `StartUtc < endUtc && startUtc < EndUtc` → `ConflictException("The doctor already has an appointment at that time.")`. 8) Lo mismo para el paciente → `ConflictException("The patient already has an appointment at that time.")`. 9) `Add` y un único `SaveChangesAsync`; devuelve el ID |
| `GetAppointmentsHandler` | Recibe `IApplicationDbContext`, `ICurrentUser` e `IClinicTimeZone`. 1) Si el usuario es `Doctor`: sin `DoctorId` → `ForbiddenException("Your user is not linked to a doctor.")`; `DoctorId` del filtro distinto del suyo → `ForbiddenException("You can only view your own appointments.")`; sin filtro, se filtra por el suyo. 2) `PageIndex < 0` o `PageSize` fuera de 1–100 → `BadRequestException`. 3) `From > To` → `BadRequestException("From must be on or before To.")`. 4) `AsNoTracking`; filtros opcionales `DoctorId`, `PatientId`, `Status`, `From` (`StartUtc >= ToUtc(From 00:00)`) y `To` (`StartUtc < ToUtc(To + 1 día 00:00)`). 5) Orden por `StartUtc` y luego por `Id`. 6) `ToDto`, página, horas locales en memoria y `PaginatedResult<AppointmentDto>` |
| `GetAppointmentByIdHandler` | Recibe `IApplicationDbContext`, `ICurrentUser` e `IClinicTimeZone`. 1) Usuario `Doctor` sin `DoctorId` → `ForbiddenException("Your user is not linked to a doctor.")`. 2) `AsNoTracking`; no existe → `NotFoundException(nameof(Appointment), id)`. 3) Usuario `Doctor` y la cita es de otro doctor → `ForbiddenException("You can only view your own appointments.")`. 4) `AppointmentDto` con horas locales |

- Las comprobaciones 1 y 2 del agendamiento devuelven 404/400 antes de mirar el horario: el mensaje dice exactamente qué falla.
- Las citas `Cancelled`, `NoShow` y `Completed` no cuentan como cruce. Hoy solo existen citas `Scheduled`, pero la regla ya filtra por estado para la fila 13.
- Una cita contigua a otra (termina 09:30 y la siguiente empieza 09:30) no es cruce.
- En el detalle el 404 va antes del 403, porque hace falta leer la cita para saber de qué doctor es. En el listado, el 403 va antes de leer la BD.
- Las queries no pasan por `ValidationBehavior`: `GetAppointmentsHandler` valida a mano.

### `Dispensario.Api/Endpoints/Appointments/`

```
Appointments/
  ScheduleAppointmentEndpoint.cs  GetAppointmentsEndpoint.cs  GetAppointmentByIdEndpoint.cs
```

| Ruta | Request → Response | Autorización | Respuestas |
| --- | --- | --- | --- |
| `POST /api/appointments` | `ScheduleAppointmentRequest(Guid PatientId, Guid DoctorId, DateTime StartLocal, string Reason)` → `ScheduleAppointmentResponse(Guid Id)` | `Reception` | 201 (`Location: /api/appointments/{id}`), 400, 401, 403, 404, 409 |
| `GET /api/appointments` | `[AsParameters] GetAppointmentsRequest(int PageIndex = 0, int PageSize = 10, Guid? DoctorId, Guid? PatientId, AppointmentStatus? Status, DateOnly? From, DateOnly? To)` → `PaginatedResult<AppointmentResponse>` | `RequireAuthorization()` + filtro por doctor en el handler | 200, 400, 401, 403 |
| `GET /api/appointments/{id:guid}` | — → `AppointmentResponse` (campos de `AppointmentDto`) | `RequireAuthorization()` + comprobación de dueño en el handler | 200, 401, 403, 404 |

Ejemplo de cuerpo del alta:

```json
{
  "patientId": "0193f1c2-…",
  "doctorId": "0193f1b8-…",
  "startLocal": "2026-11-03T09:30",
  "reason": "Control de presión arterial"
}
```

Ejemplo de respuesta del detalle:

```json
{
  "id": "0193f1d0-…",
  "patientId": "0193f1c2-…",
  "patientFullName": "Ana Pérez",
  "doctorId": "0193f1b8-…",
  "doctorFullName": "Luis Andrade",
  "startUtc": "2026-11-03T14:30:00Z",
  "endUtc": "2026-11-03T15:00:00Z",
  "startLocal": "2026-11-03T09:30:00",
  "endLocal": "2026-11-03T10:00:00",
  "reason": "Control de presión arterial",
  "status": "Scheduled",
  "createdAtUtc": "2026-10-27T15:02:11Z"
}
```

- Un `startLocal` mal formado o un `status` desconocido falla al leer la petición: 400 de `BadHttpRequestException`, sin `ValidationErrors` (spec 10).
- Todos con `WithTags("Appointments")`, `WithName` igual al command o query (`ScheduleAppointment`, `GetAppointments`, `GetAppointmentById`) y `WithSummary`.

### Tests

| Archivo | Qué prueba |
| --- | --- |
| `tests/Dispensario.Domain.UnitTests/Appointments/AppointmentTests.cs` | `Create` deja la cita `Scheduled` con los IDs, las fechas y el motivo recortado; inicio igual o posterior al fin, inicio igual o anterior a `nowUtc`, motivo en blanco y motivo de 501 caracteres → `DomainException`; 500 caracteres pasa. Sin domain events |
| `tests/Dispensario.Application.UnitTests/Common/TestAppointments.cs` | Builder de citas de prueba (como `TestDoctors`) |
| `tests/Dispensario.Application.UnitTests/Persistence/Appointments/AppointmentPersistenceTests.cs` | Ida y vuelta de una `Appointment` con `Status` como texto, fechas con `Kind = Utc` y auditoría |
| `tests/Dispensario.Application.UnitTests/Common/ClinicTimeZoneTests.cs` | Con `America/Guayaquil`: `09:30` local → `14:30` UTC y vuelta; `Today` según un `FakeTimeProvider` cerca de medianoche (04:00 UTC → día anterior local); con `America/New_York`, una hora del salto de marzo → `BadRequestException`; la validación de `ClinicOptions` rechaza una zona inexistente |
| `tests/Dispensario.Application.UnitTests/Appointments/ScheduleAppointmentCommandValidatorTests.cs` | IDs vacíos, `StartLocal` por defecto, `StartLocal` con `Kind = Utc`, motivo vacío y de 501 caracteres fallan; un comando correcto pasa |
| `tests/Dispensario.Application.UnitTests/Appointments/ScheduleAppointmentHandlerTests.cs` | Agenda dentro de un bloque y guarda `StartUtc`/`EndUtc` en UTC con la duración del doctor; paciente o doctor inexistente → `NotFoundException`; paciente o doctor inactivo → `BadRequestException`; fuera del horario, cita que sobresale del bloque e inicio desalineado (`09:10` en bloque `09:00` con 30 min) → `BadRequestException`; bloque que empieza en `09:10` acepta `09:40`; en el pasado → `DomainException`; cruce parcial con una cita del doctor → `ConflictException`; cruce con una cita del paciente con otro doctor → `ConflictException`; una cita contigua y una cita `Cancelled` en el mismo horario no son cruce |
| `tests/Dispensario.Application.UnitTests/Appointments/GetAppointmentsHandlerTests.cs` | Paginación; filtros `DoctorId`, `PatientId`, `Status` y rango `From`/`To` en fechas locales (una cita a las 20:00 locales cae en su día local aunque en UTC sea el día siguiente); orden por `StartUtc`; nombres de doctor y paciente; `StartLocal`; `From > To` y `PageSize = 101` → `BadRequestException`; como `Doctor` sin filtro solo ve las suyas, con otro `DoctorId` o sin vincular → `ForbiddenException` |
| `tests/Dispensario.Application.UnitTests/Appointments/GetAppointmentByIdHandlerTests.cs` | Devuelve el `AppointmentDto` completo; inexistente → `NotFoundException`; como `Doctor` la suya pasa y la de otro → `ForbiddenException`; `Doctor` sin vincular → `ForbiddenException` |
| `tests/Dispensario.Api.IntegrationTests/Common/AppointmentRequests.cs` | Helpers: fija un horario de lunes a domingo `08:00–18:00` a un doctor, calcula una fecha local futura (mañana o después en `Clinic:TimeZone`) y agenda una cita devolviendo su ID |
| `tests/Dispensario.Api.IntegrationTests/Appointments/AppointmentsEndpointsTests.cs` | Como `Receptionist`: alta → 201 con `Location` y `GET /api/appointments/{id}` la devuelve con `status = "Scheduled"`, nombres y `startLocal` igual al enviado; mismo horario con el mismo doctor → 409; con el mismo paciente y otro doctor → 409; fuera del horario → 400; desalineada → 400; `startLocal` con `Z` → 400 con errores de validación; en el pasado → 400; paciente inexistente → 404; doctor inactivo → 400; `GET /api/appointments?doctorId=…&from=…&to=…` la lista. Como Admin: alta → 201. Como `Doctor` vinculado: alta → 403; listado sin filtro solo devuelve las suyas; `doctorId` ajeno → 403; detalle ajeno → 403. Sin token → 401 |

- Los tests de integración usan el reloj real: las fechas se calculan siempre hacia el futuro con el helper.
- Convenciones de `TESTING.md`: `Method_Scenario_ExpectedResult`, Arrange / Act / Assert y Shouldly.

## Implementation plan

1. Crear `AppointmentId`, `AppointmentStatus` y `Appointment` en `Dispensario.Domain/Appointments/`, con `AppointmentTests`. Verificar: `dotnet test` en verde.
2. Crear `AppointmentConfiguration` y añadir `DbSet<Appointment>` a `IApplicationDbContext` y `DispensarioDbContext`. Crear `TestAppointments` y `AppointmentPersistenceTests`. Verificar: `dotnet test` en verde y, tras borrar `dispensario.db`, `dotnet run` crea la tabla `Appointments` con sus dos índices.
3. Crear `ClinicOptions`, `IClinicTimeZone` y `ClinicTimeZone`, registrarlos en `AddApplication` con `ValidateOnStart`, añadir el paquete de configuración de opciones y la sección `Clinic` a `appsettings.json`. Crear `ClinicTimeZoneTests`. Verificar: `dotnet test` en verde y `dotnet run` arranca; con `Clinic:TimeZone` inválida no arranca.
4. Crear `ScheduleAppointment` (command, validador, handler y endpoint), con `ScheduleAppointmentCommandValidatorTests`, `ScheduleAppointmentHandlerTests`, el helper `AppointmentRequests` y los casos de alta de `AppointmentsEndpointsTests`. Verificar: `dotnet test` en verde y alta desde Scalar.
5. Crear `AppointmentDto`, `AppointmentProjections` y `GetAppointmentById` (query, handler y endpoint), con `GetAppointmentByIdHandlerTests` y sus casos de integración. Verificar: `dotnet test` en verde.
6. Crear `GetAppointments` (query, handler y endpoint), con `GetAppointmentsHandlerTests` y sus casos de integración, incluidos los 403 del rol `Doctor`. Verificar: `dotnet test` en verde.
7. Actualizar la documentación:
   - `README.md` sección **Endpoints**: filas nuevas en la tabla general y apartado **Appointments** con las tres rutas: acceso, ejemplo de petición y respuesta, y tabla de códigos de error. Indicar que `startLocal` es hora local del dispensario sin offset y la regla de alineación.
   - `docs/guides/DOMAIN.md` §4 Appointments: `Create(patientId, doctorId, startUtc, endUtc, reason, nowUtc)` con sus reglas, `ReasonMaxLength`, enum completo con solo `Scheduled` en uso, `CancellationReason` diferido a la fila 13 y sin eventos en este spec. §5: carpeta `Appointments/`.
   - `docs/guides/APPLICATION.md`: `IApplicationDbContext` con `Appointments`, `IClinicTimeZone`/`ClinicOptions`, tabla "Módulo Appointments" con los casos de uso, y §6 `ScheduleAppointment` con el orden real de comprobaciones, la alineación desde el inicio del bloque y los mensajes.
   - `docs/guides/INFRASTRUCTURE.md`: tabla `Appointments`, sus índices y la ausencia de FKs entre módulos.
   - `docs/guides/API.md` §1: `Endpoints/Appointments/` con sus endpoints; §8: la sección `Clinic` llega con la fila 12 (no la 14).
   - `CLAUDE.md`: "Estado actual" con el agendamiento de citas (feature 12); "Detalles no obvios" con `startLocal` sin offset y `Clinic:TimeZone` validada al arrancar, la alineación desde el inicio del bloque, los cruces solo con `Scheduled`/`Confirmed`, la vista restringida del `Doctor` y que las horas locales se calculan en memoria.
   - `docs/PLAN.md`: la fila 12 apunta a `specs/12-appointments-scheduling.md` y se cierra la pregunta abierta de la zona horaria.

## Acceptance criteria

- [x] `dotnet build Dispensario.slnx` termina sin errores y sin warnings nuevos respecto a SPEC 11.
- [x] `dotnet test Dispensario.slnx` termina con éxito e incluye los tests de este spec y los anteriores.
- [x] Tras borrar `dispensario.db`, `dotnet run` crea la tabla `Appointments` con los índices (`DoctorId`, `StartUtc`) y (`PatientId`, `StartUtc`) y sin FKs.
- [x] Con `Clinic:TimeZone` inexistente la aplicación no arranca; con `America/Guayaquil` arranca.
- [x] `POST /api/appointments` como `Receptionist` o Admin devuelve 201 con `Location: /api/appointments/{id}`, y la cita queda `Scheduled` con `startUtc` = `startLocal` + 5 horas y `endUtc` = `startUtc` + la duración del doctor.
- [x] Una cita fuera del horario del doctor, que sobresale de su bloque o cuyo inicio no está alineado desde el inicio del bloque devuelve 400.
- [x] Una cita en el pasado, con motivo vacío o de más de 500 caracteres devuelve 400.
- [x] Un `startLocal` con `Z` u offset devuelve 400 con errores de validación.
- [x] Un paciente o doctor inexistente devuelve 404; inactivo, 400.
- [x] Un cruce con otra cita `Scheduled` del mismo doctor o del mismo paciente devuelve 409; una cita contigua se acepta.
- [x] `GET /api/appointments/{id}` devuelve la cita con `patientFullName`, `doctorFullName`, horas UTC y locales, `status` como texto y `createdAtUtc`; 404 si no existe.
- [x] `GET /api/appointments` devuelve `pageIndex`, `pageSize`, `count` y `data` ordenados por inicio; filtra por `doctorId`, `patientId`, `status` y `from`/`to` en fechas locales; responde 400 con `from > to` o `pageSize` fuera de 1–100.
- [x] Un `Doctor` vinculado recibe 403 en el alta; en el listado sin filtro solo ve sus citas; recibe 403 con un `doctorId` ajeno y en el detalle de una cita ajena; un `Doctor` sin vincular recibe 403 en ambas lecturas.
- [x] Las tres rutas responden 401 sin token.
- [x] Todos los errores salen como `application/problem+json`.
- [x] Las tres rutas nuevas aparecen en `/openapi/v1.json` con tag `Appointments`, nombre y resumen.
- [x] La sección **Endpoints** del `README.md` documenta las tres rutas nuevas con acceso, petición, respuesta y códigos de error.
- [x] `DOMAIN.md`, `APPLICATION.md`, `INFRASTRUCTURE.md`, `API.md` y `CLAUDE.md` describen el módulo Appointments y la zona horaria tal como se implementaron, y `docs/PLAN.md` fila 12 apunta a `specs/12-appointments-scheduling.md`.

## Decisions

- **Yes:** la petición manda `startLocal` en hora local del dispensario, sin offset; se guarda `StartUtc` y la respuesta devuelve UTC y local. Es lo natural para recepción y encaja con el horario. Decisión del usuario.
- **No:** recibir UTC con `Z` o un `DateTimeOffset`. Obliga al cliente a conocer la zona o permite offsets distintos al de la clínica. Descartado por el usuario.
- **Yes:** un `startLocal` con `Z` u offset da 400 en el validador. Decisión del usuario.
- **No:** ignorar el offset y tomar la hora tal cual. Escondería errores del cliente. Descartado por el usuario.
- **Yes:** `ClinicOptions` + `IClinicTimeZone` en Application, con validación de la zona al arrancar. Es un cálculo puro que reutilizará la fila 14. Decisión del usuario.
- **No:** implementación en Infrastructure o conversión inline en cada handler. Más capas o código duplicado. Descartado por el usuario.
- **Yes:** agenda `Reception` (Admin y Receptionist); leen todos los autenticados, pero un `Doctor` solo sus citas. Decisión del usuario.
- **No:** que el `Doctor` agende ni que lea citas ajenas. Descartado por el usuario.
- **Yes:** un `Doctor` que pide una cita ajena o filtra por otro doctor recibe 403, coherente con el horario (spec 10). Decisión del usuario.
- **No:** 404 en el detalle ajeno y filtro forzado en silencio en el listado. Descartado por el usuario.
- **Yes:** alineación desde el inicio del bloque: `(inicio − bloque.StartTime)` múltiplo de la duración. Es la misma regla que usará la fila 14. Decisión del usuario.
- **No:** alineación desde medianoche o sin alineación. Pierde tiempo del bloque o deja huecos inservibles. Descartado por el usuario.
- **Yes:** enum `AppointmentStatus` completo desde ya; solo se crean citas `Scheduled`. El filtro `status` y la regla de cruces no cambian en la fila 13. Decisión del usuario.
- **No:** `AppointmentScheduledDomainEvent` en este spec. Nadie reacciona hoy (`DOMAIN.md` §2 regla 7). Decisión del usuario.
- **Yes:** `GET /api/appointments` paginado con filtros `doctorId`, `patientId`, `status` y `from`/`to` (fechas locales inclusive), ordenado por inicio. Decisión del usuario.
- **No:** listado sin paginar con rango acotado o solo agenda diaria por doctor. Descartado por el usuario.
- **Yes:** `AppointmentDto` con `patientFullName` y `doctorFullName` por subconsultas en la proyección, sin navegaciones de EF. Decisión del usuario.
- **Yes:** las horas locales del DTO se calculan en memoria tras leer la página: `TimeZoneInfo` no se traduce a SQL.
- **Yes:** `Reason` obligatorio, recortado y de hasta 500 caracteres. Decisión del usuario.
- **Yes:** sin tope de horizonte; solo se exige que la cita sea futura. El dominio recibe `nowUtc` del handler. Decisión del usuario.
- **No:** máximo de 90 días vista. Descartado por el usuario.
- **Yes:** sin FK de `Appointments` hacia `Patients` y `Doctors`: los módulos solo se relacionan por ID (`ARCHITECTURE.md` §3, regla 1), como `Users.DoctorId`.
- **Yes:** `CancellationReason` se difiere a la fila 13, que es quien lo usa.
- **Yes:** paciente y doctor inactivos dan 400 (`BadRequestException`), inexistentes 404, cruces 409, como indica `APPLICATION.md` §6.
- **Yes:** el cruce del doctor se comprueba antes que el del paciente; cada uno con su mensaje.

## Risks

| Risk | Mitigation |
| --- | --- |
| Dos altas simultáneas en el mismo horario pasan la comprobación de cruces y se guardan las dos | Muy improbable con un solo dispensario. Aceptado en v1; la exclusión estricta queda fuera de alcance. |
| `TimeZoneInfo.FindSystemTimeZoneById("America/Guayaquil")` en Windows depende de ICU | .NET 6+ resuelve IDs IANA en Windows 10 1903+ con ICU. Si falla, la app no arranca (`ValidateOnStart`) y el mensaje lo indica. |
| Comparar `DateTime` en SQLite para los cruces y el rango de fechas | Se guardan como TEXT ISO 8601 en UTC con formato fijo, que ordena y compara bien como texto; los tests de handlers lo cubren con SQLite `:memory:`. |
| Un token emitido antes del vínculo no trae `doctor_id`: el doctor recibe 403 en las lecturas | Comportamiento del spec 09: hace falta un nuevo login. Documentado en `README.md`. |
| Los tests de integración usan el reloj real y podrían fallar cerca de medianoche | El helper agenda siempre a partir de mañana (fecha local) y a horas dentro del horario completo `08:00–18:00`. |
| Subir la duración de la cita de un doctor deja citas existentes desalineadas | No afecta a las ya agendadas (su `EndUtc` se guardó). Las nuevas usan la duración vigente. |
| El esquema cambia y una `dispensario.db` local antigua no tiene la tabla `Appointments` | Borrar `dispensario.db` antes de `dotnet run` (`EnsureCreated` hasta la fila 17). |

## What is **not** in this spec

- Confirmar, reprogramar, cancelar, marcar no asistida y completar citas, y `CancellationReason`.
- Slots disponibles de un doctor.
- Domain events de Appointments.
- Agendamiento por el propio `Doctor` y horizonte máximo.
- Bloqueos de agenda y feriados.
- Fecha local en `Patient` y exclusión estricta de cruces bajo concurrencia.
- Migraciones.

Cada uno de ellos, si llega, va en su propio spec.
