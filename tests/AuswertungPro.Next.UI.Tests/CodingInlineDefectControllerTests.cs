using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai.Coding;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingInlineDefectControllerTests
{
    [Fact]
    public void Accept_uses_defect_selected_by_accept_command_and_preserves_follow_up_order()
    {
        var calls = new List<string>();
        CodingEvent? selectedDefect = null;
        var acceptedDefect = Event();
        var controller = new CodingInlineDefectController(
            Bindings(
                calls,
                resolveSelectedDefect: () => selectedDefect,
                executeAcceptDefect: () =>
                {
                    calls.Add("accept");
                    selectedDefect = acceptedDefect;
                }));

        var result = controller.Accept();

        Assert.True(result.Completed);
        Assert.Equal(["accept", "persist", "detail", "refresh", "fade"], calls);
    }

    [Fact]
    public void Edit_uses_list_fallback_and_preserves_edit_completion_order()
    {
        var calls = new List<string>();
        var selectedListEvent = Event();
        selectedListEvent.AiContext = new CodingEventAiContext();
        selectedListEvent.Entry.MeterStart = 4.2;
        selectedListEvent.Entry.Zeit = TimeSpan.FromSeconds(12);
        var controller = new CodingInlineDefectController(
            Bindings(
                calls,
                hasCodingViewModel: true,
                resolveSelectedListEvent: () => selectedListEvent,
                tryEdit: codingEvent =>
                {
                    Assert.Same(selectedListEvent, codingEvent);
                    calls.Add("try");
                    return true;
                }));

        var result = controller.Edit();

        Assert.True(result.Completed);
        Assert.Equal(4.2, selectedListEvent.MeterAtCapture);
        Assert.Equal(TimeSpan.FromSeconds(12), selectedListEvent.VideoTimestamp);
        Assert.Equal(["select", "pause", "try", "edit", "persist", "refresh", "detail"], calls);
    }

    [Fact]
    public async Task AcceptAsync_zeigt_erst_nach_bestaetigter_Persistenz_Erfolg()
    {
        var calls = new List<string>();
        var acceptedDefect = Event();
        var completion = new TaskCompletionSource<CodingTrainingSamplePersistenceResult>();
        var controller = new CodingInlineDefectController(
            Bindings(
                calls,
                resolveSelectedDefect: () => acceptedDefect,
                persistAcceptedAsync: _ =>
                {
                    calls.Add("persist-start");
                    return completion.Task;
                }));

        var pending = controller.AcceptAsync();

        Assert.Equal(["accept", "persist-start"], calls);
        completion.SetResult(CodingTrainingSamplePersistenceResult.Ok);
        var result = await pending;

        Assert.True(result.Completed);
        Assert.Equal(["accept", "persist-start", "detail", "refresh", "fade"], calls);
    }

    [Fact]
    public async Task EditAsync_Persistenzfehler_wird_sichtbar_und_nicht_als_Erfolg_behandelt()
    {
        var calls = new List<string>();
        var selected = Event();
        selected.AiContext = new CodingEventAiContext();
        var controller = new CodingInlineDefectController(
            Bindings(
                calls,
                hasCodingViewModel: true,
                resolveSelectedListEvent: () => selected,
                tryEdit: _ =>
                {
                    calls.Add("try");
                    return true;
                },
                persistEditedAsync: _ => Task.FromResult(
                    CodingTrainingSamplePersistenceResult.Failed("JSON gesperrt"))));

        var result = await controller.EditAsync();

        Assert.Equal(CodingInlineDefectEditCommandWorkflowOutcome.PersistenceFailed, result.Outcome);
        Assert.Contains("JSON gesperrt", result.Error);
        Assert.Contains("error:JSON gesperrt", calls);
        Assert.DoesNotContain("refresh", calls);
        Assert.DoesNotContain("detail", calls);
    }

    [Fact]
    public void Reject_removes_selected_defect_and_clears_detail_in_existing_order()
    {
        var calls = new List<string>();
        var selectedDefect = Event();
        var events = new List<CodingEvent> { selectedDefect };
        var controller = new CodingInlineDefectController(
            Bindings(
                calls,
                resolveSelectedDefect: () => selectedDefect,
                resolveEventCollection: () => events));

        var result = controller.Reject();

        Assert.True(result.Completed);
        Assert.Empty(events);
        Assert.Equal(["clear", "hide", "refresh", "fade"], calls);
    }

    private static CodingInlineDefectControllerBindings Bindings(
        List<string> calls,
        bool hasCodingViewModel = false,
        Func<CodingEvent?>? resolveSelectedDefect = null,
        Func<CodingEvent?>? resolveSelectedListEvent = null,
        Action? executeAcceptDefect = null,
        Func<CodingEvent, bool>? tryEdit = null,
        Func<ICollection<CodingEvent>?>? resolveEventCollection = null,
        Func<CodingEvent, Task<CodingTrainingSamplePersistenceResult>>? persistAcceptedAsync = null,
        Func<CodingEvent, Task<CodingTrainingSamplePersistenceResult>>? persistEditedAsync = null)
        => new(
            HasCodingViewModel: () => hasCodingViewModel,
            ResolveSelectedDefect: resolveSelectedDefect ?? (() => null),
            ResolveSelectedListEvent: resolveSelectedListEvent ?? (() => null),
            ExecuteAcceptDefect: executeAcceptDefect ?? (() => calls.Add("accept")),
            SelectDefect: _ => calls.Add("select"),
            PausePlayback: () => calls.Add("pause"),
            TryEdit: tryEdit ?? (_ => false),
            ResolveCodingSessionService: () => null,
            ExecuteEditDefect: () => calls.Add("edit"),
            ResolveEventCollection: resolveEventCollection ?? (() => null),
            ClearSelectedDefect: () => calls.Add("clear"),
            PersistAcceptedTrainingSample: _ => calls.Add("persist"),
            PersistEditedTrainingSample: _ => calls.Add("persist"),
            UpdateInlineDefectDetail: _ => calls.Add("detail"),
            HideInlineDefectDetail: () => calls.Add("hide"),
            RefreshEvents: () => calls.Add("refresh"),
            FadeOutAiOverlayAfterAction: () => calls.Add("fade"),
            PersistAcceptedTrainingSampleAsync: persistAcceptedAsync,
            PersistEditedTrainingSampleAsync: persistEditedAsync,
            ShowPersistenceError: error => calls.Add($"error:{error}"));

    private static CodingEvent Event()
        => new()
        {
            Entry = new ProtocolEntry(),
            VideoTimestamp = TimeSpan.Zero
        };
}
