# Guía de la capa Domain

Proyectos: `src/BuildingBlocks/BuildingBlocks.Domain` (abstracciones genéricas) y `src/Services/Dispensario/Dispensario.Domain` (modelo del dispensario).
**Sin dependencias** de EF Core, ASP.NET ni FluentValidation.

## 1. Abstracciones (BuildingBlocks.Domain)
```csharp
namespace BuildingBlocks.Domain;

// Evento de dominio: marcador sin miembros (el dominio no conoce la hora; los metadatos llegarían con un outbox)
public interface IDomainEvent : INotification;

// Auditoría: la completa AuditableEntityInterceptor (sufijo Utc, como todas las fechas)
public interface IAuditable
{
    DateTime CreatedAtUtc { get; set; }
    string? CreatedBy { get; set; }
    DateTime? LastModifiedAtUtc { get; set; }
    string? LastModifiedBy { get; set; }
}

// Igualdad: mismo tipo concreto y mismo Id; implementa IEquatable<Entity<TId>>, == y !=
public abstract class Entity<TId> : IEquatable<Entity<TId>> where TId : notnull
{
    public TId Id { get; protected set; } = default!;
}

// Vista no genérica de los eventos de un aggregate (la usa DispatchDomainEventsInterceptor)
public interface IHasDomainEvents
{
    IReadOnlyCollection<IDomainEvent> DomainEvents { get; }
    void ClearDomainEvents();
}

// Marca los IDs tipados: Infrastructure los convierte a Guid por convención
public interface IStronglyTypedId
{
    Guid Value { get; }
}

public abstract class AggregateRoot<TId> : Entity<TId>, IAuditable, IHasDomainEvents where TId : notnull
{
    private readonly List<IDomainEvent> _domainEvents = [];
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();
    protected void Raise(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);
    public void ClearDomainEvents() => _domainEvents.Clear();

    public DateTime CreatedAtUtc { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? LastModifiedAtUtc { get; set; }
    public string? LastModifiedBy { get; set; }
}

public abstract record ValueObject;   // los records ya dan igualdad estructural

public class DomainException(string message) : Exception(message);
```

## 2. Reglas DDD
1. **Constructores privados + fábrica estática** `Create(...)` que valida los invariantes. EF usa un constructor privado sin parámetros.
2. **Setters privados.** El estado solo cambia mediante métodos de negocio con nombre del dominio (`Cancel`, `Close`, `Deactivate`), no con `SetStatus`.
3. Si un invariante se rompe se lanza `DomainException` con un mensaje claro en inglés.
4. **IDs tipados**: `public readonly record struct PatientId(Guid Value) : IStronglyTypedId { public static PatientId New() => new(Guid.CreateVersion7()); }`. Implementar `IStronglyTypedId` y tener el constructor `(Guid)` basta para que EF los guarde como Guid; no se registran a mano.
5. Las colecciones internas se exponen como `IReadOnlyCollection<T>` sobre un campo `List<T>` privado.
6. Las referencias a otros aggregates se hacen **solo por ID**.
7. Se emite un domain event cuando otro módulo (o un efecto secundario) puede interesarse en el hecho.
8. El dominio **no conoce la hora actual**: las fechas que necesita (p. ej. `startedAt`) se pasan como parámetro desde el handler, que usa `TimeProvider`.
9. Las validaciones de formato (longitudes, requeridos) van en FluentValidation (Application). Las **reglas de negocio** van en el dominio.

## 3. Value objects comunes (`Domain/Common`)
Cada VO es un `sealed record : ValueObject` con constructor privado y `static Create(...)`, que recorta (`Trim`) los textos, trata `null` como vacío y lanza `DomainException` si no se cumplen las reglas. Los máximos están expuestos como constantes (`FullName.MaxLength`, `Address.StreetMaxLength`…) para reutilizarlos en validadores y configuraciones EF.

