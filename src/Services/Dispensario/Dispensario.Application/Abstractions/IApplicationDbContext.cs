namespace Dispensario.Application.Abstractions;

/// <summary>
/// Unidad de trabajo de la aplicación sobre la base de datos del dispensario.
/// </summary>
public interface IApplicationDbContext
{
    // Cada spec de módulo añade aquí su DbSet<T>

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
