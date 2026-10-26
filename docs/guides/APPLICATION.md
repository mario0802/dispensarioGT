# Guía de la capa Application

Proyecto: `src/Services/Dispensario/Dispensario.Application`.
Orquesta los casos de uso con **CQRS** usando los contratos de `BuildingBlocks.CQRS`.

## 1. Estructura
```
Abstractions/
  IApplicationDbContext.cs   ICurrentUser.cs   IPasswordHasher.cs   IJwtTokenGenerator.cs
<Module>/
  Commands/<Feature>/ <Feature>Command.cs  <Feature>Handler.cs
  Queries/<Feature>/  <Feature>Query.cs    <Feature>Handler.cs
  Dtos/               <Sustantivo>Dto.cs
  EventHandlers/      <Evento>Handler.cs
DependencyInjection.cs
```

## 2. Abstracciones
> **Estado:** `IApplicationDbContext` existe desde la fila 04 solo con `SaveChangesAsync`; cada módulo añade su `DbSet<T>` al crear su aggregate. `ICurrentUser` existe en versión mínima (`Guid? UserId`, `bool IsAuthenticated`), implementada por `AnonymousCurrentUser`; la fila 07 cambia `UserId` a `UserId?`, añade `Role` y `DoctorId` y la implementa con los claims. Abajo, la forma final.

```csharp
public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    DbSet<Doctor> Doctors { get; }
    DbSet<Patient> Patients { get; }
    DbSet<Appointment> Appointments { get; }
    DbSet<Consultation> Consultations { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface ICurrentUser
{
    UserId? UserId { get; }
    Role? Role { get; }
    DoctorId? DoctorId { get; }
    bool IsAuthenticated { get; }
}

public interface IPasswordHasher { string Hash(string password); bool Verify(string hash, string password); }
public interface IJwtTokenGenerator { (string Token, DateTime ExpiresAtUtc) Generate(User user); }
```

## 3. Plantilla de un command
```csharp
// CreatePatientCommand.cs
namespace Dispensario.Application.Patients.Commands.CreatePatient;

public sealed record CreatePatientCommand(
    DocumentType DocumentType, string DocumentNumber,
    string FirstName, string LastName, DateOnly BirthDate, Gender Gender,
    string? Email, string? Phone) : ICommand<CreatePatientResult>;

public sealed record CreatePatientResult(Guid Id);

public sealed class CreatePatientCommandValidator : AbstractValidator<CreatePatientCommand>
{
    public CreatePatientCommandValidator()
    {
        RuleFor(x => x.DocumentNumber).NotEmpty().MaximumLength(20);
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        // ...
    }
}

// CreatePatientHandler.cs
/// <summary>Registra un paciente nuevo; el documento debe ser único.</summary>
public sealed class CreatePatientHandler(IApplicationDbContext db)
    : ICommandHandler<CreatePatientCommand, CreatePatientResult>
{
    public async Task<CreatePatientResult> Handle(CreatePatientCommand command, CancellationToken ct)
    {
        var document = Document.Create(command.DocumentType, command.DocumentNumber);

        // Regla de unicidad: necesita la BD, así que se valida aquí y no en el dominio
        if (await db.Patients.AnyAsync(p => p.Document == document, ct))
            throw new ConflictException($"Patient with document {document} already exists.");

        var patient = Patient.Create(document, /* ... */);
        db.Patients.Add(patient);
        await db.SaveChangesAsync(ct);

        return new CreatePatientResult(patient.Id.Value);
    }
}
```

## 4. Plantilla de una query
```csharp
public sealed record GetPatientsQuery(PaginationRequest Pagination, string? Search)
    : IQuery<PaginatedResult<PatientDto>>;

public sealed class GetPatientsHandler(IApplicationDbContext db)
    : IQueryHandler<GetPatientsQuery, PaginatedResult<PatientDto>>
{
    public async Task<PaginatedResult<PatientDto>> Handle(GetPatientsQuery query, CancellationToken ct)
    {
        var source = db.Patients.AsNoTracking();
        // ...filtro por Search en nombre o documento
        var total = await source.LongCountAsync(ct);
        var items = await source
            .OrderBy(p => p.FullName.LastName)
            .Skip(query.Pagination.PageIndex * query.Pagination.PageSize)
            .Take(query.Pagination.PageSize)
            .ProjectToType<PatientDto>()          // Mapster
            .ToListAsync(ct);
        return new PaginatedResult<PatientDto>(query.Pagination.PageIndex, query.Pagination.PageSize, total, items);
    }
}
```

## 5. Reglas
| Regla | Detalle |
|---|---|
| Un handler, un módulo de escritura | Puede leer otros DbSets, pero solo modifica aggregates de su módulo |
| Excepciones | `NotFoundException(nameof(Patient), id)`, `ConflictException` (duplicados/cruces), `BadRequestException`, `ForbiddenException`; las reglas de dominio lanzan `DomainException` |
| Validación | FluentValidation solo para forma y formato; `ValidationBehavior` aplica a los commands |
| Queries | Siempre `AsNoTracking()` y proyección a DTO; nunca devolver entidades |
| Tiempo | Se inyecta `TimeProvider` y se usa `GetUtcNow().UtcDateTime` |
| Transacciones | Un `SaveChangesAsync` por command. Los domain events se despachan **después** de guardar (ver INFRASTRUCTURE.md) |
| Mapeo | Mapster: `TypeAdapterConfig` por módulo en `<Module>/Dtos/<Module>MappingConfig.cs` (`IRegister`) |

## 6. Reglas de negocio con varios aggregates
**ScheduleAppointment** (y Reschedule):
1. El paciente existe y está activo; si no → `NotFoundException` o `BadRequestException`.
2. El doctor existe y está activo.
3. `EndUtc = StartUtc + doctor.AppointmentDurationMinutes`.
4. Al convertir a la hora local del dispensario (`Clinic:TimeZone`), el rango cae dentro de un bloque del `WeeklySchedule` y está alineado a la duración del slot; si no → `BadRequestException`.
5. No hay cruce con citas del doctor ni del paciente en estado `Scheduled` o `Confirmed`; si lo hay → `ConflictException`.
6. No se agenda en el pasado.

**StartConsultation**: el usuario actual es `Doctor` y `ICurrentUser.DoctorId` es el doctor de la consulta. Si se envía `AppointmentId`, la cita debe ser de ese doctor y paciente y estar `Scheduled` o `Confirmed`. Solo puede haber una consulta `Open` por cita.

## 7. DependencyInjection
```csharp
public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
{
    var assembly = typeof(DependencyInjection).Assembly;
    services.AddMediatR(cfg =>
    {
        cfg.LicenseKey = configuration["MediatR:LicenseKey"];
        cfg.RegisterServicesFromAssembly(assembly);
        cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
        cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
    });
    services.AddValidatorsFromAssembly(assembly);
    TypeAdapterConfig.GlobalSettings.Scan(assembly);
    return services;
}
```
