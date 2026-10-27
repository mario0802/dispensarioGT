# Guía de la capa Infrastructure

Proyecto: `src/Services/Dispensario/Dispensario.Infrastructure`.
Implementa las abstracciones de Application: persistencia (EF Core + SQLite), autenticación (JWT y hash de contraseñas) y seed.

## 1. Estructura
```
Data/
  DispensarioDbContext.cs
  Configurations/
    Users/UserConfiguration.cs
    Doctors/DoctorConfiguration.cs
    Patients/PatientConfiguration.cs
    Appointments/AppointmentConfiguration.cs
    Consultations/ConsultationConfiguration.cs
  Converters/StronglyTypedIdConverter.cs   # genérico para todo IStronglyTypedId
  Converters/UtcDateTimeConverter.cs       # UtcDateTimeConverter y NullableUtcDateTimeConverter
  Extensions/ModelConfigurationBuilderExtensions.cs   # convenciones de IDs tipados y DateTime UTC
  Interceptors/AuditableEntityInterceptor.cs
  Interceptors/DispatchDomainEventsInterceptor.cs
  Migrations/
  Seed/AdminSeedOptions.cs  Seed/DatabaseSeeder.cs
  Extensions/DatabaseExtensions.cs        # InitialiseDatabaseAsync (EnsureCreated + seed; MigrateAsync desde la fila 17)
Authentication/
  JwtOptions.cs  JwtTokenGenerator.cs  BcryptPasswordHasher.cs  CurrentUser.cs
DependencyInjection.cs
```

## 2. DbContext
```csharp
public sealed class DispensarioDbContext(DbContextOptions<DispensarioDbContext> options)
    : DbContext(options), IApplicationDbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Doctor> Doctors => Set<Doctor>();
    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<Consultation> Consultations => Set<Consultation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ApplyConfigurationsFromAssembly(typeof(DispensarioDbContext).Assembly);

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Ensamblado de Dispensario.Domain, localizado con un tipo ancla de ese proyecto
        configurationBuilder.ApplyStronglyTypedIdConventions(typeof(Email).Assembly);
        configurationBuilder.ApplyUtcDateTimeConventions();
    }
}
```
- Cada módulo añade su `DbSet<T>` aquí y en `IApplicationDbContext` cuando crea su aggregate.
- **IDs tipados por convención:** `ApplyStronglyTypedIdConventions` busca en el ensamblado todos los `struct` que implementan `IStronglyTypedId` (con constructor público `(Guid)`) y les aplica `StronglyTypedIdConverter<TId>`, que los guarda como Guid (`TEXT` en SQLite). Sirve también para los IDs nullable. Un ID nuevo no requiere tocar el `DbContext`.
- **`DateTime` en UTC:** `ApplyUtcDateTimeConventions` marca con `DateTimeKind.Utc` todo `DateTime` y `DateTime?` leído de la base de datos (SQLite los devuelve `Unspecified`). Una conversión explícita en una configuración tiene prioridad sobre esta convención.

## 3. Configuraciones EF por módulo
Reglas:
- Una clase `IEntityTypeConfiguration<T>` por aggregate, en la carpeta de su módulo.
- **Tabla explícita** (`ToTable("Patients")`).
- Los value objects se mapean según lo que necesiten:
  - **Un solo valor** (`Email`, `PhoneNumber`): `HasConversion(v => v.Value, s => Email.Create(s))` a una columna con el nombre de la propiedad. Admiten índice y comparaciones en LINQ (`u.Email == email`). Para un VO opcional, `p => p!.Value` (EF no pasa `null` al converter).
  - **Varios campos sin índice** (`FullName`, `Address`): `ComplexProperty`.
  - **Varios campos con índice** (`Document`): `OwnsOne` en la misma tabla, con el índice dentro del builder del tipo propio (`identification.HasIndex(d => new { d.Type, d.Number }).IsUnique()`) y `builder.Navigation(...).IsRequired()` si es obligatorio. `ComplexProperty` no admite índices.
