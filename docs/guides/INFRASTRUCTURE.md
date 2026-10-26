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
  Seed/DatabaseSeeder.cs
  Extensions/DatabaseExtensions.cs        # InitialiseDatabaseAsync (migrar + seed)
Authentication/
  JwtOptions.cs  JwtTokenGenerator.cs  PasswordHasher.cs  CurrentUser.cs
  AnonymousCurrentUser.cs                 # provisional hasta la fila 07 (usuario anónimo)
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
- Los value objects se mapean con `ComplexProperty` (EF 10) u `OwnsOne`, con columnas prefijadas (`Document_Type`, `Document_Number`).
- Las propiedades de los VOs son de solo lectura y EF **no las mapea por convención**: se declaran una a una dentro del `ComplexProperty` (`name.Property(n => n.FirstName).HasMaxLength(FullName.MaxLength)`). EF las materializa con el constructor privado del VO. Un VO opcional (`Address?`) se mapea igual y se relee `null` si se guardó `null`.
- Los enums se guardan como `string` (`HasConversion<string>()`), que es legible en SQLite.
- Longitudes máximas iguales a las del validador.
- **Sin navegaciones entre módulos.** Si se quiere FK física, se declara sin navegación: `builder.HasOne<Patient>().WithMany().HasForeignKey(a => a.PatientId).OnDelete(DeleteBehavior.Restrict);`.
- Las entidades hijas del mismo aggregate (`WeeklySchedule`) usan `OwnsMany` y tabla propia (`DoctorSchedules`).

Índices obligatorios:
| Tabla | Índice |
|---|---|
| `Users` | único `Email` |
| `Doctors` | único `LicenseNumber` |
| `Patients` | único (`Document_Type`, `Document_Number`); `LastName` |
| `Appointments` | (`DoctorId`, `StartUtc`), (`PatientId`, `StartUtc`) |
| `Consultations` | `PatientId`, `AppointmentId` |

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
```csharp
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public required string Issuer { get; init; }
    public required string Audience { get; init; }
    public required string Key { get; init; }          // mínimo 32 caracteres, en user-secrets
    public int ExpirationMinutes { get; init; } = 60;
}
```
- `JwtTokenGenerator`: HS256 con los claims `sub`, `email`, `name`, `role` y `doctor_id` (opcional). Usa `JsonWebTokenHandler` (`Microsoft.IdentityModel.JsonWebTokens`).
- `PasswordHasher`: envuelve `Microsoft.AspNetCore.Identity.PasswordHasher<User>` (paquete `Microsoft.Extensions.Identity.Core`). No se usa el resto de Identity.
- `CurrentUser`: lee los claims de `IHttpContextAccessor`.
- La validación del token (`AddJwtBearer`) se configura en la Api con las mismas `JwtOptions`.

## 7. Seed e inicialización
- `DatabaseSeeder` crea el usuario Admin si no existe un usuario con ese email, leyendo `Seed:Admin:Email`, `Seed:Admin:Password` y `Seed:Admin:FirstName/LastName`.
- `app.InitialiseDatabaseAsync()` (en Development y en tests de integración) ejecuta `Database.MigrateAsync()` y luego el seed.

## 8. Registro en DI
> **Estado:** hoy `AddInfrastructure` registra la persistencia (interceptores, `DispensarioDbContext`, `IApplicationDbContext`), `AnonymousCurrentUser` como `ICurrentUser` y `TimeProvider.System` (con `TryAddSingleton`). Falla al registrarse si no existe `ConnectionStrings:Database`. La fila 07 añade JWT, hasher, `HttpContextAccessor` y `CurrentUser`, y la fila 17 el seed. `IPublisher` lo registra MediatR en `AddApplication`.

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
    services.AddSingleton<IPasswordHasher, PasswordHasher>();
    services.AddHttpContextAccessor();
    services.AddScoped<ICurrentUser, CurrentUser>();
    services.TryAddSingleton(TimeProvider.System);
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
