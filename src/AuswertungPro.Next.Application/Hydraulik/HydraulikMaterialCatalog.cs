using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Hydraulik;

public sealed record MaterialOption(string Key, string Label, double KbNeu, double KbAlt)
{
    public override string ToString() => Label;
}

public static class HydraulikMaterialCatalog
{
    public static IReadOnlyList<MaterialOption> Materials { get; } =
    [
        new("Beton", "Beton", 0.0005, 0.0015),
        new("Steinzeug", "Steinzeug", 0.0003, 0.001),
        new("PVC/PE", "Kunststoff (PVC/PE)", 0.0002, 0.0005),
        new("GFK", "GFK", 0.0003, 0.0008),
        new("Guss", "Gusseisen", 0.001, 0.003),
    ];

    public static MaterialOption Resolve(string? recordMaterial, string? settingsMaterialKey)
        => ResolveMitHinweis(recordMaterial, settingsMaterialKey).Material;

    /// <summary>
    /// Wie <see cref="Resolve"/>, sagt aber, wenn das Rohrmaterial der Haltung keinem Rauheitswert zugeordnet
    /// werden konnte und deshalb die Einstellung gilt (Audit A13, 23.09.2026) — das muss im Bericht sichtbar sein.
    /// </summary>
    public static (MaterialOption Material, string? Hinweis) ResolveMitHinweis(string? recordMaterial, string? settingsMaterialKey)
    {
        var einstellung = Materials.FirstOrDefault(m =>
                string.Equals(m.Key, settingsMaterialKey, StringComparison.OrdinalIgnoreCase))
            ?? Materials[0];
        if (string.IsNullOrWhiteSpace(recordMaterial))
            return (einstellung, null);
        var zugeordnet = Zuordnen(recordMaterial);
        return zugeordnet is not null
            ? (zugeordnet, null)
            : (einstellung, $"Rohrmaterial «{recordMaterial.Trim()}» nicht zugeordnet — Einstellung verwendet");
    }

    /// <summary>
    /// Ordnet ein Rohrmaterial einem Rauheitswert zu; null, wenn es keinen gibt. Zuerst ueber das Vokabular des
    /// Programms (so speichert SewerStudio den Werkstoff: «Normalbeton», «Polyvinylchlorid» …), dann die
    /// WebGIS-Schreibweise «Gruppe, Detail (Kuerzel)», erst zuletzt die alte Teilwort-Regel — und die nur fuer
    /// Texte, die das Vokabular gar nicht kennt: sonst stand «Ton» bei «Beton» (Audit A13, 23.09.2026).
    /// </summary>
    public static MaterialOption? Zuordnen(string? rohrmaterial)
    {
        var text = (rohrmaterial ?? string.Empty).Trim();
        if (text.Length == 0) return null;

        foreach (var kandidat in Kandidaten(text))
        {
            if (!IstImVokabular(kandidat)) continue;
            var schluessel = Gruppe(kandidat);
            return schluessel is null ? null : Materials.First(m => m.Key == schluessel);
        }

        return Materials.FirstOrDefault(m =>
            m.Key.Equals(text, StringComparison.OrdinalIgnoreCase)
            || m.Label.Contains(text, StringComparison.OrdinalIgnoreCase));
    }

    public static MaterialOption? ResolveRecordMaterial(
        string? recordMaterial,
        MaterialOption? fallbackMaterial)
        => string.IsNullOrWhiteSpace(recordMaterial) ? fallbackMaterial : Zuordnen(recordMaterial) ?? fallbackMaterial;

    /// <summary>Der Text selbst, bei «Gruppe, Detail (Kuerzel)» zuerst das Detail ohne Kuerzel, dann die Gruppe.</summary>
    private static IEnumerable<string> Kandidaten(string text)
    {
        yield return text;
        var komma = text.IndexOf(',');
        if (komma <= 0) yield break;
        var detail = text[(komma + 1)..].Trim();
        var klammer = detail.IndexOf('(');
        if (klammer > 0) detail = detail[..klammer].Trim();
        if (detail.Length > 0) yield return detail;
        yield return text[..komma].Trim();
    }

    private static bool IstImVokabular(string text)
        => MaterialVokabular.Auswahl.Contains(MaterialVokabular.Normalisieren(text), StringComparer.Ordinal);

    /// <summary>Hydraulische Gruppe eines Werkstoffs aus dem Vokabular; null = kein Rauheitswert hinterlegt.</summary>
    private static string? Gruppe(string text)
    {
        var norm = MaterialVokabular.NachNorm(text);
        if (norm is null)
            return MaterialVokabular.Normalisieren(text) switch
            {
                "GFK" => "GFK",
                "Guss" => "Guss",
                _ => null,
            };
        if (norm.StartsWith("Beton_", StringComparison.Ordinal)) return "Beton";
        if (norm == "Steinzeug") return "Steinzeug";
        // GUP ist glasfaserverstaerkter Polyester, also ein GFK-Rohr; Epoxydharz ist ein Auskleidungsstoff.
        if (norm == "Kunststoff_Polyester_GUP") return "GFK";
        if (norm == "Kunststoff_Epoxydharz") return null;
        if (norm.StartsWith("Kunststoff_", StringComparison.Ordinal)) return "PVC/PE";
        if (norm.StartsWith("Guss_", StringComparison.Ordinal)) return "Guss";
        // Faserzement, Asbestzement, Ton, Zement, gebrannte Steine, Stahl, andere, unbekannt: kein Wert hinterlegt.
        return null;
    }
}
