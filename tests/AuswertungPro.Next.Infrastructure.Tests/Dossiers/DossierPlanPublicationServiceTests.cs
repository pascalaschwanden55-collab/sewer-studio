using System;
using System.IO;
using System.Text;

using AuswertungPro.Next.Infrastructure.Dossiers;
using AuswertungPro.Next.Infrastructure.Tests.Backup;

using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Dossiers;

public sealed class DossierPlanPublicationServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "dossier_plan_publish_" + Guid.NewGuid().ToString("N"));

    private readonly string _projectRoot;
    private readonly DossierPlanPublicationService _service = new();

    public DossierPlanPublicationServiceTests()
    {
        _projectRoot = Path.Combine(_root, "Projekt");
        Directory.CreateDirectory(_projectRoot);
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
            // Ein Aufraeumfehler darf den Testlauf nicht rot machen.
        }
    }

    [Fact]
    public void Publish_blockiert_Traversal_aus_dem_Projekt()
    {
        var source = SchreibeQuelle("plan.png", "Plan");
        var outside = Path.Combine(_root, "Ausserhalb");
        var traversalTarget = Path.Combine(
            _projectRoot,
            "Dossiers",
            "..",
            "..",
            "Ausserhalb");

        var result = _service.Publish(_projectRoot, source, traversalTarget);

        Assert.False(result.Success);
        Assert.Contains("ausserhalb", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.False(Directory.Exists(outside));
    }

    [JunctionFact]
    public void Publish_blockiert_Junction_im_Dossierziel()
    {
        var source = SchreibeQuelle("plan.png", "Plan");
        var dossiers = Path.Combine(_projectRoot, "Dossiers");
        var outside = Path.Combine(_root, "Fremdziel");
        var linkedTarget = Path.Combine(dossiers, "Liegenschaft");
        Directory.CreateDirectory(dossiers);
        Directory.CreateDirectory(outside);
        JunctionTestSupport.CreateDirectoryLink(linkedTarget, outside);

        try
        {
            var result = _service.Publish(_projectRoot, source, linkedTarget);

            Assert.False(result.Success);
            Assert.Contains("Verknuepfung", result.Error, StringComparison.OrdinalIgnoreCase);
            Assert.Empty(Directory.EnumerateFiles(outside));
        }
        finally
        {
            try
            {
                if (Directory.Exists(linkedTarget))
                    Directory.Delete(linkedTarget);
            }
            catch
            {
                // Nur Test-Aufraeumen; das Fremdziel wird nie rekursiv ueber den Link geloescht.
            }
        }
    }

    [Fact]
    public void Rollback_entfernt_nur_die_neu_erzeugte_unveraenderte_Datei()
    {
        var source = SchreibeQuelle("plan.png", "neuer Plan");
        var targetFolder = Path.Combine(_projectRoot, "Dossiers", "Liegenschaft");
        Directory.CreateDirectory(targetFolder);
        var existing = Path.Combine(targetFolder, "plan.png");
        File.WriteAllText(existing, "bestehender Plan", Encoding.UTF8);

        var result = _service.Publish(_projectRoot, source, targetFolder);

        Assert.True(result.Success, result.Error);
        Assert.Equal("plan (2).png", Path.GetFileName(result.ImagePath));
        var rollback = result.Publication!.Rollback();

        Assert.True(rollback.Success, rollback.Error);
        Assert.False(File.Exists(result.ImagePath));
        Assert.Equal("bestehender Plan", File.ReadAllText(existing, Encoding.UTF8));
        result.Publication.Dispose();
    }

    [Fact]
    public void Rollback_loescht_keine_nachtraeglich_veraenderte_Datei()
    {
        var source = SchreibeQuelle("plan.png", "neuer Plan");
        var targetFolder = Path.Combine(_projectRoot, "Dossiers", "Liegenschaft");
        var result = _service.Publish(_projectRoot, source, targetFolder);
        Assert.True(result.Success, result.Error);

        File.WriteAllText(result.ImagePath!, "fremd veraendert", Encoding.UTF8);

        var rollback = result.Publication!.Rollback();

        Assert.False(rollback.Success);
        Assert.True(File.Exists(result.ImagePath));
        result.Publication.Accept();
        result.Publication.Dispose();
    }

    private string SchreibeQuelle(string fileName, string content)
    {
        var sourceFolder = Path.Combine(_root, "Arbeitskopie");
        Directory.CreateDirectory(sourceFolder);
        var path = Path.Combine(sourceFolder, fileName);
        File.WriteAllText(path, content, Encoding.UTF8);
        return path;
    }
}
