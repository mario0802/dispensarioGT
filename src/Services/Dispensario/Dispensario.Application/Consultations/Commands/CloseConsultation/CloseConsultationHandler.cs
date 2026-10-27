using BuildingBlocks.CQRS;
using BuildingBlocks.Exceptions;
using Dispensario.Application.Abstractions;
using Dispensario.Domain.Consultations;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Dispensario.Application.Consultations.Commands.CloseConsultation;

/// <summary>
/// El doctor dueño cierra una consulta abierta con su diagnóstico; una consulta cerrada es final.
/// </summary>
public sealed class CloseConsultationHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
    : ICommandHandler<CloseConsultationCommand>
{
    public async Task<Unit> Handle(CloseConsultationCommand command, CancellationToken cancellationToken)
    {
        // La política Clinical garantiza el rol Doctor; aquí solo falta el vínculo
        var doctorId = currentUser.DoctorId
            ?? throw new ForbiddenException("Your user is not linked to a doctor.");

        var consultationId = new ConsultationId(command.ConsultationId);
        var consultation = await db.Consultations.SingleOrDefaultAsync(c => c.Id == consultationId, cancellationToken)
            ?? throw new NotFoundException(nameof(Consultation), command.ConsultationId);

        // El 404 va antes: hace falta leer la consulta para saber de qué doctor es
        if (consultation.DoctorId != doctorId)
            throw new ForbiddenException("You can only manage your own consultations.");

        consultation.Close(command.Diagnosis, command.Notes, timeProvider.GetUtcNow().UtcDateTime);
        await db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
