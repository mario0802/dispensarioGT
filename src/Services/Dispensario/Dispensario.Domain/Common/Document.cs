using BuildingBlocks.Domain;

namespace Dispensario.Domain.Common;

// Documento de identidad: tipo + número; el número se guarda tal como se escribió (solo Trim)
public sealed record Document : ValueObject
{
    public const int NumberMaxLength = 20;

    public DocumentType Type { get; }
    public string Number { get; }

    private Document(DocumentType type, string number)
    {
        Type = type;
        Number = number;
    }

    public static Document Create(DocumentType type, string number)
    {
        if (!Enum.IsDefined(type))
            throw new DomainException($"Document type '{type}' is not valid.");

        var normalized = number?.Trim() ?? string.Empty;

        if (normalized.Length == 0)
            throw new DomainException("Document number is required.");

        if (normalized.Length > NumberMaxLength)
            throw new DomainException($"Document number must not exceed {NumberMaxLength} characters.");

        return new Document(type, normalized);
    }

    public override string ToString() => $"{Type} {Number}";
}
