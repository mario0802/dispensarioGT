using Dispensario.Application.UnitTests.Persistence.Fakes;
using Dispensario.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Dispensario.Application.UnitTests.Persistence;

public sealed class ComplexValueObjectMappingTests : IDisposable
{
    private static readonly DateTime ScheduledAtUtc = new(2026, 10, 26, 9, 0, 0, DateTimeKind.Utc);

    private readonly TestDb _db = new();

    [Fact]
    public async Task SaveChangesAsync_WithFullNameAndAddress_ReadsBackEqualValueObjects()
    {
        var name = FullName.Create("Ana", "Pérez");
        var address = Address.Create("Av. Sol 123", "Lima", "Lima", "Frente al parque");
        await SaveAsync(TestAggregate.Create(name, ScheduledAtUtc, address));

        await using var context = _db.CreateContext();
        var loaded = await context.Aggregates.SingleAsync(TestContext.Current.CancellationToken);

        loaded.Name.ShouldBe(name);
        loaded.Address.ShouldBe(address);
    }

    [Fact]
    public async Task SaveChangesAsync_WithAddressWithoutOptionalFields_ReadsBackNullOptionals()
    {
        var address = Address.Create("Av. Sol 123", "Lima");
        await SaveAsync(TestAggregate.Create(FullName.Create("Ana", "Pérez"), ScheduledAtUtc, address));

        await using var context = _db.CreateContext();
        var loaded = await context.Aggregates.SingleAsync(TestContext.Current.CancellationToken);

        loaded.Address.ShouldNotBeNull();
        loaded.Address.ShouldBe(address);
        loaded.Address.State.ShouldBeNull();
        loaded.Address.Reference.ShouldBeNull();
    }

    [Fact]
    public async Task SaveChangesAsync_WithNullAddress_ReadsBackNull()
    {
        await SaveAsync(TestAggregate.Create(FullName.Create("Ana", "Pérez"), ScheduledAtUtc));

        await using var context = _db.CreateContext();
        var loaded = await context.Aggregates.SingleAsync(TestContext.Current.CancellationToken);

        loaded.Address.ShouldBeNull();
    }

    public void Dispose() => _db.Dispose();

    private async Task SaveAsync(TestAggregate aggregate)
    {
        await using var context = _db.CreateContext();
        context.Aggregates.Add(aggregate);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
