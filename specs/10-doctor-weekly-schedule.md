# SPEC 10 — Doctors: horario semanal

> **Status:** Implemented
> **Depends on:** SPEC 07, SPEC 09
> **Date:** 2026-10-27
> **Objective:** Añadir al aggregate `Doctor` un horario semanal de bloques sin solapes, con `SetWeeklySchedule` e `IsAvailableAt`, y exponer su lectura y su reemplazo por el Admin o por el propio doctor.

## Por qué este spec existe

Agendar citas (fila 12) exige saber cuándo atiende cada doctor, y los slots disponibles (fila 14) se calculan sobre ese horario.
`DOMAIN.md` ya anticipaba el horario como entidad hija de `Doctor` con dos invariantes: sin solapes en el mismo día y cada bloque al menos tan largo como la duración de la cita.
El spec 09 dejó abierto qué pasa si la duración cambia con un horario cargado: este spec lo cierra (`Update` lo rechaza).
También cambia un nombre de los documentos: la entidad hija pasa de `WeeklySchedule` a `ScheduleBlock`, porque representa un bloque y no el horario entero.

## Scope

**In:**

- Value object `ScheduleBlock` (`DayOfWeek`, `StartTime`, `EndTime`) en `Dispensario.Domain/Doctors/`.
- En `Doctor`: colección `Schedule`, `SetWeeklySchedule(...)`, `IsAvailableAt(...)` y la comprobación del horario en `Update`.
- Persistencia con `OwnsMany` en la tabla `DoctorScheduleBlocks`.
- Command `SetDoctorSchedule` y query `GetDoctorSchedule` en `Dispensario.Application/Doctors/`.
- Endpoints `PUT /api/doctors/{id}/schedule` (Admin, o el doctor sobre su propia ficha) y `GET /api/doctors/{id}/schedule` (cualquier usuario autenticado).
- Nueva política `AdminOrDoctor`.
- `CustomExceptionHandler` (BuildingBlocks) convierte `BadHttpRequestException` en su código (400): un `dayOfWeek` desconocido daba 500 (añadido al implementar, decisión del usuario).
- Tests unitarios de dominio, persistencia, validador y handlers, y tests de integración de los dos endpoints.
- Documentación: `README.md` (**Endpoints**), `DOMAIN.md`, `APPLICATION.md`, `INFRASTRUCTURE.md`, `API.md`, `CONVENTIONS.md`, `CLAUDE.md` y `docs/PLAN.md`.

**Out of scope (for future specs):**

- Zona horaria del dispensario (`Clinic:TimeZone`) y conversión UTC ↔ hora local: llega con la agenda (filas 12 y 14).
- Alineación de una cita a los slots del bloque y cruces con otras citas: fila 12.
- Cálculo de slots disponibles (`GET /api/doctors/{id}/available-slots`): fila 14.
- Bloqueos de agenda (vacaciones, feriados) y excepciones por fecha concreta.
- Bloques que cruzan la medianoche.
- Edición parcial del horario (añadir o quitar un bloque suelto): el `PUT` reemplaza todo.
- Incluir el horario en `DoctorDto` o en `GET /api/doctors`.
- Que el doctor edite el resto de su ficha (sigue siendo `AdminOnly`).
- Domain events del horario.
- Migraciones: siguen con `EnsureCreated` hasta la fila 17.

## Data model

### Value object `ScheduleBlock` (`Dispensario.Domain/Doctors/ScheduleBlock.cs`, nuevo)

```csharp
public sealed record ScheduleBlock : ValueObject
{
    public const int TimeStepMinutes = 5;

    public DayOfWeek DayOfWeek { get; }
    public TimeOnly StartTime { get; }
    public TimeOnly EndTime { get; }

    public int DurationMinutes => (int)(EndTime - StartTime).TotalMinutes;

    public static ScheduleBlock Create(DayOfWeek dayOfWeek, TimeOnly startTime, TimeOnly endTime);
    public bool Overlaps(ScheduleBlock other);   // mismo día y Start < other.End && other.Start < End
    public bool Contains(TimeOnly start, TimeOnly end);   // StartTime <= start && end <= EndTime
}
```

Reglas de `Create` (`DomainException` → 400):

