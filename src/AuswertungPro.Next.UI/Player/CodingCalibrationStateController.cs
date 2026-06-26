using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Player;

public sealed class CodingCalibrationStateController
{
    public bool IsCalibrating { get; private set; }

    public NormalizedPoint? Start { get; private set; }

    public void SetCalibrating(bool isCalibrating)
        => IsCalibrating = isCalibrating;

    public void SetStart(NormalizedPoint start)
        => Start = start;

    public void ClearStart()
        => Start = null;

    public void Reset()
    {
        IsCalibrating = false;
        Start = null;
    }
}
