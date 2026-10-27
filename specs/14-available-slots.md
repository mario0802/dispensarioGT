# SPEC 14 — Slots disponibles

> **Status:** Implemented
> **Depends on:** SPEC 07, SPEC 09, SPEC 10, SPEC 12, SPEC 13
> **Date:** 2026-10-27
> **Objective:** Exponer `GET /api/doctors/{id}/available-slots?date=` con los slots libres de un doctor en una fecha local del dispensario, generados con las mismas reglas de horario, alineación y cruces que usa el alta de citas.

## Por qué este spec existe

Hoy la recepción solo descubre si una hora es válida al intentar agendarla: un 400 por horario o alineación, o un 409 por cruce.
Con este endpoint ve antes qué horas puede ofrecer al paciente para un doctor y un día.
Los slots salen de `AppointmentSlotRules` (spec 13), así que todo slot devuelto es una hora que el alta acepta, salvo que otra cita lo ocupe entre la consulta y el alta.
No cambia el dominio ni la persistencia: es una query de lectura del módulo Appointments.

## Scope

**In:**

- Método `GetScheduleSlots` en `AppointmentSlotRules` que genera los slots del horario de un doctor para una fecha local, con la misma regla de alineación que `EnsureFitsSchedule`.
- Query `GetAvailableSlots` (query y handler) en `Dispensario.Application/Appointments/Queries/GetAvailableSlots/`.
- DTOs `AvailableSlotsDto` y `AvailableSlotDto` en `Dispensario.Application/Appointments/Dtos/`.
- Endpoint `GET /api/doctors/{id:guid}/available-slots?date=` en `Dispensario.Api/Endpoints/Appointments/`, para cualquier usuario autenticado.
- Tests unitarios de `GetScheduleSlots` y del handler, y tests de integración del endpoint.
- Documentación: sección **Endpoints** del `README.md`, `APPLICATION.md`, `API.md`, `CLAUDE.md` y `docs/PLAN.md`.

**Out of scope (for future specs):**

- Excluir los slots en que el **paciente** ya tiene cita (`patientId` opcional): el cruce del paciente lo sigue detectando el alta con 409.
- Devolver todos los slots con su estado (libre, ocupado, pasado).
- Rangos de fechas (`from`/`to`) o varios doctores en una sola petición.
- Horizonte máximo de consulta o de agenda (por ejemplo, 90 días vista).
- Bloqueos de agenda (vacaciones, feriados) y excepciones por fecha.
- Reservar o bloquear un slot temporalmente mientras se agenda.
- Horas locales inexistentes o ambiguas por horario de verano (`America/Guayaquil` no tiene).
- Cambios en el dominio, en la persistencia o en los endpoints existentes.

## Data model

Sin cambios en el dominio ni en la base de datos.

### Generación de slots (`Dispensario.Application/Appointments/Common/AppointmentSlotRules.cs`, cambio)

```csharp
public static class AppointmentSlotRules
{
    // ...EnsureFitsSchedule y EnsureNoOverlapsAsync del spec 13, sin cambios

    // Slots del horario del doctor en una fecha local, ordenados por inicio (Kind = Unspecified)
    public static IReadOnlyList<(DateTime LocalStart, DateTime LocalEnd)> GetScheduleSlots(Doctor doctor, DateOnly date);
}
```

- Toma los bloques de `doctor.Schedule` cuyo `DayOfWeek` es el de `date`.
- En cada bloque genera slots desde `StartTime` en pasos de `AppointmentDurationMinutes`, mientras el fin del slot sea `<= EndTime`. El sobrante final que no cabe entero se descarta: con un bloque `09:00–12:10` y 30 minutos, el último slot es `11:30–12:00`.
- Solo mira el horario: no mira `IsActive`, el pasado ni las citas (igual que `IsAvailableAt`).
- Invariante: todo slot generado pasa `EnsureFitsSchedule(doctor, LocalStart, LocalEnd)` sin lanzar.
- Un día sin bloques devuelve una lista vacía.

### `Dispensario.Application/Appointments/` (namespaces iguales a las carpetas)

```
Appointments/
  Queries/
    GetAvailableSlots/   GetAvailableSlotsQuery.cs  GetAvailableSlotsHandler.cs
  Dtos/                  AvailableSlotsDto.cs
```

```csharp
public sealed record GetAvailableSlotsQuery(Guid DoctorId, DateOnly Date) : IQuery<AvailableSlotsDto>;   // fecha local del dispensario

public sealed record AvailableSlotsDto(
    Guid DoctorId,
    DateOnly Date,
    int DurationMinutes,                       // AppointmentDurationMinutes vigente del doctor
    IReadOnlyList<AvailableSlotDto> Slots);

public sealed record AvailableSlotDto(DateTime StartLocal, DateTime EndLocal);   // horas locales sin offset
```

