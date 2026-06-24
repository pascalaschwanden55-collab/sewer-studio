using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingModeDefaultToolWorkflowTests
{
    [Fact]
    public void Execute_sets_rectangle_as_default_tool_and_updates_overlay_service_when_available()
    {
        var calls = new List<string>();

        CodingModeDefaultToolWorkflow.Execute(
            new CodingModeDefaultToolWorkflowRequest(HasOverlayService: true),
            Actions(
                setMarkToolType: tool => calls.Add($"mark:{tool}"),
                setToolLabels: label => calls.Add($"label:{label}"),
                setOverlayActiveTool: tool => calls.Add($"overlay:{tool}")));

        Assert.Equal(["mark:Rectangle", "label:Rechteck", "overlay:Rectangle"], calls);
    }

    [Fact]
    public void Execute_keeps_overlay_service_untouched_when_it_is_missing()
    {
        var calls = new List<string>();

        CodingModeDefaultToolWorkflow.Execute(
            new CodingModeDefaultToolWorkflowRequest(HasOverlayService: false),
            Actions(
                setMarkToolType: tool => calls.Add($"mark:{tool}"),
                setToolLabels: label => calls.Add($"label:{label}"),
                setOverlayActiveTool: tool => calls.Add($"overlay:{tool}")));

        Assert.Equal(["mark:Rectangle", "label:Rechteck"], calls);
    }

    private static CodingModeDefaultToolWorkflowActions Actions(
        Action<OverlayToolType>? setMarkToolType = null,
        Action<string>? setToolLabels = null,
        Action<OverlayToolType>? setOverlayActiveTool = null)
        => new(
            SetMarkToolType: setMarkToolType ?? (_ => { }),
            SetToolLabels: setToolLabels ?? (_ => { }),
            SetOverlayActiveTool: setOverlayActiveTool ?? (_ => { }));
}
