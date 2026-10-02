using AuswertungPro.Next.Infrastructure.Ai.Training;
using AuswertungPro.Next.Infrastructure.Tests.Backup;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Ai.Training;

/// <summary>
/// Gold-Speicher auf dem gemeinsamen VerknuepfungsSchutz (Deepscan 02.10.2026, A5), Regel
/// Streng: Wurzel eingeschlossen, jedes Glied muss vorhanden und lesbar sein. Ausnahmetyp
/// und Meldung bleiben wie vor der Umstellung.
/// </summary>
public sealed class PersonalGoldBrainFileServiceVerknuepfungTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "gold_verknuepfung_" + Guid.NewGuid().ToString("N"));

    public PersonalGoldBrainFileServiceVerknuepfungTests() => Directory.CreateDirectory(_root);

    [JunctionFact]
    public void EnsureNoReparsePoint_sperrt_eine_Junction_in_der_Kette()
    {
        var ziel = Directory.CreateDirectory(Path.Combine(_root, "ziel")).FullName;
        var link = Path.Combine(_root, "link");
        JunctionTestSupport.CreateDirectoryLink(link, ziel);
        File.WriteAllText(Path.Combine(ziel, "frame.png"), "x");

        var fehler = Assert.Throws<InvalidDataException>(() =>
            PersonalGoldBrainFileService.EnsureNoReparsePoint(Path.Combine(link, "frame.png"), _root));

        Assert.Contains("Verknüpfung im geschützten Pfad", fehler.Message, StringComparison.Ordinal);
        Assert.Contains(link, fehler.Message, StringComparison.OrdinalIgnoreCase);
    }

    [JunctionFact]
    public async Task CopyTreeVerifiedAsync_sperrt_eine_Junction_im_Quellbaum()
    {
        var quelle = Directory.CreateDirectory(Path.Combine(_root, "quelle")).FullName;
        var fremd = Directory.CreateDirectory(Path.Combine(_root, "fremd")).FullName;
        JunctionTestSupport.CreateDirectoryLink(Path.Combine(quelle, "link"), fremd);
        var ziel = Path.Combine(_root, "kopie");

        var fehler = await Assert.ThrowsAsync<InvalidDataException>(() =>
            PersonalGoldBrainFileService.CopyTreeVerifiedAsync(quelle, ziel, _root, CancellationToken.None));

        Assert.Contains("Verknüpfung im zu kopierenden Ordner", fehler.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EnsureNoReparsePoint_laesst_einen_normalen_Pfad_zu()
    {
        var datei = Path.Combine(Directory.CreateDirectory(Path.Combine(_root, "a")).FullName, "frame.png");
        File.WriteAllText(datei, "x");

        PersonalGoldBrainFileService.EnsureNoReparsePoint(datei, _root);
    }

    [Fact]
    public void EnsureNoReparsePoint_meldet_einen_fehlenden_Pfad_wie_bisher()
        => Assert.Throws<FileNotFoundException>(() =>
            PersonalGoldBrainFileService.EnsureNoReparsePoint(Path.Combine(_root, "fehlt.png"), _root));

    [Fact]
    public void EnsureNoReparsePoint_sperrt_einen_Pfad_ausserhalb_der_Schutzwurzel()
    {
        var aussen = Path.Combine(Path.GetTempPath(), "gold_aussen_" + Guid.NewGuid().ToString("N") + ".png");
        File.WriteAllText(aussen, "x");
        try
        {
            var fehler = Assert.Throws<InvalidDataException>(() =>
                PersonalGoldBrainFileService.EnsureNoReparsePoint(aussen, _root));
            Assert.Contains("ausserhalb der Schutzwurzel", fehler.Message, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(aussen);
        }
    }

    public void Dispose()
    {
        try
        {
            foreach (var link in Directory.EnumerateDirectories(_root, "*", SearchOption.AllDirectories)
                         .Where(d => new DirectoryInfo(d).LinkTarget is not null).ToList())
                Directory.Delete(link);
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // Test-Aufraeumen darf das Ergebnis nicht verdecken.
        }
    }
}
