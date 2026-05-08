using System;
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Infrastructure.Import.WinCan;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Unit-Tests fuer InspectionProfileExtractor.
/// Prueft ParseTimeCtr, BuildCanonicalCode, BuildProfile, BuildSegments und BuildQualityFlags.
/// </summary>
[Trait("Category", "Unit")]
public class InspectionProfileExtractorTests
{
    // =========================================================================
    // ParseTimeCtr
    // =========================================================================

    [Fact]
    public void ParseTimeCtr_Standardformat_GibtKorrekteSekundenZurueck()
    {
        // "00:01:30.00" = 90 Sekunden
        double? result = InspectionProfileExtractor.ParseTimeCtr("00:01:30.00");
        Assert.NotNull(result);
        Assert.Equal(90.0, result!.Value, precision: 2);
    }

    [Fact]
    public void ParseTimeCtr_MitStunden_GibtKorrekteSekundenZurueck()
    {
        // "01:00:00.00" = 3600 Sekunden
        double? result = InspectionProfileExtractor.ParseTimeCtr("01:00:00.00");
        Assert.NotNull(result);
        Assert.Equal(3600.0, result!.Value, precision: 2);
    }

    [Fact]
    public void ParseTimeCtr_MitHundertstel_GibtKorrekteSekundenZurueck()
    {
        // "00:00:05.50" = 5.5 Sekunden
        double? result = InspectionProfileExtractor.ParseTimeCtr("00:00:05.50");
        Assert.NotNull(result);
        Assert.Equal(5.5, result!.Value, precision: 2);
    }

    [Fact]
    public void ParseTimeCtr_Null_GibtNullZurueck()
    {
        double? result = InspectionProfileExtractor.ParseTimeCtr(null);
        Assert.Null(result);
    }

    [Fact]
    public void ParseTimeCtr_LeerString_GibtNullZurueck()
    {
        double? result = InspectionProfileExtractor.ParseTimeCtr("");
        Assert.Null(result);
    }

    [Fact]
    public void ParseTimeCtr_NurLeerzeichen_GibtNullZurueck()
    {
        double? result = InspectionProfileExtractor.ParseTimeCtr("   ");
        Assert.Null(result);
    }

    [Fact]
    public void ParseTimeCtr_UngueltigesFormat_GibtNullZurueck()
    {
        double? result = InspectionProfileExtractor.ParseTimeCtr("kein-zeitformat");
        Assert.Null(result);
    }

    // =========================================================================
    // BuildCanonicalCode
    // =========================================================================

    [Fact]
    public void BuildCanonicalCode_MitChar1UndChar2_GibtKorrekteCodes()
    {
        // "BAB" + "B" + "A" → codeMain="BAB", codeFull="BABBA"
        var (codeMain, codeFull) = InspectionProfileExtractor.BuildCanonicalCode("BAB", "B", "A");
        Assert.Equal("BAB", codeMain);
        Assert.Equal("BABBA", codeFull);
    }

    [Fact]
    public void BuildCanonicalCode_OhneChars_CodeMainGleichCodeFull()
    {
        // "BCD" + null + null → codeMain="BCD", codeFull="BCD"
        var (codeMain, codeFull) = InspectionProfileExtractor.BuildCanonicalCode("BCD", null, null);
        Assert.Equal("BCD", codeMain);
        Assert.Equal("BCD", codeFull);
    }

    [Fact]
    public void BuildCanonicalCode_NurChar1_CodeFullHatEinZusatz()
    {
        // "BCA" + "A" + null → codeMain="BCA", codeFull="BCAA"
        var (codeMain, codeFull) = InspectionProfileExtractor.BuildCanonicalCode("BCA", "A", null);
        Assert.Equal("BCA", codeMain);
        Assert.Equal("BCAA", codeFull);
    }

    [Fact]
    public void BuildCanonicalCode_Kleinbuchstaben_WerdenzuGrossUmgewandelt()
    {
        var (codeMain, codeFull) = InspectionProfileExtractor.BuildCanonicalCode("bab", "b", "a");
        Assert.Equal("BAB", codeMain);
        Assert.Equal("BABBA", codeFull);
    }

    // =========================================================================
    // BuildProfile
    // =========================================================================

    [Fact]
    public void BuildProfile_SortiertEventeNachZeit()
    {
        // Events unsortiert uebergeben — Ergebnis muss aufsteigend sein
        var rawEvents = new List<RawEvent>
        {
            new("BCD", null, null, 0.0, 30.0, null, null, null, null, null, 0),
            new("BAB", "A", null, 5.0, 10.0, null, null, null, null, null, 1),
            new("BCE", null, null, 40.0, 60.0, null, null, null, null, null, 2),
        };

        var profile = InspectionProfileExtractor.BuildProfile(
            "TEST-001", 40.0, rawEvents, null, 0.0);

        Assert.Equal(3, profile.Ereignisse.Count);
        Assert.Equal(10.0, profile.Ereignisse[0].ZeitSek); // BAB zuerst
        Assert.Equal(30.0, profile.Ereignisse[1].ZeitSek); // dann BCD
        Assert.Equal(60.0, profile.Ereignisse[2].ZeitSek); // dann BCE
    }

