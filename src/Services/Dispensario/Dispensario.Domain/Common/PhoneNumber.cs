using System.Text.RegularExpressions;
using BuildingBlocks.Domain;

namespace Dispensario.Domain.Common;

// Teléfono guardado tal como se escribió (dígitos, '+', espacios y guiones)
public sealed partial record PhoneNumber : ValueObject
{
    public const int MinLength = 7;
    public const int MaxLength = 20;

    public string Value { get; }

    private PhoneNumber(string value) => Value = value;

    public static PhoneNumber Create(string value)
    {
        var trimmed = value?.Trim() ?? string.Empty;

        if (trimmed.Length == 0)
            throw new DomainException("Phone number is required.");

        if (!FormatRegex().IsMatch(trimmed))
            throw new DomainException(
                $"Phone number must be {MinLength}-{MaxLength} characters long and contain only digits, '+', spaces and hyphens.");

        return new PhoneNumber(trimmed);
    }

    public override string ToString() => Value;

    [GeneratedRegex(@"^[0-9+\- ]{7,20}$")]
    private static partial Regex FormatRegex();
}
