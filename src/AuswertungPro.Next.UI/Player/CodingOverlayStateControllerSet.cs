namespace AuswertungPro.Next.UI.Player;

public sealed class CodingOverlayStateControllerSet
{
    public CodingCalibrationStateController CalibrationState { get; } = new();

    public CodingOverlayInputVisibilityStateController InputVisibilityState { get; } = new();

    public CodingOverlayRenderStateController RenderState { get; } = new();

    public CodingActiveToolNameStateController ActiveToolNameState { get; } = new();
}
