using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Views.Pages.Haltungsansicht;

namespace AuswertungPro.Next.UI.DataPage;

/// <summary>
/// Nova, Aufklapp-Liste (Task 2): Der Ansichtswechsel der Haltungsseite. Er wendet die
/// WPF-freie <see cref="HaltungenAnsichtRegel"/> auf die benannten Elemente an — die Seite
/// selbst reicht nur ihre Elemente herein und waechst dadurch nicht weiter.
///
/// Drei Ansichten schliessen sich aus: Aufklapp-Liste (Standard), Tabelle und die alte
/// Haltungsansicht. Was an einer Ansicht haengt (Spaltenchips, Eingabefelder, Uebersicht,
/// Abdocken), entscheidet die Regel, nicht diese Klasse.
/// </summary>
public sealed class DataPageAnsichtUmschalter
{
    /// <summary>Hinweis am Menuepunkt "Abdocken", solange die Aufklapp-Liste sichtbar ist.</summary>
    public const string AbdockenNurTabelle = "Abdocken gilt für die Tabelle";

    /// <summary>Der bisherige Hinweis am Menuepunkt "Abdocken".</summary>
    public const string AbdockenTabelle = "Tabelle in separatem Fenster öffnen (Multi-Monitor)";

    /// <summary>Loeschen in der Tabelle: Dort kann der Benutzer mehrere Zeilen markieren.</summary>
    public const string LoeschenTabelle = "Markierte Zeilen löschen";

    /// <summary>Loeschen in der Liste: Dort gibt es genau eine gewaehlte Haltung, und kein Del.</summary>
    public const string LoeschenListe = "Haltung löschen";

    /// <summary>Kennzeichnet den Loeschpunkt im geteilten Zeilenmenue (statt eines Namens im Ressourcenteil).</summary>
    public const string LoeschenMarke = "loeschen";

    /// <summary>Die benannten Elemente der Haltungsseite aus DataPage.xaml.</summary>
    public sealed record Elemente(
        DataGrid Tabelle,
        FrameworkElement AlteAnsicht,
        HaltungAufklappListe Liste,
        FrameworkElement Spaltenchips,
        FrameworkElement AlteSucheLeiste,
        FrameworkElement NovaSucheLeiste,
        MenuItem AlteAnsichtSchalter,
        MenuItem ListeSchalter,
        MenuItem TabelleSchalter,
        MenuItem AbdockenSchalter,
        ContextMenu ZeilenMenue);

    private readonly Elemente _e;
    private readonly Func<AppSettings?> _settings;
    private readonly Action _speichern;
    private readonly Action<bool, bool> _setzeArbeitsflaeche;
    private readonly Action<string, string, string> _konfliktAnListe;
    private readonly Action<string, string, string> _konfliktAnSchublade;

    /// <param name="speichern">
    /// Speichert die Einstellungen. Bewusst ein eigener Rueckruf und kein direkter Aufruf von
    /// <c>AppSettings.Save</c>: So kann ein Test den Wechsel pruefen, ohne die echte
    /// settings.json des Benutzers zu schreiben.
    /// </param>
    public DataPageAnsichtUmschalter(
        Elemente elemente,
        Func<AppSettings?> settings,
        Action speichern,
        Action<bool, bool> setzeArbeitsflaeche,
        Action<string, string, string> konfliktAnListe,
        Action<string, string, string> konfliktAnSchublade)
    {
        _e = elemente ?? throw new ArgumentNullException(nameof(elemente));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _speichern = speichern ?? throw new ArgumentNullException(nameof(speichern));
        _setzeArbeitsflaeche = setzeArbeitsflaeche ?? throw new ArgumentNullException(nameof(setzeArbeitsflaeche));
        _konfliktAnListe = konfliktAnListe ?? throw new ArgumentNullException(nameof(konfliktAnListe));
        _konfliktAnSchublade = konfliktAnSchublade ?? throw new ArgumentNullException(nameof(konfliktAnSchublade));
    }

