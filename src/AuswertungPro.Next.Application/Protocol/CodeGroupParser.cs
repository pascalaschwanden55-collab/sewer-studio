namespace AuswertungPro.Next.Application.Protocol;

/// <summary>
/// Zerlegt einen Gruppen-String der Form "Major/Base" in seine Bestandteile.
/// Reine Funktion ohne Seiteneffekte; aus ProtocolCodePickerViewModel extrahiert.
/// </summary>
public static class CodeGroupParser
{
    /// <summary>
    /// Gibt (Major, Base) zurueck.
    /// Enthaelt <paramref name="group"/> einen Schraegstrich, werden Major und Base getrennt;
    /// sonst gilt der gesamte Wert als beides.
    /// Null oder Leerstring wird als "Unbekannt" behandelt.
    /// </summary>
    public static (string Major, string Base) ParseGroup(string? group)
    {
        var g = (group ?? "Unbekannt").Trim();
        if (string.IsNullOrEmpty(g))
            g = "Unbekannt";

        if (g.Contains('/'))
        {
            var parts = g.Split('/', 2, StringSplitOptions.TrimEntries);
            return (parts[0], parts.Length > 1 ? parts[1] : parts[0]);
        }

        return (g, g);
    }

    /// <summary>
    /// Normiert einen Gruppen-String auf den Anzeigenamen: leer oder null ergibt "Unbekannt".
    /// </summary>
    public static string NormalizeGroup(string? group)
        => string.IsNullOrWhiteSpace(group) ? "Unbekannt" : group.Trim();
}
