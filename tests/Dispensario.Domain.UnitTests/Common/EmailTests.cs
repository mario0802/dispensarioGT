using BuildingBlocks.Domain;
using Dispensario.Domain.Common;

namespace Dispensario.Domain.UnitTests.Common;

public sealed class EmailTests
{
    [Fact]
    public void Create_WithMixedCaseAndSpaces_NormalizesValue()
    {
        var email = Email.Create(" Ana@Mail.COM ");

        email.Value.ShouldBe("ana@mail.com");
        email.ToString().ShouldBe("ana@mail.com");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("ana")]
    [InlineData("ana@mail")]
    [InlineData("@mail.com")]
    [InlineData("ana@@mail.com")]
    [InlineData("ana perez@mail.com")]
    public void Create_WithInvalidValue_ThrowsDomainException(string? value)
    {
        var act = () => Email.Create(value!);

        act.ShouldThrow<DomainException>();
    }

    [Fact]
    public void Create_WithMaxLength_ReturnsEmail()
    {
        var value = new string('a', Email.MaxLength - "@mail.com".Length) + "@mail.com";

        var email = Email.Create(value);

        email.Value.Length.ShouldBe(254);
    }

    [Fact]
    public void Create_WithValueTooLong_ThrowsDomainException()
    {
        var value = new string('a', Email.MaxLength + 1 - "@mail.com".Length) + "@mail.com";

        var act = () => Email.Create(value);

        act.ShouldThrow<DomainException>();
    }

    [Fact]
    public void Equals_DifferentCase_ReturnsTrue()
    {
        var left = Email.Create("ANA@MAIL.COM");
        var right = Email.Create("ana@mail.com");

        var result = left == right;

        result.ShouldBeTrue();
    }
}
