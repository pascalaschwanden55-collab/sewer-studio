using System.IO;
using System.Linq;
using System.Text.Json;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.QgisBridge;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class QgisBridgeSnapshotBuilderTests
{
    private const string MiniXtf = @"<?xml version='1.0' encoding='UTF-8'?>
<TRANSFER><DATASECTION><X>
  <a.Haltung TID='h1'><Bezeichnung>A-B</Bezeichnung>
    <Verlauf><POLYLINE><COORD><C1>2690000</C1><C2>1190000</C2></COORD>
    <COORD><C1>2690010</C1><C2>1190000</C2></COORD></POLYLINE></Verlauf>
  </a.Haltung>
  <a.Abwasserknoten TID='k1'><Bezeichnung>S1</Bezeichnung>
    <Lage><COORD><C1>2690100</C1><C2>1190000</C2></COORD></Lage>
  </a.Abwasserknoten>
  <a.Abwasserknoten TID='k2'><Bezeichnung>S2</Bezeichnung>
    <Lage><COORD><C1>2690100</C1><C2>1190020</C2></COORD></Lage>
  </a.Abwasserknoten></X></DATASECTION></TRANSFER>";

    [Fact]
    public void BuildStatus_reports_live_project_and_xtf_counts()
    {
        using var fixture = QgisBridgeFixture.Create();
        var sut = fixture.CreateBuilder();
        var snapshot = QgisProjectSnapshot.Capture(CreateProjectWithImportedDamage(), "A-B", selectionStamp: 7);

        var status = sut.BuildStatus(snapshot);

        Assert.True(status.Ok);
        Assert.Equal("A-B", status.CurrentHolding);
        Assert.Equal(7, status.SelectionStamp);
        Assert.Equal(1, status.ProjectHoldingCount);
        Assert.Equal(1, status.NetworkFeatureCount);
        Assert.Equal(1, status.DamageFeatureCount);
        Assert.True(status.XtfFound);
    }

    [Fact]
    public void BuildCurrentGeoJson_exports_selected_holding_as_wgs84_line()
    {
        using var fixture = QgisBridgeFixture.Create();
        var sut = fixture.CreateBuilder();
        var snapshot = QgisProjectSnapshot.Capture(CreateProjectWithImportedDamage(), "A-B");

        var geoJson = sut.BuildCurrentGeoJson(snapshot);

        var feature = Assert.Single(geoJson.Features);
        Assert.Equal("A-B", feature.Properties["haltung"]);
        Assert.Equal(1, feature.Properties["schaden_count"]);
        var line = Assert.IsType<GeoJsonLineString>(feature.Geometry);
        Assert.Equal(2, line.Coordinates.Length);
        // LV95 unveraendert (EPSG:2056): exakt die Kataster-Koordinaten, keine Umrechnung.
        Assert.Equal(2690000, line.Coordinates[0][0], precision: 3);
        Assert.Equal(1190000, line.Coordinates[0][1], precision: 3);
    }

    [Fact]
    public void BuildDamagesGeoJson_projects_damage_meter_to_holding_geometry()
    {
        using var fixture = QgisBridgeFixture.Create();
        var sut = fixture.CreateBuilder();
        var snapshot = QgisProjectSnapshot.Capture(CreateProjectWithImportedDamage(), null);

        var geoJson = sut.BuildDamagesGeoJson(snapshot);

        var feature = Assert.Single(geoJson.Features);
        Assert.Equal("A-B", feature.Properties["haltung"]);
        Assert.Equal("BAB", feature.Properties["code"]);
        Assert.Equal("import", feature.Properties["source"]);
        var point = Assert.IsType<GeoJsonPoint>(feature.Geometry);
        // Schaden bei 5 m auf der 10-m-Linie (2690000..2690010) => exakt bei 2690005.
        Assert.Equal(2690005, point.Coordinates[0], precision: 3);
        Assert.Equal(1190000, point.Coordinates[1], precision: 3);
    }

    [Fact]
    public void BuildDamagesGeoJson_prefers_protocol_entries_over_imported_findings()
    {
        using var fixture = QgisBridgeFixture.Create();
        var sut = fixture.CreateBuilder();
        var project = CreateProjectWithImportedDamage();
        var record = project.Data[0];
        record.Protocol = new ProtocolDocument
        {
            HaltungId = "A-B",
            Current = new ProtocolRevision
            {
                Entries =
                {
                    new ProtocolEntry
                    {
                        Code = "BBA",
                        Beschreibung = "Wurzeln im Rohr",
                        MeterStart = 2.0,
                        MeterEnd = 8.0,
                        IsStreckenschaden = true
                    },
                    new ProtocolEntry { Code = "GELOESCHT", MeterStart = 1.0, IsDeleted = true }
                }
            }
        };

        var geoJson = sut.BuildDamagesGeoJson(QgisProjectSnapshot.Capture(project, null));

        // Nur der aktive Protokolleintrag zaehlt: importierte Findings und
        // geloeschte Eintraege duerfen nicht doppelt erscheinen.
        var feature = Assert.Single(geoJson.Features);
        Assert.Equal("BBA", feature.Properties["code"]);
        Assert.Equal("Wurzeln im Rohr", feature.Properties["beschreibung"]);
        Assert.Equal("protokoll", feature.Properties["source"]);
        Assert.Equal(true, feature.Properties["streckenschaden"]);

        // Streckenschaden 2-8 m wird als Mittelpunkt (5 m) auf der 10-m-Linie verortet.
        var point = Assert.IsType<GeoJsonPoint>(feature.Geometry);
        Assert.Equal(2690005, point.Coordinates[0], precision: 3);
    }

    [Fact]
    public void BuildDamagesGeoJson_misst_gegen_fliessrichtung_vom_anderen_ende()
    {
        using var fixture = QgisBridgeFixture.Create();
        var sut = fixture.CreateBuilder();
        var project = CreateProjectWithImportedDamage();
        project.Data[0].VsaFindings[0].MeterStart = 2.0;
        project.Data[0].SetFieldValue("Inspektionsrichtung", "Gegen Fliessrichtung", FieldSource.Manual, userEdited: true);

        var geoJson = sut.BuildDamagesGeoJson(QgisProjectSnapshot.Capture(project, null));

        // Aufnahme startete am "nach"-Schacht: 2 m ab Polyline-ENDE der 10-m-Linie => bei 2690008.
        var feature = Assert.Single(geoJson.Features);
        Assert.Equal("gegen_fliessrichtung", feature.Properties["richtung"]);
        var point = Assert.IsType<GeoJsonPoint>(feature.Geometry);
        Assert.Equal(2690008, point.Coordinates[0], precision: 3);
    }

    [Fact]
    public void Umgekehrt_benannte_haltung_findet_geometrie_und_misst_vom_anderen_ende()
    {
        using var fixture = QgisBridgeFixture.Create();
        var sut = fixture.CreateBuilder();

        // Kataster kennt "A-B" (Fliessrichtung); die Aufnahme lief von B nach A,
        // darum heisst die Haltung im Projekt "B-A".
        var project = new Project { Name = "Richtung-Test" };
        var record = new HaltungRecord();
        record.SetFieldValue("Haltungsname", "B-A", FieldSource.Manual, userEdited: true);
        record.VsaFindings.Add(new VsaFinding { KanalSchadencode = "BAB", MeterStart = 2.0 });
        project.Data.Add(record);

        var snapshot = QgisProjectSnapshot.Capture(project, "B-A");
        var damages = sut.BuildDamagesGeoJson(snapshot);
        var current = sut.BuildCurrentGeoJson(snapshot);

        var feature = Assert.Single(damages.Features);
        Assert.Equal("B-A", feature.Properties["haltung"]);
        Assert.Equal("A-B", feature.Properties["haltung_kataster"]);
        Assert.Equal("gegen_fliessrichtung", feature.Properties["richtung"]);
        var point = Assert.IsType<GeoJsonPoint>(feature.Geometry);
        Assert.Equal(2690008, point.Coordinates[0], precision: 3);

        // Auch die "aktuelle Haltung" muss ueber den umgekehrten Namen gefunden werden.
        var currentFeature = Assert.Single(current.Features);
        Assert.Equal("A-B", currentFeature.Properties["haltung_kataster"]);
    }

    [Fact]
    public void Haltung_ohne_katasterkante_faellt_auf_schachtlinie_zurueck()
    {
        using var fixture = QgisBridgeFixture.Create();
        var sut = fixture.CreateBuilder();

        // "S1-S2" existiert im Kataster nicht als Haltung — aber beide Schaechte
        // haben Koordinaten. Erwartet: gerade Naeherungslinie S1 -> S2 (20 m).
        var project = new Project { Name = "Fallback-Test" };
        var record = new HaltungRecord();
        record.SetFieldValue("Haltungsname", "S1-S2", FieldSource.Manual, userEdited: true);
        record.VsaFindings.Add(new VsaFinding { KanalSchadencode = "BAB", MeterStart = 5.0 });
        project.Data.Add(record);
        var snapshot = QgisProjectSnapshot.Capture(project, "S1-S2");

        var current = sut.BuildCurrentGeoJson(snapshot);
        var damages = sut.BuildDamagesGeoJson(snapshot);
        var status = sut.BuildStatus(snapshot);

        var line = Assert.Single(current.Features);
        Assert.Equal("schacht_naeherung", line.Properties["geometrie_quelle"]);
        Assert.Null(line.Properties["haltung_kataster"]);

        // Schaden bei 5 m auf der 20-m-Linie (1190000 -> 1190020) => bei 1190005.
        var damage = Assert.Single(damages.Features);
        var point = Assert.IsType<GeoJsonPoint>(damage.Geometry);
        Assert.Equal(2690100, point.Coordinates[0], precision: 3);
        Assert.Equal(1190005, point.Coordinates[1], precision: 3);

        Assert.True(status.CurrentHoldingHasGeometry);
        Assert.Equal(1, status.DamageFeatureCount);
    }

    [Fact]
    public void Status_meldet_fehlende_geometrie_der_aktuellen_haltung()
    {
        using var fixture = QgisBridgeFixture.Create();
        var sut = fixture.CreateBuilder();
        var project = CreateProjectWithImportedDamage();

        var status = sut.BuildStatus(QgisProjectSnapshot.Capture(project, "GIBTSNICHT-99"));

        Assert.False(status.CurrentHoldingHasGeometry);
    }

    [Fact]
    public void Richtungswechsel_invalidiert_den_damages_fingerprint()
    {
        var before = QgisProjectSnapshot.Capture(CreateProjectWithImportedDamage(), null).DamagesFingerprint(1);

        var project = CreateProjectWithImportedDamage();
        project.Data[0].SetFieldValue("Inspektionsrichtung", "Gegen Fliessrichtung", FieldSource.Manual, userEdited: true);
        var after = QgisProjectSnapshot.Capture(project, null).DamagesFingerprint(1);

        Assert.NotEqual(before, after);
    }

    [Fact]
    public void BuildGeoJson_serializes_geometry_types_for_qgis()
    {
        using var fixture = QgisBridgeFixture.Create();
        var sut = fixture.CreateBuilder();
        var snapshot = QgisProjectSnapshot.Capture(CreateProjectWithImportedDamage(), "A-B");
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        var currentJson = JsonSerializer.Serialize(sut.BuildCurrentGeoJson(snapshot), options);
        var damagesJson = JsonSerializer.Serialize(sut.BuildDamagesGeoJson(snapshot), options);

        Assert.Contains("\"type\":\"FeatureCollection\"", currentJson);
        Assert.Contains("\"type\":\"LineString\"", currentJson);
        Assert.Contains("\"coordinates\"", currentJson);
        Assert.Contains("\"type\":\"Point\"", damagesJson);

        // CRS-Angabe: QGIS muss die Koordinaten als LV95 (EPSG:2056) interpretieren.
        Assert.Contains("\"crs\"", currentJson);
        Assert.Contains("EPSG::2056", currentJson);
        Assert.Contains("EPSG::2056", damagesJson);
    }

    [Fact]
    public void Capture_falls_back_to_findings_when_protocol_only_has_deleted_entries()
    {
        var project = CreateProjectWithImportedDamage();
        var record = project.Data[0];
        record.Protocol = new ProtocolDocument
        {
            HaltungId = "A-B",
            Current = new ProtocolRevision
            {
                Entries = { new ProtocolEntry { Code = "X", MeterStart = 1.0, IsDeleted = true } }
            }
        };

        var snapshot = QgisProjectSnapshot.Capture(project, null);

        var haltung = Assert.Single(snapshot.Haltungen);
        var damage = Assert.Single(haltung.Schaeden);
        Assert.Equal("import", damage.Source);
        Assert.Equal("BAB", damage.Code);
    }

    [Fact]
    public void Teilstrecken_suffix_findet_die_durchgehende_katasterhaltung()
    {
        using var fixture = QgisBridgeFixture.Create();
        var sut = fixture.CreateBuilder();

        // Kataster kennt nur "A-B"; SewerStudio nummeriert eine Zweit-/Teilaufnahme als "A-B.1".
        // Erwartet: dieselbe Kataster-Geometrie, damit QGIS auf die Haltung zoomen kann.
        var project = new Project { Name = "Teilstrecke" };
        var record = new HaltungRecord();
        record.SetFieldValue("Haltungsname", "A-B.1", FieldSource.Manual, userEdited: true);
        record.VsaFindings.Add(new VsaFinding { KanalSchadencode = "BAB", MeterStart = 5.0 });
        project.Data.Add(record);
        var snapshot = QgisProjectSnapshot.Capture(project, "A-B.1");

        var status = sut.BuildStatus(snapshot);
        var current = sut.BuildCurrentGeoJson(snapshot);

        Assert.True(status.CurrentHoldingHasGeometry);
        var feature = Assert.Single(current.Features);
        Assert.Equal("A-B.1", feature.Properties["haltung"]);
        // Geometrie stammt aus der durchgehenden Kataster-Haltung "A-B".
        Assert.Equal("A-B", feature.Properties["haltung_kataster"]);
        var line = Assert.IsType<GeoJsonLineString>(feature.Geometry);
        Assert.Equal(2, line.Coordinates.Length);
        Assert.Equal(2690000, line.Coordinates[0][0], precision: 3);
    }

    [Theory]
    [InlineData("22836-21687.1", "22836-21687")] // Teilstrecke .1 -> durchgehende Basis-Haltung
    [InlineData("A-B.99", "A-B")]                // zweistellige Laufnummer gilt noch als Suffix
    [InlineData("22836-7.32154", null)]          // echter Kataster-Knoten (lange Nummer) bleibt unberuehrt
    [InlineData("1089398-22542", null)]          // gar kein Punkt-Suffix
    [InlineData("A-B", null)]                     // kein Suffix
    [InlineData("A-B.100", null)]                // dreistellig -> keine Teilstrecken-Laufnummer
    [InlineData("", null)]
    [InlineData(null, null)]
    public void TryStripSubsectionSuffix_erkennt_nur_kurze_laufnummern(string? input, string? expected)
    {
        Assert.Equal(expected, QgisBridgeSnapshotBuilder.TryStripSubsectionSuffix(input));
    }

    [Fact]
    public void BuildCurrentGeoJson_liefert_nutzungsart_fuer_regelbasierte_einfaerbung()
    {
        using var fixture = QgisBridgeFixture.Create();
        var sut = fixture.CreateBuilder();
        var project = CreateProjectWithImportedDamage();
        project.Data[0].SetFieldValue("Nutzungsart", "Mischabwasser", FieldSource.Manual, userEdited: true);
        var snapshot = QgisProjectSnapshot.Capture(project, "A-B");

        var current = sut.BuildCurrentGeoJson(snapshot);

        // Nutzungsart wird mitgeliefert, damit QGIS die aktuelle Haltung regelbasiert
        // nach Nutzungsart einfaerben kann (analog Leitungen-Layer).
        var feature = Assert.Single(current.Features);
        Assert.Equal("Mischabwasser", feature.Properties["nutzungsart"]);
    }

    [Fact]
    public void BuildSanierungstypGeoJson_nur_kategorisierte_haltungen_mit_nr_und_kategorie()
    {
        using var fixture = QgisBridgeFixture.Create();
        var sut = fixture.CreateBuilder();

        var project = new Project { Name = "Ausgefuehrt-durch-Test" };
        project.Data.Add(Haltung("A-B", ausgefuehrt: "Baumeister", nr: "1"));      // Kataster-Kante
        project.Data.Add(Haltung("S1-S2", ausgefuehrt: "Kanalsanierer", nr: "2")); // Schacht-Fallback
        project.Data.Add(Haltung("S2-S1", ausgefuehrt: "", nr: "3"));              // Geometrie ok, aber kein Ausfuehrender -> raus

        var geoJson = sut.BuildSanierungstypGeoJson(QgisProjectSnapshot.Capture(project, null));

        Assert.Equal(2, geoJson.Features.Count);

        var baumeister = geoJson.Features.Single(f => (string?)f.Properties["haltung"] == "A-B");
        Assert.Equal("Baumeister", baumeister.Properties["ausgefuehrt_durch"]);
        Assert.Equal("1", baumeister.Properties["nr"]);
        Assert.IsType<GeoJsonLineString>(baumeister.Geometry);

        // "Kanalsanierer" faellt kanonisch auf "Sanierer".
        var sanierer = geoJson.Features.Single(f => (string?)f.Properties["haltung"] == "S1-S2");
        Assert.Equal("Sanierer", sanierer.Properties["ausgefuehrt_durch"]);
        Assert.Equal("2", sanierer.Properties["nr"]);

        // Haltung ohne Ausfuehrenden erscheint nicht.
        Assert.DoesNotContain(geoJson.Features, f => (string?)f.Properties["haltung"] == "S2-S1");
    }

    [Fact]
    public void BuildSchachtSanierungstypGeoJson_nur_kategorisierte_schaechte_als_punkte()
    {
        using var fixture = QgisBridgeFixture.Create();
        var sut = fixture.CreateBuilder();

        var project = new Project { Name = "Schacht-Ausgefuehrt-durch-Test" };
        project.SchaechteData.Add(Schacht("S1", ausgefuehrt: "Baumeister", nr: "1"));       // Kataster-Knoten -> Punkt
        project.SchaechteData.Add(Schacht("S2", ausgefuehrt: "", nr: "2"));                 // im Kataster, aber kein Ausfuehrender -> raus
        project.SchaechteData.Add(Schacht("KS99", ausgefuehrt: "Kanalsanierer", nr: "3"));  // Ausfuehrender, aber nicht im Kataster -> raus

        var geo = sut.BuildSchachtSanierungstypGeoJson(QgisProjectSnapshot.Capture(project, null));

        var feature = Assert.Single(geo.Features);
        Assert.Equal("S1", feature.Properties["schacht"]);
        Assert.Equal("Baumeister", feature.Properties["ausgefuehrt_durch"]);
        Assert.Equal("1", feature.Properties["nr"]);
        Assert.IsType<GeoJsonPoint>(feature.Geometry);
    }

    [Fact]
    public void Ausgefuehrt_durch_aenderung_invalidiert_den_schacht_sanierungstyp_fingerprint()
    {
        var before = QgisProjectSnapshot.Capture(SchachtProjekt("S1", null), null).SchachtSanierungstypFingerprint(1);
        var after = QgisProjectSnapshot.Capture(SchachtProjekt("S1", "Baumeister"), null).SchachtSanierungstypFingerprint(1);

        Assert.NotEqual(before, after);
    }

    [Fact]
    public void BuildDamagesGeoJson_streckt_meter_bis_rohrende_auf_end_schacht()
    {
        using var fixture = QgisBridgeFixture.Create();
        var sut = fixture.CreateBuilder();

        var project = new Project { Name = "Skalierung" };
        var record = new HaltungRecord();
        record.SetFieldValue("Haltungsname", "A-B", FieldSource.Manual, userEdited: true);
        // Inspektion: Rohrende (BCE) bei 7.3 m; ein Schaden bei 3.65 m (= halbe Survey-Strecke).
        record.VsaFindings.Add(new VsaFinding { KanalSchadencode = "BCE", MeterStart = 7.3 });
        record.VsaFindings.Add(new VsaFinding { KanalSchadencode = "BAB", MeterStart = 3.65 });
        project.Data.Add(record);

        var geoJson = sut.BuildDamagesGeoJson(QgisProjectSnapshot.Capture(project, null));

        // Kataster-Linie A-B ist 10 m lang (2690000..2690010).
        // 3.65 m von 7.3 m Survey = 50% => 5 m => 2690005 (statt unskaliert 2690003.65).
        var schaden = geoJson.Features.Single(f => (string?)f.Properties["code"] == "BAB");
        Assert.Equal(2690005, Assert.IsType<GeoJsonPoint>(schaden.Geometry).Coordinates[0], precision: 3);

        // Rohrende (7.3 m) faellt exakt auf das Geometrie-Ende (10 m) => 2690010 (End-Schacht).
        var rohrende = geoJson.Features.Single(f => (string?)f.Properties["code"] == "BCE");
        Assert.Equal(2690010, Assert.IsType<GeoJsonPoint>(rohrende.Geometry).Coordinates[0], precision: 3);
    }

    [Theory]
    [InlineData(3.65, 7.3, 10.0, 5.0)]   // halbe Survey-Strecke -> halbe Geometrie
    [InlineData(7.3, 7.3, 10.0, 10.0)]   // Rohrende -> Geometrie-Ende
    [InlineData(0.0, 7.3, 10.0, 0.0)]    // Rohranfang -> Anfang
    [InlineData(5.0, null, 10.0, 5.0)]   // kein Rohrende -> absolut (keine Streckung)
    [InlineData(5.0, 0.0, 10.0, 5.0)]    // ungueltiges Rohrende -> absolut
    [InlineData(5.0, 7.3, 0.0, 5.0)]     // keine Geometrielaenge -> absolut
    public void ScaleMeter_streckt_nur_mit_gueltigem_rohrende(double meter, double? surveyEnd, double geomLength, double expected)
    {
        Assert.Equal(expected, QgisBridgeSnapshotBuilder.ScaleMeter(meter, surveyEnd, geomLength), precision: 6);
    }

    [Fact]
    public void BuildSchaechteGeoJson_liefert_alle_katasterknoten_mit_projektmarkierung()
    {
        using var fixture = QgisBridgeFixture.Create();
        var sut = fixture.CreateBuilder();
        // Projekt kennt nur S1, hier bewusst mit Innen-Leerzeichen ("S 1") -> muss auf "S1" matchen.
        var snapshot = QgisProjectSnapshot.Capture(
            CreateProjectWithSchacht("S 1", sanieren: "Ja", resultat: "ZK 3"), null);

        var geo = sut.BuildSchaechteGeoJson(snapshot);

        Assert.Equal(2, geo.Features.Count); // S1 + S2 aus dem MiniXtf
        var s1 = geo.Features.Single(f => (string?)f.Properties["schacht"] == "S1");
        Assert.Equal(true, s1.Properties["im_projekt"]);
        Assert.Equal("Ja", s1.Properties["sanieren"]);
        Assert.Equal("ZK 3", s1.Properties["pruefungsresultat"]);
        Assert.IsType<GeoJsonPoint>(s1.Geometry);

        var s2 = geo.Features.Single(f => (string?)f.Properties["schacht"] == "S2");
        Assert.Equal(false, s2.Properties["im_projekt"]);
    }

    [Fact]
    public void BuildCurrentSchachtGeoJson_liefert_den_gewaehlten_schacht_als_punkt()
    {
        using var fixture = QgisBridgeFixture.Create();
        var sut = fixture.CreateBuilder();
        var snapshot = QgisProjectSnapshot.Capture(
            CreateProjectWithSchacht("S1"), null, currentSchacht: "S1");

        var geo = sut.BuildCurrentSchachtGeoJson(snapshot);

        var feature = Assert.Single(geo.Features);
        Assert.Equal("S1", feature.Properties["schacht"]);
        Assert.Equal(true, feature.Properties["current"]);
        var point = Assert.IsType<GeoJsonPoint>(feature.Geometry);
        // S1 liegt im MiniXtf bei 2690100 / 1190000 (LV95, unveraendert).
        Assert.Equal(2690100, point.Coordinates[0], precision: 3);
        Assert.Equal(1190000, point.Coordinates[1], precision: 3);
    }

    [Fact]
    public void BuildCurrentSchachtGeoJson_ohne_auswahl_ist_leer()
    {
        using var fixture = QgisBridgeFixture.Create();
        var sut = fixture.CreateBuilder();
        var snapshot = QgisProjectSnapshot.Capture(CreateProjectWithSchacht("S1"), null);

        Assert.Empty(sut.BuildCurrentSchachtGeoJson(snapshot).Features);
    }

    [Fact]
    public void BuildStatus_meldet_schacht_zaehler_und_auswahl()
    {
        using var fixture = QgisBridgeFixture.Create();
        var sut = fixture.CreateBuilder();
        var snapshot = QgisProjectSnapshot.Capture(
            CreateProjectWithSchacht("S1"), null, selectionStamp: 0,
            currentSchacht: "S1", schachtSelectionStamp: 4);

        var status = sut.BuildStatus(snapshot);

        Assert.Equal(2, status.SchachtFeatureCount);  // 2 Knoten im XTF
        Assert.Equal(1, status.ProjectSchachtCount);  // 1 im Projekt
        Assert.Equal("S1", status.CurrentSchacht);
        Assert.Equal(4, status.SchachtSelectionStamp);
    }

    [Fact]
    public void Sanieren_aenderung_invalidiert_den_schaechte_fingerprint()
    {
        var before = QgisProjectSnapshot.Capture(CreateProjectWithSchacht("S1", sanieren: "Nein"), null).SchaechteFingerprint(1);
        var after = QgisProjectSnapshot.Capture(CreateProjectWithSchacht("S1", sanieren: "Ja"), null).SchaechteFingerprint(1);

        Assert.NotEqual(before, after);
    }

    [Theory]
    [InlineData("KS 60191", "KS60191")]
    [InlineData("  ks 60191 ", "KS60191")]
    [InlineData("S1", "S1")]
    [InlineData(null, "")]
    [InlineData("   ", "")]
    public void QgisSchachtNameMatcher_normalisiert_tolerant(string? input, string expected)
    {
        Assert.Equal(expected, QgisSchachtNameMatcher.Normalize(input));
    }

    private static Project CreateProjectWithSchacht(string nummer, string? sanieren = null, string? resultat = null)
    {
        var project = new Project { Name = "Schacht-Test" };
        var record = new SchachtRecord();
        record.SetFieldValue("Schachtnummer", nummer);
        if (sanieren is not null)
            record.SetFieldValue("Sanieren", sanieren);
        if (resultat is not null)
            record.SetFieldValue("Pruefungsresultat", resultat);
        project.SchaechteData.Add(record);
        return project;
    }

    private static HaltungRecord Haltung(string name, string ausgefuehrt, string nr)
    {
        var record = new HaltungRecord();
        record.SetFieldValue("Haltungsname", name, FieldSource.Manual, userEdited: true);
        if (!string.IsNullOrEmpty(ausgefuehrt))
            record.SetFieldValue("Ausgefuehrt_durch", ausgefuehrt, FieldSource.Manual, userEdited: true);
        record.SetFieldValue("NR", nr, FieldSource.Manual, userEdited: true);
        return record;
    }

    private static SchachtRecord Schacht(string nummer, string? ausgefuehrt = null, string? nr = null)
    {
        var record = new SchachtRecord();
        record.SetFieldValue("Schachtnummer", nummer);
        if (!string.IsNullOrEmpty(ausgefuehrt))
            record.SetFieldValue("Ausgefuehrt_durch", ausgefuehrt);
        if (!string.IsNullOrEmpty(nr))
            record.SetFieldValue("NR", nr);
        return record;
    }

    private static Project SchachtProjekt(string nummer, string? ausgefuehrt)
    {
        var project = new Project { Name = "Schacht-Fingerprint-Test" };
        project.SchaechteData.Add(Schacht(nummer, ausgefuehrt));
        return project;
    }

    private static Project CreateProjectWithImportedDamage()
    {
        var project = new Project { Name = "Bridge-Test" };
        var record = new HaltungRecord();
        record.SetFieldValue("Haltungsname", "A-B", FieldSource.Manual, userEdited: true);
        record.SetFieldValue("Zustandsklasse", "4", FieldSource.Manual, userEdited: true);
        record.VsaFindings.Add(new VsaFinding
        {
            KanalSchadencode = "BAB",
            MeterStart = 5.0,
            Raw = "Riss bei 5 m"
        });
        project.Data.Add(record);
        return project;
    }

    private sealed class QgisBridgeFixture : IDisposable
    {
        private QgisBridgeFixture(string directory, string xtfPath, string cachePath)
        {
            DirectoryPath = directory;
            XtfPath = xtfPath;
            CachePath = cachePath;
        }

        private string DirectoryPath { get; }
        private string XtfPath { get; }
        private string CachePath { get; }

        public static QgisBridgeFixture Create()
        {
            var directory = Directory.CreateTempSubdirectory().FullName;
            var xtfPath = Path.Combine(directory, "kataster.xtf");
            var cachePath = Path.Combine(directory, "network_cache.json");
            File.WriteAllText(xtfPath, MiniXtf);
            return new QgisBridgeFixture(directory, xtfPath, cachePath);
        }

        public QgisBridgeSnapshotBuilder CreateBuilder()
        {
            var settings = new AppSettings
            {
                AbwasserkatasterXtfPath = XtfPath,
                KantonUriXtfDirectory = DirectoryPath
            };

            // Beide Caches in den Temp-Ordner umleiten (nie den echten AppData-Cache anfassen).
            return new QgisBridgeSnapshotBuilder(
                settings,
                CachePath,
                Path.Combine(DirectoryPath, "manhole_cache.json"));
        }

        public void Dispose()
        {
            if (Directory.Exists(DirectoryPath))
                Directory.Delete(DirectoryPath, recursive: true);
        }
    }
}
