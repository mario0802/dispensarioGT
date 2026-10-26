using Dispensario.Domain.Common;
using Dispensario.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dispensario.Infrastructure.Data.Configurations.Users;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    private const int PasswordHashMaxLength = 100;
    private const int EnumMaxLength = 20;

    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(u => u.Id);

        builder.Ignore(u => u.DomainEvents);

        // VOs de un solo valor: una columna con conversión, así admiten índice y comparaciones en LINQ
        builder.Property(u => u.Email)
            .HasConversion(email => email.Value, value => Email.Create(value))
            .HasColumnName("Email")
            .HasMaxLength(Email.MaxLength)
            .IsRequired();
        builder.HasIndex(u => u.Email).IsUnique();

        builder.Property(u => u.Phone)
            .HasConversion(phone => phone!.Value, value => PhoneNumber.Create(value))
            .HasColumnName("Phone")
            .HasMaxLength(PhoneNumber.MaxLength);

        builder.Property(u => u.PasswordHash)
            .HasMaxLength(PasswordHashMaxLength)
            .IsRequired();

        // Las propiedades de solo lectura de los VOs no se mapean por convención: se declaran una a una
        builder.ComplexProperty(u => u.FullName, fullName =>
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

        // OwnsOne en la misma tabla: el índice único compuesto necesita propiedades indexables
        builder.OwnsOne(u => u.Identification, identification =>
        {
            identification.Property(d => d.Type)
                .HasColumnName("Identification_Type")
                .HasConversion<string>()
                .HasMaxLength(EnumMaxLength)
                .IsRequired();
            identification.Property(d => d.Number)
                .HasColumnName("Identification_Number")
                .HasMaxLength(Document.NumberMaxLength)
                .IsRequired();
            identification.HasIndex(d => new { d.Type, d.Number }).IsUnique();
        });
        builder.Navigation(u => u.Identification).IsRequired();

        builder.Property(u => u.Role)
            .HasConversion<string>()
            .HasMaxLength(EnumMaxLength)
            .IsRequired();

        // Sin FK: el aggregate Doctor todavía no existe (fila 09)
        builder.Property(u => u.DoctorId);

        builder.Property(u => u.IsActive).IsRequired();
    }
}