| VO | Campos | Normalización | Reglas |
|---|---|---|---|
| `FullName` | `FirstName`, `LastName` | Trim | no vacíos, máx. 100 cada uno; `ToString()` = "First Last" |
| `Email` | `Value` | Trim + minúsculas | máx. 254, regex `^[^@\s]+@[^@\s]+\.[^@\s]+$` |
| `PhoneNumber` | `Value` | Trim (se guarda tal como se escribió) | regex `^[0-9+\- ]{7,20}$`: dígitos, `+`, espacios y guiones; 7–20 caracteres |
| `Address` | `Street`, `City`, `State?`, `Reference?` | Trim; opcionales en blanco → `null` | `Street` requerido, máx. 200; `City` requerido, máx. 100; `State` máx. 100; `Reference` máx. 250 |
| `Document` | `DocumentType Type`, `Number` | Trim del número (sin mayúsculas ni regex) | `Type` definido (`NationalId`, `Passport`); `Number` requerido, máx. 20. Lo usan Users y Patients |

## 4. Modelo por módulo

### Users
| Miembro | Tipo | Notas |
|---|---|---|
| `Id` | `UserId` | |
| `Email` | `Email` | único |
| `PasswordHash` | `string` | hash bcrypt; lo genera `IPasswordHasher` (Application, fila 07). El dominio no hashea |
| `FullName` | `FullName` | |
| `Identification` | `Document` | obligatorio; único (`Type` + `Number`) |
| `Phone` | `PhoneNumber?` | opcional |
| `Role` | `Role` enum | `Admin`, `Doctor`, `Receptionist` |
| `DoctorId` | `DoctorId?` | opcional también con `Role == Doctor` (decidido en la fila 09); se asigna con `LinkDoctor` desde `POST /api/users/{id}/link-doctor`. Único: un doctor se vincula a un solo usuario |
| `IsActive` | `bool` | `true` al crear |

Métodos: `Create(email, passwordHash, fullName, identification, phone, role)`, `ChangePassword(newHash)`, `UpdateProfile(fullName, phone)` (`phone` null borra el teléfono), `Deactivate()`, `Activate()`, `LinkDoctor(doctorId)`.
Reglas: el hash no puede estar vacío y el rol debe estar definido; `Deactivate` sobre un inactivo y `Activate` sobre un activo lanzan `DomainException`; `LinkDoctor` solo con `Role == Doctor` y si no hay ya un doctor vinculado.
`Email` e `Identification` no cambian tras crear el usuario.
Eventos: `UserCreatedDomainEvent(UserId, Email, Role)`.

### Doctors
| Miembro | Tipo | Notas |
|---|---|---|
| `Id` | `DoctorId` | |
| `FullName` | `FullName` | |
| `LicenseNumber` | `string` | Trim + mayúsculas; máx. 30 (`Doctor.LicenseNumberMaxLength`); único sin distinguir mayúsculas |
| `Specialty` | `string` | Trim; texto libre en v1; máx. 100 (`Doctor.SpecialtyMaxLength`) |
| `Email` | `Email` | obligatorio; no único (independiente del email de login) |
| `Phone` | `PhoneNumber?` | opcional |
| `AppointmentDurationMinutes` | `int` | entre 10 y 120, múltiplo de 5; el alta usa 30 (`Doctor.DefaultAppointmentDurationMinutes`) si no se indica |
| `Schedule` | `IReadOnlyCollection<ScheduleBlock>` | horario semanal: VOs propiedad del aggregate (fila 10), en hora local del dispensario |
| `IsActive` | `bool` | `true` al crear |

Métodos (fila 09): `Create(fullName, licenseNumber, specialty, email, phone, appointmentDurationMinutes)`, `Update(...)` (mismos parámetros; reemplaza todo, `phone` null borra el teléfono y también vale sobre un doctor inactivo), `Deactivate()`, `Activate()`.
Reglas (fila 09): matrícula y especialidad no vacías tras el `Trim` y dentro de su máximo; duración en rango y múltiplo de 5; `Deactivate` sobre un inactivo y `Activate` sobre un activo lanzan `DomainException`. El valor por defecto de 30 lo aplica el handler de alta, no el dominio.
Regla (fila 10): `Update` rechaza una duración mayor que algún bloque del horario (`Appointment duration cannot be longer than a schedule block.`), antes de cambiar ningún campo.
Eventos: ninguno en v1.