### Handler

| Handler | Comportamiento, en orden |
| --- | --- |
| `GetAvailableSlotsHandler` | Recibe `IApplicationDbContext`, `IClinicTimeZone` y `TimeProvider`. 1) `Date < clinicTimeZone.Today` → `BadRequestException("Date cannot be in the past.")`. 2) `AsNoTracking`; el doctor no existe → `NotFoundException(nameof(Doctor), id)`; inactivo → `BadRequestException("Doctor is inactive.")`. 3) `AppointmentSlotRules.GetScheduleSlots(doctor, Date)`; si no hay ninguno, devuelve el DTO con `Slots` vacío sin más consultas. 4) Convierte cada slot a UTC con `ToUtc` y descarta los que tienen `startUtc <= nowUtc`. 5) Lee con `AsNoTracking` las citas `Scheduled` o `Confirmed` del doctor que se cruzan con el día (`StartUtc < ToUtc(Date + 1 día 00:00) && ToUtc(Date 00:00) < EndUtc`), proyectando solo `StartUtc` y `EndUtc`. 6) Descarta en memoria los slots que se cruzan con alguna (`StartUtc < slotEndUtc && slotStartUtc < EndUtc`; contiguos no cuentan). 7) Devuelve `AvailableSlotsDto` con los slots restantes en hora local, ordenados por inicio |

- La query no pasa por `ValidationBehavior`: el pasado se comprueba a mano en el handler, antes de leer la BD (como la paginación de `GetAppointments`).
- `nowUtc` sale de `TimeProvider`; "hoy" es la fecha local del dispensario (`IClinicTimeZone.Today`), no la UTC.
- El filtro del paso 4 usa `<=`, igual que `Appointment.Create`: un slot que empieza justo ahora ya no se ofrece.
- Una cita `Cancelled`, `NoShow` o `Completed` no ocupa su slot.
- Una cita que solo se solapa en parte con un slot (por ejemplo, agendada con otra duración antes de cambiarla) también lo descarta.
- No hay comprobación de dueño: cualquier usuario autenticado, también un `Doctor` sin vincular, ve los slots de cualquier doctor. La respuesta no lleva datos de pacientes.

### `Dispensario.Api/Endpoints/Appointments/GetAvailableSlotsEndpoint.cs` (nuevo)

| Ruta | Request → Response | Autorización | Respuestas |
| --- | --- | --- | --- |
| `GET /api/doctors/{id:guid}/available-slots?date=` | `Guid id` de la ruta y `DateOnly date` de la query (obligatorio) → `AvailableSlotsResponse` | `RequireAuthorization()` | 200, 400, 401, 404 |

```csharp
public sealed record AvailableSlotsResponse(
    Guid DoctorId, DateOnly Date, int DurationMinutes, IReadOnlyList<AvailableSlotResponse> Slots);

public sealed record AvailableSlotResponse(DateTime StartLocal, DateTime EndLocal);
```

Ejemplo: `GET /api/doctors/0193f1b8-…/available-slots?date=2026-11-03`

```json
{
  "doctorId": "0193f1b8-…",
  "date": "2026-11-03",
  "durationMinutes": 30,
  "slots": [
    { "startLocal": "2026-11-03T09:00:00", "endLocal": "2026-11-03T09:30:00" },
    { "startLocal": "2026-11-03T10:00:00", "endLocal": "2026-11-03T10:30:00" }
  ]
}
```

- `WithTags("Appointments")`, `WithName("GetAvailableSlots")` y `WithSummary("Get doctor available slots")`; `Produces<AvailableSlotsResponse>()` y `ProducesProblem` para 400, 401 y 404.
- El endpoint vive en `Endpoints/Appointments/` aunque la ruta cuelgue de `/api/doctors`: la query es del módulo Appointments.
- `date` ausente o mal formada falla al enlazar la petición: 400 de `BadHttpRequestException`, sin `ValidationErrors`.
- La respuesta se mapea con `Adapt<AvailableSlotsResponse>()`.

### Tests

