namespace Dispensario.Application.UnitTests.Persistence.Fakes;

// Entidad propia de TestAggregate (OwnsMany), como lo será WeeklySchedule en Doctor
public sealed class TestChild
{
    // Constructor para EF
    private TestChild()
    {
        Description = string.Empty;
    }

    public TestChild(string description) => Description = description;

    public string Description { get; private set; }
}