| Regla | Mensaje |
| --- | --- |
| `DayOfWeek` no definido | `Day of week is invalid.` |
| Hora con segundos o fracciones, o minutos que no son múltiplo de 5 | `Schedule times must be whole minutes and a multiple of 5.` |
| `StartTime >= EndTime` | `Schedule block start time must be before end time.` |

- Un bloque nunca cruza la medianoche: como `StartTime < EndTime` dentro de un `TimeOnly`, el máximo es `23:55`.
- Dos bloques contiguos (`09:00–12:00` y `12:00–14:00`) **no** se solapan y no se fusionan.

### Cambios en `Doctor` (`Dispensario.Domain/Doctors/Doctor.cs`)

```csharp
private readonly List<ScheduleBlock> _schedule = [];
public IReadOnlyCollection<ScheduleBlock> Schedule => _schedule.AsReadOnly();

public void SetWeeklySchedule(IEnumerable<ScheduleBlock> blocks);   // reemplaza todo; lista vacía borra el horario
public bool IsAvailableAt(DateTime localStart, DateTime localEnd);
```

`SetWeeklySchedule` comprueba, en este orden, y no cambia nada si algo falla (`DomainException` → 400):

| Regla | Mensaje |
| --- | --- |
| Doctor inactivo | `Doctor is inactive.` |
| Algún bloque dura menos que `AppointmentDurationMinutes` | `Each schedule block must last at least the appointment duration.` |
| Dos bloques del mismo día se solapan (incluidos dos bloques idénticos) | `Schedule blocks must not overlap.` |

`Update` (spec 09) añade una regla, comprobada antes de cambiar ningún campo:

| Regla | Mensaje |
| --- | --- |
| La nueva duración es mayor que algún bloque del horario actual | `Appointment duration cannot be longer than a schedule block.` |

`IsAvailableAt(localStart, localEnd)`:

- Recibe horas **locales del dispensario**; ignora `DateTime.Kind`. La conversión desde UTC la hará el handler de la fila 12.
- Devuelve `false` si `localStart >= localEnd` o si no caen en la misma fecha (`localStart.Date != localEnd.Date`).
- Devuelve `true` si algún bloque de `localStart.DayOfWeek` contiene entero el rango (`Contains(TimeOnly.FromDateTime(localStart), TimeOnly.FromDateTime(localEnd))`). Un rango que empieza en un bloque y termina en otro contiguo devuelve `false`.
- **No** mira `IsActive`, ni la alineación a slots, ni las citas existentes: eso lo decide el handler que lo use.

- `Create` deja el horario vacío.
- Sin domain events.

### Persistencia (`Dispensario.Infrastructure/Data/Configurations/Doctors/DoctorConfiguration.cs`)

`builder.OwnsMany(d => d.Schedule, ...)` con el campo `_schedule` como backing field, tabla `DoctorScheduleBlocks`:

| Columna | Origen | Notas |
| --- | --- | --- |
| `DoctorId` | FK del propietario | borrado en cascada (es el mismo aggregate) |
| `Id` | `int` en sombra | clave primaria autoincremental (SQLite no autoincrementa una clave compuesta con `DoctorId`; ajustado al implementar) |
| `DayOfWeek` | `DayOfWeek` | `HasConversion<string>()`, requerida |
| `StartTime`, `EndTime` | `TimeOnly` | TEXT en SQLite, requeridas |

- Sin índices adicionales.
- `AuditableEntityInterceptor` ya marca el `Doctor` como modificado cuando solo cambian sus bloques (`INFRASTRUCTURE.md` §4): no se toca.
- El esquema cambia: hay que **borrar `dispensario.db`** local antes de `dotnet run`.

### `Dispensario.Application/Doctors/` (namespaces iguales a las carpetas)

```
Doctors/
  Commands/
    SetDoctorSchedule/   SetDoctorScheduleCommand.cs  SetDoctorScheduleHandler.cs
  Queries/
    GetDoctorSchedule/   GetDoctorScheduleQuery.cs  GetDoctorScheduleHandler.cs
  Dtos/                  DoctorScheduleDto.cs
```

