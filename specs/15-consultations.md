# SPEC 15 — Consultations

> **Status:** Implemented
> **Depends on:** SPEC 07, SPEC 08, SPEC 09, SPEC 11, SPEC 12, SPEC 13
> **Date:** 2026-10-27
> **Objective:** Crear el aggregate `Consultation` con sus casos de uso Start, Update y Close para el doctor dueño, y las lecturas por ID e historia del paciente para doctores y Admin.

## Por qué este spec existe

Hasta ahora el dispensario agenda citas, pero no registra la atención clínica.
La consulta guarda el motivo, el diagnóstico y las notas de una atención, con o sin cita previa.
Solo el doctor que atiende escribe su consulta; cualquier doctor vinculado y el Admin leen la historia del paciente, para dar continuidad clínica.
La recepción no ve datos clínicos.
Cerrar una consulta **no** completa la cita en este spec: el evento `ConsultationClosedDomainEvent`, `Appointment.Complete` y su handler llegan juntos con la fila 16.

## Scope

**In:**

- Aggregate `Consultation` (`ConsultationId`, `ConsultationStatus`) con `Start`, `Update` y `Close`, sus reglas y sus tests de dominio.
- Persistencia: `DbSet<Consultation>` en `IApplicationDbContext` y `DispensarioDbContext`, y `ConsultationConfiguration` con la tabla `Consultations` y sus índices.
- Commands `StartConsultation`, `UpdateConsultation` y `CloseConsultation` (command, validador y handler) en `Dispensario.Application/Consultations/Commands/`.
- Queries `GetConsultationById` y `GetPatientConsultations` en `Dispensario.Application/Consultations/Queries/`.
- `ConsultationDto` y `ConsultationProjections` en `Dispensario.Application/Consultations/Dtos/`.
- Endpoints `POST /api/consultations`, `PUT /api/consultations/{id}` y `POST /api/consultations/{id}/close` (política `Clinical`), y `GET /api/consultations/{id}` y `GET /api/patients/{id}/consultations` (política `AdminOrDoctor`).
- Tests unitarios de dominio, persistencia, validadores y handlers, y tests de integración de los endpoints.
- Documentación: sección **Endpoints** del `README.md`, `DOMAIN.md`, `APPLICATION.md`, `INFRASTRUCTURE.md`, `API.md`, `CLAUDE.md` y `docs/PLAN.md`.

**Out of scope (for future specs):**

- `ConsultationClosedDomainEvent`, `Appointment.Complete` y el handler que completa la cita al cerrar la consulta: fila 16.
- Reabrir una consulta cerrada o editarla tras el cierre.
- Que el Admin u otro rol inicie, edite o cierre consultas en nombre de un doctor.
- Que la recepción lea consultas.
- Listado general de consultas (`GET /api/consultations` con filtros) o "mis consultas" del doctor.
- Límite de consultas `Open` por doctor o por paciente.
- Qué pasa con una consulta `Open` si la recepción cancela o marca no asistida su cita después de iniciarla.
- Signos vitales, recetas, exámenes, códigos CIE-10 y adjuntos.
- Exclusión estricta de dos consultas simultáneas para la misma cita (la cubre el índice único, con 500).
- Migraciones: siguen con `EnsureCreated` hasta la fila 17.

## Data model

### Ciclo de vida

```
Start ──► Open ──Update──► Open
            │
            └──Close──► Closed   (final)
```

### Aggregate `Consultation` (`Dispensario.Domain/Consultations/`, nuevo)

```
Consultations/  Consultation.cs  ConsultationId.cs  ConsultationStatus.cs
```

```csharp
public readonly record struct ConsultationId(Guid Value) : IStronglyTypedId
{
    public static ConsultationId New() => new(Guid.CreateVersion7());
}

public enum ConsultationStatus { Open, Closed }

public sealed class Consultation : AggregateRoot<ConsultationId>
{
    public const int ReasonMaxLength = 500;
    public const int DiagnosisMaxLength = 1000;
    public const int NotesMaxLength = 4000;

    public PatientId PatientId { get; private set; }
    public DoctorId DoctorId { get; private set; }
    public AppointmentId? AppointmentId { get; private set; }   // null = consulta sin cita
    public string Reason { get; private set; }                  // obligatorio; Trim
    public string? Diagnosis { get; private set; }               // Trim; en blanco → null
    public string? Notes { get; private set; }                   // Trim; en blanco → null
    public ConsultationStatus Status { get; private set; }       // Open al iniciar
    public DateTime StartedAtUtc { get; private set; }
    public DateTime? ClosedAtUtc { get; private set; }           // solo en Closed

    public static Consultation Start(
        PatientId patientId, DoctorId doctorId, AppointmentId? appointmentId,
        string reason, DateTime startedAtUtc);

    public void Update(string reason, string? diagnosis, string? notes);
    public void Close(string diagnosis, string? notes, DateTime closedAtUtc);
}
```

