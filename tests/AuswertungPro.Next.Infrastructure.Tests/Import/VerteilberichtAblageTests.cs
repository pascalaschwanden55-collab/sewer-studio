using AuswertungPro.Next.Infrastructure.Import;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

public sealed class VerteilberichtAblageTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"verteilbericht-{Guid.NewGuid():N}");

    [Fact]
    public void Schreibt_neben_die_Importberichte_und_ueberschreibt_nie()
    {
        Directory.CreateDirectory(_root);
        var zeit = new DateTime(2026, 9, 28, 10, 15, 0);
        var ablage = new VerteilberichtAblage(() => zeit);

        var erster = ablage.Schreibe(_root, "Schächte", "Zeile A");
        var zweiter = ablage.Schreibe(_root, "Schächte", "Zeile B");

        Assert.NotNull(erster);
        Assert.NotNull(zweiter);
        Assert.NotEqual(erster, zweiter);
        Assert.Equal(Path.Combine(_root, "__IMPORT_REPORTS"), Path.GetDirectoryName(erster));
        Assert.Equal("verteilung_Schächte_20260928_101500.txt", Path.GetFileName(erster));
        Assert.Contains("Zeile A", File.ReadAllText(erster!));
        Assert.Contains("Zeile B", File.ReadAllText(zweiter!));
    }

    [Fact]
    public void Ohne_Projektordner_entsteht_nichts()
    {
        var ablage = new VerteilberichtAblage();

        Assert.Null(ablage.Schreibe(Path.Combine(_root, "fehlt"), "Haltungen", "x"));
        Assert.False(Directory.Exists(_root));
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
        catch (IOException) { }
    }
}
