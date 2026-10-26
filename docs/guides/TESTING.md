# Guía de testing

Proyectos en `src/Services/Dispensario/tests/`: **xUnit v3**, **Shouldly** para las aserciones y **NSubstitute** para los dobles.

| Proyecto | Qué prueba | Dependencias |
|---|---|---|
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
        Context.Database.EnsureCreated();
    }

    public void Dispose() { Context.Dispose(); _connection.Dispose(); }
}
```

## Integración
- `DispensarioApiFactory : WebApplicationFactory<Program>` reemplaza el `DbContextOptions` por SQLite en memoria (conexión compartida), fija `Jwt:Key`, `Seed:Admin:*` y `MediatR:LicenseKey` con `UseSetting`, y ejecuta `InitialiseDatabaseAsync`.
- Helper `AuthenticateAsAsync(Role)` para obtener un `HttpClient` con token.
- Cada spec de módulo añade sus casos: un *happy path* y los errores 400/401/403/404/409 que correspondan.

## Definición de "hecho" para un spec
- `dotnet build` sin warnings nuevos.
- `dotnet test Dispensario.slnx` en verde.
- Cada criterio de aceptación del spec tiene al menos un test que lo cubre.
