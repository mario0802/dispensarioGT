using BuildingBlocks.Domain;
using Dispensario.Domain.Common;
using Dispensario.Domain.Doctors;
using Dispensario.Domain.Users.Events;

namespace Dispensario.Domain.Users;

// Usuario de la aplicación; recibe el hash de la contraseña ya calculado (el dominio no hashea)
public sealed class User : AggregateRoot<UserId>
{
    // Constructor para EF: los VOs y el hash los materializa EF justo después
    private User()
    {
        Email = null!;
        PasswordHash = null!;
        FullName = null!;
        Identification = null!;
    }

    private User(
        UserId id,
        Email email,
        string passwordHash,
        FullName fullName,
        Document identification,
        PhoneNumber? phone,
        Role role)
    {
        Id = id;
        Email = email;
        PasswordHash = passwordHash;
        FullName = fullName;
        Identification = identification;
        Phone = phone;
        Role = role;
        IsActive = true;
    }

    public Email Email { get; private set; }
    public string PasswordHash { get; private set; }
    public FullName FullName { get; private set; }
    public Document Identification { get; private set; }
    public PhoneNumber? Phone { get; private set; }
    public Role Role { get; private set; }
    public DoctorId? DoctorId { get; private set; }
    public bool IsActive { get; private set; }

    public static User Create(
        Email email,
        string passwordHash,
        FullName fullName,
        Document identification,
        PhoneNumber? phone,
        Role role)
    {
        EnsurePasswordHash(passwordHash);

        if (!Enum.IsDefined(role))
            throw new DomainException($"Role '{role}' is not valid.");

        var user = new User(UserId.New(), email, passwordHash, fullName, identification, phone, role);
        user.Raise(new UserCreatedDomainEvent(user.Id, email, role));
        return user;
    }

    public void ChangePassword(string newPasswordHash)
    {
        EnsurePasswordHash(newPasswordHash);
        PasswordHash = newPasswordHash;
    }

    // Un teléfono null borra el que hubiera
    public void UpdateProfile(FullName fullName, PhoneNumber? phone)
    {
        FullName = fullName;
        Phone = phone;
    }

    public void Deactivate()
    {
        if (!IsActive)
            throw new DomainException("User is already inactive.");

        IsActive = false;
    }

    public void Activate()
    {
        if (IsActive)
            throw new DomainException("User is already active.");

        IsActive = true;
    }

    public void LinkDoctor(DoctorId doctorId)
    {
        if (Role != Role.Doctor)
            throw new DomainException("Only users with the Doctor role can be linked to a doctor.");

        if (DoctorId is not null)
            throw new DomainException("User is already linked to a doctor.");

        DoctorId = doctorId;
    }

    private static void EnsurePasswordHash(string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new DomainException("Password hash is required.");
    }
}
