# SPEC 13 — Appointments: ciclo de vida

> **Status:** Implemented
> **Depends on:** SPEC 07, SPEC 09, SPEC 10, SPEC 11, SPEC 12
> **Date:** 2026-10-27
> **Objective:** Añadir al aggregate `Appointment` las transiciones `Confirm`, `Cancel`, `MarkNoShow` y `Reschedule` con su máquina de estados y exponerlas como endpoints de acción sobre `/api/appointments/{id}`.

## Por qué este spec existe

El spec 12 solo crea citas `Scheduled`: el enum `AppointmentStatus` está completo, pero ninguna cita cambia de estado.
La recepción necesita confirmar, cancelar y reprogramar citas, y registrar cuándo el paciente no se presentó.
El doctor también puede cancelar sus citas y marcarlas como no asistidas (decisión del usuario).
`Reschedule` repite las reglas del alta (horario, alineación y cruces), así que esas reglas salen de `ScheduleAppointmentHandler` a una clase común de Application, sin cambiar su comportamiento.
`Complete` no entra: la crea la fila 16 junto con el handler de `ConsultationClosedDomainEvent`, que es su único uso.

## Scope

**In:**

- Métodos de dominio `Confirm`, `Cancel`, `MarkNoShow` y `Reschedule` en `Appointment`, la propiedad `CancellationReason` y la constante `CancellationReasonMaxLength`, con sus tests de dominio.
- Persistencia: columna `CancellationReason` (nullable, 500) en `AppointmentConfiguration`.
- Clase común `AppointmentSlotRules` en `Dispensario.Application/Appointments/Common/` con las reglas de horario, alineación y cruces; `ScheduleAppointmentHandler` pasa a usarla sin cambiar su comportamiento.
- Commands `ConfirmAppointment`, `CancelAppointment`, `MarkAppointmentNoShow` y `RescheduleAppointment` (validadores donde hay cuerpo y handlers) en `Dispensario.Application/Appointments/Commands/`.
- `AppointmentDto` y `AppointmentResponse` añaden `CancellationReason`.
- Endpoints `POST /api/appointments/{id}/confirm` y `PUT /api/appointments/{id}/schedule` (`Reception`), y `POST /api/appointments/{id}/cancel` y `POST /api/appointments/{id}/no-show` (`Reception` o el `Doctor` dueño de la cita).
- Tests unitarios de dominio, persistencia, validadores y handlers, y tests de integración de los endpoints.
- Documentación: sección **Endpoints** del `README.md`, `DOMAIN.md`, `APPLICATION.md`, `INFRASTRUCTURE.md`, `API.md`, `CLAUDE.md` y `docs/PLAN.md`.

**Out of scope (for future specs):**

- Transición `Complete` (método de dominio, handler y endpoint): fila 16.
- Domain events de Appointments (`AppointmentCancelledDomainEvent`, `AppointmentRescheduledDomainEvent`…): no hay consumidores.
- Cambiar el doctor, el paciente o el motivo al reprogramar.
- Que un `Doctor` confirme o reprograme citas.
- Fecha de cancelación (`CancelledAtUtc`) o quién canceló: la auditoría (`UpdatedAtUtc`, `UpdatedBy`) ya lo registra.
- Deshacer una cancelación o una no asistencia (volver a `Scheduled`).
- Cálculo de slots disponibles (fila 14).
- Notificaciones al paciente.
- Exclusión estricta de cruces bajo concurrencia (dos reprogramaciones o altas simultáneas).
- Migraciones: siguen con `EnsureCreated` hasta la fila 17.

## Data model

### Máquina de estados

```
Scheduled ──Confirm──► Confirmed
    │                      │
    ├──Cancel──────────────┼──► Cancelled
    ├──MarkNoShow──────────┴──► NoShow
    └──Reschedule (Scheduled|Confirmed → Scheduled, con nuevas fechas)
```

