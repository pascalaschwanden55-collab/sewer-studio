using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.DataPage;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.DataPage;

namespace AuswertungPro.Next.UI.Tests;

public sealed class BeobachtungenPhotoOpenControllerTests
{
    [Fact]
    public void Erstes_vorhandenes_Foto_oeffnen_fehlende_ueberspringen_und_dann_stoppen()
    {
        var locator = new RecordingFileLocator("aufgeloest.jpg") { MissingRaw = "fehlt.jpg" };
        var shell = new RecordingShellOpen(true);
        var result = new BeobachtungenPhotoOpenController(locator, shell)
            .OpenFirst([" ", "fehlt.jpg", "vorhanden.jpg", "weiteres.jpg"], "projekt.json");
        Assert.Equal(BeobachtungenPhotoOpenStatus.Opened, result.Status);
        Assert.Equal(new[] { "aufgeloest.jpg" }, shell.Paths);
        Assert.Equal(new (string?, string?)[] { ("fehlt.jpg", "projekt.json"), ("vorhanden.jpg", "projekt.json") }, locator.ResolveCalls);
    }

    [Fact]
    public void Alle_Fotos_fehlen_oder_keine_hinterlegt()
    {
        var controller = new BeobachtungenPhotoOpenController(new RecordingFileLocator(null), new RecordingShellOpen(true));
        Assert.Equal(BeobachtungenPhotoOpenStatus.Ignored, controller.OpenFirst([], null).Status);
        Assert.Equal(BeobachtungenPhotoOpenStatus.NotFound, controller.OpenFirst(["a.jpg", "b.jpg"], null).Status);
    }

    [Fact]
    public void Oeffnungsfehler_startet_nicht_weitere_Fotos()
    {
        var shell = new RecordingShellOpen(false, "Fehler");
        var controller = new BeobachtungenPhotoOpenController(new RecordingFileLocator("a.jpg"), shell);
        var result = controller.OpenFirst(["a.jpg", "b.jpg"], "projekt.json");
        Assert.Equal(BeobachtungenPhotoOpenStatus.OpenFailed, result.Status);
        Assert.Equal("Fehler", result.Error);
        Assert.Single(shell.Paths);
    }

    [Fact]
    public void Leerer_Pfad_wird_ignoriert()
    {
        var locator = new RecordingFileLocator("C:\\Projekt\\foto.jpg");
        var shellOpen = new RecordingShellOpen(result: true);
        var controller = new BeobachtungenPhotoOpenController(locator, shellOpen);

        var result = controller.Open("   ", "C:\\Projekt\\projekt.json");

        Assert.Equal(BeobachtungenPhotoOpenStatus.Ignored, result.Status);
        Assert.Empty(locator.ResolveCalls);
        Assert.Empty(shellOpen.Paths);
    }

    [Fact]
    public void Fehlende_Datei_wird_gemeldet_und_nicht_geoeffnet()
    {
        var locator = new RecordingFileLocator(resolvedPath: null);
        var shellOpen = new RecordingShellOpen(result: true);
        var controller = new BeobachtungenPhotoOpenController(locator, shellOpen);

        var result = controller.Open(
            "Fotos\\schaden.jpg",
            "C:\\Projekt\\projekt.json");

        Assert.Equal(BeobachtungenPhotoOpenStatus.NotFound, result.Status);
        Assert.Equal(
            [("Fotos\\schaden.jpg", "C:\\Projekt\\projekt.json")],
            locator.ResolveCalls);
        Assert.Empty(shellOpen.Paths);
    }

    [Fact]
    public void Gefundene_Datei_wird_ueber_den_sicheren_Dienst_geoeffnet()
    {
        const string resolvedPath = "C:\\Projekt\\Fotos\\schaden.jpg";
        var locator = new RecordingFileLocator(resolvedPath);
        var shellOpen = new RecordingShellOpen(result: true);
        var controller = new BeobachtungenPhotoOpenController(locator, shellOpen);

        var result = controller.Open("Fotos\\schaden.jpg", "projekt.json");

        Assert.Equal(BeobachtungenPhotoOpenStatus.Opened, result.Status);
        Assert.Null(result.Error);
        Assert.Equal([resolvedPath], shellOpen.Paths);
    }

    [Fact]
    public void Fehler_des_Oeffnungsdienstes_bleibt_erhalten()
    {
        const string resolvedPath = "C:\\Projekt\\Fotos\\schaden.jpg";
        var locator = new RecordingFileLocator(resolvedPath);
        var shellOpen = new RecordingShellOpen(result: false, error: "Start blockiert");
        var controller = new BeobachtungenPhotoOpenController(locator, shellOpen);

        var result = controller.Open("Fotos\\schaden.jpg", "projekt.json");

        Assert.Equal(BeobachtungenPhotoOpenStatus.OpenFailed, result.Status);
        Assert.Equal("Start blockiert", result.Error);
        Assert.Equal([resolvedPath], shellOpen.Paths);
    }

    private sealed class RecordingFileLocator(string? resolvedPath)
        : IInspectionProtocolFileLocator
    {
        public List<(string? Raw, string? ProjectPath)> ResolveCalls { get; } = [];
        public string? MissingRaw { get; init; }

        public string? ResolveExistingPath(string? raw, string? projectPath)
        {
            ResolveCalls.Add((raw, projectPath));
            return raw == MissingRaw ? null : resolvedPath;
        }

        public string? FindProtocolPath(
            HaltungRecord record,
            string? resolvedLink,
            string? initialFolder,
            string? projectPath,
            string? storedFilesRaw)
            => throw new NotSupportedException();

        public List<string> ResolveOriginalPdfPaths(HaltungRecord record, string projectFolder)
            => throw new NotSupportedException();

        public void AddResolvedPdf(List<string> paths, string? raw, string projectFolder)
            => throw new NotSupportedException();

        public void ResolveSchachtPdfPaths(
            SchachtRecord schacht,
            string projectFolder,
            List<string> paths)
            => throw new NotSupportedException();
    }

    private sealed class RecordingShellOpen(bool result, string? error = null)
        : ISafeShellOpenService
    {
        public List<string?> Paths { get; } = [];

        public bool TryOpen(string? path, out string? actualError)
        {
            Paths.Add(path);
            actualError = error;
            return result;
        }
    }
}