`ScheduleBlock` (fila 10): `sealed record : ValueObject` con `DayOfWeek`, `TimeOnly StartTime`, `TimeOnly EndTime` y `DurationMinutes` calculado; `Create(dayOfWeek, startTime, endTime)`, `Overlaps(other)` y `Contains(start, end)`.
Reglas de `ScheduleBlock.Create`: día definido; horas en minutos exactos y múltiplo de 5 (`ScheduleBlock.TimeStepMinutes`); `StartTime < EndTime`, así que un bloque nunca cruza la medianoche. Dos bloques contiguos (`09:00–12:00` y `12:00–14:00`) no se solapan.
Métodos (fila 10):
- `SetWeeklySchedule(IEnumerable<ScheduleBlock>)` reemplaza todo el horario (una lista vacía lo borra). Comprueba, en orden, que el doctor esté activo (`Doctor is inactive.`), que cada bloque dure al menos `AppointmentDurationMinutes` y que los bloques del mismo día no se solapen; si algo falla, el horario anterior no cambia.
- `IsAvailableAt(DateTime localStart, DateTime localEnd)` recibe horas locales del dispensario (ignora `Kind`) y devuelve `true` solo si el rango cabe entero en un bloque de ese día. Devuelve `false` si `start >= end` o si caen en fechas distintas. No mira `IsActive`, ni la alineación a slots, ni las citas: eso lo decide el handler que lo usa (fila 12).

### Patients
| Miembro | Tipo | Notas |
|---|---|---|
| `Id` | `PatientId` | |
| `Document` | `Document` VO de `Common` (`DocumentType Type`, `string Number`) | obligatorio; único (`Type` + `Number`, el número distingue mayúsculas); **editable**; tipos `NationalId`, `Passport` |
| `FullName` | `FullName` | obligatorio |
| `BirthDate` | `DateOnly` | obligatoria; no puede ser posterior a `today` |
| `Gender` | `Gender` enum | obligatorio; `Male`, `Female`, `Other` |
| `Email?` / `Phone?` | VOs | opcionales |
| `Address?` | `Address` | opcional |
| `BloodType?` | enum | opcional; `APositive`, `ANegative`, … `ONegative`. La notación clínica (`"A+"`) la traduce Application (`BloodTypeNotation`) |
| `Allergies?` | `string` | opcional; texto libre con `Trim`, en blanco → `null`; máx. 1000 (`Patient.AllergiesMaxLength`) |
| `IsActive` | `bool` | `true` al crear |

Métodos (fila 11): `Create(document, fullName, birthDate, gender, email, phone, address, bloodType, allergies, today)`, `Update(...)` (mismos parámetros; reemplaza todo, incluido el documento, un opcional `null` borra el valor y también vale sobre un paciente inactivo), `Deactivate()`, `Activate()`.
Reglas: `birthDate > today` → `Birth date cannot be in the future.` (la fecha de hoy la pasa el handler con `TimeProvider`; hoy es válida); `Gender` y `BloodType` definidos; alergias dentro del máximo; `Deactivate` sobre un inactivo y `Activate` sobre un activo lanzan `DomainException`. La unicidad del documento la comprueba el handler (409).
Eventos: ninguno en v1 (no hay consumidores).

### Appointments
| Miembro | Tipo | Notas |
|---|---|---|
| `Id` | `AppointmentId` | |
| `PatientId` / `DoctorId` | IDs tipados | solo IDs: sin navegaciones ni FKs hacia Patients y Doctors |
| `StartUtc` / `EndUtc` | `DateTime` (UTC) | `StartUtc < EndUtc`; el fin lo calcula el handler con la duración del doctor |
| `Reason` | `string` | obligatorio; Trim; máx. 500 (`Appointment.ReasonMaxLength`) |
| `Status` | `AppointmentStatus` | `Scheduled`, `Confirmed`, `Completed`, `Cancelled`, `NoShow`; `Scheduled` al crear |
| `CancellationReason?` | `string` | fila 13: solo en `Cancelled`; Trim; máx. 500 (`Appointment.CancellationReasonMaxLength`) |

