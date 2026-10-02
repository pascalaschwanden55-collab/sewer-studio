using AuswertungPro.Next.Infrastructure.Ai.Training;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Ai.Training;

public sealed class PersonalGoldInboxFileServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "sewer-personal-gold-inbox-tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task LoadAsync_legt_Codeordner_an_und_liefert_nur_Bilder_mit_Ordnerhinweis()
    {
        var service = new PersonalGoldInboxFileService(
            _root,
            code => code switch
            {
                "BAB" => "Riss",
                "BAC" => "Leitungsbruch / Einsturz",
                "BCA" => "Seitlicher Anschluss",
                _ => null
            });
        var inbox = service.EnsureFolders();
        var babFrame = Path.Combine(inbox, "BAB - Riss", "riss.jpg");
        var legacyBcaFolder = Directory.CreateDirectory(Path.Combine(inbox, "BCA")).FullName;
        var legacyBcaFrame = Path.Combine(legacyBcaFolder, "anschluss.jpg");
        var unassignedFrame = Path.Combine(inbox, "_OHNE_ZUORDNUNG", "offen.png");
        var completedFrame = Path.Combine(inbox, "_ERLEDIGT", "fertig.jpg");
        await File.WriteAllBytesAsync(babFrame, [1, 2, 3]);
        await File.WriteAllBytesAsync(legacyBcaFrame, [2, 3, 4]);
        await File.WriteAllBytesAsync(unassignedFrame, [4, 5, 6]);
        await File.WriteAllBytesAsync(completedFrame, [7, 8, 9]);
        await File.WriteAllTextAsync(Path.Combine(inbox, "BAB - Riss", "notiz.txt"), "kein Bild");

        var result = await service.LoadAsync();

        Assert.Equal(inbox, result.RootPath);
        Assert.Empty(result.Issues);
        Assert.Equal(3, result.Images.Count);
        var bab = Assert.Single(result.Images, image => image.FramePath == babFrame);
        Assert.Equal("BAB", bab.SuggestedMainCode);
        Assert.StartsWith("gold_inbox_", bab.QueueId);
        Assert.Equal("BCA", Assert.Single(
            result.Images,
            image => image.FramePath == legacyBcaFrame).SuggestedMainCode);
        Assert.Null(Assert.Single(
            result.Images,
            image => image.FramePath == unassignedFrame).SuggestedMainCode);
        Assert.DoesNotContain(result.Images, image => image.FramePath == completedFrame);
        Assert.True(Directory.Exists(Path.Combine(inbox, "BCA - Seitlicher Anschluss")));
        Assert.True(Directory.Exists(Path.Combine(inbox, "BAC - Leitungsbruch - Einsturz")));
        Assert.True(Directory.Exists(Path.Combine(inbox, "BBD - Eindringender Boden")));
    }

    // Deepscan A5: Ordner- und Dateipruefung laufen ueber den gemeinsamen VerknuepfungsSchutz.
    [Backup.JunctionFact]
    public async Task LoadAsync_ueberspringt_verknuepfte_Ordner_und_Dateien_mit_Hinweis()
    {
        var service = new PersonalGoldInboxFileService(_root, _ => null);
        var inbox = service.EnsureFolders();
        var fremd = Directory.CreateDirectory(Path.Combine(_root, "fremd")).FullName;
        var fremdesBild = Path.Combine(fremd, "fremd.jpg");
        await File.WriteAllBytesAsync(fremdesBild, [1, 2, 3]);
        var ordnerLink = Path.Combine(inbox, "verknuepft");
        Directory.CreateSymbolicLink(ordnerLink, fremd);
        var dateiLink = Path.Combine(inbox, "_OHNE_ZUORDNUNG", "link.jpg");
        File.CreateSymbolicLink(dateiLink, fremdesBild);

        var result = await service.LoadAsync();

        Assert.Empty(result.Images);
        Assert.Contains(result.Issues, i => i.Contains("Verknüpfter Ordner wurde übersprungen", StringComparison.Ordinal) && i.Contains(ordnerLink, StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Issues, i => i.Contains("Verknüpfte Datei wurde übersprungen", StringComparison.Ordinal) && i.Contains(dateiLink, StringComparison.OrdinalIgnoreCase));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}
