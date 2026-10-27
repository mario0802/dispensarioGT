namespace Dispensario.Application.Doctors.Dtos;

// Horario semanal de un doctor, de lunes a domingo y por hora de inicio
public sealed record DoctorScheduleDto(Guid DoctorId, IReadOnlyList<ScheduleBlockDto> Blocks);

// Horas "HH:mm" en hora local del dispensario
public sealed record ScheduleBlockDto(DayOfWeek DayOfWeek, string StartTime, string EndTime);