```csharp
public sealed record SetDoctorScheduleCommand(Guid DoctorId, IReadOnlyList<ScheduleBlockInput> Blocks) : ICommand;
public sealed record ScheduleBlockInput(DayOfWeek DayOfWeek, string StartTime, string EndTime);   // horas "HH:mm"

public sealed record GetDoctorScheduleQuery(Guid DoctorId) : IQuery<DoctorScheduleDto>;

public sealed record DoctorScheduleDto(Guid DoctorId, IReadOnlyList<ScheduleBlockDto> Blocks);
public sealed record ScheduleBlockDto(DayOfWeek DayOfWeek, string StartTime, string EndTime);   // horas "HH:mm"
```

### Validador (`SetDoctorScheduleCommandValidator`, en el archivo del command)

| Campo | Reglas |
| --- | --- |
| `DoctorId` | `NotEmpty` |
| `Blocks` | `NotNull` (una lista vacía es válida) |
| `Blocks[i]` | `NotNull` |
| `Blocks[i].DayOfWeek` | `IsInEnum` |
| `Blocks[i].StartTime`, `Blocks[i].EndTime` | `NotEmpty` y `Matches("^([01][0-9]|2[0-3]):[0-5][0-9]$")` con el mensaje `Time must use the HH:mm format.` |

- El múltiplo de 5, el orden inicio < fin, la duración mínima y los solapes son reglas de dominio (400 con `DomainException`), no del validador.

### Handlers

| Handler | Comportamiento, en orden |
| --- | --- |
| `SetDoctorScheduleHandler` | Recibe `IApplicationDbContext` e `ICurrentUser`. 1) Si `currentUser.Role == Role.Doctor` y `currentUser.DoctorId` no es el `DoctorId` del command (también si es `null`) → `ForbiddenException("You can only change your own schedule.")`, sin leer la BD. 2) Doctor no existe → `NotFoundException(nameof(Doctor), id)`. 3) Parsea cada hora con `TimeOnly.ParseExact(value, "HH:mm", CultureInfo.InvariantCulture)` y crea cada `ScheduleBlock`. 4) `doctor.SetWeeklySchedule(blocks)`. 5) Un único `SaveChangesAsync` |
| `GetDoctorScheduleHandler` | `AsNoTracking`; doctor no existe → `NotFoundException(nameof(Doctor), id)`. Devuelve `DoctorScheduleDto` con los bloques ordenados de lunes a domingo (`((int)day + 6) % 7`) y luego por `StartTime`, y las horas formateadas `"HH:mm"`. Sin horario → `Blocks` vacía. También responde para un doctor inactivo. El orden se hace en memoria tras leer el doctor: son pocos bloques de un solo doctor |

- El 403 va **antes** del 404: un `Doctor` no puede averiguar si existe el ID de otro doctor.
- `ValidationBehavior` corre antes que el handler: un cuerpo mal formado da 400 aunque el `Doctor` no sea el dueño.
- `UpdateDoctorHandler` no cambia: la nueva regla vive en `Doctor.Update`, y EF carga los bloques propios junto con el doctor.

### `Dispensario.Api`

`Authorization/Policies.cs` y `Extensions/ApiServiceExtensions.cs`: nueva política `AdminOrDoctor` = `RequireRole(Admin, Doctor)`.

```
Endpoints/Doctors/
  SetDoctorScheduleEndpoint.cs  GetDoctorScheduleEndpoint.cs
```

| Ruta | Request → Response | Autorización | Respuestas |
| --- | --- | --- | --- |
| `PUT /api/doctors/{id:guid}/schedule` | `SetDoctorScheduleRequest(IReadOnlyList<ScheduleBlockRequest> Blocks)` con `ScheduleBlockRequest(DayOfWeek DayOfWeek, string StartTime, string EndTime)` | `AdminOrDoctor` + comprobación de dueño en el handler | 204, 400, 401, 403, 404 |
| `GET /api/doctors/{id:guid}/schedule` | — → `DoctorScheduleResponse(Guid DoctorId, IReadOnlyList<ScheduleBlockResponse> Blocks)` | `RequireAuthorization()` | 200, 401, 404 |

Ejemplo de cuerpo (`dayOfWeek` viaja como texto por el `JsonStringEnumConverter` global):