    /// <summary>
    /// W01: Die verworfene Eingabe gehoert in die Kopfzeile des Formulars, das sie gezeigt hat —
    /// in der Listenansicht die aufgeklappte Zeile, sonst die Eingabefelder-Schublade. Ohne diese
    /// Weiche meldete jeder Konflikt in die Schublade, die in der Liste gar nicht sichtbar ist.
    /// </summary>
    public void MeldeKonflikt(string feld, string aktuellerWert, string eingabe)
    {
        if (ListeZeigtFormular)
            _konfliktAnListe(feld, aktuellerWert, eingabe);
        else
            _konfliktAnSchublade(feld, aktuellerWert, eingabe);
    }

    /// <summary>
    /// Was die SICHTBARE Ansicht markiert hat. Die Mehrfachauswahl der Tabelle ueberlebt einen
    /// Ansichtswechsel; ohne diese Unterscheidung loeschte die Liste fuenf Haltungen, obwohl nur
    /// eine markiert aussieht.
    /// </summary>
    public IReadOnlyList<HaltungRecord> MarkierteZeilen(HaltungRecord? gewaehlt)
    {
        if (!ListeSichtbar)
            return _e.Tabelle.SelectedItems.OfType<HaltungRecord>().ToList();

        return gewaehlt is null ? [] : [gewaehlt];
    }

    // Die Wahl dieses Programmlaufs. Sie zaehlt nur, solange es noch keine Einstellungen gibt
    // (Seitenaufbau vor dem ViewModel); sobald welche da sind, gewinnen die gespeicherten.
    private string? _gewaehltOhneEinstellungen;

    /// <summary>Zeigt die Seite gerade die Aufklapp-Liste?</summary>
    public bool ListeSichtbar => _e.Liste.Visibility == Visibility.Visible;

    /// <summary>
    /// Steht das bearbeitbare Formular gerade in der Liste? Nur dann gehoert ein Konflikthinweis
    /// dorthin; sonst in die Eingabefelder-Schublade der Tabelle.
    /// </summary>
    public bool ListeZeigtFormular => ListeSichtbar && _e.Liste.Aufgeklappt is not null;

    /// <summary>Die gespeicherte Nova-Ansicht, normalisiert ("liste" oder "tabelle").</summary>
    public string Gewaehlt
        => HaltungenAnsichtRegel.Normalisiere(_settings()?.HaltungenAnsicht ?? _gewaehltOhneEinstellungen);

