using System;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Ai.Coding;

public sealed class CodingApplyDialogService
{
    private readonly Func<string, string, bool> _confirmWarn;
    private readonly Func<string, string, DialogConfirm> _confirmCancel;

    public CodingApplyDialogService(
        Func<string, string, bool> confirmWarn,
        Func<string, string, DialogConfirm> confirmCancel)
    {
        _confirmWarn = confirmWarn;
        _confirmCancel = confirmCancel;
    }

    public bool ConfirmEmptyProtocol(CodingApplyEmptyProtocolGuardResult guard)
        => !guard.RequiresConfirmation || _confirmWarn(guard.Message, guard.Title);

    public bool ConfirmUnappliedChangesOnClose(Func<bool> applyChanges)
    {
        var result = _confirmCancel(
            "Es gibt noch nicht \u00fcbernommene Codierungen.\n\n" +
            "Ja = \u00fcbernehmen\nNein = verwerfen\nAbbrechen = Fenster offen lassen",
            "Codier-Modus");

        return CodingUnappliedChangesClosePolicy.ShouldClose(result, applyChanges);
    }
}
