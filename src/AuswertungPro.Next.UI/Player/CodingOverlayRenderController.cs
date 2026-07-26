using System.Windows.Media;
using System.Windows.Media.Effects;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Player;

public sealed class CodingOverlayRenderController
{
    private readonly IOverlaySurface _surface;
    private readonly IOverlayCoordinateMapper _mapper;

    public CodingOverlayRenderController(
        IOverlaySurface surface,
        IOverlayCoordinateMapper mapper)
    {
        _surface = surface ?? throw new ArgumentNullException(nameof(surface));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    }

    public void ClearTransient(bool clearManualOverlay)
        => _surface.ClearTransient(clearManualOverlay);

    public bool RenderOverlayGeometry(
        OverlayGeometry overlay,
        bool isPreview,
        NormalizedPoint? labelAnchor,
        PipeCalibration? calibration)
        => CodingOverlayGeometryRenderer.Render(
            _surface.Canvas,
            overlay,
            isPreview,
            labelAnchor,
            _mapper.ToPixel,
            calibration,
            _surface.Width,
            _surface.Height);

    public int RenderAiOverlays(
        IEnumerable<CodingEvent> events,
        PipeCalibration? calibration)
        => CodingAiOverlayRenderer.Render(
            _surface.Canvas,
            events,
            _surface.Width,
            _surface.Height,
            calibration?.PipeCenter ?? new NormalizedPoint(0.5, 0.5),
            _mapper.ToPixel);

    public void RenderReferenceDn(
        PipeCalibration? calibration,
        bool showReferenceDn)
        => ReferenceDnOverlayRenderer.Render(
            _surface.Canvas,
            calibration,
            showReferenceDn,
            _surface.Width,
            _surface.Height);

    public CodingCalibrationPreviewState RenderCalibrationPreview(
        NormalizedPoint start,
        NormalizedPoint end)
    {
        var preview = CodingCalibrationPreviewPolicy.Build(
            _mapper.ToPixel(start),
            _mapper.ToPixel(end));

        CodingCalibrationPreviewLineRenderer.Render(_surface.Canvas, preview);
        return preview;
    }

    public bool RenderActiveSchema(
        SchemaOverlayBase active,
        OverlayGeometry? overlay)
    {
        ArgumentNullException.ThrowIfNull(active);

        var glowEffect = CreateActiveSchemaGlowEffect();
        return active switch
        {
            PipeBendSchema bend => CodingActivePipeBendSchemaRenderer.Render(
                _surface.Canvas,
                bend,
                overlay,
                glowEffect,
                _mapper.ToPixel),
            FillLevelSchema fill => CodingActiveFillLevelSchemaRenderer.Render(
                _surface.Canvas,
                fill,
                overlay,
                glowEffect,
                _mapper.ToPixel,
                _surface.Width,
                _surface.Height),
            IntrusionSchema intrusion => CodingActiveIntrusionSchemaRenderer.Render(
                _surface.Canvas,
                intrusion,
                overlay,
                glowEffect,
                _mapper.ToPixel,
                _surface.Width,
                _surface.Height),
            _ => false
        };
    }

    private static DropShadowEffect CreateActiveSchemaGlowEffect()
        => new()
        {
            Color = Colors.Black,
            BlurRadius = 8,
            ShadowDepth = 0,
            Opacity = 0.95
        };
}
