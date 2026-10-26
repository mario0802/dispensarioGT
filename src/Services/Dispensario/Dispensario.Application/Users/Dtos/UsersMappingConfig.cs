using Dispensario.Domain.Users;
using Mapster;

namespace Dispensario.Application.Users.Dtos;

/// <summary>
/// Mapeos de Mapster del módulo Users: los IDs pasan a Guid y los VOs a sus valores.
/// </summary>
public sealed class UsersMappingConfig : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<User, CurrentUserDto>()
            .Map(dest => dest.Id, src => src.Id.Value)
            .Map(dest => dest.Email, src => src.Email.Value)
            .Map(dest => dest.FirstName, src => src.FullName.FirstName)
            .Map(dest => dest.LastName, src => src.FullName.LastName)
            .Map(dest => dest.IdentificationType, src => src.Identification.Type)
            .Map(dest => dest.IdentificationNumber, src => src.Identification.Number)
            .Map(dest => dest.Phone, src => src.Phone == null ? null : src.Phone.Value)
            .Map(dest => dest.DoctorId, src => src.DoctorId == null ? (Guid?)null : src.DoctorId.Value.Value);
    }
}
