namespace BuildingBlocks.Domain;

// Datos de auditoría; los completa AuditableEntityInterceptor (Infrastructure) al guardar
public interface IAuditable
{
    DateTime CreatedAtUtc { get; set; }
    string? CreatedBy { get; set; }
    DateTime? LastModifiedAtUtc { get; set; }
    string? LastModifiedBy { get; set; }
}
