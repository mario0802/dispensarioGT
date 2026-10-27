using BuildingBlocks.Domain;

namespace Dispensario.Domain.Doctors;

// Bloque de atención de un día de la semana, en hora local del dispensario; nunca cruza la medianoche
public sealed record ScheduleBlock : ValueObject
{
    public const int TimeStepMinutes = 5;

    public DayOfWeek DayOfWeek { get; }
    public TimeOnly StartTime { get; }
    public TimeOnly EndTime { get; }

    private ScheduleBlock(DayOfWeek dayOfWeek, TimeOnly startTime, TimeOnly endTime)
    {
        DayOfWeek = dayOfWeek;
        StartTime = startTime;
        EndTime = endTime;
    }

    public int DurationMinutes => (int)(EndTime - StartTime).TotalMinutes;

    public static ScheduleBlock Create(DayOfWeek dayOfWeek, TimeOnly startTime, TimeOnly endTime)
    {
        if (!Enum.IsDefined(dayOfWeek))
            throw new DomainException("Day of week is invalid.");

        if (!IsOnTimeStep(startTime) || !IsOnTimeStep(endTime))
            throw new DomainException($"Schedule times must be whole minutes and a multiple of {TimeStepMinutes}.");

        if (startTime >= endTime)
            throw new DomainException("Schedule block start time must be before end time.");

        return new ScheduleBlock(dayOfWeek, startTime, endTime);
    }

    // Dos bloques contiguos (uno termina cuando empieza el otro) no se solapan
    public bool Overlaps(ScheduleBlock other) =>
        DayOfWeek == other.DayOfWeek && StartTime < other.EndTime && other.StartTime < EndTime;

    public bool Contains(TimeOnly start, TimeOnly end) => StartTime <= start && end <= EndTime;

    private static bool IsOnTimeStep(TimeOnly time) =>
        time.Ticks % TimeSpan.TicksPerMinute == 0 && time.Minute % TimeStepMinutes == 0;
}
