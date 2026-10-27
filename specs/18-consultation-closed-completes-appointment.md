# SPEC 18 — Cerrar la consulta completa la cita

> **Status:** Approved
> **Depends on:** SPEC 04, SPEC 15, SPEC 16
> **Date:** 2026-10-27
> **Objective:** Hacer que `Consultation.Close` emita `ConsultationClosedDomainEvent` y que un handler en `Appointments/EventHandlers` lleve la cita enlazada a `Completed`.

## Por qué este spec existe

El spec 16 dejó la fila 16 del backlog a medias: el tipo del evento y `Appointment.Complete` existen, pero nadie los usa.
Este spec cierra la fila 16 y, con ella, el flujo de negocio del backend.
Se usa el domain event porque la regla 4 de `docs/ARCHITECTURE.md` prohíbe que un handler de Consultations modifique un aggregate de Appointments.
Se consideró completar la cita directamente desde `CloseConsultationHandler` y se descartó por esa regla (ver Decisions).
Los eventos son **en proceso**: los publica MediatR con el `DispatchDomainEventsInterceptor` que ya existe (fila 04), sin RabbitMQ, Kafka ni ningún broker.
El interceptor publica **después** del commit, así que la consulta y la cita se guardan en dos `SaveChangesAsync` distintos.
Por eso el handler no falla si la cita ya no se puede completar: la consulta ya está cerrada y un error no la desharía.

## Scope

**In:**

- `Consultation.Close` emite `ConsultationClosedDomainEvent(Id, AppointmentId)` siempre, con o sin cita.
- Handler `ConsultationClosedHandler` en `Dispensario.Application/Appointments/EventHandlers/ConsultationClosedHandler.cs`: completa la cita si está `Scheduled` o `Confirmed`.
- Si la cita no existe o ya no está pendiente, el handler la ignora y registra un `Warning` con `ILogger`.
- Tests de dominio: `Close` emite el evento (sustituye a `Close_DoesNotRaiseDomainEvents`) y un `Close` fallido no emite nada.
- Tests de Application: `ConsultationClosedHandlerTests` en `tests/Dispensario.Application.UnitTests/Appointments/`.
- Tests de integración en `ConsultationsEndpointsTests`: cerrar una consulta con cita deja la cita `Completed`; con la cita `NoShow`, la consulta se cierra y la cita no cambia.
- Documentación: `docs/guides/DOMAIN.md`, `docs/guides/APPLICATION.md`, `docs/PLAN.md` (fila 16 completa), `CLAUDE.md` y `README.md` (nota de `status` de la cita, ciclo de vida de la consulta y `POST /api/consultations/{id}/close`).

**Out of scope (for future specs):**

- Tests E2E de la fila 18 del backlog (login → doctor → paciente → cita → consulta → cita `Completed`, más 401/403/404/409).
- Publicar los eventos antes del commit o en la misma transacción (cambio de `DispatchDomainEventsInterceptor`).
- Outbox, reintentos o un broker (RabbitMQ, Kafka, MassTransit).
- Un endpoint o command para completar citas a mano.
- `CompletedAtUtc` u otra columna nueva en `Appointments`.
- Otros consumidores del evento (notificaciones, estadísticas).
- Eventos de Appointments (`AppointmentScheduledDomainEvent`, etc.).

## Data model

Este spec no introduce estructuras de datos nuevas ni cambia el modelo de EF: no hay migración.
Reutiliza `ConsultationClosedDomainEvent` y `Appointment.Complete` del spec 16.

### `Consultation.Close` (`Dispensario.Domain/Consultations/Consultation.cs`, cambio)

- Tras pasar a `Closed`, llama a `Raise(new ConsultationClosedDomainEvent(Id, AppointmentId))`.
- Se emite siempre: en una consulta sin cita, `AppointmentId` es `null`.
- Si una regla de `Close` falla (`DomainException`), no se emite nada.
- `Closed` es final, así que el evento sale como mucho una vez por consulta.
- Se quita el comentario del tipo del evento que dice que hoy no se emite.

### `ConsultationClosedHandler` (`Dispensario.Application/Appointments/EventHandlers/ConsultationClosedHandler.cs`, nuevo)

```csharp
// Completa la cita enlazada a una consulta cerrada; corre después del commit de la consulta
public sealed class ConsultationClosedHandler(
    IApplicationDbContext db,
    ILogger<ConsultationClosedHandler> logger)
    : INotificationHandler<ConsultationClosedDomainEvent>
```

| Caso | Acción |
|---|---|
| `AppointmentId` es `null` | Nada, sin log |
| La cita no existe | Nada, `Warning` |
| La cita está `Cancelled`, `NoShow` o `Completed` | Nada, `Warning` (la cita no cambia) |
| La cita está `Scheduled` o `Confirmed` | `appointment.Complete()` y `SaveChangesAsync` |