Reglas (`DomainException` → 400), en este orden dentro de cada método:

| Método | Regla | Mensaje |
| --- | --- | --- |
| `Start` | `reason` en blanco tras el `Trim` | `Reason is required.` |
| `Start` | `reason` de más de 500 caracteres tras el `Trim` | `Reason must be at most 500 characters.` |
| `Update` | estado `Closed` | `Only open consultations can be updated.` |
| `Update` | `reason` en blanco / de más de 500 | los mismos mensajes que `Start` |
| `Update` | `diagnosis` de más de 1000 caracteres tras el `Trim` | `Diagnosis must be at most 1000 characters.` |
| `Update` | `notes` de más de 4000 caracteres tras el `Trim` | `Notes must be at most 4000 characters.` |
| `Close` | estado `Closed` | `Consultation is already closed.` |
| `Close` | `diagnosis` en blanco tras el `Trim` | `Diagnosis is required to close a consultation.` |
| `Close` | `diagnosis` de más de 1000 / `notes` de más de 4000 | los mismos mensajes que `Update` |
| `Close` | `closedAtUtc < StartedAtUtc` | `Consultation cannot be closed before it started.` |

- `startedAtUtc` y `closedAtUtc` los pasa el handler con `TimeProvider` (regla 8 de `DOMAIN.md`).
- `Start` crea la consulta `Open`, con `Diagnosis`, `Notes` y `ClosedAtUtc` en `null`.
- `Update` reemplaza los tres textos: un opcional `null` o en blanco borra el valor.
- `Close` reemplaza `Diagnosis` y `Notes` (un `notes` `null` lo borra), fija `ClosedAtUtc` y deja la consulta `Closed`.
- `Closed` es final: ninguna operación sale de él.
- Si una regla falla, la consulta no cambia.
- Las reglas que dependen de otros aggregates (doctor, paciente, cita) se validan en el handler.
- Sin domain events en este spec.

### Persistencia (`Dispensario.Infrastructure/Data/Configurations/Consultations/ConsultationConfiguration.cs`, nuevo)

`IApplicationDbContext` y `DispensarioDbContext` añaden `DbSet<Consultation> Consultations`.

| Columna | Origen | Notas |
| --- | --- | --- |
| `Id` | `ConsultationId` | Guid por convención |
| `PatientId` | `PatientId` | requerido, **sin FK** hacia `Patients` |
| `DoctorId` | `DoctorId` | requerido, **sin FK** hacia `Doctors` |
| `AppointmentId` | `AppointmentId?` | nullable, **sin FK** hacia `Appointments` |
| `Reason` | `string` | `Consultation.ReasonMaxLength`, requerido |
| `Diagnosis` | `string?` | `Consultation.DiagnosisMaxLength`, nullable |
| `Notes` | `string?` | `Consultation.NotesMaxLength`, nullable |
| `Status` | `ConsultationStatus` | nombre del enum como texto |
| `StartedAtUtc` | `DateTime` | UTC como TEXT ISO |
| `ClosedAtUtc` | `DateTime?` | UTC, nullable |
| auditoría | `IAuditable` | como el resto de tablas |

Índices:

| Índice | Tipo | Para qué |
| --- | --- | --- |
| `AppointmentId` | único | una sola consulta por cita; SQLite admite varios `NULL`, así que las consultas sin cita no chocan (como `Users.DoctorId`) |
| (`PatientId`, `StartedAtUtc`) | no único | historia del paciente ordenada por fecha |

- El esquema cambia: hay que **borrar `dispensario.db`** local antes de `dotnet run`.

### `Dispensario.Application/Consultations/` (namespaces iguales a las carpetas)

```
Consultations/
  Commands/
    StartConsultation/    StartConsultationCommand.cs   StartConsultationHandler.cs
    UpdateConsultation/   UpdateConsultationCommand.cs  UpdateConsultationHandler.cs
    CloseConsultation/    CloseConsultationCommand.cs   CloseConsultationHandler.cs
  Queries/
    GetConsultationById/      GetConsultationByIdQuery.cs      GetConsultationByIdHandler.cs
    GetPatientConsultations/  GetPatientConsultationsQuery.cs  GetPatientConsultationsHandler.cs
  Dtos/  ConsultationDto.cs  ConsultationProjections.cs
```

