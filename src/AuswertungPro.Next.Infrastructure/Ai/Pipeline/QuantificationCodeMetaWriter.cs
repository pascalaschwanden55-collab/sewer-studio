using System.Globalization;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Infrastructure.Ai.Pipeline;

/// <summary>
/// Schreibt SAM-Quantifizierung als CodeMeta-Parameter (vsa.*) in einen ProtocolEntry:
/// Messwerte + HERKUNFT (geschaetzt/automatisch/manuell, aus Etappe 1) + STATUS (Vorschlag = KI).
/// So ist eine geschaetzte mm-Angabe von einer gemessenen unterscheidbar (Gold-Fund-Wahrheit).
/// CodeMeta wird ueberall automatisch durchgereicht (Mapper-Clone/Merge/Export) - kein Nebenpfad.
/// </summary>
public static class QuantificationCodeMetaWriter
{
    public const string QuantStatusVorschlag = "Vorschlag";
    public const string QuantStatusBestaetigt = "bestaetigt";
    public const string QuantStatusKorrigiert = "korrigiert";

    public static void Apply(ProtocolEntry entry, string code, MaskQuantificationService.QuantifiedMask quant)
    {
        var hasAnyValue =
            !string.IsNullOrEmpty(quant.ClockPosition)
            || quant.HeightMm.HasValue
            || quant.WidthMm.HasValue
            || quant.ExtentPercent is > 0
            || quant.CrossSectionReductionPercent is > 0;

        if (!hasAnyValue)
            return;

        entry.CodeMeta ??= new ProtocolEntryCodeMeta { Code = code };
        var p = entry.CodeMeta.Parameters;

        if (!string.IsNullOrEmpty(quant.ClockPosition))
            p["vsa.uhr.von"] = quant.ClockPosition;
        if (quant.HeightMm.HasValue)
            p["vsa.hoehe.mm"] = quant.HeightMm.Value.ToString(CultureInfo.InvariantCulture);
        if (quant.WidthMm.HasValue)
            p["vsa.breite.mm"] = quant.WidthMm.Value.ToString(CultureInfo.InvariantCulture);
        if (quant.ExtentPercent is > 0)
            p["vsa.ausdehnung.prozent"] = quant.ExtentPercent.Value.ToString(CultureInfo.InvariantCulture);
        if (quant.CrossSectionReductionPercent is > 0)
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
