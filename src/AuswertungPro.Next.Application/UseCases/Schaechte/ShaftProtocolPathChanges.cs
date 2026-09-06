using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Application.UseCases.Schaechte;

/// <summary>Zieht Medienverweise aller Protokollstaende nach demselben Dateiplan nach.</summary>
public sealed class ShaftProtocolPathChanges
{
    private sealed record Change(Action<string> Write, string Before, string After);
    private readonly List<Change> _changes = [];

    public static ShaftProtocolPathChanges Prepare(ProtocolDocument? document, Func<string, string> map)
    {
        var result = new ShaftProtocolPathChanges();
        if (document is null)
            return result;

        foreach (var revision in new[] { document.Original, document.Current }.Concat(document.History))
        {
            result.AddList(revision.ImportVideoPaths, map);
            foreach (var entry in revision.Entries)
            {
                result.AddList(entry.FotoPaths, map);
                result.AddList(entry.OriginalFotoPaths, map);
                if (!string.IsNullOrWhiteSpace(entry.Mpeg))
                    result.Add(entry.Mpeg, value => entry.Mpeg = value, map);
            }
        }
        return result;
    }

    public void Apply()
    {
        foreach (var change in _changes)
            change.Write(change.After);
    }

    public void Restore()
    {
        foreach (var change in _changes)
            change.Write(change.Before);
    }

    private void AddList(List<string>? paths, Func<string, string> map)
    {
        if (paths is null)
            return;
        for (var i = 0; i < paths.Count; i++)
        {
            var index = i;
            Add(paths[index], value => paths[index] = value, map);
        }
    }

    private void Add(string before, Action<string> write, Func<string, string> map)
    {
        var after = map(before);
        if (!string.Equals(before, after, StringComparison.Ordinal))
            _changes.Add(new(write, before, after));
    }
}
