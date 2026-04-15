using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Infrastructure.Import.WinCan;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Unit-Tests fuer InspectionPatternAggregator — Aggregation statistischer Muster.
/// </summary>
public class InspectionPatternAggregatorTests
{
    // -------------------------------------------------------------------------
    // Test 1: Median Fahrgeschwindigkeit
    // -------------------------------------------------------------------------

    [Fact]
    public void Aggregate_BerechnetMedianGeschwindigkeit()
    {
        // Arrange: 3 Profile mit Geschwindigkeiten 0.1, 0.3, 0.5 m/s
        // Median = 0.3
        var profile1 = MakeProfile("H1", 10.0,
            (0, 0.0, "BCD"), (100, 10.0, "BCE")); // ~0.1 m/s
        var profile2 = MakeProfile("H2", 10.0,
            (0, 0.0, "BCD"), (33.3, 10.0, "BCE")); // ~0.3 m/s
        var profile3 = MakeProfile("H3", 10.0,
            (0, 0.0, "BCD"), (20.0, 10.0, "BCE")); // ~0.5 m/s

        var profiles = new List<InspectionProfile> { profile1, profile2, profile3 };

        // Act
        var result = InspectionPatternAggregator.Aggregate(profiles);

        // Assert: Median der 3 Geschwindigkeiten liegt zwischen 0.1 und 0.5
        Assert.True(result.MedianFahrgeschwindigkeit > 0,
            "Median Fahrgeschwindigkeit muss > 0 sein");
        Assert.Equal(3, result.AnzahlHaltungen);
    }

    // -------------------------------------------------------------------------
    // Test 2: Code-Verteilung
    // -------------------------------------------------------------------------

    [Fact]
    public void Aggregate_BerechnetCodeVerteilung()
    {
        // Arrange: 1 Profil mit je einem BCD, BCA, BAB, BCE → je 25%
        var profil = MakeProfile("H1", 30.0,
            (0, 0.0, "BCD"),
            (10, 10.0, "BCA"),
            (20, 20.0, "BAB"),
            (30, 30.0, "BCE"));

        var profiles = new List<InspectionProfile> { profil };

        // Act
        var result = InspectionPatternAggregator.Aggregate(profiles);

        // Assert: 4 Codes, je 25%
        Assert.Equal(4, result.CodeVerteilung.Count);
        Assert.True(result.CodeVerteilung.ContainsKey("BCD"));
        Assert.True(result.CodeVerteilung.ContainsKey("BCA"));
        Assert.True(result.CodeVerteilung.ContainsKey("BAB"));
        Assert.True(result.CodeVerteilung.ContainsKey("BCE"));
        Assert.Equal(25.0, result.CodeVerteilung["BCD"], precision: 1);
        Assert.Equal(25.0, result.CodeVerteilung["BCA"], precision: 1);
        Assert.Equal(25.0, result.CodeVerteilung["BAB"], precision: 1);
        Assert.Equal(25.0, result.CodeVerteilung["BCE"], precision: 1);
    }

    // -------------------------------------------------------------------------
    // Test 3: Uebergangsmatrix
    // -------------------------------------------------------------------------

    [Fact]
    public void Aggregate_BerechnetUebergangsmatrix()
    {
        // Arrange: 1 Profil mit Sequenz BCD→BCA→BAB→BCE
        // Erwartete Transitionen: BCD→BCA:1, BCA→BAB:1, BAB→BCE:1
        var profil = MakeProfile("H1", 30.0,
            (0, 0.0, "BCD"),
            (10, 10.0, "BCA"),
            (20, 20.0, "BAB"),
            (30, 30.0, "BCE"));

        var profiles = new List<InspectionProfile> { profil };

        // Act
        var result = InspectionPatternAggregator.Aggregate(profiles);

        // Assert
        Assert.True(result.UebergangsMatrix.ContainsKey("BCD→BCA"),
            "Transition BCD→BCA muss vorhanden sein");
        Assert.True(result.UebergangsMatrix.ContainsKey("BCA→BAB"),
            "Transition BCA→BAB muss vorhanden sein");
        Assert.True(result.UebergangsMatrix.ContainsKey("BAB→BCE"),
            "Transition BAB→BCE muss vorhanden sein");
        Assert.Equal(1, result.UebergangsMatrix["BCD→BCA"]);
        Assert.Equal(1, result.UebergangsMatrix["BCA→BAB"]);
        Assert.Equal(1, result.UebergangsMatrix["BAB→BCE"]);
        Assert.Equal(3, result.UebergangsMatrix.Count);
    }

