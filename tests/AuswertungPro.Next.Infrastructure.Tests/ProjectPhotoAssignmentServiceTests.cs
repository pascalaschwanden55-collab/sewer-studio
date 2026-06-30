using System;
using System.IO;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Infrastructure.Import;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class ProjectPhotoAssignmentServiceTests
{
    [Fact]
    public void AssignFromFolder_ExternalHaltungNamedPhotos_CopiedAndAssignedRelative()
    {
        var root = NewDir();
        var ext = NewDir();
        File.WriteAllText(Path.Combine(ext, "H_22149-3.01_044.jpg"), "a");
        File.WriteAllText(Path.Combine(ext, "H_22149-3.01_045.jpg"), "b");
        try
        {
            var project = new Project();
            var rec = NewRecordWithEntry("22149-3.01", "Riss laengs, Foto2");
            project.AddRecord(rec);

            var result = new ProjectPhotoAssignmentService().AssignFromFolder(root, ext, project);

            Assert.Equal(1, result.HoldingsMatched);
            Assert.Equal(2, result.PhotosCopied);
            Assert.Equal(2, result.PhotosAssigned);

            var fotos = rec.Protocol!.Current.Entries[0].FotoPaths;
            Assert.Equal(2, fotos.Count);
            foreach (var f in fotos)
            {
                Assert.False(Path.IsPathRooted(f), $"FotoPath sollte relativ sein: {f}");
                Assert.True(File.Exists(Path.Combine(root, f)), $"Foto sollte im Projekt liegen: {f}");
                Assert.Contains("Fotos", f, StringComparison.OrdinalIgnoreCase);
            }
        }
        finally { TryDelete(root); TryDelete(ext); }
    }

    [Fact]
    public void AssignFromFolder_PhotoAlreadyInProject_LinkedRelativeNotCopied()
    {
        var root = NewDir();
        var inProj = Path.Combine(root, "Fotos");
        Directory.CreateDirectory(inProj);
        File.WriteAllText(Path.Combine(inProj, "H_22149-3.01_001.jpg"), "a");
        try
        {
            var project = new Project();
            var rec = NewRecordWithEntry("22149-3.01", "Foto1");
            project.AddRecord(rec);

            var result = new ProjectPhotoAssignmentService().AssignFromFolder(root, inProj, project);

            Assert.Equal(1, result.HoldingsMatched);
            Assert.Equal(0, result.PhotosCopied); // schon im Projekt -> nur relativ verlinkt
            var fotos = rec.Protocol!.Current.Entries[0].FotoPaths;
            Assert.Single(fotos);
            Assert.False(Path.IsPathRooted(fotos[0]));
        }
        finally { TryDelete(root); }
    }

    [Fact]
    public void AssignFromFolder_GuidNamedPhotos_Unmatched()
    {
        var root = NewDir();
        var ext = NewDir();
        File.WriteAllText(Path.Combine(ext, "6082ac00-e8c8-4f48-9b18-55b5ef73f1be_Foto1_110825.png"), "a");
        try
        {
            var project = new Project();
            var rec = NewRecordWithEntry("22149-3.01", "Foto1");
            project.AddRecord(rec);

            var result = new ProjectPhotoAssignmentService().AssignFromFolder(root, ext, project);

            Assert.Equal(0, result.HoldingsMatched);
            Assert.Equal(1, result.UnmatchedFiles);
            Assert.Empty(rec.Protocol!.Current.Entries[0].FotoPaths);
        }
        finally { TryDelete(root); TryDelete(ext); }
    }

    [Fact]
    public void AssignFromFolder_LeftoverPhotos_AttachedToFirstEntry()
    {
        // Keine "foto"-Marker -> alle Fotos landen sichtbar an der ersten Beobachtung.
        var root = NewDir();
        var ext = NewDir();
        File.WriteAllText(Path.Combine(ext, "H_06-001_001.jpg"), "a");
        File.WriteAllText(Path.Combine(ext, "H_06-001_002.jpg"), "b");
        try
        {
            var project = new Project();
            var rec = NewRecordWithEntry("06-001", "Riss ohne Marker");
            rec.Protocol!.Current.Entries.Add(new ProtocolEntry { Code = "BCD", Beschreibung = "Anfang" });
            project.AddRecord(rec);

            var result = new ProjectPhotoAssignmentService().AssignFromFolder(root, ext, project);

            Assert.Equal(1, result.HoldingsMatched);
            Assert.Equal(2, result.PhotosAssigned);
            Assert.Equal(2, rec.Protocol!.Current.Entries[0].FotoPaths.Count); // an erste Beobachtung
        }
        finally { TryDelete(root); TryDelete(ext); }
    }

    private static HaltungRecord NewRecordWithEntry(string holding, string beschreibung)
    {
        var rec = new HaltungRecord();
        rec.SetFieldValue("Haltungsname", holding, FieldSource.Xtf, userEdited: false);
        rec.Protocol = new ProtocolDocument();
        rec.Protocol.Current.Entries.Add(new ProtocolEntry { Code = "BAB", Beschreibung = beschreibung });
        return rec;
    }

    private static string NewDir()
    {
        var d = Path.Combine(Path.GetTempPath(), $"pa-{Guid.NewGuid():N}");
        Directory.CreateDirectory(d);
        return d;
    }

    private static void TryDelete(string d)
    {
        try { if (Directory.Exists(d)) Directory.Delete(d, recursive: true); } catch { }
    }
}
