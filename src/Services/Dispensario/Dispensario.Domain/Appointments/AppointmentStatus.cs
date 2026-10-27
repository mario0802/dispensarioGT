namespace Dispensario.Domain.Appointments;

// Estados de la cita; las transiciones entre ellos llegan con el ciclo de vida (fila 13)
public enum AppointmentStatus
{
    Scheduled,
    Confirmed,
    Completed,
    Cancelled,
    NoShow
}
