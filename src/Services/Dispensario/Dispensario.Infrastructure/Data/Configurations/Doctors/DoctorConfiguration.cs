using Dispensario.Domain.Common;
using Dispensario.Domain.Doctors;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dispensario.Infrastructure.Data.Configurations.Doctors;

public sealed class DoctorConfiguration : IEntityTypeConfiguration<Doctor>
{
    public void Configure(EntityTypeBuilder<Doctor> builder)
    {
        builder.ToTable("Doctors");
        builder.HasKey(d => d.Id);

        builder.Ignore(d => d.DomainEvents);

        // Las propiedades de solo lectura de los VOs no se mapean por convención: se declaran una a una
        builder.ComplexProperty(d => d.FullName, fullName =>
        {
            fullName.Property(n => n.FirstName)
                .HasColumnName("FullName_FirstName")
                .HasMaxLength(FullName.MaxLength)
                .IsRequired();
            fullName.Property(n => n.LastName)
                .HasColumnName("FullName_LastName")
                .HasMaxLength(FullName.MaxLength)
                .IsRequired();
        });

        // El dominio la guarda en mayúsculas, así el índice único no distingue mayúsculas
        builder.Property(d => d.LicenseNumber)
            .HasMaxLength(Doctor.LicenseNumberMaxLength)
            .IsRequired();
        builder.HasIndex(d => d.LicenseNumber).IsUnique();

        builder.Property(d => d.Specialty)
            .HasMaxLength(Doctor.SpecialtyMaxLength)
            .IsRequired();

        // VOs de un solo valor: una columna con conversión. El email del doctor no es único
        builder.Property(d => d.Email)
            .HasConversion(email => email.Value, value => Email.Create(value))
            .HasColumnName("Email")
            .HasMaxLength(Email.MaxLength)
            .IsRequired();

        builder.Property(d => d.Phone)
            .HasConversion(phone => phone!.Value, value => PhoneNumber.Create(value))
            .HasColumnName("Phone")
            .HasMaxLength(PhoneNumber.MaxLength);

        builder.Property(d => d.AppointmentDurationMinutes).IsRequired();

        builder.Property(d => d.IsActive).IsRequired();
    }
}
