using System.Windows;
using AuswertungPro.Next.UI.Helpers;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void DetectionAccept_Click(object sender, RoutedEventArgs e)
        => _liveDetectionConfirmationTrainingController.AcceptAsync()
            .SafeFireAndForget("DetectionAccept");

    private void DetectionCorrect_Click(object sender, RoutedEventArgs e)
        => _liveDetectionConfirmationTrainingController.CorrectAsync()
            .SafeFireAndForget("DetectionCorrect");
}
