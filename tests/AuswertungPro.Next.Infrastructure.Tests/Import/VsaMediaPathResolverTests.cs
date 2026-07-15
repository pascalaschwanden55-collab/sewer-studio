using AuswertungPro.Next.Infrastructure.Import.Xtf;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

public sealed class VsaMediaPathResolverTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        $"vsa-media-path-{Guid.NewGuid():N}");

    [Fact]
    public void ResolvePhoto_FindetFotoImExportRootOberhalbDesDokumenteOrdners()
    {
        var documents = Path.Combine(_root, "Dokumente");
        var photo = Path.Combine(_root, "Foto", "schaden.jpg");
        Directory.CreateDirectory(documents);
        Directory.CreateDirectory(Path.GetDirectoryName(photo)!);
        File.WriteAllText(photo, "foto");

        var result = VsaMediaPathResolver.ResolvePhoto(
            Path.Combine(documents, "export.xtf"),
            relativeFolder: null,
            "schaden.jpg");

        Assert.Equal(photo, result, ignoreCase: true);
    }

    [Fact]
    public void ResolveVideo_BevorzugtExplizitenRelativordner()
    {
        var documents = Path.Combine(_root, "Dokumente");
        var video = Path.Combine(documents, "Medien", "film.mp4");
        var directVideo = Path.Combine(documents, "film.mp4");
        var standardVideo = Path.Combine(documents, "Video", "film.mp4");
        Directory.CreateDirectory(Path.GetDirectoryName(video)!);
        Directory.CreateDirectory(Path.GetDirectoryName(standardVideo)!);
        File.WriteAllText(video, "video");
        File.WriteAllText(directVideo, "direkt");
        File.WriteAllText(standardVideo, "standard");

        var result = VsaMediaPathResolver.ResolveVideo(
            Path.Combine(documents, "export.xtf"),
            "Medien",
            "film.mp4");

        Assert.Equal(video, result, ignoreCase: true);
    }

    [Fact]
    public void ResolveVideo_FindetVideoZweiOrdnerebenenOberhalbDerXtfDatei()
    {
        var documents = Path.Combine(_root, "Zwischenordner", "Dokumente");
        var video = Path.Combine(_root, "Video", "film.mp4");
        Directory.CreateDirectory(documents);
        Directory.CreateDirectory(Path.GetDirectoryName(video)!);
        File.WriteAllText(video, "video");

        var result = VsaMediaPathResolver.ResolveVideo(
            Path.Combine(documents, "export.xtf"),
            relativeFolder: null,
            "film.mp4");

        Assert.Equal(video, result, ignoreCase: true);
    }

    [Fact]
    public void ResolvePhoto_FehlendeDateiLiefertWieBisherBevorzugtenKandidaten()
    {
        var documents = Path.Combine(_root, "Dokumente");
        Directory.CreateDirectory(documents);

        var result = VsaMediaPathResolver.ResolvePhoto(
            Path.Combine(documents, "export.xtf"),
            "Bilder",
            "fehlt.jpg");

        Assert.Equal(
            Path.Combine(documents, "Bilder", "fehlt.jpg"),
            result,
            ignoreCase: true);
    }

    [Fact]
    public void ResolvePhoto_LeererOderAbsoluterDateinameBleibtUnveraendert()
    {
        Assert.Equal(string.Empty, VsaMediaPathResolver.ResolvePhoto("export.xtf", null, "  "));
        var absolute = Path.Combine(_root, "direkt.jpg");
        Assert.Equal(absolute, VsaMediaPathResolver.ResolvePhoto("export.xtf", null, absolute));
    }

    [Fact]
    public void InstanceService_FindetVideoWieDieFassade()
    {
        var documents = Path.Combine(_root, "Dokumente");
        var video = Path.Combine(_root, "Video", "film.mp4");
        Directory.CreateDirectory(documents);
        Directory.CreateDirectory(Path.GetDirectoryName(video)!);
        File.WriteAllText(video, "video");
        var resolver = new VsaMediaPathFileResolver();

        var result = resolver.ResolveVideo(
            Path.Combine(documents, "export.xtf"),
            relativeFolder: null,
            "film.mp4");

        Assert.Equal(video, result, ignoreCase: true);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
        catch
        {
            // Temp-Aufraeumen darf den Test nicht verdecken.
        }
    }
}
