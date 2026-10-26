using BuildingBlocks.Domain;
using Dispensario.Domain.Common;

namespace Dispensario.Domain.UnitTests.Common;

public sealed class PhoneNumberTests
{
    [Theory]
    [InlineData("+51 999-111-222")]
    [InlineData("999111222")]
    [InlineData("01 234 5678")]
    public void Create_WithValidValue_KeepsFormat(string value)
    {
        var phone = PhoneNumber.Create(value);

        phone.Value.ShouldBe(value);
        phone.ToString().ShouldBe(value);
    }

    [Fact]
    public void Create_WithSurroundingSpaces_TrimsValue()
    {
        var phone = PhoneNumber.Create("  999111222  ");

        phone.Value.ShouldBe("999111222");
    }

    [Theory]
    [InlineData(7)]
    [InlineData(20)]
    public void Create_WithLengthAtLimit_ReturnsPhoneNumber(int length)
    {
        var value = new string('9', length);

        var phone = PhoneNumber.Create(value);

        phone.Value.Length.ShouldBe(length);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("12345a7")]
    [InlineData("(01) 2345678")]
    [InlineData("123456")]
    [InlineData("123456789012345678901")]
    public void Create_WithInvalidValue_ThrowsDomainException(string? value)
    {
        var act = () => PhoneNumber.Create(value!);

        act.ShouldThrow<DomainException>();
    }
}
