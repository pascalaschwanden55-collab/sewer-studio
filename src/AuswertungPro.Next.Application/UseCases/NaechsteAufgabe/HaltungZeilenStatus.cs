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
/// Ampelregel (Nova-Fixwelle 2b, F2): Die KI-Spalte spricht ueber die KI, nicht ueber den
/// Arbeitsablauf. Massgeblich ist deshalb, ob im Protokoll ueberhaupt KI-Eintraege stehen.
/// Keine KI-Eintraege heisst <see cref="KiAmpel.KeineAnalyse"/> — auch an einer fachlich
/// abgeschlossenen Haltung, denn dort hat schlicht nie eine KI gerechnet. Gibt es KI-Eintraege
/// und mindestens einen noch nicht bestaetigten, gilt <see cref="KiAmpel.Offen"/> mit der
/// Anzahl im Text. Ist keiner mehr offen, aber die Haltung noch nicht abgeschlossen, gilt
/// <see cref="KiAmpel.Bestaetigt"/> ("bestätigt") — vorher stand dort faelschlich
/// "keine Analyse". Erst mit abgeschlossener Haltung heisst es "geprüft"; die Zustandsklasse
/// 0 oder 1 faerbt das als <see cref="KiAmpel.Kritisch"/> ein.
///
/// <see cref="HaltungPruefstatus"/> bleibt unveraendert: Aufgaben-Chip und Uebersicht lesen
/// weiter dieselbe fachliche Regel.
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
                : pruefstand != HaltungPruefstand.Abgeschlossen
                    ? (KiAmpel.Bestaetigt, "bestätigt")
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
