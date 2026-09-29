using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;
using AuswertungPro.Next.Application.UseCases.Suche;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.DataPage;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AuswertungPro.Next.UI.ViewModels;

/// <summary>Suchfeld der Kopfzeile (Strg+K). Sucht live; ein gewaehlter Treffer springt zur Seite
/// oder fuehrt einen Befehl aus (Aufgabe 14, Optikanalyse 28.09.2026).</summary>
public sealed partial class GlobaleSucheViewModel : ObservableObject, IDisposable
{
    private readonly ShellViewModel _shell;
    private bool _disposed;
    [ObservableProperty] private string _text = string.Empty;
    [ObservableProperty] private bool _listeOffen;
    /// <summary>Ueber Pfeiltasten markierter Index (Inventar 3.5); -1 = keine Markierung.</summary>
    [ObservableProperty] private int _markiertIndex = -1;
    public ObservableCollection<GlobaleSucheTreffer> Treffer { get; } = new();
    public string LeerText => Text.Trim().Length == 0 ? "Name, Nummer, Strasse oder Befehl eingeben." : Treffer.Count == 0 ? "Kein Treffer" : string.Empty;

    public GlobaleSucheViewModel(ShellViewModel shell)
    {
        _shell = shell;
        _shell.PropertyChanged += OnShellGeaendert;

        // Fix-Runde 1, Befund 1: Diese vier Befehle haengen an einem Betriebs-Schutz
        // (Import/Export laeuft), der sich AUCH aendern kann, waehrend die Trefferliste bereits
        // offen ist. Ohne diese Anbindung bliebe ein zuvor sichtbarer, inzwischen gesperrter
        // Treffer bis zum naechsten Tastendruck sichtbar stehen.
        _shell.SaveCommand.CanExecuteChanged += OnBefehlsVerfuegbarkeitGeaendert;
        _shell.SaveAsProjectCommand.CanExecuteChanged += OnBefehlsVerfuegbarkeitGeaendert;
        _shell.NewProjectCommand.CanExecuteChanged += OnBefehlsVerfuegbarkeitGeaendert;
        _shell.OpenProjectCommand.CanExecuteChanged += OnBefehlsVerfuegbarkeitGeaendert;
    }

    /// <summary>
    /// Aktualisiert die Trefferliste, wenn sich die Verfuegbarkeit eines Betriebs-gebundenen
    /// Befehls aendert (Fix-Runde 1, Befund 1) - z. B. weil waehrend die Suche offen ist ein
    /// Import/Export startet oder endet. Nur relevant, solange ueberhaupt gesucht wird.
    /// </summary>
    private void OnBefehlsVerfuegbarkeitGeaendert(object? sender, EventArgs e)
    {
        if (Text.Trim().Length > 0)
            AktualisiereTreffer(Text);
    }

    /// <summary>
    /// R1 (Gesamtaudit 08.09.2026): Ein Treffer zeigt direkt auf einen Datensatz. Wechselt das
    /// Projekt, gehoert er zum alten Bestand und wuerde beim Anklicken ein Objekt oeffnen, das
    /// im offenen Projekt gar nicht existiert. Die Liste wird deshalb verworfen.
    /// </summary>
    private void OnShellGeaendert(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (nameof(ShellViewModel.Project) or nameof(ShellViewModel.IsProjectReady)))
            return;