- Las columnas de los VOs de varios campos llevan el prefijo de la propiedad y se fijan con `HasColumnName` (`FullName_FirstName`, `Identification_Number`).
- Las propiedades de los VOs son de solo lectura y EF **no las mapea por convención**: se declaran una a una dentro del `ComplexProperty` (`name.Property(n => n.FirstName).HasMaxLength(FullName.MaxLength)`). EF las materializa con el constructor privado del VO. Un VO opcional (`Address?`) se mapea igual y se relee `null` si se guardó `null`.
- Los enums se guardan como `string` (`HasConversion<string>()`), que es legible en SQLite.
- Longitudes máximas iguales a las del validador.
- **Sin navegaciones entre módulos.** Si se quiere FK física, se declara sin navegación: `builder.HasOne<Patient>().WithMany().HasForeignKey(a => a.PatientId).OnDelete(DeleteBehavior.Restrict);`.
- Las colecciones propias del aggregate (`Doctor.Schedule` de `ScheduleBlock`) usan `OwnsMany` con tabla propia (`DoctorScheduleBlocks`), FK en sombra al propietario con borrado en cascada y una clave `int` en sombra (`Id`) que es la clave primaria sola: SQLite solo autoincrementa un `INTEGER PRIMARY KEY`, no una clave compuesta. Las propiedades del VO se declaran una a una y la navegación usa el campo (`HasField("_schedule")`). EF carga la colección con el propietario, sin `Include`.

Índices obligatorios:
| Tabla | Índice |
|---|---|
| `Users` | único `Email`; único (`Identification_Type`, `Identification_Number`); único `DoctorId` (sin FK; SQLite admite varios `NULL`) |
| `Doctors` | único `LicenseNumber` (guardado en mayúsculas por el dominio) |
| `Patients` | único (`Document_Type`, `Document_Number`); sin índice de `LastName` (vive en un `ComplexProperty`, que no admite índices) |
| `Appointments` | (`DoctorId`, `StartUtc`), (`PatientId`, `StartUtc`) |
| `Consultations` | `PatientId`, `AppointmentId` |

Tabla `Doctors` (fila 09): `Id`, `FullName_FirstName`, `FullName_LastName` (`ComplexProperty`), `LicenseNumber`, `Specialty`, `Email` y `Phone` (`HasConversion`, el email sin índice), `AppointmentDurationMinutes`, `IsActive` y las columnas de auditoría. Tabla `DoctorScheduleBlocks` (fila 10): `Id` (clave autoincremental), `DoctorId` (FK a `Doctors`, en cascada, con su índice), `DayOfWeek` (texto), `StartTime` y `EndTime` (`TimeOnly` como TEXT).

Tabla `Patients` (fila 11): `Id`; `Document_Type` (texto) y `Document_Number` (`OwnsOne` requerido, con el índice único compuesto); `FullName_FirstName` y `FullName_LastName` (`ComplexProperty`); `BirthDate` (`DateOnly` como TEXT); `Gender` (texto); `Email` y `Phone` (`HasConversion`, nullable, el email sin índice); `Address_Street`, `Address_City`, `Address_State` y `Address_Reference` (`ComplexProperty` **opcional**: columnas nullable, y `Street`, requerida dentro del VO, distingue la dirección `null`); `BloodType` (nombre del enum como texto, nullable); `Allergies` (máx. 1000, nullable); `IsActive` y las columnas de auditoría.

Tabla `Appointments` (fila 12): `Id`; `PatientId` y `DoctorId` (Guid por convención, requeridos, **sin FK** hacia `Patients` ni `Doctors`: los módulos solo se relacionan por ID, igual que `Users.DoctorId`); `StartUtc` y `EndUtc` (`DateTime` UTC como TEXT ISO, que ordena y compara bien en las consultas de cruces y rangos); `Reason` (máx. 500); `Status` (nombre del enum como texto); `CancellationReason` (fila 13; máx. 500, nullable, solo con valor en las citas `Cancelled`); y las columnas de auditoría. Índices no únicos (`DoctorId`, `StartUtc`) y (`PatientId`, `StartUtc`).

