using Dispensario.Domain.Patients;

namespace Dispensario.Application.Patients;

/// <summary>
/// Traduce entre la notación clínica que usa la Api ("A+", "AB-"…) y el enum <see cref="BloodType"/> del dominio.
/// </summary>
public static class BloodTypeNotation
{
    public const string InvalidMessage = "Blood type must be one of A+, A-, B+, B-, AB+, AB-, O+, O-.";

    private static readonly Dictionary<string, BloodType> FromNotation = new(StringComparer.Ordinal)
    {
        ["A+"] = BloodType.APositive,
        ["A-"] = BloodType.ANegative,
        ["B+"] = BloodType.BPositive,
        ["B-"] = BloodType.BNegative,
        ["AB+"] = BloodType.ABPositive,
        ["AB-"] = BloodType.ABNegative,
        ["O+"] = BloodType.OPositive,
        ["O-"] = BloodType.ONegative,
    };

    private static readonly Dictionary<BloodType, string> ToNotationMap =
        FromNotation.ToDictionary(pair => pair.Value, pair => pair.Key);

    // Solo los ocho valores exactos: en mayúsculas y sin espacios
    public static bool TryParse(string? value, out BloodType bloodType)
    {
        bloodType = default;
        return value is not null && FromNotation.TryGetValue(value, out bloodType);
    }

    public static string ToNotation(BloodType bloodType) =>
        ToNotationMap.TryGetValue(bloodType, out var notation)
            ? notation
            : throw new ArgumentOutOfRangeException(nameof(bloodType), bloodType, "Unknown blood type.");
}