| Transición | Estado de origen | Momento | Estado final |
| --- | --- | --- | --- |
| `Confirm` | `Scheduled` | antes del inicio (`StartUtc > nowUtc`) | `Confirmed` |
| `Cancel` | `Scheduled`, `Confirmed` | antes del inicio | `Cancelled` |
| `MarkNoShow` | `Scheduled`, `Confirmed` | desde el inicio (`StartUtc <= nowUtc`) | `NoShow` |
| `Reschedule` | `Scheduled`, `Confirmed` | antes del inicio actual, y el nuevo inicio también futuro | `Scheduled` |

- `Completed`, `Cancelled` y `NoShow` son finales en este spec: ninguna transición sale de ellos.
- `Complete` (desde `Scheduled` o `Confirmed`) llega con la fila 16.

### Aggregate `Appointment` (`Dispensario.Domain/Appointments/Appointment.cs`, cambios)

```csharp
public sealed class Appointment : AggregateRoot<AppointmentId>
{
    public const int ReasonMaxLength = 500;
    public const int CancellationReasonMaxLength = 500;

    // ...miembros del spec 12
    public string? CancellationReason { get; private set; }    // solo en Cancelled; Trim

    public void Confirm(DateTime nowUtc);
    public void Cancel(string reason, DateTime nowUtc);
    public void MarkNoShow(DateTime nowUtc);
    public void Reschedule(DateTime startUtc, DateTime endUtc, DateTime nowUtc);
}
```

Reglas (`DomainException` → 400), en este orden dentro de cada método:

| Método | Regla | Mensaje |
| --- | --- | --- |
| `Confirm` | estado distinto de `Scheduled` | `Only scheduled appointments can be confirmed.` |
| `Confirm` | `StartUtc <= nowUtc` | `Appointment has already started.` |
| `Cancel` | estado distinto de `Scheduled` y `Confirmed` | `Only scheduled or confirmed appointments can be cancelled.` |
| `Cancel` | `StartUtc <= nowUtc` | `Appointment has already started.` |
| `Cancel` | `reason` en blanco tras el `Trim` | `Cancellation reason is required.` |
| `Cancel` | `reason` de más de 500 caracteres tras el `Trim` | `Cancellation reason must be at most 500 characters.` |
| `MarkNoShow` | estado distinto de `Scheduled` y `Confirmed` | `Only scheduled or confirmed appointments can be marked as no-show.` |
| `MarkNoShow` | `StartUtc > nowUtc` | `Appointment has not started yet.` |
| `Reschedule` | estado distinto de `Scheduled` y `Confirmed` | `Only scheduled or confirmed appointments can be rescheduled.` |
| `Reschedule` | `StartUtc <= nowUtc` (inicio actual) | `Appointment has already started.` |
| `Reschedule` | `startUtc == StartUtc` | `New start must be different from the current one.` |
| `Reschedule` | `startUtc >= endUtc` | `Appointment start must be before its end.` |
| `Reschedule` | `startUtc <= nowUtc` (nuevo inicio) | `Appointment cannot be scheduled in the past.` |

- `nowUtc` lo pasa el handler con `TimeProvider`, igual que en `Create`.
- `Cancel` guarda el motivo recortado en `CancellationReason`; el resto de transiciones no lo tocan (siempre es `null` fuera de `Cancelled`).
- `Reschedule` cambia `StartUtc` y `EndUtc` y deja la cita en `Scheduled`, también si estaba `Confirmed`.
- Si una regla falla, la cita no cambia.
- Los mensajes de pasado y de inicio/fin coinciden con los de `Create` para que el cliente vea el mismo texto en el alta y en la reprogramación.
- Sin domain events.

### Persistencia (`AppointmentConfiguration`, cambio)

| Columna | Origen | Notas |
| --- | --- | --- |
| `CancellationReason` | `string?` | `Appointment.CancellationReasonMaxLength`, nullable |

- El esquema cambia: hay que **borrar `dispensario.db`** local antes de `dotnet run`.

