namespace Dispensario.Application.Appointments.Dtos;

// Slots libres de un doctor en una fecha local del dispensario; DurationMinutes es la duración vigente del doctor
public sealed record AvailableSlotsDto(
    Guid DoctorId,
    DateOnly Date,
    int DurationMinutes,
    IReadOnlyList<AvailableSlotDto> Slots);

// Horas locales del dispensario (sin offset)
public sealed record AvailableSlotDto(DateTime StartLocal, DateTime EndLocal);
