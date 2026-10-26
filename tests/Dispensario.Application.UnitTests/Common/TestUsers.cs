using Dispensario.Domain.Common;
using Dispensario.Domain.Users;

namespace Dispensario.Application.UnitTests.Common;

// Usuarios de prueba válidos; el hash no es real salvo que se pase uno
public static class TestUsers
{
    public static User Create(
        Role role = Role.Admin,
        string email = "ana.perez@dispensario.local",
        string documentNumber = "1712345678",
        string passwordHash = "hash",
        string? phone = null) => User.Create(
        Email.Create(email),
        passwordHash,
        FullName.Create("Ana", "Pérez"),
        Document.Create(DocumentType.NationalId, documentNumber),
        phone is null ? null : PhoneNumber.Create(phone),
        role);
}
