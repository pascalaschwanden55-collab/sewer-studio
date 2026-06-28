using System.Collections.Generic;
using AuswertungPro.Next.Infrastructure.Import.Common;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

/// <summary>
/// Charakterisierungstests fuer <see cref="MediaFileIndex"/>.
/// Stellt sicher, dass Build und ResolveSingle dieselbe Semantik wie
/// die frueheren lokalen BuildFileIndex/ResolveFile-Bloecke in IBAK, WinCan
/// und KINS haben.
/// </summary>
public class MediaFileIndexTests
{
    private static HashSet<string> ExtSet(params string[] exts)
        => new(exts, System.StringComparer.OrdinalIgnoreCase);

    // ----- Build -----

    [Fact]
    public void Build_LeereEingabe_GibtLeerenIndexZurueck()
    {
        var index = MediaFileIndex.Build(System.Array.Empty<string>(), ExtSet(".mp4"));
        Assert.Empty(index);
    }

    [Fact]
    public void Build_DateiMitPassenderExtension_WirdAufgenommen()
    {
        var files = new[] { @"C:\Export\video.mp4" };
        var index = MediaFileIndex.Build(files, ExtSet(".mp4"));
        Assert.True(index.ContainsKey("video.mp4"));
        Assert.Single(index["video.mp4"]);
        Assert.Equal(@"C:\Export\video.mp4", index["video.mp4"][0]);
    }

    [Fact]
    public void Build_DateiMitNichtPassenderExtension_WirdNichtAufgenommen()
    {
        var files = new[] { @"C:\Export\bericht.pdf" };
        var index = MediaFileIndex.Build(files, ExtSet(".mp4"));
        Assert.Empty(index);
    }

    [Fact]
    public void Build_MehrereDateienGleicherName_AlleEintraege()
    {
        var files = new[]
        {
            @"C:\A\video.mp4",
            @"C:\B\video.mp4"
        };
        var index = MediaFileIndex.Build(files, ExtSet(".mp4"));
        Assert.True(index.ContainsKey("video.mp4"));
        Assert.Equal(2, index["video.mp4"].Count);
    }

    [Fact]
    public void Build_ExtensionVergleichCaseInsensitive()
    {
        // ".MP4" (Grossbuchstaben) muss mit ".mp4"-Extension-Set matchen
        var files = new[] { @"C:\Export\VIDEO.MP4" };
        var index = MediaFileIndex.Build(files, ExtSet(".mp4"));
        Assert.True(index.ContainsKey("VIDEO.MP4"));
    }

    [Fact]
    public void Build_SchluesselVergleichCaseInsensitive()
    {
        var files = new[] { @"C:\Export\Video.mp4" };
        var index = MediaFileIndex.Build(files, ExtSet(".mp4"));
        // OrdinalIgnoreCase: "video.mp4" und "Video.mp4" sind gleich
        Assert.True(index.ContainsKey("video.mp4"));
    }

    [Fact]
    public void Build_MehrereExtensions_AlleWerdenAufgenommen()
    {
        var files = new[]
        {
            @"C:\Export\vid.mp4",
            @"C:\Export\foto.jpg",
            @"C:\Export\doc.pdf",
            @"C:\Export\skip.exe"
        };
        var index = MediaFileIndex.Build(files, ExtSet(".mp4", ".jpg", ".pdf"));
        Assert.True(index.ContainsKey("vid.mp4"));
        Assert.True(index.ContainsKey("foto.jpg"));
        Assert.True(index.ContainsKey("doc.pdf"));
        Assert.False(index.ContainsKey("skip.exe"));
    }

    // ----- ResolveSingle -----

    [Fact]
    public void ResolveSingle_DateinameNichtImIndex_GibtNullZurueck()
    {
        var index = new Dictionary<string, List<string>>(System.StringComparer.OrdinalIgnoreCase);
        Assert.Null(MediaFileIndex.ResolveSingle(index, "video.mp4"));
    }

    [Fact]
    public void ResolveSingle_EindeutigerTreffer_GibtPfadZurueck()
    {
        var index = new Dictionary<string, List<string>>(System.StringComparer.OrdinalIgnoreCase)
        {
            ["video.mp4"] = new List<string> { @"C:\Export\video.mp4" }
        };
        Assert.Equal(@"C:\Export\video.mp4", MediaFileIndex.ResolveSingle(index, "video.mp4"));
    }

    [Fact]
    public void ResolveSingle_MehrdeutigerTreffer_GibtNullZurueck()
    {
        var index = new Dictionary<string, List<string>>(System.StringComparer.OrdinalIgnoreCase)
        {
            ["video.mp4"] = new List<string> { @"C:\A\video.mp4", @"C:\B\video.mp4" }
        };
        Assert.Null(MediaFileIndex.ResolveSingle(index, "video.mp4"));
    }

    [Fact]
    public void ResolveSingle_LeereListe_GibtNullZurueck()
    {
        var index = new Dictionary<string, List<string>>(System.StringComparer.OrdinalIgnoreCase)
        {
            ["video.mp4"] = new List<string>()
        };
        Assert.Null(MediaFileIndex.ResolveSingle(index, "video.mp4"));
    }

    [Fact]
    public void ResolveSingle_VergleichCaseInsensitive()
    {
        var index = new Dictionary<string, List<string>>(System.StringComparer.OrdinalIgnoreCase)
        {
            ["Video.MP4"] = new List<string> { @"C:\Export\Video.MP4" }
        };
        // Abfrage in Kleinbuchstaben muss trotzdem treffen
        Assert.Equal(@"C:\Export\Video.MP4", MediaFileIndex.ResolveSingle(index, "video.mp4"));
    }
}
