using BuildingBlocks.Domain;

namespace Dispensario.Domain.Doctors;

// ID tipado del doctor
public readonly record struct DoctorId(Guid Value) : IStronglyTypedId
{
    public static DoctorId New() => new(Guid.CreateVersion7());
}