```csharp
public sealed record StartConsultationCommand(
    Guid PatientId, Guid? AppointmentId, string Reason) : ICommand<StartConsultationResult>;
public sealed record StartConsultationResult(Guid Id);

public sealed record UpdateConsultationCommand(
    Guid ConsultationId, string Reason, string? Diagnosis, string? Notes) : ICommand;

public sealed record CloseConsultationCommand(
    Guid ConsultationId, string Diagnosis, string? Notes) : ICommand;

public sealed record GetConsultationByIdQuery(Guid ConsultationId) : IQuery<ConsultationDto>;

public sealed record GetPatientConsultationsQuery(Guid PatientId, PaginationRequest Pagination)
    : IQuery<PaginatedResult<ConsultationDto>>;

// StartedAtLocal y ClosedAtLocal en hora local del dispensario (sin offset)
public sealed record ConsultationDto(
    Guid Id,
    Guid PatientId,
    string PatientFullName,
    Guid DoctorId,
    string DoctorFullName,
    Guid? AppointmentId,
    string Reason,
    string? Diagnosis,
    string? Notes,
    ConsultationStatus Status,
    DateTime StartedAtUtc,
    DateTime? ClosedAtUtc,
    DateTime StartedAtLocal,
    DateTime? ClosedAtLocal,
    DateTime CreatedAtUtc);
```

- `ConsultationProjections.ToDto(consultations, patients, doctors)` saca los nombres con subconsultas sobre `Patients` y `Doctors`, sin Mapster, como `AppointmentProjections`.
- `ToDto` deja `StartedAtLocal` y `ClosedAtLocal` vacías; `WithLocalTimes(clinicTimeZone)` las rellena en memoria tras leer (máximo 100 filas por página).
- El doctor de la consulta nunca llega en el cuerpo: es `ICurrentUser.DoctorId`.

### Validadores (en el archivo de cada command)

| Validador | Reglas |
| --- | --- |
| `StartConsultationCommandValidator` | `PatientId` `NotEmpty`; `AppointmentId` `NotEmpty` cuando no es `null`; `Reason` `NotEmpty` y `MaximumLength(Consultation.ReasonMaxLength)` |
| `UpdateConsultationCommandValidator` | `ConsultationId` `NotEmpty`; `Reason` `NotEmpty` y `MaximumLength(500)`; `Diagnosis` `MaximumLength(Consultation.DiagnosisMaxLength)`; `Notes` `MaximumLength(Consultation.NotesMaxLength)` |
| `CloseConsultationCommandValidator` | `ConsultationId` `NotEmpty`; `Diagnosis` `NotEmpty` y `MaximumLength(1000)`; `Notes` `MaximumLength(4000)` |

- Los máximos salen de las constantes de `Consultation`.

### Handlers

| Handler | Comportamiento, en orden |
| --- | --- |
| `StartConsultationHandler` | Recibe `IApplicationDbContext`, `ICurrentUser`, `IClinicTimeZone` y `TimeProvider`. 1) `ICurrentUser.DoctorId` es `null` → `ForbiddenException("Your user is not linked to a doctor.")`. 2) Su doctor no existe → `NotFoundException(nameof(Doctor), id)`; inactivo → `BadRequestException("Doctor is inactive.")`. 3) El paciente no existe → `NotFoundException(nameof(Patient), id)`; inactivo → `BadRequestException("Patient is inactive.")`. 4) Si llega `AppointmentId`: la cita no existe → `NotFoundException(nameof(Appointment), id)`; es de otro doctor → `ForbiddenException("You can only manage your own appointments.")`; es de otro paciente → `BadRequestException("Appointment does not belong to the patient.")`; su estado no es `Scheduled` ni `Confirmed` → `BadRequestException("Only scheduled or confirmed appointments can start a consultation.")`; su fecha local (`ToLocal(StartUtc)`) no es `IClinicTimeZone.Today` → `BadRequestException("Appointment is not scheduled for today.")`; ya hay una consulta con esa cita (`Open` o `Closed`) → `ConflictException("The appointment already has a consultation.")`. 5) `Consultation.Start(..., nowUtc)`. 6) Un único `SaveChangesAsync`; devuelve el ID |
| `UpdateConsultationHandler` | Recibe `IApplicationDbContext` y `ICurrentUser`. 1) Sin `DoctorId` → `ForbiddenException("Your user is not linked to a doctor.")`. 2) La consulta no existe → `NotFoundException(nameof(Consultation), id)`. 3) Es de otro doctor → `ForbiddenException("You can only manage your own consultations.")`. 4) `consultation.Update(reason, diagnosis, notes)`. 5) `SaveChangesAsync` |
| `CloseConsultationHandler` | Recibe `IApplicationDbContext`, `ICurrentUser` y `TimeProvider`. Igual que `UpdateConsultationHandler`, con `consultation.Close(diagnosis, notes, nowUtc)` en el paso 4 |
| `GetConsultationByIdHandler` | Recibe `IApplicationDbContext`, `ICurrentUser` y `IClinicTimeZone`. 1) Usuario `Doctor` sin `DoctorId` → `ForbiddenException("Your user is not linked to a doctor.")`. 2) `AsNoTracking` + `ToDto`; no existe → `NotFoundException(nameof(Consultation), id)`. 3) `WithLocalTimes` |
| `GetPatientConsultationsHandler` | Recibe `IApplicationDbContext`, `ICurrentUser` y `IClinicTimeZone`. 1) Usuario `Doctor` sin `DoctorId` → `ForbiddenException`. 2) Paginación como `GetUsers`: `PageIndex < 0` → `BadRequestException("PageIndex must be greater than or equal to 0.")`; `PageSize` fuera de 1–100 → `BadRequestException("PageSize must be between 1 and 100.")`. 3) El paciente no existe → `NotFoundException(nameof(Patient), id)` (uno inactivo sí se lee). 4) Todas las consultas del paciente (`Open` y `Closed`, de cualquier doctor), ordenadas por `StartedAtUtc` descendente y luego por `Id` descendente, paginadas. 5) `WithLocalTimes` sobre la página |

