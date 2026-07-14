using System.Windows;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void CodingCalibrate_Click(object sender, RoutedEventArgs e)
        => _codingManualCalibrationController.Toggle();

    private bool TryStartCodingCalibration(NormalizedPoint norm)
        => _codingCalibrationPointerController.Start(norm);

    private bool TryPreviewCodingCalibration(NormalizedPoint norm)
        => _codingCalibrationPointerController.Preview(norm);

    private bool TryFinishCodingCalibration(NormalizedPoint norm)
        => _codingCalibrationPointerController.Finish(norm);
}
