# SPEC 16 — Evento de consulta cerrada y `Appointment.Complete`

> **Status:** Approved
> **Depends on:** SPEC 03, SPEC 13, SPEC 15
> **Date:** 2026-10-27
> **Objective:** Definir el tipo `ConsultationClosedDomainEvent` sin emitirlo y añadir al aggregate `Appointment` la transición `Complete`, ambos sin consumidores, como preparación del flujo "consulta cerrada → cita completada".

## Por qué este spec existe

La fila 16 del backlog prevé que, al cerrar una consulta, un handler de Appointments complete la cita con un domain event.
El usuario decide avanzar solo con las piezas de dominio: el contrato del evento y la transición de la cita.
El evento es un tipo **dummy**: existe, pero `Consultation.Close` no lo emite y nadie lo escucha.
`Appointment.Complete` existe y tiene tests, pero ningún handler ni endpoint lo llama.
Cerrar una consulta sigue sin cambiar la cita; el cableado (emitir el evento, el handler en `Appointments/EventHandlers` y sus tests) queda para un spec posterior.
Esto se aparta de la regla 7 de `DOMAIN.md` §2 (un evento se emite cuando alguien puede interesarse): aquí ni siquiera se emite, así que la regla no se rompe, pero queda un tipo sin uso hasta el siguiente spec.

## Scope

**In:**

- Record `ConsultationClosedDomainEvent(ConsultationId ConsultationId, AppointmentId? AppointmentId)` en `Dispensario.Domain/Consultations/Events/ConsultationClosedDomainEvent.cs`, sin emitirlo desde ningún sitio.
- Método `Appointment.Complete()` en `Dispensario.Domain/Appointments/Appointment.cs`: `Scheduled` o `Confirmed` → `Completed`, sin restricción de hora.
- Tests de dominio de `Complete` en `tests/Dispensario.Domain.UnitTests/Appointments/AppointmentTests.cs`.
- Test de dominio que fija que `Consultation.Close` no emite eventos, en `tests/Dispensario.Domain.UnitTests/Consultations/ConsultationTests.cs`.
- Documentación: `docs/guides/DOMAIN.md`, `docs/PLAN.md` (fila 16 anotada como parcial), `CLAUDE.md` y la nota de `status` del `README.md`.

**Out of scope (for future specs):**

- Emitir `ConsultationClosedDomainEvent` desde `Consultation.Close` o desde el handler.
- El handler `ConsultationClosedHandler` en `Dispensario.Application/Appointments/EventHandlers/` y cualquier `INotificationHandler`.
- Que cerrar una consulta complete la cita.
- Un endpoint o command que llame a `Complete`.
- `CompletedAtUtc` u otra columna nueva en `Appointments`.
- Cambios en Application, Infrastructure o Api.
- Migraciones (fila 17) y el test E2E con la cita `Completed` (fila 18).

## Data model

### `ConsultationClosedDomainEvent` (`Dispensario.Domain/Consultations/Events/ConsultationClosedDomainEvent.cs`, nuevo)

```csharp
namespace Dispensario.Domain.Consultations.Events;

// Se emitirá al cerrar una consulta; AppointmentId es null en una consulta sin cita.
// Hoy no se emite ni tiene consumidores (fila 16 parcial)
public sealed record ConsultationClosedDomainEvent(ConsultationId ConsultationId, AppointmentId? AppointmentId) : IDomainEvent;
```

- Sigue el patrón de `Users/Events/UserCreatedDomainEvent.cs`: record sellado, en la subcarpeta `Events` del módulo.
- Payload: solo `ConsultationId` y `AppointmentId?`, como ya documenta `DOMAIN.md`.
- `Consultation.Close` **no** cambia: no llama a `Raise`.

### Aggregate `Appointment` (`Dispensario.Domain/Appointments/Appointment.cs`, cambio)

```csharp
// Sin nowUtc: la consulta puede empezar antes de la hora de la cita
public void Complete()
```

| Método | Estado de origen | Momento | Estado final |
|---|---|---|---|
| `Complete` | `Scheduled`, `Confirmed` | sin restricción | `Completed` |