### Reglas comunes de agenda (`Dispensario.Application/Appointments/Common/AppointmentSlotRules.cs`, nuevo)

```csharp
public static class AppointmentSlotRules
{
    // Cabe en un bloque del horario y está alineada desde el inicio del bloque (horas locales)
    public static void EnsureFitsSchedule(Doctor doctor, DateTime localStart, DateTime localEnd);

    // Sin cruces con citas Scheduled/Confirmed del doctor y, después, del paciente
    public static Task EnsureNoOverlapsAsync(
        IApplicationDbContext db, DoctorId doctorId, PatientId patientId,
        DateTime startUtc, DateTime endUtc,
        AppointmentId? excludedAppointmentId, CancellationToken cancellationToken);
}
```

- `EnsureFitsSchedule` lanza, en orden, `BadRequestException("The doctor is not available at that time.")` y `BadRequestException("Start time must align with the doctor's appointment slots.")`, con la misma lógica que hoy tiene `ScheduleAppointmentHandler` (`IsAvailableAt` + `IsAlignedToSlots`).
- `EnsureNoOverlapsAsync` lanza `ConflictException("The doctor already has an appointment at that time.")` y luego `ConflictException("The patient already has an appointment at that time.")`. Con `excludedAppointmentId` ignora esa cita, para que una reprogramación no choque consigo misma.
- `ScheduleAppointmentHandler` sustituye su código de horario, alineación y cruces por estas dos llamadas (con `excludedAppointmentId = null`). Su comportamiento, el orden de las comprobaciones y sus tests no cambian.

### `Dispensario.Application/Appointments/Commands/` (namespaces iguales a las carpetas)

```
Commands/
  ConfirmAppointment/      ConfirmAppointmentCommand.cs      ConfirmAppointmentHandler.cs
  CancelAppointment/       CancelAppointmentCommand.cs       CancelAppointmentHandler.cs
  MarkAppointmentNoShow/   MarkAppointmentNoShowCommand.cs   MarkAppointmentNoShowHandler.cs
  RescheduleAppointment/   RescheduleAppointmentCommand.cs   RescheduleAppointmentHandler.cs
```

```csharp
public sealed record ConfirmAppointmentCommand(Guid AppointmentId) : ICommand;
public sealed record CancelAppointmentCommand(Guid AppointmentId, string Reason) : ICommand;
public sealed record MarkAppointmentNoShowCommand(Guid AppointmentId) : ICommand;
public sealed record RescheduleAppointmentCommand(
    Guid AppointmentId,
    DateTime StartLocal) : ICommand;      // hora local del dispensario, sin offset
```

### Validadores (en el archivo de cada command)

| Validador | Reglas |
| --- | --- |
| `CancelAppointmentCommandValidator` | `AppointmentId` `NotEmpty`; `Reason` `NotEmpty` y `MaximumLength(Appointment.CancellationReasonMaxLength)` |
| `RescheduleAppointmentCommandValidator` | `AppointmentId` `NotEmpty`; `StartLocal` `NotEmpty` y `Kind == DateTimeKind.Unspecified` con el mensaje `Start time must be a local time without offset.` |

- `ConfirmAppointment` y `MarkAppointmentNoShow` no tienen validador: el ID llega de la ruta con `{id:guid}`.

### Handlers

