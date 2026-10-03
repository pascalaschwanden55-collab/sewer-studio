using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.ViewModels.Windows;

public partial class TrainingCenterViewModel
{
    /// <summary>
    /// PR #85: Waehrend eines Laufs (IsBusy) ist die Ordnerliste gesperrt. Sonst speichert z. B. der Scan
    /// eine inzwischen geaenderte Ordnerliste zu Faellen aus der alten. Die Momentaufnahme im
    /// Scan-Workflow bleibt als zweite Sicherung.
    /// </summary>
    private bool KannOrdnerAendern() => !IsBusy;

    private void UpdateRootFolderDisplay()
    {
        RootFolder = TrainingCenterDisplayFormatter.FormatRootFolders(_rootFolders);
    }
}
