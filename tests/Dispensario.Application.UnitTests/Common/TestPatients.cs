using Dispensario.Domain.Common;
using Dispensario.Domain.Patients;

namespace Dispensario.Application.UnitTests.Common;

// Pacientes de prueba válidos
public static class TestPatients
{
    public static readonly DateOnly Today = DateOnly.FromDateTime(Persistence.Fakes.DispensarioTestDb.StartTime.UtcDateTime);

    public static Patient Create(
        string documentNumber = "0102030405",
        DocumentType documentType = DocumentType.NationalId,
        string firstName = "Ana",
        string lastName = "Pérez",
        string? email = null,
        string? phone = null,
        Address? address = null,
        BloodType? bloodType = null,
        string? allergies = null) => Patient.Create(
        Document.Create(documentType, documentNumber),
        FullName.Create(firstName, lastName),
        new DateOnly(1990, 5, 14),
        Gender.Female,
        email is null ? null : Email.Create(email),
        phone is null ? null : PhoneNumber.Create(phone),
        address,
        bloodType,
        allergies,
        Today);
}