| Handler | Comportamiento, en orden |
| --- | --- |
| `ConfirmAppointmentHandler` | Recibe `IApplicationDbContext` y `TimeProvider`. 1) La cita no existe → `NotFoundException(nameof(Appointment), id)`. 2) `appointment.Confirm(nowUtc)`. 3) `SaveChangesAsync` |
| `CancelAppointmentHandler` | Recibe `IApplicationDbContext`, `ICurrentUser` y `TimeProvider`. 1) Usuario `Doctor` sin `DoctorId` → `ForbiddenException("Your user is not linked to a doctor.")`. 2) La cita no existe → `NotFoundException`. 3) Usuario `Doctor` y la cita es de otro doctor → `ForbiddenException("You can only manage your own appointments.")`. 4) `appointment.Cancel(reason, nowUtc)`. 5) `SaveChangesAsync` |
| `MarkAppointmentNoShowHandler` | Igual que `CancelAppointmentHandler`, con `appointment.MarkNoShow(nowUtc)` en el paso 4 |
| `RescheduleAppointmentHandler` | Recibe `IApplicationDbContext`, `IClinicTimeZone` y `TimeProvider`. 1) La cita no existe → `NotFoundException`. 2) Su paciente no existe → `NotFoundException(nameof(Patient), id)`; inactivo → `BadRequestException("Patient is inactive.")`. 3) Su doctor no existe → `NotFoundException(nameof(Doctor), id)`; inactivo → `BadRequestException("Doctor is inactive.")`. 4) `startUtc = ToUtc(StartLocal)`, `endUtc = startUtc + AppointmentDurationMinutes` (duración vigente), `localEnd = StartLocal + duración`. 5) `appointment.Reschedule(startUtc, endUtc, nowUtc)`. 6) `AppointmentSlotRules.EnsureFitsSchedule`. 7) `AppointmentSlotRules.EnsureNoOverlapsAsync` excluyendo la propia cita. 8) Un único `SaveChangesAsync` |

- La cita se lee con seguimiento (sin `AsNoTracking`) para guardar el cambio; paciente y doctor, con `AsNoTracking`.
- En `Cancel` y `MarkNoShow` el orden de 403/404 es el mismo que en `GetAppointmentById` (spec 12): sin vincular, 403 antes de leer; ajena, 403 después del 404.
- Admin y `Receptionist` pueden cancelar y marcar no asistidas citas de cualquier doctor.
- En `Reschedule`, un doctor o paciente inactivo da 400 antes que el estado de la cita: hacen falta el doctor y su duración para calcular el nuevo fin.
- Si una regla de los pasos 6 o 7 falla tras el paso 5, no se guarda nada: el cambio en memoria se descarta con la petición.

### `Dispensario.Api/Endpoints/Appointments/`

```
Appointments/
  ConfirmAppointmentEndpoint.cs  CancelAppointmentEndpoint.cs
  MarkAppointmentNoShowEndpoint.cs  RescheduleAppointmentEndpoint.cs
```

| Ruta | Request | Autorización | Respuestas |
| --- | --- | --- | --- |
| `POST /api/appointments/{id:guid}/confirm` | — | `Reception` | 204, 400, 401, 403, 404 |
| `POST /api/appointments/{id:guid}/cancel` | `CancelAppointmentRequest(string Reason)` | `RequireAuthorization()` + comprobación de dueño en el handler | 204, 400, 401, 403, 404 |
| `POST /api/appointments/{id:guid}/no-show` | — | `RequireAuthorization()` + comprobación de dueño en el handler | 204, 400, 401, 403, 404 |
| `PUT /api/appointments/{id:guid}/schedule` | `RescheduleAppointmentRequest(DateTime StartLocal)` | `Reception` | 204, 400, 401, 403, 404, 409 |

Ejemplos de cuerpo:

```json
{ "reason": "El paciente pidió cancelar por viaje" }
```

```json
{ "startLocal": "2026-11-04T10:00" }
```

- Todos con `WithTags("Appointments")`, `WithName` igual al command (`ConfirmAppointment`, `CancelAppointment`, `MarkAppointmentNoShow`, `RescheduleAppointment`) y `WithSummary`.
- `GetAppointmentByIdEndpoint` añade `string? CancellationReason` a `AppointmentResponse`, después de `Status`. `AppointmentDto` y `AppointmentProjections.ToDto` lo añaden en la misma posición.
- Un `startLocal` mal formado falla al leer la petición: 400 de `BadHttpRequestException`, sin `ValidationErrors`.

