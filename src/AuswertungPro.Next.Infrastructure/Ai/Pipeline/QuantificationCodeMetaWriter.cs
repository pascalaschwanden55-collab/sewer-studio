using System.Globalization;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Infrastructure.Ai.Pipeline;

/// <summary>
/// Schreibt SAM-Quantifizierung als CodeMeta-Parameter (vsa.*) in einen ProtocolEntry:
/// Messwerte + HERKUNFT (geschaetzt/automatisch/manuell, aus Etappe 1) + STATUS (Vorschlag = KI).
/// So ist eine geschaetzte mm-Angabe von einer gemessenen unterscheidbar (Gold-Fund-Wahrheit).
/// CodeMeta wird ueberall automatisch durchgereicht (Mapper-Clone/Merge/Export) - kein Nebenpfad.
///
/// CODEABHAENGIG (Teil 10): Welche Felder ueberhaupt geschrieben werden, entscheidet
/// <see cref="QuantificationGate"/> aus (a) der Manifest-Quant-Regel (OB Q1/Q2/Uhrlage erlaubt,
/// vom Aufrufer als <see cref="QuantificationGate.ManifestQuantRule"/> uebergeben) und (b) der
/// VSA-Einheiten-Tabelle. So bekommt z.B. eine Infiltration keine mm/%-Werte und ein Haarriss
/// keine Quantifizierung. Wird keine Regel uebergeben, gilt eine permissive Default-Regel
/// (Q1+Q2+Uhrlage) — abwaertskompatibel fuer Aufrufer ohne Katalog.
/// </summary>
public static class QuantificationCodeMetaWriter
{
    public const string QuantStatusVorschlag = "Vorschlag";
    public const string QuantStatusBestaetigt = "bestaetigt";
    public const string QuantStatusKorrigiert = "korrigiert";

    public static void Apply(
        ProtocolEntry entry,
        string code,
        MaskQuantificationService.QuantifiedMask quant,
        QuantificationGate.ManifestQuantRule? manifestRule = null)
    {
        // Permissive Default, wenn der Aufrufer keine Manifest-Regel kennt (Abwaertskompatibilitaet).
        var rule = manifestRule ?? new QuantificationGate.ManifestQuantRule(HasQ1: true, HasQ2: true, AllowClock: true);

        var available = new QuantificationGate.AvailableValues(
            HasHeightMm: quant.HeightMm.HasValue,
            HasWidthMm: quant.WidthMm.HasValue,
            HasExtentPercent: quant.ExtentPercent is > 0,
            HasCrossSectionPercent: quant.CrossSectionReductionPercent is > 0,
            HasClock: !string.IsNullOrEmpty(quant.ClockPosition));

        var decision = QuantificationGate.Decide(code, rule, available);
        if (!decision.WritesAnything)
            return;

        entry.CodeMeta ??= new ProtocolEntryCodeMeta { Code = code };
        var p = entry.CodeMeta.Parameters;

        if (decision.WriteClock && !string.IsNullOrEmpty(quant.ClockPosition))
            p["vsa.uhr.von"] = quant.ClockPosition;
        if (decision.WriteHeightMm && quant.HeightMm.HasValue)
            p["vsa.hoehe.mm"] = quant.HeightMm.Value.ToString(CultureInfo.InvariantCulture);
        if (decision.WriteWidthMm && quant.WidthMm.HasValue)
            p["vsa.breite.mm"] = quant.WidthMm.Value.ToString(CultureInfo.InvariantCulture);
        if (decision.WriteExtentPercent && quant.ExtentPercent is > 0)
            p["vsa.ausdehnung.prozent"] = quant.ExtentPercent.Value.ToString(CultureInfo.InvariantCulture);
        if (decision.WriteCrossSectionPercent && quant.CrossSectionReductionPercent is > 0)
            p["vsa.querschnitt.prozent"] = quant.CrossSectionReductionPercent.Value.ToString(CultureInfo.InvariantCulture);

        p["vsa.kalibrierung.quelle"] = HerkunftLabel(quant.CalibrationSource);
        p["vsa.quant.quelle"] = QuantStatusVorschlag;
    }

    /// <summary>Kalibrierungs-Herkunft als Klartext: geschaetzt / automatisch / manuell.</summary>
    public static string HerkunftLabel(CalibrationSource source) => source switch
    {
        CalibrationSource.Manual => "manuell",
        CalibrationSource.Auto => "automatisch",
        _ => "geschaetzt"
    };
}
