using BuildingBlocks.Domain;

namespace Dispensario.Domain.Patients;

// ID tipado del paciente
public readonly record struct PatientId(Guid Value) : IStronglyTypedId
{
    public static PatientId New() => new(Guid.CreateVersion7());
}
