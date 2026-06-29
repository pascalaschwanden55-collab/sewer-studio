using System.IO;
using AuswertungPro.Next.Application.Ai.Backup;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Charakterisierungstests fuer FramePathRemapper.
/// Prueft RemapPathToLocal (Annotationen) und RemapFramePath (TrainingSamples).
/// fileExists wird als Delegate uebergeben — kein echter Dateizugriff noetig.
/// </summary>
public class FramePathRemapperTests
{
    private readonly string _localDir = Path.Combine("C:", "KI_BRAIN", "frames");
    private readonly string _localImagesDir = Path.Combine("C:", "KI_BRAIN", "teacher_images");

    // ── RemapPathToLocal ─────────────────────────────────────────────

    [Fact]
    public void RemapPathToLocal_FremdPfad_WirdRemappedWennLokalVorhanden()
    {
        // Fremder Pfad zeigt auf anderen Rechner
        var fremdPfad = Path.Combine("D:", "OldMachine", "teacher_images", "frame001.jpg");
        var erwartet  = Path.Combine(_localImagesDir, "frame001.jpg");

        // fileExists gibt true nur fuer den lokalen Pfad
        var result = FramePathRemapper.RemapPathToLocal(
            fremdPfad,
            _localImagesDir,
            p => string.Equals(p, erwartet, System.StringComparison.OrdinalIgnoreCase));

        Assert.Equal(erwartet, result);
    }

    [Fact]
    public void RemapPathToLocal_GleicherPfad_GibtNull()
    {
        var localPath = Path.Combine(_localImagesDir, "frame001.jpg");

        // Pfad ist bereits lokal — kein Remap noetig
        var result = FramePathRemapper.RemapPathToLocal(
            localPath,
            _localImagesDir,
            p => true);

        Assert.Null(result);
    }

    [Fact]
    public void RemapPathToLocal_DateiExistiertNichtLokal_GibtNull()
    {
        var fremdPfad = Path.Combine("D:", "OldMachine", "teacher_images", "missing.jpg");

        var result = FramePathRemapper.RemapPathToLocal(
            fremdPfad,
            _localImagesDir,
            _ => false); // Datei existiert nirgends lokal

        Assert.Null(result);
    }

    [Fact]
    public void RemapPathToLocal_NullEingabe_GibtNull()
    {
        var result = FramePathRemapper.RemapPathToLocal(null, _localImagesDir, _ => true);
        Assert.Null(result);
    }

    [Fact]
    public void RemapPathToLocal_LeerString_GibtNull()
    {
        var result = FramePathRemapper.RemapPathToLocal("", _localImagesDir, _ => true);
        Assert.Null(result);
    }

    [Fact]
    public void RemapPathToLocal_UnterverzeichnisWirdBehalten()
    {
        // Datei liegt in crops/ Unterordner
        var fremdPfad = Path.Combine("D:", "OldMachine", "teacher_images", "crops", "crop001.jpg");
        var erwartetSub = Path.Combine(_localImagesDir, "crops", "crop001.jpg");

        // Direkter Pfad existiert NICHT, Unterverzeichnis schon
        var result = FramePathRemapper.RemapPathToLocal(
            fremdPfad,
            _localImagesDir,
            p => string.Equals(p, erwartetSub, System.StringComparison.OrdinalIgnoreCase));

        Assert.Equal(erwartetSub, result);
    }

    // ── RemapFramePath ───────────────────────────────────────────────

    [Fact]
    public void RemapFramePath_FremdPfad_WirdRemapped()
    {
        var fremdPfad = Path.Combine("E:", "Brain", "frames", "frame0042.png");
        var erwartet  = Path.Combine(_localDir, "frame0042.png");

        var result = FramePathRemapper.RemapFramePath(
            fremdPfad,
            _localDir,
            p => string.Equals(p, erwartet, System.StringComparison.OrdinalIgnoreCase));

        Assert.Equal(erwartet, result);
    }

    [Fact]
    public void RemapFramePath_BereitsLokal_GibtNull()
    {
        var lokalerPfad = Path.Combine(_localDir, "frame0042.png");

        var result = FramePathRemapper.RemapFramePath(
            lokalerPfad,
            _localDir,
            _ => true);

        Assert.Null(result);
    }

    [Fact]
    public void RemapFramePath_DateiExistiertNicht_GibtNull()
    {
        var fremdPfad = Path.Combine("E:", "Brain", "frames", "ghost.png");

        var result = FramePathRemapper.RemapFramePath(
            fremdPfad,
            _localDir,
            _ => false);

        Assert.Null(result);
    }

    [Fact]
    public void RemapFramePath_NullEingabe_GibtNull()
    {
        var result = FramePathRemapper.RemapFramePath(null, _localDir, _ => true);
        Assert.Null(result);
    }
}