        Verwerfe();
    }

    private void Verwerfe()
    {
        // Ein nicht leeres Feld sucht ueber OnTextChanged sofort im neuen Bestand weiter;
        // hier wird es geleert, damit kein alter Treffer sichtbar bleibt.
        if (Text.Length != 0)
        {
            Text = string.Empty;
            return;
        }

        Treffer.Clear();
        ListeOffen = false;
        MarkiertIndex = -1;
        OnPropertyChanged(nameof(LeerText));
    }

    partial void OnTextChanged(string value) => AktualisiereTreffer(value);

    /// <summary>Baut die Trefferliste neu auf - bei Texteingabe und bei einer geaenderten
    /// Befehlsverfuegbarkeit (siehe <see cref="OnBefehlsVerfuegbarkeitGeaendert"/>).</summary>
    private void AktualisiereTreffer(string value)
    {
        Treffer.Clear();
        var haltungen = _shell.IsProjectReady ? _shell.Project.Data : Enumerable.Empty<HaltungRecord>();
        var schaechte = _shell.IsProjectReady ? _shell.Project.SchaechteData : Enumerable.Empty<SchachtRecord>();
        var treffer = GlobaleSucheRegel.Suche(value, haltungen, schaechte, SchaechteColumnPolicy.GetSchachtNumber, BaueBefehle());

        // Die reine Regel liefert nie einen Gruppenkopf (Domain-fern) - die UI setzt ihn davor,
        // sobald der erste Befehlstreffer kommt. Erscheinen Befehle zuerst (Reihenfolge-Regel bei
        // Wortsuchen), steht die Ueberschrift ganz oben; kommen sie nach den Datentreffern, steht
        // sie an der Nahtstelle.
        var kopfGesetzt = false;
        foreach (var t in treffer)
        {
            if (t.Art == GlobaleSucheArt.Befehl && !kopfGesetzt)
            {
                Treffer.Add(new GlobaleSucheTreffer(GlobaleSucheArt.Gruppenkopf, "Befehle", null));
                kopfGesetzt = true;
            }
            Treffer.Add(t);
        }

        ListeOffen = value.Trim().Length > 0;
        MarkiertIndex = -1;
        OnPropertyChanged(nameof(LeerText));
    }

    /// <summary>
    /// Baut den Befehlskatalog frisch aus dem aktuellen Shell-Zustand: alle Seiten der Leiste
    /// («Gehe zu: …», Glyph und Verfuegbarkeit vom jeweiligen <c>NavItem</c>) und die
    /// Hauptbefehle. <c>Verfuegbar</c> kommt ueberall vom echten <c>CanExecute</c> bzw. von
    /// <c>NavItem.IsAvailable</c> - ein Befehl ohne offenes Projekt taucht dann gar nicht erst
    /// in der Trefferliste auf, statt nur deaktiviert zu erscheinen. Die Ausfuehrung ruft
    /// ausschliesslich vorhandene ShellViewModel-Befehle/-Methoden auf.
    /// </summary>
    private IReadOnlyList<GlobaleSucheBefehlEintrag> BaueBefehle()
    {
        var befehle = new List<GlobaleSucheBefehlEintrag>();

        foreach (var seite in _shell.NavItems)
            befehle.Add(new GlobaleSucheBefehlEintrag(
                $"GoTo:{seite.Title}",
                $"Gehe zu: {seite.DisplayTitle}",
                seite.Icon,
                seite.IsAvailable,
                () => _shell.NavigateTo(seite.Title)));

        // "Import starten" ist bewusst ein zweiter Weg zur selben Seite (Entscheid Aufgabe 14):
        // Der Suchtext "start" oder "import starten" soll unabhaengig vom generischen
        // "Gehe zu: Import" treffen. Beide fuehren dieselbe Navigation aus.
        var importSeite = _shell.NavItems.FirstOrDefault(n => n.Title == "Import");
        if (importSeite is not null)
            befehle.Add(new GlobaleSucheBefehlEintrag(
                "ImportStarten", "Import starten", importSeite.Icon, importSeite.IsAvailable,
                () => _shell.NavigateTo("Import")));

        // Glyphen sind dieselben Segoe-Fluent-Codepunkte wie im Menue Datei/Hilfe (MainWindow.xaml
        // &#xE710; usw.) - ein Befehl in der Suche zeigt so dasselbe Symbol wie im Menue
        // (Fix-Runde 1, Befund 2: waren bereits als rohe, im Editor unsichtbare PUA-Zeichen
        // gesetzt, jetzt als lesbare \uXXXX-Escapes mit identischem Codepunkt).
        befehle.Add(Befehl("NeuesProjekt", "Neues Projekt", "\uE710", _shell.NewProjectCommand));
        befehle.Add(Befehl("ProjektOeffnen", "Projekt öffnen", "\uE838", _shell.OpenProjectCommand));
        befehle.Add(Befehl("Speichern", "Speichern", "\uE74E", _shell.SaveCommand));
        befehle.Add(Befehl("SpeichernUnter", "Speichern unter", "\uE792", _shell.SaveAsProjectCommand));

        var einstellungenSeite = _shell.NavItems.FirstOrDefault(n => n.Title == "Einstellungen");
        if (einstellungenSeite is not null)
            befehle.Add(new GlobaleSucheBefehlEintrag(
                "Einstellungen", "Einstellungen", einstellungenSeite.Icon, einstellungenSeite.IsAvailable,
                () => _shell.NavigateTo("Einstellungen")));

        befehle.Add(Befehl("Handbuch", "Handbuch", "\uE736", _shell.OpenHandbuchCommand, _shell.SelectedNavItem?.Title));
        befehle.Add(Befehl("Tastenkuerzel", "Tastenkürzel", "\uE765", _shell.OpenTastenkuerzelCommand));
        befehle.Add(Befehl("UeberSewerStudio", "Über SewerStudio", "\uE946", _shell.ShowAboutCommand));
        // Fokusmodus hat im Menue keinen Eintrag mit Glyph (er ist dort ein checkbarer
        // Menuepunkt ohne Icon, siehe CLAUDE.md-Regel zu Menue-Icons) - dasselbe "gross
        // anzeigen"-Symbol wie der vorhandene Vollflaechen-Umschalter (HaltungFelderDrawer.xaml).
        befehle.Add(Befehl("Fokusmodus", "Fokusmodus", "\uE740", _shell.ToggleFocusModeCommand));

        return befehle;
    }

    /// <summary>
    /// Fix-Runde 1, Befund 1: <c>Verfuegbar</c> ist der beim Bauen der Liste erfasste Stand - er
    /// entscheidet nur, ob der Treffer ueberhaupt erscheint. Die Ausfuehrung prueft
    /// <c>CanExecute</c> UNMITTELBAR vor <c>Execute</c> ein zweites Mal: Wird ein Betriebs-Schutz
    /// aktiv, waehrend die Trefferliste bereits offen ist (Import/Export startet zwischen Anzeige
    /// und Klick), fuehrt ein Klick auf einen inzwischen gesperrten Treffer nichts mehr aus.
    /// </summary>
    private static GlobaleSucheBefehlEintrag Befehl(string schluessel, string anzeigename, string glyph, ICommand command, object? parameter = null)
        => new(schluessel, anzeigename, glyph, command.CanExecute(parameter), () =>
        {
            if (command.CanExecute(parameter))
                command.Execute(parameter);
        });

    /// <summary>Treffer waehlen: fuehrt einen Befehl aus oder navigiert zur passenden Seite,
    /// schliesst die Liste und leert das Feld. Ein Gruppenkopf ist nie auswaehlbar.</summary>
    public void Waehle(GlobaleSucheTreffer? treffer)
    {
        if (treffer is null || treffer.Art == GlobaleSucheArt.Gruppenkopf) return;

        if (treffer.Art == GlobaleSucheArt.Befehl)
        {
            (treffer.Ziel as Action)?.Invoke();
            ListeOffen = false;
            Text = string.Empty;
            return;
        }

        // Zweite Sicherung zu OnShellGeaendert: Zwischen Anzeige und Klick kann das Projekt
        // gewechselt haben. Ein fremder Datensatz wird nie geoeffnet.
        if (!GehoertZumOffenenProjekt(treffer))
        {
            Verwerfe();
            return;
        }

        switch (treffer.Art)
        {
            case GlobaleSucheArt.Haltung: _shell.NavigateToHolding(treffer.Ziel as HaltungRecord); break;
            case GlobaleSucheArt.Schacht: _shell.NavigateToShaft(treffer.Ziel as SchachtRecord); break;
            case GlobaleSucheArt.Strasse: _shell.NavigateToDataPage(new DataPageStartFilter(FieldKeys.Street, (string)treffer.Ziel!)); break;
        }
        ListeOffen = false;
        Text = string.Empty;
    }

    /// <summary>
    /// Haltung und Schacht sind Objektverweise und muessen im offenen Projekt liegen. Die
    /// Strasse ist nur ein Filtertext; sie fuehrt hoechstens zu einer leeren Liste.
    /// </summary>
    private bool GehoertZumOffenenProjekt(GlobaleSucheTreffer treffer)
    {
        if (!_shell.IsProjectReady)
            return false;

        return treffer.Ziel switch
        {
            HaltungRecord haltung => _shell.Project.Data.Contains(haltung),
            SchachtRecord schacht => _shell.Project.SchaechteData.Contains(schacht),
            string => true,
            _ => false
        };
    }

    private bool Auswaehlbar(int index) => index >= 0 && index < Treffer.Count && Treffer[index].Art != GlobaleSucheArt.Gruppenkopf;

    /// <summary>Erster auswaehlbarer Index ab (und einschliesslich) <paramref name="ab"/> in
    /// aufsteigender Richtung; <c>null</c>, wenn keiner mehr folgt.</summary>
    private int? NaechsterAuswaehlbarerAufwaerts(int ab)
    {
        for (var i = Math.Max(ab, 0); i < Treffer.Count; i++)
            if (Treffer[i].Art != GlobaleSucheArt.Gruppenkopf) return i;
        return null;
    }

    /// <summary>Wie oben, aber abwaerts (fuer Pfeiltaste hoch und den Rueckfall).</summary>
    private int? NaechsterAuswaehlbarerAbwaerts(int ab)
    {
        for (var i = Math.Min(ab, Treffer.Count - 1); i >= 0; i--)
            if (Treffer[i].Art != GlobaleSucheArt.Gruppenkopf) return i;
        return null;
    }

    /// <summary>Pfeiltaste unten: naechsten auswaehlbaren Treffer markieren, Gruppenkoepfe
    /// werden uebersprungen (Inventar 3.5, erweitert Aufgabe 14).</summary>
    public void MarkiereNaechsten()
    {
        var index = NaechsterAuswaehlbarerAufwaerts(MarkiertIndex + 1) ?? NaechsterAuswaehlbarerAbwaerts(Treffer.Count - 1);
        if (index is not null) MarkiertIndex = index.Value;
    }

    /// <summary>Pfeiltaste oben: vorherigen auswaehlbaren Treffer markieren (Inventar 3.5,
    /// erweitert Aufgabe 14).</summary>
    public void MarkiereVorherigen()
    {
        var index = NaechsterAuswaehlbarerAbwaerts(MarkiertIndex - 1) ?? NaechsterAuswaehlbarerAufwaerts(0);
        if (index is not null) MarkiertIndex = index.Value;
    }

    /// <summary>Enter im Feld: markierten oder ersten auswaehlbaren Treffer waehlen (Inventar 3.5).</summary>
    public void WaehleErstenOderMarkierten()
    {
        if (Treffer.Count == 0) return;
        var index = Auswaehlbar(MarkiertIndex) ? MarkiertIndex : NaechsterAuswaehlbarerAufwaerts(0);
        if (index is int i) Waehle(Treffer[i]);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _shell.PropertyChanged -= OnShellGeaendert;
        _shell.SaveCommand.CanExecuteChanged -= OnBefehlsVerfuegbarkeitGeaendert;
        _shell.SaveAsProjectCommand.CanExecuteChanged -= OnBefehlsVerfuegbarkeitGeaendert;
        _shell.NewProjectCommand.CanExecuteChanged -= OnBefehlsVerfuegbarkeitGeaendert;
        _shell.OpenProjectCommand.CanExecuteChanged -= OnBefehlsVerfuegbarkeitGeaendert;
    }
}
