# SPEC 04 — Persistencia base

> **Status:** Implemented
> **Depends on:** SPEC 03
> **Date:** 2026-10-26
> **Objective:** Crear la base de persistencia (`IApplicationDbContext`, `DispensarioDbContext` sobre SQLite, interceptores de auditoría y de domain events, convenciones de IDs tipados y de fechas UTC, y `AddInfrastructure`), probada con un `DbContext` de pruebas porque aún no existe ningún aggregate.

## Por qué este spec existe

La fila 04 llega antes que cualquier aggregate, ID tipado o usuario autenticado.
`INFRASTRUCTURE.md` §2 propone registrar a mano un converter por cada ID en `ConfigureConventions`, lo que obliga a tocar el `DbContext` en cada spec de módulo.
Este spec lo sustituye por una convención genérica basada en una interfaz marcadora del dominio.
`AuditableEntityInterceptor` necesita `ICurrentUser`, que según el plan llega en la fila 07: aquí se crea una versión mínima con una implementación provisional anónima.
Además, SPEC 03 dejó pendiente comprobar que los VOs con constructor privado encajan con `ComplexProperty` de EF Core 10, y se verifica aquí.

## Scope

**In:**

- Paquetes EF Core 10 (`Microsoft.EntityFrameworkCore`, `Microsoft.EntityFrameworkCore.Sqlite`) y `Microsoft.Extensions.TimeProvider.Testing` en `Directory.Packages.props`.
- `IStronglyTypedId` e `IHasDomainEvents` en `BuildingBlocks.Domain`; `AggregateRoot<TId>` implementa `IHasDomainEvents`.
- `IApplicationDbContext` (solo `SaveChangesAsync`) e `ICurrentUser` mínimo (`Guid? UserId`, `bool IsAuthenticated`) en `Dispensario.Application/Abstractions/`.
- `DispensarioDbContext` sin `DbSet`, con `ApplyConfigurationsFromAssembly` y las convenciones de IDs tipados y de `DateTime` UTC.
- `StronglyTypedIdConverter<TId>` y `UtcDateTimeConverter` / `NullableUtcDateTimeConverter`, más las extensiones de `ModelConfigurationBuilder` que los aplican.
- `AuditableEntityInterceptor` (incluye cambios en entidades propias) y `DispatchDomainEventsInterceptor` (después del commit).
- `AnonymousCurrentUser`, implementación provisional de `ICurrentUser`.
- `AddInfrastructure(IServiceCollection, IConfiguration)` con la parte de persistencia.
- Tests en `tests/Dispensario.Application.UnitTests/Persistence/` con un `TestDbContext` sobre SQLite `:memory:`.
- Actualizar `INFRASTRUCTURE.md`, `APPLICATION.md`, `DOMAIN.md`, `TESTING.md`, `CLAUDE.md` y `docs/PLAN.md`.

**Out of scope (for future specs):**

- `DbSet` de los módulos y sus configuraciones EF: llegan con cada aggregate (filas 06, 09, 11, 12, 15).
- Llamar a `AddInfrastructure` desde `Program.cs`, `appsettings.json` con `ConnectionStrings:Database` y el registro de MediatR (`IPublisher`): fila 05.
- `ICurrentUser` completo (`UserId` tipado, `Role`, `DoctorId`) y `CurrentUser` sobre los claims: fila 07.
- JWT, `PasswordHasher` y `DatabaseSeeder`: filas 07 y 17.
- `Microsoft.EntityFrameworkCore.Design`, migraciones e `InitialiseDatabaseAsync`: fila 17.
- Proteger `CreatedAtUtc` / `CreatedBy` frente a modificaciones en un update.
- *Outbox* y metadatos de los domain events.

## Data model

### Paquetes

| Paquete | Versión | Lo referencia |
| --- | --- | --- |
| `Microsoft.EntityFrameworkCore` | 10.0.14 | `Dispensario.Application` |
| `Microsoft.EntityFrameworkCore.Sqlite` | 10.0.14 | `Dispensario.Infrastructure` |
| `Microsoft.Extensions.TimeProvider.Testing` | 10.11.0 (última estable al implementar) | `Dispensario.Application.UnitTests` |

