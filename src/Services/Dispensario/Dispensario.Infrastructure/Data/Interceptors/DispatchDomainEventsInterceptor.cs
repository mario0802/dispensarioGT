using BuildingBlocks.Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Dispensario.Infrastructure.Data.Interceptors;

/// <summary>
/// Publica los domain events de los aggregates rastreados después de que el guardado se confirma.
/// </summary>
public sealed class DispatchDomainEventsInterceptor(IPublisher publisher) : SaveChangesInterceptor
{
    // Se comprueba antes del commit: publicar de forma síncrona bloquearía hilos,
    // y fallar después de guardar dejaría los datos confirmados y los eventos sin publicar
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is not null && GetAggregatesWithEvents(eventData.Context).Count > 0)
            throw new InvalidOperationException("Domain events can only be dispatched with SaveChangesAsync.");

        return base.SavingChanges(eventData, result);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
            await DispatchDomainEventsAsync(eventData.Context, cancellationToken);

        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    private async Task DispatchDomainEventsAsync(DbContext context, CancellationToken cancellationToken)
    {
        var aggregates = GetAggregatesWithEvents(context);

        var domainEvents = aggregates.SelectMany(aggregate => aggregate.DomainEvents).ToList();

        // Se limpian antes de publicar para que un handler que vuelva a guardar no republique los mismos eventos
        foreach (var aggregate in aggregates)
            aggregate.ClearDomainEvents();

        foreach (var domainEvent in domainEvents)
            await publisher.Publish(domainEvent, cancellationToken);
    }

    private static List<IHasDomainEvents> GetAggregatesWithEvents(DbContext context) =>
        context.ChangeTracker.Entries<IHasDomainEvents>()
            .Select(entry => entry.Entity)
            .Where(aggregate => aggregate.DomainEvents.Count > 0)
            .ToList();
}
