using Dispensario.Application.UnitTests.Persistence.Fakes;
using Dispensario.Domain.Common;
using Dispensario.Infrastructure.Data.Converters;
using Microsoft.EntityFrameworkCore;

namespace Dispensario.Application.UnitTests.Persistence;

public sealed class StronglyTypedIdConventionTests : IDisposable
{
    private static readonly DateTime ScheduledAtUtc = new(2026, 10, 26, 9, 0, 0, DateTimeKind.Utc);

    private readonly TestDb _db = new();

    [Fact]
    public void Model_StronglyTypedIds_UseConverterAndTextColumn()
    {
        using var context = _db.CreateContext();
        var entityType = context.Model.FindEntityType(typeof(TestAggregate))!;

        var id = entityType.FindProperty(nameof(TestAggregate.Id))!;
        var parentId = entityType.FindProperty(nameof(TestAggregate.ParentId))!;

        id.GetValueConverter().ShouldBeOfType<StronglyTypedIdConverter<TestAggregateId>>();
        id.GetColumnType().ShouldBe("TEXT");
        parentId.GetValueConverter().ShouldNotBeNull();
        parentId.GetColumnType().ShouldBe("TEXT");
    }

    [Fact]
    public async Task SaveChangesAsync_WithIdAndNullableId_ReadsBackSameValues()
    {
        var parentId = TestAggregateId.New();
        var aggregate = TestAggregate.Create(FullName.Create("Ana", "Pérez"), ScheduledAtUtc, parentId: parentId);
        await SaveAsync(aggregate);

        await using var context = _db.CreateContext();
        var loaded = await context.Aggregates.SingleAsync(TestContext.Current.CancellationToken);

        loaded.Id.ShouldBe(aggregate.Id);
        loaded.ParentId.ShouldBe(parentId);
    }

    [Fact]
    public async Task SaveChangesAsync_WithNullParentId_ReadsBackNull()
    {
        var aggregate = TestAggregate.Create(FullName.Create("Ana", "Pérez"), ScheduledAtUtc);
        await SaveAsync(aggregate);

        await using var context = _db.CreateContext();
        var loaded = await context.Aggregates.SingleAsync(TestContext.Current.CancellationToken);

        loaded.ParentId.ShouldBeNull();
    }

    [Fact]
    public async Task Where_ByStronglyTypedId_FindsAggregate()
    {
        var parentId = TestAggregateId.New();
        var target = TestAggregate.Create(FullName.Create("Ana", "Pérez"), ScheduledAtUtc, parentId: parentId);
        var other = TestAggregate.Create(FullName.Create("Luis", "Gómez"), ScheduledAtUtc);
        await SaveAsync(target, other);

        await using var context = _db.CreateContext();
        var byId = await context.Aggregates
            .Where(x => x.Id == target.Id)
            .ToListAsync(TestContext.Current.CancellationToken);
        var byParentId = await context.Aggregates
            .Where(x => x.ParentId == parentId)
            .ToListAsync(TestContext.Current.CancellationToken);

        byId.Single().Id.ShouldBe(target.Id);
        byParentId.Single().Id.ShouldBe(target.Id);
    }

    public void Dispose() => _db.Dispose();

    private async Task SaveAsync(params TestAggregate[] aggregates)
    {
        await using var context = _db.CreateContext();
        context.Aggregates.AddRange(aggregates);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