Reglas (`DomainException`):
- Estado distinto de `Scheduled`/`Confirmed` → `Only scheduled or confirmed appointments can be completed.`. Reutiliza `IsPending`.
- Si la regla falla, la cita no cambia.
- `Completed` es final: `Confirm`, `Cancel`, `MarkNoShow` y `Reschedule` ya lo rechazan con sus mensajes actuales (fila 13), sin cambios.
- `Complete` no toca `StartUtc`, `EndUtc`, `Reason` ni `CancellationReason`.

Máquina de estados resultante:
```
Scheduled ──Confirm──► Confirmed
    │                      │
    ├──Complete────────────┼──► Completed   (final)
    ├──Cancel──────────────┼──► Cancelled   (final)
    ├──MarkNoShow──────────┴──► NoShow      (final)
    └──Reschedule (Scheduled|Confirmed → Scheduled, con nuevas fechas)
```

### Persistencia

Sin cambios: `Status` ya se guarda como texto y `Completed` ya existe en `AppointmentStatus` desde la fila 12.
No hace falta borrar `dispensario.db`.

### Tests

`AppointmentTests` (nuevos):
- `Complete_WhenScheduled_SetsCompleted`.
- `Complete_WhenConfirmed_SetsCompleted`.
- `Complete_WhenCancelled_ThrowsDomainException`, `Complete_WhenNoShow_ThrowsDomainException` y `Complete_WhenCompleted_ThrowsDomainException`, con el mensaje exacto y el estado sin cambios.
- `Complete_BeforeStart_SetsCompleted`: una cita futura se completa (no hay restricción de hora).
- `Confirm_WhenCompleted_ThrowsDomainException`, `Cancel_WhenCompleted_ThrowsDomainException`, `MarkNoShow_WhenCompleted_ThrowsDomainException` y `Reschedule_WhenCompleted_ThrowsDomainException`: `Completed` es final.
- `Complete_DoesNotChangeTimesOrReason`.

`ConsultationTests` (nuevo):
- `Close_DoesNotRaiseDomainEvents`: tras `Close`, `DomainEvents` está vacío. Fija la decisión del evento dummy; el spec del consumidor lo cambiará.

## Implementation plan

1. Crear `Dispensario.Domain/Consultations/Events/ConsultationClosedDomainEvent.cs`. Compila con `dotnet build Dispensario.slnx`.
2. Añadir `Close_DoesNotRaiseDomainEvents` a `ConsultationTests`. `dotnet test tests/Dispensario.Domain.UnitTests --filter "FullyQualifiedName~ConsultationTests"` en verde.
3. Añadir `Appointment.Complete()` con su regla.
4. Añadir los tests de `Complete` y de la finalidad de `Completed` a `AppointmentTests`. `dotnet test tests/Dispensario.Domain.UnitTests --filter "FullyQualifiedName~AppointmentTests"` en verde.
5. Actualizar `docs/guides/DOMAIN.md`: método y regla de `Complete` en Appointments, máquina de estados sin "llega con la fila 16", evento de Consultations definido pero no emitido, y `Consultations/Events/ConsultationClosedDomainEvent.cs` en la estructura de carpetas.
6. Actualizar `CLAUDE.md` (estado actual con feature 16 parcial; notas de las features 13 y 15 sobre `Complete` y el evento) y la nota de `status` del `README.md` (cerrar una consulta aún no completa la cita).
7. Anotar `specs/16-consultation-closed-event-and-appointment-complete.md` en la fila 16 de `docs/PLAN.md`, marcada como parcial: el emisor y el handler en Appointments quedan pendientes.

## Acceptance criteria

