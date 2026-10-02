namespace AuswertungPro.Next.Infrastructure.Tests.Backup;

/// <summary>
/// AP-4: Die echten Verknuepfungstests schuetzen Dateioperationen davor, ueber
/// Directory-Links hinweg fremde Daten zu lesen oder zu loeschen. Werden sie alle
/// still uebersprungen, waere ein gruener Lauf keine Sicherheitspruefung. Deshalb
/// werden sowohl ihr Bestand als auch die Testfaehigkeit der Umgebung sichtbar bewacht.
/// </summary>
public sealed class JunctionCapabilityGateTests
{
    [Fact]
    public void Alle_Verknuepfungsschutztests_sind_registriert_und_ausfuehrbar()
    {
        const int expectedJunctionFacts = 108; // Zusaetzlich sieben Schutzfaelle der Haltungsumbenennung;
                                              // zwei fuer «Ordner wird zur Verknuepfung» (Audit A01, 23.09.2026);
                                              // einer fuer die WebGIS-Berichtsablage (C3, 24.09.2026);
                                              // einer fuer den Papierkorb des Verteilabgleichs (GA02, 28.09.2026);
                                              // elf fuer gemeldete uebersprungene Ordner (R1, 02.10.2026);
                                              // einer fuer den Eval-Schutzordner (Deepscan A1/R2, 02.10.2026).

        var actualJunctionFacts = typeof(JunctionCapabilityGateTests).Assembly
            .GetTypes()
            .SelectMany(type => type.GetMethods())
            .Count(method => method.CustomAttributes.Any(attribute =>
                attribute.AttributeType == typeof(JunctionFactAttribute)));

        Assert.Equal(expectedJunctionFacts, actualJunctionFacts);

        var grund = JunctionTestSupport.UnavailableReason;
        if (grund is null)
            return;

        var erlaubt = Environment.GetEnvironmentVariable("SEWER_ALLOW_JUNCTION_SKIP");
        Assert.True(
            string.Equals(erlaubt, "1", StringComparison.Ordinal),
            $"Alle {expectedJunctionFacts} Junction-Schutztests werden uebersprungen: {grund} "
            + "Entweder den Windows-Entwicklermodus einschalten oder "
            + "SEWER_ALLOW_JUNCTION_SKIP=1 bewusst setzen.");
    }
}
