using System.IO;
using System.Text;
using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Dauerhaftes Schreiben vor dem Umbenennen (Codeaudit 2026-08-17, Befund 6).
///
/// Das Umbenennen fuehrt NTFS im Journal, den Inhalt nicht. Ohne erzwungenes
/// Schreiben auf den Datentraeger kann ein Stromausfall dazwischen eine Datei
/// mit richtigem Namen und leerem Inhalt hinterlassen. Ein Programmabsturz ist
/// davon nicht betroffen — der Puffer gehoert dem Betriebssystem.
///
/// Die Dauerhaftigkeit selbst laesst sich hier nicht pruefen: Einen Stromausfall
/// kann kein Test ausloesen. Pruefbar ist zweierlei, und beides ist wertvoll —
/// dass der dauerhafte Weg dieselben Bytes erzeugt wie der schnelle, und dass
/// die zwei Stellen, an denen die Wiederherstellung spaeter auf den Inhalt baut,
/// ihn auch wirklich anfordern. Der zweite Test ist der eigentliche Waechter:
/// Er faellt, sobald jemand die Dauerhaftigkeit still wieder herausnimmt.
/// </summary>
public sealed class DauerhaftesSchreibenTests
{
    private sealed class TempDir : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "dauerhaft-tests", Guid.NewGuid().ToString("N"));

        public TempDir() => Directory.CreateDirectory(Path);

        public void Dispose()
        {
            try { Directory.Delete(Path, recursive: true); } catch { }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SchreibtDenselbenInhalt_ObDauerhaftOderNicht(bool durable)
    {
        using var temp = new TempDir();
        var ziel = Path.Combine(temp.Path, "inhalt.json");
        const string inhalt = "{\"a\":1,\"text\":\"Umlaute äöü und — Gedankenstrich\"}";

        AtomicTextFileWriter.WriteAllText(ziel, inhalt, durable);

        Assert.Equal(inhalt, File.ReadAllText(ziel, new UTF8Encoding(false)));
    }

    [Fact]
    public void DauerhaftesSchreiben_ErsetztEineBestehendeDateiUndLegtBackupAn()
    {
        using var temp = new TempDir();
        var ziel = Path.Combine(temp.Path, "inhalt.json");
        AtomicTextFileWriter.WriteAllText(ziel, "alt", durable: true);

        AtomicTextFileWriter.WriteAllText(ziel, "neu", durable: true);

        Assert.Equal("neu", File.ReadAllText(ziel));
        Assert.True(File.Exists(ziel + ".bak"), "Die Vorgaengerversion muss als .bak erhalten bleiben.");
        Assert.Equal("alt", File.ReadAllText(ziel + ".bak"));
    }

    [Fact]
    public void DauerhaftesSchreiben_LaesstKeineZwischendateiZurueck()
    {
        using var temp = new TempDir();
        var ziel = Path.Combine(temp.Path, "inhalt.json");

        AtomicTextFileWriter.WriteAllText(ziel, "inhalt", durable: true);

        Assert.Empty(Directory.GetFiles(temp.Path, "*.tmp"));
    }

    // ── Waechter ────────────────────────────────────────────────────────────
    // Quelltextpruefung nach dem Muster von MaintainabilityFitnessTests: Die
    // beiden Stellen, deren Wiederherstellung auf dem Inhalt aufbaut, muessen
    // dauerhaft schreiben. Ein stilles Zurueckdrehen faellt hier auf.

    [Fact]
    public void Transaktionsmarker_WirdDauerhaftGeschrieben()
    {
        var quelle = LiesQuelle(
            "src", "AuswertungPro.Next.Infrastructure", "Import", "FileImportTransactionJournal.cs");

        Assert.Contains("durable: true", quelle);
    }

    [Fact]
    public void Projektspeicher_SchreibtDenZwischenstandDauerhaft()
    {
        var quelle = LiesQuelle(
            "src", "AuswertungPro.Next.Infrastructure", "Projects", "JsonProjectRepository.cs");

        Assert.Contains("flushToDisk: true", quelle);
        Assert.DoesNotContain("File.WriteAllText(tempPath", quelle);
    }

    private static string LiesQuelle(params string[] teile)
    {
        var pfad = Path.Combine(new[] { TestRepoPaths.RepoRoot() }.Concat(teile).ToArray());
        Assert.True(File.Exists(pfad), $"Quelldatei fehlt: {pfad}");
        return File.ReadAllText(pfad);
    }
}