Métodos (fila 12): `Create(patientId, doctorId, startUtc, endUtc, reason, nowUtc)`. `nowUtc` lo pasa el handler con `TimeProvider` (regla 8).
Reglas de `Create`, en orden: `startUtc >= endUtc` → `Appointment start must be before its end.`; `startUtc <= nowUtc` → `Appointment cannot be scheduled in the past.`; motivo en blanco → `Reason is required.`; motivo de más de 500 caracteres → `Reason must be at most 500 characters.`.
El enum `AppointmentStatus` está completo desde la fila 12; las citas se crean `Scheduled`.

Máquina de estados (filas 13 y 16):
```
Scheduled ──Confirm──► Confirmed
    │                      │
    ├──Complete────────────┼──► Completed   (final)
    ├──Cancel──────────────┼──► Cancelled   (final)
    ├──MarkNoShow──────────┴──► NoShow      (final)
    └──Reschedule (Scheduled|Confirmed → Scheduled, con nuevas fechas)
```

Métodos (fila 13): `Confirm(nowUtc)`, `Cancel(reason, nowUtc)`, `MarkNoShow(nowUtc)` y `Reschedule(startUtc, endUtc, nowUtc)`. `nowUtc` lo pasa el handler (regla 8).
Método (fila 16): `Complete()`, sin `nowUtc`: la consulta puede empezar antes de la hora de la cita. Solo lo llama `ConsultationClosedHandler` (`Appointments/EventHandlers`, spec 18) al recibir `ConsultationClosedDomainEvent`; no hay endpoint para completar una cita.

| Método | Estado de origen | Momento | Estado final |
|---|---|---|---|
| `Confirm` | `Scheduled` | antes del inicio (`StartUtc > nowUtc`) | `Confirmed` |
| `Cancel` | `Scheduled`, `Confirmed` | antes del inicio | `Cancelled` |
| `MarkNoShow` | `Scheduled`, `Confirmed` | desde el inicio (`StartUtc <= nowUtc`) | `NoShow` |
| `Reschedule` | `Scheduled`, `Confirmed` | antes del inicio actual; el nuevo inicio también futuro | `Scheduled` |
| `Complete` | `Scheduled`, `Confirmed` | sin restricción | `Completed` |

Reglas (`DomainException`), en este orden dentro de cada método:
- `Confirm`: estado distinto de `Scheduled` → `Only scheduled appointments can be confirmed.`; ya empezada → `Appointment has already started.`.
- `Cancel`: estado distinto de `Scheduled`/`Confirmed` → `Only scheduled or confirmed appointments can be cancelled.`; ya empezada → `Appointment has already started.`; motivo en blanco → `Cancellation reason is required.`; motivo de más de 500 → `Cancellation reason must be at most 500 characters.`.
- `MarkNoShow`: estado distinto de `Scheduled`/`Confirmed` → `Only scheduled or confirmed appointments can be marked as no-show.`; antes del inicio → `Appointment has not started yet.`.
- `Reschedule`: estado distinto de `Scheduled`/`Confirmed` → `Only scheduled or confirmed appointments can be rescheduled.`; ya empezada → `Appointment has already started.`; mismo inicio → `New start must be different from the current one.`; inicio ≥ fin → `Appointment start must be before its end.`; nuevo inicio pasado → `Appointment cannot be scheduled in the past.`.
- `Completed`, `Cancelled` y `NoShow` son finales. Si una regla falla, la cita no cambia.
- `Cancel` guarda el motivo recortado en `CancellationReason`; `Reschedule` cambia `StartUtc`/`EndUtc` y vuelve a `Scheduled` aunque estuviera `Confirmed`.
- `Complete`: estado distinto de `Scheduled`/`Confirmed` → `Only scheduled or confirmed appointments can be completed.`. Solo cambia `Status` (sin `CompletedAtUtc`).
- Las reglas que dependen de **otros** aggregates (horario del doctor, cruces) se validan en el handler (ver APPLICATION.md).

Eventos: ninguno en las filas 12 y 13 (no hay consumidores, regla 7). `AppointmentScheduledDomainEvent`, `AppointmentCancelledDomainEvent` y `AppointmentRescheduledDomainEvent` se añadirán cuando algún módulo reaccione a ellos.

