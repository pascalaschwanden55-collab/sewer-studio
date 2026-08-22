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
            _sp.Dialogs);
        var window = new Views.Windows.MeasureTemplateEditorWindow
        {
            DataContext = vm
        };
        window.ShowDialog();
    }

    public sealed partial class NavItem : ObservableObject
    {
        private bool _isAvailable = true;

        public NavItem(string icon, string title, Func<object> createPage, bool? canOpenWithoutProject = null)
        {
            Icon = icon;
            Title = title;
            CreatePage = createPage;
            CanOpenWithoutProject = canOpenWithoutProject ?? ShellNavigationPolicy.CanOpenWithoutProject(title);
        }

        public string Icon { get; }

        public string Title { get; }

        public string ToolTipDescription => Title switch
        {
            "Uebersicht" => "Projekt-Cockpit mit Zustands-, Kosten- und Fortschrittsauswertung.",
            "Projekt" => "Projektstammdaten, Speicherort und Bearbeitungsdaten pflegen.",
            "Haltungen" => "Haltungen pruefen, filtern, Videos und Protokolle oeffnen.",
            "Schaechte" => "Schachtdaten anzeigen, kontrollieren und zugehoerige Protokolle oeffnen.",
            "Import" => "Inspektionsdaten, PDFs, Videos und Zusatzquellen ins Projekt uebernehmen.",
            "Export" => "Excel- und PDF-Ausgaben fuer Auswertung und Weitergabe erzeugen.",
            "Karte" => "Haltungen raeumlich ansehen und von der Karte aus oeffnen.",
            "Medienkonflikte" => "Fehlende, doppelte oder mehrdeutige Medienzuordnungen klaeren.",
            "Druckcenter" => "Dossiers und Berichte fuer Haltungen oder Projektumfang erstellen.",
            "Dossiers" => "Eigentuemerdossiers zusammenstellen, bearbeiten und als PDF ausgeben.",
            "Sanierungs-Matrix" => "Massnahmen, Kosten und Varianten fuer Sanierung bearbeiten.",
            "Schacht-Matrix" => "Sanierungsmassnahmen und Kosten je Schacht (NPK Kap. 700) erfassen.",
            "VSA" => "VSA-Zustandsklassen und Bewertungsdaten kontrollieren.",
            "Schattenauswertung" => "KI-Vorschlaege im Hintergrund pruefen und mit den Projektdaten vergleichen.",
            "Diagnose" => "Logs, Diagnoseinformationen und technische Details pruefen.",
            "Einstellungen" => "Pfade, Theme, KI-Start und Programmverhalten konfigurieren.",
            _ => "Ansicht oeffnen."
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
