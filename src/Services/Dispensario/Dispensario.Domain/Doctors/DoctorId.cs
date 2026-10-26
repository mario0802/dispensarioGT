using BuildingBlocks.Domain;

namespace Dispensario.Domain.Doctors;

// ID tipado del doctor; el aggregate Doctor llega con la fila 09
public readonly record struct DoctorId(Guid Value) : IStronglyTypedId
{
    public static DoctorId New() => new(Guid.CreateVersion7());
}
