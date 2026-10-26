using BuildingBlocks.Domain;
using Dispensario.Domain.Common;

namespace Dispensario.Domain.UnitTests.Common;

public sealed class FullNameTests
{
    [Fact]
    public void Create_WithSurroundingSpaces_TrimsValues()
    {
        var fullName = FullName.Create(" Ana ", " Pérez ");

        fullName.FirstName.ShouldBe("Ana");
        fullName.LastName.ShouldBe("Pérez");
    }

    [Fact]
    public void ToString_Always_ReturnsFirstAndLastName()
    {
        var fullName = FullName.Create(" Ana ", " Pérez ");

        var result = fullName.ToString();

        result.ShouldBe("Ana Pérez");
    }

    [Theory]
    [InlineData("", "Pérez")]
    [InlineData("   ", "Pérez")]
    [InlineData(null, "Pérez")]
    [InlineData("Ana", "")]
    [InlineData("Ana", "   ")]
    [InlineData("Ana", null)]
    public void Create_WithEmptyPart_ThrowsDomainException(string? firstName, string? lastName)
    {
        var act = () => FullName.Create(firstName!, lastName!);

        act.ShouldThrow<DomainException>();
    }

    [Fact]
    public void Create_WithMaxLengthParts_ReturnsFullName()
    {
        var name = new string('a', FullName.MaxLength);

        var fullName = FullName.Create(name, name);

        fullName.FirstName.Length.ShouldBe(100);
        fullName.LastName.Length.ShouldBe(100);
    }

    [Theory]
    [InlineData(101, 1)]
    [InlineData(1, 101)]
    public void Create_WithPartTooLong_ThrowsDomainException(int firstNameLength, int lastNameLength)
    {
        var act = () => FullName.Create(new string('a', firstNameLength), new string('b', lastNameLength));

        act.ShouldThrow<DomainException>();
    }

    [Fact]
    public void Equals_SameValues_ReturnsTrue()
    {
        var left = FullName.Create("Ana", "Pérez");
        var right = FullName.Create(" Ana", "Pérez ");

        var result = left == right;

        result.ShouldBeTrue();
    }
}
