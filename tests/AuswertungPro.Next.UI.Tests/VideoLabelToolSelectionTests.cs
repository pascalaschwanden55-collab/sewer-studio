using System.IO;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class VideoLabelToolSelectionTests
{
    [Fact]
    public void ServerPy_nimmt_standardmaessig_anschluss_und_bogen_mit()
    {
        var server = File.ReadAllText(RepoFile("tools", "VideoLabelTool", "server.py"));

        Assert.Contains("\"BCA\"", server);
        Assert.Contains("\"BCC\"", server);
        Assert.Contains("yolo_vsa_cls_dataset_v3_bal", server);
    }

    [Fact]
    public void ServerPy_mischt_klassen_statt_erst_alle_risse_zu_zeigen()
    {
        var server = File.ReadAllText(RepoFile("tools", "VideoLabelTool", "server.py"));

        Assert.Contains("def interleave_by_class", server);
        Assert.Contains("def interleave_by_holding", server);
        Assert.Contains("def dedupe_near_duplicates", server);
        Assert.Contains("--dedupe-window", server);
        Assert.Contains("haltung_keys.sort(key=lambda k: (", server);
        Assert.Contains("0 if findings[k][\"video_available\"] else 1", server);
        Assert.Contains("order = prio + interleave_by_class", server);
        AssertNoForbiddenTokens(server, "rest.sort(key=lambda k: (findings[k][\"klass\"]");
    }

    [Fact]
    public void AppHtml_bietet_klassenauswahl_mit_mix_an()
    {
        var html = File.ReadAllText(RepoFile("tools", "VideoLabelTool", "app.html"));

        Assert.Contains("id=\"classfilter\"", html);
        Assert.Contains("Alle / Mix", html);
        Assert.Contains("function matchesClassFilter", html);
    }

    private static void AssertNoForbiddenTokens(string source, params string[] forbiddenTokens)
    {
        var hits = forbiddenTokens
            .Where(token => source.Contains(token, StringComparison.Ordinal))
            .ToArray();

        Assert.True(hits.Length == 0, "Verbotene alte VideoLabelTool-Sortierung gefunden: " + string.Join(", ", hits));
    }
}
