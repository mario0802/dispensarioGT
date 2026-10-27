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
| `Schedules` | `IReadOnlyCollection<WeeklySchedule>` | entidad hija, propiedad del aggregate (llega con la fila 10) |
| `IsActive` | `bool` | `true` al crear |

Métodos (fila 09): `Create(fullName, licenseNumber, specialty, email, phone, appointmentDurationMinutes)`, `Update(...)` (mismos parámetros; reemplaza todo, `phone` null borra el teléfono y también vale sobre un doctor inactivo), `Deactivate()`, `Activate()`.
Reglas (fila 09): matrícula y especialidad no vacías tras el `Trim` y dentro de su máximo; duración en rango y múltiplo de 5; `Deactivate` sobre un inactivo y `Activate` sobre un activo lanzan `DomainException`. El valor por defecto de 30 lo aplica el handler de alta, no el dominio.
Eventos: ninguno en v1.

`WeeklySchedule` (fila 10): `DayOfWeek`, `TimeOnly StartTime`, `TimeOnly EndTime` (con `Start < End`).
Invariantes (fila 10): los bloques del mismo día no se solapan, y cada bloque dura al menos `AppointmentDurationMinutes`.
Métodos (fila 10): `SetWeeklySchedule(IEnumerable<...>)` (reemplaza todo el horario), `IsAvailableAt(DateTime localStart, DateTime localEnd)`.

### Patients
| Miembro | Tipo | Notas |
|---|---|---|
| `Id` | `PatientId` | |
| `Document` | `Document` VO de `Common` (`DocumentType Type`, `string Number`) | único (`Type` + `Number`); tipos `NationalId`, `Passport` |
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
  Common/ FullName.cs Email.cs PhoneNumber.cs Address.cs Document.cs DocumentType.cs
  Doctors/ Doctor.cs DoctorId.cs           # WeeklySchedule llega con la fila 10
  Users/
    User.cs UserId.cs Role.cs
    Events/UserCreatedDomainEvent.cs
  Patients/
    Patient.cs PatientId.cs Gender.cs BloodType.cs
    Events/PatientRegisteredDomainEvent.cs
```
