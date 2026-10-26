using Dispensario.Application.Abstractions;
using Dispensario.Infrastructure.Data.Interceptors;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace Dispensario.Application.UnitTests.Persistence.Fakes;

// Base de datos SQLite en memoria con los interceptores reales; la conexión sigue abierta mientras viva el TestDb
public sealed class TestDb : IDisposable
{
    public static readonly DateTimeOffset StartTime = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);

    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public TestDb()
    {
        _connection.Open();

        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    public FakeTimeProvider TimeProvider { get; } = new(StartTime);

    public ICurrentUser CurrentUser { get; } = Substitute.For<ICurrentUser>();

    public IPublisher Publisher { get; } = Substitute.For<IPublisher>();

    // Cada llamada da un contexto nuevo, así las relecturas no salen del change tracker
    public TestDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(
                new AuditableEntityInterceptor(TimeProvider, CurrentUser),
                new DispatchDomainEventsInterceptor(Publisher))
            .Options;

        return new TestDbContext(options);
    }

    public void Dispose() => _connection.Dispose();
}