- Las versiones se declaran solo en `Directory.Packages.props`.
- Si 10.0.14 no existe para EF Core, se usa la última 10.0.x estable y se anota en el spec al implementar.

### `BuildingBlocks.Domain` (namespace `BuildingBlocks.Domain`)

```csharp
// IStronglyTypedId.cs — marca los IDs tipados para que Infrastructure los convierta a Guid por convención
public interface IStronglyTypedId
{
    Guid Value { get; }
}

// IHasDomainEvents.cs — vista no genérica de AggregateRoot<TId> para el interceptor de eventos
public interface IHasDomainEvents
{
    IReadOnlyCollection<IDomainEvent> DomainEvents { get; }
    void ClearDomainEvents();
}

// AggregateRoot.cs — solo cambia la lista de interfaces
public abstract class AggregateRoot<TId> : Entity<TId>, IAuditable, IHasDomainEvents where TId : notnull
```

Forma que tendrán los IDs tipados de los módulos (no se crea ninguno en este spec):

```csharp
public readonly record struct PatientId(Guid Value) : IStronglyTypedId
{
    public static PatientId New() => new(Guid.CreateVersion7());
}
```

- Requisito de la convención: un `struct` no abstracto que implementa `IStronglyTypedId` y tiene un constructor público `(Guid value)`.

### `Dispensario.Application/Abstractions/` (namespace `Dispensario.Application.Abstractions`)

```csharp
public interface IApplicationDbContext
{
    // Cada spec de módulo añade aquí su DbSet<T>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

// Versión mínima: la fila 07 cambia UserId a UserId? y añade Role y DoctorId
public interface ICurrentUser
{
    Guid? UserId { get; }
    bool IsAuthenticated { get; }
}
```

### `Dispensario.Infrastructure/` (namespaces iguales a las carpetas)

```
Data/
  DispensarioDbContext.cs
  Converters/StronglyTypedIdConverter.cs
  Converters/UtcDateTimeConverter.cs              # UtcDateTimeConverter y NullableUtcDateTimeConverter
  Extensions/ModelConfigurationBuilderExtensions.cs
  Interceptors/AuditableEntityInterceptor.cs
  Interceptors/DispatchDomainEventsInterceptor.cs
Authentication/AnonymousCurrentUser.cs
DependencyInjection.cs
```

