using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;

namespace AuswertungPro.Next.UI.Views.Windows;

/// <summary>
/// Generischer Druckoptionen-Dialog — ersetzt DossierPrintDialog und HydraulikPrintDialog.
/// Konfigurierbar ueber PrintDialogConfig fuer beliebige PDF-Typen.
/// </summary>
public partial class PrintOptionsDialog : Window
{
    public PrintOptionsDialogViewModel ViewModel { get; }

    private readonly IDialogService _dialogs = App.Resolve<IDialogService>();

    public PrintOptionsDialog(PrintDialogConfig config)
    {
        ViewModel = new PrintOptionsDialogViewModel(config);
        DataContext = ViewModel;
        InitializeComponent();
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.HasAnySelection())
        {
            _dialogs.ShowMessage(
                "Bitte mindestens eine Sektion auswaehlen.",
                ViewModel.DialogTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    /// <summary>
    /// Gibt die ausgewaehlten Optionen als Dictionary (Key → IsChecked) zurueck.
    /// </summary>
    public Dictionary<string, bool> GetSelectedOptions() =>
        ViewModel.Sections
            .SelectMany(s => s.Options)
            .ToDictionary(o => o.Key, o => o.IsChecked);
}

// ── Konfiguration ──────────────────────────────────────────────

/// <summary>
/// Konfiguration fuer den generischen PrintOptionsDialog.
/// </summary>
public sealed record PrintDialogConfig
{
    public required string Title { get; init; }
    public string Subtitle { get; init; } = "Abschnitte fuer den Druck auswaehlen.";
    public string OkText { get; init; } = "PDF erstellen";
    public string OkTooltip { get; init; } = "PDF mit den gewaehlten Abschnitten erzeugen";
    public required List<PrintSectionConfig> Sections { get; init; }
    public List<PrintInfoLine>? InfoLines { get; init; }
}

public sealed record PrintSectionConfig
{
    public string? Header { get; init; }
    public required List<PrintOptionConfig> Options { get; init; }
}

public sealed record PrintOptionConfig
{
    public required string Key { get; init; }
    public required string Label { get; init; }
    public bool DefaultChecked { get; init; } = true;
    public bool Enabled { get; init; } = true;
    public string? InfoText { get; init; }
}

public sealed record PrintInfoLine
{
    public required string Text { get; init; }
    public bool IsPositive { get; init; } = true;

    public Brush Foreground => IsPositive
        ? new SolidColorBrush(Color.FromRgb(0x16, 0xA3, 0x4A))
        : new SolidColorBrush(Color.FromRgb(0x8B, 0x94, 0x9E));
}

// ── ViewModel ──────────────────────────────────────────────────

public sealed class PrintOptionsDialogViewModel : INotifyPropertyChanged
{
    public string DialogTitle { get; }
    public string DialogSubtitle { get; }
    public string OkButtonText { get; }
    public string OkButtonTooltip { get; }
    public ObservableCollection<PrintSectionViewModel> Sections { get; }
    public ObservableCollection<PrintInfoLine> InfoLines { get; }

    public PrintOptionsDialogViewModel(PrintDialogConfig config)
    {
        DialogTitle = config.Title;
        DialogSubtitle = config.Subtitle;
        OkButtonText = config.OkText;
        OkButtonTooltip = config.OkTooltip;

        Sections = new ObservableCollection<PrintSectionViewModel>(
            config.Sections.Select(s => new PrintSectionViewModel
            {
                Header = s.Header,
                Options = new ObservableCollection<PrintOptionViewModel>(
                    s.Options.Select(o => new PrintOptionViewModel
                    {
                        Key = o.Key,
                        Label = o.Label,
                        IsChecked = o.DefaultChecked && o.Enabled,
                        IsEnabled = o.Enabled,
                        InfoText = o.InfoText
                    }))
            }));

        InfoLines = new ObservableCollection<PrintInfoLine>(config.InfoLines ?? []);
    }

    public bool HasAnySelection() =>
        Sections.Any(s => s.Options.Any(o => o.IsChecked));

#pragma warning disable CS0067
    public event PropertyChangedEventHandler? PropertyChanged;
#pragma warning restore CS0067
}

public sealed class PrintSectionViewModel : INotifyPropertyChanged
{
    public string? Header { get; init; }
    public ObservableCollection<PrintOptionViewModel> Options { get; init; } = [];
#pragma warning disable CS0067
    public event PropertyChangedEventHandler? PropertyChanged;
#pragma warning restore CS0067
}

public sealed class PrintOptionViewModel : INotifyPropertyChanged
{
    public required string Key { get; init; }
    public required string Label { get; init; }
    public string? InfoText { get; init; }

    private bool _isChecked;
    public bool IsChecked
    {
        get => _isChecked;
        set { _isChecked = value; OnPropertyChanged(); }
    }

    private bool _isEnabled = true;
    public bool IsEnabled
    {
        get => _isEnabled;
        set { _isEnabled = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

// ── Factory-Methoden fuer bekannte Dialogtypen ─────────────────

/// <summary>
/// Factory fuer die beiden bekannten Druckdialoge (Dossier und Hydraulik).
/// Koennen spaeter um weitere PDF-Typen erweitert werden.
/// </summary>
public static class PrintDialogFactory
{
    /// <summary>
    /// Erstellt die Konfiguration fuer den Haltungsdossier-Druckdialog.
    /// </summary>
    public static PrintDialogConfig CreateDossierConfig(
        bool schachtVonAvailable = true, string? schachtVonNr = null,
        bool schachtBisAvailable = true, string? schachtBisNr = null,
        bool hydraulikAvailable = true,
        bool kostenAvailable = true,
        int originalPdfCount = 0)
    {
        return new PrintDialogConfig
        {
            Title = "Haltungsdossier — Druckoptionen",
            Subtitle = "Die Reihenfolge entspricht der Ausgabe im PDF.",
            OkText = "Dossier erstellen",
            OkTooltip = "Haltungsdossier-PDF mit den gewaehlten Abschnitten erzeugen",
            Sections =
            [
                new PrintSectionConfig
                {
                    Header = "1) Haltung",
                    Options =
                    [
                        new() { Key = "Deckblatt", Label = "Deckblatt (Projekt- und Haltungsuebersicht)" },
                        new() { Key = "Haltungsprotokoll", Label = "Haltungsprotokoll (Beobachtungen / Codes)" },
                        new() { Key = "Fotos", Label = "Fotos aus Haltungsprotokoll" },
                    ]
                },
                new PrintSectionConfig
                {
                    Header = "2) Schaechte",
                    Options =
                    [
                        new() { Key = "SchachtVon", Label = "Schacht Von (Stammdaten + Befunde)", Enabled = schachtVonAvailable },
                        new() { Key = "SchachtBis", Label = "Schacht Bis (Stammdaten + Befunde)", Enabled = schachtBisAvailable },
                    ]
                },
                new PrintSectionConfig
                {
                    Header = "3) Auswertung",
                    Options =
                    [
                        new() { Key = "Hydraulik", Label = "Hydraulik-Berechnung", Enabled = hydraulikAvailable },
                        new() { Key = "Kostenschaetzung", Label = "Kostenschaetzung / Massnahmen", Enabled = kostenAvailable },
                    ]
                },
                new PrintSectionConfig
                {
                    Header = "4) Anhaenge",
                    Options =
                    [
                        new() { Key = "OriginalProtokolle", Label = "Original-Protokolle als PDF-Anhang", Enabled = originalPdfCount > 0 },
                    ]
                },
            ],
            InfoLines =
            [
                new() { Text = schachtVonAvailable ? $"Schacht Von: verfuegbar ({schachtVonNr})" : $"Schacht Von: nicht verfuegbar ({schachtVonNr ?? "-"})", IsPositive = schachtVonAvailable },
                new() { Text = schachtBisAvailable ? $"Schacht Bis: verfuegbar ({schachtBisNr})" : $"Schacht Bis: nicht verfuegbar ({schachtBisNr ?? "-"})", IsPositive = schachtBisAvailable },
                new() { Text = hydraulikAvailable ? "Hydraulik: verfuegbar" : "Hydraulik: nicht verfuegbar", IsPositive = hydraulikAvailable },
                new() { Text = kostenAvailable ? "Kostenschaetzung: verfuegbar" : "Kostenschaetzung: nicht verfuegbar", IsPositive = kostenAvailable },
                new() { Text = originalPdfCount > 0 ? $"Original-Protokolle: {originalPdfCount} PDF" : "Original-Protokolle: nicht verfuegbar", IsPositive = originalPdfCount > 0 },
            ]
        };
    }

    /// <summary>
    /// Erstellt die Konfiguration fuer den Hydraulik-Druckdialog.
    /// </summary>
    public static PrintDialogConfig CreateHydraulikConfig()
    {
        return new PrintDialogConfig
        {
            Title = "Hydraulik-PDF — Druckoptionen",
            Subtitle = "Abschnitte fuer den Hydraulik-Bericht auswaehlen.",
            OkText = "PDF erstellen",
            OkTooltip = "Hydraulik-Bericht als PDF mit den gewaehlten Abschnitten erzeugen",
            Sections =
            [
                new PrintSectionConfig
                {
                    Options =
                    [
                        new() { Key = "Teilfuellung", Label = "Teilfuellung (v, Q, A, Lu, Rhy, Bsp)" },
                        new() { Key = "Vollfuellung", Label = "Vollfuellung (v_V, Q_V)" },
                        new() { Key = "Kennzahlen", Label = "Kennzahlen (Re, Fr, Lambda, Ny, Tau)" },
                        new() { Key = "Ablagerung", Label = "Ablagerungsgefahr (v_c, I_c, Tau_c)" },
                        new() { Key = "Auslastung", Label = "Auslastung (h/D)" },
                        new() { Key = "Bewertung", Label = "Bewertung (Ampel-Uebersicht)" },
                    ]
                },
            ]
        };
    }

    /// <summary>
    /// Konvertiert die Dialog-Auswahl in DossierPrintOptions (fuer bestehende PDF-Builder).
    /// </summary>
    public static Application.Reports.DossierPrintOptions ToDossierOptions(Dictionary<string, bool> selected) =>
        new()
        {
            IncludeDeckblatt = selected.GetValueOrDefault("Deckblatt", true),
            IncludeHaltungsprotokoll = selected.GetValueOrDefault("Haltungsprotokoll", true),
            IncludeFotos = selected.GetValueOrDefault("Fotos", true),
            IncludeSchachtVon = selected.GetValueOrDefault("SchachtVon", true),
            IncludeSchachtBis = selected.GetValueOrDefault("SchachtBis", true),
            IncludeHydraulik = selected.GetValueOrDefault("Hydraulik", true),
            IncludeKostenschaetzung = selected.GetValueOrDefault("Kostenschaetzung", true),
            IncludeOriginalProtokolle = selected.GetValueOrDefault("OriginalProtokolle", true),
        };

    /// <summary>
    /// Konvertiert die Dialog-Auswahl in HydraulikPrintOptions (fuer bestehende PDF-Builder).
    /// </summary>
    public static Application.Reports.HydraulikPrintOptions ToHydraulikOptions(Dictionary<string, bool> selected) =>
        new()
        {
            IncludeTeilfuellung = selected.GetValueOrDefault("Teilfuellung", true),
            IncludeVollfuellung = selected.GetValueOrDefault("Vollfuellung", true),
            IncludeKennzahlen = selected.GetValueOrDefault("Kennzahlen", true),
            IncludeAblagerung = selected.GetValueOrDefault("Ablagerung", true),
            IncludeAuslastung = selected.GetValueOrDefault("Auslastung", true),
            IncludeBewertung = selected.GetValueOrDefault("Bewertung", true),
        };
}
