using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.UI.Ai;

public sealed record CodingApplyEmptyProtocolGuardResult(
    bool RequiresConfirmation,
    string Message,
    string Title);

public static class CodingApplyEmptyProtocolGuard
{
    public static CodingApplyEmptyProtocolGuardResult Build(
        int codingEventEntryCount,
        IEnumerable<ProtocolEntry> existingEntries)
    {
        if (codingEventEntryCount > 0)
            return NoConfirmation;

        var activeFindingCount = existingEntries.Count(
            entry => !entry.IsDeleted && !string.IsNullOrWhiteSpace(entry.Code));
        if (activeFindingCount == 0)
            return NoConfirmation;

        return new CodingApplyEmptyProtocolGuardResult(
            RequiresConfirmation: true,
            $"Die Befundliste ist leer.\n\n\"\u00dcbernehmen\" w\u00fcrde {activeFindingCount} bestehende(n) Befund(e) dieser Haltung l\u00f6schen und die prim\u00e4ren Sch\u00e4den leeren.\n\nWirklich eine leere Codierung \u00fcbernehmen?",
            "Leere Codierung \u00fcbernehmen?");
    }

    private static CodingApplyEmptyProtocolGuardResult NoConfirmation { get; } =
        new(false, string.Empty, string.Empty);
}
