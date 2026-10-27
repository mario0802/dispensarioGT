using Dispensario.Domain.Consultations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dispensario.Infrastructure.Data.Configurations.Consultations;

public sealed class ConsultationConfiguration : IEntityTypeConfiguration<Consultation>
{
    // Largo de "Closed" con margen, como en Appointments
    private const int EnumMaxLength = 20;

    public void Configure(EntityTypeBuilder<Consultation> builder)
    {
        builder.ToTable("Consultations");
        builder.HasKey(c => c.Id);

        builder.Ignore(c => c.DomainEvents);

        // Solo IDs de Patients, Doctors y Appointments: los módulos no se relacionan con FKs ni navegaciones
        builder.Property(c => c.PatientId).IsRequired();
        builder.Property(c => c.DoctorId).IsRequired();
        builder.Property(c => c.AppointmentId);

        builder.Property(c => c.Reason)
            .HasMaxLength(Consultation.ReasonMaxLength)
            .IsRequired();

        builder.Property(c => c.Diagnosis)
            .HasMaxLength(Consultation.DiagnosisMaxLength);

        builder.Property(c => c.Notes)
            .HasMaxLength(Consultation.NotesMaxLength);

        builder.Property(c => c.Status)
            .HasConversion<string>()
            .HasMaxLength(EnumMaxLength)
            .IsRequired();

        builder.Property(c => c.StartedAtUtc).IsRequired();
        builder.Property(c => c.ClosedAtUtc);

        // Una sola consulta por cita; SQLite admite varios NULL, así que las consultas sin cita no chocan
        builder.HasIndex(c => c.AppointmentId).IsUnique();

        // Historia del paciente ordenada por fecha
        builder.HasIndex(c => new { c.PatientId, c.StartedAtUtc });
    }
}