    [Fact]
    public void BuildProfile_BerechnetDauerKorrekt()
    {
        var rawEvents = new List<RawEvent>
        {
            new("BCD", null, null, 0.0,  0.0, null, null, null, null, null, 0),
            new("BCE", null, null, 30.0, 120.0, null, null, null, null, null, 1),
        };

        var profile = InspectionProfileExtractor.BuildProfile(
            "TEST-002", 30.0, rawEvents, null, 0.0);

        // Dauer = 120 - 0 = 120 Sekunden
        Assert.Equal(120.0, profile.DauerSekunden, precision: 1);
    }

    [Fact]
    public void BuildProfile_ErkennungsLuecken()
    {
        // Zwei Events mit 10 Sekunden Abstand → Luecke (> 5s Schwellwert)
        var rawEvents = new List<RawEvent>
        {
            new("BCD", null, null,  0.0, 0.0,  null, null, null, null, null, 0),
            new("BAB", "A", null,  5.0, 60.0, null, null, null, null, null, 1),  // 60s spaeter = Luecke
            new("BCE", null, null, 30.0, 70.0, null, null, null, null, null, 2), // 10s spaeter = keine Luecke
        };

        var profile = InspectionProfileExtractor.BuildProfile(
            "TEST-003", 30.0, rawEvents, null, 0.0);

        Assert.NotEmpty(profile.Luecken);
        // Erste Luecke: 0s → 60s, Dauer 60s
        Assert.Equal(60.0, profile.Luecken[0].DauerSek, precision: 1);
    }

    [Fact]
    public void BuildProfile_BerechnetFahrgeschwindigkeit()
    {
        // Kamera faehrt gleichmaessig: 20m in 40 Sekunden = 0.5 m/s
        var rawEvents = new List<RawEvent>
        {
            new("BCD", null, null,  0.0,  0.0, null, null, null, null, null, 0),
            new("BAB", "A", null, 10.0, 20.0, null, null, null, null, null, 1),
            new("BAB", "B", null, 20.0, 40.0, null, null, null, null, null, 2),
            new("BCE", null, null, 30.0, 60.0, null, null, null, null, null, 3),
        };

        var profile = InspectionProfileExtractor.BuildProfile(
            "TEST-004", 30.0, rawEvents, null, 0.0);

        Assert.NotNull(profile.Statistik.FahrgeschwindigkeitMS);
        Assert.True(profile.Statistik.FahrgeschwindigkeitMS > 0);
    }

    // =========================================================================
    // BuildSegments
    // =========================================================================

    [Fact]
    public void BuildSegments_VorBcdIstSchacht()
    {
        // Drei Events: erster vor BCD → erstes Segment = "schacht"
        var events = new List<ProfileEvent>
        {
            new(ZeitSek: 0.0,  Meter: null, CodeMain: "UNK", CodeFull: "UNK",
                Char1: null, Char2: null, Uhr1: null, Uhr2: null,
                Q1: null, Streckenlaenge: null, Bemerkung: null),
            new(ZeitSek: 5.0,  Meter: 0.0, CodeMain: "BCD", CodeFull: "BCD",
                Char1: null, Char2: null, Uhr1: null, Uhr2: null,
                Q1: null, Streckenlaenge: null, Bemerkung: null),
            new(ZeitSek: 10.0, Meter: 5.0, CodeMain: "BAB", CodeFull: "BABAA",
                Char1: "A", Char2: "A", Uhr1: null, Uhr2: null,
                Q1: null, Streckenlaenge: null, Bemerkung: null),
        };

        var segmente = InspectionProfileExtractor.BuildSegments(events);

        Assert.NotEmpty(segmente);
        Assert.Equal("schacht", segmente[0].Typ);
    }

    [Fact]
    public void BuildSegments_NachBcdMitDistanzIstAxialFahrt()
    {
        // Zwei Events nach BCD mit Distanz-Aenderung → axial_fahrt
        var events = new List<ProfileEvent>
        {
            new(ZeitSek: 0.0,  Meter: 0.0,  CodeMain: "BCD", CodeFull: "BCD",
                Char1: null, Char2: null, Uhr1: null, Uhr2: null,
                Q1: null, Streckenlaenge: null, Bemerkung: null),
            new(ZeitSek: 5.0,  Meter: 5.0,  CodeMain: "BAB", CodeFull: "BABAA",
                Char1: "A", Char2: "A", Uhr1: null, Uhr2: null,
                Q1: null, Streckenlaenge: null, Bemerkung: null),
            new(ZeitSek: 10.0, Meter: 15.0, CodeMain: "BBC", CodeFull: "BBCA",
                Char1: "A", Char2: null, Uhr1: null, Uhr2: null,
                Q1: null, Streckenlaenge: null, Bemerkung: null),
        };

        var segmente = InspectionProfileExtractor.BuildSegments(events);

        // Segment 0 = uebergang (BCD-Fenster), Segment 1 = axial_fahrt
        Assert.Equal(2, segmente.Count);
        Assert.Equal("axial_fahrt", segmente[1].Typ);
    }