## 4. Particularidades de SQLite
| Tema | Decisión |
|---|---|
| Esquemas | SQLite no los soporta: los módulos se separan por nombre de tabla y carpeta de configuración |
| Fechas | `DateTime` en UTC (sufijo `Utc` en el nombre). Evitar `DateTimeOffset`, que no se puede ordenar ni comparar en SQL con SQLite |
| `TimeOnly`/`DateOnly` | Soportados por EF Core SQLite (se guardan como TEXT) |
| Decimales | No hay en v1. Si aparecen, convertir a `double` o a `long` en céntimos |
| Concurrencia | No hay `rowversion`; si hiciera falta, token manual `Version` con `IsConcurrencyToken()` |
| Comparación de texto | Case-sensitive por defecto: el email se normaliza a minúsculas en el VO y las búsquedas usan `EF.Functions.Like` |
| Cadena de conexión | `Data Source=dispensario.db` (en `appsettings.json`). El `.db` está en `.gitignore` |

## 5. Interceptores
- **AuditableEntityInterceptor** (`SavingChanges` y `SavingChangesAsync`): para cada `IAuditable` en `Added` completa `CreatedAtUtc`/`CreatedBy`, y en `Modified`, `LastModifiedAtUtc`/`LastModifiedBy`. Un aggregate `Unchanged` cuyas entidades propias (`OwnsOne`/`OwnsMany`) se añadieron, modificaron o borraron también cuenta como modificado. Usa `TimeProvider` e `ICurrentUser` (`CreatedBy` = `UserId` como texto, `null` si es anónimo).
- **DispatchDomainEventsInterceptor** (`SavedChangesAsync`): recoge los eventos de los aggregates rastreados (`IHasDomainEvents`), limpia sus listas **antes** de publicar y los publica en orden con `IPublisher.Publish`. Se despachan **después** del commit; si el guardado falla, no se publica nada y los eventos siguen en el aggregate. Si un handler de eventos modifica datos, llama a su propio `SaveChangesAsync`.
- Los eventos solo se despachan con `SaveChangesAsync`: un `SaveChanges` síncrono con eventos pendientes lanza `InvalidOperationException` **antes** de guardar.
  > Cuando se pase a microservicios, este interceptor se reemplaza por un *Outbox* (tabla `OutboxMessages` + worker).

## 6. Autenticación
Infrastructure referencia `Microsoft.AspNetCore.App` (`FrameworkReference`) para `IHttpContextAccessor`, `IHost` y el logging, y los paquetes `BCrypt.Net-Next` y `Microsoft.IdentityModel.JsonWebTokens` (misma versión que trae `JwtBearer` de forma transitiva).

