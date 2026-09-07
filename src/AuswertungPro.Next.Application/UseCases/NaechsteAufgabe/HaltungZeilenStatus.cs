using System;
using System.Linq;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.UseCases.NaechsteAufgabe;

/// <summary>Ampelfarbe der KI-Statusspalte (Inventar 4.3, Nova-Etappe 2b).</summary>
public enum KiAmpel { KeineAnalyse, Offen, Geprueft, Kritisch }

/// <summary>Ergebnis der Zeilenstatus-Regel fuer eine Haltungszeile.</summary>
public sealed record HaltungZeilenStatusErgebnis(
    KiAmpel Ampel,
    string AmpelText,
    int OffeneBefunde,
    HaltungPruefstand Pruefstand,
    string PruefungText,
    bool HatVideo,
    bool HatProtokoll,
    string ZustandsklasseChip);

/// <summary>
/// Nova-Etappe 2b (Inventar 4.3, Zellregeln): WPF-freie Regel fuer die Statusspalten
/// KI-Ampel, Pruefung, Video und Protokoll der Haltungstabelle (Task 3 baut die Spalten
/// darauf auf). Reine Rechnung ohne Datenaenderung; nutzt <see cref="HaltungPruefstatus"/>
/// wieder, statt dessen Logik zu kopieren.
///
/// Ampelregel: ohne jede Analyse (<see cref="HaltungPruefstand.Offen"/>) gilt
/// <see cref="KiAmpel.KeineAnalyse"/>. Sonst zaehlt jeder noch nicht bestaetigte KI-Befund
/// (auch bei bereits "abgeschlossen" markierter Haltung): gibt es mindestens einen, gilt
/// <see cref="KiAmpel.Offen"/> mit der Anzahl im Text. Ohne offene KI-Befunde entscheidet die
/// Zustandsklasse: 0 oder 1 gilt als <see cref="KiAmpel.Kritisch"/>, alles andere (auch eine
/// fehlende Klasse) als <see cref="KiAmpel.Geprueft"/>.
/// </summary>
public static class HaltungZeilenStatus
{
    public static HaltungZeilenStatusErgebnis Bestimme(HaltungRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        var pruefstand = HaltungPruefstatus.Bestimme(record);
        var offeneBefunde = ZaehleOffeneKiBefunde(record);

        var (ampel, ampelText) = pruefstand == HaltungPruefstand.Offen
            ? (KiAmpel.KeineAnalyse, "keine Analyse")
            : offeneBefunde > 0
                ? (KiAmpel.Offen, $"{offeneBefunde} offen")
                : IstKritischeZustandsklasse(record)
                    ? (KiAmpel.Kritisch, "geprüft")
                    : (KiAmpel.Geprueft, "geprüft");

        return new HaltungZeilenStatusErgebnis(
            ampel,
            ampelText,
            offeneBefunde,
            pruefstand,
            HaltungPruefstatus.Text(pruefstand),
            HaltungPruefstatus.HatVideo(record),
            HatProtokoll(record),
            ZustandsklasseChip(record));
    }

    private static int ZaehleOffeneKiBefunde(HaltungRecord record)
    {
        var entries = record.Protocol?.Current?.Entries;
        return entries is null ? 0 : entries.Count(e => !e.IsDeleted && e.Ai is { Accepted: false });
    }

    /// <summary>
    /// Feldnamen wie im ExcelTemplateExportService-Linkvertrag (siehe
    /// <c>ExcelSchachtFeldzuordnung</c>): irgendeines der drei Protokollfelder genuegt.
    /// </summary>
    private static bool HatProtokoll(HaltungRecord record)
        => !string.IsNullOrWhiteSpace(record.GetFieldValue(FieldKeys.PdfPath))
           || !string.IsNullOrWhiteSpace(record.GetFieldValue(FieldKeys.PdfEigen))
           || !string.IsNullOrWhiteSpace(record.GetFieldValue(FieldKeys.PdfAll));

    private static bool IstKritischeZustandsklasse(HaltungRecord record)
        => TryParseZustandsklasse(record, out var klasse) && klasse <= 1;

    private static string ZustandsklasseChip(HaltungRecord record)
        => TryParseZustandsklasse(record, out var klasse) ? $"Z{klasse}" : "–";

    private static bool TryParseZustandsklasse(HaltungRecord record, out int klasse)
    {
        klasse = 0;
        var text = record.GetFieldValue(FieldKeys.ConditionClass)?.Trim();
        return !string.IsNullOrEmpty(text) && int.TryParse(text, out klasse) && klasse is >= 0 and <= 4;
    }
}
