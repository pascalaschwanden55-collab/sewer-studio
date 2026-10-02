using AuswertungPro.Next.Infrastructure.Costs;

namespace AuswertungPro.Next.Infrastructure.Tests.Costs;

/// <summary>Gemeinsames Entfernen der Benutzer-Overrides von Kostenkatalog und Massnahmenvorlagen (B6, Deepscan 02.10.2026).</summary>
public sealed class CostStoreFileProbeTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "sewer-probe-" + Guid.NewGuid().ToString("N"));

    public CostStoreFileProbeTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); }
        catch (IOException) { /* Aufraeumen im Temp-Ordner des Tests; ein gesperrter Rest stoert das Ergebnis nicht */ }
    }

    [Fact]
    public void Vorhandene_Datei_wird_entfernt()
    {
        var path = Path.Combine(_root, "override.json");
        File.WriteAllText(path, "{}");

        Assert.True(CostStoreFileProbe.TryRemove(path, out var error), error);
        Assert.False(File.Exists(path));
        Assert.Equal("", error);
    }

    [Fact]
    public void Fehlende_Datei_gilt_als_entfernt()
    {
        Assert.True(CostStoreFileProbe.TryRemove(Path.Combine(_root, "gibt-es-nicht.json"), out var error), error);
    }

    [Fact]
    public void Ordner_am_Dateipfad_wird_nicht_angefasst_und_gemeldet()
    {
        var path = Path.Combine(_root, "override.json");
        Directory.CreateDirectory(path);

        Assert.False(CostStoreFileProbe.TryRemove(path, out var error));
        Assert.Contains("Ordner", error);
        Assert.True(Directory.Exists(path));
    }
}