```json
{
  "blocks": [
    { "dayOfWeek": "Monday", "startTime": "09:00", "endTime": "12:00" },
    { "dayOfWeek": "Monday", "startTime": "14:00", "endTime": "18:00" }
  ]
}
```

- Un `dayOfWeek` que no es un `DayOfWeek` no llega al validador: falla la lectura del JSON y ASP.NET lanza `BadHttpRequestException`. `CustomExceptionHandler` la convierte en un ProblemDetails con el `StatusCode` de la excepción (400), sin `ValidationErrors`. Antes caía en 500, en este y en todos los endpoints.
- Ambos con `WithTags("Doctors")`, `WithName` igual al command o query (`SetDoctorSchedule`, `GetDoctorSchedule`) y `WithSummary`.

### Tests

| Archivo | Qué prueba |
| --- | --- |
| `tests/BuildingBlocks.UnitTests/Exceptions/CustomExceptionHandlerTests.cs` (amplía) | `BadHttpRequestException` → 400 con título `BadHttpRequestException`; con 413 → 413 |
| `tests/Dispensario.Domain.UnitTests/Doctors/ScheduleBlockTests.cs` (nuevo) | `Create` válido; día fuera del enum, `09:03`, hora con segundos e inicio igual o posterior al fin → `DomainException`; `DurationMinutes`; `Overlaps` con solape parcial, contenido e idéntico → true; contiguos y días distintos → false |
| `tests/Dispensario.Domain.UnitTests/Doctors/DoctorTests.cs` (amplía) | `Create` deja el horario vacío. `SetWeeklySchedule` reemplaza el horario; lista vacía lo borra; contiguos se aceptan; solape → `DomainException` y el horario anterior no cambia; bloque más corto que la duración → `DomainException`; doctor inactivo → `DomainException`. `Update` con una duración mayor que un bloque → `DomainException` y no cambia nada; igual a la del bloque más corto → pasa. `IsAvailableAt`: dentro de un bloque → true; justo en los bordes → true; fuera, otro día, rango que salta entre dos bloques contiguos, inicio ≥ fin y fechas distintas → false |
| `tests/Dispensario.Application.UnitTests/Persistence/Doctors/DoctorPersistenceTests.cs` (amplía) | Ida y vuelta de un doctor con bloques (día como texto, horas exactas); reemplazar el horario borra las filas anteriores; cambiar solo los bloques actualiza `LastModifiedAtUtc` |
| `tests/Dispensario.Application.UnitTests/Doctors/SetDoctorScheduleCommandValidatorTests.cs` (nuevo) | `DoctorId` vacío, `Blocks` null, día fuera del enum, `"9:00"`, `"24:00"` y `"09:00:00"` fallan; lista vacía y un comando correcto pasan |
| `tests/Dispensario.Application.UnitTests/Doctors/SetDoctorScheduleHandlerTests.cs` (nuevo) | Admin fija el horario de cualquier doctor; un `Doctor` fija el suyo; un `Doctor` sobre otro doctor o sin `DoctorId` → `ForbiddenException` también si el ID no existe; inexistente → `NotFoundException`; solape → `DomainException`; doctor inactivo → `DomainException` |
| `tests/Dispensario.Application.UnitTests/Doctors/GetDoctorScheduleHandlerTests.cs` (nuevo) | Bloques ordenados lunes → domingo y por hora (un bloque de domingo sale el último); horas en `"HH:mm"`; sin horario → lista vacía; inexistente → `NotFoundException` |
| `tests/Dispensario.Application.UnitTests/Doctors/UpdateDoctorHandlerTests.cs` (amplía) | Duración mayor que un bloque del horario → `DomainException` |
| `tests/Dispensario.Api.IntegrationTests/Common/DoctorRequests.cs` (amplía) | Helper que crea un usuario `Doctor`, lo vincula a un doctor y devuelve un cliente con su token |
| `tests/Dispensario.Api.IntegrationTests/Doctors/DoctorScheduleEndpointsTests.cs` (nuevo) | Como Admin: `PUT` → 204 y `GET` devuelve los bloques ordenados con `dayOfWeek` como texto; `PUT` con `[]` lo vacía; solape → 400; `"9:00"` → 400 con errores de validación; doctor inexistente → 404; doctor inactivo → 400; `PUT /api/doctors/{id}` con una duración mayor que un bloque → 400. Como `Doctor` vinculado: `PUT` sobre su ficha → 204 y sobre otra → 403. Como `Doctor` sin vincular → 403. Como `Receptionist`: `GET` → 200 y `PUT` → 403. Sin token → 401 |

