using BuildingBlocks.Domain;

namespace Dispensario.Domain.Common;

// Nombre y apellido de una persona
public sealed record FullName : ValueObject
{
    public const int MaxLength = 100;

    public string FirstName { get; }
    public string LastName { get; }

    private FullName(string firstName, string lastName)
    {
        FirstName = firstName;
        LastName = lastName;
    }

    public static FullName Create(string firstName, string lastName)
    {
        var first = firstName?.Trim() ?? string.Empty;
        var last = lastName?.Trim() ?? string.Empty;

        if (first.Length == 0)
            throw new DomainException("First name is required.");

        if (first.Length > MaxLength)
            throw new DomainException($"First name must not exceed {MaxLength} characters.");

        if (last.Length == 0)
            throw new DomainException("Last name is required.");

        if (last.Length > MaxLength)
            throw new DomainException($"Last name must not exceed {MaxLength} characters.");

        return new FullName(first, last);
    }

    public override string ToString() => $"{FirstName} {LastName}";
}