### Tests

| Archivo | Qué prueba |
| --- | --- |
| `tests/Dispensario.Domain.UnitTests/Appointments/AppointmentTests.cs` (amplía) | `Confirm` desde `Scheduled` → `Confirmed`; desde `Confirmed`, `Cancelled` y `NoShow` → `DomainException`; ya empezada → `DomainException`. `Cancel` desde `Scheduled` y `Confirmed` → `Cancelled` con el motivo recortado; desde `Cancelled` y `NoShow` → `DomainException`; ya empezada, motivo en blanco y de 501 caracteres → `DomainException`. `MarkNoShow` en el inicio exacto y después → `NoShow`; antes del inicio y desde `Cancelled` → `DomainException`. `Reschedule` desde `Confirmed` → `Scheduled` con las nuevas fechas; misma hora, nuevo inicio pasado, inicio ≥ fin, cita ya empezada y desde `Cancelled` → `DomainException`. Una regla fallida no cambia el estado. Sin domain events |
| `tests/Dispensario.Application.UnitTests/Persistence/Appointments/AppointmentPersistenceTests.cs` (amplía) | Ida y vuelta de una cita cancelada con `CancellationReason` y `Status = Cancelled` |
| `tests/Dispensario.Application.UnitTests/Appointments/CancelAppointmentCommandValidatorTests.cs` | ID vacío, motivo vacío y de 501 caracteres fallan; un comando correcto pasa |
| `tests/Dispensario.Application.UnitTests/Appointments/RescheduleAppointmentCommandValidatorTests.cs` | ID vacío, `StartLocal` por defecto y con `Kind = Utc` fallan; un comando correcto pasa |
| `tests/Dispensario.Application.UnitTests/Appointments/ConfirmAppointmentHandlerTests.cs` | Confirma y persiste `Confirmed`; inexistente → `NotFoundException`; ya confirmada → `DomainException` |
| `tests/Dispensario.Application.UnitTests/Appointments/CancelAppointmentHandlerTests.cs` | Como `Receptionist` cancela y persiste el motivo; inexistente → `NotFoundException`; como `Doctor` la suya pasa, la de otro → `ForbiddenException`; `Doctor` sin vincular → `ForbiddenException` |
| `tests/Dispensario.Application.UnitTests/Appointments/MarkAppointmentNoShowHandlerTests.cs` | Con un `FakeTimeProvider` tras el inicio persiste `NoShow`; antes del inicio → `DomainException`; inexistente → `NotFoundException`; `Doctor` dueño pasa, ajeno y sin vincular → `ForbiddenException` |
| `tests/Dispensario.Application.UnitTests/Appointments/RescheduleAppointmentHandlerTests.cs` | Reprograma dentro del horario con la duración vigente y deja `Scheduled` una cita `Confirmed`; mover la cita 15 minutos sobre su propio horario no es cruce consigo misma; inexistente → `NotFoundException`; doctor o paciente inactivo → `BadRequestException`; fuera del horario y desalineada → `BadRequestException`; cruce con otra cita del doctor y con otra del paciente → `ConflictException`; misma hora y en el pasado → `DomainException` |
| `tests/Dispensario.Application.UnitTests/Appointments/ScheduleAppointmentHandlerTests.cs` | Sin cambios: debe seguir en verde tras extraer `AppointmentSlotRules` |
| `tests/Dispensario.Api.IntegrationTests/Common/AppointmentRequests.cs` (amplía) | Helper que inserta directamente en la BD (scope de `factory.Services`) una cita ya empezada, creada con `Appointment.Create` y un `nowUtc` anterior a su inicio, para probar `no-show` con el reloj real |
| `tests/Dispensario.Api.IntegrationTests/Appointments/AppointmentLifecycleEndpointsTests.cs` | Como `Receptionist`: `confirm` → 204 y el detalle muestra `Confirmed`; `confirm` repetido → 400; `cancel` → 204 y el detalle muestra `Cancelled` y `cancellationReason`; `cancel` sin motivo → 400 con errores de validación; tras cancelar, el mismo horario se puede volver a agendar (201); `PUT schedule` → 204 y el detalle muestra el nuevo `startLocal` y `Scheduled`; `PUT schedule` con cruce → 409, desalineado → 400, con `Z` → 400; `no-show` sobre una cita futura → 400 y sobre una empezada → 204; cita inexistente → 404. Como `Doctor` vinculado: `cancel` y `no-show` de la suya → 204, de una ajena → 403; `confirm` y `PUT schedule` → 403. Sin token → 401 en las cuatro rutas |

