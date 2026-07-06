using System.IO;
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
