using Dispensario.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Dispensario.Application.Abstractions;

/// <summary>
/// Unidad de trabajo de la aplicación sobre la base de datos del dispensario.
/// </summary>
public interface IApplicationDbContext
{
    // Cada spec de módulo añade aquí su DbSet<T>
    DbSet<User> Users { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
