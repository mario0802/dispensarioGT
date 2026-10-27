using BuildingBlocks.Domain;
using Dispensario.Domain.Appointments;

namespace Dispensario.Domain.Consultations.Events;

// Se emitirá al cerrar una consulta; AppointmentId es null en una consulta sin cita.
// Hoy no se emite ni tiene consumidores (fila 16 parcial)
public sealed record ConsultationClosedDomainEvent(ConsultationId ConsultationId, AppointmentId? AppointmentId) : IDomainEvent;
