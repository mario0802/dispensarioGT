using BuildingBlocks.CQRS;
using FluentValidation;

namespace Dispensario.Application.Doctors.Commands.SetDoctorSchedule;

// Reemplaza todo el horario del doctor; una lista vacía lo borra
public sealed record SetDoctorScheduleCommand(Guid DoctorId, IReadOnlyList<ScheduleBlockInput> Blocks) : ICommand;

// Horas en formato "HH:mm", en hora local del dispensario
public sealed record ScheduleBlockInput(DayOfWeek DayOfWeek, string StartTime, string EndTime);

// Solo la forma: el múltiplo de 5, el orden, la duración mínima y los solapes son reglas del dominio
public sealed class SetDoctorScheduleCommandValidator : AbstractValidator<SetDoctorScheduleCommand>
{
    public const string TimePattern = "^([01][0-9]|2[0-3]):[0-5][0-9]$";

    public SetDoctorScheduleCommandValidator()
    {
        RuleFor(x => x.DoctorId).NotEmpty();
        RuleFor(x => x.Blocks).NotNull();
        RuleForEach(x => x.Blocks).NotNull().ChildRules(block =>
        {
            block.RuleFor(b => b.DayOfWeek).IsInEnum();
            block.RuleFor(b => b.StartTime).NotEmpty().Matches(TimePattern).WithMessage("Time must use the HH:mm format.");
            block.RuleFor(b => b.EndTime).NotEmpty().Matches(TimePattern).WithMessage("Time must use the HH:mm format.");
        });
    }
}
