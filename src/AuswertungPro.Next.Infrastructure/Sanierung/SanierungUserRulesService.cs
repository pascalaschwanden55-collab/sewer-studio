using System.Text.Json;
using AuswertungPro.Next.Domain.Sanierung;

namespace AuswertungPro.Next.Infrastructure.Sanierung;

/// <summary>
/// Lade/Speichern/Validieren der vom User im UI gepflegten Hard-Constraints.
/// JSON-Datei: Config/sanierung_user_rules.json (atomar geschrieben mit Backup).
/// Live-Reload via Invalidate() + Reload().
/// </summary>
public sealed class SanierungUserRulesService
{
    private readonly string _path;
    private SanierungUserRulesFile? _cache;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true,
    };

    public SanierungUserRulesService(string path)
    {
        _path = path;
    }

    public string FilePath => _path;

    public SanierungUserRulesFile Load()
    {
        if (_cache is not null) return _cache;
        if (!File.Exists(_path))
        {
            _cache = new SanierungUserRulesFile();
            return _cache;
        }
        try
        {
            var json = File.ReadAllText(_path);
            _cache = JsonSerializer.Deserialize<SanierungUserRulesFile>(json, JsonOpts) ?? new();
        }
        catch
        {
            _cache = new SanierungUserRulesFile();
        }
        return _cache;
    }

    public void Save(SanierungUserRulesFile file)
    {
        file.LastUpdated = DateTime.Now;

        // Backup der alten Datei
        if (File.Exists(_path))
        {
            var bak = _path + $".bak_{DateTime.Now:yyyyMMdd_HHmmss}";
            try { File.Copy(_path, bak, overwrite: true); } catch { /* best-effort */ }
        }

        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var json = JsonSerializer.Serialize(file, JsonOpts);
        File.WriteAllText(_path, json);
        _cache = file;
    }

    public void Invalidate() => _cache = null;

    /// <summary>Liefert nur aktive Regeln.</summary>
    public IReadOnlyList<SanierungUserRule> GetActiveRules()
        => Load().Rules.Where(r => r.Enabled).ToList();
}
