using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using AuswertungPro.Next.Application.UseCases.Datenaenderungen;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.DataPage;

/// <summary>
/// Optik Aufgabe 16 / Wartbarkeit Z8: die gemeinsame Anbindung der Haltungs- und der Schachtseite an den
/// Rueckgaengig-Verlauf. Die Seiten behalten nur ihre Huellen (<c>...MitVerlauf</c>) um die alten Handler;
/// was wann erfasst wird, steht hier an einer Stelle. Tabellenzelle und Auswahlfeld erfassen nur das Feld
/// samt dem, was es ableitet (<see cref="DatenaenderungsVerlauf.ZellSchrittFelder"/>), Formular und
/// «Spalte leeren» den ganzen Datensatz bzw. alle Datensaetze.
/// </summary>
internal sealed class DatenVerlaufSeitenAnbindung(Func<IDatenaenderungsVerlauf?> verlauf)
{
    private readonly DatenVerlaufZellErfassung _zelle = new();

    public IDisposable? Erfasse(HaltungRecord? record, string? feld, bool nurZelle = false)
        => record is not null && verlauf() is { } v ? v.Erfasse(record, feld, Felder(feld, nurZelle)) : null;

    public IDisposable? Erfasse(SchachtRecord? record, string? feld, bool nurZelle = false)
        => record is not null && verlauf() is { } v ? v.Erfasse(record, feld, Felder(feld, nurZelle)) : null;

    public IDisposable? ErfasseSpalte(IEnumerable<HaltungRecord> records, string beschreibung)
        => verlauf()?.ErfasseMehrere(records, beschreibung);

    /// <summary>Die Schachtliste wird als Momentaufnahme uebergeben (wie bisher <c>Records.ToList()</c>).</summary>
    public IDisposable? ErfasseSpalte(IEnumerable<SchachtRecord> records, string beschreibung)
        => verlauf()?.ErfasseMehrere(records.ToList(), beschreibung);

    /// <summary>Feld einer Tabellenspalte (ihr <c>Tag</c>); ohne Spalte kein Feld.</summary>
    public static string? ZellFeld(DataGridColumn? spalte) => spalte?.GetValue(FrameworkElement.TagProperty) as string;

    /// <summary>Der Bereich umschliesst genau den alten Handler und wird danach (auch bei Fehler) geschlossen.</summary>
    public static void Umschliesse(IDisposable? erfassung, Action alterHandler)
    {
        using (erfassung)
            alterHandler();
    }

    /// <summary>Oeffnet den Zellbereich VOR dem alten Handler; ein noch offener wird dabei sofort geschlossen.</summary>
    public void OeffneZelle(IDisposable? erfassung, Action alterHandler)
    {
        _zelle.Beginne(erfassung);
        alterHandler();
    }

    /// <summary>Oeffnet den Zellbereich NACH dem alten Handler und nur, wenn dieser das Bearbeiten nicht abgebrochen hat.</summary>
    public void OeffneZelleNach(Action alterHandler, Func<bool> weiter, Func<IDisposable?> erfasse)
    {
        alterHandler();
        if (weiter())
            _zelle.Beginne(erfasse());
    }

    /// <summary>Alter Commit-Handler, danach Schliessen des Zellbereichs mit Eingabe-Prioritaet.</summary>
    public void CommitZelle(Action alterHandler, Dispatcher dispatcher)
    {
        alterHandler();
        _zelle.BeendeNachCommit(dispatcher);
    }

    private static IEnumerable<string>? Felder(string? feld, bool nurZelle)
        => nurZelle && feld is not null ? DatenaenderungsVerlauf.ZellSchrittFelder(feld) : null;
}
