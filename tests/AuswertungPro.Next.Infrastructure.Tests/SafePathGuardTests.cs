using System.IO;
using AuswertungPro.Next.Infrastructure.Ai.Backup;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Charakterisierungstests fuer SafePathGuard.IsSafeToDelete.
/// Prueft Temp-Verzeichnis-Bedingung und "backup"-Bedingung.
/// </summary>
public class SafePathGuardTests
{
    // ── Erlaubte Pfade (Temp + "backup" im Namen) ────────────────────

    [Fact]
    public void SafeToDelete_TempMitBackupImNamen_Erlaubt()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), "sewerstudio_import_backup_20260601_120000");

        Assert.True(SafePathGuard.IsSafeToDelete(tempPath));
    }

    [Fact]
    public void SafeToDelete_TempMitBackupInMischschreibung_Erlaubt()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), "SewerStudio_BACKUP_xyz");

        Assert.True(SafePathGuard.IsSafeToDelete(tempPath));
    }

    // ── Gesperrte Pfade ─────────────────────────────────────────────

    [Fact]
    public void SafeToDelete_AusserHalbTemp_Gesperrt()
    {
        // Pfad liegt nicht im Temp-Verzeichnis
        var path = Path.Combine("C:", "KI_BRAIN", "backup");

        Assert.False(SafePathGuard.IsSafeToDelete(path));
    }

    [Fact]
    public void SafeToDelete_TempOhneBackupImNamen_Gesperrt()
    {
        // Im Temp-Verzeichnis, aber "backup" fehlt im Namen
        var tempPath = Path.Combine(Path.GetTempPath(), "sewerstudio_import_20260601");

        Assert.False(SafePathGuard.IsSafeToDelete(tempPath));
    }

    [Fact]
    public void SafeToDelete_NullEingabe_Gesperrt()
    {
        Assert.False(SafePathGuard.IsSafeToDelete(null));
    }

    [Fact]
    public void SafeToDelete_LeerString_Gesperrt()
    {
        Assert.False(SafePathGuard.IsSafeToDelete(""));
    }

    [Fact]
    public void SafeToDelete_NurLeerzeichen_Gesperrt()
    {
        Assert.False(SafePathGuard.IsSafeToDelete("   "));
    }

    [Fact]
    public void SafeToDelete_SystemRoot_NieErlaubt()
    {
        // C:\ darf nie geloescht werden
        Assert.False(SafePathGuard.IsSafeToDelete("C:\\"));
    }
}
