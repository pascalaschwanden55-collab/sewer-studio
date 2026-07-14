using System.Threading.Tasks;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    /// <summary>Rueckgabe: true wenn gespeichert, false wenn abgebrochen.</summary>
    private async Task<bool> SaveMarkAsTrainingAsync(OverlayGeometry overlay, double timestampSec, string? clockPosition, byte[]? preCapturedFrame = null)
        => (await _liveDetectionManualMarkTrainingController.SaveAsync(
            overlay,
            timestampSec,
            clockPosition,
            preCapturedFrame)).ReturnValue;
}