```csharp
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    [Required] public required string Issuer { get; init; }
    [Required] public required string Audience { get; init; }
    [Required, MinLength(32)] public required string Key { get; init; }   // en user-secrets
    [Range(1, 1440)] public int ExpirationMinutes { get; init; } = 60;
}
```
- `JwtOptions` se valida con `ValidateDataAnnotations().ValidateOnStart()`: sin `Jwt:Key`, o con menos de 32 caracteres, la aplicación **no arranca**.
- `JwtTokenGenerator`: HS256 con `JsonWebTokenHandler` (`Microsoft.IdentityModel.JsonWebTokens`). Claims `sub` (UserId), `email`, `name` (`FullName`), `role`, `jti` (Guid nuevo por token) y `doctor_id` (solo si el usuario tiene doctor). `iat`, `nbf` y `exp` salen de `TimeProvider`. Los nombres de `role` y `doctor_id` están en las constantes `CurrentUser.RoleClaimType` y `CurrentUser.DoctorIdClaimType`.
- `BcryptPasswordHasher`: implementa `IPasswordHasher` con bcrypt (`BCrypt.Net-Next`) y factor de trabajo **12** fijo (unos 250 ms por hash). No se usa ASP.NET Identity. La columna `Users.PasswordHash` admite hasta 100 caracteres (un hash bcrypt ocupa 60).
- `CurrentUser`: lee los claims de `IHttpContextAccessor`. Un claim ausente o inválido da `null`. Sin `HttpContext` (seed, procesos en segundo plano) o con un usuario no autenticado, todo es `null` e `IsAuthenticated = false`, así que esos guardados quedan con `CreatedBy = null`.
- La validación del token (`AddJwtBearer`) se configura en la Api con las mismas `JwtOptions` (ver `API.md` §4).

## 7. Seed e inicialización
- `AdminSeedOptions` (sección `Seed:Admin`): `Email`, `Password`, `FirstName`, `LastName`, `DocumentType` y `DocumentNumber`. No usa `ValidateOnStart`.
- `DatabaseSeeder.SeedAsync` crea el usuario Admin (`Role.Admin`, sin teléfono) si no existe un usuario con ese email:
  - Si `Seed:Admin:Password` está vacío, registra el warning `Admin seed skipped: 'Seed:Admin:Password' is empty.` y no crea nada.
  - Los VOs validan la configuración: un email o un documento inválidos lanzan `DomainException` al arrancar.
  - Si otro usuario ya tiene el mismo documento con otro email, el guardado falla por el índice único: se cambia `Seed:Admin:DocumentNumber`.
- `app.InitialiseDatabaseAsync()` (solo en Development, y por tanto también en los tests de integración) ejecuta `Database.EnsureCreatedAsync()` y luego el seed.
  > Hasta la fila 17 no hay migraciones: `EnsureCreated` no actualiza una base ya creada. Si el modelo cambia, hay que borrar `dispensario.db` en local. La fila 17 sustituye `EnsureCreatedAsync` por `MigrateAsync` (y habrá que borrar la base una vez).

## 8. Registro en DI
> **Estado:** registro real desde la fila 07. Falla al registrarse si no existe `ConnectionStrings:Database`. `IPublisher` lo registra MediatR en `AddApplication`, y el logging que necesita `DatabaseSeeder`, el host.

```csharp
public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
{
    var connectionString = configuration.GetConnectionString("Database")
        ?? throw new InvalidOperationException("Connection string 'Database' not found.");

    services.AddScoped<ISaveChangesInterceptor, AuditableEntityInterceptor>();
    services.AddScoped<ISaveChangesInterceptor, DispatchDomainEventsInterceptor>();

    services.AddDbContext<DispensarioDbContext>((sp, options) =>
    {
        options.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>());
        options.UseSqlite(connectionString);
    });
    services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<DispensarioDbContext>());

    services.AddOptions<JwtOptions>().BindConfiguration(JwtOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();
    services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();
    services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
    services.AddHttpContextAccessor();
    services.AddScoped<ICurrentUser, CurrentUser>();
    services.TryAddSingleton(TimeProvider.System);

    services.AddOptions<AdminSeedOptions>().BindConfiguration(AdminSeedOptions.SectionName);
    services.AddScoped<DatabaseSeeder>();
    return services;
}
```

## 9. Migraciones
```bash
dotnet tool install --global dotnet-ef --version 10.*
dotnet ef migrations add <Name> -p src/Services/Dispensario/Dispensario.Infrastructure -s src/Services/Dispensario/Dispensario.Api -o Data/Migrations
```
- Nombres en PascalCase y en inglés, que describan el cambio: `AddDoctorSchedules`.
- Revisar siempre la migración generada antes de aplicarla. No editar migraciones ya aplicadas.