- Convenciones de `TESTING.md`: `Method_Scenario_ExpectedResult`, Arrange / Act / Assert y Shouldly.

## Implementation plan

1. Crear `ScheduleBlock` con `ScheduleBlockTests`. Verificar: `dotnet test` en verde.
2. Añadir a `Doctor` la colección `Schedule`, `SetWeeklySchedule`, `IsAvailableAt` y la regla nueva de `Update`, y actualizar su comentario de cabecera. Ampliar `DoctorTests`. Verificar: `dotnet test` en verde.
3. Mapear `Schedule` con `OwnsMany` en `DoctorConfiguration` y ampliar `DoctorPersistenceTests`. Verificar: `dotnet test` en verde y, tras borrar `dispensario.db`, `dotnet run` crea `DoctorScheduleBlocks`.
4. Crear `SetDoctorSchedule` (command, validador y handler), el mapeo de `BadHttpRequestException` en `CustomExceptionHandler` con sus tests, la política `AdminOrDoctor` y `SetDoctorScheduleEndpoint`, con `SetDoctorScheduleCommandValidatorTests`, `SetDoctorScheduleHandlerTests`, el helper de `DoctorRequests` y los casos de `PUT` de `DoctorScheduleEndpointsTests`. Verificar: `dotnet test` en verde.
5. Crear `DoctorScheduleDto`, `GetDoctorSchedule` (query y handler) y `GetDoctorScheduleEndpoint`, con `GetDoctorScheduleHandlerTests` y los casos de `GET`. Añadir el caso de duración a `UpdateDoctorHandlerTests` y su caso de integración. Verificar: `dotnet test` en verde.
6. Actualizar la documentación:
   - `README.md` sección **Endpoints**: filas nuevas en la tabla general y, en **Doctors**, los apartados `PUT /api/doctors/{id}/schedule` y `GET /api/doctors/{id}/schedule` con acceso, ejemplo de petición y respuesta, y tabla de códigos de error. En `PUT /api/doctors/{id}`, el nuevo 400 por duración mayor que un bloque.
   - `docs/guides/DOMAIN.md` §4 Doctors: `Schedule : IReadOnlyCollection<ScheduleBlock>` en lugar de `Schedules`/`WeeklySchedule`, reglas de `ScheduleBlock`, `SetWeeklySchedule` (inactivo, duración mínima, solapes, lista vacía), la regla nueva de `Update` y la semántica de `IsAvailableAt`. §5: `ScheduleBlock.cs` en `Doctors/`.
   - `docs/guides/APPLICATION.md`: casos de uso `SetDoctorSchedule` y `GetDoctorSchedule` en la tabla del módulo Doctors; en §6, el paso 4 de `ScheduleAppointment` usa `Doctor.IsAvailableAt` con horas locales.
   - `docs/guides/INFRASTRUCTURE.md`: `OwnsMany` en la tabla `DoctorScheduleBlocks` (en lugar de `DoctorSchedules`) y sus columnas.
   - `docs/guides/API.md`: política `AdminOrDoctor` y los dos endpoints nuevos en `Endpoints/Doctors/`.
   - `docs/guides/CONVENTIONS.md`: el ejemplo de tabla `DoctorSchedules` pasa a `DoctorScheduleBlocks`.
   - `CLAUDE.md`: "Estado actual" con el horario semanal (feature 10); "Detalles no obvios" con el reemplazo completo del horario, horas `"HH:mm"` múltiplo de 5, `IsAvailableAt` con horas locales y sin mirar `IsActive`, el 403 antes del 404 para un `Doctor`, y que `PUT /api/doctors/{id}` rechaza una duración mayor que un bloque.
   - `docs/PLAN.md`: la fila 10 apunta a `specs/10-doctor-weekly-schedule.md`.

## Acceptance criteria

