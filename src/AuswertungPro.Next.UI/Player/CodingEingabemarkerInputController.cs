using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Player;

public interface ICodingEingabemarkerInputController
{
    CodingEingabemarkerKeyInputWorkflowResult HandleKey(bool isEscape, bool isEnter);

    CodingEingabemarkerSelectionInputWorkflowResult HandleSelection(
        bool isPopupVisible,
        string? selectedText);
}

public sealed record CodingEingabemarkerInputControllerBindings(
    Action CancelMarker,
    Action ClearDetectionOverlays,
    Action Submit,
    Action<string> ApplyQuickSelection);

public sealed class CodingEingabemarkerInputController : ICodingEingabemarkerInputController
{
    private readonly CodingEingabemarkerInputControllerBindings _bindings;

    public CodingEingabemarkerInputController(
        CodingEingabemarkerInputControllerBindings bindings)
    {
        ArgumentNullException.ThrowIfNull(bindings);
        ArgumentNullException.ThrowIfNull(bindings.CancelMarker);
        ArgumentNullException.ThrowIfNull(bindings.ClearDetectionOverlays);
        ArgumentNullException.ThrowIfNull(bindings.Submit);
        ArgumentNullException.ThrowIfNull(bindings.ApplyQuickSelection);

        _bindings = bindings;
    }

    public CodingEingabemarkerKeyInputWorkflowResult HandleKey(bool isEscape, bool isEnter)
        => CodingEingabemarkerKeyInputWorkflow.Execute(
            new CodingEingabemarkerKeyInputWorkflowRequest(isEscape, isEnter),
            new CodingEingabemarkerKeyInputWorkflowActions(
                CancelMarker: _bindings.CancelMarker,
                ClearDetectionOverlays: _bindings.ClearDetectionOverlays,
                Submit: _bindings.Submit));

    public CodingEingabemarkerSelectionInputWorkflowResult HandleSelection(
        bool isPopupVisible,
        string? selectedText)
        => CodingEingabemarkerSelectionInputWorkflow.Execute(
            new CodingEingabemarkerSelectionInputWorkflowRequest(
                isPopupVisible,
                selectedText),
            new CodingEingabemarkerSelectionInputWorkflowActions(
                ApplyQuickSelection: _bindings.ApplyQuickSelection,
                Submit: _bindings.Submit));
}