- [ ] Existe `ConsultationClosedDomainEvent` en el namespace `Dispensario.Domain.Consultations.Events`, con `ConsultationId` y `AppointmentId?`, e implementa `IDomainEvent`.
- [ ] Ningún código de `src/` crea una instancia de `ConsultationClosedDomainEvent`.
- [ ] No existe ningún `INotificationHandler` en `src/`.
- [ ] `Consultation.Close` deja `DomainEvents` vacío (test en verde).
- [ ] `Appointment.Complete()` lleva una cita `Scheduled` o `Confirmed` a `Completed`, también antes de su hora de inicio.
- [ ] `Complete` sobre una cita `Cancelled`, `NoShow` o `Completed` lanza `DomainException` con `Only scheduled or confirmed appointments can be completed.` y la cita no cambia.
- [ ] `Confirm`, `Cancel`, `MarkNoShow` y `Reschedule` sobre una cita `Completed` lanzan `DomainException`.
- [ ] Cerrar una consulta con cita por `POST /api/consultations/{id}/close` deja la cita en su estado anterior (los tests de integración del spec 15 siguen en verde).
- [ ] No cambia ningún archivo de `Dispensario.Application`, `Dispensario.Infrastructure` ni `Dispensario.Api`.
- [ ] `dotnet build Dispensario.slnx` sin errores ni advertencias nuevas.
- [ ] `dotnet test Dispensario.slnx` en verde.
- [ ] `DOMAIN.md`, `CLAUDE.md`, `README.md` y la fila 16 de `PLAN.md` reflejan el estado parcial.

## Decisions

- **Yes:** el evento es solo un tipo; `Consultation.Close` no lo emite. Decisión del usuario.
- **No:** emitirlo sin handler, como `UserCreatedDomainEvent`. Descartado por el usuario: el emisor llega con el consumidor.
- **No:** emitirlo solo cuando hay cita. Descartado: no se emite en ningún caso.
- **Yes:** payload `(ConsultationId, AppointmentId?)`, el documentado en `DOMAIN.md`. Decisión del usuario.
- **No:** añadir `DoctorId`, `PatientId` o `ClosedAtUtc` al evento. Descartado por el usuario: el consumidor puede releer la consulta.
- **Yes:** `Appointment.Complete` entra ahora, solo en el dominio y con tests. Decisión del usuario.
- **No:** dejar `Complete` para el spec del consumidor. Descartado por el usuario.
- **Yes:** `Complete` desde `Scheduled` o `Confirmed`, como `Cancel` y `MarkNoShow`. Decisión del usuario: una cita no confirmada también se atiende.
- **No:** solo desde `Confirmed`. Descartado por el usuario.
- **Yes:** `Complete()` sin `nowUtc` ni restricción de hora. Decisión del usuario: la consulta puede iniciarse antes de la hora de la cita (el spec 15 solo exige que sea de hoy).
- **No:** `Complete(nowUtc)` solo desde el inicio, como `MarkNoShow`. Descartado: rompería el futuro handler al cerrar una consulta adelantada.
- **Yes:** solo cambia `Status`; sin `CompletedAtUtc`. Decisión del usuario: no cambia el modelo de EF y la hora de cierre ya está en la consulta.
- **Yes:** un estado inválido en `Complete` lanza `DomainException` (400), como el resto de transiciones de la fila 13.
- **Yes:** un test fija que `Close` no emite eventos, para que el cambio del spec del consumidor sea explícito.
- **Yes:** la fila 16 de `PLAN.md` se mantiene y se anota como parcial. Decisión del usuario.
- **No:** partir la fila 16 en dos filas. Descartado por el usuario.

## Risks

| Risk | Mitigation |
| --- | --- |
| El tipo del evento queda sin uso y alguien supone que cerrar la consulta ya completa la cita | `DOMAIN.md`, `CLAUDE.md`, `README.md` y la fila 16 de `PLAN.md` dicen explícitamente que no se emite ni se consume; el test `Close_DoesNotRaiseDomainEvents` lo fija. |
| `Complete` sin restricción de hora permite completar una cita de otro día cuando llegue el handler | El spec 15 ya exige que la cita de una consulta sea de hoy; el futuro handler solo completa citas enlazadas a una consulta. |
| El test E2E de la fila 18 espera la cita `Completed` | La fila 18 depende del spec que emita y consuma el evento; queda anotado en la fila 16 como pendiente. |

## What is **not** in this spec

- Emitir `ConsultationClosedDomainEvent`.
- El handler en `Appointments/EventHandlers` y cualquier `INotificationHandler`.
- Que cerrar una consulta complete la cita.
- Un endpoint o command para completar citas.
- `CompletedAtUtc` y cambios de persistencia.
- Cambios en Application, Infrastructure o Api.
- Migraciones y tests E2E.

Cada uno de ellos, si llega, va en su propio spec.
