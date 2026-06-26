using System.Windows;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Player;

public enum CodingEingabemarkerPhase
{
    Inactive,
    Drawing,
    Input,
    Analyzing
}

public sealed class CodingEingabemarkerStateController
{
    public CodingEingabemarkerPhase Phase { get; private set; } = CodingEingabemarkerPhase.Inactive;

    public Point DragStart { get; private set; }

    public Rect NormalizedSelection { get; private set; }

    public System.Windows.Shapes.Rectangle? PreviewRect { get; private set; }

    public bool IsDrawing => Phase == CodingEingabemarkerPhase.Drawing;

    public bool HasPreview => PreviewRect != null;

    public CodingOverlayInputEingabemarkerState OverlayInputState => Phase switch
    {
        CodingEingabemarkerPhase.Drawing => CodingOverlayInputEingabemarkerState.Drawing,
        CodingEingabemarkerPhase.Input or CodingEingabemarkerPhase.Analyzing => CodingOverlayInputEingabemarkerState.InputBlocked,
        _ => CodingOverlayInputEingabemarkerState.Inactive
    };

    public void SetDrawingPhase()
        => Phase = CodingEingabemarkerPhase.Drawing;

    public void SetInactivePhase()
        => Phase = CodingEingabemarkerPhase.Inactive;

    public void SetInputPhase()
        => Phase = CodingEingabemarkerPhase.Input;

    public void SetAnalyzingPhase()
        => Phase = CodingEingabemarkerPhase.Analyzing;

    public void StoreDragStart(Point point)
        => DragStart = point;

    public void StoreNormalizedSelection(Rect rect)
        => NormalizedSelection = rect;

    public void SetPreview(System.Windows.Shapes.Rectangle? previewRect)
        => PreviewRect = previewRect;

    public void ClearPreview()
        => PreviewRect = null;
}
