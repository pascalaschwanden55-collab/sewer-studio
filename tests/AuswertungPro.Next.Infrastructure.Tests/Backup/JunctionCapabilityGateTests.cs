namespace AuswertungPro.Next.Infrastructure.Tests.Backup;

/// <summary>
/// AP-4 (Audit 2026-08-10): Die 13 [JunctionFact]-Tests schuetzen Spiegelung und
/// Vollsicherung davor, ueber eine Verzeichnis-Verknuepfung hinweg fremde Dateien
/// zu loeschen. Werden sie still uebersprungen, faerbt sich der Lauf gruen, ohne
/// dass der Schutz je lief — ein kaputtes IsReparsePoint bliebe lokal unsichtbar.
/// Dieser Gate meldet ein fehlendes Verknuepfungsrecht einmal hart, statt es
/// 13-mal still zu ueberspringen.
/// </summary>
public sealed class JunctionCapabilityGateTests
{
    [Fact]
    public void Verknuepfungsrecht_ist_vorhanden_oder_ausdruecklich_erlaubt()
    {
        var grund = JunctionTestSupport.UnavailableReason;
        if (grund is null)
            return; // Recht vorhanden — die 13 Schutztests laufen wirklich.

        var erlaubt = Environment.GetEnvironmentVariable("SEWER_ALLOW_JUNCTION_SKIP");
        Assert.True(
            string.Equals(erlaubt, "1", StringComparison.Ordinal),
            $"Junction-Schutztests werden uebersprungen: {grund} "
            + "Entweder den Windows-Entwicklermodus einschalten (dann laufen alle 13 "
            + "wirklich), oder SEWER_ALLOW_JUNCTION_SKIP=1 setzen, um das Ueberspringen "
            + "ausdruecklich zu erlauben.");
    }
}
