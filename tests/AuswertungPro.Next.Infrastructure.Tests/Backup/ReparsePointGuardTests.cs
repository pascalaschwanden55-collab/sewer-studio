using System.IO;
using AuswertungPro.Next.Infrastructure.Backup;

namespace AuswertungPro.Next.Infrastructure.Tests.Backup;

/// <summary>
/// Logik des Junction-Schutzes. Faelle mit echten Verknuepfungen haengen von den
/// Rechten ab (CreateSymbolicLink braucht Admin oder Entwicklermodus) und enden
/// dann ohne Befund — die reinen Pfad-Faelle laufen immer.
/// </summary>
public sealed class ReparsePointGuardTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "sewerstudio-reparse-guard-" + Guid.NewGuid().ToString("N"));
    private readonly List<string> _createdLinks = new();

    public void Dispose()
    {
        foreach (var link in _createdLinks.OrderByDescending(path => path.Length))
        {
            try
            {
                if (Directory.Exists(link))
                    Directory.Delete(link);
            }
            catch
            {
                // Nur Testaufraeumen.
            }
        }

        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private void CreateDirectoryLink(string link, string target)
    {
        JunctionTestSupport.CreateDirectoryLink(link, target);
        _createdLinks.Add(link);
    }

    [Fact]
    public void IsReparsePoint_Normale_eintraege_sind_keine_verknuepfung()
    {
        var dir = Directory.CreateDirectory(Path.Combine(_root, "ordner"));
        var file = Path.Combine(_root, "datei.txt");
        File.WriteAllText(file, "inhalt");

        Assert.False(ReparsePointGuard.IsReparsePoint(dir.FullName));
        Assert.False(ReparsePointGuard.IsReparsePoint(file));
        Assert.False(ReparsePointGuard.IsReparsePoint(Path.Combine(_root, "fehlt")));
    }

    [Fact]
    public void HasReparsePointBelow_Normale_kette_ist_sauber()
    {
        var root = Path.Combine(_root, "backup");
        var deep = Path.Combine(root, "a", "b");
        Directory.CreateDirectory(deep);

        Assert.False(ReparsePointGuard.HasReparsePointBelow(root, Path.Combine(deep, "datei.txt")));
        Assert.False(ReparsePointGuard.HasReparsePointBelow(root, root));   // Root selbst wird nicht geprueft
    }

    [Fact]
    public void HasReparsePointBelow_Pfad_ausserhalb_des_roots_terminiert_ohne_befund()
    {
        var root = Path.Combine(_root, "backup");
        Directory.CreateDirectory(root);
        var outside = Path.Combine(_root, "daneben", "datei.txt");

        Assert.False(ReparsePointGuard.HasReparsePointBelow(root, outside));
    }

    [JunctionFact]
    public void HasReparsePointBelow_Verknuepfung_als_eintrag_und_als_elternordner_wird_erkannt()
    {
        var root = Path.Combine(_root, "backup");
        var foreign = Path.Combine(_root, "fremd");
        Directory.CreateDirectory(Path.Combine(root, "ebene"));
        Directory.CreateDirectory(foreign);

        var junction = Path.Combine(root, "ebene", "link");
        CreateDirectoryLink(junction, foreign);

        Assert.True(ReparsePointGuard.IsReparsePoint(junction));
        Assert.True(ReparsePointGuard.HasReparsePointBelow(root, junction));
        Assert.True(ReparsePointGuard.HasReparsePointBelow(root, Path.Combine(junction, "datei.txt")));
        // Der Root selbst zaehlt nicht als Befund, auch wenn der Pfad daneben sauber ist.
        Assert.False(ReparsePointGuard.HasReparsePointBelow(root, Path.Combine(root, "ebene", "normal.txt")));
    }
}
