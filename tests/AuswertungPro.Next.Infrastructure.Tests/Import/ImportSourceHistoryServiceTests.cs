using AuswertungPro.Next.Application.Import;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

/// <summary>
/// Charakterisierungstests fuer ImportSourceHistoryService.
/// Sichert das Verhalten der Quellen-Historienverwaltung in den Projekt-Metadaten.
/// </summary>
public class ImportSourceHistoryServiceTests
{
    [Fact]
    public void Track_SpeichertLetztImportQuelle()
    {
        var metadata = new Dictionary<string, string>();
        ImportSourceHistoryService.Track(metadata, @"C:\Projekte\Uri", "WinCan");

        Assert.Equal(@"C:\Projekte\Uri", metadata["ImportQuelle"]);
        Assert.Equal("WinCan", metadata["ImportQuellTyp"]);
    }

    [Fact]
    public void Track_HistorieEnthaltEintrag()
    {
        var metadata = new Dictionary<string, string>();
        ImportSourceHistoryService.Track(metadata, @"C:\Daten\test.pdf", "PDF");

        var history = ImportSourceHistoryService.GetHistory(metadata);
        Assert.Single(history);
        Assert.Contains("PDF", history[0]);
        Assert.Contains(@"C:\Daten\test.pdf", history[0]);
    }

    [Fact]
    public void Track_MehrereEintraege_WerdenAngehaengt()
    {
        var metadata = new Dictionary<string, string>();
        ImportSourceHistoryService.Track(metadata, @"C:\A", "XTF");
        ImportSourceHistoryService.Track(metadata, @"C:\B", "PDF");
        ImportSourceHistoryService.Track(metadata, @"C:\C", "WinCan");

        var history = ImportSourceHistoryService.GetHistory(metadata);
        Assert.Equal(3, history.Count);
    }

    [Fact]
    public void Track_BeiMehr20Eintraegen_WirdAuf20Begrenzt()
    {
        var metadata = new Dictionary<string, string>();
        for (var i = 0; i < 25; i++)
        {
            ImportSourceHistoryService.Track(metadata, $@"C:\Ordner{i}", "XTF");
        }

        var history = ImportSourceHistoryService.GetHistory(metadata);
        Assert.Equal(20, history.Count);
        // Neueste Eintraege behalten
        Assert.Contains(@"C:\Ordner24", history[history.Count - 1]);
    }

    [Fact]
    public void GetHistory_BeiLeererMetadata_GibtLeereListeZurueck()
    {
        var metadata = new Dictionary<string, string>();
        var history = ImportSourceHistoryService.GetHistory(metadata);
        Assert.Empty(history);
    }

    [Fact]
    public void Track_UeberschreibtLetztQuelleBeimNaechstenAufruf()
    {
        var metadata = new Dictionary<string, string>();
        ImportSourceHistoryService.Track(metadata, @"C:\Erster", "XTF");
        ImportSourceHistoryService.Track(metadata, @"C:\Zweiter", "PDF");

        Assert.Equal(@"C:\Zweiter", metadata["ImportQuelle"]);
        Assert.Equal("PDF", metadata["ImportQuellTyp"]);
    }
}