- Las políticas de los endpoints garantizan el rol: `Clinical` (solo `Doctor`) en los tres commands y `AdminOrDoctor` en las dos queries. Los handlers solo comprueban el vínculo y el dueño.
- En los commands el orden de 403/404 es el de las citas (spec 13): sin vincular, 403 antes de leer; ajena, 403 después del 404.
- En las queries cualquier `Doctor` vinculado lee cualquier consulta; el Admin no necesita vínculo.
- `Update` y `Close` leen la consulta con seguimiento; el resto de lecturas, con `AsNoTracking`.
- `Update` y `Close` no comprueban si el doctor o el paciente siguen activos: una consulta abierta se puede terminar aunque se desactiven después.
- Iniciar una consulta no cambia el estado de la cita: sigue `Scheduled` o `Confirmed` hasta la fila 16.
- Las queries no pasan por `ValidationBehavior`: la paginación se comprueba a mano, como en `GetUsers` y `GetAppointments`.

### `Dispensario.Api/Endpoints/Consultations/` (nuevo)

```
Consultations/
  StartConsultationEndpoint.cs   UpdateConsultationEndpoint.cs   CloseConsultationEndpoint.cs
  GetConsultationByIdEndpoint.cs GetPatientConsultationsEndpoint.cs
```

| Ruta | Request → Response | Autorización | Respuestas |
| --- | --- | --- | --- |
| `POST /api/consultations` | `StartConsultationRequest(Guid PatientId, Guid? AppointmentId, string Reason)` → `StartConsultationResponse(Guid Id)` con `Location: /api/consultations/{id}` | `Clinical` | 201, 400, 401, 403, 404, 409 |
| `PUT /api/consultations/{id:guid}` | `UpdateConsultationRequest(string Reason, string? Diagnosis, string? Notes)` | `Clinical` | 204, 400, 401, 403, 404 |
| `POST /api/consultations/{id:guid}/close` | `CloseConsultationRequest(string Diagnosis, string? Notes)` | `Clinical` | 204, 400, 401, 403, 404 |
| `GET /api/consultations/{id:guid}` | → `ConsultationResponse` | `AdminOrDoctor` | 200, 401, 403, 404 |
| `GET /api/patients/{id:guid}/consultations?pageIndex=&pageSize=` | `GetPatientConsultationsRequest(int PageIndex = 0, int PageSize = 10)` → `PaginatedResult<ConsultationResponse>` | `AdminOrDoctor` | 200, 400, 401, 403, 404 |

Ejemplos de cuerpo:

```json
{ "patientId": "0193f1b8-…", "appointmentId": "0193f2c4-…", "reason": "Dolor de cabeza desde hace tres días" }
```

```json
{ "reason": "Dolor de cabeza desde hace tres días", "diagnosis": "Cefalea tensional", "notes": "Sin signos de alarma." }
```

```json
{ "diagnosis": "Cefalea tensional", "notes": "Control en dos semanas." }
```

`ConsultationResponse` tiene los mismos campos que `ConsultationDto`, en el mismo orden; `status` viaja como `"Open"` o `"Closed"`.

