using BuildingBlocks.Domain;

namespace BuildingBlocks.UnitTests.Domain;

public sealed class ValueObjectTests
{
    [Fact]
    public void Equals_SameTypeAndSameValues_ReturnsTrue()
    {
        var left = new Money(10, "PEN");
        var right = new Money(10, "PEN");

        var result = left.Equals(right);

        result.ShouldBeTrue();
        (left == right).ShouldBeTrue();
        left.GetHashCode().ShouldBe(right.GetHashCode());
    }

    [Fact]
    public void Equals_SameTypeAndDifferentValues_ReturnsFalse()
    {
        var left = new Money(10, "PEN");
        var right = new Money(20, "PEN");

        var result = left.Equals(right);

        result.ShouldBeFalse();
        (left != right).ShouldBeTrue();
    }

    [Fact]
    public void Equals_DifferentTypeAndSameValues_ReturnsFalse()
    {
        ValueObject left = new Money(10, "PEN");
        ValueObject right = new OtherMoney(10, "PEN");

        var result = left.Equals(right);

        result.ShouldBeFalse();
        (left == right).ShouldBeFalse();
    }

    private sealed record Money(decimal Amount, string Currency) : ValueObject;

    private sealed record OtherMoney(decimal Amount, string Currency) : ValueObject;
}
