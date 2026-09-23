using System.Text.RegularExpressions;

namespace AuswertungPro.Next.Domain.Models;

/// <summary>
/// Die WebGIS-Auswahllisten der Felder, die Schritt A auf WebGIS-Begriffe umstellt
/// (Plan docs/superpowers/plans/2026-09-23-webgis-begriffe-schritt-a.md). Quelle ist der
/// eingebettete Objektaktenkatalog, aus den WebGIS-Masken erhoben — keine zweite Liste.
/// Material und FunktionHierarchisch folgen in Schritt B und C.
/// </summary>
public static class WebGisBegriffe
{
    // Vor den Listen: statische Felder werden in Quelltext-Reihenfolge initialisiert.
    private static readonly Regex KuerzelAmEnde = new(@"\s*\([A-Za-z]{1,4}\)\s*$", RegexOptions.Compiled);

    public const string SchachtFunktion = "Funktion";

    public static IReadOnlyList<string> HaltungFelder { get; } =
    [
        FieldKeys.OperatingStatus, FieldKeys.PositionAccuracy, FieldKeys.HydraulicFunction, FieldKeys.ConnectionType,
        FieldKeys.BeddingEncasement, FieldKeys.RehabilitationNeed, FieldKeys.UsageType, FieldKeys.ProfileType,
    ];

    /// <summary>Schachtfelder ohne die Funktion (die nur beim Normschacht umgestellt wird).</summary>
    public static IReadOnlyList<string> SchachtFelder { get; } =
    [
        FieldKeys.OperatingStatus, FieldKeys.PositionAccuracy, FieldKeys.RehabilitationNeed, FieldKeys.UsageType,
    ];

    private static readonly Lazy<IReadOnlyDictionary<string, WebGisWerteliste>> Haltung = new(() =>
        new Dictionary<string, WebGisWerteliste>(StringComparer.Ordinal)
        {
            [FieldKeys.OperatingStatus] = Liste("haltung-C04", StatusAliase),
            [FieldKeys.PositionAccuracy] = Liste("haltung-C13"),
            [FieldKeys.HydraulicFunction] = Liste("haltung-C03"),
            [FieldKeys.ConnectionType] = Liste("haltung-C18"),
            [FieldKeys.BeddingEncasement] = Liste("haltung-C17"),
            [FieldKeys.RehabilitationNeed] = Liste("haltung-C28"),
            [FieldKeys.UsageType] = Liste("haltung-C01", NutzungsartAliase, NutzungsartVokabular.Normalisieren),
            [FieldKeys.ProfileType] = Liste("haltung-C07", ProfiltypAliase, ProfiltypVokabular.Normalisieren),
        });

    private static readonly Lazy<IReadOnlyDictionary<string, WebGisWerteliste>> Schacht = new(() =>
        new Dictionary<string, WebGisWerteliste>(StringComparer.Ordinal)
        {
            [FieldKeys.OperatingStatus] = Liste("schacht-C05", StatusAliase),
            [FieldKeys.PositionAccuracy] = Liste("schacht-C11"),
            [FieldKeys.RehabilitationNeed] = Liste("schacht-C23"),
            [FieldKeys.UsageType] = Liste("schacht-C02", NutzungsartAliase, NutzungsartVokabular.Normalisieren),
            [SchachtFunktion] = Liste("schacht-C00", FunktionAliase, SchachtFunktionVokabular.Normalisieren),
        });

    // Nur belegte Paare mit gleicher Bedeutung. Alles andere bleibt stehen und wird markiert.
    private static IReadOnlyDictionary<string, string> StatusAliase => new Dictionary<string, string>
        { ["tot"] = "Tot/Aufgehoben, verfüllt" };
    // Das Vokabular fuehrt Regenabwasser schon als alte Schreibweise von Niederschlagsabwasser.
    private static IReadOnlyDictionary<string, string> NutzungsartAliase => new Dictionary<string, string>
        { ["Niederschlagsabwasser"] = "Regenabwasser", ["Bachwasser"] = "Bachabwasser" };
    private static IReadOnlyDictionary<string, string> ProfiltypAliase => new Dictionary<string, string>
        { ["Anderes"] = "Andere (A)", ["Anderes (A)"] = "Andere (A)" };
    // Das Vokabular fuehrt pumpenschacht schon als Schreibweise von Pumpwerk.
    private static IReadOnlyDictionary<string, string> FunktionAliase => new Dictionary<string, string>
        { ["Pumpwerk"] = "Pumpenschacht" };

    private static WebGisWerteliste Liste(string katalogId,
        IReadOnlyDictionary<string, string>? aliase = null, Func<string?, string>? vorstufe = null)
    {
        var katalog = FieldCatalog.Objektfelder.Auswahl(katalogId)
            ?? throw new InvalidOperationException($"WebGIS-Liste {katalogId} fehlt im Objektaktenkatalog.");
        return new WebGisWerteliste(katalog.Eintraege.Where(e => !e.Eigen).Select(e => e.Label), aliase, vorstufe);
    }

    /// <summary>Die WebGIS-Liste des Felds, oder null, wenn das Feld (noch) nicht umgestellt ist.</summary>
    public static WebGisWerteliste? Fuer(bool schacht, string feld)
        => (schacht ? Schacht.Value : Haltung.Value).GetValueOrDefault(feld);

    /// <summary>WebGIS-Beschriftung zum Wert; fuer nicht umgestellte Felder der Wert unveraendert.</summary>
    public static string Normalisieren(bool schacht, string feld, string? wert)
        => Fuer(schacht, feld)?.Normalisieren(wert) ?? (wert ?? "");

    /// <summary>
    /// Vergleichsform: Kuerzel in Klammern am Ende weg, Unterstrich zu Leerzeichen, ae/oe/ue zu
    /// Umlaut, Leerraum zusammengezogen, klein. Dieselbe Regel wie beim Senden (WebGisHandwertKarte).
    /// </summary>
    public static string Falte(string? s)
    {
        var t = (s ?? string.Empty).Trim();
        t = KuerzelAmEnde.Replace(t, "");
        // Erst klein, dann ae/oe/ue: sonst bliebe «Ueberschiebmuffen» am Wortanfang stehen.
        t = t.ToLowerInvariant().Replace('_', ' ');
        t = t.Replace("ae", "ä").Replace("oe", "ö").Replace("ue", "ü");
        t = Regex.Replace(t, @"\s+", " ");
        return t.Trim();
    }
}