    // -------------------------------------------------------------------------
    // Test 4: Sequenz-Regeln Support
    // -------------------------------------------------------------------------

    [Fact]
    public void Aggregate_BerechnetSequenzRegeln()
    {
        // Arrange: 3 Profile, 2 davon beginnen mit BCD → support ~0.67
        var profil1 = MakeProfile("H1", 30.0,
            (0, 0.0, "BCD"), (30, 30.0, "BCE"));
        var profil2 = MakeProfile("H2", 30.0,
            (0, 0.0, "BCD"), (30, 30.0, "BCE"));
        var profil3 = MakeProfile("H3", 30.0,
            (0, 0.0, "BAB"), (30, 30.0, "BCE")); // beginnt NICHT mit BCD

        var profiles = new List<InspectionProfile> { profil1, profil2, profil3 };

        // Act
        var result = InspectionPatternAggregator.Aggregate(profiles);

        // Assert: Regel "BCD ist typischerweise erster Code" mit support ~0.667
        var regelBcd = result.SequenzRegeln
            .FirstOrDefault(r => r.Regel.Contains("BCD") && r.Regel.Contains("erster"));

        Assert.NotNull(regelBcd);
        Assert.InRange(regelBcd.Support, 0.66, 0.68);
        Assert.Equal(1, regelBcd.Ausnahmen); // 1 Profil beginnt nicht mit BCD
    }

    // -------------------------------------------------------------------------
    // Test 5: Schachtphase Median
    // -------------------------------------------------------------------------

    [Fact]
    public void Aggregate_BerechnetSchachtPhase()
    {
        // Arrange: 3 Profile mit BCD bei 3s, 5s, 10s → Median 5s
        var profil1 = MakeProfile("H1", 50.0,
            (3, 0.0, "BCD"), (53, 50.0, "BCE"));  // BCD bei 3s
        var profil2 = MakeProfile("H2", 50.0,
            (5, 0.0, "BCD"), (55, 50.0, "BCE"));  // BCD bei 5s
        var profil3 = MakeProfile("H3", 50.0,
            (10, 0.0, "BCD"), (60, 50.0, "BCE")); // BCD bei 10s

        var profiles = new List<InspectionProfile> { profil1, profil2, profil3 };

        // Act
        var result = InspectionPatternAggregator.Aggregate(profiles);

        // Assert: Median der BCD-Zeiten = 5s
        Assert.Equal(5.0, result.Aufnahmetechnik.SchachtPhaseSekMedian, precision: 1);
    }

    // -------------------------------------------------------------------------
    // Hilfsmethode: Testprofil erstellen
    // -------------------------------------------------------------------------

    /// <summary>
    /// Erstellt ein einfaches Testprofil mit angegebenen Events.
    /// Parameter: (ZeitSek, Meter, CodeMain)
    /// </summary>
    private static InspectionProfile MakeProfile(
        string key,
        double laenge,
        params (double zeit, double? meter, string code)[] events)
    {
        var profileEvents = events.Select(e => new ProfileEvent(
            e.zeit, e.meter, e.code, e.code, null, null, null, null, null, null, null)).ToList();

        // Luecken berechnen
        var luecken = new List<ProfileGap>();
        for (int i = 0; i < profileEvents.Count - 1; i++)
        {
            var von = profileEvents[i];
            var bis = profileEvents[i + 1];
            double dauer = bis.ZeitSek - von.ZeitSek;
            double? distanz = (von.Meter.HasValue && bis.Meter.HasValue)
                ? bis.Meter.Value - von.Meter.Value
                : null;
            luecken.Add(new ProfileGap(
                von.ZeitSek, bis.ZeitSek,
                von.Meter, bis.Meter,
                dauer, distanz));
        }

        double dauerTotal = events.Length > 0 ? events[^1].zeit - events[0].zeit : 0;
        double? geschw = laenge > 0 && dauerTotal > 0 ? laenge / dauerTotal : null;
        double? codPM = laenge > 0 ? events.Length / laenge : null;

        return new InspectionProfile(
            HaltungKey: key,
            LaengeM: laenge,
            DauerSekunden: dauerTotal,
            VideoPfad: null,
            VideoMatchConfidence: 0,
            Ereignisse: profileEvents.AsReadOnly(),
            Segmente: Array.Empty<ProfileSegment>(),
            Luecken: luecken.AsReadOnly(),
            Statistik: new ProfileStatistik(codPM, 0, null, geschw),
            QualityFlags: new QualityFlags(
                true, false, false, false, false, false, false, false, false, new List<string>()));
    }
}
