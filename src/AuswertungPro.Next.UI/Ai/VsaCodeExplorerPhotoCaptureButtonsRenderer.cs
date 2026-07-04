using System.Windows.Controls;

namespace AuswertungPro.Next.UI.Ai;

public sealed record VsaCodeExplorerPhotoCaptureButtonsRenderTargets(
    Button CaptureFoto1Button,
    Button CaptureFoto2Button);

public static class VsaCodeExplorerPhotoCaptureButtonsRenderer
{
    public static void Apply(
        bool isCaptureRunning,
        VsaCodeExplorerPhotoCaptureButtonsRenderTargets targets)
    {
        var canCapture = !isCaptureRunning;
        targets.CaptureFoto1Button.IsEnabled = canCapture;
        targets.CaptureFoto2Button.IsEnabled = canCapture;
    }
}