- Comprueba el estado antes de llamar a `Complete`, así que en los casos ignorados no hay `DomainException`.
- Mensajes de log (en inglés, con plantilla estructurada):
  - `Appointment {AppointmentId} of closed consultation {ConsultationId} was not found.`
  - `Appointment {AppointmentId} of closed consultation {ConsultationId} is {Status} and was not completed.`
- Lo registra MediatR al escanear el ensamblado de Application en `AddApplication`; no hace falta registrarlo a mano.
- Vive en Appointments porque es el módulo que cambia su propio aggregate (regla 4 de `ARCHITECTURE.md`).
- `CloseConsultationHandler` no cambia: sigue con un solo `SaveChangesAsync` y no conoce Appointments.

### Flujo

```
POST /api/consultations/{id}/close
  → CloseConsultationHandler: consultation.Close(...) → SaveChangesAsync (commit de la consulta)
     → DispatchDomainEventsInterceptor.SavedChangesAsync → IPublisher.Publish(ConsultationClosedDomainEvent)
        → ConsultationClosedHandler: appointment.Complete() → SaveChangesAsync (commit de la cita)
  ← 204
```

### Tests

`ConsultationTests` (Domain):
- `Close_DoesNotRaiseDomainEvents` se sustituye por `Close_WithAppointment_RaisesConsultationClosedDomainEvent`: un único evento con el `Id` y el `AppointmentId` de la consulta.
- `Close_WithoutAppointment_RaisesConsultationClosedDomainEventWithNullAppointment`.
- `Close_WhenRuleFails_RaisesNoDomainEvents`: sin diagnóstico o ya cerrada, `DomainEvents` sigue vacío.
- `Start_RaisesNoDomainEvents` y `Update_RaisesNoDomainEvents`, si no existen ya.

`ConsultationClosedHandlerTests` (Application, SQLite en memoria, `NullLogger<ConsultationClosedHandler>`):
- `Handle_WithScheduledAppointment_CompletesAppointment`.
- `Handle_WithConfirmedAppointment_CompletesAppointment`.
- `Handle_WithoutAppointment_DoesNothing`.
- `Handle_WithUnknownAppointment_DoesNotThrow`.
- `Handle_WithCancelledAppointment_KeepsStatus`, `Handle_WithNoShowAppointment_KeepsStatus` y `Handle_WithCompletedAppointment_KeepsStatus`.

`ConsultationsEndpointsTests` (integración):
- `CloseConsultation_WithAppointment_CompletesAppointment`: tras el 204, `GET /api/appointments/{id}` devuelve `status: "Completed"`.
- `CloseConsultation_WithNoShowAppointment_ClosesConsultationAndKeepsNoShow`: se inicia la consulta, se marca la cita `NoShow` y el cierre responde 204 con la cita `NoShow`.
- `StartConsultation_WithAppointmentOfToday_ReturnsCreatedAndKeepsAppointmentScheduled` se mantiene: iniciar no cambia la cita.

## Implementation plan

1. Emitir el evento en `Consultation.Close` y quitar el comentario de "no se emite" en `ConsultationClosedDomainEvent`. Sustituir `Close_DoesNotRaiseDomainEvents` por los tests nuevos de `ConsultationTests`. `dotnet test tests/Dispensario.Domain.UnitTests --filter "FullyQualifiedName~ConsultationTests"` en verde.
2. Crear `Appointments/EventHandlers/ConsultationClosedHandler.cs`. `dotnet build Dispensario.slnx` sin errores.
3. Añadir `ConsultationClosedHandlerTests`. `dotnet test tests/Dispensario.Application.UnitTests --filter "FullyQualifiedName~ConsultationClosedHandlerTests"` en verde.
4. Añadir los dos tests de integración a `ConsultationsEndpointsTests`. `dotnet test tests/Dispensario.Api.IntegrationTests --filter "FullyQualifiedName~ConsultationsEndpointsTests"` en verde.
5. Actualizar `docs/guides/DOMAIN.md`: `Close` emite el evento y `Complete` lo llama el handler de Appointments (secciones de Appointments y Consultations).
6. Actualizar `docs/guides/APPLICATION.md`: fila de `CloseConsultationCommand` (emite el evento vía `Close`) y fila nueva de `ConsultationClosedHandler` en Appointments.
7. Actualizar `README.md`: nota de `status` de la cita, nota del ciclo de vida de la consulta ("Cerrar una consulta aún no completa la cita") y descripción de `POST /api/consultations/{id}/close` (cita `Scheduled`/`Confirmed` → `Completed`; cualquier otra no cambia).
8. Actualizar `CLAUDE.md` (estado actual con la feature 16 completa; notas de las features 13 y 15 sobre `Complete` y el evento) y anotar en la fila 16 de `docs/PLAN.md` los specs 16 y 18, ya sin "parcial".

## Acceptance criteria

