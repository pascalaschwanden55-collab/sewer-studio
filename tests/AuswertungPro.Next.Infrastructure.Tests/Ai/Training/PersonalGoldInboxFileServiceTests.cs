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

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}
