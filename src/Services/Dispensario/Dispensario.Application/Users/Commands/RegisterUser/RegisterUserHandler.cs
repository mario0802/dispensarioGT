using BuildingBlocks.CQRS;
using BuildingBlocks.Exceptions;
using Dispensario.Application.Abstractions;
using Dispensario.Domain.Common;
using Dispensario.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Dispensario.Application.Users.Commands.RegisterUser;

/// <summary>
/// Registra un usuario nuevo; el email y la identificación deben ser únicos.
/// </summary>
public sealed class RegisterUserHandler(IApplicationDbContext db, IPasswordHasher passwordHasher)
    : ICommandHandler<RegisterUserCommand, RegisterUserResult>
{
    public async Task<RegisterUserResult> Handle(RegisterUserCommand command, CancellationToken cancellationToken)
    {
        // Email.Create normaliza a minúsculas, así que la unicidad no distingue mayúsculas
        var email = Email.Create(command.Email);
        var identification = Document.Create(command.DocumentType, command.DocumentNumber);
        var fullName = FullName.Create(command.FirstName, command.LastName);
        var phone = command.Phone is null ? null : PhoneNumber.Create(command.Phone);

        // Reglas de unicidad: necesitan la BD, así que se validan aquí y no en el dominio
        if (await db.Users.AnyAsync(u => u.Email == email, cancellationToken))
            throw new ConflictException($"User with email {email} already exists.");

        // Un owned type no se compara entero en LINQ: se comparan sus columnas
        if (await db.Users.AnyAsync(
                u => u.Identification.Type == identification.Type && u.Identification.Number == identification.Number,
                cancellationToken))
            throw new ConflictException($"User with identification {identification} already exists.");

        var user = User.Create(email, passwordHasher.Hash(command.Password), fullName, identification, phone, command.Role);
        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);

        return new RegisterUserResult(user.Id.Value);
    }
}
