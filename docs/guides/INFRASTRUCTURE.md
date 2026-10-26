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
  Converters/StronglyTypedIdConverters.cs
  Interceptors/AuditableEntityInterceptor.cs
  Interceptors/DispatchDomainEventsInterceptor.cs
  Migrations/
  Seed/DatabaseSeeder.cs
  Extensions/DatabaseExtensions.cs        # InitialiseDatabaseAsync (migrar + seed)
Authentication/
  JwtOptions.cs  JwtTokenGenerator.cs  PasswordHasher.cs  CurrentUser.cs
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

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        // Conversión global de IDs tipados a Guid
        builder.Properties<PatientId>().HaveConversion<PatientIdConverter>();
        // ... uno por cada ID
    }
}
```

## 3. Configuraciones EF por módulo
Reglas:
- Una clase `IEntityTypeConfiguration<T>` por aggregate, en la carpeta de su módulo.
- **Tabla explícita** (`ToTable("Patients")`).
- Los value objects se mapean con `ComplexProperty` (EF 10) u `OwnsOne`, con columnas prefijadas (`Document_Type`, `Document_Number`).
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
- **AuditableEntityInterceptor** (`SavingChanges`): para cada `IAuditable` en `Added` completa `CreatedAt`/`CreatedBy`, y en `Modified`, `LastModifiedAt`/`LastModifiedBy`. Usa `TimeProvider` e `ICurrentUser`.
- **DispatchDomainEventsInterceptor** (`SavedChanges`): recoge los eventos de los aggregates rastreados, limpia sus listas y los publica con `IPublisher.Publish`. Se despachan **después** del commit. Si un handler de eventos modifica datos, llama a su propio `SaveChangesAsync`.
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
```csharp
public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
{
    services.AddScoped<ISaveChangesInterceptor, AuditableEntityInterceptor>();
    services.AddScoped<ISaveChangesInterceptor, DispatchDomainEventsInterceptor>();

    services.AddDbContext<DispensarioDbContext>((sp, options) =>
    {
        options.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>());
        options.UseSqlite(configuration.GetConnectionString("Database"));
    });
    services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<DispensarioDbContext>());

    services.AddOptions<JwtOptions>().BindConfiguration(JwtOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();
    services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();
    services.AddSingleton<IPasswordHasher, PasswordHasher>();
    services.AddHttpContextAccessor();
    services.AddScoped<ICurrentUser, CurrentUser>();
    services.AddSingleton(TimeProvider.System);
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
