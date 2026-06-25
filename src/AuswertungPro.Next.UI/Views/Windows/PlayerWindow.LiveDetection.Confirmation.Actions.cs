using System.Windows;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void DetectionSkip_Click(object sender, RoutedEventArgs e)
    {
        LiveDetectionConfirmationSkipCommandWorkflow.Execute(
            new LiveDetectionConfirmationSkipCommandActions(
                ResumeDetection: ResumeDetection));
    }
}
