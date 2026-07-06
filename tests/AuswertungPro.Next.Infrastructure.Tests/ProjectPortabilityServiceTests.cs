using System;
using System.IO;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class ProjectPortabilityServiceTests
{
    [Fact]
    public void MakePortable_AbsoluteExternalVideoLink_RelinksToHoldingCopyRelative()
    {
        // Link zeigt absolut auf die QUELLE; das verteilte Video liegt im Projekt-Haltungsordner.
        // -> Link wird auf die Projekt-Kopie umgebogen (relativ), kein Neu-Kopieren.
        var root = NewDir();
        var holding = "22149-3.01";
        var holdingFolder = Path.Combine(root, "Verteilung", holding);
        Directory.CreateDirectory(holdingFolder);
        var holdingVideo = Path.Combine(holdingFolder, "20260616_22149-3.01.mpg");
        File.WriteAllText(holdingVideo, "v");
        try
        {
            var project = new Project();
            var rec = new HaltungRecord();
            rec.SetFieldValue("Haltungsname", holding, FieldSource.Xtf, userEdited: false);
            rec.SetFieldValue("Link", @"D:\Videoprojekte\Meien\Film\H_22149-3.01.mpg", FieldSource.Xtf, userEdited: false);
            project.AddRecord(rec);

            var result = new ProjectPortabilityService().MakePortable(root, project);

            var link = rec.GetFieldValue("Link") ?? "";
            Assert.False(Path.IsPathRooted(link), $"Link sollte relativ sein: {link}");
            var resolved = ProjectPathResolver.ResolveFilePathFromProjectFolder(link, root);
            Assert.NotNull(resolved);
            Assert.Equal(Path.GetFullPath(holdingVideo), Path.GetFullPath(resolved!), ignoreCase: true);
            Assert.True(result.RelinkedPaths >= 1);
        }
        finally { TryDelete(root); }
    }

    [Fact]
    public void MakePortable_AbsoluteExternalFoto_CopiedIntoHoldingFotosRelative()
    {
        // Foto liegt nur in der externen Quelle -> wird in <Haltung>/Fotos kopiert + relativ verlinkt.
        var root = NewDir();
        var ext = NewDir();
        var holding = "22149-3.01";
        Directory.CreateDirectory(Path.Combine(root, "Verteilung", holding));
        var srcFoto = Path.Combine(ext, "H_22149-3.01_044.jpg");
        File.WriteAllText(srcFoto, "img");
        try
        {
            var project = new Project();
            var rec = new HaltungRecord();
            rec.SetFieldValue("Haltungsname", holding, FieldSource.Xtf, userEdited: false);
            rec.VsaFindings.Add(new VsaFinding { FotoPath = srcFoto });
            project.AddRecord(rec);

            var result = new ProjectPortabilityService().MakePortable(root, project);

            var foto = rec.VsaFindings[0].FotoPath ?? "";
            Assert.False(Path.IsPathRooted(foto), $"FotoPath sollte relativ sein: {foto}");
            Assert.Contains("Fotos", foto, StringComparison.OrdinalIgnoreCase);
            Assert.True(File.Exists(Path.Combine(root, foto)), $"Foto sollte ins Projekt kopiert sein: {foto}");
            Assert.True(result.FotosCopied >= 1);
        }
        finally { TryDelete(root); TryDelete(ext); }
    }

    [Fact]
    public void MakePortable_ExternalFotoWithSameSizedDifferentExistingName_UsesCollisionPath()
    {
        var root = NewDir();
        var ext = NewDir();
        var holding = "22149-3.01";
        var holdingFolder = Path.Combine(root, "Verteilung", holding);
        var fotoFolder = Path.Combine(holdingFolder, "Fotos");
        Directory.CreateDirectory(fotoFolder);
        var existing = Path.Combine(fotoFolder, "H_22149-3.01_044.jpg");
        File.WriteAllText(existing, "old");
        var srcFoto = Path.Combine(ext, "H_22149-3.01_044.jpg");
        File.WriteAllText(srcFoto, "new");
        try
        {
            var project = new Project();
            var rec = new HaltungRecord();
            rec.SetFieldValue("Haltungsname", holding, FieldSource.Xtf, userEdited: false);
            rec.VsaFindings.Add(new VsaFinding { FotoPath = srcFoto });
            project.AddRecord(rec);

            new ProjectPortabilityService().MakePortable(root, project);

            var foto = rec.VsaFindings[0].FotoPath ?? "";
            var resolved = ProjectPathResolver.ResolveFilePathFromProjectFolder(foto, root);
            Assert.NotNull(resolved);
            Assert.EndsWith("_1.jpg", resolved, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("old", File.ReadAllText(existing));
            Assert.Equal("new", File.ReadAllText(resolved!));
        }
        finally { TryDelete(root); TryDelete(ext); }
    }

    [Fact]
    public void MakePortable_AbsoluteInsideProject_MadeRelative()
    {
        var root = NewDir();
        var holding = "X-1";
        var dir = Path.Combine(root, "Verteilung", holding);
        Directory.CreateDirectory(dir);
        var pdf = Path.Combine(dir, "20260616_X-1.pdf");
        File.WriteAllText(pdf, "p");
        try
        {
            var project = new Project();
            var rec = new HaltungRecord();
            rec.SetFieldValue("Haltungsname", holding, FieldSource.Xtf, userEdited: false);
            rec.SetFieldValue("PDF_Path", pdf, FieldSource.Xtf, userEdited: false); // absolut, INNERHALB Projekt
            project.AddRecord(rec);

            new ProjectPortabilityService().MakePortable(root, project);

            var p = rec.GetFieldValue("PDF_Path") ?? "";
            Assert.False(Path.IsPathRooted(p), $"PDF_Path sollte relativ sein: {p}");
            Assert.NotNull(ProjectPathResolver.ResolveFilePathFromProjectFolder(p, root));
        }
        finally { TryDelete(root); }
    }

    [Fact]
    public void MakePortable_PrefersDirectHoldingCopyOverRecursiveLegacyMatch()
    {
        var root = NewDir();
        var holding = "X-3";
        var dir = Path.Combine(root, "Verteilung", holding);
        Directory.CreateDirectory(dir);
        var direct = Path.Combine(dir, "20260616_X-3.mpg");
        File.WriteAllText(direct, "direct");
        var legacyDir = Path.Combine(dir, "Video");
        Directory.CreateDirectory(legacyDir);
        var recursive = Path.Combine(legacyDir, "H_X-3.mpg");
        File.WriteAllText(recursive, "legacy");
        try
        {
            var project = new Project();
            var rec = new HaltungRecord();
            rec.SetFieldValue("Haltungsname", holding, FieldSource.Xtf, userEdited: false);
            rec.SetFieldValue("Link", @"D:\Quelle\H_X-3.mpg", FieldSource.Xtf, userEdited: false);
            project.AddRecord(rec);

            new ProjectPortabilityService().MakePortable(root, project);

            var link = rec.GetFieldValue("Link") ?? "";
            var resolved = ProjectPathResolver.ResolveFilePathFromProjectFolder(link, root);
            Assert.Equal(Path.GetFullPath(direct), Path.GetFullPath(resolved!), ignoreCase: true);
        }
        finally { TryDelete(root); }
    }

    [Fact]
    public void MakePortable_AlreadyRelativeResolving_Kept()
    {
        var root = NewDir();
        var holding = "X-2";
        var dir = Path.Combine(root, "Verteilung", holding);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "v.mpg"), "v");
        var rel = Path.Combine("Verteilung", holding, "v.mpg");
        try
        {
            var project = new Project();
            var rec = new HaltungRecord();
            rec.SetFieldValue("Haltungsname", holding, FieldSource.Xtf, userEdited: false);
            rec.SetFieldValue("Link", rel, FieldSource.Xtf, userEdited: false);
            project.AddRecord(rec);

            new ProjectPortabilityService().MakePortable(root, project);

            Assert.Equal(rel, rec.GetFieldValue("Link"));
        }
        finally { TryDelete(root); }
    }

    private static string NewDir()
    {
        var d = Path.Combine(Path.GetTempPath(), $"port-{Guid.NewGuid():N}");
        Directory.CreateDirectory(d);
        return d;
    }

    private static void TryDelete(string d)
    {
        try { if (Directory.Exists(d)) Directory.Delete(d, recursive: true); } catch { }
    }
}
