using BuildingBlocks.Domain;
using Dispensario.Domain.Appointments;

namespace Dispensario.Domain.Consultations.Events;

// Se emite al cerrar una consulta; AppointmentId es null en una consulta sin cita
public sealed record ConsultationClosedDomainEvent(ConsultationId ConsultationId, AppointmentId? AppointmentId) : IDomainEvent;
