using System.Collections.ObjectModel;

namespace AuswertungPro.Next.Domain.Models;

/// <summary>
/// Das Material eines Schachtbauwerks — mit den Begriffen der Norm, an einer Stelle.
///
/// Massgebend ist die Modelldatei SIA405_Abwasser_2020_2_d_LV95. <c>Normschacht.Material</c>
/// kennt dort nur vier Werte: andere, Beton, Kunststoff, unbekannt. Das ist deutlich
/// weniger als <c>Haltung.Material</c> mit 24 — deshalb ein eigenes Vokabular und
/// nicht das der Haltungen.
///
/// Wie bei <see cref="MaterialVokabular"/> bleibt der genaue Begriff im Programm
/// erhalten; nur die Datei vergroebert. "Fertigbetonelement" und "Ortsbeton" sind
/// fuer die Zustandsbeurteilung ein Unterschied, fuer die Norm an dieser Stelle nicht.
///
/// Reine Werte-Logik ohne Zustand und ohne Dateizugriff.
/// </summary>
public static class SchachtMaterialVokabular
{
    private sealed record Konzept(string[] Gelesen, string App, string? Norm);

    private static readonly Konzept[] Konzepte =
    [
        // --- die vier Normwerte selbst ---
        // "Beton" liest auch Beton_unbekannt und Beton Normalbeton: Der AWU-Export
        // schreibt am Normschacht Werte aus der Haltungsliste (28080 mal
        // Beton_unbekannt), und Zone 1.15 enthaelt "Beton Normalbeton".
        new(["beton", "beton_unbekannt", "beton_normalbeton", "normalbeton",
             "beton_spezialbeton", "beton_ortsbeton", "beton_pressrohrbeton"],
            "Beton", "Beton"),
        new(["kunststoff", "kunststoff_unbekannt"], "Kunststoff", "Kunststoff"),
        new(["andere"], "andere", "andere"),
        new(["unbekannt"], "unbekannt", "unbekannt"),

        // --- SchachtPro-Begriffe: genauer als die Norm, deshalb eigene Eintraege ---
        new(["fertigbetonelement", "fertigbeton"], "Fertigbetonelement", "Beton"),
        new(["ortsbeton"], "Ortsbeton", "Beton"),
        new(["polyethylen", "pe"], "Polyethylen", "Kunststoff"),
        new(["polypropylen", "pp"], "Polypropylen", "Kunststoff"),
        new(["gfk", "glasfaser", "glasfaserverstaerkter kunststoff"], "GFK", "Kunststoff"),
        // Gemauert kennt die Norm nicht als eigenen Wert - "andere" ist dort die
        // vorgesehene Aussage, nicht eine Notloesung.
        new(["gemauert", "mauerwerk"], "Gemauert", "andere")
    ];

    /// <summary>
    /// Die Auswahl im Programm: leer plus genau ein Begriff je Konzept. Jeder Eintrag
    /// liefert einen der vier Normwerte — es kann also kein ungueltiger Wert in eine
    /// XTF geraten.
    /// </summary>
    public static readonly IReadOnlyList<string> Auswahl = new ReadOnlyCollection<string>(
        new[] { "" }
            .Concat(Konzepte.Select(k => k.App))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(v => v, StringComparer.OrdinalIgnoreCase)
            .ToList());

    /// <summary>Bringt eine gelesene Schreibweise auf den Begriff des Programms.</summary>
    public static string Normalisieren(string? wert)
    {
        var text = (wert ?? "").Trim();
        return text.Length == 0 ? "" : Finde(text)?.App ?? text;
    }

    /// <summary>Die in SIA405 gueltige Schreibweise, oder <c>null</c>.</summary>
    public static string? NachNorm(string? wert) => Finde((wert ?? "").Trim())?.Norm;

    private static Konzept? Finde(string text)
    {
        if (text.Length == 0)
            return null;

        var klein = text.ToLowerInvariant();
        var mitUnterstrich = klein.Replace(' ', '_');

        return Konzepte.FirstOrDefault(k =>
            k.Gelesen.Contains(klein)
            || k.Gelesen.Contains(mitUnterstrich)
            || string.Equals(k.App, text, StringComparison.OrdinalIgnoreCase));
    }
}
