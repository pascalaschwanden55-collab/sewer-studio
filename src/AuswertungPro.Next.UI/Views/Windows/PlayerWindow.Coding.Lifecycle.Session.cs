using System;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels.Windows;
using InfraSelfImproving = AuswertungPro.Next.Infrastructure.Ai.SelfImproving;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void CreateCodingSessionState()
    {
        _codingSessionService = CreateCodingSessionService();
        _codingOverlayService = new OverlayToolService();
        _codingSchemaManager.Cancel();
        _codingSchemaType = null;
        _codingVm = new CodingSessionViewModel(
            _codingSessionService,
            _codingOverlayService,
            new InfraSelfImproving.CodingFeedbackRecorder());
        _codingVm.VideoPath = _videoPath;
        _codingVm.PropertyChanged += CodingVm_PropertyChanged;
    }

    private void ApplyCodingDnCalibration()
    {
        if (_haltungRecord == null || _codingOverlayService == null)
            return;

        var dnCalibration = CodingDnCalibrationPolicy.Build(_haltungRecord.Fields);
        if (dnCalibration.Calibration != null)
            _codingOverlayService.SetCalibration(dnCalibration.Calibration);
        TxtCodingCalibDn.Text = dnCalibration.DnText;
        TxtCodingCalibStatus.Text = dnCalibration.CalibrationStatusText;
    }

    private bool TryStartCodingSession()
    {
        if (_haltungRecord == null || _codingVm == null || _codingSessionService == null)
            return false;

        try
        {
            _codingVm.StartSessionCommand.Execute(_haltungRecord);
        }
        catch (Exception ex)
        {
            DialogHost.Current.Warn(ex.Message, "Codier-Modus");
            ExitCodingMode();
            return false;
        }

        // StartSessionCommand faengt Fehler intern ab, z.B. fehlende Haltungslaenge.
        if (_codingSessionService.ActiveSession == null)
        {
            ExitCodingMode();
            return false;
        }

        _codingSessionService.PauseSession();
        TxtCodingRange.Text = $"/ {_codingVm.EndMeter:F2}m";
        TxtCodingMeter.Text = "0.00m";
        return true;
    }
}
