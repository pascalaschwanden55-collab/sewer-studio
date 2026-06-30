using System.Collections.ObjectModel;
using AuswertungPro.Next.Application.Ai.Training;

namespace AuswertungPro.Next.UI.Ai.Training;

public static class SelfTrainingCodeDistributionController
{
    public static CodeDistributionEntry Apply(
        ObservableCollection<CodeDistributionEntry> entries,
        string code,
        MatchLevel level)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var entry = entries.FirstOrDefault(e => e.Code == code);
        if (entry is null)
        {
            entry = new CodeDistributionEntry { Code = code };
            entries.Add(entry);
        }

        SelfTrainingStatusCalculator.ApplyMatch(entry, level);
        return entry;
    }
}