- [ ] `dotnet build Dispensario.slnx` termina sin errores y sin warnings nuevos respecto a SPEC 09.
- [ ] `dotnet test Dispensario.slnx` termina con éxito e incluye los tests de este spec y los anteriores.
- [ ] Tras borrar `dispensario.db`, `dotnet run` crea la tabla `DoctorScheduleBlocks` con `DoctorId`, `Id`, `DayOfWeek` (texto), `StartTime` y `EndTime`.
- [ ] `PUT /api/doctors/{id}/schedule` como Admin responde 204 y `GET /api/doctors/{id}/schedule` devuelve exactamente los bloques enviados, ordenados de lunes a domingo y por hora, con horas `"HH:mm"`.
- [ ] Un segundo `PUT` reemplaza el horario entero, y un `PUT` con `blocks: []` lo deja vacío.
- [ ] Dos bloques solapados del mismo día devuelven 400; dos bloques contiguos se aceptan.
- [ ] Un bloque más corto que `appointmentDurationMinutes`, con minutos que no son múltiplo de 5 o con inicio ≥ fin devuelve 400.
- [ ] Una hora que no sigue `"HH:mm"` devuelve 400 con errores de validación; un `dayOfWeek` inválido devuelve 400 (ProblemDetails de `BadHttpRequestException`).
- [ ] Fijar el horario de un doctor inactivo devuelve 400; leerlo devuelve 200.
- [ ] `PUT /api/doctors/{id}` con una duración mayor que algún bloque del horario devuelve 400 y no cambia el doctor.
- [ ] Un usuario `Doctor` vinculado puede fijar su propio horario (204) y recibe 403 sobre otro doctor, también si ese ID no existe; un `Doctor` sin vincular recibe 403.
- [ ] `Receptionist` recibe 403 en el `PUT`; cualquier rol autenticado recibe 200 en el `GET`; sin token ambos responden 401; un ID inexistente da 404 al Admin.
- [ ] `Doctor.IsAvailableAt` devuelve true solo si el rango local cabe entero en un bloque de ese día, según `DoctorTests`.
- [ ] Todos los errores salen como `application/problem+json`.
- [ ] Las dos rutas nuevas aparecen en `/openapi/v1.json` con tag `Doctors`, nombre y resumen.
- [ ] La sección **Endpoints** del `README.md` documenta las dos rutas nuevas y el nuevo 400 de `PUT /api/doctors/{id}`.
- [ ] `DOMAIN.md`, `APPLICATION.md`, `INFRASTRUCTURE.md`, `API.md`, `CONVENTIONS.md` y `CLAUDE.md` usan `ScheduleBlock` y `DoctorScheduleBlocks` (ya no `WeeklySchedule` ni `DoctorSchedules`), y `docs/PLAN.md` fila 10 apunta a `specs/10-doctor-weekly-schedule.md`.

## Decisions

