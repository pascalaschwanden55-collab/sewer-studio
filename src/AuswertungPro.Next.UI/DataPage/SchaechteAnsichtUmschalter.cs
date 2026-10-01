using System;
using System.Windows;
using System.Windows.Controls;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Views.Pages.Schachtansicht;

namespace AuswertungPro.Next.UI.DataPage;

/// <summary>
/// Nova, Aufklapp-Liste (Task 6): Der Ansichtswechsel der Schachtseite. Kopie von
/// <see cref="DataPageAnsichtUmschalter"/> ohne Abdocken (das gibt es an der Schachtseite
/// nicht) und ohne getrennte alte/neue Suchleiste (die Schachtseite hat nur eine Suchpille,
/// die in allen drei Ansichten sichtbar bleibt).
///
/// Drei Ansichten schliessen sich aus: Aufklapp-Liste (Standard), Tabelle und die alte
/// Schachtansicht. Die WPF-freie <see cref="HaltungenAnsichtRegel"/> entscheidet — dieselbe
/// Regel wie bei den Haltungen, hier nur mit <c>AppSettings.SchaechteAnsicht</c> gefuettert.
/// Was an einer Ansicht haengt (Uebersicht, Eingabefelder), entscheidet die Regel, nicht diese
/// Klasse.
/// </summary>
public sealed class SchaechteAnsichtUmschalter
{
    /// <summary>Die benannten Elemente der Schachtseite aus SchaechtePage.xaml.</summary>
    public sealed record Elemente(
        DataGrid Tabelle,
        FrameworkElement AlteAnsicht,
        SchachtAufklappListe Liste,
        FrameworkElement Spaltenchips,
        MenuItem AlteAnsichtSchalter,
        MenuItem ListeSchalter,
        MenuItem TabelleSchalter);

    private readonly Elemente _e;
    private readonly Func<AppSettings?> _settings;
    private readonly Action _speichern;
    private readonly Action<bool, bool> _setzeArbeitsflaeche;

    // Die Wahl dieses Programmlaufs. Sie zaehlt nur, solange es noch keine Einstellungen gibt
    // (Seitenaufbau vor dem ViewModel); sobald welche da sind, gewinnen die gespeicherten.
    private string? _gewaehltOhneEinstellungen;

    /// <param name="speichern">
    /// Speichert die Einstellungen. Bewusst ein eigener Rueckruf und kein direkter Aufruf von
    /// <c>AppSettings.Save</c>: So kann ein Test den Wechsel pruefen, ohne die echte
    /// settings.json des Benutzers zu schreiben.
    /// </param>
    public SchaechteAnsichtUmschalter(
        Elemente elemente,
        Func<AppSettings?> settings,
        Action speichern,
        Action<bool, bool> setzeArbeitsflaeche)
    {
        _e = elemente ?? throw new ArgumentNullException(nameof(elemente));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _speichern = speichern ?? throw new ArgumentNullException(nameof(speichern));
        _setzeArbeitsflaeche = setzeArbeitsflaeche ?? throw new ArgumentNullException(nameof(setzeArbeitsflaeche));
    }

    /// <summary>Zeigt die Seite gerade die Aufklapp-Liste?</summary>
    public bool ListeSichtbar => _e.Liste.Visibility == Visibility.Visible;

    /// <summary>Die gespeicherte Nova-Ansicht, normalisiert ("liste" oder "tabelle").</summary>
    public string Gewaehlt
        => HaltungenAnsichtRegel.Normalisiere(_settings()?.SchaechteAnsicht ?? _gewaehltOhneEinstellungen);

    /// <summary>
    /// Wendet die aktuelle Ansicht auf alle Elemente an. Mehrfaches Aufrufen ist unschaedlich;
    /// die Seite ruft es beim Aufbau, beim Umschalten und beim Wechsel der alten Ansicht.
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

        // Genau einer der beiden Nova-Punkte ist angehakt — auch in der alten Ansicht, damit
        // sichtbar bleibt, welche Ansicht beim Zurueckschalten wiederkommt.
        var istListe = HaltungenAnsichtRegel.IstListe(Gewaehlt);
        _e.ListeSchalter.IsChecked = istListe;
        _e.TabelleSchalter.IsChecked = !istListe;

        if (sicht.Liste)
        {
            // Die Mehrfachauswahl der Tabelle ueberlebt den Wechsel sonst unsichtbar weiter.
            _e.Tabelle.SelectedItems.Clear();
            if (_e.Liste.SelectedItem is { } gewaehlt)
                _e.Tabelle.SelectedItem = gewaehlt;
            _e.Liste.ScrolleZurAuswahl();
        }
    }

    /// <summary>
    /// Waehlt die Ansicht aus dem angeklickten Menuepunkt (<c>Tag</c> "liste" oder "tabelle"),
    /// speichert sie und wendet sie sofort an. Ein Klick verlaesst dabei die alte
    /// Schachtansicht — die drei Punkte sind eine Gruppe.
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
        if (_settings() is { } settings && !string.Equals(settings.SchaechteAnsicht, neu, StringComparison.Ordinal))
        {
            settings.SchaechteAnsicht = neu;
            _speichern();
        }

        _e.AlteAnsichtSchalter.IsChecked = false;
        WendeAn();
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
