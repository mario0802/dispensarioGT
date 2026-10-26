using Dispensario.Application.UnitTests.Persistence.Fakes;
using Dispensario.Domain.Common;
using Dispensario.Domain.Users;
using Dispensario.Infrastructure.Authentication;
using Dispensario.Infrastructure.Data.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dispensario.Application.UnitTests.Persistence.Seed;

public sealed class DatabaseSeederTests : IDisposable
{
    private const string Password = "Admin-Seed-Password-1";

    private readonly DispensarioTestDb _db = new();
    private readonly BcryptPasswordHasher _hasher = new();
    private readonly ListLogger<DatabaseSeeder> _logger = new();

    [Fact]
    public async Task SeedAsync_WithPassword_CreatesAdminUser()
    {
        await SeedAsync(CreateOptions(Password));

        await using var context = _db.CreateContext();
        var admin = await context.Users.SingleAsync(TestContext.Current.CancellationToken);
        admin.Email.ShouldBe(Email.Create("admin@dispensario.local"));
        admin.FullName.ShouldBe(FullName.Create("System", "Admin"));
        admin.Identification.ShouldBe(Document.Create(DocumentType.Passport, "AB123456"));
        admin.Role.ShouldBe(Role.Admin);
        admin.IsActive.ShouldBeTrue();
        admin.CreatedBy.ShouldBeNull();
        _hasher.Verify(admin.PasswordHash, Password).ShouldBeTrue();
    }

    [Fact]
    public async Task SeedAsync_RunTwice_DoesNotDuplicateAdmin()
    {
        await SeedAsync(CreateOptions(Password));

        await SeedAsync(CreateOptions(Password));

        await using var context = _db.CreateContext();
        (await context.Users.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(1);
    }

    [Fact]
    public async Task SeedAsync_WithEmptyPassword_SkipsSeedAndLogsWarning()
    {
        await SeedAsync(CreateOptions(string.Empty));

        await using var context = _db.CreateContext();
        (await context.Users.AnyAsync(TestContext.Current.CancellationToken)).ShouldBeFalse();
        _logger.Entries.ShouldContain(entry =>
            entry.Level == LogLevel.Warning
            && entry.Message == "Admin seed skipped: 'Seed:Admin:Password' is empty.");
    }

    public void Dispose() => _db.Dispose();

    private async Task SeedAsync(AdminSeedOptions options)
    {
        await using var context = _db.CreateContext();
        var seeder = new DatabaseSeeder(context, _hasher, Options.Create(options), _logger);
        await seeder.SeedAsync(TestContext.Current.CancellationToken);
    }

    private static AdminSeedOptions CreateOptions(string password) => new()
    {
        Email = "admin@dispensario.local",
        Password = password,
        FirstName = "System",
        LastName = "Admin",
        DocumentType = DocumentType.Passport,
        DocumentNumber = "AB123456",
    };

    // Logger mínimo que guarda los mensajes para poder comprobarlos
    private sealed class ListLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Entries.Add((logLevel, formatter(state, exception)));
    }
}
