namespace Dispensario.Application.Patients.Dtos;

public sealed record AddressDto(string Street, string City, string? State, string? Reference);