- Convenciones de `TESTING.md`: `Method_Scenario_ExpectedResult`, Arrange / Act / Assert y Shouldly.

## Implementation plan

1. Añadir `CancellationReason`, `CancellationReasonMaxLength` y los métodos `Confirm`, `Cancel`, `MarkNoShow` y `Reschedule` a `Appointment`, con sus tests en `AppointmentTests`. Verificar: `dotnet test` en verde.
2. Mapear `CancellationReason` en `AppointmentConfiguration` y ampliar `AppointmentPersistenceTests`. Verificar: `dotnet test` en verde y, tras borrar `dispensario.db`, `dotnet run` crea la columna.
3. Crear `AppointmentSlotRules` y hacer que `ScheduleAppointmentHandler` la use. Verificar: `dotnet test` en verde sin tocar `ScheduleAppointmentHandlerTests` ni los tests de integración del alta.
4. Añadir `CancellationReason` a `AppointmentDto`, `AppointmentProjections.ToDto` y `AppointmentResponse`. Verificar: `dotnet test` en verde y `GET /api/appointments/{id}` devuelve `cancellationReason: null`.
5. Crear `ConfirmAppointment` (command, handler y endpoint), con `ConfirmAppointmentHandlerTests` y sus casos de integración en `AppointmentLifecycleEndpointsTests`. Verificar: `dotnet test` en verde.
6. Crear `CancelAppointment` (command, validador, handler y endpoint), con sus tests de validador, handler e integración, incluidos los del rol `Doctor`. Verificar: `dotnet test` en verde.
7. Crear `MarkAppointmentNoShow` (command, handler y endpoint), el helper de cita empezada en `AppointmentRequests` y sus tests de handler e integración. Verificar: `dotnet test` en verde.
8. Crear `RescheduleAppointment` (command, validador, handler y endpoint), con sus tests de validador, handler e integración. Verificar: `dotnet test` en verde y reprogramación desde Scalar.
9. Actualizar la documentación:
   - `README.md` sección **Endpoints**: filas nuevas en la tabla general y, en **Appointments**, las cuatro rutas con acceso, ejemplo de petición y tabla de códigos de error; la máquina de estados y `cancellationReason` en la respuesta del detalle.
   - `docs/guides/DOMAIN.md` §4 Appointments: métodos, reglas y mensajes de las transiciones, `CancellationReason` y la tabla de momentos; `Complete` sigue diferido a la fila 16; sin eventos.
   - `docs/guides/APPLICATION.md`: tabla "Módulo Appointments" con los cuatro casos de uso, `AppointmentSlotRules` y §6 con el orden de `RescheduleAppointment`.
   - `docs/guides/INFRASTRUCTURE.md`: columna `CancellationReason` de `Appointments`.
   - `docs/guides/API.md` §1: los cuatro endpoints nuevos en `Endpoints/Appointments/`.
   - `CLAUDE.md`: "Estado actual" con el ciclo de vida de citas (feature 13); "Detalles no obvios" con los momentos de cada transición, que el `Doctor` solo cancela y marca no asistidas sus citas, que reprogramar vuelve a `Scheduled` y no choca consigo misma, y que `Complete` llega con la fila 16.
   - `docs/PLAN.md`: la fila 13 apunta a `specs/13-appointments-lifecycle.md`.

