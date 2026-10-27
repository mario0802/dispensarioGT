using Dispensario.Domain.Common;
using Dispensario.Domain.Patients;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dispensario.Infrastructure.Data.Configurations.Patients;

public sealed class PatientConfiguration : IEntityTypeConfiguration<Patient>
{
    // Largo de "ABPositive" con margen, como en Users
    private const int EnumMaxLength = 20;

    public void Configure(EntityTypeBuilder<Patient> builder)
    {
        builder.ToTable("Patients");
        builder.HasKey(p => p.Id);

        builder.Ignore(p => p.DomainEvents);

        // OwnsOne en la misma tabla: el índice único compuesto necesita propiedades indexables
        builder.OwnsOne(p => p.Document, document =>
        {
            document.Property(d => d.Type)
                .HasColumnName("Document_Type")
                .HasConversion<string>()
                .HasMaxLength(EnumMaxLength)
                .IsRequired();
            document.Property(d => d.Number)
                .HasColumnName("Document_Number")
                .HasMaxLength(Document.NumberMaxLength)
                .IsRequired();
            document.HasIndex(d => new { d.Type, d.Number }).IsUnique();
        });
        builder.Navigation(p => p.Document).IsRequired();

        // Las propiedades de solo lectura de los VOs no se mapean por convención: se declaran una a una
        builder.ComplexProperty(p => p.FullName, fullName =>
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

        builder.Property(p => p.BirthDate).IsRequired();

        builder.Property(p => p.Gender)
            .HasConversion<string>()
            .HasMaxLength(EnumMaxLength)
            .IsRequired();

        // VOs de un solo valor: una columna con conversión. El email del paciente no es único
        builder.Property(p => p.Email)
            .HasConversion(email => email!.Value, value => Email.Create(value))
            .HasColumnName("Email")
            .HasMaxLength(Email.MaxLength);

        builder.Property(p => p.Phone)
            .HasConversion(phone => phone!.Value, value => PhoneNumber.Create(value))
            .HasColumnName("Phone")
            .HasMaxLength(PhoneNumber.MaxLength);

        // Dirección opcional: las columnas son nullable y Street (requerida dentro del VO) distingue el null
        builder.ComplexProperty(p => p.Address, address =>
        {
            address.Property(a => a.Street)
                .HasColumnName("Address_Street")
                .HasMaxLength(Address.StreetMaxLength)
                .IsRequired();
            address.Property(a => a.City)
                .HasColumnName("Address_City")
                .HasMaxLength(Address.CityMaxLength)
                .IsRequired();
            address.Property(a => a.State)
                .HasColumnName("Address_State")
                .HasMaxLength(Address.StateMaxLength);
            address.Property(a => a.Reference)
                .HasColumnName("Address_Reference")
                .HasMaxLength(Address.ReferenceMaxLength);
        });

        // Se guarda el nombre del enum ("APositive"); la notación "A+" es cosa de Application
        builder.Property(p => p.BloodType)
            .HasConversion<string>()
            .HasMaxLength(EnumMaxLength);

        builder.Property(p => p.Allergies)
            .HasMaxLength(Patient.AllergiesMaxLength);

        builder.Property(p => p.IsActive).IsRequired();
    }
}
