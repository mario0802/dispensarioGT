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

## 4. Modelo por módulo

### Users
| Miembro | Tipo | Notas |
|---|---|---|
| `Id` | `UserId` | |
| `Email` | `Email` | único |
| `PasswordHash` | `string` | lo genera `IPasswordHasher` (Application) |
| `FullName` | `FullName` | |
| `Role` | `Role` enum | `Admin`, `Doctor`, `Receptionist` |
| `DoctorId` | `DoctorId?` | obligatorio si `Role == Doctor` |
| `IsActive` | `bool` | |

Métodos: `Create`, `ChangePassword(newHash)`, `Deactivate()`, `Activate()`, `LinkDoctor(doctorId)`.
Eventos: `UserCreatedDomainEvent`.

### Doctors
| Miembro | Tipo | Notas |
|---|---|---|
| `Id` | `DoctorId` | |
| `FullName` | `FullName` | |
| `LicenseNumber` | `string` | único |
| `Specialty` | `string` | texto libre en v1 |
| `Email` / `Phone` | `Email` / `PhoneNumber` | |
| `AppointmentDurationMinutes` | `int` | entre 10 y 120 |
| `Schedules` | `IReadOnlyCollection<WeeklySchedule>` | entidad hija, propiedad del aggregate |
| `IsActive` | `bool` | |

`WeeklySchedule`: `DayOfWeek`, `TimeOnly StartTime`, `TimeOnly EndTime` (con `Start < End`).
Invariantes: los bloques del mismo día no se solapan, y cada bloque dura al menos `AppointmentDurationMinutes`.
Métodos: `Create`, `Update(...)`, `SetWeeklySchedule(IEnumerable<...>)` (reemplaza todo el horario), `Deactivate()`, `IsAvailableAt(DateTime localStart, DateTime localEnd)`.

### Patients
| Miembro | Tipo | Notas |
|---|---|---|
| `Id` | `PatientId` | |
| `Document` | `Document` VO (`DocumentType Type`, `string Number`) | único (`Type` + `Number`) |
| `FullName` | `FullName` | |
| `BirthDate` | `DateOnly` | no puede ser futura |
| `Gender` | `Gender` enum | `Male`, `Female`, `Other` |
| `Email?` / `Phone?` | VOs | |
| `Address?` | `Address` | |
| `BloodType?` | enum | `APositive`, `ANegative`, … `ONegative` |
| `Allergies?` | `string` | texto libre |
| `IsActive` | `bool` | |

Métodos: `Create`, `UpdatePersonalData(...)`, `UpdateMedicalData(bloodType, allergies)`, `Deactivate()`.

### Appointments
| Miembro | Tipo |
|---|---|
| `Id` | `AppointmentId` |
| `PatientId` / `DoctorId` | IDs tipados |
| `StartUtc` / `EndUtc` | `DateTime` (UTC) |
| `Reason` | `string` |
| `Status` | `AppointmentStatus` |
| `CancellationReason?` | `string` |

Máquina de estados:
```
Scheduled ──Confirm──► Confirmed ──Complete──► Completed
    │                      │
    ├──Cancel──────────────┼──► Cancelled
    ├──MarkNoShow──────────┴──► NoShow
    └──Reschedule (Scheduled|Confirmed → Scheduled, con nuevas fechas)
```
- `Cancel`, `MarkNoShow` y `Reschedule` no se permiten sobre `Completed`, `Cancelled` ni `NoShow`.
- `Complete` se permite desde `Scheduled` o `Confirmed`.
- Las reglas que dependen de **otros** aggregates (horario del doctor, cruces) se validan en el handler (ver APPLICATION.md).

Eventos: `AppointmentScheduledDomainEvent`, `AppointmentCancelledDomainEvent`, `AppointmentRescheduledDomainEvent`.

### Consultations
| Miembro | Tipo |
|---|---|
| `Id` | `ConsultationId` |
| `PatientId` / `DoctorId` | IDs tipados |
| `AppointmentId?` | `AppointmentId?` (consulta sin cita = null) |
| `Reason` | `string` |
| `Diagnosis?` / `Notes?` | `string` |
| `StartedAtUtc` / `ClosedAtUtc?` | `DateTime` |
| `Status` | `Open`, `Closed` |

Métodos: `Start(...)`, `Update(reason, diagnosis, notes)` (solo si está `Open`), `Close(diagnosis, notes, closedAtUtc)`, que exige `Diagnosis` no vacío.
Eventos: `ConsultationClosedDomainEvent(ConsultationId, AppointmentId?)`.

## 5. Estructura de carpetas (ejemplo)
```
Dispensario.Domain/
  Common/ FullName.cs Email.cs PhoneNumber.cs Address.cs
  Patients/
    Patient.cs PatientId.cs Document.cs DocumentType.cs Gender.cs BloodType.cs
    Events/PatientRegisteredDomainEvent.cs
```
