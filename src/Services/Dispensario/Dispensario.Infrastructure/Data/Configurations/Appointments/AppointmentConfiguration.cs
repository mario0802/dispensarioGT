using Dispensario.Domain.Appointments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dispensario.Infrastructure.Data.Configurations.Appointments;

public sealed class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    // Largo de "Scheduled" con margen, como en Users
    private const int EnumMaxLength = 20;

    public void Configure(EntityTypeBuilder<Appointment> builder)
    {
        builder.ToTable("Appointments");
        builder.HasKey(a => a.Id);

        builder.Ignore(a => a.DomainEvents);

        // Solo IDs de Patients y Doctors: los módulos no se relacionan con FKs ni navegaciones
        builder.Property(a => a.PatientId).IsRequired();
        builder.Property(a => a.DoctorId).IsRequired();

        builder.Property(a => a.StartUtc).IsRequired();
        builder.Property(a => a.EndUtc).IsRequired();

        builder.Property(a => a.Reason)
            .HasMaxLength(Appointment.ReasonMaxLength)
            .IsRequired();

        builder.Property(a => a.Status)
            .HasConversion<string>()
            .HasMaxLength(EnumMaxLength)
            .IsRequired();

        // Solo tiene valor en las citas canceladas
        builder.Property(a => a.CancellationReason)
            .HasMaxLength(Appointment.CancellationReasonMaxLength);

        // Agenda del doctor y citas del paciente: los cruces se buscan por estos dos índices
        builder.HasIndex(a => new { a.DoctorId, a.StartUtc });
        builder.HasIndex(a => new { a.PatientId, a.StartUtc });
    }
}
