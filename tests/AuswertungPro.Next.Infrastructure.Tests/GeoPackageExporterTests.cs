using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Domain.Geometry;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Export.GeoPackage;
using Microsoft.Data.Sqlite;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Phase 2 (GeoPackage-Bruecke 2026-05-23):
/// Akzeptanzkriterien:
///   AK1 SewerStudio kann ein Project mit Geometrie als .gpkg exportieren
///   AK2 .gpkg enthaelt 2 Layer: haltungen + schaechte in LV95
///   AK3 Attribute (Name, DN, Material, Zustandsklasse) als Spalten
///   AK4 QGIS 3.34+ kann die Datei lesen (-> SRS=2056 + valider WKB-Header)
///   AK5 SQLite-Lib oeffnet die Datei und liest Layer + Geometrien zurueck
/// </summary>
public class GeoPackageExporterTests : IDisposable
{
    private readonly string _tempPath = Path.Combine(
        Path.GetTempPath(),
        $"phase2_test_{Guid.NewGuid():N}.gpkg");

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { if (File.Exists(_tempPath)) File.Delete(_tempPath); } catch { }
        try { if (File.Exists(_tempPath + "-journal")) File.Delete(_tempPath + "-journal"); } catch { }
    }

    [Fact]
    public void Export_erzeugt_SQLite_Datei_mit_GeoPackage_Magic()
    {
        // AK1 + AK4 (Header) - Datei muss SQLite-Magic UND GeoPackage-application_id haben.
        var project = new Project();
        new GeoPackageExporter().Export(project, _tempPath);

        Assert.True(File.Exists(_tempPath), "Export-Datei muss erzeugt werden");

        var bytes = File.ReadAllBytes(_tempPath);
        Assert.True(bytes.Length >= 100, "SQLite-Header ist 100 Bytes");

        var sqliteMagic = System.Text.Encoding.ASCII.GetString(bytes, 0, 16);
        Assert.StartsWith("SQLite format 3", sqliteMagic);

        // GeoPackage Magic "GPKG" als big-endian uint32 bei Offset 68
        var appId = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(68, 4));
        Assert.Equal(0x47504B47u, appId);
    }

    [Fact]
    public void Export_legt_haltungen_und_schaechte_Layer_an()
    {
        // AK2 - beide Layer muessen in gpkg_contents stehen.
        var project = new Project();
        new GeoPackageExporter().Export(project, _tempPath);

        var tables = QueryTableNames(_tempPath);
        Assert.Contains("haltungen", tables);
        Assert.Contains("schaechte", tables);
        Assert.Contains("gpkg_contents", tables);
        Assert.Contains("gpkg_geometry_columns", tables);
        Assert.Contains("gpkg_spatial_ref_sys", tables);

        var layerNames = QueryGpkgContentsTableNames(_tempPath);
        Assert.Contains("haltungen", layerNames);
        Assert.Contains("schaechte", layerNames);
    }

    [Fact]
    public void Export_schreibt_Haltung_mit_LineString_und_Attributen()
    {
        // AK3 + AK4 + AK5 - Haltung mit Geometrie landet als Zeile;
        // Attribute korrekt; WKB-Geometrie roundtrippt.
        var project = new Project();
        var h = MakeHaltung(
            name: "H1", dn: "300", material: "Beton", zustandsklasse: "3",
            verlauf: new[]
            {
                new Lv95Coordinate(2687970.000, 1168928.000),
                new Lv95Coordinate(2687980.000, 1168935.000),
                new Lv95Coordinate(2687990.000, 1168950.000),
            });
        project.Data.Add(h);

        var result = new GeoPackageExporter().Export(project, _tempPath);
        Assert.Equal(1, result.HaltungenExportiert);

        using var conn = OpenReadOnly(_tempPath);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT name, dn, material, zustandsklasse, geom FROM haltungen";
        using var r = cmd.ExecuteReader();
        Assert.True(r.Read());
        Assert.Equal("H1", r.GetString(0));
        Assert.Equal(300L, r.GetInt64(1));
        Assert.Equal("Beton", r.GetString(2));
        Assert.Equal("3", r.GetString(3));

        var blob = (byte[])r["geom"];
        var (srid, coords) = DecodeGeoPackageLineString(blob);
        Assert.Equal(2056, srid);
        Assert.Equal(3, coords.Count);
        Assert.Equal(2687970.000, coords[0].x, precision: 3);
        Assert.Equal(1168928.000, coords[0].y, precision: 3);
        Assert.Equal(2687990.000, coords[2].x, precision: 3);
        Assert.Equal(1168950.000, coords[2].y, precision: 3);
    }

    [Fact]
    public void Export_schreibt_Schacht_mit_Point_in_LV95()
    {
        var project = new Project();
        var s = new SchachtRecord();
        s.Fields["Bezeichnung"] = "S1";
        s.Lage = new SchachtLage
        {
            Punkt = new Lv95Coordinate(2687970.0, 1168928.0),
            Source = GeometrySource.Xtf,
        };
        project.SchaechteData.Add(s);

        var result = new GeoPackageExporter().Export(project, _tempPath);
        Assert.Equal(1, result.SchaechteExportiert);

        using var conn = OpenReadOnly(_tempPath);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT name, geom FROM schaechte";
        using var r = cmd.ExecuteReader();
        Assert.True(r.Read());
        Assert.Equal("S1", r.GetString(0));

        var blob = (byte[])r["geom"];
        var (srid, x, y) = DecodeGeoPackagePoint(blob);
        Assert.Equal(2056, srid);
        Assert.Equal(2687970.0, x, precision: 3);
        Assert.Equal(1168928.0, y, precision: 3);
    }

    [Fact]
    public void Export_ueberspringt_Datensaetze_ohne_Geometrie_und_zaehlt_sie()
    {
        var project = new Project();

        // 1 Haltung MIT Geometrie
        project.Data.Add(MakeHaltung(
            name: "H1", dn: "200", material: "PVC", zustandsklasse: "2",
            verlauf: new[]
            {
                new Lv95Coordinate(2687970.0, 1168928.0),
                new Lv95Coordinate(2687980.0, 1168935.0),
            }));

        // 1 Haltung OHNE Geometrie
        var hOhne = new HaltungRecord();
        hOhne.Fields["Haltungsname"] = "H_NOGEOM";
        project.Data.Add(hOhne);

        // 1 Schacht ohne Lage
        var sOhne = new SchachtRecord();
        sOhne.Fields["Bezeichnung"] = "S_NOGEOM";
        project.SchaechteData.Add(sOhne);

        var result = new GeoPackageExporter().Export(project, _tempPath);

        Assert.Equal(1, result.HaltungenExportiert);
        Assert.Equal(1, result.HaltungenOhneGeometrie);
        Assert.Equal(0, result.SchaechteExportiert);
        Assert.Equal(1, result.SchaechteOhneLage);

        using var conn = OpenReadOnly(_tempPath);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM haltungen";
        Assert.Equal(1L, (long)cmd.ExecuteScalar()!);
    }

    // ---- Helpers ------------------------------------------------------------

    private static HaltungRecord MakeHaltung(
        string name, string dn, string material, string zustandsklasse,
        IReadOnlyList<Lv95Coordinate> verlauf)
    {
        var h = new HaltungRecord();
        h.Fields["Haltungsname"] = name;
        h.Fields["DN_mm"] = dn;
        h.Fields["Rohrmaterial"] = material;
        h.Fields["Zustandsklasse"] = zustandsklasse;
        h.Geometrie = new HaltungGeometrie
        {
            Verlauf = verlauf,
            Source = GeometrySource.Xtf,
        };
        return h;
    }

    private static SqliteConnection OpenReadOnly(string path)
    {
        var conn = new SqliteConnection($"Data Source={path};Mode=ReadOnly");
        conn.Open();
        return conn;
    }

    private static List<string> QueryTableNames(string path)
    {
        using var conn = OpenReadOnly(path);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table'";
        using var r = cmd.ExecuteReader();
        var list = new List<string>();
        while (r.Read()) list.Add(r.GetString(0));
        return list;
    }

    private static List<string> QueryGpkgContentsTableNames(string path)
    {
        using var conn = OpenReadOnly(path);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT table_name FROM gpkg_contents";
        using var r = cmd.ExecuteReader();
        var list = new List<string>();
        while (r.Read()) list.Add(r.GetString(0));
        return list;
    }

    /// <summary>
    /// Dekodiert einen GeoPackage-Binary-Blob fuer LineString (XY ohne Z/M).
    /// Format: GPKG-Header (Magic 'GP', Version, Flags, SRS_ID, optional Envelope)
    /// + WKB (byte_order, geom_type, num_points, points...)
    /// </summary>
    private static (int srid, List<(double x, double y)> coords) DecodeGeoPackageLineString(byte[] blob)
    {
        var (srid, wkbOffset) = ParseGpkgHeader(blob);

        // WKB: byte 0 = byte_order, bytes 1..4 = geom_type (LE/BE je nach byte_order)
        var byteOrder = blob[wkbOffset];
        if (byteOrder != 1) throw new InvalidDataException("Erwarte Little-Endian WKB");
        var geomType = BinaryPrimitives.ReadUInt32LittleEndian(blob.AsSpan(wkbOffset + 1, 4));
        if (geomType != 2) throw new InvalidDataException($"Erwarte LineString (2), got {geomType}");

        var numPoints = BinaryPrimitives.ReadUInt32LittleEndian(blob.AsSpan(wkbOffset + 5, 4));
        var coords = new List<(double x, double y)>((int)numPoints);
        var p = wkbOffset + 9;
        for (var i = 0u; i < numPoints; i++)
        {
            var x = BinaryPrimitives.ReadDoubleLittleEndian(blob.AsSpan(p, 8));
            var y = BinaryPrimitives.ReadDoubleLittleEndian(blob.AsSpan(p + 8, 8));
            coords.Add((x, y));
            p += 16;
        }
        return (srid, coords);
    }

    private static (int srid, double x, double y) DecodeGeoPackagePoint(byte[] blob)
    {
        var (srid, wkbOffset) = ParseGpkgHeader(blob);

        var byteOrder = blob[wkbOffset];
        if (byteOrder != 1) throw new InvalidDataException("Erwarte Little-Endian WKB");
        var geomType = BinaryPrimitives.ReadUInt32LittleEndian(blob.AsSpan(wkbOffset + 1, 4));
        if (geomType != 1) throw new InvalidDataException($"Erwarte Point (1), got {geomType}");

        var x = BinaryPrimitives.ReadDoubleLittleEndian(blob.AsSpan(wkbOffset + 5, 8));
        var y = BinaryPrimitives.ReadDoubleLittleEndian(blob.AsSpan(wkbOffset + 13, 8));
        return (srid, x, y);
    }

    /// <summary>
    /// Parst den GeoPackage-Binary-Header und liefert SRS_ID + Offset wo das WKB anfaengt.
    /// Spec: OGC 12-128r19, Kapitel 2.1.3 ("StandardGeoPackageBinary").
    /// </summary>
    private static (int srid, int wkbOffset) ParseGpkgHeader(byte[] blob)
    {
        if (blob.Length < 8) throw new InvalidDataException("GPKG-Header zu kurz");
        if (blob[0] != (byte)'G' || blob[1] != (byte)'P')
            throw new InvalidDataException("Fehlende GPKG-Magic 'GP'");

        var version = blob[2];
        if (version != 0) throw new InvalidDataException($"Unbekannte GPKG-Version {version}");

        var flags = blob[3];
        // bit 0 = byte order: 0 = BE, 1 = LE
        var headerLe = (flags & 0x01) != 0;
        // bits 1-3 = envelope contents (000 = kein Envelope; 001 = XY = 32 bytes)
        var envelopeCode = (flags >> 1) & 0x07;
        var envelopeBytes = envelopeCode switch
        {
            0 => 0,
            1 => 32, // XY envelope: minX,maxX,minY,maxY als doubles
            2 => 48, // XYZ
            3 => 48, // XYM
            4 => 64, // XYZM
            _ => throw new InvalidDataException($"Ungueltiger Envelope-Code {envelopeCode}"),
        };

        var sridSpan = blob.AsSpan(4, 4);
        var srid = headerLe
            ? BinaryPrimitives.ReadInt32LittleEndian(sridSpan)
            : BinaryPrimitives.ReadInt32BigEndian(sridSpan);

        return (srid, 8 + envelopeBytes);
    }
}
