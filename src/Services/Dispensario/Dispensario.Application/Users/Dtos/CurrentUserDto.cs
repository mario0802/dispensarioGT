using Dispensario.Domain.Common;
using Dispensario.Domain.Users;

namespace Dispensario.Application.Users.Dtos;

public sealed record CurrentUserDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    DocumentType IdentificationType,
    string IdentificationNumber,
    string? Phone,
    Role Role,
    Guid? DoctorId,
    bool IsActive);