- Todos con `WithTags("Consultations")`, `WithName` igual al command o query (`StartConsultation`, `UpdateConsultation`, `CloseConsultation`, `GetConsultationById`, `GetPatientConsultations`) y `WithSummary`.
- `GetPatientConsultationsEndpoint` vive en `Endpoints/Consultations/` aunque la ruta cuelgue de `/api/patients`: la query es del módulo Consultations (como los slots del spec 14).
- Las respuestas se mapean con `Adapt<...>()`.
- Un Receptionist recibe 403 en las cinco rutas (por política).

### Tests

| Archivo | Qué prueba |
| --- | --- |
| `tests/Dispensario.Domain.UnitTests/Consultations/ConsultationTests.cs` (nuevo) | `Start` crea una consulta `Open` con el motivo recortado, la cita (o `null`) y `StartedAtUtc`; motivo en blanco y de 501 → `DomainException`. `Update` reemplaza los textos y convierte opcionales en blanco a `null`; sobre una `Closed`, motivo inválido, diagnóstico de 1001 y notas de 4001 → `DomainException`. `Close` deja `Closed` con diagnóstico, notas y `ClosedAtUtc`; ya cerrada, diagnóstico en blanco y `closedAtUtc` anterior al inicio → `DomainException`. Una regla fallida no cambia la consulta. Sin domain events |
| `tests/Dispensario.Application.UnitTests/Persistence/Consultations/ConsultationPersistenceTests.cs` (nuevo) | Ida y vuelta de una consulta cerrada con cita y de una abierta sin cita; dos consultas sin cita se guardan (varios `NULL`); dos con la misma cita → `DbUpdateException` por el índice único |
| `tests/Dispensario.Application.UnitTests/Consultations/StartConsultationCommandValidatorTests.cs` | Paciente vacío, `AppointmentId` vacío (no `null`), motivo vacío y de 501 fallan; un comando correcto con y sin cita pasa |
| `tests/Dispensario.Application.UnitTests/Consultations/UpdateConsultationCommandValidatorTests.cs` | ID vacío, motivo vacío, diagnóstico de 1001 y notas de 4001 fallan; un comando correcto con opcionales `null` pasa |
| `tests/Dispensario.Application.UnitTests/Consultations/CloseConsultationCommandValidatorTests.cs` | ID vacío, diagnóstico vacío y de 1001, notas de 4001 fallan; un comando correcto pasa |
| `tests/Dispensario.Application.UnitTests/Consultations/StartConsultationHandlerTests.cs` | Con un `FakeTimeProvider`: inicia sin cita y con una cita de hoy (`Scheduled` y `Confirmed`) y persiste `Open`; la cita no cambia de estado; sin vincular → `ForbiddenException`; doctor inactivo → `BadRequestException`; paciente inexistente → `NotFoundException`, inactivo → `BadRequestException`; cita inexistente → `NotFoundException`; de otro doctor → `ForbiddenException`; de otro paciente, `Cancelled`, de ayer y de mañana → `BadRequestException`; con una consulta previa (abierta o cerrada) → `ConflictException` |
| `tests/Dispensario.Application.UnitTests/Consultations/UpdateConsultationHandlerTests.cs` | Actualiza y persiste; inexistente → `NotFoundException`; de otro doctor y sin vincular → `ForbiddenException`; cerrada → `DomainException` |
| `tests/Dispensario.Application.UnitTests/Consultations/CloseConsultationHandlerTests.cs` | Cierra y persiste `Closed` con `ClosedAtUtc` del `FakeTimeProvider`; inexistente → `NotFoundException`; de otro doctor y sin vincular → `ForbiddenException`; ya cerrada → `DomainException` |
| `tests/Dispensario.Application.UnitTests/Consultations/GetConsultationByIdHandlerTests.cs` | Devuelve el DTO con nombres y horas locales; como `Doctor` de otra consulta → la devuelve; como Admin → la devuelve; `Doctor` sin vincular → `ForbiddenException`; inexistente → `NotFoundException` |
| `tests/Dispensario.Application.UnitTests/Consultations/GetPatientConsultationsHandlerTests.cs` | Devuelve las consultas del paciente de varios doctores, `Open` y `Closed`, de la más reciente a la más antigua y paginadas; no incluye las de otro paciente; paciente inactivo se lee; inexistente → `NotFoundException`; paginación inválida y `Doctor` sin vincular → excepción antes de leer |
| `tests/Dispensario.Api.IntegrationTests/Common/AppointmentRequests.cs` (amplía) | Helper que inserta en la BD una cita de hoy que empieza ahora (`DateTime.UtcNow`), creada con `Appointment.Create` y un `nowUtc` anterior, como `SeedStartedAppointmentAsync` |
| `tests/Dispensario.Api.IntegrationTests/Common/ConsultationRequests.cs` (nuevo) | Cuerpos de ejemplo y un helper que inicia una consulta y devuelve su ID |
| `tests/Dispensario.Api.IntegrationTests/Consultations/ConsultationsEndpointsTests.cs` (nuevo) | Como `Doctor` vinculado: `POST` sin cita → 201 con `Location` y el detalle muestra `Open`; con su cita de hoy → 201 y la cita sigue `Scheduled`; con la misma cita otra vez → 409; con una cita de mañana → 400; con una cita de otro doctor → 403; paciente inexistente → 404; sin motivo → 400 con errores de validación; `PUT` → 204 y el detalle muestra los cambios; `close` → 204 y el detalle muestra `Closed` y `closedAtUtc`; `close` sin diagnóstico → 400; `PUT` y `close` sobre una cerrada → 400; `PUT` y `close` sobre la consulta de otro doctor → 403. Como otro `Doctor` vinculado y como Admin: `GET` detalle e historia → 200. Historia: varias consultas ordenadas de la más reciente a la más antigua, `pageSize=0` → 400, paciente inexistente → 404. Como `Doctor` sin vincular: las cinco rutas → 403. Como Admin: `POST` → 403. Como `Receptionist`: las cinco rutas → 403. Sin token → 401. Inexistente → 404. Los errores salen como `application/problem+json` |