| Archivo | Qué prueba |
| --- | --- |
| `tests/Dispensario.Application.UnitTests/Appointments/AppointmentSlotRulesTests.cs` (nuevo) | `GetScheduleSlots`: un bloque `09:00–12:00` con 30 minutos da 6 slots de `09:00` a `11:30`; un bloque `09:00–12:10` descarta el sobrante (último `11:30–12:00`); un bloque que empieza a `09:10` alinea desde `09:10`; dos bloques del mismo día salen ordenados; un día sin bloques → vacío; los bloques de otros días no cuentan; cada slot generado pasa `EnsureFitsSchedule` |
| `tests/Dispensario.Application.UnitTests/Appointments/GetAvailableSlotsHandlerTests.cs` (nuevo) | Con un `FakeTimeProvider`: un día futuro devuelve todos los slots del horario con `DurationMinutes`; una cita `Scheduled` y otra `Confirmed` quitan sus slots; una `Cancelled` no; una cita contigua no quita el slot vecino; una cita de otro doctor no cuenta; hoy solo devuelve los slots con inicio posterior a ahora (y no el que empieza justo ahora); fecha pasada → `BadRequestException`; doctor inexistente → `NotFoundException`; inactivo → `BadRequestException`; día sin horario → `Slots` vacío |
| `tests/Dispensario.Api.IntegrationTests/Appointments/AvailableSlotsEndpointsTests.cs` (nuevo) | Como `Receptionist`: un doctor con horario en un día futuro → 200 con los slots esperados y `durationMinutes`; tras agendar una cita, su slot desaparece; tras cancelarla, vuelve; día sin horario → 200 con `slots: []`; fecha pasada → 400; sin `date` y con `date` mal formada → 400; doctor inactivo → 400; inexistente → 404. Como `Doctor` (sin vincular) → 200. Sin token → 401. Los errores salen como `application/problem+json` |

- Convenciones de `TESTING.md`: `Method_Scenario_ExpectedResult`, Arrange / Act / Assert y Shouldly.
- Los tests de integración usan el reloj real: eligen una fecha futura (la próxima del día de la semana del horario, al menos una semana vista) con los helpers existentes de `DoctorRequests` y `AppointmentRequests`. El filtrado de "hoy" se prueba solo en el handler.

## Implementation plan

1. Añadir `GetScheduleSlots` a `AppointmentSlotRules`, con `AppointmentSlotRulesTests`. Verificar: `dotnet test` en verde y los tests existentes del alta y la reprogramación sin cambios.
2. Crear `AvailableSlotsDto`, `AvailableSlotDto`, `GetAvailableSlotsQuery` y `GetAvailableSlotsHandler`, con `GetAvailableSlotsHandlerTests`. Verificar: `dotnet test` en verde.
3. Crear `GetAvailableSlotsEndpoint` con `AvailableSlotsEndpointsTests`. Verificar: `dotnet test` en verde y la consulta desde Scalar devuelve los slots de un doctor con horario.
4. Actualizar la documentación:
   - `README.md` sección **Endpoints**: fila nueva en la tabla general y, en **Appointments** (o junto al horario de **Doctors**, con referencia cruzada), la ruta con acceso, parámetro `date`, ejemplo de respuesta, reglas (solo slots libres, alineados desde el inicio del bloque, futuros, sin cruces con citas `Scheduled`/`Confirmed` del doctor) y tabla de códigos de error.
   - `docs/guides/APPLICATION.md`: tabla "Módulo Appointments" con `GetAvailableSlots`, `GetScheduleSlots` en la descripción de `AppointmentSlotRules` y el orden del handler.
   - `docs/guides/API.md` §1: `GetAvailableSlotsEndpoint` en `Endpoints/Appointments/` (ruta bajo `/api/doctors`, autenticado).
   - `CLAUDE.md`: "Estado actual" con los slots disponibles (feature 14); "Detalles no obvios" con que la ruta cuelga de `/api/doctors` pero el código vive en Appointments, que solo mira los cruces del doctor (no del paciente), que fecha pasada e inactivo dan 400 y un día sin horario da `[]`.
   - `docs/PLAN.md`: la fila 14 apunta a `specs/14-available-slots.md`.

## Acceptance criteria

- [ ] `dotnet build Dispensario.slnx` termina sin errores y sin warnings nuevos respecto a SPEC 13.
- [ ] `dotnet test Dispensario.slnx` termina con éxito e incluye los tests de este spec y los anteriores, con `ScheduleAppointmentHandlerTests` y `RescheduleAppointmentHandlerTests` sin modificar.
- [ ] `GET /api/doctors/{id}/available-slots?date=` de un doctor activo con horario ese día devuelve 200 con `doctorId`, `date`, `durationMinutes` y los slots alineados desde el inicio de cada bloque, en hora local sin offset y ordenados por inicio.
- [ ] Un slot ocupado por una cita `Scheduled` o `Confirmed` del doctor no aparece; con la cita cancelada, vuelve a aparecer.
- [ ] Todo slot devuelto se puede agendar con `POST /api/appointments` (salvo cruce con otra cita del paciente).
- [ ] Para la fecha de hoy solo aparecen slots con inicio posterior a la hora actual.
- [ ] Un día sin bloques en el horario devuelve 200 con `slots: []`.
- [ ] Una fecha anterior a hoy (fecha local del dispensario), una `date` ausente o mal formada y un doctor inactivo devuelven 400; un doctor inexistente devuelve 404; sin token devuelve 401.
- [ ] Cualquier rol autenticado (`Admin`, `Receptionist`, `Doctor`, vinculado o no) obtiene 200.
- [ ] Todos los errores salen como `application/problem+json`.
- [ ] La ruta aparece en `/openapi/v1.json` con tag `Appointments`, nombre `GetAvailableSlots` y resumen.
- [ ] La sección **Endpoints** del `README.md` documenta la ruta con acceso, parámetro, respuesta y códigos de error.
- [ ] `APPLICATION.md`, `API.md` y `CLAUDE.md` describen los slots disponibles tal como se implementaron, y `docs/PLAN.md` fila 14 apunta a `specs/14-available-slots.md`.

