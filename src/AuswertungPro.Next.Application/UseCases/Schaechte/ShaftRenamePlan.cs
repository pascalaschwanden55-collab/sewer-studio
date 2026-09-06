namespace AuswertungPro.Next.Application.UseCases.Schaechte;

/// <summary>Ein gemeinsamer, dateisystemfreier Plan fuer Umbenennungen und Verweise.</summary>
public sealed class ShaftRenamePlan
{
    public sealed record Move(string Source, string Destination, bool IsDirectory);
    private readonly List<Move> _moves = [];
    public IReadOnlyList<Move> Moves => _moves;

    public void AddFolder(
        string sourceFolder,
        string targetFolder,
        IEnumerable<string> files,
        IEnumerable<string> directories,
        IReadOnlyCollection<string> aliases,
        string newNumber)
    {
        var orderedAliases = aliases.OrderByDescending(a => a.Length).ToArray();
        foreach (var file in files)
            Add(file, Path.Combine(Path.GetDirectoryName(file)!, RenameFileName(Path.GetFileName(file), orderedAliases, newNumber)), false);

        foreach (var folder in directories.OrderByDescending(p => p.Length))
        {
            var name = Path.GetFileName(folder);
            if (orderedAliases.Contains(name, StringComparer.OrdinalIgnoreCase))
                Add(folder, Path.Combine(Path.GetDirectoryName(folder)!, newNumber), true);
        }
        Add(sourceFolder, targetFolder, true);
    }

    public string MapPath(string absolutePath)
    {
        var result = Path.GetFullPath(absolutePath);
        foreach (var move in _moves)
        {
            if (string.Equals(result, move.Source, StringComparison.OrdinalIgnoreCase))
                result = move.Destination;
            else if (move.IsDirectory && result.StartsWith(move.Source + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                result = move.Destination + result[move.Source.Length..];
        }
        return result;
    }

    private void Add(string source, string destination, bool isDirectory)
    {
        if (!string.Equals(source, destination, StringComparison.OrdinalIgnoreCase))
            _moves.Add(new(Path.GetFullPath(source), Path.GetFullPath(destination), isDirectory));
    }

    private static string RenameFileName(string name, IReadOnlyList<string> aliases, string newNumber)
    {
        foreach (var alias in aliases)
        {
            if (name.Contains(alias, StringComparison.OrdinalIgnoreCase))
                return name.Replace(alias, newNumber, StringComparison.OrdinalIgnoreCase);
        }

        // Kompatibel zu bereits belegten datumsbasierten Schacht-PDFs mit alter Nummer.
        if (Path.GetExtension(name).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            var stem = Path.GetFileNameWithoutExtension(name);
            var separator = stem.LastIndexOf('_');
            if (separator > 0 && separator < stem.Length - 1)
                return stem[..(separator + 1)] + newNumber + Path.GetExtension(name);
        }
        return name;
    }
}
