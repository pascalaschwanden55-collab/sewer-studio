namespace AuswertungPro.Next.UI.Ai.Coding;

public enum CodingConfirmationDecisionCommandOutcome
{
    Skipped,
    Applied
}

/// <summary>
/// Ergebnis der Entscheidungsanwendung inkl. Goldspeicherung. Nur bei
/// <see cref="PersistenceSucceeded"/> = true darf das Panel geschlossen und die Wiedergabe
/// fortgesetzt werden; sonst bleibt das Panel offen und zeigt den Fehler mit Retry an.
/// </summary>
public sealed record CodingConfirmationDecisionApplyOutcome(
    bool Applied,
    bool PersistenceSucceeded,
    string? PersistenceError)
{
    public static CodingConfirmationDecisionApplyOutcome Skipped { get; } = new(false, true, null);

    public static CodingConfirmationDecisionApplyOutcome Saved { get; } = new(true, true, null);

    public static CodingConfirmationDecisionApplyOutcome PersistenceFailed(string? error)
        => new(true, false, error);
}

public sealed record CodingConfirmationDecisionCommandActions(
    Func<Task<CodingConfirmationDecisionApplyOutcome>> ApplyDecision,
    Action CloseConfirmationPanel,
    Action ResumeAfterConfirmation,
    Action<string?> ShowPersistenceError);

public sealed record CodingConfirmationDecisionCommandResult(
    CodingConfirmationDecisionCommandOutcome Outcome)
{
    public bool Applied => Outcome == CodingConfirmationDecisionCommandOutcome.Applied;
}

public static class CodingConfirmationDecisionCommandWorkflow
{
    public static async Task<CodingConfirmationDecisionCommandResult> Execute(
        CodingConfirmationDecisionCommandActions actions)
    {
        ArgumentNullException.ThrowIfNull(actions);

        // Bewusst KEIN ConfigureAwait(false): Close/Resume/Fehleranzeige muessen auf dem
        // UI-Thread des Aufrufers weiterlaufen.
        var outcome = await actions.ApplyDecision();
        if (!outcome.PersistenceSucceeded)
        {
            // Panel bleibt offen: Fehler anzeigen, "Erneut speichern" anbieten,
            // weder schliessen noch fortsetzen.
            actions.ShowPersistenceError(outcome.PersistenceError);
            return new CodingConfirmationDecisionCommandResult(
                CodingConfirmationDecisionCommandOutcome.Applied);
        }

        actions.CloseConfirmationPanel();
        actions.ResumeAfterConfirmation();

        return new CodingConfirmationDecisionCommandResult(outcome.Applied
            ? CodingConfirmationDecisionCommandOutcome.Applied
            : CodingConfirmationDecisionCommandOutcome.Skipped);
    }
}
