using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai;

public sealed record CodingCalibrationToggleState(
    bool IsCalibrating,
    OverlayToolType ActiveTool,
    string? ActiveToolName,
    string ToolLabel,
    bool ShowHint,
    string HintText);

public static class CodingCalibrationTogglePolicy
{
    public const string CalibrateButtonName = "BtnCodingCalibrate";
    public const string CalibrationLabel = "Kalibrieren";
    public const string CalibrationHintText = "Linie ueber den sichtbaren Rohrdurchmesser zeichnen";

    public static CodingCalibrationToggleState Build(bool isCurrentlyCalibrating)
    {
        var isCalibrating = !isCurrentlyCalibrating;
        return new CodingCalibrationToggleState(
            IsCalibrating: isCalibrating,
            ActiveTool: OverlayToolType.None,
            ActiveToolName: isCalibrating ? CalibrateButtonName : null,
            ToolLabel: isCalibrating ? CalibrationLabel : "",
            ShowHint: isCalibrating,
            HintText: CalibrationHintText);
    }
}
