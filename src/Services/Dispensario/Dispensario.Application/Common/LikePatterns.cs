namespace Dispensario.Application.Common;

// Patrones para EF.Functions.Like: los comodines del usuario (%, _) se tratan como texto literal
public static class LikePatterns
{
    public const string Escape = "\\";

    // Coincidencia exacta (en SQLite, sin distinguir mayúsculas en ASCII)
    public static string Equal(string value) => EscapeWildcards(value);

    public static string Contains(string value) => $"%{EscapeWildcards(value)}%";

    private static string EscapeWildcards(string value) =>
        value.Replace(Escape, Escape + Escape).Replace("%", Escape + "%").Replace("_", Escape + "_");
}