## Acceptance criteria

- [ ] `dotnet build Dispensario.slnx` termina sin errores y sin warnings nuevos respecto a SPEC 12.
- [ ] `dotnet test Dispensario.slnx` termina con éxito e incluye los tests de este spec y los anteriores, con `ScheduleAppointmentHandlerTests` sin modificar.
- [ ] Tras borrar `dispensario.db`, `dotnet run` crea la columna `CancellationReason` nullable en `Appointments`.
- [ ] `POST /api/appointments/{id}/confirm` como `Receptionist` o Admin sobre una cita `Scheduled` futura devuelve 204 y la cita queda `Confirmed`; sobre una cita `Confirmed`, `Cancelled` o ya empezada devuelve 400.
- [ ] `POST /api/appointments/{id}/cancel` con motivo sobre una cita `Scheduled` o `Confirmed` futura devuelve 204, la cita queda `Cancelled` y el detalle muestra `cancellationReason`; sin motivo o con más de 500 caracteres devuelve 400.
- [ ] Una cita cancelada deja libre su horario: se puede agendar otra en la misma hora con el mismo doctor.
- [ ] `POST /api/appointments/{id}/no-show` sobre una cita `Scheduled` o `Confirmed` ya empezada devuelve 204 y la cita queda `NoShow`; sobre una cita futura devuelve 400.
- [ ] `PUT /api/appointments/{id}/schedule` con un `startLocal` válido devuelve 204, la cita queda `Scheduled` con las nuevas horas y el fin calculado con la duración vigente del doctor.
- [ ] `PUT /api/appointments/{id}/schedule` devuelve 400 con la misma hora, en el pasado, fuera del horario, desalineado, con `Z` u offset, o con doctor o paciente inactivo; 409 si cruza con otra cita del doctor o del paciente.
- [ ] Mover una cita a un horario que solo se solapa con ella misma se acepta.
- [ ] Cualquier transición sobre una cita `Cancelled` o `NoShow` devuelve 400.
- [ ] Las cuatro rutas devuelven 404 con una cita inexistente y 401 sin token.
- [ ] Un `Doctor` vinculado recibe 204 al cancelar o marcar no asistida una cita suya y 403 sobre una ajena; recibe 403 en `confirm` y `PUT schedule`; un `Doctor` sin vincular recibe 403 en `cancel` y `no-show`.
- [ ] Todos los errores salen como `application/problem+json`.
- [ ] Las cuatro rutas nuevas aparecen en `/openapi/v1.json` con tag `Appointments`, nombre y resumen.
- [ ] La sección **Endpoints** del `README.md` documenta las cuatro rutas nuevas con acceso, petición y códigos de error, y `cancellationReason` en la respuesta.
- [ ] `DOMAIN.md`, `APPLICATION.md`, `INFRASTRUCTURE.md`, `API.md` y `CLAUDE.md` describen el ciclo de vida tal como se implementó, y `docs/PLAN.md` fila 13 apunta a `specs/13-appointments-lifecycle.md`.

## Decisions

