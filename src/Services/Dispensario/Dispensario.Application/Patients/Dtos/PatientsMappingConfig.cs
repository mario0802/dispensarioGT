using Dispensario.Domain.Patients;
using Mapster;

namespace Dispensario.Application.Patients.Dtos;

/// <summary>
/// Mapeos de Mapster del módulo Patients: los IDs pasan a Guid, los VOs a sus valores y el tipo de sangre a la notación clínica.
/// </summary>
public sealed class PatientsMappingConfig : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        // BirthDate, Gender, Allergies, IsActive y CreatedAtUtc se mapean por nombre.
        // ToNotation no se traduce a SQL: EF lo evalúa en cliente en la proyección final
        config.NewConfig<Patient, PatientDto>()
            .Map(dest => dest.Id, src => src.Id.Value)
            .Map(dest => dest.DocumentType, src => src.Document.Type)
            .Map(dest => dest.DocumentNumber, src => src.Document.Number)
            .Map(dest => dest.FirstName, src => src.FullName.FirstName)
            .Map(dest => dest.LastName, src => src.FullName.LastName)
            .Map(dest => dest.Email, src => src.Email == null ? null : src.Email.Value)
            .Map(dest => dest.Phone, src => src.Phone == null ? null : src.Phone.Value)
            .Map(dest => dest.Address, src => src.Address == null
                ? null
                : new AddressDto(src.Address.Street, src.Address.City, src.Address.State, src.Address.Reference))
            .Map(dest => dest.BloodType, src => src.BloodType == null ? null : BloodTypeNotation.ToNotation(src.BloodType.Value));
    }
}
