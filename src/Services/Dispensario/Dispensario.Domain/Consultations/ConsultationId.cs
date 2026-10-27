using BuildingBlocks.Domain;

namespace Dispensario.Domain.Consultations;

// ID tipado de la consulta
public readonly record struct ConsultationId(Guid Value) : IStronglyTypedId
{
    public static ConsultationId New() => new(Guid.CreateVersion7());
}
