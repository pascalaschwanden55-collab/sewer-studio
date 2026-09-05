using System;
using System.Collections.Generic;
using AuswertungPro.Next.Application.UseCases.BendSuggestions;

namespace AuswertungPro.Next.Application.UseCases.CodingSuggestions;

/// <summary>Die drei Helfer, die im Codiermodus vorschlagen duerfen.</summary>
public enum CodingSuggestionKind
{
    Bogen = 0,
    Rohranfang = 1,
    Rohrende = 2
}

/// <summary>Zustand eines Teil-Durchlaufs. Ein Fehler ist nie "kein Vorschlag".</summary>
public enum CodingSuggestionPartStatus
{
    Bereit = 0,
    NichtVerfuegbar = 1,
    Fehler = 2
}

public sealed record CodingSuggestionPartState(CodingSuggestionPartStatus Status, string Grund)
{
    public static CodingSuggestionPartState Bereit { get; } = new(CodingSuggestionPartStatus.Bereit, string.Empty);

    public static CodingSuggestionPartState NichtVerfuegbar(string grund)
        => new(CodingSuggestionPartStatus.NichtVerfuegbar, grund);

    public static CodingSuggestionPartState Fehler(string grund)
        => new(CodingSuggestionPartStatus.Fehler, grund);
}

/// <summary>Ein Vorschlag an einer Videostelle.</summary>
/// <param name="Meter">Gelesener oder gefuellter Meterstand; null = nicht lesbar.</param>
/// <param name="MeterIsEstimated">True = aus Nachbarn gefuellt, nur grobe Lage.</param>
/// <param name="IsStrong">Bogen: ueber der starken Grenze des Arbeitspunkts. Anfang/Ende: immer true.</param>
/// <param name="AcceptancePrecision">Anfang/Ende: gepinnter Abnahmewert (Precision). Bogen: 0.</param>
public sealed record CodingSuggestion(
    CodingSuggestionKind Kind,
    double PeakTimeSeconds,
    double? Meter,
    bool MeterIsEstimated,
    double Confidence,
    bool IsStrong,
    double AcceptancePrecision);

/// <summary>Ergebnis des Vorabdurchlaufs fuer den Codiermodus.</summary>
public sealed record CodingSuggestionSet(
    IReadOnlyList<CodingSuggestion> Suggestions,
    IReadOnlyList<MeterTrackPoint> MeterTrack,
    CodingSuggestionPartState BogenTeil,
    CodingSuggestionPartState AnfangEndeTeil)
{
    public static CodingSuggestionSet Leer(string grund)
        => new(
            Array.Empty<CodingSuggestion>(),
            Array.Empty<MeterTrackPoint>(),
            CodingSuggestionPartState.NichtVerfuegbar(grund),
            CodingSuggestionPartState.NichtVerfuegbar(grund));
}

/// <summary>
/// Der einzige Bogen-Kandidat mit gemessenem Arbeitspunkt (workpoint.json). Im
/// Codiermodus gibt es keine Modellwahl; dieser Pin ist dieselbe Konstante wie
/// im Training Studio (BendSuggestionListViewModel). Ein anderes Gewicht braucht
/// eine neue Messung UND einen neuen Pin.
/// </summary>
public static class CodingBendCandidatePin
{
    public const string Id = "bcc_nc15_seed46_20260808";
    public const string WeightSha256 = "8ad82c1b0186ec02126a18f095d551d7a083faa90855350b22a6e893ac860114";
}
