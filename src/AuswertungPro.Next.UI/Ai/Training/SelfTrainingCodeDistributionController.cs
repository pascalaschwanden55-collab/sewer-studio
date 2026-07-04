using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Application.Ai.Training;

namespace AuswertungPro.Next.UI.Ai.Training;

public static class SelfTrainingCodeDistributionController
{
    public static void ApplyMatchOnUi(
        IList<CodeDistributionEntry> entries,
        string code,
        MatchLevel level,
        Action<Action> onUi)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(onUi);

        onUi(() => ApplyMatch(entries, code, level));
    }

    public static void ApplyMatch(
        IList<CodeDistributionEntry> entries,
        string code,
        MatchLevel level)
    {
        var entry = entries.FirstOrDefault(e => e.Code == code);
        if (entry is null)
        {
            entry = new CodeDistributionEntry { Code = code };
            entries.Add(entry);
        }

        SelfTrainingStatusCalculator.ApplyMatch(entry, level);
    }
}
