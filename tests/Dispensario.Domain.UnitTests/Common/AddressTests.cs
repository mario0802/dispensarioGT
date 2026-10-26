using BuildingBlocks.Domain;
using Dispensario.Domain.Common;

namespace Dispensario.Domain.UnitTests.Common;

public sealed class AddressTests
{
    [Fact]
    public void Create_WithAllFields_TrimsValues()
    {
        var address = Address.Create(" Av. Sol 123 ", " Lima ", " Lima ", " Frente al parque ");

        address.Street.ShouldBe("Av. Sol 123");
        address.City.ShouldBe("Lima");
        address.State.ShouldBe("Lima");
        address.Reference.ShouldBe("Frente al parque");
    }

    [Fact]
    public void Create_WithBlankOptionalFields_SetsThemToNull()
    {
        var address = Address.Create("Av. Sol 123", "Lima", "  ", null);

        address.State.ShouldBeNull();
        address.Reference.ShouldBeNull();
    }

    [Theory]
    [InlineData("", "Lima")]
    [InlineData("   ", "Lima")]
    [InlineData(null, "Lima")]
    [InlineData("Av. Sol 123", "")]
    [InlineData("Av. Sol 123", "   ")]
    [InlineData("Av. Sol 123", null)]
    public void Create_WithMissingRequiredField_ThrowsDomainException(string? street, string? city)
    {
        var act = () => Address.Create(street!, city!);

        act.ShouldThrow<DomainException>();
    }

    [Fact]
    public void Create_WithMaxLengthFields_ReturnsAddress()
    {
        var address = Address.Create(
            new string('s', Address.StreetMaxLength),
            new string('c', Address.CityMaxLength),
            new string('e', Address.StateMaxLength),
            new string('r', Address.ReferenceMaxLength));

        address.Street.Length.ShouldBe(200);
        address.City.Length.ShouldBe(100);
        address.State!.Length.ShouldBe(100);
        address.Reference!.Length.ShouldBe(250);
    }

    [Theory]
    [InlineData(201, 1, 1, 1)]
    [InlineData(1, 101, 1, 1)]
    [InlineData(1, 1, 101, 1)]
    [InlineData(1, 1, 1, 251)]
    public void Create_WithFieldTooLong_ThrowsDomainException(int street, int city, int state, int reference)
    {
        var act = () => Address.Create(
            new string('s', street), new string('c', city), new string('e', state), new string('r', reference));

        act.ShouldThrow<DomainException>();
    }

    [Fact]
    public void Equals_SameValues_ReturnsTrue()
    {
        var left = Address.Create("Av. Sol 123", "Lima");
        var right = Address.Create("Av. Sol 123", "Lima", " ", "");

        var result = left == right;

        result.ShouldBeTrue();
    }
}
