using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Controls;

namespace AuswertungPro.Next.UI.Services;

public static class DropdownOptionList
{
    public static string ExtractText(object? value)
    {
        if (value is null)
            return string.Empty;
        if (value is string text)
            return text;
        if (value is ComboBox combo)
            return combo.Text ?? string.Empty;
        return value.ToString() ?? string.Empty;
    }

    public static bool AddIfMissing(ObservableCollection<string> options, string? value)
    {
        var text = Normalize(value);
        if (text.Length == 0)
            return false;

        if (options.Any(x => string.Equals(x, text, StringComparison.OrdinalIgnoreCase)))
            return false;

        options.Insert(0, text);
        return true;
    }

    public static bool Remove(ObservableCollection<string> options, string? value)
    {
        var text = Normalize(value);
        if (text.Length == 0)
            return false;

        var existing = options.FirstOrDefault(x => string.Equals(x, text, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
            return false;

        options.Remove(existing);
        return true;
    }

    public static void ReplaceWith(ObservableCollection<string> options, IEnumerable<string> values)
    {
        options.Clear();
        foreach (var value in values)
            options.Add(value);
    }

    public static bool EnsureExact(ObservableCollection<string> options, IReadOnlyList<string> required)
    {
        if (options.Count == required.Count)
        {
            var same = true;
            for (var i = 0; i < required.Count; i++)
            {
                if (!string.Equals(options[i], required[i], StringComparison.Ordinal))
                {
                    same = false;
                    break;
                }
            }

            if (same)
                return false;
        }

        ReplaceWith(options, required);
        return true;
    }

    private static string Normalize(string? value)
        => (value ?? string.Empty).Trim();
}
