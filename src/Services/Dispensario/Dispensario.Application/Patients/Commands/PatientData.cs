using Dispensario.Application.Patients.Commands.CreatePatient;
using Dispensario.Domain.Common;
using Dispensario.Domain.Patients;

namespace Dispensario.Application.Patients.Commands;

// VOs del paciente construidos desde los campos de CreatePatient y UpdatePatient (los VOs lanzan DomainException)
internal sealed record PatientData(
    Document Document,
    FullName FullName,
    Email? Email,
    PhoneNumber? Phone,
    Address? Address,
    BloodType? BloodType)
{
    public static PatientData From(
        DocumentType documentType,
        string documentNumber,
        string firstName,
        string lastName,
        string? email,
        string? phone,
        AddressInput? address,
        string? bloodType)
    {
        // El validador ya rechazó una notación desconocida; aquí solo se traduce
        BloodType? parsedBloodType = null;
        if (bloodType is not null)
        {
            if (!BloodTypeNotation.TryParse(bloodType, out var value))
                throw new ArgumentException(BloodTypeNotation.InvalidMessage, nameof(bloodType));

            parsedBloodType = value;
        }

        return new PatientData(
            Document.Create(documentType, documentNumber),
            FullName.Create(firstName, lastName),
            email is null ? null : Email.Create(email),
            phone is null ? null : PhoneNumber.Create(phone),
            address is null ? null : Address.Create(address.Street, address.City, address.State, address.Reference),
            parsedBloodType);
    }
}
