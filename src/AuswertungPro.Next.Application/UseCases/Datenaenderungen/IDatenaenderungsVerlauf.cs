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
/// <param name="Angewendet">true, wenn der Schritt VOLLSTAENDIG zurueckgesetzt bzw. erneut gesetzt
/// wurde und als neuer Eintrag im Gegenstapel steht.</param>
/// <param name="Meldung">Klartext fuer die Statuszeile oder einen Hinweis.</param>
/// <param name="Datensaetze">Die betroffenen Haltungen/Schaechte (fuer die Aktualisierung der Anzeige).</param>
/// <param name="Teilweise">Schlusswelle (Item 4): true nur im Sonderfall "nicht vollständig" — das
/// Anwenden schlug mitten im Schritt fehl UND der anschliessende Rueckbau der bereits geschriebenen
/// Teile schlug ebenfalls fehl (<see cref="DatenaenderungsVerlauf.GrundFehler"/>, der ganze Verlauf
/// wird dabei geleert). <see cref="Angewendet"/> bleibt dann false (der Schritt ist NICHT als
/// Gegen-Eintrag wiederholbar), aber an den genannten <see cref="Datensaetze"/> koennen einzelne
/// Felder tatsaechlich einen neuen Wert tragen — Aufrufer muessen Projekt/Anzeige trotzdem
/// aktualisieren, statt den Fall wie einen reinen No-op ("Es wurde nichts geändert.") zu behandeln.</param>
public sealed record DatenaenderungsErgebnis(bool Angewendet, string Meldung, IReadOnlyList<object> Datensaetze, bool Teilweise = false);

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
///
/// REGEL fuer jeden neuen externen Schreibweg (schreibt Felder an der Eingabe vorbei, etwa eine
/// Uebernahme aus GeoShop, QGIS, WebGIS oder einer Objektakte): danach <see cref="Leere"/> mit
/// <see cref="DatenaenderungsVerlauf.GrundUebernahme"/> aufrufen. Sonst nimmt Strg+Z spaeter Werte
/// zurueck, die der Schritt gar nicht mehr besitzt. Heutige Stellen:
///  - Haltungen: <c>DataPageViewModel.MeldeFelderExternErgaenzt</c>, aufgerufen von
///    <c>DataPageViewModel.KatasterKennungen</c>, <c>.QgisNachfuellen</c> und <c>.WebGisHolen</c>.
///  - Schaechte: <c>SchaechtePageViewModel.MeldeUebernahme</c>, aufgerufen von <c>.KatasterKennungen</c>,
///    <c>.QgisNachfuellen</c> und <c>.WebGisHolen</c>.
///  - <c>WebGisHolenAblauf</c> (Holen ueber die Shell) und <c>ObjektaktenDialog</c> (nach erfolgreichem
///    Speichern der Objektakte).
///  - <c>ShellViewModel.DatenVerlauf</c>: Import, Uebertragung oder Projektwechsel
///    (<see cref="DatenaenderungsVerlauf.GrundVorgang"/>).
/// </summary>
public interface IDatenaenderungsVerlauf
{
    /// <summary>Stapel oder Beschreibungen haben sich geaendert (Menue, Befehle nachziehen).</summary>
    event EventHandler? Geaendert;

    /// <summary>Ein nicht leerer Verlauf wurde geleert; <see cref="DatenaenderungsVerlaufGeleertEventArgs.Grund"/> erklaert warum.</summary>
    event EventHandler<DatenaenderungsVerlaufGeleertEventArgs>? Geleert;

    /// <summary>Bindet den Verlauf an das offene Projekt (leert ihn bei einem Wechsel).</summary>
    void Binde(Project? projekt);

    /// <param name="nurFelder">Nur diese Felder gehoeren zum Schritt (Tabellenzelle, siehe
    /// <see cref="DatenaenderungsVerlauf.ZellSchrittFelder"/>); ohne Angabe alle Felder des Datensatzes.</param>
    IDisposable Erfasse(HaltungRecord datensatz, string? feld = null, IEnumerable<string>? nurFelder = null);
    IDisposable Erfasse(SchachtRecord datensatz, string? feld = null, IEnumerable<string>? nurFelder = null);
    IDisposable ErfasseMehrere(IEnumerable<HaltungRecord> datensaetze, string beschreibung);
    IDisposable ErfasseMehrere(IEnumerable<SchachtRecord> datensaetze, string beschreibung);

    /// <summary>Eine Eingabe in der Objektakte: Bestandsfelder UND die Aktenwerte des Verbunds.</summary>
    IDisposable ErfasseObjektakte(ObjektaktenBearbeitung bearbeitung, string? feld = null);

    /// <summary>Eine Eingabe ist noch offen (Zelle im Bearbeitungsmodus); bis sie schliesst, wird nichts zurueckgenommen.</summary>
    bool EingabeOffen { get; }

    bool KannRueckgaengig(DatenaenderungsBereich bereich);
    bool KannWiederholen(DatenaenderungsBereich bereich);
    string? RueckgaengigBeschreibung(DatenaenderungsBereich bereich);
    string? WiederholenBeschreibung(DatenaenderungsBereich bereich);
    DatenaenderungsErgebnis Rueckgaengig(DatenaenderungsBereich bereich);
    DatenaenderungsErgebnis Wiederholen(DatenaenderungsBereich bereich);

    /// <summary>Leert beide Verlaeufe; ein offener Erfassungsbereich wird verworfen.</summary>
    void Leere(string grund);
}
