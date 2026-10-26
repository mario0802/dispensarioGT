using BuildingBlocks.Domain;

namespace Dispensario.Domain.Common;

// Dirección postal; State y Reference son opcionales
public sealed record Address : ValueObject
{
    public const int StreetMaxLength = 200;
    public const int CityMaxLength = 100;
    public const int StateMaxLength = 100;
    public const int ReferenceMaxLength = 250;

    public string Street { get; }
    public string City { get; }
    public string? State { get; }
    public string? Reference { get; }

    private Address(string street, string city, string? state, string? reference)
    {
        Street = street;
        City = city;
        State = state;
        Reference = reference;
    }

    public static Address Create(string street, string city, string? state = null, string? reference = null)
    {
        var normalizedStreet = street?.Trim() ?? string.Empty;
        var normalizedCity = city?.Trim() ?? string.Empty;
        var normalizedState = NullIfBlank(state);
        var normalizedReference = NullIfBlank(reference);

        if (normalizedStreet.Length == 0)
            throw new DomainException("Street is required.");

        if (normalizedStreet.Length > StreetMaxLength)
            throw new DomainException($"Street must not exceed {StreetMaxLength} characters.");

        if (normalizedCity.Length == 0)
            throw new DomainException("City is required.");

        if (normalizedCity.Length > CityMaxLength)
            throw new DomainException($"City must not exceed {CityMaxLength} characters.");

        if (normalizedState?.Length > StateMaxLength)
            throw new DomainException($"State must not exceed {StateMaxLength} characters.");

        if (normalizedReference?.Length > ReferenceMaxLength)
            throw new DomainException($"Reference must not exceed {ReferenceMaxLength} characters.");

        return new Address(normalizedStreet, normalizedCity, normalizedState, normalizedReference);
    }

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
