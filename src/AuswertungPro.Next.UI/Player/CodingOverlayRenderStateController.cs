namespace AuswertungPro.Next.UI.Player;

public sealed class CodingOverlayRenderStateController
{
    public double VideoAspect { get; private set; }

    public bool ShowReferenceDn { get; private set; }

    public void SetVideoAspect(double aspect)
        => VideoAspect = aspect;

    public void ShowReferenceDiameter()
        => ShowReferenceDn = true;
}
