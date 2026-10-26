using System.Text.RegularExpressions;
using BuildingBlocks.Domain;

namespace Dispensario.Domain.Common;

// Dirección de correo normalizada a minúsculas (SQLite compara el texto distinguiendo mayúsculas)
public sealed partial record Email : ValueObject
{
    public const int MaxLength = 254;

    public string Value { get; }

    private Email(string value) => Value = value;

    public static Email Create(string value)
    {
        var normalized = value?.Trim().ToLowerInvariant() ?? string.Empty;

        if (normalized.Length == 0)
            throw new DomainException("Email is required.");

        if (normalized.Length > MaxLength)
            throw new DomainException($"Email must not exceed {MaxLength} characters.");

        if (!FormatRegex().IsMatch(normalized))
            throw new DomainException($"Email '{normalized}' has an invalid format.");

        return new Email(normalized);
    }

    public override string ToString() => Value;

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex FormatRegex();
}
