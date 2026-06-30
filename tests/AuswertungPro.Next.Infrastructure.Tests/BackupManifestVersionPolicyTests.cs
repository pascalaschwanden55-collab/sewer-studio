using AuswertungPro.Next.Application.Ai.Backup;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Charakterisierungstests fuer BackupManifestVersionPolicy.
/// Prueft Kompatibilitaetspruefung und Fehlermeldungsformat.
/// </summary>
public class BackupManifestVersionPolicyTests
{
    [Fact]
    public void IsCompatible_GleicheVersion_Kompatibel()
    {
        Assert.True(BackupManifestVersionPolicy.IsCompatible(BackupManifestVersionPolicy.CurrentVersion));
    }

    [Fact]
    public void IsCompatible_AeltereVersion_Kompatibel()
    {
        Assert.True(BackupManifestVersionPolicy.IsCompatible(1));
    }

    [Fact]
    public void IsCompatible_NeuereVersion_NichtKompatibel()
    {
        Assert.False(BackupManifestVersionPolicy.IsCompatible(BackupManifestVersionPolicy.CurrentVersion + 1));
    }

    [Fact]
    public void IsCompatible_Version0_Kompatibel()
    {
        Assert.True(BackupManifestVersionPolicy.IsCompatible(0));
    }

    [Fact]
    public void CurrentVersion_IstZwei()
    {
        // Manifest-Version ist ein Vertrag — aendert sich nur absichtlich
        Assert.Equal(2, BackupManifestVersionPolicy.CurrentVersion);
    }

    [Fact]
    public void FormatIncompatibleMessage_EnthaeltVersionshinweis()
    {
        var msg = BackupManifestVersionPolicy.FormatIncompatibleMessage(5);

        Assert.Contains("5", msg);
        Assert.Contains(BackupManifestVersionPolicy.CurrentVersion.ToString(), msg);
    }

    [Fact]
    public void FormatIncompatibleMessage_IstNichtLeer()
    {
        var msg = BackupManifestVersionPolicy.FormatIncompatibleMessage(99);

        Assert.False(string.IsNullOrWhiteSpace(msg));
    }
}
