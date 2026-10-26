namespace BuildingBlocks.Domain;

// Marca los IDs tipados para que Infrastructure los convierta a Guid por convención
public interface IStronglyTypedId
{
    Guid Value { get; }
}
