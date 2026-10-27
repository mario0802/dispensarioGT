using BuildingBlocks.CQRS;

namespace Dispensario.Application.Appointments.Commands.ConfirmAppointment;

// Sin validador: el ID llega de la ruta con {id:guid}
public sealed record ConfirmAppointmentCommand(Guid AppointmentId) : ICommand;
