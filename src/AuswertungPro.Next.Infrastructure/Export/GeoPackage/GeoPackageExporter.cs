using System.Buffers.Binary;
using AuswertungPro.Next.Domain.Geometry;
using AuswertungPro.Next.Domain.Models;
using Microsoft.Data.Sqlite;

namespace AuswertungPro.Next.Infrastructure.Export.GeoPackage;

/// <summary>
/// Exportiert ein <see cref="Project"/> als OGC GeoPackage (SQLite-basiert).
/// Erzeugt zwei Layer:
/// <list type="bullet">
///   <item><c>haltungen</c> (LineString in LV95) mit Attributen Name, DN, Material, Zustandsklasse</item>
///   <item><c>schaechte</c> (Point in LV95) mit Attribut Name</item>
/// </list>
///
/// Phase 2 (GeoPackage-Bruecke nach QGIS 2026-05-23).
/// </summary>
public sealed class GeoPackageExporter
{
    public sealed record ExportResult(
        int HaltungenExportiert,
        int SchaechteExportiert,
        int HaltungenOhneGeometrie,
        int SchaechteOhneLage);

    private const int Lv95Srid = 2056;
    // "GPKG" als big-endian uint32 -> 0x47504B47 = 1196444487
    private const int GeoPackageApplicationId = 0x47504B47;
    // GeoPackage 1.3.0 -> 10300
    private const int GeoPackageUserVersion = 10300;

    public ExportResult Export(Project project, string gpkgPath)
    {
        if (project is null) throw new ArgumentNullException(nameof(project));
        if (string.IsNullOrWhiteSpace(gpkgPath))
            throw new ArgumentException("Pfad fehlt", nameof(gpkgPath));

        // Bestehende Datei loeschen - GeoPackage muss frisch aufgebaut werden
        if (File.Exists(gpkgPath)) File.Delete(gpkgPath);

        var dir = Path.GetDirectoryName(Path.GetFullPath(gpkgPath));
        if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);

        // Pooling=False: Datei wird beim Dispose sofort freigegeben.
        // Ohne diese Option haelt der Connection-Pool das File-Lock auch nach
        // Connection.Close, was Folgezugriffe (Read, Move, Delete) auf Windows blockiert.
        using var conn = new SqliteConnection($"Data Source={gpkgPath};Pooling=False");
        conn.Open();

        InitializeGeoPackageSchema(conn);

        var haltungenStats = WriteHaltungen(conn, project);
        var schaechteStats = WriteSchaechte(conn, project);

