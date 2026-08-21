using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Versioning;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using AuswertungPro.Next.Infrastructure.Tests.Backup;
using AuswertungPro.Next.Infrastructure.Common;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Tests fuer SafeFileEnumeration. Schwerpunkt: ein gesperrter/verschwundener Unterordner
/// darf den ganzen Lauf NICHT abbrechen (Lazy-Throw-Fix — der Zugriffsfehler entsteht erst
/// beim Iterieren, muss aber gefangen und der Ordner uebersprungen werden).
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class SafeFileEnumerationTests
{
    [JunctionFact]
    public void EnumerateFilesSafe_BetrittKeineVerzeichnisVerknuepfung_UndMeldetSieAlsUebersprungen()
    {
        var testRoot = Path.Combine(Path.GetTempPath(), "sfe_link_" + Guid.NewGuid().ToString("N"));
        var root = Path.Combine(testRoot, "root");
        var normal = Path.Combine(root, "a-normal");
        var external = Path.Combine(testRoot, "external");
        var link = Path.Combine(root, "b-link");
        var cycleLink = Path.Combine(normal, "cycle-to-root");
        var fileLink = Path.Combine(root, "c-file-link.txt");

        try
        {
            Directory.CreateDirectory(normal);
            Directory.CreateDirectory(external);
            File.WriteAllText(Path.Combine(root, "root.txt"), "root");
            File.WriteAllText(Path.Combine(normal, "normal.txt"), "normal");
            File.WriteAllText(Path.Combine(external, "fremd.txt"), "fremd");
            JunctionTestSupport.CreateDirectoryLink(link, external);
            JunctionTestSupport.CreateDirectoryLink(cycleLink, root);
            File.CreateSymbolicLink(fileLink, Path.Combine(external, "fremd.txt"));

            var skipped = new List<string>();

            var files = SafeFileEnumeration
                .EnumerateFilesSafe(root, "*.txt", recursive: true, skippedDirectories: skipped)
                .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
                .ToList();
            var directories = SafeFileEnumeration
                .EnumerateDirectoriesSafe(root)
                .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
                .ToList();
            var explicitlySelectedLinkRootFiles = SafeFileEnumeration
                .EnumerateFilesSafe(link, "*.txt", recursive: true)
                .Select(Path.GetFileName)
                .ToList();

            Assert.Equal(new[] { "root.txt", "a-normal/normal.txt" }, files);
            Assert.Equal(new[] { ".", "a-normal" }, directories);
            Assert.Equal(new[] { "fremd.txt" }, explicitlySelectedLinkRootFiles);
            Assert.Contains(link, skipped, StringComparer.OrdinalIgnoreCase);
            Assert.Contains(cycleLink, skipped, StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            try
            {
                if (Directory.Exists(cycleLink))
                    Directory.Delete(cycleLink);
                if (Directory.Exists(link))
                    Directory.Delete(link);
                if (File.Exists(fileLink))
                    File.Delete(fileLink);
            }
            catch
            {
                // best-effort cleanup
            }

            try
            {
                if (Directory.Exists(testRoot))
                    Directory.Delete(testRoot, recursive: true);
            }
            catch
            {
                // best-effort cleanup
            }
        }
    }

    [Fact]
    public void EnumerateFilesSafe_UeberspringtGesperrtenUnterordner_OhneAbbruch()
    {
        var root = Path.Combine(Path.GetTempPath(), "sfe_" + Guid.NewGuid().ToString("N"));
        var okDir = Path.Combine(root, "ok");
        var lockedDir = Path.Combine(root, "locked");
        Directory.CreateDirectory(okDir);
        Directory.CreateDirectory(lockedDir);
        File.WriteAllText(Path.Combine(okDir, "ok.txt"), "x");
        File.WriteAllText(Path.Combine(lockedDir, "secret.txt"), "y");

        var denyApplied = TryDenyEnumerate(lockedDir);
        try
        {
            var skipped = new List<string>();
            // Darf NICHT werfen, auch wenn ein Unterordner gesperrt ist.
            var files = SafeFileEnumeration
                .EnumerateFilesSafe(root, "*", recursive: true, skippedDirectories: skipped)
                .ToList();

            // Erreichbare Datei kommt immer durch.
            Assert.Contains(files, f => f.EndsWith("ok.txt", StringComparison.OrdinalIgnoreCase));

            // Nur wenn die Sperre auf diesem System tatsaechlich greift, wird der Skip geprueft
            // (kein Fehlalarm auf Umgebungen, die die Deny-ACL nicht durchsetzen).
            if (denyApplied && DenyIsEffective(lockedDir))
            {
                Assert.Equal(new[] { "ok.txt" }, files.Select(Path.GetFileName).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray());
                Assert.Contains(skipped, d => d.EndsWith("locked", StringComparison.OrdinalIgnoreCase));
            }
        }
        finally
        {
            TryRemoveDeny(lockedDir);
            try { Directory.Delete(root, recursive: true); } catch { /* best-effort cleanup */ }
        }
    }

    [Fact]
    public void EnumerateFilesSafe_NichtExistierenderRoot_GibtLeereListe()
    {
        var missing = Path.Combine(Path.GetTempPath(), "sfe_missing_" + Guid.NewGuid().ToString("N"));
        Assert.Empty(SafeFileEnumeration.EnumerateFilesSafe(missing, "*", recursive: true).ToList());
    }

    [Fact]
    public void EnumerateFilesSafe_Rekursiv_FindetDateienInUnterordnern()
    {
        var root = Path.Combine(Path.GetTempPath(), "sfe_" + Guid.NewGuid().ToString("N"));
        var sub = Path.Combine(root, "a", "b");
        Directory.CreateDirectory(sub);
        File.WriteAllText(Path.Combine(root, "top.txt"), "1");
        File.WriteAllText(Path.Combine(sub, "deep.txt"), "2");
        try
        {
            var files = SafeFileEnumeration.EnumerateFilesSafe(root, "*.txt", recursive: true).ToList();
            Assert.Contains(files, f => f.EndsWith("top.txt", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(files, f => f.EndsWith("deep.txt", StringComparison.OrdinalIgnoreCase));

            var flat = SafeFileEnumeration.EnumerateFilesSafe(root, "*.txt", recursive: false).ToList();
            Assert.Equal(new[] { "top.txt" }, flat.Select(Path.GetFileName).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray());
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }

    [SupportedOSPlatform("windows")]
    private static SecurityIdentifier CurrentUser() => WindowsIdentity.GetCurrent().User!;

    private static readonly FileSystemRights EnumerateRights =
        FileSystemRights.ListDirectory | FileSystemRights.ReadData | FileSystemRights.Traverse;

    [SupportedOSPlatform("windows")]
    private static bool TryDenyEnumerate(string dir)
    {
        try
        {
            var di = new DirectoryInfo(dir);
            var sec = di.GetAccessControl();
            sec.AddAccessRule(new FileSystemAccessRule(CurrentUser(), EnumerateRights, AccessControlType.Deny));
            di.SetAccessControl(sec);
            return true;
        }
        catch { return false; }
    }

    [SupportedOSPlatform("windows")]
    private static void TryRemoveDeny(string dir)
    {
        try
        {
            var di = new DirectoryInfo(dir);
            var sec = di.GetAccessControl();
            sec.RemoveAccessRuleAll(new FileSystemAccessRule(CurrentUser(), EnumerateRights, AccessControlType.Deny));
            di.SetAccessControl(sec);
        }
        catch { /* best-effort */ }
    }

    private static bool DenyIsEffective(string dir)
    {
        try { _ = new List<string>(Directory.EnumerateFileSystemEntries(dir)); return false; }
        catch { return true; }
    }
}
