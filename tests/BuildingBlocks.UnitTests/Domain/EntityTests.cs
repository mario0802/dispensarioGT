using BuildingBlocks.Domain;

namespace BuildingBlocks.UnitTests.Domain;

public sealed class EntityTests
{
    [Fact]
    public void Equals_SameTypeAndSameId_ReturnsTrue()
    {
        var id = Guid.CreateVersion7();
        var left = new TestEntity(id);
        var right = new TestEntity(id);

        var result = left.Equals(right);

        result.ShouldBeTrue();
        (left == right).ShouldBeTrue();
        (left != right).ShouldBeFalse();
    }

    [Fact]
    public void Equals_SameTypeAndDifferentId_ReturnsFalse()
    {
        var left = new TestEntity(Guid.CreateVersion7());
        var right = new TestEntity(Guid.CreateVersion7());

        var result = left.Equals(right);

        result.ShouldBeFalse();
        (left == right).ShouldBeFalse();
        (left != right).ShouldBeTrue();
    }

    [Fact]
    public void Equals_DifferentTypeAndSameId_ReturnsFalse()
    {
        var id = Guid.CreateVersion7();
        Entity<Guid> left = new TestEntity(id);
        Entity<Guid> right = new OtherTestEntity(id);

        var result = left.Equals(right);

        result.ShouldBeFalse();
        (left == right).ShouldBeFalse();
    }

    [Fact]
    public void Equals_WithNull_ReturnsFalse()
    {
        var entity = new TestEntity(Guid.CreateVersion7());

        var result = entity.Equals(null);

        result.ShouldBeFalse();
        (entity == null).ShouldBeFalse();
        (null == entity).ShouldBeFalse();
        (entity != null).ShouldBeTrue();
    }

    [Fact]
    public void EqualityOperator_BothNull_ReturnsTrue()
    {
        TestEntity? left = null;
        TestEntity? right = null;

        var result = left == right;

        result.ShouldBeTrue();
    }

    [Fact]
    public void GetHashCode_EqualEntities_ReturnsSameValue()
    {
        var id = Guid.CreateVersion7();
        var left = new TestEntity(id);
        var right = new TestEntity(id);

        var result = left.GetHashCode() == right.GetHashCode();

        result.ShouldBeTrue();
    }

    private sealed class TestEntity : Entity<Guid>
    {
        public TestEntity(Guid id) => Id = id;
    }

    private sealed class OtherTestEntity : Entity<Guid>
    {
        public OtherTestEntity(Guid id) => Id = id;
    }
}