- Convenciones de `TESTING.md`: `Method_Scenario_ExpectedResult`, Arrange / Act / Assert y Shouldly.
- Los doctores vinculados de integración se obtienen con `DoctorRequests.AuthenticateAsLinkedDoctorAsync`.

## Implementation plan

1. Crear `ConsultationId`, `ConsultationStatus` y `Consultation` (`Start`, `Update`, `Close`) con `ConsultationTests`. Verificar: `dotnet test` en verde.
2. Añadir `DbSet<Consultation>` a `IApplicationDbContext` y `DispensarioDbContext`, crear `ConsultationConfiguration` y `ConsultationPersistenceTests`. Verificar: `dotnet test` en verde y, tras borrar `dispensario.db`, `dotnet run` crea la tabla `Consultations` con sus índices.
3. Crear `ConsultationDto`, `ConsultationProjections` y `GetConsultationById` (query, handler y endpoint con `ConsultationResponse`), con `GetConsultationByIdHandlerTests`. Verificar: `dotnet test` en verde y la ruta aparece en Scalar.
4. Crear `StartConsultation` (command, validador, handler y endpoint), el helper de cita de hoy en `AppointmentRequests`, `ConsultationRequests` y sus tests de validador, handler e integración (incluido el `GET` detalle). Verificar: `dotnet test` en verde.
5. Crear `UpdateConsultation` (command, validador, handler y endpoint) con sus tests de validador, handler e integración. Verificar: `dotnet test` en verde.
6. Crear `CloseConsultation` (command, validador, handler y endpoint) con sus tests de validador, handler e integración. Verificar: `dotnet test` en verde.
7. Crear `GetPatientConsultations` (query, handler y endpoint) con sus tests de handler e integración, y los casos de rol (`Receptionist`, Admin, `Doctor` sin vincular, sin token) de las cinco rutas. Verificar: `dotnet test` en verde y el flujo iniciar → editar → cerrar → historia desde Scalar.
8. Actualizar la documentación:
   - `README.md` sección **Endpoints**: filas nuevas en la tabla general y una sección **Consultations** con las cinco rutas, acceso, ejemplos de petición y respuesta, el ciclo `Open` → `Closed`, las reglas de inicio con cita (del doctor y del paciente, `Scheduled`/`Confirmed`, de hoy, una por cita) y la tabla de códigos de error; nota de que cerrar aún no completa la cita (fila 16).
   - `docs/guides/DOMAIN.md` §4 Consultations: miembros con sus máximos, firmas de `Start`/`Update`/`Close`, reglas y mensajes; sin eventos hasta la fila 16. Añadir `Consultations/` a §5.
   - `docs/guides/APPLICATION.md`: §2 con `Consultations` en el estado de `IApplicationDbContext`; tabla "Módulo Consultations" con los cinco casos de uso; `ConsultationDto` y `ConsultationProjections`; §6 **StartConsultation** reescrito con el orden de este spec (una consulta por cita, no una `Open`).
   - `docs/guides/INFRASTRUCTURE.md`: tabla `Consultations` con sus columnas y los índices único `AppointmentId` y (`PatientId`, `StartedAtUtc`).
   - `docs/guides/API.md` §1: los cinco endpoints en `Endpoints/Consultations/` (la historia bajo `/api/patients`).
   - `CLAUDE.md`: "Estado actual" con las consultas (feature 15); "Detalles no obvios" con quién escribe y quién lee, el 403 de recepción, la regla de la cita de hoy, una consulta por cita (índice único con varios `NULL`), que `Closed` es final y que cerrar no completa la cita hasta la fila 16.
   - `docs/PLAN.md`: la fila 15 apunta a `specs/15-consultations.md`.

