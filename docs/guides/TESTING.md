# Guía de testing

Proyectos en `tests/` en la raíz del repositorio, sin subcarpetas: **xUnit v3**, **Shouldly** para las aserciones y **NSubstitute** para los dobles.

| Proyecto | Qué prueba | Dependencias |
|---|---|---|
| `BuildingBlocks.UnitTests` | Código común: `CustomExceptionHandler` (`Exceptions/`) y abstracciones DDD (`Domain/`: `Entity`, `AggregateRoot`, `ValueObject`) | BuildingBlocks (y BuildingBlocks.Domain de forma transitiva) |
| `Dispensario.Domain.UnitTests` | Invariantes, máquinas de estado, value objects y eventos emitidos | Solo Domain |
| `Dispensario.Application.UnitTests` | Handlers (reglas con varios aggregates, excepciones) y validadores | Application + Infrastructure (DbContext sobre SQLite en memoria) |
| `Dispensario.Api.IntegrationTests` | HTTP de punta a punta: auth, códigos de estado y flujo completo | `WebApplicationFactory<Program>` + SQLite en memoria |

## Convenciones
- Nombre del test: `Method_Scenario_ExpectedResult`, por ejemplo `Cancel_WhenCompleted_ThrowsDomainException`.
- Estructura Arrange / Act / Assert separada por una línea en blanco (sin comentarios AAA).
- Una clase de tests por aggregate o handler: `AppointmentTests`, `ScheduleAppointmentHandlerTests`.
- Builders o *object mothers* de datos de prueba en `Common/` de cada proyecto de tests (p. ej. `DoctorBuilder`).
- **No usar** `EF Core InMemory`: no respeta las restricciones ni el SQL real. Se usa SQLite `Data Source=:memory:` con la conexión abierta durante todo el test.
- `TimeProvider` se reemplaza por `FakeTimeProvider` (`Microsoft.Extensions.TimeProvider.Testing`) para fijar la fecha.

## Tests de persistencia (Application.UnitTests/Persistence)
Las convenciones e interceptores de Infrastructure se prueban sin aggregates reales, con tipos de prueba en `Persistence/Fakes/`:
- `TestAggregate` (ID tipado, ID nullable, VOs `FullName` y `Address?` como `ComplexProperty`, fecha UTC, entidades propias con `OwnsMany`) y `TestDomainEvent`.
- `TestDbContext`, que aplica las mismas convenciones que `DispensarioDbContext` (`ApplyStronglyTypedIdConventions`, `ApplyUtcDateTimeConventions`).
- `TestDb`: SQLite `:memory:` con los interceptores reales, `FakeTimeProvider` e `ICurrentUser`/`IPublisher` de NSubstitute. `CreateContext()` da un contexto nuevo en cada llamada, para releer sin pasar por el *change tracker*.

## SQLite en memoria (Application.UnitTests)
```csharp
public sealed class SqliteTestDb : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    public DispensarioDbContext Context { get; }

    public SqliteTestDb()
    {
        _connection.Open();
        var options = new DbContextOptionsBuilder<DispensarioDbContext>().UseSqlite(_connection).Options;
        Context = new DispensarioDbContext(options);
        Context.Database.Migrate();
    }

    public void Dispose() { Context.Dispose(); _connection.Dispose(); }
}
```
- La versión real es `DispensarioTestDb` (`Persistence/Fakes/`): crea el esquema con `Migrate()`, así que los tests de esquema (`Migrate_*`) verifican la migración y no el modelo. `TestDb`, el contexto genérico de los tests de BuildingBlocks, sigue con `EnsureCreated` porque no tiene migraciones.
- `MigrationsTests` falla si el modelo cambia sin migración (`HasPendingModelChanges`) y comprueba que la base vacía queda con `InitialCreate` aplicada.
- `ListLogger<T>` (`Persistence/Fakes/`) guarda los mensajes del log para los tests de los seeders.

## Integración
- `DispensarioApiFactory : WebApplicationFactory<Program>` (en `tests/Dispensario.Api.IntegrationTests/Common/`) corre en Development y reemplaza la configuración del `DbContext` por SQLite en memoria con una conexión compartida, abierta mientras vive la factory. En EF Core 10 hay que quitar `DbContextOptions<DispensarioDbContext>` **y** `IDbContextOptionsConfiguration<DispensarioDbContext>` antes de volver a llamar a `AddDbContext`, y añadir de nuevo los interceptores. Fija `ConnectionStrings:Database` y `MediatR:LicenseKey` con `UseSetting`.
- Cada clase de tests la usa con `IClassFixture<DispensarioApiFactory>`.
- Fija también los secretos con `UseSetting`: `Jwt:Issuer`, `Jwt:Audience`, `Jwt:Key` (clave de test de 32+ caracteres) y `Seed:Admin:Email`/`Seed:Admin:Password`. Las constantes `JwtIssuer`, `JwtAudience`, `JwtKey`, `AdminEmail` y `AdminPassword` de la factory sirven para los tests.
- Como corre en Development, `InitialiseDatabaseAsync` aplica las migraciones (`MigrateAsync`) y siembra el Admin sobre la conexión en memoria: no hace falta preparar la base en cada test.
- La factory apaga los datos de ejemplo (`Seed:SampleData:Enabled=false`): cada test crea sus propios datos.
- Helper `AuthenticateAsAsync(Role)`: inserta un usuario activo del rol pedido (email y documento únicos por llamada, hash con el `IPasswordHasher` real), hace `POST /api/auth/login` y devuelve un `HttpClient` con `Authorization: Bearer <token>`. Cada llamada cuesta un hash bcrypt (~250 ms).
- Los tests de handlers usan `IPasswordHasher` e `IJwtTokenGenerator` de NSubstitute; solo los tests del hasher, del seed y de integración pagan el coste de bcrypt.
- Las queries que mapean con Mapster registran su `IRegister` en el test (`TypeAdapterConfig.GlobalSettings.Apply(new UsersMappingConfig())`), porque `AddApplication` no se ejecuta en los tests unitarios.
- Los datos de prueba de usuarios salen de `Common/TestUsers.cs` en `Application.UnitTests`.
- Cada spec de módulo añade sus casos: un *happy path* y los errores 400/401/403/404/409 que correspondan.

## Definición de "hecho" para un spec
- `dotnet build` sin warnings nuevos.
- `dotnet test Dispensario.slnx` en verde.
- Cada criterio de aceptación del spec tiene al menos un test que lo cubre.
