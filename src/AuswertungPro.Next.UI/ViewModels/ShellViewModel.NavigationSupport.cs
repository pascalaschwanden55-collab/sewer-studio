using CommunityToolkit.Mvvm.ComponentModel;

namespace AuswertungPro.Next.UI.ViewModels;

public sealed partial class ShellViewModel
{
    private void OpenPriceCatalog()
    {
        // EIN Preis-Katalog: derselbe, den Kostenrechner und Sanierungs-Matrix nutzen
        // (cost_catalog.json über CostCatalogStore). Der alte PriceCatalogEditor wird
        // bewusst nicht mehr geöffnet, damit es nur einen anwendbaren Katalog gibt.
        var dialog = new Dialogs.CostCatalogEditorDialog(
            _sp.Settings.LastProjectPath,
            _sp.CostStores.CreateCostCatalogStore());
        dialog.ShowDialog();
    }

    private void OpenTemplateEditor()
    {
        var vm = new Windows.MeasureTemplateEditorViewModel(
            _sp.Settings.LastProjectPath,
            _sp.CostStores.CreateMeasureTemplateStore(),
            _sp.CostStores.CreateCostCatalogStore(),
            _sp.Dialogs,
            toasts: _sp.Toasts);
        var window = new Views.Windows.MeasureTemplateEditorWindow
        {
            DataContext = vm
        };
        window.ShowDialog();
    }

    /// <summary>Aufgabe 5 (Programmidentitaet): «Über SewerStudio». Der Grafikkartenname kommt vom
    /// bereits laufenden <see cref="Monitor"/> (synchron, kein neuer Sensor).</summary>
    private void ShowAbout()
    {
        var window = new Views.Windows.AboutWindow(Monitor.GpuName, _sp.Dialogs);
        window.ShowDialog();
    }

    /// <summary>Aufgabe 6 (Hilfe-Menue, F1, Tastenkuerzel, Handbuch): oeffnet das Handbuch beim
    /// Abschnitt der aktuell gewaehlten Seite. <see cref="Views.Windows.HandbuchWindow.ZeigeAn"/>
    /// haelt das Fenster als Einzelstueck - ein bereits offenes Handbuch wird nur weitergeschaltet.</summary>
    private void OpenHandbuch(string? seitenSchluessel)
        => Views.Windows.HandbuchWindow.ZeigeAn(seitenSchluessel);

    /// <summary>Aufgabe 6: oeffnet die Tastenkuerzel-Uebersicht (ebenfalls ein Einzelstueck).</summary>
    private void OpenTastenkuerzel()
        => Views.Windows.TastenkuerzelWindow.ZeigeAn();

    public sealed partial class NavItem : ObservableObject
    {
        private bool _isAvailable = true;

        public NavItem(string icon, string title, Func<object> createPage, bool? canOpenWithoutProject = null)
        {
            Icon = icon;
            Title = title;
            CreatePage = createPage;
            CanOpenWithoutProject = canOpenWithoutProject ?? ShellNavigationPolicy.CanOpenWithoutProject(title);
            Group = ShellNavigationGroups.GroupOf(title);
        }

        public string Icon { get; }

        /// <summary>Gruppe in der linken Leiste (Projekt, Daten, Bewertung, System).</summary>
        public string Group { get; }

        public string Title { get; }

        /// <summary>Anzeigename mit echten Umlauten (Nova-Etappe 2); Title bleibt der ASCII-Schluessel fuer Trigger/Vergleiche.</summary>
        public string DisplayTitle => ShellNavigationTitles.Anzeige(Title);

        public string ToolTipDescription => Title switch
        {
            "Uebersicht" => "Projekt-Cockpit mit Zustands-, Kosten- und Fortschrittsauswertung.",
            "Projekt" => "Projektstammdaten, Speicherort und Bearbeitungsdaten pflegen.",
            "Haltungen" => "Haltungen prüfen, filtern, Videos und Protokolle öffnen.",
            "Schaechte" => "Schachtdaten anzeigen, kontrollieren und zugehörige Protokolle öffnen.",
            "Import" => "Inspektionsdaten, PDFs, Videos und Zusatzquellen ins Projekt übernehmen.",
            "Export" => "Excel- und PDF-Ausgaben für Auswertung und Weitergabe erzeugen.",
            "Medienkonflikte" => "Fehlende, doppelte oder mehrdeutige Medienzuordnungen klären.",
            "Druckcenter" => "Dossiers und Berichte für Haltungen oder Projektumfang erstellen.",
            "Dossiers" => "Eigentümerdossiers zusammenstellen, bearbeiten und als PDF ausgeben.",
            "Sanierungs-Matrix" => "Massnahmen, Kosten und Varianten für Sanierung bearbeiten.",
            "Schacht-Matrix" => "Sanierungsmassnahmen und Kosten je Schacht (NPK Kap. 700) erfassen.",
            "VSA" => "VSA-Zustandsklassen und Bewertungsdaten kontrollieren.",
            "Schattenauswertung" => "KI-Vorschläge im Hintergrund prüfen und mit den Projektdaten vergleichen.",
            "Diagnose" => "Logs, Diagnoseinformationen und technische Details prüfen.",
            "Einstellungen" => "Pfade, Theme, KI-Start und Programmverhalten konfigurieren.",
            _ => "Ansicht öffnen."
        };

        public string ToolTipShortcut => string.Empty;

        public Func<object> CreatePage { get; }

        public bool CanOpenWithoutProject { get; }

        public bool RequiresProject => !CanOpenWithoutProject;

        public bool IsAvailable
        {
            get => _isAvailable;
            private set
            {
                if (SetProperty(ref _isAvailable, value))
                    OnPropertyChanged(nameof(AvailabilityOpacity));
            }
        }

        public double AvailabilityOpacity => IsAvailable ? 1.0 : 0.5;

        public void UpdateAvailability(bool isProjectReady)
            => IsAvailable = isProjectReady || CanOpenWithoutProject;
    }
}
