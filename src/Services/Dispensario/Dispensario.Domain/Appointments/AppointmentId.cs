using BuildingBlocks.Domain;

namespace Dispensario.Domain.Appointments;

// ID tipado de la cita
public readonly record struct AppointmentId(Guid Value) : IStronglyTypedId
{
    public static AppointmentId New() => new(Guid.CreateVersion7());
}
