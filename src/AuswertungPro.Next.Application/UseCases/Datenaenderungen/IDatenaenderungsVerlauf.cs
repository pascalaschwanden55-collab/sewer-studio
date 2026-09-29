using AuswertungPro.Next.Application.UseCases.Objektakten;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.UseCases.Datenaenderungen;

/// <summary>Getrennte Verlaeufe: Strg+Z auf der Haltungsseite nimmt nur Haltungsaenderungen zurueck.</summary>
public enum DatenaenderungsBereich
{
    Haltungen,
    Schaechte,
}

/// <summary>Ergebnis eines Rueckgaengig-/Wiederholen-Schritts.</summary>
/// <param name="Angewendet">true, wenn Werte zurueckgesetzt bzw. erneut gesetzt wurden.</param>
/// <param name="Meldung">Klartext fuer die Statuszeile oder einen Hinweis.</param>
/// <param name="Datensaetze">Die betroffenen Haltungen/Schaechte (fuer die Aktualisierung der Anzeige).</param>
public sealed record DatenaenderungsErgebnis(bool Angewendet, string Meldung, IReadOnlyList<object> Datensaetze);

/// <summary>Der Verlauf wurde geleert, obwohl er Eintraege hatte.</summary>
public sealed class DatenaenderungsVerlaufGeleertEventArgs(string grund) : EventArgs
{
    public string Grund { get; } = grund;
}

/// <summary>
/// Rueckgaengig/Wiederholen fuer Haltungs- und Schachtdaten (Optikanalyse 28.09.2026, Aufgabe 16).
///
/// Erfasst werden nur Benutzereingaben, und zwar als Bereich: <c>using (verlauf.Erfasse(...))</c>
/// merkt sich VOR der Eingabe Wert und Herkunftsdaten aller Felder der genannten Datensaetze und
/// vergleicht beim Schliessen. Alles, was sich in diesem Bereich geaendert hat, ist EIN Schritt
/// (abhaengige Felder, Kostenfelder, ganze Spalte). Ein innerer Bereich wird Teil des aeusseren.
///
/// Rueckgaengig setzt nur zurueck, wenn die Felder noch exakt den eigenen Eintrag tragen (Wert,
/// Herkunft, Handmarke); sonst wird der Eintrag verworfen und nichts geschrieben. Eine Aenderung
/// eines Umbenennungsfelds (Haltungsname, Schacht oben/unten, Schachtnummer) zieht Dateien mit und
/// ist nie rueckgaengig machbar: Sie leert den Verlauf. Ebenso leeren ihn Projektwechsel, neue,
/// geloeschte oder verschobene Datensaetze sowie Importe und Uebernahmen (<see cref="Leere"/>).
/// </summary>
public interface IDatenaenderungsVerlauf
{
    /// <summary>Stapel oder Beschreibungen haben sich geaendert (Menue, Befehle nachziehen).</summary>
    event EventHandler? Geaendert;

    /// <summary>Ein nicht leerer Verlauf wurde geleert; <see cref="DatenaenderungsVerlaufGeleertEventArgs.Grund"/> erklaert warum.</summary>
    event EventHandler<DatenaenderungsVerlaufGeleertEventArgs>? Geleert;

    /// <summary>Bindet den Verlauf an das offene Projekt (leert ihn bei einem Wechsel).</summary>
    void Binde(Project? projekt);

    IDisposable Erfasse(HaltungRecord datensatz, string? feld = null);
    IDisposable Erfasse(SchachtRecord datensatz, string? feld = null);
    IDisposable ErfasseMehrere(IEnumerable<HaltungRecord> datensaetze, string beschreibung);
    IDisposable ErfasseMehrere(IEnumerable<SchachtRecord> datensaetze, string beschreibung);

    /// <summary>Eine Eingabe in der Objektakte: Bestandsfelder UND die Aktenwerte des Verbunds.</summary>
    IDisposable ErfasseObjektakte(ObjektaktenBearbeitung bearbeitung, string? feld = null);

    bool KannRueckgaengig(DatenaenderungsBereich bereich);
    bool KannWiederholen(DatenaenderungsBereich bereich);
    string? RueckgaengigBeschreibung(DatenaenderungsBereich bereich);
    string? WiederholenBeschreibung(DatenaenderungsBereich bereich);
    DatenaenderungsErgebnis Rueckgaengig(DatenaenderungsBereich bereich);
    DatenaenderungsErgebnis Wiederholen(DatenaenderungsBereich bereich);

    /// <summary>Leert beide Verlaeufe; ein offener Erfassungsbereich wird verworfen.</summary>
    void Leere(string grund);
}
