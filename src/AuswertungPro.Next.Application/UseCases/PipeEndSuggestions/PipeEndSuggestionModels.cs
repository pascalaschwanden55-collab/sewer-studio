using System;
using System.Collections.Generic;

namespace AuswertungPro.Next.Application.UseCases.PipeEndSuggestions;

/// <summary>Die zwei Klassen, fuer die es eine freigegebene Lernstufe gibt.</summary>
public enum PipeEndKind
{
    /// <summary>Rohranfang, VSA-Code BCD. Sitzt am Videoanfang.</summary>
    Rohranfang = 0,

    /// <summary>Rohrende, VSA-Code BCE. Sitzt am Videoende.</summary>
    Rohrende = 1
}

/// <summary>Feste Zuordnungen je Klasse — Klartext, Sidecar-Klassenname, VSA-Code.</summary>
public static class PipeEndKinds
{
    /// <summary>Klassenname im Sidecar (Freigabedatei und Modell heissen so).</summary>
    public static string Klasse(PipeEndKind kind) => kind switch
    {
        PipeEndKind.Rohranfang => "rohranfang",
        PipeEndKind.Rohrende => "rohrende",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };

    /// <summary>Anzeigename fuer den Menschen.</summary>
    public static string Label(PipeEndKind kind) => kind switch
    {
        PipeEndKind.Rohranfang => "Rohranfang",
        PipeEndKind.Rohrende => "Rohrende",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };

    /// <summary>Hauptcode nach VSA-KEK.</summary>
    public static string VsaCode(PipeEndKind kind) => kind switch
    {
        PipeEndKind.Rohranfang => "BCD",
        PipeEndKind.Rohrende => "BCE",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };
}

/// <summary>Konfidenz der Lernstufe fuer EIN Videobild — keine Box, nur "wie sicher zeigt das ganze Bild die Klasse".</summary>
public sealed record PipeEndFrameScore(double TimeSeconds, double Confidence);

/// <summary>
/// Genau eine vorgeschlagene Stelle je Klasse: die staerkste gruppierte Meldung
/// des Modells im ganzen Video.
/// </summary>
public sealed record PipeEndSuggestion(
    PipeEndKind Kind,
    double TimeStartSeconds,
    double TimeEndSeconds,
    double PeakTimeSeconds,
    double MaxConfidence,
    int FrameCount);

/// <summary>
/// Regeln der Zusammenfassung — identisch mit dem Abnahmeskript
/// (training/scripts/lernstufe_videolauf.py, zusammenfassen), mit dem die
/// Freigabe vom 2026-08-12 gemessen wurde. Die Werte hier sind deshalb keine
/// Stellschrauben: Wer sie aendert, misst die Freigabe neu.
/// </summary>
public sealed record PipeEndRuleOptions
{
    /// <summary>Schwelle fuer die fertige Stelle (Abnahme: --schwelle 0,50).</summary>
    public double Threshold { get; init; } = 0.50;

    /// <summary>
    /// Aufnahmegrenze fuer das einzelne Bild. Gesammelt wird ab hier, damit ein
    /// Konfidenzeinbruch eine Stelle nicht in zwei Vorschlaege zerlegt.
    /// </summary>
    public double FloorConfidence { get; init; } = 0.10;

    /// <summary>Eine Luecke ueber diesem Abstand trennt zwei Stellen (ZEIT_LUECKE_S).</summary>
    public double TimeGapSeconds { get; init; } = 3.0;

    /// <summary>
    /// Videoanfang ausblenden — der Schacht sieht wie ein Rohrende aus. Fuer die
    /// Klasse, die GENAU dort sitzt (Rohranfang), muss der Wert 0 sein, sonst
    /// fliegt der einzige echte Treffer raus.
    /// </summary>
    public double SkipFirstSeconds { get; init; } = 0.0;

    /// <summary>Die in der Abnahme verwendeten Vorgaben je Klasse.</summary>
    public static PipeEndRuleOptions ForKind(PipeEndKind kind) => new()
    {
        SkipFirstSeconds = kind == PipeEndKind.Rohrende ? 3.0 : 0.0
    };
}

/// <summary>
/// Eine freigegebene Lernstufe, wie sie der Sidecar unter /classify/lernstufen
/// fuehrt: Klasse plus Gewicht-Hash. Der Client nennt beides bei jeder Anfrage
/// und prueft beides an der Antwort; einen Modellpfad gibt es in diesem Vertrag
/// nicht.
/// </summary>
/// <param name="Precision">Anteil der bestaetigten Vorschlaege in der Abnahme.</param>
/// <param name="Recall">Anteil der Videos mit sichtbarem Befund, deren Vorschlag bestaetigt wurde.</param>
public sealed record PipeEndLernstufePin(
    PipeEndKind Kind,
    string Klasse,
    string WeightSha256,
    double Precision,
    double Recall);

/// <summary>
/// Die zwei gepinnten Lernstufen. Werte aus
/// C:\KI_BRAIN\training\lernstufen\freigaben\rohranfang_v1.json und
/// rohrende_v1.json (Freigabe 2026-08-12, Regel "staerkste Meldung je Video",
/// 60 bzw. 46 Videos, Clip-Urteil). Ein anderes Gewicht braucht eine neue
/// Freigabe UND neue Zahlen hier — die Messung gehoert zum Gewicht.
/// </summary>
public static class PipeEndLernstufePins
{
    public static PipeEndLernstufePin Rohranfang { get; } = new(
        PipeEndKind.Rohranfang,
        PipeEndKinds.Klasse(PipeEndKind.Rohranfang),
        "40b0315aabc43095c61b196e5bf6011fb2123b7f99a2ccc3ce4a75ca6b910d9b",
        Precision: 0.8545,
        Recall: 0.9783);

    public static PipeEndLernstufePin Rohrende { get; } = new(
        PipeEndKind.Rohrende,
        PipeEndKinds.Klasse(PipeEndKind.Rohrende),
        "fb70e77ce5e3676ac1376c17f1bdfdf208f15c8010f3fa720d395aab7a95a4f2",
        Precision: 0.8889,
        Recall: 0.8837);

    public static IReadOnlyList<PipeEndLernstufePin> All { get; } = [Rohranfang, Rohrende];
}
