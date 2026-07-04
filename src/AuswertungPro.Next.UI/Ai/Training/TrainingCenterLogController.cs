using System;
using System.Collections.Generic;

namespace AuswertungPro.Next.UI.Ai.Training;

public static class TrainingCenterLogController
{
    public const int MaxSelfTrainingLogEntries = 100;

    public static string FormatEntry(string message, DateTime timestamp)
        => $"[{timestamp:HH:mm:ss}] {message}";

    public static string AppendLogText(string currentText, string entryText)
        => currentText + entryText + "\n";

    public static void AppendSelfTrainingEntry(
        IList<string> entries,
        string entryText,
        int maxEntries = MaxSelfTrainingLogEntries)
    {
        entries.Add(entryText);
        while (entries.Count > maxEntries)
            entries.RemoveAt(0);
    }

    public static void AppendSelfTrainingLog(
        string message,
        DateTime timestamp,
        Action<Action> onUi,
        IList<string> entries)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(onUi);
        ArgumentNullException.ThrowIfNull(entries);

        var entryText = FormatEntry(message, timestamp);
        onUi(() => AppendSelfTrainingEntry(entries, entryText));
    }

    public static void AppendSelfTrainingLog(
        string message,
        Action<Action> onUi,
        IList<string> entries)
        => AppendSelfTrainingLog(message, DateTime.Now, onUi, entries);

    public static void AppendLog(
        string message,
        DateTime timestamp,
        Action<Action> onUi,
        Func<string> getLogText,
        Action<string> setLogText,
        IList<string> entries)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(onUi);
        ArgumentNullException.ThrowIfNull(getLogText);
        ArgumentNullException.ThrowIfNull(setLogText);
        ArgumentNullException.ThrowIfNull(entries);

        var entryText = FormatEntry(message, timestamp);
        onUi(() =>
        {
            setLogText(AppendLogText(getLogText(), entryText));
            AppendSelfTrainingEntry(entries, entryText);
        });
    }

    public static void AppendLog(
        string message,
        Action<Action> onUi,
        Func<string> getLogText,
        Action<string> setLogText,
        IList<string> entries)
        => AppendLog(message, DateTime.Now, onUi, getLogText, setLogText, entries);
}