    [Fact]
    public void BuildSegments_StillstandBeiNahezu0MeterUndMehr2Sekunden()
    {
        // Zwei Events: gleicher Meterstand, 3s Pause → stillstand
        var events = new List<ProfileEvent>
        {
            new(ZeitSek: 5.0,  Meter: 0.0,  CodeMain: "BCD", CodeFull: "BCD",
                Char1: null, Char2: null, Uhr1: null, Uhr2: null,
                Q1: null, Streckenlaenge: null, Bemerkung: null),
            new(ZeitSek: 10.0, Meter: 10.0, CodeMain: "BAB", CodeFull: "BABAA",
                Char1: "A", Char2: "A", Uhr1: null, Uhr2: null,
                Q1: null, Streckenlaenge: null, Bemerkung: null),
            new(ZeitSek: 13.0, Meter: 10.01, CodeMain: "BBA", CodeFull: "BBA",
                Char1: null, Char2: null, Uhr1: null, Uhr2: null,
                Q1: null, Streckenlaenge: null, Bemerkung: null),
        };

        var segmente = InspectionProfileExtractor.BuildSegments(events);

        // Letztes Segment soll Stillstand sein (delta < 0.05m, delta_time > 2s)
        var letztes = segmente[^1];
        Assert.Equal("stillstand", letztes.Typ);
    }

    // =========================================================================
    // BuildQualityFlags
    // =========================================================================

    [Fact]
    public void BuildQualityFlags_MissingBcd_WennKeinBcdEvent()
    {
        var events = new List<ProfileEvent>
        {
            new(ZeitSek: 0.0,  Meter: 0.0,  CodeMain: "BAB", CodeFull: "BABAA",
                Char1: "A", Char2: "A", Uhr1: null, Uhr2: null,
                Q1: null, Streckenlaenge: null, Bemerkung: null),
            new(ZeitSek: 10.0, Meter: 5.0,  CodeMain: "BCE", CodeFull: "BCE",
                Char1: null, Char2: null, Uhr1: null, Uhr2: null,
                Q1: null, Streckenlaenge: null, Bemerkung: null),
        };

        var flags = InspectionProfileExtractor.BuildQualityFlags(events, "video.mpg", 30.0);

        Assert.True(flags.MissingBcd);
        Assert.False(flags.MissingBce);
    }

    [Fact]
    public void BuildQualityFlags_NonMonotonicDistance_BeiRueckwaeртsFahrt()
    {
        // Events mit abnehmenden Meterangaben → non_monotonic_distance
        var events = new List<ProfileEvent>
        {
            new(ZeitSek: 0.0,  Meter: 0.0,  CodeMain: "BCD", CodeFull: "BCD",
                Char1: null, Char2: null, Uhr1: null, Uhr2: null,
                Q1: null, Streckenlaenge: null, Bemerkung: null),
            new(ZeitSek: 5.0,  Meter: 10.0, CodeMain: "BAB", CodeFull: "BABAA",
                Char1: "A", Char2: "A", Uhr1: null, Uhr2: null,
                Q1: null, Streckenlaenge: null, Bemerkung: null),
            new(ZeitSek: 10.0, Meter: 5.0,  CodeMain: "BBC", CodeFull: "BBCA",
                Char1: "A", Char2: null, Uhr1: null, Uhr2: null,
                Q1: null, Streckenlaenge: null, Bemerkung: null), // Meter faellt zurueck!
        };

        var flags = InspectionProfileExtractor.BuildQualityFlags(events, "video.mpg", 30.0);

        Assert.True(flags.NonMonotonicDistance);
    }

    [Fact]
    public void BuildQualityFlags_MissingVideo_WennKeinPfad()
    {
        var events = new List<ProfileEvent>
        {
            new(ZeitSek: 0.0, Meter: 0.0, CodeMain: "BCD", CodeFull: "BCD",
                Char1: null, Char2: null, Uhr1: null, Uhr2: null,
                Q1: null, Streckenlaenge: null, Bemerkung: null),
        };

        var flags = InspectionProfileExtractor.BuildQualityFlags(events, null, 30.0);

        Assert.True(flags.MissingVideo);
    }