## Acceptance criteria

- [x] `dotnet build Dispensario.slnx` termina sin errores y sin warnings nuevos respecto a SPEC 14.
- [x] `dotnet test Dispensario.slnx` termina con éxito e incluye los tests de este spec y los anteriores.
- [x] Tras borrar `dispensario.db`, `dotnet run` crea la tabla `Consultations` con las columnas del spec, el índice único `AppointmentId` y el índice (`PatientId`, `StartedAtUtc`).
- [x] `POST /api/consultations` como `Doctor` vinculado y activo, con un paciente activo y sin cita, devuelve 201 con `Location` y la consulta queda `Open` con ese doctor.
- [x] Con un `appointmentId` de una cita `Scheduled` o `Confirmed` del mismo doctor y paciente y de la fecha local de hoy, devuelve 201 y la cita no cambia de estado.
- [x] Con una cita de otro día, de otro paciente o en estado `Cancelled`, `NoShow` o `Completed` devuelve 400; de otro doctor, 403; inexistente, 404; con una cita que ya tiene consulta (abierta o cerrada), 409.
- [x] Un paciente inexistente devuelve 404; un paciente o un doctor inactivo, 400; un motivo vacío o de más de 500 caracteres, 400.
- [x] `PUT /api/consultations/{id}` del doctor dueño sobre una consulta `Open` devuelve 204 y reemplaza motivo, diagnóstico y notas (un opcional `null` borra el valor).
- [x] `POST /api/consultations/{id}/close` con diagnóstico devuelve 204, la consulta queda `Closed` con `closedAtUtc`; sin diagnóstico devuelve 400.
- [x] `PUT` y `close` sobre una consulta `Closed` devuelven 400; sobre la consulta de otro doctor, 403.
- [x] `GET /api/consultations/{id}` como cualquier `Doctor` vinculado o como Admin devuelve 200 con nombres del paciente y del doctor, horas UTC y locales, y `status`.
- [x] `GET /api/patients/{id}/consultations` como `Doctor` vinculado o Admin devuelve 200 con todas las consultas del paciente (de cualquier doctor, `Open` y `Closed`), de la más reciente a la más antigua y paginadas; paginación inválida, 400; paciente inexistente, 404.
- [x] Un `Receptionist` recibe 403 en las cinco rutas; el Admin recibe 403 en `POST`, `PUT` y `close`; un `Doctor` sin vincular recibe 403 en las cinco rutas.
- [x] Las cinco rutas devuelven 401 sin token y las de `{id}`, 404 con una consulta inexistente.
- [x] Todos los errores salen como `application/problem+json`.
- [x] Las cinco rutas aparecen en `/openapi/v1.json` con tag `Consultations`, nombre y resumen.
- [x] La sección **Endpoints** del `README.md` documenta las cinco rutas con acceso, petición, respuesta y códigos de error.
- [x] `DOMAIN.md`, `APPLICATION.md`, `INFRASTRUCTURE.md`, `API.md` y `CLAUDE.md` describen las consultas tal como se implementaron, y `docs/PLAN.md` fila 15 apunta a `specs/15-consultations.md`.

## Decisions

