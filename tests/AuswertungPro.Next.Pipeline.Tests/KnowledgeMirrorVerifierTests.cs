using System;
using System.IO;
using AuswertungPro.Next.Application.Maintenance;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

public class KnowledgeMirrorVerifierTests : IDisposable
{
    private readonly string _tempDir;

    public KnowledgeMirrorVerifierTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"mirror_verify_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    [Fact]
    public void ComputeSha256_ProducesStableHash()
    {
        var f = Path.Combine(_tempDir, "data.bin");
        File.WriteAllBytes(f, new byte[] { 1, 2, 3, 4, 5 });

        var hash1 = KnowledgeMirrorVerifier.ComputeSha256(f);
        var hash2 = KnowledgeMirrorVerifier.ComputeSha256(f);
        Assert.Equal(hash1, hash2);
        Assert.Equal(64, hash1.Length); // SHA256 = 32 bytes = 64 hex chars
    }

    [Fact]
    public void Verify_NoManifest_ReturnsNoManifestStatus()
    {
        var dbPath = Path.Combine(_tempDir, "KnowledgeBase.db");
        File.WriteAllBytes(dbPath, new byte[] { 1, 2, 3 });

        var result = KnowledgeMirrorVerifier.Verify(dbPath);

        Assert.Equal(VerificationStatus.NoManifest, result.Status);
        Assert.False(result.IsRestoreSafe);
    }

    [Fact]
    public void Verify_MirrorMissing_ReturnsMirrorMissing()
    {
        var dbPath = Path.Combine(_tempDir, "KnowledgeBase.db");
        var result = KnowledgeMirrorVerifier.Verify(dbPath);

        Assert.Equal(VerificationStatus.MirrorMissing, result.Status);
        Assert.False(result.IsRestoreSafe);
    }

    [Fact]
    public void Verify_HashMatch_ReturnsOk()
    {
        var dbPath = Path.Combine(_tempDir, "KnowledgeBase.db");
        File.WriteAllBytes(dbPath, new byte[] { 1, 2, 3, 4, 5 });

        var hash = KnowledgeMirrorVerifier.ComputeSha256(dbPath);
        var bytes = new FileInfo(dbPath).Length;
        KnowledgeMirrorVerifier.WriteManifest(_tempDir, "KnowledgeBase.db", hash, bytes);

        var result = KnowledgeMirrorVerifier.Verify(dbPath);

        Assert.Equal(VerificationStatus.Ok, result.Status);
        Assert.True(result.IsRestoreSafe);
    }

    [Fact]
    public void Verify_HashMismatch_ReturnsHashMismatch()
    {
        var dbPath = Path.Combine(_tempDir, "KnowledgeBase.db");
        File.WriteAllBytes(dbPath, new byte[] { 1, 2, 3, 4, 5 });

        // Manifest mit anderem Hash schreiben (simuliert Korruption)
        KnowledgeMirrorVerifier.WriteManifest(_tempDir, "KnowledgeBase.db", "0000abc", 5);

        var result = KnowledgeMirrorVerifier.Verify(dbPath);

        Assert.Equal(VerificationStatus.HashMismatch, result.Status);
        Assert.False(result.IsRestoreSafe);
        Assert.Contains("Mismatch", result.Message);
    }

    [Fact]
    public void Verify_SizeMismatch_ReturnsSizeMismatch()
    {
        var dbPath = Path.Combine(_tempDir, "KnowledgeBase.db");
        File.WriteAllBytes(dbPath, new byte[] { 1, 2, 3, 4, 5 });

        // Hash korrekt, aber Bytes-Wert im Manifest falsch
        var hash = KnowledgeMirrorVerifier.ComputeSha256(dbPath);
        KnowledgeMirrorVerifier.WriteManifest(_tempDir, "KnowledgeBase.db", hash, 999_999);

        var result = KnowledgeMirrorVerifier.Verify(dbPath);

        Assert.Equal(VerificationStatus.SizeMismatch, result.Status);
        Assert.False(result.IsRestoreSafe);
    }

    [Fact]
    public void WriteManifest_RoundTrip()
    {
        KnowledgeMirrorVerifier.WriteManifest(_tempDir, "test.db", "abc123", 12345);

        var manifest = KnowledgeMirrorVerifier.ReadManifest(_tempDir);

        Assert.NotNull(manifest);
        Assert.Equal("test.db", manifest!.FileName);
        Assert.Equal("abc123", manifest.Sha256);
        Assert.Equal(12345, manifest.Bytes);
    }

    [Fact]
    public void ReadManifest_CorruptFile_ReturnsNull()
    {
        File.WriteAllText(Path.Combine(_tempDir, "manifest.json"), "{ broken json");

        var manifest = KnowledgeMirrorVerifier.ReadManifest(_tempDir);
        Assert.Null(manifest);
    }
}