- **Yes:** `PUT /api/doctors/{id}/schedule` reemplaza el horario entero y `GET /api/doctors/{id}/schedule` lo devuelve; `DoctorDto` no cambia. Decisión del usuario.
- **No:** el horario dentro de `DoctorDto`. Engordaría el listado paginado o obligaría a dos DTOs. Descartado por el usuario.
- **Yes:** escriben el Admin o el propio doctor (política `AdminOrDoctor` y comprobación de `ICurrentUser.DoctorId` en el handler). Decisión del usuario.
- **Yes:** el 403 del `Doctor` ajeno va antes del 404, sin leer la BD: no revela qué IDs existen. Decisión del usuario.
- **Yes:** lectura para cualquier usuario autenticado, como el resto de lecturas de doctores. Decisión del usuario.
- **Yes:** `Doctor.Update` rechaza una duración mayor que algún bloque: el invariante se cumple siempre. Decisión del usuario.
- **No:** comprobar la duración solo en `SetWeeklySchedule`. Dejaría bloques sin slots y un invariante débil. Descartado por el usuario.
- **Yes:** la entidad hija se llama `ScheduleBlock`, la colección `Schedule` y la tabla `DoctorScheduleBlocks`; se actualizan las guías. Decisión del usuario.
- **Yes:** `ScheduleBlock` es un value object (`sealed record`) mapeado con `OwnsMany`. No tiene identidad propia: el horario se reemplaza entero y dos bloques iguales son el mismo bloque. Propuesta de este spec; `DOMAIN.md` hablaba de entidad hija.
- **Yes:** horas en minutos exactos y múltiplo de 5, coherente con la duración de la cita. Decisión del usuario.
- **Yes:** bloques contiguos permitidos y sin fusionar; ningún bloque cruza la medianoche. Decisión del usuario.
- **Yes:** `IsAvailableAt` solo comprueba que el rango local cabe entero en un bloque; no mira `IsActive`, alineación ni citas. Decisión del usuario.
- **No:** que `IsAvailableAt` exija doctor activo o alinee a slots. Lo deciden los handlers de la fila 12. Descartado por el usuario.
- **Yes:** `IsAvailableAt` recibe horas locales; la zona horaria (`Clinic:TimeZone`) llega con la agenda. Mantiene el dominio sin zona horaria.
- **Yes:** lista vacía borra el horario. Decisión del usuario.
- **Yes:** fijar el horario de un doctor inactivo devuelve 400 (`Doctor is inactive.`). Decisión del usuario.
- **No:** máximo de bloques en el validador. No lo pidió el usuario.
- **Yes:** mensajes de error genéricos (sin día ni bloques concretos). Decisión del usuario.
- **Yes:** horas como texto `"HH:mm"` validado con regex, y orden de lunes a domingo en el `GET`. Decisión del usuario.
- **No:** `TimeOnly` nativo en el request y orden domingo → sábado. Descartado por el usuario.
- **Yes:** las reglas de horario (múltiplo de 5, inicio < fin, duración, solapes) viven en el dominio; el validador solo comprueba la forma (`DOMAIN.md` §2 regla 9).
- **No:** domain events del horario. Nadie reacciona hoy.
- **Yes:** `CustomExceptionHandler` mapea `BadHttpRequestException` a su `StatusCode`. Al implementar se vio que un enum desconocido en el JSON daba 500 en todos los endpoints. Decisión del usuario.
- **No:** recibir `dayOfWeek` como texto y validarlo con `Enum.TryParse` para tener `ValidationErrors`. Solo arreglaba este endpoint. Descartado por el usuario.
- **Yes:** `DoctorScheduleBlocks.Id` es la clave primaria sola, no compuesta con `DoctorId`: SQLite solo autoincrementa un `INTEGER PRIMARY KEY`. Ajustado al implementar.

## Risks

| Risk | Mitigation |
| --- | --- |
| EF puede no reemplazar bien una colección `OwnsMany` de records con igualdad por valor (dos bloques iguales, borrar y reinsertar) | El paso 3 lo comprueba con `DoctorPersistenceTests` (reemplazo completo). Si falla, `ScheduleBlock` pasa a `sealed class` con la misma API. |
| Un token emitido antes del vínculo no trae `doctor_id`: el doctor recibe 403 al fijar su horario | Es el comportamiento del spec 09: hace falta un nuevo login. Documentado en `CLAUDE.md` y `README.md`. |
| Un usuario `Doctor` desactivado conserva su token hasta que caduca y puede seguir cambiando su horario | Igual que el resto de endpoints (solo `/api/auth/me` y `/api/users/me*` lo comprueban). Aceptado en v1. |
| `TimeOnly` en SQLite se guarda como TEXT | EF Core SQLite lo soporta y compara bien en formato fijo; aquí las comparaciones se hacen en memoria, en el dominio. |
| Bajar la duración nunca falla, pero subirla puede bloquear la edición del doctor | El 400 con mensaje claro indica que hay que ajustar el horario antes. |
| El esquema cambia y una `dispensario.db` local antigua no tiene `DoctorScheduleBlocks` | Borrar `dispensario.db` antes de `dotnet run` (`EnsureCreated` hasta la fila 17). |

## What is **not** in this spec

- Zona horaria del dispensario y conversión UTC ↔ local.
- Alineación a slots, cruces de citas y cálculo de slots disponibles.
- Bloqueos de agenda, feriados y excepciones por fecha.
- Bloques que cruzan la medianoche y edición parcial del horario.
- Horario dentro de `DoctorDto` o del listado.
- Edición del resto de la ficha por el propio doctor.
- Domain events del horario y migraciones.

Cada uno de ellos, si llega, va en su propio spec.
