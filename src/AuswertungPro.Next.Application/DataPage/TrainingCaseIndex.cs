using System;
using System.Collections.Generic;

namespace AuswertungPro.Next.Application.DataPage;

/// <summary>
/// Haelt die normalisierten Training-Center-Haltungen und beantwortet Matches fuer die UI-Markierung.
/// </summary>
public sealed class TrainingCaseIndex
{
    private readonly HashSet<string> _trainedHaltungen = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlySet<string> TrainedHaltungen => _trainedHaltungen;

    public void ReplaceCaseIds(IEnumerable<string?> caseIds)
    {
        ArgumentNullException.ThrowIfNull(caseIds);

        _trainedHaltungen.Clear();
        foreach (var caseId in caseIds)
        {
            var name = TrainingCaseIdNormalizer.NormalizeCaseId(caseId);
            if (!string.IsNullOrWhiteSpace(name))
                _trainedHaltungen.Add(name);
        }
    }

    public bool IsTrainedCase(string? haltungsname)
    {
        if (string.IsNullOrWhiteSpace(haltungsname) || _trainedHaltungen.Count == 0)
            return false;

        if (_trainedHaltungen.Contains(haltungsname))
            return true;

        var stripped = TrainingCaseIdNormalizer.StripNodePrefixes(haltungsname);
        foreach (var trained in _trainedHaltungen)
        {
            if (string.Equals(TrainingCaseIdNormalizer.StripNodePrefixes(trained), stripped, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
