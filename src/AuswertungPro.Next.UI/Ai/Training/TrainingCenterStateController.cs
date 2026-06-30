using System;
using System.Collections.Generic;
using System.Linq;

namespace AuswertungPro.Next.UI.Ai.Training;

public static class TrainingCenterStateController
{
    public static IReadOnlyList<string> RestoreExistingRootFolders(
        TrainingCenterState state,
        Func<string, bool> directoryExists)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(directoryExists);

        var restored = new List<string>();
        foreach (var folder in state.RootFolders)
        {
            if (directoryExists(folder))
                restored.Add(folder);
        }

        return restored;
    }

    public static bool AddSelectedRootFolders(
        IList<string> rootFolders,
        IEnumerable<string> selectedFolders)
    {
        ArgumentNullException.ThrowIfNull(rootFolders);
        ArgumentNullException.ThrowIfNull(selectedFolders);

        var changed = false;
        foreach (var folder in selectedFolders)
        {
            if (AddRootFolder(rootFolders, folder))
                changed = true;
        }

        return changed;
    }

    public static void ReplaceRootFolders(
        IList<string> rootFolders,
        IEnumerable<string> replacement)
    {
        ArgumentNullException.ThrowIfNull(rootFolders);
        ArgumentNullException.ThrowIfNull(replacement);

        rootFolders.Clear();
        foreach (var folder in replacement)
            rootFolders.Add(folder);
    }

    public static bool AddRootFolder(
        IList<string> rootFolders,
        string folder)
    {
        ArgumentNullException.ThrowIfNull(rootFolders);
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);

        if (rootFolders.Any(existing => string.Equals(existing, folder, StringComparison.OrdinalIgnoreCase)))
            return false;

        rootFolders.Add(folder);
        return true;
    }

    public static TrainingCenterState BuildState(
        IEnumerable<TrainingCase> cases,
        IEnumerable<string> rootFolders,
        DateTime updatedUtc)
    {
        ArgumentNullException.ThrowIfNull(cases);
        ArgumentNullException.ThrowIfNull(rootFolders);

        return new TrainingCenterState
        {
            Cases = cases.ToList(),
            RootFolders = rootFolders.ToList(),
            UpdatedUtc = updatedUtc
        };
    }
}
