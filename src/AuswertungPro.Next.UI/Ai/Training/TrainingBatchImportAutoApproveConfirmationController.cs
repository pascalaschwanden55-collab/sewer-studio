using AuswertungPro.Next.UI;

namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record TrainingBatchImportAutoApproveConfirmationResult(
    bool ShouldContinue,
    string? StatusText);

public static class TrainingBatchImportAutoApproveConfirmationController
{
    public static TrainingBatchImportAutoApproveConfirmationResult Confirm(
        IDialogService dialogs)
    {
        ArgumentNullException.ThrowIfNull(dialogs);

        return Confirm((message, title) => dialogs.ConfirmWarn(message, title));
    }

    public static TrainingBatchImportAutoApproveConfirmationResult Confirm(
        Func<string, string, bool> confirmWarn)
    {
        ArgumentNullException.ThrowIfNull(confirmWarn);

        var confirmed = confirmWarn(
            "Achtung: Der Batch-Import indexiert erkannte Samples OHNE manuelle Pr\u00fcfung direkt in die Knowledge Base (Auto-Approve).\n\n"
            + "Falsche Code-/Meter-Zuordnungen verschlechtern dauerhaft alle k\u00fcnftigen KI-Vorschl\u00e4ge. "
            + "F\u00fcr gepr\u00fcftes Lernen stattdessen 'Selbsttraining' mit der Review-Queue nutzen.\n\n"
            + "Trotzdem ungepr\u00fcft in die Knowledge Base lernen?",
            "Batch-Import + KB (ungepr\u00fcft)");

        return confirmed
            ? new TrainingBatchImportAutoApproveConfirmationResult(true, null)
            : new TrainingBatchImportAutoApproveConfirmationResult(false, "Batch-Import abgebrochen.");
    }
}
