using AuswertungPro.Next.Application.Ai.Evaluation;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingProtocolMatchControllerTests
{
    [Fact]
    public void SeekSelectedImportEvent_uses_current_selection_and_meter_navigation()
    {
        var calls = new List<string>();
        var selected = new CodingEvent
        {
            Entry = new ProtocolEntry(),
            VideoTimestamp = TimeSpan.Zero,
            MeterAtCapture = 12.3
        };
        var controller = new CodingProtocolMatchController(
            Bindings(
                calls,
                selectedItem: selected,
                hasCodingSessionService: true));

        var result = controller.SeekSelectedImportEvent();

        Assert.True(result.Completed);
        Assert.Equal(["move:12.3", "pending", "sync"], calls);
    }

    [Fact]
    public void RunMatch_preserves_match_store_summary_refresh_and_highlight_order()
    {
        var calls = new List<string>();
        var routing = Routing();
        var controller = new CodingProtocolMatchController(
            Bindings(
                calls,
                hasCodingViewModel: true,
                runMatch: () =>
                {
                    calls.Add("run");
                    return routing;
                }));

        var result = controller.RunMatch();

        Assert.True(result.Completed);
        Assert.Same(routing, result.Routing);
        Assert.Equal(["run", "store", "summary", "refresh", "highlights"], calls);
    }

    private static CodingProtocolMatchControllerBindings Bindings(
        List<string> calls,
        object? selectedItem = null,
        bool hasCodingSessionService = false,
        bool hasCodingViewModel = false,
        Func<CodingMatchRouting>? runMatch = null)
        => new(
            ResolveSelectedImportEvent: () => selectedItem,
            HasCodingSessionService: () => hasCodingSessionService,
            SeekMilliseconds: milliseconds => calls.Add($"seek:{milliseconds}"),
            MoveToMeter: meter => calls.Add($"move:{meter:F1}"),
            MarkNavigationPending: () => calls.Add("pending"),
            SyncVideoToCodingMeter: () => calls.Add("sync"),
            HasCodingViewModel: () => hasCodingViewModel,
            RunMatch: runMatch ?? Routing,
            StoreMatch: _ => calls.Add("store"),
            ApplySummary: _ => calls.Add("summary"),
            RefreshEvents: () => calls.Add("refresh"),
            ScheduleHighlights: () => calls.Add("highlights"));

    private static CodingMatchRouting Routing()
        => new(
            new BefundMatchResult(),
            Trainingskandidaten: [],
            ReviewGelb: [],
            FalscherCodeReview: [],
            Verpasst: [],
            Fehlalarm: []);
}
