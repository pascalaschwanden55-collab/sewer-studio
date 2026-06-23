using System.Windows;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void CodingMode_Click(object sender, RoutedEventArgs e)
    {
        if (_haltungRecord == null)
        {
            DialogHost.Current.Info(
                "Codier-Modus benötigt eine Haltung.\n" +
                "Bitte das Video über die Datenseite mit einer Haltung öffnen.",
                "Codier-Modus");
            return;
        }

        EnterCodingMode();
    }

    private void EnterCodingMode()
    {
        if (_isCodingMode || _haltungRecord == null) return;
        _isCodingMode = true;
        ResetFrameReadiness();

        PrepareCodingModePlayback();
        CreateCodingSessionState();
        ApplyCodingDnCalibration();

        // Fallback: Haltungslaenge pruefen, ggf. manuell abfragen
        EnsureHaltungslaenge(_haltungRecord);

        if (!TryStartCodingSession())
            return;

        InitializeCodingImportReferences();
        ActivateDefaultCodingTool();
        ShowCodingModeUi();

        InitializeCodingTimeline();
        StartCodingModeBackgroundServices();

        // Bestehende Protokoll-Eintraege direkt in Import-Referenz laden
        // (NICHT in KI-Befunde - die startet leer)
        LoadExistingProtocolEventsAsImport();

        // Video an Anfang setzen (direkt, nicht ueber PropertyChanged)
        _codingNavPending = true;
        SyncVideoToCodingMeter();
    }

}
