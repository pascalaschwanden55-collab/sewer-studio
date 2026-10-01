using System;
using System.Linq;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.UseCases.NaechsteAufgabe;

/// <summary>
/// Ampelfarbe der KI-Statusspalte (Inventar 4.3, Nova-Etappe 2b).
/// <see cref="Bestaetigt"/> kam mit der Fixwelle dazu (F2).
/// </summary>
public enum KiAmpel { KeineAnalyse, Offen, Bestaetigt, Geprueft, Kritisch }

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
/// Offene KI-Befunde bleiben offen, angenommene heissen bestaetigt. Weder der
/// Sanierungsstatus noch die persoenliche Erledigt-Markierung erteilen eine KI-Freigabe.
/// </summary>
public static class HaltungZeilenStatus
{
    public static HaltungZeilenStatusErgebnis Bestimme(HaltungRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        var pruefstand = HaltungPruefstatus.Bestimme(record);
        var kiEintraege = ZaehleKiEintraege(record);
        var offeneBefunde = ZaehleOffeneKiBefunde(record);

        var (ampel, ampelText) = kiEintraege == 0
            ? (KiAmpel.KeineAnalyse, "keine Analyse")
            : offeneBefunde > 0
                ? (KiAmpel.Offen, $"{offeneBefunde} offen")
                : (KiAmpel.Bestaetigt, "bestätigt");

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
    /// Wie viele nicht geloeschte Protokolleintraege stammen ueberhaupt von der KI? Genau das
    /// unterscheidet "keine Analyse" von "bestätigt" — ein Eintrag von Hand zaehlt nicht mit.
    /// </summary>
    private static int ZaehleKiEintraege(HaltungRecord record)
    {
        var entries = record.Protocol?.Current?.Entries;
        return entries is null ? 0 : entries.Count(e => !e.IsDeleted && e.Ai is not null);
    }

    /// <summary>
    /// Nova-Fixwelle 2b (F1): Die Antwort kommt aus der gemeinsamen Kandidatenregel
    /// <see cref="HaltungProtokollQuelle"/> — hinterlegter Pfad mit .pdf-Endung, kein
    /// Dateizugriff je Zeile.
    /// </summary>
    private static bool HatProtokoll(HaltungRecord record) => HaltungProtokollQuelle.Vorhanden(record);

    private static string ZustandsklasseChip(HaltungRecord record)
        => TryParseZustandsklasse(record, out var klasse) ? $"Z{klasse}" : "–";

    private static bool TryParseZustandsklasse(HaltungRecord record, out int klasse)
    {
        klasse = 0;
        var text = record.GetFieldValue(FieldKeys.ConditionClass)?.Trim();
        return !string.IsNullOrEmpty(text) && int.TryParse(text, out klasse) && klasse is >= 0 and <= 4;
    }
}
