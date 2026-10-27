namespace Dispensario.Infrastructure.Data.Seed;

/// <summary>
/// Configuración de los datos de ejemplo que se siembran en Development (sección <c>Seed:SampleData</c>).
/// </summary>
// Sin ValidateOnStart, como AdminSeedOptions
public sealed class SampleDataSeedOptions
{
    public const string SectionName = "Seed:SampleData";

    // true por defecto: en Development siempre se siembra salvo que se apague (los tests de integración lo apagan)
    public bool Enabled { get; init; } = true;

    // En user-secrets; vacía → se siembran doctores y pacientes, pero no los usuarios
    public string Password { get; init; } = string.Empty;
}
