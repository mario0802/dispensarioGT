using Dispensario.Application.Patients;
using Dispensario.Domain.Patients;

namespace Dispensario.Application.UnitTests.Patients;

public sealed class BloodTypeNotationTests
{
    [Theory]
    [InlineData("A+", BloodType.APositive)]
    [InlineData("A-", BloodType.ANegative)]
    [InlineData("B+", BloodType.BPositive)]
    [InlineData("B-", BloodType.BNegative)]
    [InlineData("AB+", BloodType.ABPositive)]
    [InlineData("AB-", BloodType.ABNegative)]
    [InlineData("O+", BloodType.OPositive)]
    [InlineData("O-", BloodType.ONegative)]
    public void TryParseAndToNotation_WithClinicalNotation_RoundTrip(string notation, BloodType expected)
    {
        BloodTypeNotation.TryParse(notation, out var bloodType).ShouldBeTrue();

        bloodType.ShouldBe(expected);
        BloodTypeNotation.ToNotation(bloodType).ShouldBe(notation);
    }

    [Theory]
    [InlineData("a+")]
    [InlineData("A +")]
    [InlineData(" A+")]
    [InlineData("APositive")]
    [InlineData("X+")]
    [InlineData("")]
    [InlineData(null)]
    public void TryParse_WithInvalidValue_ReturnsFalse(string? value)
    {
        BloodTypeNotation.TryParse(value, out _).ShouldBeFalse();
    }
}