- **Yes:** leen consultas (detalle e historia) cualquier `Doctor` vinculado y el Admin, con la política `AdminOrDoctor` existente. Decisión del usuario.
- **No:** solo doctores, solo el doctor dueño, o todos los autenticados (la recepción vería datos clínicos). Descartado por el usuario.
- **Yes:** solo el `Doctor` dueño inicia, edita y cierra (política `Clinical`); el doctor de la consulta es siempre `ICurrentUser.DoctorId`, nunca llega en el cuerpo. Decisión del usuario, como `ARCHITECTURE.md` §6.
- **No:** que el Admin escriba consultas en nombre de un doctor. Descartado por el usuario.
- **Yes:** un `Doctor` sin vincular recibe 403 antes de leer también en las lecturas, como en citas. Decisión del usuario.
- **Yes:** una consulta con cita exige que la cita sea del doctor actual (403 si no) y del paciente, `Scheduled` o `Confirmed`, y de la fecha local de hoy (400). Decisión del usuario.
- **No:** sin restricción de tiempo, o solo desde el inicio de la cita. Descartado por el usuario.
- **Yes:** una sola consulta por cita, abierta o cerrada, con índice único en `AppointmentId` y 409 desde el handler. Decisión del usuario.
- **No:** una consulta `Open` a la vez por cita, como decía `APPLICATION.md` §6. Tras la fila 16 la cita ya estaría `Completed`; se corrige la guía. Descartado por el usuario.
- **Yes:** consultas sin cita permitidas (`appointmentId` opcional); paciente y doctor deben existir y estar activos; sin límite de consultas `Open` por doctor o paciente. Decisión del usuario.
- **Yes:** `Reason` obligatorio (500), `Diagnosis` (1000) y `Notes` (4000) opcionales mientras está `Open`, texto libre. Decisión del usuario.
- **No:** códigos CIE-10 o campos estructurados. Fuera de v1.
- **Yes:** `Close(diagnosis, notes, closedAtUtc)` recibe el diagnóstico (obligatorio) y las notas en el cuerpo; `Closed` es final. Decisión del usuario, como `DOMAIN.md`.
- **No:** `Close()` sin cuerpo, o reabrir consultas. Descartado por el usuario.
- **Yes:** `ConsultationClosedDomainEvent`, `Appointment.Complete` y el handler quedan enteros para la fila 16; este spec no emite eventos ni toca la cita. Decisión del usuario, por la regla 7 de `DOMAIN.md`.
- **Yes:** rutas REST bajo `/api/consultations` y la historia en `GET /api/patients/{id}/consultations`, con el código en el módulo Consultations. Decisión del usuario.
- **No:** historia en `GET /api/consultations?patientId=`. Descartado por el usuario.
- **Yes:** historia paginada (como `GetUsers`, máx. 100), con todas las consultas del paciente, `Open` y `Closed`, de la más reciente a la más antigua; un paciente inactivo también se lee. Decisión del usuario.
- **No:** solo `Closed`, o lista sin paginar. Descartado por el usuario.
- **Yes:** `PUT` reemplaza los tres textos (un opcional `null` borra), como en Patients; el DTO trae los nombres por subconsulta y las horas locales en memoria, como `AppointmentDto`. Decisión del usuario.
- **Yes:** `Update` y `Close` no comprueban si el doctor o el paciente siguen activos, para poder terminar una consulta ya abierta. Si en la revisión se prefiere 400, se cambia aquí.
- **Yes:** las reglas que cruzan aggregates (cita, doctor, paciente) usan `BadRequestException`/`ForbiddenException`/`ConflictException` en el handler; las del propio aggregate, `DomainException`.
- **Yes:** sin FKs físicas hacia `Patients`, `Doctors` ni `Appointments`, como `Appointments` y `Users.DoctorId`.

## Risks

| Risk | Mitigation |
| --- | --- |
| Dos inicios simultáneos con la misma cita pasan la comprobación del handler | El índice único de `AppointmentId` rechaza el segundo; sale 500 en vez de 409. Aceptado en v1, como los cruces de citas. |
| La recepción cancela o marca no asistida una cita con consulta `Open` | No se bloquea en este spec (fuera de alcance); la consulta sigue abierta y se puede cerrar. Lo revisa la fila 16. |
| Cerrar una consulta no completa la cita y la agenda muestra la cita `Scheduled`/`Confirmed` | Documentado en `README.md` y `CLAUDE.md`; la fila 16 lo resuelve. |
| El helper de cita "de hoy" de integración cruza la medianoche local entre el seed y la petición | La cita empieza en el instante del seed y la petición es inmediata; el caso de borde de "hoy" se prueba con `FakeTimeProvider` en el handler. |
| La historia de un paciente con muchas consultas trae textos largos (hasta 4000 caracteres de notas) por fila | Paginación con máximo de 100. |
| El esquema cambia y una `dispensario.db` local antigua no tiene `Consultations` | Borrar `dispensario.db` antes de `dotnet run` (`EnsureCreated` hasta la fila 17). |

## What is **not** in this spec

- `ConsultationClosedDomainEvent` y la cita completada al cerrar (fila 16).
- Reabrir consultas, escritura por el Admin y lectura por la recepción.
- Listado general de consultas y límites de consultas abiertas.
- Signos vitales, recetas, exámenes, CIE-10 y adjuntos.
- Migraciones.

Cada uno de ellos, si llega, va en su propio spec.
