using System.IO;
using System.Text.Json;
using AuswertungPro.Next.Domain.Models;
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
  </a.Haltung></X></DATASECTION></TRANSFER>";

    [Fact]
    public void BuildStatus_reports_live_project_and_xtf_counts()
    {
        using var fixture = QgisBridgeFixture.Create();
        var sut = fixture.CreateBuilder();
        var project = CreateProjectWithDamage();

        var status = sut.BuildStatus(project, "A-B");

        Assert.True(status.Ok);
        Assert.Equal("A-B", status.CurrentHolding);
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
        var project = CreateProjectWithDamage();

        var geoJson = sut.BuildCurrentGeoJson(project, "A-B");

        var feature = Assert.Single(geoJson.Features);
        Assert.Equal("A-B", feature.Properties["haltung"]);
        var line = Assert.IsType<GeoJsonLineString>(feature.Geometry);
        Assert.Equal(2, line.Coordinates.Length);
        Assert.InRange(line.Coordinates[0][0], 8.61, 8.63);
        Assert.InRange(line.Coordinates[0][1], 46.85, 46.90);
    }

    [Fact]
    public void BuildDamagesGeoJson_projects_damage_meter_to_holding_geometry()
    {
        using var fixture = QgisBridgeFixture.Create();
        var sut = fixture.CreateBuilder();
        var project = CreateProjectWithDamage();

        var geoJson = sut.BuildDamagesGeoJson(project);

        var feature = Assert.Single(geoJson.Features);
        Assert.Equal("A-B", feature.Properties["haltung"]);
        Assert.Equal("BAB", feature.Properties["code"]);
        var point = Assert.IsType<GeoJsonPoint>(feature.Geometry);
        Assert.InRange(point.Coordinates[0], 8.61, 8.63);
        Assert.InRange(point.Coordinates[1], 46.85, 46.90);
    }

    [Fact]
    public void BuildGeoJson_serializes_geometry_types_for_qgis()
    {
        using var fixture = QgisBridgeFixture.Create();
        var sut = fixture.CreateBuilder();
        var project = CreateProjectWithDamage();
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        var currentJson = JsonSerializer.Serialize(sut.BuildCurrentGeoJson(project, "A-B"), options);
        var damagesJson = JsonSerializer.Serialize(sut.BuildDamagesGeoJson(project), options);

        Assert.Contains("\"type\":\"FeatureCollection\"", currentJson);
        Assert.Contains("\"type\":\"LineString\"", currentJson);
        Assert.Contains("\"coordinates\"", currentJson);
        Assert.Contains("\"type\":\"Point\"", damagesJson);
    }

    private static Project CreateProjectWithDamage()
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

            return new QgisBridgeSnapshotBuilder(settings, CachePath);
        }

        public void Dispose()
        {
            if (Directory.Exists(DirectoryPath))
                Directory.Delete(DirectoryPath, recursive: true);
        }
    }
}
