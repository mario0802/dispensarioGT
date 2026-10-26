using BuildingBlocks.Domain;
using Dispensario.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Dispensario.Infrastructure.Data.Interceptors;

/// <summary>
/// Completa la auditoría (<see cref="IAuditable"/>) de las entidades creadas o modificadas antes de guardar.
/// </summary>
public sealed class AuditableEntityInterceptor(TimeProvider timeProvider, ICurrentUser currentUser) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        UpdateAuditableEntities(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        UpdateAuditableEntities(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void UpdateAuditableEntities(DbContext? context)
    {
        if (context is null)
            return;

        // El interceptor se ejecuta antes de que SaveChanges detecte los cambios
        context.ChangeTracker.DetectChanges();

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var userId = currentUser.UserId?.Value.ToString();

        var changedOwnedEntries = context.ChangeTracker.Entries()
            .Where(entry => entry.Metadata.IsOwned()
                && entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();

        foreach (var entry in context.ChangeTracker.Entries<IAuditable>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Property(x => x.CreatedAtUtc).CurrentValue = now;
                entry.Property(x => x.CreatedBy).CurrentValue = userId;
            }
            else if (entry.State == EntityState.Modified
                || (entry.State == EntityState.Unchanged && changedOwnedEntries.Any(owned => IsOwnedBy(owned, entry))))
            {
                // Se escribe a través del entry para que un aggregate Unchanged pase a Modified
                entry.Property(x => x.LastModifiedAtUtc).CurrentValue = now;
                entry.Property(x => x.LastModifiedBy).CurrentValue = userId;
            }
        }
    }

    // Una entidad propia apunta a su dueño con la FK de la ownership (p. ej. WeeklySchedule → Doctor)
    private static bool IsOwnedBy(EntityEntry owned, EntityEntry owner)
    {
        var ownership = owned.Metadata.FindOwnership();
        if (ownership is null || !ownership.PrincipalEntityType.IsAssignableFrom(owner.Metadata))
            return false;

        var foreignKey = ownership.Properties;
        var principalKey = ownership.PrincipalKey.Properties;

        return foreignKey
            .Select((property, index) => Equals(
                owned.Property(property.Name).CurrentValue,
                owner.Property(principalKey[index].Name).CurrentValue))
            .All(matches => matches);
    }
}