### Consultations
| Miembro | Tipo | Notas |
|---|---|---|
| `Id` | `ConsultationId` | |
| `PatientId` / `DoctorId` | IDs tipados | solo IDs: sin navegaciones ni FKs hacia Patients y Doctors |
| `AppointmentId?` | `AppointmentId?` | consulta sin cita = `null`; sin FK hacia Appointments |
| `Reason` | `string` | obligatorio; Trim; máx. 500 (`Consultation.ReasonMaxLength`) |
| `Diagnosis?` | `string` | Trim, en blanco → `null`; máx. 1000 (`Consultation.DiagnosisMaxLength`); obligatorio al cerrar |
| `Notes?` | `string` | Trim, en blanco → `null`; máx. 4000 (`Consultation.NotesMaxLength`) |
| `Status` | `ConsultationStatus` | `Open` al iniciar, `Closed` (final) |
| `StartedAtUtc` / `ClosedAtUtc?` | `DateTime` (UTC) | `ClosedAtUtc` solo en `Closed` |

Métodos (fila 15): `Start(patientId, doctorId, appointmentId?, reason, startedAtUtc)`, `Update(reason, diagnosis, notes)` y `Close(diagnosis, notes, closedAtUtc)`. Las fechas las pasa el handler con `TimeProvider` (regla 8).

```
Start ──► Open ──Update──► Open
            │
            └──Close──► Closed   (final)
```

Reglas (`DomainException`), en este orden dentro de cada método:
- `Start`: motivo en blanco → `Reason is required.`; de más de 500 → `Reason must be at most 500 characters.`.
- `Update`: estado `Closed` → `Only open consultations can be updated.`; motivo como en `Start`; diagnóstico de más de 1000 → `Diagnosis must be at most 1000 characters.`; notas de más de 4000 → `Notes must be at most 4000 characters.`.
- `Close`: estado `Closed` → `Consultation is already closed.`; diagnóstico en blanco → `Diagnosis is required to close a consultation.`; diagnóstico y notas largos como en `Update`; `closedAtUtc < StartedAtUtc` → `Consultation cannot be closed before it started.`.
- `Update` reemplaza los tres textos (un opcional `null` o en blanco borra el valor). `Close` reemplaza `Diagnosis` y `Notes`, fija `ClosedAtUtc` y deja la consulta `Closed`. Si una regla falla, la consulta no cambia.
- Las reglas que dependen de **otros** aggregates (doctor y paciente activos; cita del doctor y del paciente, pendiente, de hoy y sin otra consulta) se validan en el handler (ver APPLICATION.md).

Eventos: `ConsultationClosedDomainEvent(ConsultationId, AppointmentId?)` (`Consultations/Events/`). `Close` lo emite **siempre** al pasar a `Closed` (spec 18), con `AppointmentId` `null` en una consulta sin cita; un `Close` que falla no emite nada y, como `Closed` es final, sale como mucho una vez. `Start` y `Update` no emiten eventos. Su único consumidor es `ConsultationClosedHandler` (`Appointments/EventHandlers`), que completa la cita enlazada si sigue `Scheduled`/`Confirmed` y, si no, la deja como está y registra un `Warning`. Se despacha tras el commit de la consulta, así que consulta y cita se guardan en dos `SaveChangesAsync`.

## 5. Estructura de carpetas (ejemplo)
```
Dispensario.Domain/
  Common/ FullName.cs Email.cs PhoneNumber.cs Address.cs Document.cs DocumentType.cs
  Doctors/ Doctor.cs DoctorId.cs ScheduleBlock.cs
  Users/
    User.cs UserId.cs Role.cs
    Events/UserCreatedDomainEvent.cs
  Patients/ Patient.cs PatientId.cs Gender.cs BloodType.cs
  Appointments/ Appointment.cs AppointmentId.cs AppointmentStatus.cs
  Consultations/
    Consultation.cs ConsultationId.cs ConsultationStatus.cs
    Events/ConsultationClosedDomainEvent.cs
```
