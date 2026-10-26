using Dispensario.Application.Abstractions;
using Dispensario.Domain.Common;
using Dispensario.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dispensario.Infrastructure.Data.Seed;

/// <summary>
/// Crea el usuario Admin de <see cref="AdminSeedOptions"/> si todavía no existe un usuario con ese email.
/// </summary>
public sealed class DatabaseSeeder(
    DispensarioDbContext dbContext,
    IPasswordHasher passwordHasher,
    IOptions<AdminSeedOptions> options,
    ILogger<DatabaseSeeder> logger)
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var admin = options.Value;

        if (string.IsNullOrWhiteSpace(admin.Password))
        {
            logger.LogWarning("Admin seed skipped: 'Seed:Admin:Password' is empty.");
            return;
        }

        // Los VOs validan la configuración: un email o un documento inválidos fallan al arrancar
        var email = Email.Create(admin.Email);

        if (await dbContext.Users.AnyAsync(u => u.Email == email, cancellationToken))
            return;

        var user = User.Create(
            email,
            passwordHasher.Hash(admin.Password),
            FullName.Create(admin.FirstName, admin.LastName),
            Document.Create(admin.DocumentType, admin.DocumentNumber),
            phone: null,
            Role.Admin);

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Admin user {Email} seeded.", email.Value);
    }
}