    /// <summary>
    /// Wendet die aktuelle Ansicht auf alle Elemente an. Mehrfaches Aufrufen ist unschaedlich;
    /// die Seite ruft es beim Aufbau, beim Projektwechsel und nach jedem Umschalten.
    /// </summary>
    public void WendeAn()
    {
        var novaAktiv = _e.AlteAnsichtSchalter.IsChecked != true;
        var sicht = HaltungenAnsichtRegel.Bestimme(novaAktiv, Gewaehlt);

        _e.Liste.Visibility = Sichtbarkeit(sicht.Liste);
        _e.Tabelle.Visibility = Sichtbarkeit(sicht.Tabelle);
        _e.AlteAnsicht.Visibility = Sichtbarkeit(sicht.AlteAnsicht);
        _e.Spaltenchips.Visibility = Sichtbarkeit(sicht.Spaltenchips);
        _setzeArbeitsflaeche(sicht.Uebersicht, sicht.Eingabefelder);

        // Die Suchpille gehoert zu beiden Nova-Ansichten; die alte Suche-Zeile zur alten Ansicht.
        _e.NovaSucheLeiste.Visibility = Sichtbarkeit(!sicht.AlteAnsicht);
        _e.AlteSucheLeiste.Visibility = Sichtbarkeit(sicht.AlteAnsicht);

        // Genau einer der beiden Nova-Punkte ist angehakt — auch in der alten Ansicht, damit
        // sichtbar bleibt, welche Ansicht beim Zurueckschalten wiederkommt.
        var istListe = HaltungenAnsichtRegel.IstListe(Gewaehlt);
        _e.ListeSchalter.IsChecked = istListe;
        _e.TabelleSchalter.IsChecked = !istListe;

        // Abgedockt wird die Tabelle (oder die alte Ansicht). Solange die Tabelle in einem
        // eigenen Fenster steht, ist der Umschalter der alten Ansicht gesperrt — daran haengt
        // hier auch das Abdocken selbst, damit kein zweites Fenster entsteht.
        var abdockbar = sicht.AbdockenMoeglich || sicht.AlteAnsicht;
        _e.AbdockenSchalter.IsEnabled = abdockbar && _e.AlteAnsichtSchalter.IsEnabled;
        _e.AbdockenSchalter.ToolTip = abdockbar ? AbdockenTabelle : AbdockenNurTabelle;
        // Abgedockt bleibt die Ansicht, wie sie ist: Ein Wechsel wuerde die Tabelle aus dem
        // fremden Fenster herausschalten. Der Schalter der alten Ansicht sperrt dort schon
        // heute; die beiden Nova-Punkte folgen ihm.
        _e.ListeSchalter.IsEnabled = _e.AlteAnsichtSchalter.IsEnabled;
        _e.TabelleSchalter.IsEnabled = _e.AlteAnsichtSchalter.IsEnabled;

        // Der Loeschpunkt im geteilten Zeilenmenue sagt, was er wirklich tut. In der Liste ist
        // genau eine Haltung gewaehlt, und die Del-Taste loescht dort nicht — dann darf auch
        // kein Tastenkuerzel danebenstehen.
        foreach (var punkt in _e.ZeilenMenue.Items.OfType<MenuItem>())
        {
            if (punkt.Tag as string != LoeschenMarke)
                continue;
            punkt.Header = sicht.Liste ? LoeschenListe : LoeschenTabelle;
            punkt.InputGestureText = sicht.Liste ? string.Empty : "Del";
        }

        if (sicht.Liste)
        {
            // Die Mehrfachauswahl der Tabelle ueberlebt den Wechsel sonst unsichtbar weiter —
            // und "Loeschen" nimmt sich dann fuenf Haltungen, obwohl eine markiert aussieht.
            _e.Tabelle.SelectedItems.Clear();
            if (_e.Liste.SelectedItem is { } gewaehlt)
                _e.Tabelle.SelectedItem = gewaehlt;
            _e.Liste.ScrolleZurAuswahl();
        }
    }

    /// <summary>
    /// Waehlt die Ansicht aus dem angeklickten Menuepunkt (<c>Tag</c> "liste" oder "tabelle"),
    /// speichert sie und wendet sie sofort an. Ein Klick verlaesst dabei die alte
    /// Haltungsansicht — die drei Punkte sind eine Gruppe.
    /// </summary>
    public void Waehle(object? absender)
    {
        if (absender is not FrameworkElement { Tag: string schluessel })
            return;

        Waehle(schluessel);
    }

    /// <summary>Waehlt "liste" oder "tabelle"; unbekannte Werte gelten als "liste".</summary>
    public void Waehle(string? ansicht)
    {
        var neu = HaltungenAnsichtRegel.Normalisiere(ansicht);
        _gewaehltOhneEinstellungen = neu;
        if (_settings() is { } settings && !string.Equals(settings.HaltungenAnsicht, neu, StringComparison.Ordinal))
        {
            settings.HaltungenAnsicht = neu;
            _speichern();
        }

        _e.AlteAnsichtSchalter.IsChecked = false;
        WendeAn();
    }

    /// <summary>
    /// Ausdruecklicher Sprung von aussen (Dossier, Karte, globale Suche): In der Liste wird die
    /// Haltung zusaetzlich aufgeklappt. Ein blosser Auswahlwechsel per Pfeiltaste tut das nicht.
    /// </summary>
    public void ZeigeHaltung(HaltungRecord? record)
    {
        if (record is null || _e.Liste.Visibility != Visibility.Visible)
            return;

        _e.Liste.KlappeAuf(record);
    }

    /// <summary>Die Auswahl hat gewechselt: In der Liste die gewaehlte Zeile in Sicht scrollen.</summary>
    public void FolgeAuswahl()
    {
        if (_e.Liste.Visibility == Visibility.Visible)
            _e.Liste.ScrolleZurAuswahl();
    }

    private static Visibility Sichtbarkeit(bool sichtbar)
        => sichtbar ? Visibility.Visible : Visibility.Collapsed;
}
