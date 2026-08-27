using System.Windows;
using AuswertungPro.Next.Application.Reports;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class DossierPrintDialog : Window
{
    private readonly IProtocolPdfLayoutSettings _protocolPdfLayoutSettings;

    public DossierPrintOptions? SelectedOptions { get; private set; }

    public DossierPrintDialog()
        : this(DefaultProtocolPdfLayoutSettings.Instance)
    {
    }

    public DossierPrintDialog(IProtocolPdfLayoutSettings protocolPdfLayoutSettings)
    {
        _protocolPdfLayoutSettings = protocolPdfLayoutSettings
            ?? throw new ArgumentNullException(nameof(protocolPdfLayoutSettings));
        InitializeComponent();
    }

    public void SetAvailability(bool schachtVonFound, string? schachtVonNr,
                                bool schachtBisFound, string? schachtBisNr,
                                bool hydraulikAvailable,
                                bool kostenAvailable,
                                int originalPdfCount)
    {
        InfoSchachtVon.Text = schachtVonFound
            ? $"Schacht Von: verfügbar ({schachtVonNr})"
            : $"Schacht Von: nicht verfügbar ({schachtVonNr ?? "-"})";
        InfoSchachtVon.Foreground = MakeBrush(schachtVonFound);

        InfoSchachtBis.Text = schachtBisFound
            ? $"Schacht Bis: verfügbar ({schachtBisNr})"
            : $"Schacht Bis: nicht verfügbar ({schachtBisNr ?? "-"})";
        InfoSchachtBis.Foreground = MakeBrush(schachtBisFound);

        InfoHydraulik.Text = hydraulikAvailable
            ? "Hydraulik: verfügbar (DN + Gefälle vorhanden)"
            : "Hydraulik: nicht verfügbar (DN oder Gefälle fehlt)";
        InfoHydraulik.Foreground = MakeBrush(hydraulikAvailable);

        InfoKosten.Text = kostenAvailable
            ? "Kostenschätzung: verfügbar"
            : "Kostenschätzung: nicht verfügbar";
        InfoKosten.Foreground = MakeBrush(kostenAvailable);

        InfoOriginalPdf.Text = originalPdfCount > 0
            ? $"Original-Protokolle: verfügbar ({originalPdfCount} PDF)"
            : "Original-Protokolle: nicht verfügbar";
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
            DialogHost.Current.Info(
                "Bitte mindestens eine Sektion für das Dossier auswählen.",
                "Dossier");
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
            // Fotoseiten wie im Haltungsprotokoll: dieselbe zentrale Einstellung entscheidet.
            PhotosPerPage = _protocolPdfLayoutSettings.PhotosPerPage,
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

    /// <summary>
    /// Kompatibilitaet fuer Designer und alte direkte Fensteraufrufe. Der produktive Weg
    /// uebergibt die zentral registrierte Einstellung ausdruecklich.
    /// </summary>
    private sealed class DefaultProtocolPdfLayoutSettings : IProtocolPdfLayoutSettings
    {
        public static DefaultProtocolPdfLayoutSettings Instance { get; } = new();

        public int PhotosPerPage => ProtocolPdfPhotoLayout.DefaultPhotosPerPage;
    }
}
