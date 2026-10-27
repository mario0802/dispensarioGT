using BuildingBlocks.CQRS;

namespace Dispensario.Application.Appointments.Commands.MarkAppointmentNoShow;

// Sin validador: el ID llega de la ruta con {id:guid}
public sealed record MarkAppointmentNoShowCommand(Guid AppointmentId) : ICommand;
