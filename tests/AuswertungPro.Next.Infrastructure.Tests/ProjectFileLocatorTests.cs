using System;
using System.IO;
using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class ProjectFileLocatorTests
{
    [Fact]
    public void ProjectRootFromFile_DateiInProjektdateien_GibtElternRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "P1");
        var file = Path.Combine(root, "Projektdateien", "projekt.json");
        Assert.Equal(root, ProjectFileLocator.ProjectRootFromFile(file));
    }

    [Fact]
    public void ProjectRootFromFile_DateiImRoot_GibtVerzeichnis()
    {
        var root = Path.Combine(Path.GetTempPath(), "P2");
        var file = Path.Combine(root, "projekt.json");
        Assert.Equal(root, ProjectFileLocator.ProjectRootFromFile(file));
    }

    [Fact]
    public void ProjectRootFromFile_LeerOderNull_GibtNull()
    {
        Assert.Null(ProjectFileLocator.ProjectRootFromFile(null));
        Assert.Null(ProjectFileLocator.ProjectRootFromFile(""));
    }

    [Fact]
    public void TargetPath_IstImmerProjektdateien()
    {
        var root = Path.Combine(Path.GetTempPath(), "P3");
        Assert.Equal(Path.Combine(root, "Projektdateien", "projekt.json"), ProjectFileLocator.TargetPath(root));
    }

    [Fact]
    public void Locate_FindetProjektdateienVorRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), $"pfl-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "Projektdateien"));
            File.WriteAllText(Path.Combine(root, "projekt.json"), "{}");
            File.WriteAllText(Path.Combine(root, "Projektdateien", "projekt.json"), "{}");
            Assert.Equal(Path.Combine(root, "Projektdateien", "projekt.json"), ProjectFileLocator.Locate(root));
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }

    [Fact]
    public void Locate_AltprojektImRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), $"pfl-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, "projekt.json"), "{}");
            Assert.Equal(Path.Combine(root, "projekt.json"), ProjectFileLocator.Locate(root));
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }

    [Fact]
    public void Locate_KeineDatei_GibtNull()
    {
        var root = Path.Combine(Path.GetTempPath(), $"pfl-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try { Assert.Null(ProjectFileLocator.Locate(root)); }
        finally { try { Directory.Delete(root, true); } catch { } }
    }
}
