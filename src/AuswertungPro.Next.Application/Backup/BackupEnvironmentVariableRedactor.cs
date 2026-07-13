namespace AuswertungPro.Next.Application.Backup;

/// <summary>Verhindert Klartext-Geheimnisse in wiederherstellbaren Sicherungstexten.</summary>
public static class BackupEnvironmentVariableRedactor
{
    public const string RedactedValue = "***redigiert***";

    private static readonly string[] SensitiveNameParts =
    [
        "TOKEN",
        "SECRET",
        "KEY",
        "AUTH"
    ];

    public static string RedactValue(string variableName, string? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(variableName);
        return SensitiveNameParts.Any(part =>
                variableName.Contains(part, StringComparison.OrdinalIgnoreCase))
            ? RedactedValue
            : value ?? string.Empty;
    }
}
