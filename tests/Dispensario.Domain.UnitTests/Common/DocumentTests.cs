using BuildingBlocks.Domain;
using Dispensario.Domain.Common;

namespace Dispensario.Domain.UnitTests.Common;

public sealed class DocumentTests
{
    [Fact]
    public void Create_WithSpaces_TrimsNumber()
    {
        var document = Document.Create(DocumentType.NationalId, "  12345678 ");

        document.Type.ShouldBe(DocumentType.NationalId);
        document.Number.ShouldBe("12345678");
    }

    [Fact]
    public void Create_WithLowercaseNumber_KeepsNumberAsWritten()
    {
        var document = Document.Create(DocumentType.Passport, "ab-123");

        document.Number.ShouldBe("ab-123");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WithEmptyNumber_ThrowsDomainException(string? number)
    {
        var act = () => Document.Create(DocumentType.NationalId, number!);

        act.ShouldThrow<DomainException>().Message.ShouldBe("Document number is required.");
    }

    [Fact]
    public void Create_WithMaxLength_ReturnsDocument()
    {
        var document = Document.Create(DocumentType.NationalId, new string('9', Document.NumberMaxLength));

        document.Number.Length.ShouldBe(20);
    }

    [Fact]
    public void Create_WithNumberTooLong_ThrowsDomainException()
    {
        var act = () => Document.Create(DocumentType.NationalId, new string('9', Document.NumberMaxLength + 1));

        act.ShouldThrow<DomainException>().Message.ShouldBe("Document number must not exceed 20 characters.");
    }

    [Fact]
    public void Create_WithUndefinedType_ThrowsDomainException()
    {
        var act = () => Document.Create((DocumentType)99, "12345678");

        act.ShouldThrow<DomainException>().Message.ShouldBe("Document type '99' is not valid.");
    }

    [Fact]
    public void Equals_SameTypeAndNumber_ReturnsTrue()
    {
        var left = Document.Create(DocumentType.NationalId, "12345678");
        var right = Document.Create(DocumentType.NationalId, " 12345678 ");

        var result = left == right;

        result.ShouldBeTrue();
    }

    [Fact]
    public void Equals_SameNumberDifferentType_ReturnsFalse()
    {
        var left = Document.Create(DocumentType.NationalId, "12345678");
        var right = Document.Create(DocumentType.Passport, "12345678");

        var result = left == right;

        result.ShouldBeFalse();
    }
}
