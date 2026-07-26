using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Ai.Coding;

public static class CodingUnappliedChangesClosePolicy
{
    public static bool ShouldClose(DialogConfirm result, Func<bool> applyChanges)
        => result switch
        {
            DialogConfirm.Cancel => false,
            DialogConfirm.Yes => applyChanges(),
            _ => true
        };
}
