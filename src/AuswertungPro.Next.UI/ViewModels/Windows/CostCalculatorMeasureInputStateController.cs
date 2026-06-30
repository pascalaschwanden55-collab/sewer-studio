namespace AuswertungPro.Next.UI.ViewModels.Windows;

public sealed class CostCalculatorMeasureInputStateController
{
    private bool _suppressDnTextChange;
    private bool _suppressLengthTextChange;
    private bool _suppressConnectionsTextChange;

    public void ApplyDnText(string value, Action<string> setText)
        => ApplySuppressed(ref _suppressDnTextChange, value, setText);

    public void ApplyLengthText(string value, Action<string> setText)
        => ApplySuppressed(ref _suppressLengthTextChange, value, setText);

    public void ApplyConnectionsText(string value, Action<string> setText)
        => ApplySuppressed(ref _suppressConnectionsTextChange, value, setText);

    public bool ShouldHandleDnTextChange()
        => !_suppressDnTextChange;

    public bool ShouldHandleLengthTextChange()
        => !_suppressLengthTextChange;

    public bool ShouldHandleConnectionsTextChange()
        => !_suppressConnectionsTextChange;

    private static void ApplySuppressed(
        ref bool suppressionFlag,
        string value,
        Action<string> setText)
    {
        ArgumentNullException.ThrowIfNull(setText);

        suppressionFlag = true;
        try
        {
            setText(value);
        }
        finally
        {
            suppressionFlag = false;
        }
    }
}
