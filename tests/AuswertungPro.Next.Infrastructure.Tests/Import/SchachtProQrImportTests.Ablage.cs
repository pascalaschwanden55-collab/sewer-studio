using System.Text.Json.Nodes;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

public sealed partial class SchachtProQrImportTests
{
    [Fact]
    public void QrBild_WirdImSchachtVerteiltOrdnerAbgelegt_UndBestehendesPdfBleibt()
    {
        using var folder = new QrProjectFolder();
        var project = new Project();
        var shaft = new SchachtRecord();
        shaft.SetFieldValue("Schachtnummer", "QR-001", FieldSource.Spro, false);
        shaft.SetFieldValue(FieldKeys.PdfPath, "Schächte_Verteilt/QR-001/original.pdf", FieldSource.Pdf, false);
        project.SchaechteData.Add(shaft);
        using var staging = new ImportFileStagingService().Begin(Path.Combine(folder.Root, "projekt.json"))!;
        var context = new ImportRunContext(CancellationToken.None, null, new ImportRunLog(), fileStaging: staging);
        var node = JsonNode.Parse(Json)!;
        node["protocol"]!["datum"] = "19.09.2026";
        var service = Service(Encode(node.ToJsonString()));
        var result = service.ImportImage(_path, project, context);
        Assert.True(result.Ok, result.ErrorMessage);
        var target = Path.Combine(ProjectStructure.SchachtVerteiltDir(folder.Root, "QR-001"), "20260919_QR-001_QR.png");
        Assert.False(File.Exists(target));
        Assert.Single(staging.PreparedFiles);
        staging.Publish();
        staging.Accept();
        Assert.Equal(File.ReadAllBytes(_path), File.ReadAllBytes(target));
        Assert.Single(project.SchaechteData);
        Assert.Equal("Schächte_Verteilt/QR-001/original.pdf", shaft.GetFieldValue(FieldKeys.PdfPath));
        Assert.Equal("Schächte_Verteilt/QR-001/20260919_QR-001_QR.png", project.Metadata["SchachtPro.QR.Bild.SP-Test-001"]);
        using var again = new ImportFileStagingService().Begin(Path.Combine(folder.Root, "projekt.json"))!;
        Assert.True(service.ImportImage(_path, project,
            new ImportRunContext(CancellationToken.None, null, new ImportRunLog(), fileStaging: again)).Ok);
        Assert.Empty(again.PreparedFiles);
    }

    [Fact]
    public void VerwerfenVeroeffentlichterQrAblage_EntferntNurNeueKopie()
    {
        using var folder = new QrProjectFolder();
        string target;
        using (var staging = new ImportFileStagingService().Begin(Path.Combine(folder.Root, "projekt.json"))!)
        {
            Assert.True(Service(Fixture).ImportImage(_path, new Project(),
                new ImportRunContext(CancellationToken.None, null, new ImportRunLog(), fileStaging: staging)).Ok);
            var prepared = Assert.Single(staging.PreparedFiles);
            staging.Publish();
            target = Directory.GetFiles(ProjectStructure.SchachtVerteiltDir(folder.Root, "QR-001"), "*.png").Single();
            Assert.True(File.Exists(target));
        }
        Assert.False(File.Exists(target));
        Assert.True(File.Exists(_path));
    }

    [Fact]
    public void Ablagefehler_LegtKeinenSchachtAn()
    {
        using var folder = new QrProjectFolder();
        // Eine Datei blockiert den benoetigten Schachtordner.
        var targetFolder = ProjectStructure.SchachtVerteiltDir(folder.Root, "QR-001");
        Directory.CreateDirectory(Path.GetDirectoryName(targetFolder)!);
        File.WriteAllText(targetFolder, "Bestehende Datei");
        using var staging = new ImportFileStagingService().Begin(Path.Combine(folder.Root, "projekt.json"))!;
        var project = new Project();
        var result = Service(Fixture).ImportImage(_path, project,
            new ImportRunContext(CancellationToken.None, null, new ImportRunLog(), fileStaging: staging));
        Assert.False(result.Ok);
        Assert.Empty(project.SchaechteData);
        Assert.Empty(staging.PreparedFiles);
        Assert.Equal("Bestehende Datei", File.ReadAllText(targetFolder));
    }

    [Fact]
    public void GleichnamigesAnderesBild_WirdNichtUeberschrieben()
    {
        using var folder = new QrProjectFolder();
        var directory = ProjectStructure.SchachtVerteiltDir(folder.Root, "QR-001");
        Directory.CreateDirectory(directory);
        var original = Path.Combine(directory, "00000000_QR-001_QR.png");
        File.WriteAllBytes(original, [9, 8, 7]);
        using var staging = new ImportFileStagingService().Begin(Path.Combine(folder.Root, "projekt.json"))!;
        var project = new Project();
        Assert.True(Service(Fixture).ImportImage(_path, project,
            new ImportRunContext(CancellationToken.None, null, new ImportRunLog(), fileStaging: staging)).Ok);
        staging.Publish();
        staging.Accept();
        Assert.Equal(new byte[] { 9, 8, 7 }, File.ReadAllBytes(original));
        Assert.Equal(2, Directory.GetFiles(directory, "*.png").Length);
    }

    [Fact]
    public void BeschaedigterQr_BereitetKeineDateiVor()
    {
        using var folder = new QrProjectFolder();
        using var staging = new ImportFileStagingService().Begin(Path.Combine(folder.Root, "projekt.json"))!;
        Assert.False(Service(Fixture[..^8] + "00000000").ImportImage(_path, new Project(),
            new ImportRunContext(CancellationToken.None, null, new ImportRunLog(), fileStaging: staging)).Ok);
        Assert.Empty(staging.PreparedFiles);
    }

    private sealed class QrProjectFolder : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "spro-qr-ablage-" + Guid.NewGuid().ToString("N"));
        public QrProjectFolder() => Directory.CreateDirectory(Root);
        public void Dispose() => Directory.Delete(Root, true);
    }
}