    [Fact]
    public void BuildQualityFlags_FewEvents_WengerAlsDreiEvents()
    {
        var events = new List<ProfileEvent>
        {
            new(ZeitSek: 0.0,  Meter: 0.0,  CodeMain: "BCD", CodeFull: "BCD",
                Char1: null, Char2: null, Uhr1: null, Uhr2: null,
                Q1: null, Streckenlaenge: null, Bemerkung: null),
            new(ZeitSek: 10.0, Meter: 30.0, CodeMain: "BCE", CodeFull: "BCE",
                Char1: null, Char2: null, Uhr1: null, Uhr2: null,
                Q1: null, Streckenlaenge: null, Bemerkung: null),
        };

        var flags = InspectionProfileExtractor.BuildQualityFlags(events, "video.mpg", 30.0);

        Assert.True(flags.FewEvents);
    }

    [Fact]
    public void BuildQualityFlags_AllesOk_KeineFlagsGesetzt()
    {
        var events = new List<ProfileEvent>
        {
            new(ZeitSek: 0.0,  Meter: 0.0,  CodeMain: "BCD", CodeFull: "BCD",
                Char1: null, Char2: null, Uhr1: null, Uhr2: null,
                Q1: null, Streckenlaenge: null, Bemerkung: null),
            new(ZeitSek: 5.0,  Meter: 5.0,  CodeMain: "BAB", CodeFull: "BABAA",
                Char1: "A", Char2: "A", Uhr1: null, Uhr2: null,
                Q1: null, Streckenlaenge: null, Bemerkung: null),
            new(ZeitSek: 10.0, Meter: 10.0, CodeMain: "BBC", CodeFull: "BBCA",
                Char1: "A", Char2: null, Uhr1: null, Uhr2: null,
                Q1: null, Streckenlaenge: null, Bemerkung: null),
            new(ZeitSek: 15.0, Meter: 30.0, CodeMain: "BCE", CodeFull: "BCE",
                Char1: null, Char2: null, Uhr1: null, Uhr2: null,
                Q1: null, Streckenlaenge: null, Bemerkung: null),
        };

        var flags = InspectionProfileExtractor.BuildQualityFlags(events, "video.mpg", 30.0);

        Assert.False(flags.MissingBcd);
        Assert.False(flags.MissingBce);
        Assert.False(flags.NonMonotonicDistance);
        Assert.False(flags.NonMonotonicTime);
        Assert.False(flags.MissingVideo);
        Assert.False(flags.FewEvents);
    }

    // =========================================================================
    // Integrationstest — echte DB3 (nur lokal, Skip auf CI)
    // =========================================================================

    private const string TestDb3 = @"G:\GEP_Altdorf_2025_Zone_1.15_29261_925_Export\GEP_Altdorf_2025_Zone_1.15_29261_925_Export\DISK1\Projects\GEP_Altdorf_2025_Zone_1.15_29261_925\DB\GEP_Altdorf_2025_Zone_1.15_29261_925.db3";

    [Fact]
    public void Integration_ExtractFromDb3_EchterExport()
    {
        if (!System.IO.File.Exists(TestDb3))
        {
            // Skip auf CI / anderer Rechner
            return;
        }

        var profiles = InspectionProfileExtractor.ExtractFromDb3(TestDb3);

        // Mindestens 30 Profile (92 Haltungen, nicht alle haben TimeCtr)
        Assert.True(profiles.Count >= 30, $"Nur {profiles.Count} Profile extrahiert");

        // Bekannte Haltung pruefen
        var p = profiles.FirstOrDefault(x => x.HaltungKey == "80638-80631")
             ?? profiles[0]; // Fallback
        Assert.True(p.Ereignisse.Count >= 2, $"Nur {p.Ereignisse.Count} Events");
        Assert.True(p.Segmente.Count >= 1, $"Keine Segmente");
        Assert.NotNull(p.Statistik.FahrgeschwindigkeitMS);
        Assert.True(p.Statistik.FahrgeschwindigkeitMS > 0);

        // BCD muss dabei sein
        Assert.Contains(p.Ereignisse, e => e.CodeMain == "BCD");

        // Profile speichern
        var outDir = @"C:\KI_BRAIN\inspection_profiles";
        InspectionProfileExtractor.SaveProfiles(profiles, outDir);
        Assert.True(System.IO.Directory.Exists(outDir));

        // Aggregieren
        var patterns = InspectionPatternAggregator.Aggregate(profiles);
        InspectionPatternAggregator.SavePatterns(patterns, @"C:\KI_BRAIN\inspection_patterns.json");

        Assert.True(patterns.AnzahlHaltungen >= 30);
        Assert.True(patterns.MedianFahrgeschwindigkeit > 0);
        Assert.True(patterns.UebergangsMatrix.Count > 0);
        Assert.True(patterns.SequenzRegeln.Count >= 2);
    }
}