```csharp
public sealed class DispensarioDbContext(DbContextOptions<DispensarioDbContext> options)
    : DbContext(options), IApplicationDbContext
{
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

- `ApplyStronglyTypedIdConventions(this ModelConfigurationBuilder, Assembly)`: busca en el ensamblado los tipos que cumplen el requisito y, para cada uno, llama a `Properties(type).HaveConversion(typeof(StronglyTypedIdConverter<>).MakeGenericType(type))`. `DispensarioDbContext` le pasa el ensamblado de `Dispensario.Domain` (con `Dispensario.Domain.Common.Email` como tipo ancla); el `TestDbContext` le pasa el ensamblado de tests.
- `StronglyTypedIdConverter<TId> : ValueConverter<TId, Guid>` con `where TId : struct, IStronglyTypedId`: hacia la BD usa `id.Value`; desde la BD construye `TId` con su constructor `(Guid)` mediante un árbol de expresión. Si el tipo no tiene ese constructor, lanza `InvalidOperationException` al construir el modelo.
- `ApplyUtcDateTimeConventions(this ModelConfigurationBuilder)`: `Properties<DateTime>()` con `UtcDateTimeConverter` y `Properties<DateTime?>()` con `NullableUtcDateTimeConverter`. Al leer aplican `DateTime.SpecifyKind(value, DateTimeKind.Utc)`; al escribir dejan el valor como está.
- Sin `DbSet` en este spec.

### Interceptores

| Interceptor | Base | Momento | Comportamiento |
| --- | --- | --- | --- |
| `AuditableEntityInterceptor(TimeProvider, ICurrentUser)` | `SaveChangesInterceptor` | `SavingChanges` y `SavingChangesAsync` | Para cada entrada `IAuditable`: en `Added` fija `CreatedAtUtc = now` y `CreatedBy`; en `Modified`, o si está `Unchanged` pero tiene alguna entidad propia (`OwnsOne`/`OwnsMany`) en `Added`, `Modified` o `Deleted`, fija `LastModifiedAtUtc = now` y `LastModifiedBy` |
| `DispatchDomainEventsInterceptor(IPublisher)` | `SaveChangesInterceptor` | `SavedChangesAsync` (después del commit) | Recoge los eventos de las entradas `IHasDomainEvents`, limpia sus listas **antes** de publicar y publica cada evento con `IPublisher.Publish` en orden, propagando el `CancellationToken` |

- `now = timeProvider.GetUtcNow().UtcDateTime`.
- `CreatedBy` / `LastModifiedBy = currentUser.UserId?.ToString()`.
- `DispatchDomainEventsInterceptor.SavingChanges` (síncrono, **antes** del commit): si hay eventos pendientes lanza `InvalidOperationException("Domain events can only be dispatched with SaveChangesAsync.")`; si no los hay, no hace nada. *(Ajuste en la implementación: el borrador decía `SavedChanges`, pero lanzar después del commit dejaría los datos guardados y los eventos sin publicar.)*
- Si `SaveChanges` falla, `SavedChanges` no se ejecuta y los eventos siguen en el aggregate.

### `AnonymousCurrentUser` y `AddInfrastructure`

```csharp
// Provisional hasta la fila 07: no hay autenticación todavía
public sealed class AnonymousCurrentUser : ICurrentUser
{
    public Guid? UserId => null;
    public bool IsAuthenticated => false;
}

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
    services.AddScoped<ICurrentUser, AnonymousCurrentUser>();
    services.TryAddSingleton(TimeProvider.System);
    return services;
}
```

- `IPublisher` no se registra aquí: lo registra MediatR en `AddApplication` (fila 05).
- La cadena de conexión se lee al llamar a `AddInfrastructure`, así que su ausencia falla en el arranque y no al resolver el `DbContext`.

### Tests (`tests/Dispensario.Application.UnitTests/Persistence/`)

Tipos de apoyo en `Persistence/Fakes/`:

| Tipo | Contenido |
| --- | --- |
| `TestAggregateId` | `readonly record struct (Guid Value) : IStronglyTypedId` con `New()` |
| `TestAggregate : AggregateRoot<TestAggregateId>` | `FullName Name` (complex), `Address? Address` (complex opcional), `TestAggregateId? ParentId`, `DateTime ScheduledAtUtc`, `IReadOnlyCollection<TestChild> Children`, métodos `Create`, `Rename`, `AddChild`, `RaiseTestEvent` |
| `TestChild` | entidad propia mapeada con `OwnsMany` en tabla `TestChildren` |
| `TestDomainEvent` | `sealed record (TestAggregateId Id, int Number) : IDomainEvent`; `Number` distingue dos eventos del mismo aggregate al comprobar el orden |
| `TestDbContext` | `DbSet<TestAggregate>`; aplica `ApplyStronglyTypedIdConventions(typeof(TestDbContext).Assembly)` y `ApplyUtcDateTimeConventions()`; configura `TestAggregate` con `ComplexProperty` para `Name` y `Address`, declarando cada propiedad del VO con `Property(...)` |
| `TestDb` | `IDisposable`: abre una `SqliteConnection("Data Source=:memory:")`, crea `TestDbContext` con los dos interceptores reales (`FakeTimeProvider`, `ICurrentUser` e `IPublisher` de NSubstitute) y llama a `EnsureCreated()` |

| Archivo | Qué prueba |
| --- | --- |
| `StronglyTypedIdConventionTests.cs` | El ID y el ID nullable se guardan como Guid (columna `TEXT`) y se releen iguales; `ParentId` null se relee null; se puede filtrar con `Where(x => x.Id == id)` |
| `UtcDateTimeConventionTests.cs` | Un `DateTime` UTC releído desde un contexto nuevo tiene `Kind == Utc` y el mismo valor; lo mismo para `CreatedAtUtc` y `LastModifiedAtUtc` (nullable) |
| `ComplexValueObjectMappingTests.cs` | `FullName` y `Address` se guardan y se releen iguales con un contexto nuevo; `Address` null se relee null |
| `AuditableEntityInterceptorTests.cs` | `Added` fija `CreatedAtUtc` y `CreatedBy`; `Modified` fija `LastModifiedAtUtc` y `LastModifiedBy`; añadir un `TestChild` a un aggregate sin más cambios fija `LastModifiedAtUtc`; usuario anónimo → `CreatedBy` null |
| `DispatchDomainEventsInterceptorTests.cs` | Tras `SaveChangesAsync` se publican los eventos en orden y la lista queda vacía; sin eventos no se llama a `Publish`; `SaveChanges` síncrono con eventos lanza `InvalidOperationException`; si el guardado falla (violación de clave) no se publica nada y los eventos siguen en el aggregate |
| `DependencyInjectionTests.cs` | Con `ConnectionStrings:Database = "Data Source=:memory:"` y un `IPublisher` sustituto, `IApplicationDbContext` se resuelve como la misma instancia que `DispensarioDbContext` en el scope y su modelo se construye; `ICurrentUser` es `AnonymousCurrentUser`; sin cadena de conexión `AddInfrastructure` lanza `InvalidOperationException` |

- Convenciones de `TESTING.md`: `Method_Scenario_ExpectedResult`, Arrange / Act / Assert, Shouldly y NSubstitute.
- Las relecturas usan un `TestDbContext` nuevo sobre la misma conexión para no leer del *change tracker*.

## Implementation plan

1. Añadir `Microsoft.EntityFrameworkCore`, `Microsoft.EntityFrameworkCore.Sqlite` y `Microsoft.Extensions.TimeProvider.Testing` a `Directory.Packages.props` y referenciarlos desde `Dispensario.Application`, `Dispensario.Infrastructure` y `Dispensario.Application.UnitTests`. Verificar: `dotnet build Dispensario.slnx` y `dotnet test Dispensario.slnx` en verde.
2. Crear `IStronglyTypedId.cs` e `IHasDomainEvents.cs` en `BuildingBlocks.Domain` y hacer que `AggregateRoot<TId>` implemente `IHasDomainEvents`. Ampliar `AggregateRootTests` con un caso que compruebe que es `IHasDomainEvents`. Verificar: `dotnet test` en verde.
3. Crear `Abstractions/IApplicationDbContext.cs` y `Abstractions/ICurrentUser.cs` en `Dispensario.Application`. Verificar: compila.
4. Crear `Data/Converters/StronglyTypedIdConverter.cs`, `Data/Converters/UtcDateTimeConverter.cs` y `Data/Extensions/ModelConfigurationBuilderExtensions.cs`. Verificar: compila.
5. Crear `Data/DispensarioDbContext.cs`. Verificar: compila.
6. Crear los tipos de `Persistence/Fakes/` (sin interceptores todavía en `TestDb`), `StronglyTypedIdConventionTests`, `UtcDateTimeConventionTests` y `ComplexValueObjectMappingTests`. Si el mapeo de los VOs falla, ajustar los VOs de `Dispensario.Domain/Common` aquí y anotarlo en el spec. Verificar: `dotnet test` en verde.
7. Crear `Data/Interceptors/AuditableEntityInterceptor.cs`, añadirlo a `TestDb` y crear `AuditableEntityInterceptorTests`. Verificar: `dotnet test` en verde.
8. Crear `Data/Interceptors/DispatchDomainEventsInterceptor.cs`, añadirlo a `TestDb` y crear `DispatchDomainEventsInterceptorTests`. Verificar: `dotnet test` en verde.
9. Crear `Authentication/AnonymousCurrentUser.cs`, `DependencyInjection.cs` con `AddInfrastructure` y `DependencyInjectionTests`. Verificar: `dotnet test` en verde.
10. Actualizar la documentación:
    - `docs/guides/INFRASTRUCTURE.md`: §1 con la estructura nueva (`Converters/`, `Extensions/ModelConfigurationBuilderExtensions.cs`, `AnonymousCurrentUser`); §2 con las convenciones genéricas en lugar del registro por ID y el `DateTime` UTC; §5 con las entidades propias en la auditoría y el `SaveChanges` síncrono; §8 indicando qué parte de `AddInfrastructure` existe ya y qué añade la fila 07.
    - `docs/guides/APPLICATION.md` §2: `IApplicationDbContext` crece con cada módulo; `ICurrentUser` mínimo hasta la fila 07.
    - `docs/guides/DOMAIN.md`: §1 con `IStronglyTypedId` e `IHasDomainEvents`; §2 regla 4 con `: IStronglyTypedId` en el ejemplo.
    - `docs/guides/TESTING.md`: tests de persistencia con `TestDbContext` en `Dispensario.Application.UnitTests/Persistence/`.
    - `CLAUDE.md`: "Estado actual" (persistencia base lista; las migraciones llegan en la fila 17) y la regla de IDs tipados.
    - `docs/PLAN.md`: la fila 04 apunta a este spec.

## Acceptance criteria

- [x] `dotnet build Dispensario.slnx` termina sin errores y sin warnings nuevos respecto a SPEC 03.
- [x] `dotnet test Dispensario.slnx` termina con éxito e incluye las seis clases de tests de `Persistence/` y los tests de SPEC 03.
- [x] `Directory.Packages.props` declara `Microsoft.EntityFrameworkCore`, `Microsoft.EntityFrameworkCore.Sqlite` y `Microsoft.Extensions.TimeProvider.Testing`, y ningún `.csproj` lleva atributo `Version`.
- [x] `BuildingBlocks.Domain` sigue sin más paquetes que `MediatR.Contracts` y contiene `IStronglyTypedId` e `IHasDomainEvents`.
- [x] `Dispensario.Domain` no referencia EF Core.
- [x] `AggregateRoot<TId>` implementa `IHasDomainEvents`.
- [x] `IApplicationDbContext` e `ICurrentUser` existen en `Dispensario.Application/Abstractions/` con los miembros del data model.
- [x] `DispensarioDbContext` implementa `IApplicationDbContext` y no declara ningún `DbSet`.
- [x] Un ID tipado y un ID tipado nullable se guardan y se releen iguales, y se pueden usar en un `Where` (cubierto por `StronglyTypedIdConventionTests`).
- [x] Todo `DateTime` y `DateTime?` releído de SQLite tiene `Kind == DateTimeKind.Utc` (cubierto por `UtcDateTimeConventionTests`).
- [x] `FullName` y `Address?` se mapean con `ComplexProperty` y se releen iguales, incluido `Address` null (cubierto por `ComplexValueObjectMappingTests`).
- [x] Con `FakeTimeProvider` fijado a una fecha, `CreatedAtUtc` toma esa fecha al insertar y `LastModifiedAtUtc` la nueva fecha al modificar, también cuando solo cambia una entidad propia.
- [x] `CreatedBy` es el `UserId` del `ICurrentUser` como texto, y `null` con un usuario anónimo.
- [x] Tras `SaveChangesAsync`, `IPublisher.Publish` recibe cada evento una vez y en orden, y `DomainEvents` queda vacío.
- [x] Si `SaveChangesAsync` falla, no se publica ningún evento y los eventos siguen en el aggregate.
- [x] `SaveChanges` síncrono con eventos pendientes lanza `InvalidOperationException`.
- [x] `AddInfrastructure` sin `ConnectionStrings:Database` lanza `InvalidOperationException`; con ella, `IApplicationDbContext` y `DispensarioDbContext` resuelven la misma instancia en un scope.
- [x] Ningún archivo nuevo de `src/` contiene `DateTime.Now` ni `DateTime.UtcNow`.
- [x] `Program.cs` no ha cambiado.
- [x] `INFRASTRUCTURE.md`, `APPLICATION.md`, `DOMAIN.md`, `TESTING.md` y `CLAUDE.md` describen las convenciones genéricas, el `ICurrentUser` mínimo y los tests de persistencia, y `INFRASTRUCTURE.md` ya no propone un `HaveConversion` por cada ID.
- [x] `docs/PLAN.md` fila 04 apunta a `specs/04-persistence-base.md`.

## Decisions

- **Yes:** convención genérica para los IDs tipados (`IStronglyTypedId` + `StronglyTypedIdConverter<TId>` + escaneo del ensamblado de Domain). Los specs de módulo no tocan el `DbContext` para registrar IDs.
- **No:** un converter por ID registrado a mano (`INFRASTRUCTURE.md` §2). Una línea más por módulo y fácil de olvidar.
- **No:** converter genérico sin interfaz marcadora. Seguiría exigiendo un registro manual por ID.
- **Yes:** `IStronglyTypedId` no genérico con `Guid Value`. Todos los IDs de v1 son `Guid` v7.
- **No:** `IStronglyTypedId<TSelf>` con un `static abstract From(Guid)`. Añade un miembro a cada ID para lo que ya hace su constructor.
- **Yes:** `IHasDomainEvents` en `BuildingBlocks.Domain`. El interceptor necesita leer los eventos sin conocer el `TId` de cada aggregate, y evita reflexión.
- **Yes:** `ICurrentUser` mínimo (`Guid? UserId`, `IsAuthenticated`) con `AnonymousCurrentUser` provisional. El interceptor queda completo ya y la fila 07 solo cambia la implementación y amplía la interfaz.
- **No:** rellenar solo las fechas y dejar `CreatedBy` para la fila 07. Obligaría a volver a modificar y probar el interceptor.
- **No:** una interfaz estrecha `IAuditUserProvider`. Otra abstracción para un único dato que `ICurrentUser` ya va a tener.
- **Yes:** `Guid? UserId` en lugar de `string?`. Se acerca al `UserId?` tipado final; el cambio en la fila 07 es local.
- **Yes:** convención global que marca como UTC todo `DateTime` leído de SQLite. Sin ella el JSON sale sin `Z` y las comparaciones con `TimeProvider` pueden fallar.
- **Yes:** la auditoría trata como modificado un aggregate cuyas entidades propias cambian. Cambiar el horario de un doctor (`OwnsMany`) debe actualizar `LastModifiedAtUtc`.
- **No:** proteger `CreatedAtUtc` / `CreatedBy` en los updates. No se pidió; los setters solo los usa el interceptor.
- **Yes:** eventos despachados en `SavedChangesAsync`, después del commit, limpiando la lista antes de publicar. Así lo fija `INFRASTRUCTURE.md` §5 y un handler que vuelva a guardar no republica los mismos eventos.
- **Yes:** `SaveChanges` síncrono con eventos pendientes lanza excepción. Publicar de forma síncrona bloquearía hilos y perder eventos en silencio sería peor.
- **Yes:** `IApplicationDbContext` sin `DbSet` y `DispensarioDbContext` vacío. Cada módulo añade el suyo con su aggregate.
- **Yes:** `Microsoft.EntityFrameworkCore` se referencia ya desde Application, como acepta `ARCHITECTURE.md` §2, aunque todavía no se use `DbSet`. Es el spec de la base de persistencia.
- **Yes:** probar con un `TestDbContext` en `Dispensario.Application.UnitTests/Persistence/` que usa los interceptores y las convenciones reales. Es la única forma de probarlos sin aggregates.
- **No:** un proyecto `Dispensario.Infrastructure.UnitTests`. Application.UnitTests ya referencia Infrastructure y usa SQLite según `TESTING.md`.
- **No:** posponer los tests a la fila 06. Los interceptores y convenciones son la base de todos los módulos.
- **Yes:** comprobar aquí que los VOs de SPEC 03 funcionan con `ComplexProperty`. Cierra el riesgo que dejó abierto SPEC 03.
- **Yes:** `AddInfrastructure` no se llama desde `Program.cs` en este spec. `IPublisher` no existe hasta que la fila 05 registre MediatR.
- **Yes:** la cadena de conexión se valida al registrar. Un error de configuración se ve al arrancar.
- **Yes (al implementar):** los VOs de SPEC 03 no se modifican. EF Core 10 no mapea por convención sus propiedades de solo lectura (`No suitable constructor was found for the type 'TestAggregate.Address#Address'`), pero sí lo hace si cada `ComplexProperty` las declara con `Property(...)`, y entonces usa el constructor privado. Las configuraciones de los módulos ya declaran cada propiedad para fijar `HasMaxLength`.
- **No (al implementar):** cambiar los VOs a `{ get; private set; }`. Rompe su inmutabilidad para algo que se resuelve en la configuración.
- **Yes (al implementar):** `Address?` como `ComplexProperty` opcional funciona en EF Core 10 y se relee `null`; no hace falta `OwnsOne`. Los IDs nullable reciben la conversión sin registrar `Nullable<TId>` aparte.
- **Yes (al implementar):** el `SaveChanges` síncrono con eventos se rechaza en `SavingChanges`, antes del commit, y no en `SavedChanges`.
- **No:** `Microsoft.EntityFrameworkCore.Design` ni migraciones. No hay entidades que migrar hasta los módulos; la migración inicial es la fila 17.

## Risks

| Risk | Mitigation |
| --- | --- |
| `Properties(typeof(TId))` no aplica la conversión a `TId?` (IDs nullable) | `StronglyTypedIdConventionTests` cubre un ID nullable. Si falla, la extensión registra también `Properties(typeof(Nullable<>).MakeGenericType(type))`. |
| `ComplexProperty` opcional (`Address?`) o el constructor privado de los VOs no funciona en EF Core 10 con SQLite | `ComplexValueObjectMappingTests` lo detecta en el paso 6. Se ajustan los VOs (p. ej. constructor sin parámetros privado) o se usa `OwnsOne` para `Address`, y se anota en Decisions. |
| La conversión UTC global choca con un converter explícito de una configuración futura | Las configuraciones explícitas tienen prioridad sobre las convenciones; se documenta en `INFRASTRUCTURE.md`. |
| `IConfiguration` o `ISaveChangesInterceptor` no están disponibles en Infrastructure sin un paquete extra | `Microsoft.AspNetCore.App` llega de forma transitiva desde `BuildingBlocks`. Si no, se añade el `FrameworkReference` en Infrastructure. |
| El escaneo de IDs usa un ensamblado equivocado y no encuentra los IDs del dominio | El tipo ancla está en `Dispensario.Domain`; la fila 06 lo confirmará con `UserId`. |
| Excepción de un handler de eventos tras el commit deja los datos guardados y el evento perdido | Comportamiento aceptado en v1 (`INFRASTRUCTURE.md` §5); se resuelve con el *outbox*. |

## What is **not** in this spec

- `DbSet` y configuraciones EF de los módulos.
- `Program.cs`, `appsettings.json` y el registro de MediatR.
- `ICurrentUser` completo, `CurrentUser`, JWT, hasher y seed.
- Paquete `Design`, migraciones e inicialización de la base de datos.
- Protección de los campos de creación en los updates.
- *Outbox* y metadatos de eventos.

Cada uno de ellos va en su propio spec (filas 05, 06–15 y 17 del backlog).