## Decisions

- **Yes:** cualquier usuario autenticado consulta los slots de cualquier doctor (`RequireAuthorization()`), como `GET /api/doctors/{id}/schedule`. Decisión del usuario.
- **No:** limitar al `Doctor` a sus propios slots, o abrirlo solo a `Reception`. La respuesta no expone datos de pacientes. Descartado por el usuario.
- **Yes:** solo se devuelven slots libres: del horario, alineados, futuros y sin cruce con citas `Scheduled`/`Confirmed` del doctor. Decisión del usuario.
- **No:** devolver todos los slots con su estado. Expondría la ocupación y no lo necesita el alta. Descartado por el usuario.
- **Yes:** fecha pasada → 400 (`Date cannot be in the past.`); para hoy se filtran los slots ya empezados. Decisión del usuario.
- **No:** lista vacía para fechas pasadas. Descartado por el usuario.
- **Yes:** doctor inactivo → 400 (`Doctor is inactive.`), como en el alta; día sin horario → 200 con lista vacía; inexistente → 404. Decisión del usuario.
- **Yes:** la fecha pasada se comprueba antes de leer la BD, como la paginación de `GetAppointments`: con una fecha pasada y un doctor inexistente sale 400, no 404.
- **Yes:** respuesta como objeto con `doctorId`, `date`, `durationMinutes` y `slots` (`startLocal`, `endLocal`), sin horas UTC. Decisión del usuario.
- **No:** array plano o slots con horas UTC. Descartado por el usuario.
- **Yes:** query y endpoint en el módulo Appointments (tag `Appointments`) aunque la ruta cuelgue de `/api/doctors`: reutilizan `AppointmentSlotRules` y leen `Doctors` como ya hace el alta. Decisión del usuario.
- **No:** el endpoint en `Endpoints/Doctors/` o la query en el módulo Doctors, que tendría que leer Appointments. Descartado por el usuario.
- **Yes:** solo cuentan los cruces del doctor. Decisión del usuario.
- **No:** `patientId` opcional para excluir también los cruces del paciente. Descartado por el usuario; queda fuera de alcance.
- **Yes:** la generación vive en `AppointmentSlotRules.GetScheduleSlots`, junto a la regla de alineación que valida el alta, para que ambas no diverjan. Decisión del usuario.
- **No:** un método de dominio `Doctor.GetSlotsFor`. Descartado por el usuario.
- **Yes:** los cruces se calculan en memoria con las citas del doctor de ese día (una consulta), en vez de llamar a `EnsureNoOverlapsAsync` por slot: un día tiene como mucho unas decenas de slots y citas.
- **Yes:** el sobrante final de un bloque que no llega a la duración se descarta, igual que lo rechaza `IsAvailableAt` al agendar.

## Risks

| Risk | Mitigation |
| --- | --- |
| La generación de slots y la validación del alta divergen y se ofrece una hora que el alta rechaza | Ambas viven en `AppointmentSlotRules`; un test comprueba que cada slot generado pasa `EnsureFitsSchedule`, y el de integración agenda un slot devuelto. |
| Entre la consulta y el alta otra persona agenda el mismo slot | El alta sigue validando cruces y responde 409; no se reservan slots (fuera de alcance). |
| Con una zona con horario de verano, `ToUtc` lanza 400 en un slot de hora inexistente y la consulta del día falla | `America/Guayaquil` no tiene horario de verano; cambiar a una zona con DST requiere su propio spec. |
| Los tests de integración dependen del reloj real y del día de la semana | Usan una fecha calculada a una semana vista o más en el día del horario creado; "hoy" solo se prueba con `FakeTimeProvider`. |
| Un cambio de duración del doctor deja citas antiguas que no coinciden con la nueva rejilla | El cruce se calcula por solapamiento de rangos, no por igualdad de inicio: cualquier slot tocado por la cita se descarta. |

## What is **not** in this spec

- Cruces del paciente (`patientId`) y slots con estado.
- Rangos de fechas, varios doctores y horizonte máximo.
- Bloqueos de agenda, reservas temporales y horario de verano.
- Cambios en el dominio, la persistencia o los endpoints existentes.

Cada uno de ellos, si llega, va en su propio spec.
