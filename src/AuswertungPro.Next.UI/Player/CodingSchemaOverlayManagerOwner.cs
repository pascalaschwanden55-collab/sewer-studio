using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Ai;

namespace AuswertungPro.Next.UI.Player;

public sealed class CodingSchemaOverlayManagerOwner
{
    private readonly SchemaOverlayManager _manager = new();

    public SchemaOverlayBase? Active => _manager.Active;

    public bool IsActive => _manager.IsActive;

    public bool IsDragging => _manager.IsDragging;

    public void Activate(SchemaOverlayBase schema, PipeCalibration? calibration = null)
        => _manager.Activate(schema, calibration);

    public void Place(NormalizedPoint clickPos)
    {
        _manager.Place(clickPos);
    }

    public string? HitTest(NormalizedPoint mousePos, double threshold = 0.025)
        => _manager.HitTest(mousePos, threshold);

    public void BeginDrag(string handleId)
    {
        _manager.BeginDrag(handleId);
    }

    public void UpdateDrag(NormalizedPoint mousePos)
    {
        _manager.UpdateDrag(mousePos);
    }

    public void EndDrag()
    {
        _manager.EndDrag();
    }

    public OverlayGeometry? Confirm()
        => _manager.Confirm();

    public void Cancel()
    {
        _manager.Cancel();
    }

    public void Reset()
    {
        _manager.Reset();
    }
}
