using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingBatchImportAutoApproveConfirmationControllerTests
{
    [Fact]
    public void Confirm_liefert_continue_wenn_dialog_bestaetigt()
    {
        string? message = null;
        string? title = null;

        var result = TrainingBatchImportAutoApproveConfirmationController.Confirm((m, t) =>
        {
            message = m;
            title = t;
            return true;
        });

        Assert.True(result.ShouldContinue);
        Assert.Null(result.StatusText);
        Assert.Contains("Auto-Approve", message);
        Assert.Equal("Batch-Import + KB (ungepr\u00fcft)", title);
    }

    [Fact]
    public void Confirm_liefert_abbruchstatus_wenn_dialog_abgelehnt()
    {
        var result = TrainingBatchImportAutoApproveConfirmationController.Confirm((_, _) => false);

        Assert.False(result.ShouldContinue);
        Assert.Equal("Batch-Import abgebrochen.", result.StatusText);
    }
}
