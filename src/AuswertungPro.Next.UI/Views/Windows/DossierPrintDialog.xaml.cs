using System.Windows;
using AuswertungPro.Next.Application.Reports;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class DossierPrintDialog : Window
{
    public DossierPrintOptions? SelectedOptions { get; private set; }

    public DossierPrintDialog()
    {
        InitializeComponent();
    }

    public void SetAvailability(bool schachtVonFound, string? schachtVonNr,
                                bool schachtBisFound, string? schachtBisNr,
                                bool hydraulikAvailable,
                                bool kostenAvailable,
                                int originalPdfCount)
    {
        InfoSchachtVon.Text = schachtVonFound
            ? $"Schacht Von: verfuegbar ({schachtVonNr})"
            : $"Schacht Von: nicht verfuegbar ({schachtVonNr ?? "-"})";
        InfoSchachtVon.Foreground = MakeBrush(schachtVonFound);

        InfoSchachtBis.Text = schachtBisFound
            ? $"Schacht Bis: verfuegbar ({schachtBisNr})"
            : $"Schacht Bis: nicht verfuegbar ({schachtBisNr ?? "-"})";
        InfoSchachtBis.Foreground = MakeBrush(schachtBisFound);

        InfoHydraulik.Text = hydraulikAvailable
            ? "Hydraulik: verfuegbar (DN + Gefaelle vorhanden)"
            : "Hydraulik: nicht verfuegbar (DN oder Gefaelle fehlt)";
        InfoHydraulik.Foreground = MakeBrush(hydraulikAvailable);

        InfoKosten.Text = kostenAvailable
            ? "Kostenschaetzung: verfuegbar"
            : "Kostenschaetzung: nicht verfuegbar";
        InfoKosten.Foreground = MakeBrush(kostenAvailable);

        InfoOriginalPdf.Text = originalPdfCount > 0
            ? $"Original-Protokolle: verfuegbar ({originalPdfCount} PDF)"
            : "Original-Protokolle: nicht verfuegbar";
        InfoOriginalPdf.Foreground = MakeBrush(originalPdfCount > 0);

        if (!schachtVonFound)
        {
            ChkSchachtVon.IsEnabled = false;
            ChkSchachtVon.IsChecked = false;
        }

        if (!schachtBisFound)
        {
            ChkSchachtBis.IsEnabled = false;
            ChkSchachtBis.IsChecked = false;
        }

        if (!hydraulikAvailable)
        {
            ChkHydraulik.IsEnabled = false;
            ChkHydraulik.IsChecked = false;
        }

        if (!kostenAvailable)
        {
            ChkKostenschaetzung.IsEnabled = false;
            ChkKostenschaetzung.IsChecked = false;
        }

        if (originalPdfCount == 0)
        {
            ChkOriginalProtokolle.IsEnabled = false;
            ChkOriginalProtokolle.IsChecked = false;
        }
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (!HasAnySelection())
        {
            MessageBox.Show(
                "Bitte mindestens eine Sektion fuer das Dossier auswaehlen.",
                "Dossier",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        SelectedOptions = new DossierPrintOptions
        {
            IncludeDeckblatt = ChkDeckblatt.IsChecked == true,
            IncludeHaltungsprotokoll = ChkHaltungsprotokoll.IsChecked == true,
            IncludeFotos = ChkFotos.IsChecked == true,
            IncludeSchachtVon = ChkSchachtVon.IsChecked == true,
            IncludeSchachtBis = ChkSchachtBis.IsChecked == true,
            IncludeHydraulik = ChkHydraulik.IsChecked == true,
            IncludeKostenschaetzung = ChkKostenschaetzung.IsChecked == true,
            IncludeOriginalProtokolle = ChkOriginalProtokolle.IsChecked == true,
        };
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private static System.Windows.Media.SolidColorBrush MakeBrush(bool positive) =>
        new(positive
            ? System.Windows.Media.Color.FromRgb(0x16, 0xA3, 0x4A)
            : System.Windows.Media.Color.FromRgb(0x8B, 0x94, 0x9E));

    private bool HasAnySelection() =>
        ChkDeckblatt.IsChecked == true
        || ChkHaltungsprotokoll.IsChecked == true
        || ChkFotos.IsChecked == true
        || ChkSchachtVon.IsChecked == true
        || ChkSchachtBis.IsChecked == true
        || ChkHydraulik.IsChecked == true
        || ChkKostenschaetzung.IsChecked == true
        || ChkOriginalProtokolle.IsChecked == true;
}
