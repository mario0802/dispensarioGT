using Dispensario.Application.Abstractions;
using Dispensario.Infrastructure.Data;
using Dispensario.Infrastructure.Data.Interceptors;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace Dispensario.Application.UnitTests.Persistence.Fakes;

// Como TestDb, pero sobre el DispensarioDbContext real con las configuraciones de los módulos
public sealed class DispensarioTestDb : IDisposable
{
    public static readonly DateTimeOffset StartTime = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);

    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public DispensarioTestDb()
    {
        _connection.Open();

        // Sin migraciones hasta la fila 17: el esquema sale del modelo
        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    public FakeTimeProvider TimeProvider { get; } = new(StartTime);

    public ICurrentUser CurrentUser { get; } = Substitute.For<ICurrentUser>();

    public IPublisher Publisher { get; } = Substitute.For<IPublisher>();

    public SqliteConnection Connection => _connection;

    // Cada llamada da un contexto nuevo, así las relecturas no salen del change tracker
    public DispensarioDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<DispensarioDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(
                new AuditableEntityInterceptor(TimeProvider, CurrentUser),
                new DispatchDomainEventsInterceptor(Publisher))
            .Options;

        return new DispensarioDbContext(options);
    }

    public void Dispose() => _connection.Dispose();
}
