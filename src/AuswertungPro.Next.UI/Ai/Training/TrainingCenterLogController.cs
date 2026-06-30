using System;
using System.Collections.Generic;

namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record TrainingCenterLogLine(string EntryText, string LogTextAppend);

public static class TrainingCenterLogController
{
    public static TrainingCenterLogLine CreateLine(DateTime now, string message)
    {
        var entryText = $"[{now:HH:mm:ss}] {message}";
        return new TrainingCenterLogLine(entryText, entryText + "\n");
    }

    public static void AppendCapped(IList<string> entries, string entryText, int maxEntries = 100)
    {
        ArgumentNullException.ThrowIfNull(entries);

        entries.Add(entryText);
        while (entries.Count > maxEntries)
            entries.RemoveAt(0);
    }
}
