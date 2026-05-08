using System.IO;
using AuswertungPro.Next.Application.Common;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Tests fuer SEC-H4 Path-Traversal-Schutz in <see cref="ProjectPathResolver.SanitizePathSegment"/>.
/// Schuetzt gegen Path-Traversal-Attacken via Haltungs-IDs aus externen Quellen
/// (manipulierte WinCan-DB / Import-XML / Mail-Anhaenge).
/// </summary>
[Trait("Category", "Unit")]
public class ProjectPathResolverTests
{
    [Theory]
    [InlineData("Haltung-1", "Haltung-1")]
    [InlineData("123-Anschluss/A1", "123-Anschluss_A1")]
    [InlineData("normaler_name", "normaler_name")]
    public void SanitizePathSegment_NormalInput_PassesThrough(string input, string expected)
    {
        Assert.Equal(expected, ProjectPathResolver.SanitizePathSegment(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SanitizePathSegment_NullOrEmpty_ReturnsUnknown(string? input)
    {
        Assert.Equal("UNKNOWN", ProjectPathResolver.SanitizePathSegment(input));
    }

    [Theory]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("...")] // Drei Punkte → trimmEnd entfernt alle, dann leer
    [InlineData(". ")]  // mit trailing space → trimmt sich auf "." → UNKNOWN
    public void SanitizePathSegment_DotOnly_ReturnsUnknown(string input)
    {
        Assert.Equal("UNKNOWN", ProjectPathResolver.SanitizePathSegment(input));
    }

    [Theory]
    [InlineData("..foo", "_foo")]    // führendes ".." → "_foo"
    [InlineData("foo..bar", "foo_bar")] // eingebettetes ".." → "foo_bar"
    [InlineData("a..b..c", "a_b_c")]    // mehrere "..": alle ersetzen
    public void SanitizePathSegment_DoubleDotEmbedded_ReplacedWithUnderscore(string input, string expected)
    {
        Assert.Equal(expected, ProjectPathResolver.SanitizePathSegment(input));
    }

    [Theory]
    [InlineData("foo.")]      // Trailing-Dot — Windows-"foo." Alias
    [InlineData("foo. ")]     // Trailing-Dot + Space
    [InlineData("foo  ")]     // Trailing-Spaces
    public void SanitizePathSegment_TrailingDotsAndSpaces_AreTrimmed(string input)
    {
        var result = ProjectPathResolver.SanitizePathSegment(input);
        // Trailing-Dot/Space wurde getrimmed — kein Windows-Alias-Trick
        Assert.False(result.EndsWith('.'));
        Assert.False(result.EndsWith(' '));
    }

    [Fact]
    public void SanitizePathSegment_InvalidFileNameChars_ReplacedWithUnderscore()
    {
        // ":" ist invalid, "?" ist invalid, "<" ist invalid
        var result = ProjectPathResolver.SanitizePathSegment("foo:bar?<x>");
        Assert.DoesNotContain(":", result);
        Assert.DoesNotContain("?", result);
        Assert.DoesNotContain("<", result);
    }

    [Fact]
    public void ResolveContainedFile_WithinProjectFolder_Returns()
    {
        // Setup: Temp-Projektordner mit einer Datei
        var tempDir = Path.Combine(Path.GetTempPath(), $"sewerstudio_test_{System.Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var fileName = "test.txt";
            var fullPath = Path.Combine(tempDir, fileName);
            File.WriteAllText(fullPath, "test");

            var result = ProjectPathResolver.ResolveContainedFile(fileName, tempDir);
            Assert.Equal(Path.GetFullPath(fullPath), result);
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    [Fact]
    public void ResolveContainedFile_OutsideProjectFolder_ReturnsNull()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"sewerstudio_test_{System.Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            // ".." sprengt aus dem Projektordner heraus.
            var maliciousRelative = Path.Combine("..", "..", "..", "windows", "system32", "kernel32.dll");
            var result = ProjectPathResolver.ResolveContainedFile(maliciousRelative, tempDir);
            Assert.Null(result);
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    [Fact]
    public void ResolveContainedFile_NonExistentFile_ReturnsNull()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"sewerstudio_test_{System.Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var result = ProjectPathResolver.ResolveContainedFile("does_not_exist.txt", tempDir);
            Assert.Null(result);
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    [Fact]
    public void IsRelative_AbsolutePath_False()
    {
        Assert.False(ProjectPathResolver.IsRelative(@"C:\Users\test"));
        Assert.False(ProjectPathResolver.IsRelative("/absolute/unix/path"));
    }

    [Fact]
    public void IsRelative_RelativePath_True()
    {
        Assert.True(ProjectPathResolver.IsRelative("foo.txt"));
        Assert.True(ProjectPathResolver.IsRelative(@"sub\file.txt"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsRelative_NullOrEmpty_False(string? input)
    {
        Assert.False(ProjectPathResolver.IsRelative(input));
    }
}