        return new ExportResult(
            HaltungenExportiert: haltungenStats.written,
            SchaechteExportiert: schaechteStats.written,
            HaltungenOhneGeometrie: haltungenStats.skipped,
            SchaechteOhneLage: schaechteStats.skipped);
    }

    // ---- Schema -------------------------------------------------------------

    private static void InitializeGeoPackageSchema(SqliteConnection conn)
    {
        Exec(conn, $"PRAGMA application_id = {GeoPackageApplicationId};");
        Exec(conn, $"PRAGMA user_version = {GeoPackageUserVersion};");

        Exec(conn, """
            CREATE TABLE gpkg_spatial_ref_sys (
                srs_name TEXT NOT NULL,
                srs_id INTEGER NOT NULL PRIMARY KEY,
                organization TEXT NOT NULL,
                organization_coordsys_id INTEGER NOT NULL,
                definition TEXT NOT NULL,
                description TEXT
            );
            """);

        Exec(conn, """
            CREATE TABLE gpkg_contents (
                table_name TEXT NOT NULL PRIMARY KEY,
                data_type TEXT NOT NULL,
                identifier TEXT UNIQUE,
                description TEXT DEFAULT '',
                last_change DATETIME NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
                min_x DOUBLE,
                min_y DOUBLE,
                max_x DOUBLE,
                max_y DOUBLE,
                srs_id INTEGER,
                CONSTRAINT fk_gc_r_srs_id FOREIGN KEY (srs_id) REFERENCES gpkg_spatial_ref_sys(srs_id)
            );
            """);

        Exec(conn, """
            CREATE TABLE gpkg_geometry_columns (
                table_name TEXT NOT NULL,
                column_name TEXT NOT NULL,
                geometry_type_name TEXT NOT NULL,
                srs_id INTEGER NOT NULL,
                z TINYINT NOT NULL,
                m TINYINT NOT NULL,
                CONSTRAINT pk_geom_cols PRIMARY KEY (table_name, column_name),
                CONSTRAINT fk_gc_tn FOREIGN KEY (table_name) REFERENCES gpkg_contents(table_name),
                CONSTRAINT fk_gc_srs FOREIGN KEY (srs_id) REFERENCES gpkg_spatial_ref_sys(srs_id)
            );
            """);

        // Pflicht-Eintraege gemaess GeoPackage-Spec + LV95
        Exec(conn, """
            INSERT INTO gpkg_spatial_ref_sys
                (srs_name, srs_id, organization, organization_coordsys_id, definition, description)
            VALUES
                ('Undefined cartesian SRS', -1, 'NONE', -1, 'undefined', 'undefined cartesian coordinate reference system'),
                ('Undefined geographic SRS', 0, 'NONE', 0, 'undefined', 'undefined geographic coordinate reference system'),
                ('WGS 84', 4326, 'EPSG', 4326, 'GEOGCS["WGS 84",DATUM["WGS_1984",SPHEROID["WGS 84",6378137,298.257223563]],PRIMEM["Greenwich",0],UNIT["degree",0.0174532925199433],AUTHORITY["EPSG","4326"]]', 'longitude/latitude coordinates in decimal degrees on the WGS 84 spheroid'),
                ('CH1903+ / LV95', 2056, 'EPSG', 2056, 'PROJCS["CH1903+ / LV95",GEOGCS["CH1903+",DATUM["CH1903+",SPHEROID["Bessel 1841",6377397.155,299.1528128]],PRIMEM["Greenwich",0],UNIT["degree",0.0174532925199433]],PROJECTION["Hotine_Oblique_Mercator_Azimuth_Center"],PARAMETER["latitude_of_center",46.95240555555556],PARAMETER["longitude_of_center",7.439583333333333],PARAMETER["azimuth",90],PARAMETER["rectified_grid_angle",90],PARAMETER["scale_factor",1],PARAMETER["false_easting",2600000],PARAMETER["false_northing",1200000],UNIT["metre",1],AUTHORITY["EPSG","2056"]]', 'Schweizer Landeskoordinaten 1995 (LV95) - EPSG:2056');
            """);

        Exec(conn, """
            CREATE TABLE haltungen (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT,
                dn INTEGER,
                material TEXT,
                zustandsklasse TEXT,
                geom BLOB
            );
            """);

        Exec(conn, """
            CREATE TABLE schaechte (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT,
                geom BLOB
            );
            """);

        Exec(conn, $"""
            INSERT INTO gpkg_contents (table_name, data_type, identifier, srs_id) VALUES
                ('haltungen', 'features', 'haltungen', {Lv95Srid}),
                ('schaechte', 'features', 'schaechte', {Lv95Srid});
            """);

        Exec(conn, $"""
            INSERT INTO gpkg_geometry_columns VALUES
                ('haltungen', 'geom', 'LINESTRING', {Lv95Srid}, 0, 0),
                ('schaechte', 'geom', 'POINT', {Lv95Srid}, 0, 0);
            """);
    }

    // ---- Daten schreiben ----------------------------------------------------

    private static (int written, int skipped) WriteHaltungen(SqliteConnection conn, Project project)
    {
        var written = 0;
        var skipped = 0;

        using var tx = conn.BeginTransaction();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO haltungen (name, dn, material, zustandsklasse, geom)
            VALUES ($name, $dn, $material, $zk, $geom);
            """;
        var pName = cmd.CreateParameter(); pName.ParameterName = "$name"; cmd.Parameters.Add(pName);
        var pDn = cmd.CreateParameter(); pDn.ParameterName = "$dn"; cmd.Parameters.Add(pDn);
        var pMat = cmd.CreateParameter(); pMat.ParameterName = "$material"; cmd.Parameters.Add(pMat);
        var pZk = cmd.CreateParameter(); pZk.ParameterName = "$zk"; cmd.Parameters.Add(pZk);
        var pGeom = cmd.CreateParameter(); pGeom.ParameterName = "$geom"; cmd.Parameters.Add(pGeom);

        foreach (var h in project.Data)
        {
            if (h.Geometrie is null || h.Geometrie.Verlauf.Count < 2)
            {
                skipped++;
                continue;
            }

            pName.Value = (object?)h.GetFieldValue("Haltungsname") ?? DBNull.Value;
            pDn.Value = ParseIntOrNull(h.GetFieldValue("DN_mm"));
            pMat.Value = (object?)NullIfEmpty(h.GetFieldValue("Rohrmaterial")) ?? DBNull.Value;
            pZk.Value = (object?)NullIfEmpty(h.GetFieldValue("Zustandsklasse")) ?? DBNull.Value;
            pGeom.Value = EncodeLineStringBlob(h.Geometrie.Verlauf);

            cmd.ExecuteNonQuery();
            written++;
        }

        tx.Commit();
        return (written, skipped);
    }

    private static (int written, int skipped) WriteSchaechte(SqliteConnection conn, Project project)
    {
        var written = 0;
        var skipped = 0;

        using var tx = conn.BeginTransaction();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO schaechte (name, geom) VALUES ($name, $geom);
            """;
        var pName = cmd.CreateParameter(); pName.ParameterName = "$name"; cmd.Parameters.Add(pName);
        var pGeom = cmd.CreateParameter(); pGeom.ParameterName = "$geom"; cmd.Parameters.Add(pGeom);

        foreach (var s in project.SchaechteData)
        {
            if (s.Lage is null)
            {
                skipped++;
                continue;
            }

            pName.Value = (object?)s.GetFieldValue("Bezeichnung") ?? DBNull.Value;
            pGeom.Value = EncodePointBlob(s.Lage.Punkt);

            cmd.ExecuteNonQuery();
            written++;
        }

        tx.Commit();
        return (written, skipped);
    }

    // ---- WKB / GeoPackage Binary Encoder ------------------------------------

    /// <summary>
    /// Kodiert eine LineString-Geometrie als StandardGeoPackageBinary (Spec 12-128r19,
    /// 2.1.3). Header: 'GP' Magic + Version + Flags + SRS_ID = 8 Bytes (kein Envelope).
    /// Danach WKB: byte_order=LE, geom_type=2 (LineString), N Punkte (x,y als double LE).
    /// </summary>
    private static byte[] EncodeLineStringBlob(IReadOnlyList<Lv95Coordinate> coords)
    {
        var total = 8 + 1 + 4 + 4 + coords.Count * 16;
        var buf = new byte[total];
        WriteGpkgHeader(buf.AsSpan(0, 8));

        var wkb = buf.AsSpan(8);
        wkb[0] = 1; // Byte-Order: little-endian
        BinaryPrimitives.WriteUInt32LittleEndian(wkb.Slice(1, 4), 2u); // LineString
        BinaryPrimitives.WriteUInt32LittleEndian(wkb.Slice(5, 4), (uint)coords.Count);

        var p = 9;
        foreach (var c in coords)
        {
            BinaryPrimitives.WriteDoubleLittleEndian(wkb.Slice(p, 8), c.Ost);
            BinaryPrimitives.WriteDoubleLittleEndian(wkb.Slice(p + 8, 8), c.Nord);
            p += 16;
        }
        return buf;
    }

    /// <summary>
    /// Kodiert einen Point als StandardGeoPackageBinary. Header 8 Bytes + WKB
    /// (byte_order=LE, geom_type=1, x,y als double LE) = 8+1+4+16 = 29 Bytes.
    /// </summary>
    private static byte[] EncodePointBlob(Lv95Coordinate punkt)
    {
        var buf = new byte[8 + 1 + 4 + 16];
        WriteGpkgHeader(buf.AsSpan(0, 8));

        var wkb = buf.AsSpan(8);
        wkb[0] = 1; // Byte-Order LE
        BinaryPrimitives.WriteUInt32LittleEndian(wkb.Slice(1, 4), 1u); // Point
        BinaryPrimitives.WriteDoubleLittleEndian(wkb.Slice(5, 8), punkt.Ost);
        BinaryPrimitives.WriteDoubleLittleEndian(wkb.Slice(13, 8), punkt.Nord);
        return buf;
    }

    private static void WriteGpkgHeader(Span<byte> header8)
    {
        header8[0] = (byte)'G';
        header8[1] = (byte)'P';
        header8[2] = 0;     // Version
        header8[3] = 0x01;  // Flags: bit0=1 (LE header), bits 1-3=0 (kein Envelope)
        BinaryPrimitives.WriteInt32LittleEndian(header8.Slice(4, 4), Lv95Srid);
    }

    // ---- Helpers ------------------------------------------------------------

    private static void Exec(SqliteConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private static object ParseIntOrNull(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return DBNull.Value;
        return int.TryParse(raw.Trim(), out var n) ? n : DBNull.Value;
    }

    private static string? NullIfEmpty(string? raw)
        => string.IsNullOrWhiteSpace(raw) ? null : raw;
}