- **Yes:** `Reception` hace las cuatro transiciones; el `Doctor` vinculado puede `Cancel` y `MarkNoShow` solo sobre sus citas. Decisión del usuario.
- **No:** solo `Reception`, o el `Doctor` con las cuatro. Descartado por el usuario.
- **Yes:** `cancel` y `no-show` usan `RequireAuthorization()` y la comprobación de dueño en el handler, como el detalle del spec 12, en vez de una política nueva: hoy no hay una política "todos los roles".
- **Yes:** `Reschedule` solo cambia `startLocal`; mismo doctor y paciente, mismas reglas que el alta y duración vigente del doctor. Decisión del usuario.
- **No:** cambiar el doctor o el motivo al reprogramar. Descartado por el usuario.
- **Yes:** reprogramar deja la cita en `Scheduled` aunque estuviera `Confirmed`, como prevé `DOMAIN.md`: la nueva hora necesita una nueva confirmación.
- **Yes:** reprogramar a la misma hora da 400 (`New start must be different from the current one.`). Decisión del usuario.
- **No:** aceptar la misma hora. Devolvería en silencio una cita `Confirmed` a `Scheduled`. Descartado por el usuario.
- **Yes:** `CancellationReason` obligatorio, recortado y de hasta 500 caracteres, visible en el DTO. Decisión del usuario.
- **No:** motivo opcional ni `CancelledAtUtc`. La auditoría ya guarda cuándo y quién. Descartado por el usuario.
- **Yes:** `Confirm`, `Cancel` y `Reschedule` solo antes del inicio; `MarkNoShow` solo desde el inicio. El dominio recibe `nowUtc`. Decisión del usuario.
- **No:** transiciones sin restricción de tiempo. Descartado por el usuario.
- **Yes:** una transición no permitida por el estado lanza `DomainException` (400), como `Deactivate`/`Activate`. Decisión del usuario.
- **No:** 409 para estados inválidos. Descartado por el usuario.
- **Yes:** `POST` de acción con 204 para `confirm`, `cancel` y `no-show`, y `PUT /api/appointments/{id}/schedule` para reprogramar. Decisión del usuario.
- **No:** `POST /reschedule` ni un `PATCH /status` genérico. Descartado por el usuario.
- **Yes:** `Complete` queda para la fila 16, junto con su único uso. Decisión del usuario.
- **No:** domain events en este spec: nadie reacciona hoy (`DOMAIN.md` §2 regla 7). Decisión del usuario.
- **Yes:** las reglas de horario, alineación y cruces se extraen a `AppointmentSlotRules` y las usan el alta y la reprogramación. Decisión del usuario.
- **No:** duplicar la lógica en `RescheduleAppointmentHandler`. Descartado por el usuario.
- **Yes:** `AppointmentSlotRules` es una clase estática que recibe el `IApplicationDbContext`: no tiene estado ni dependencias propias y no necesita registro en DI. Si en la revisión se prefiere un servicio inyectable, se cambia aquí.
- **Yes:** los cruces de la reprogramación excluyen la propia cita (`excludedAppointmentId`).
- **Yes:** el `no-show` de integración se prueba con una cita insertada directamente en la BD, porque la Api no permite agendar en el pasado y los tests usan el reloj real.

## Risks

| Risk | Mitigation |
| --- | --- |
| Extraer `AppointmentSlotRules` cambia sin querer el comportamiento del alta | `ScheduleAppointmentHandlerTests` y los tests de integración del alta no se modifican y deben seguir en verde (paso 3). |
| Dos reprogramaciones o altas simultáneas al mismo horario pasan la comprobación de cruces | Igual que en el spec 12: aceptado en v1. |
| El helper que inserta una cita ya empezada se salta las reglas de agenda | Solo se usa para `no-show`; crea la cita con `Appointment.Create` y un `nowUtc` anterior, así que respeta las invariantes del aggregate. |
| Subir la duración del doctor hace que una reprogramación ocupe más que la cita original | Es el comportamiento elegido (duración vigente); el cruce y el horario se validan con el nuevo fin. |
| El esquema cambia y una `dispensario.db` local antigua no tiene `CancellationReason` | Borrar `dispensario.db` antes de `dotnet run` (`EnsureCreated` hasta la fila 17). |

## What is **not** in this spec

- `Complete` y la cita completada al cerrar una consulta (fila 16).
- Domain events de Appointments.
- Cambiar doctor, paciente o motivo al reprogramar, y que el `Doctor` confirme o reprograme.
- `CancelledAtUtc`, deshacer cancelaciones o no asistencias.
- Slots disponibles, notificaciones y exclusión estricta de cruces bajo concurrencia.
- Migraciones.

Cada uno de ellos, si llega, va en su propio spec.