- [ ] `Consultation.Close` deja en `DomainEvents` un único `ConsultationClosedDomainEvent` con el `Id` y el `AppointmentId` de la consulta, también cuando `AppointmentId` es `null`.
- [ ] Un `Close` que lanza `DomainException` no deja eventos.
- [ ] Existe `ConsultationClosedHandler` en el namespace `Dispensario.Application.Appointments.EventHandlers` e implementa `INotificationHandler<ConsultationClosedDomainEvent>`.
- [ ] Cerrar por `POST /api/consultations/{id}/close` una consulta con cita `Scheduled` o `Confirmed` responde 204 y deja la cita `Completed`.
- [ ] Cerrar una consulta con la cita `NoShow` responde 204, la consulta queda `Closed` y la cita sigue `NoShow`.
- [ ] Con la cita inexistente o no pendiente, el handler no lanza excepción, no guarda y registra un `Warning`.
- [ ] Cerrar una consulta sin cita responde 204 y no cambia ninguna cita.
- [ ] Iniciar una consulta con cita deja la cita `Scheduled` (el test del spec 15 sigue en verde).
- [ ] `CloseConsultationHandler` no lee ni escribe `Appointments`.
- [ ] No cambia `DispatchDomainEventsInterceptor` ni ningún archivo de `Dispensario.Infrastructure` o `Dispensario.Api`.
- [ ] `MigrationsTests.Model_HasNoPendingChanges` sigue en verde (sin migración nueva).
- [ ] `dotnet build Dispensario.slnx` sin errores ni advertencias nuevas.
- [ ] `dotnet test Dispensario.slnx` en verde.
- [ ] `DOMAIN.md`, `APPLICATION.md`, `README.md`, `CLAUDE.md` y la fila 16 de `PLAN.md` dicen que cerrar una consulta completa la cita.

## Decisions

- **Yes:** domain event en proceso con MediatR (`INotification`) y el interceptor existente. No necesita infraestructura nueva y respeta la regla 4 de `ARCHITECTURE.md`.
- **No:** RabbitMQ, Kafka u otro broker. Decisión del usuario: son para integration events entre servicios separados, y aquí todo corre en un solo proceso.
- **No:** completar la cita directamente desde `CloseConsultationHandler`. Lo planteó primero el usuario. Daría atomicidad (un solo `SaveChangesAsync`), pero rompe la regla 4 y luego habría que migrarlo al evento.
- **Yes:** mantener el despacho después del commit. Decisión del usuario: no toca Infrastructure ni cambia el comportamiento de `UserCreatedDomainEvent`.
- **No:** publicar en `SavingChangesAsync` para guardar consulta y cita en la misma transacción. Descartado: cambia la infraestructura común y sus tests.
- **Yes:** el evento se emite siempre, también sin cita. Decisión del usuario: el hecho "consulta cerrada" ocurre igual, y el handler de Appointments ignora el `null`. Revierte el "no se emite en ningún caso" del spec 16.
- **No:** emitirlo solo cuando hay cita. Descartado por el usuario.
- **Yes:** una cita inexistente o no pendiente se ignora y se registra un `Warning`. Decisión del usuario: la consulta ya está guardada, y fallar devolvería un error de un cierre que sí ocurrió.
- **No:** dejar que `Complete` lance la `DomainException` y el cliente reciba 400. Descartado por el usuario.
- **No:** ignorar sin log. Descartado por el usuario: se pierde el rastro de la incoherencia.
- **Yes:** el handler se llama `ConsultationClosedHandler`, como lo nombra el spec 16 y la convención `<Evento>Handler.cs` de `APPLICATION.md`.
- **Yes:** los tests E2E de la fila 18 del backlog van en un spec aparte. Decisión del usuario.

## Risks

| Risk | Mitigation |
| --- | --- |
| Falla el `SaveChangesAsync` del handler (error de BD): la consulta queda `Closed`, la cita no se completa y el cliente recibe 500 | Aceptado: es el coste del despacho post-commit, ya documentado en `INFRASTRUCTURE.md` (`DispatchDomainEventsInterceptor`). Un outbox iría en otro spec si hace falta. |
| El `SaveChangesAsync` del handler corre dentro de `SavedChangesAsync` del mismo `DbContext` | Ya previsto: el interceptor limpia los eventos antes de publicar. El test de integración `CloseConsultation_WithAppointment_CompletesAppointment` cubre el camino real. |
| Una cita `Cancelled` o `NoShow` sigue enlazada a una consulta `Closed` sin aviso al cliente | El `Warning` lo registra en el log; el estado de la cita se ve en `GET /api/appointments/{id}`. |
| Un consumidor futuro del evento recibe `AppointmentId` `null` | El payload ya es `AppointmentId?` desde el spec 16; queda documentado en `DOMAIN.md`. |

## What is **not** in this spec

- Tests E2E de la fila 18 del backlog.
- Cambios en `DispatchDomainEventsInterceptor` o despacho dentro de la transacción.
- Outbox, reintentos o brokers.
- Un endpoint o command para completar citas.
- `CompletedAtUtc` y migraciones.
- Otros consumidores del evento y eventos de Appointments.

Cada uno de ellos, si llega, va en su propio spec.
