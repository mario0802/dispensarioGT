using Dispensario.Application.UnitTests.Persistence.Fakes;
using Microsoft.EntityFrameworkCore;

namespace Dispensario.Application.UnitTests.Persistence;

public sealed class MigrationsTests : IDisposable
{
    // DispensarioTestDb aplica las migraciones sobre una base en memoria vacía al crearse
    private readonly DispensarioTestDb _db = new();

    [Fact]
    public void Model_HasNoPendingChanges()
    {
        using var context = _db.CreateContext();

        // Falla si el modelo cambia sin añadir una migración
        context.Database.HasPendingModelChanges().ShouldBeFalse();
    }

    [Fact]
    public async Task Migrate_OnEmptyDatabase_AppliesInitialCreate()
    {
        await using var context = _db.CreateContext();

        var applied = await context.Database.GetAppliedMigrationsAsync(TestContext.Current.CancellationToken);

        applied.ShouldHaveSingleItem().ShouldEndWith("_InitialCreate");
    }

    public void Dispose() => _db.Dispose();
}
