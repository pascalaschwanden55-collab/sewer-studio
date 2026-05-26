using System.Collections;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FirebirdSql.Data.FirebirdClient;

const string Schema = "cadaster-db-pilot-v1";

var options = ParseArgs(args);
Directory.CreateDirectory(options.OutputDirectory);
var nativeClient = ConfigureNativeClient(options);

var fdbFiles = DiscoverFdbs(options).ToList();
var reports = new List<FdbReport>();
CadasterManifest? manifest = null;

foreach (var (path, index) in fdbFiles.Select((path, index) => (path, index + 1)))
{
    if (!options.Quiet)
        Console.WriteLine($"[{index,2}/{fdbFiles.Count,2}] {path}");
    reports.Add(InspectFdb(path, nativeClient, options.OutputDirectory));
}

if (options.ExportManifest)
    manifest = BuildCadasterManifest(options, fdbFiles, nativeClient, options.OutputDirectory);

var aggregate = BuildAggregate(options, reports, nativeClient);
var stamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
var outPath = Path.Combine(options.OutputDirectory, $"cadaster_pilot_{stamp}.json");
var manifestPath = Path.Combine(options.OutputDirectory, $"cadaster_manifest_{stamp}.json");

var jsonOptions = new JsonSerializerOptions
{
    WriteIndented = true,
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
};
var topologyResults = options.ExportTopology
    ? WriteCadasterTopologyOutputs(options, fdbFiles, nativeClient, options.OutputDirectory, jsonOptions)
    : [];
File.WriteAllText(outPath, JsonSerializer.Serialize(aggregate, jsonOptions));
if (manifest is not null)
    File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, jsonOptions));

if (!options.Quiet)
{
    var meta = aggregate.Meta;
    Console.WriteLine();
    Console.WriteLine($"FDBs gefunden       : {meta.FdbCount}");
    Console.WriteLine($"Verbindbar          : {meta.ConnectedCount}");
    Console.WriteLine($"GISOBJECT gesamt    : {meta.GisobjectRowCount}");
    Console.WriteLine($"Topologie-Paare     : {meta.TopologyPairCount}");
    Console.WriteLine($"Stammdaten-Zeilen   : {meta.StammdatenRowCount}");
    Console.WriteLine($"STATION Beobacht.   : {meta.StationRowCount}");
    Console.WriteLine($"STATION mit Code    : {meta.StationWithCodeCount}");
    Console.WriteLine($"FOTO-Zeilen         : {meta.FotoRowCount}");
    Console.WriteLine($"FILMPOS-Zeilen      : {meta.FilmPosRowCount}");
    Console.WriteLine($"Medien-Kandidaten   : {meta.MediaCandidateTableCount}");
    Console.WriteLine($"Beobachtungs-Kand.  : {meta.ObservationCandidateTableCount}");
    Console.WriteLine($"Report              : {outPath}");
    if (manifest is not null)
    {
        Console.WriteLine($"Manifest Samples    : {manifest.Meta.SampleCount}");
        Console.WriteLine($"Manifest mit Foto   : {manifest.Meta.WithPhotoPathCount}");
        Console.WriteLine($"Manifest mit Video  : {manifest.Meta.WithVideoPathCount}");
        Console.WriteLine($"Manifest            : {manifestPath}");
    }
    if (topologyResults.Count > 0)
    {
        Console.WriteLine($"Topology JSONs      : {topologyResults.Count(r => r.Written),4}/{topologyResults.Count}");
        Console.WriteLine($"Topology Haltungen  : {topologyResults.Sum(r => r.HaltungCount)}");
        Console.WriteLine($"Topology Output     : {ResolveTopologyOutputRoot(options)}");
        foreach (var failed in topologyResults.Where(r => !r.Written).Take(5))
            Console.WriteLine($"Topology Fehler     : {failed.SourceFdb} -> {failed.Error}");
    }
}

Console.Out.Flush();
Console.Error.Flush();
ExitProcess(0);
return;

static CliOptions ParseArgs(string[] args)
{
    var root = @"D:\Videoprojekte";
    string? singleFdb = null;
    var output = Path.Combine(AppContext.BaseDirectory, "output");
    string? fbclient = null;
    var max = 0;
    var quiet = false;
    var exportManifest = false;
    var exportTopology = false;
    string? topologyOutput = null;

    for (var i = 0; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--root":
                root = RequireValue(args, ref i, "--root");
                break;
            case "--fdb":
                singleFdb = RequireValue(args, ref i, "--fdb");
                break;
            case "--out":
                output = RequireValue(args, ref i, "--out");
                break;
            case "--fbclient":
                fbclient = RequireValue(args, ref i, "--fbclient");
                break;
            case "--max":
                if (!int.TryParse(RequireValue(args, ref i, "--max"), out max) || max < 0)
                    throw new ArgumentException("--max muss eine Zahl >= 0 sein.");
                break;
            case "--quiet":
                quiet = true;
                break;
            case "--export-manifest":
                exportManifest = true;
                break;
            case "--export-topology":
                exportTopology = true;
                break;
            case "--topology-out":
                topologyOutput = RequireValue(args, ref i, "--topology-out");
                break;
            case "--help":
            case "-h":
                PrintUsage();
                ExitProcess(0);
                break;
            default:
                throw new ArgumentException($"Unbekannter Parameter: {args[i]}");
        }
    }

    return new CliOptions(
        root,
        singleFdb,
        Path.GetFullPath(output),
        fbclient,
        max,
        quiet,
        exportManifest,
        exportTopology,
        string.IsNullOrWhiteSpace(topologyOutput) ? null : Path.GetFullPath(topologyOutput));
}

static string RequireValue(string[] args, ref int index, string name)
{
    if (index + 1 >= args.Length)
        throw new ArgumentException($"{name} erwartet einen Wert.");
    index++;
    return args[index];
}

static void PrintUsage()
{
    Console.WriteLine("""
        CadasterDbReader

        Read-only Diagnose fuer Firebird-Stammdaten-DBs (.fdb).

        Optionen:
          --root <dir>    Root fuer rekursive Suche nach Arizona.fdb. Default: D:\Videoprojekte
          --fdb <file>    Einzelne FDB lesen statt Root-Scan.
          --out <dir>     Output-Verzeichnis. Default: Tool-bin/output
          --fbclient <dll> Explizite x64 fbclient.dll. Falls leer: Auto-Suche unter Root.
          --max <n>       Maximal n FDBs lesen, 0 = alle.
          --export-manifest Zusaetzlich cadaster-manifest-v1 schreiben.
          --export-topology Zusaetzlich haltung-topology-v1 JSONs schreiben.
          --topology-out <dir> Output fuer topology.json. Default: tools\HaltungTopologyExtractor\output
          --quiet         Nur Fehler/Report schreiben.
        """);
}

static string? ConfigureNativeClient(CliOptions options)
{
    var candidate = options.FbClient;
    if (string.IsNullOrWhiteSpace(candidate))
        candidate = FindCompatibleFirebirdClient(options.Root);

    if (string.IsNullOrWhiteSpace(candidate) || !File.Exists(candidate))
        return null;

    var dir = Path.GetDirectoryName(candidate);
    if (!string.IsNullOrWhiteSpace(dir))
        SetDllDirectory(dir);
    NativeLibrary.Load(candidate);
    return candidate;
}

static string? FindCompatibleFirebirdClient(string root)
{
    if (!Directory.Exists(root))
        return null;

    var wanted = Environment.Is64BitProcess ? "x64" : "x86";
    var names = Environment.Is64BitProcess
        ? new[] { "fbclient.dll", "fbembed.dll" }
        : new[] { "fbembed.dll", "fbclient.dll" };

    var candidates = new List<(string Path, int Score)>();
    foreach (var name in names)
    {
        foreach (var file in EnumerateFilesSafe(root, name))
        {
            if (TryReadPeArchitecture(file) == wanted)
                candidates.Add((file, ScoreNativeClient(file, name)));
        }
    }

    return candidates
        .OrderByDescending(c => c.Score)
        .ThenBy(c => c.Path, StringComparer.OrdinalIgnoreCase)
        .Select(c => c.Path)
        .FirstOrDefault();
}

static int ScoreNativeClient(string path, string requestedName)
{
    var dir = Path.GetDirectoryName(path) ?? "";
    var score = 0;

    if (!Environment.Is64BitProcess && string.Equals(Path.GetFileName(path), "fbembed.dll", StringComparison.OrdinalIgnoreCase))
        score += 10;
    if (Environment.Is64BitProcess && string.Equals(Path.GetFileName(path), "fbclient.dll", StringComparison.OrdinalIgnoreCase))
        score += 10;
    if (File.Exists(Path.Combine(dir, "firebird.conf")))
        score += 8;
    if (File.Exists(Path.Combine(dir, "ib_util.dll")))
        score += 4;
    if (Directory.Exists(Path.Combine(dir, "plugins")))
        score += 4;
    if (dir.Contains("ScanExplorer", StringComparison.OrdinalIgnoreCase))
        score -= 4;
    if (dir.Contains("Gbak", StringComparison.OrdinalIgnoreCase))
        score -= 2;
    if (string.Equals(Path.GetFileName(path), requestedName, StringComparison.OrdinalIgnoreCase))
        score += 1;

    return score;
}

static string? TryReadPeArchitecture(string path)
{
    try
    {
        using var fs = File.OpenRead(path);
        using var reader = new BinaryReader(fs);
        fs.Seek(0x3C, SeekOrigin.Begin);
        var peOffset = reader.ReadInt32();
        fs.Seek(peOffset + 4, SeekOrigin.Begin);
        var machine = reader.ReadUInt16();
        return machine switch
        {
            0x014c => "x86",
            0x8664 => "x64",
            _ => $"0x{machine:X}"
        };
    }
    catch
    {
        return null;
    }
}

static IEnumerable<string> DiscoverFdbs(CliOptions options)
{
    if (!string.IsNullOrWhiteSpace(options.SingleFdb))
    {
        if (File.Exists(options.SingleFdb))
            yield return Path.GetFullPath(options.SingleFdb);
        yield break;
    }

    if (!Directory.Exists(options.Root))
        yield break;

    var count = 0;
    foreach (var path in EnumerateFilesSafe(options.Root, "Arizona.fdb"))
    {
        yield return path;
        count++;
        if (options.Max > 0 && count >= options.Max)
            yield break;
    }
}

static IEnumerable<string> EnumerateFilesSafe(string root, string pattern, SearchOption searchOption = SearchOption.AllDirectories)
{
    var pending = new Stack<string>();
    pending.Push(root);
    while (pending.Count > 0)
    {
        var current = pending.Pop();
        IEnumerable<string> files;
        try { files = Directory.EnumerateFiles(current, pattern); }
        catch { continue; }

        foreach (var file in files)
            yield return file;

        if (searchOption == SearchOption.TopDirectoryOnly)
            continue;

        IEnumerable<string> dirs;
        try { dirs = Directory.EnumerateDirectories(current); }
        catch { continue; }

        foreach (var dir in dirs)
            pending.Push(dir);
    }
}

static FdbReport InspectFdb(string fdbPath, string? nativeClient, string outputDirectory)
{
    var report = new FdbReport
    {
        FdbPath = fdbPath,
        SizeBytes = new FileInfo(fdbPath).Length,
        ProjectRootGuess = GuessProjectRoot(fdbPath)
    };

    try
    {
        var databasePath = PrepareFirebirdDatabasePath(fdbPath, outputDirectory);
        report.DatabaseConnectionPath = databasePath;
        using var conn = OpenConnection(databasePath, nativeClient);
        report.Connected = true;
        report.Tables = LoadTables(conn);
        report.TableCount = report.Tables.Count;
        report.GisObject = InspectGisObject(conn, report.Tables);
        report.KnownTables = InspectKnownTables(report.Tables);
        report.Station = InspectStation(conn, report.Tables);
        report.MediaCandidates = FindCandidateTables(report.Tables, CandidateKind.Media);
        report.ObservationCandidates = FindCandidateTables(report.Tables, CandidateKind.Observation);
        report.CodeLikeCandidates = FindCandidateTables(report.Tables, CandidateKind.CodeLike);
    }
    catch (Exception ex)
    {
        report.Connected = false;
        report.Error = $"{ex.GetType().Name}: {ex.Message}";
    }

    return report;
}

static FbConnection OpenConnection(string fdbPath, string? nativeClient)
{
    var builder = new FbConnectionStringBuilder
    {
        Database = fdbPath,
        UserID = Environment.GetEnvironmentVariable("FDB_USER") ?? "SYSDBA",
        Password = Environment.GetEnvironmentVariable("FDB_PASSWORD") ?? "masterkey",
        ServerType = FbServerType.Embedded,
        Charset = "NONE"
    };

    if (!string.IsNullOrWhiteSpace(nativeClient))
        builder.ClientLibrary = ToFirebirdPath(nativeClient);

    var conn = new FbConnection(builder.ToString());
    conn.Open();
    return conn;
}

static string PrepareFirebirdDatabasePath(string fdbPath, string outputDirectory)
{
    var directPath = ToFirebirdPath(fdbPath);
    if (IsAscii(directPath))
        return directPath;

    var cacheDir = Path.Combine(outputDirectory, "_firebird_ascii_cache", StableId(fdbPath));
    Directory.CreateDirectory(cacheDir);
    var cachedPath = Path.Combine(cacheDir, "Arizona.fdb");

    var source = new FileInfo(fdbPath);
    var shouldCopy = true;
    if (File.Exists(cachedPath))
    {
        var cached = new FileInfo(cachedPath);
        shouldCopy = cached.Length != source.Length || cached.LastWriteTimeUtc < source.LastWriteTimeUtc;
    }

    if (shouldCopy)
    {
        File.Copy(fdbPath, cachedPath, overwrite: true);
        File.SetLastWriteTimeUtc(cachedPath, source.LastWriteTimeUtc);
    }

    return cachedPath;
}

static string ToFirebirdPath(string path)
{
    if (IsAscii(path))
        return path;

    var shortPath = TryGetShortPath(path);
    return !string.IsNullOrWhiteSpace(shortPath) && IsAscii(shortPath) ? shortPath : path;
}

static bool IsAscii(string text) => text.All(c => c <= 127);

static string StableId(string text)
{
    var hash = SHA256.HashData(Encoding.UTF8.GetBytes(text));
    return Convert.ToHexString(hash).Substring(0, 12).ToLowerInvariant();
}

static string? TryGetShortPath(string path)
{
    try
    {
        var buffer = new StringBuilder(32768);
        var written = GetShortPathName(path, buffer, buffer.Capacity);
        if (written <= 0 || written >= buffer.Capacity)
            return null;
        return buffer.ToString();
    }
    catch
    {
        return null;
    }
}

static List<TableReport> LoadTables(FbConnection conn)
{
    var tables = new List<TableReport>();
    using var cmd = conn.CreateCommand();
    cmd.CommandText = """
        SELECT TRIM(RDB$RELATION_NAME)
        FROM RDB$RELATIONS
        WHERE RDB$VIEW_BLR IS NULL
          AND (RDB$SYSTEM_FLAG IS NULL OR RDB$SYSTEM_FLAG = 0)
        ORDER BY 1
        """;
    using var reader = cmd.ExecuteReader();
    while (reader.Read())
    {
        var name = reader.GetString(0).Trim();
        if (string.IsNullOrWhiteSpace(name))
            continue;
        tables.Add(new TableReport
        {
            Name = name,
            Columns = LoadColumns(conn, name),
            RowCount = CountRows(conn, name)
        });
    }
    return tables;
}

static List<string> LoadColumns(FbConnection conn, string table)
{
    var columns = new List<string>();
    using var cmd = conn.CreateCommand();
    cmd.CommandText = """
        SELECT TRIM(RDB$FIELD_NAME)
        FROM RDB$RELATION_FIELDS
        WHERE RDB$RELATION_NAME = @table
        ORDER BY RDB$FIELD_POSITION
        """;
    cmd.Parameters.AddWithValue("@table", table);
    using var reader = cmd.ExecuteReader();
    while (reader.Read())
    {
        var column = reader.GetString(0).Trim();
        if (!string.IsNullOrWhiteSpace(column))
            columns.Add(column);
    }
    return columns;
}

static long CountRows(FbConnection conn, string table)
{
    try
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT COUNT(*) FROM {QuoteId(table)}";
        return Convert.ToInt64(cmd.ExecuteScalar());
    }
    catch
    {
        return -1;
    }
}

static GisObjectReport? InspectGisObject(FbConnection conn, List<TableReport> tables)
{
    var table = tables.FirstOrDefault(t => string.Equals(t.Name, "GISOBJECT", StringComparison.OrdinalIgnoreCase));
    if (table is null)
        return null;

    var discrim = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
    try
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT DISCRIM, COUNT(*)
            FROM GISOBJECT
            GROUP BY DISCRIM
            ORDER BY 2 DESC
            """;
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var key = reader.IsDBNull(0) ? "" : reader.GetString(0).Trim();
            discrim[string.IsNullOrWhiteSpace(key) ? "(null)" : key] = Convert.ToInt64(reader.GetValue(1));
        }
    }
    catch
    {
        // Keep the rest of the report useful.
    }

    var topologyPairs = CountScalar(conn, """
        SELECT COUNT(*)
        FROM GISOBJECT startObj
        JOIN GISOBJECT endObj ON startObj.GISOBJECT_END = endObj.ID
        WHERE startObj.DISCRIM IN ('Cn', 'Lt')
          AND startObj.OBJ_NAME IS NOT NULL
          AND endObj.OBJ_NAME IS NOT NULL
        """);

    var stammdatenRows = CountScalar(conn, """
        SELECT COUNT(*)
        FROM GISOBJECT
        WHERE DISCRIM IN ('Lt','Sc')
          AND OBJ_NAME IS NOT NULL
        """);

    var withLength = CountScalar(conn, """
        SELECT COUNT(*)
        FROM GISOBJECT
        WHERE DISCRIM IN ('Lt','Sc')
          AND OBJ_LENGTH IS NOT NULL
        """);

    var withProfile = CountScalar(conn, """
        SELECT COUNT(*)
        FROM GISOBJECT
        WHERE DISCRIM IN ('Lt','Sc')
          AND (PROFILE_HEIGHT IS NOT NULL OR PROFILE_WIDTH IS NOT NULL)
        """);

    var samples = LoadGisSamples(conn);
    return new GisObjectReport(
        RowCount: table.RowCount,
        DiscrimCounts: discrim,
        TopologyPairCount: topologyPairs,
        StammdatenRowCount: stammdatenRows,
        StammdatenWithLengthCount: withLength,
        StammdatenWithProfileCount: withProfile,
        Samples: samples);
}

static long CountScalar(FbConnection conn, string sql)
{
    try
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        return Convert.ToInt64(cmd.ExecuteScalar());
    }
    catch
    {
        return 0;
    }
}

static List<Dictionary<string, object?>> LoadGisSamples(FbConnection conn)
{
    var samples = new List<Dictionary<string, object?>>();
    try
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT FIRST 10 OBJ_NAME, DISCRIM, OBJ_LENGTH, PROFILE_HEIGHT, PROFILE_WIDTH, STR3, STR5
            FROM GISOBJECT
            WHERE DISCRIM IN ('Lt','Sc')
              AND OBJ_NAME IS NOT NULL
            ORDER BY OBJ_NAME
            """;
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            samples.Add(new Dictionary<string, object?>
            {
                ["objName"] = ReadValue(reader, 0),
                ["discrim"] = ReadValue(reader, 1),
                ["lengthM"] = ReadValue(reader, 2),
                ["profileHeightMm"] = ReadValue(reader, 3),
                ["profileWidthMm"] = ReadValue(reader, 4),
                ["street"] = ReadValue(reader, 5),
                ["city"] = ReadValue(reader, 6)
            });
        }
    }
    catch
    {
        // Optional sample only.
    }
    return samples;
}

static KnownTablesReport InspectKnownTables(List<TableReport> tables)
{
    return new KnownTablesReport(
        StationRows: RowCount(tables, "STATION"),
        FilmPosRows: RowCount(tables, "FILMPOS"),
        FotoRows: RowCount(tables, "FOTO"),
        MediumFileRows: RowCount(tables, "MEDIUMFILE"),
        ShootingSequenceRows: RowCount(tables, "SHOOTINGSEQUENCE"),
        SubActivityRows: RowCount(tables, "SUBACTIVITY"));
}

static long RowCount(List<TableReport> tables, string tableName)
    => tables.FirstOrDefault(t => string.Equals(t.Name, tableName, StringComparison.OrdinalIgnoreCase))?.RowCount ?? 0;

static StationReport? InspectStation(FbConnection conn, List<TableReport> tables)
{
    var rowCount = RowCount(tables, "STATION");
    if (rowCount == 0)
        return null;

    return new StationReport(
        RowCount: rowCount,
        WithCode: CountWhere(conn, "STATION", "CODE IS NOT NULL AND TRIM(CODE) <> ''"),
        WithContentCode: CountWhere(conn, "STATION", "CONTENTCODE IS NOT NULL"),
        WithDistance: CountWhere(conn, "STATION", "DISTANCE IS NOT NULL"),
        WithRemark: CountWhere(conn, "STATION", "REMARK IS NOT NULL AND TRIM(REMARK) <> ''"),
        WithRangeDamage: CountWhere(conn, "STATION", "RANGEDAMAGE IS NOT NULL"),
        WithRangeDamageLength: CountWhere(conn, "STATION", "RANGEDAMAGE_LENGTH IS NOT NULL"),
        WithObservationType: CountWhere(conn, "STATION", "OBSERVATION_TYPE IS NOT NULL"),
        WithObservationClass: CountWhere(conn, "STATION", "OBSERVATION_CLASS IS NOT NULL"),
        WithAnyNum: CountWhere(conn, "STATION", string.Join(" OR ", Enumerable.Range(1, 10).Select(i => $"NUM{i} IS NOT NULL"))),
        WithAnyStr: CountWhere(conn, "STATION", string.Join(" OR ", Enumerable.Range(1, 10).Select(i => $"STR{i} IS NOT NULL AND TRIM(STR{i}) <> ''"))),
        TopCodes: LoadTopCounts(conn, "STATION", "CODE", 30),
        Samples: LoadStationSamples(conn));
}

static long CountWhere(FbConnection conn, string table, string condition)
    => CountScalar(conn, $"SELECT COUNT(*) FROM {QuoteId(table)} WHERE {condition}");

static List<ValueCountReport> LoadTopCounts(FbConnection conn, string table, string column, int limit)
{
    var rows = new List<ValueCountReport>();
    try
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            SELECT FIRST {limit} {QuoteId(column)}, COUNT(*)
            FROM {QuoteId(table)}
            WHERE {QuoteId(column)} IS NOT NULL AND TRIM({QuoteId(column)}) <> ''
            GROUP BY {QuoteId(column)}
            ORDER BY 2 DESC
            """;
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            rows.Add(new ValueCountReport(Convert.ToString(reader.GetValue(0)), Convert.ToInt64(reader.GetValue(1))));
    }
    catch
    {
        // Optional code distribution only.
    }
    return rows;
}

static List<Dictionary<string, object?>> LoadStationSamples(FbConnection conn)
{
    var rows = new List<Dictionary<string, object?>>();
    try
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT FIRST 20
                ID,
                SUBACTIVITY_ID,
                DISTANCE,
                CODE,
                CONTENTCODE,
                REMARK,
                RANGEDAMAGE,
                RANGEDAMAGE_LENGTH,
                OBSERVATION_TYPE,
                OBSERVATION_CLASS,
                CONNECTION_POINT_NAME,
                NUM1,
                NUM2,
                NUM3,
                STR1,
                STR2,
                STR3
            FROM STATION
            ORDER BY ID
            """;
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < reader.FieldCount; i++)
                row[reader.GetName(i)] = ReadValue(reader, i);
            rows.Add(row);
        }
    }
    catch
    {
        // Optional samples only.
    }
    return rows;
}

static CadasterManifest BuildCadasterManifest(CliOptions options, List<string> fdbFiles, string? nativeClient, string outputDirectory)
{
    var sources = new List<CadasterManifestSource>();
    var samples = new List<CadasterManifestSample>();

    foreach (var fdbPath in fdbFiles)
    {
        var source = new CadasterManifestSource
        {
            SourceFdb = fdbPath,
            ProjectRoot = GuessProjectRoot(fdbPath)
        };
        sources.Add(source);

        try
        {
            var databasePath = PrepareFirebirdDatabasePath(fdbPath, outputDirectory);
            using var conn = OpenConnection(databasePath, nativeClient);
            var projectRoot = GuessProjectRoot(fdbPath);
            var photoLookup = BuildMediaLookup(projectRoot, "Foto");
            var videoLookup = BuildMediaLookup(projectRoot, "Film");

            var photosByStation = LoadPhotoManifestRows(conn, photoLookup);
            var filmPositionsByStation = LoadFilmPositionManifestRows(conn, videoLookup);
            var stationRows = LoadStationManifestRows(conn);

            source.Connected = true;
            source.StationCount = stationRows.Count;
            source.PhotoRowCount = photosByStation.Sum(p => p.Value.Count);
            source.FilmPositionRowCount = filmPositionsByStation.Sum(p => p.Value.Count);
            source.PhotoPathCount = photosByStation.Sum(p => p.Value.Count(x => !string.IsNullOrWhiteSpace(x.ResolvedPath)));
            source.VideoPathCount = filmPositionsByStation.Sum(p => p.Value.Count(x => !string.IsNullOrWhiteSpace(x.VideoPath)));

            foreach (var row in stationRows)
            {
                photosByStation.TryGetValue(row.StationId, out var photos);
                filmPositionsByStation.TryGetValue(row.StationId, out var filmPositions);
                photos ??= [];
                filmPositions ??= [];
                var primaryFilm = filmPositions.FirstOrDefault(fp => !string.IsNullOrWhiteSpace(fp.VideoPath)) ?? filmPositions.FirstOrDefault();

                samples.Add(new CadasterManifestSample
                {
                    SampleId = $"cadaster:{StableId(fdbPath)}:station:{row.StationId}",
                    Source = "cadaster-firebird",
                    SourceFdb = fdbPath,
                    ProjectRoot = projectRoot,
                    StationId = row.StationId,
                    SubActivityId = row.SubActivityId,
                    Code = row.Code,
                    ContentCode = row.ContentCode,
                    TrainingCategory = TrainingCategoryForCode(row.Code),
                    DistanceM = row.DistanceM,
                    Remark = row.Remark,
                    RangeDamage = row.RangeDamage,
                    RangeDamageLength = row.RangeDamageLength,
                    ObservationType = row.ObservationType,
                    ObservationClass = row.ObservationClass,
                    ConnectionPointName = row.ConnectionPointName,
                    Num1 = row.Num1,
                    Num2 = row.Num2,
                    Num3 = row.Num3,
                    Str1 = row.Str1,
                    Str2 = row.Str2,
                    Str3 = row.Str3,
                    PrimaryPhotoPath = photos.FirstOrDefault(p => !string.IsNullOrWhiteSpace(p.ResolvedPath))?.ResolvedPath,
                    PrimaryVideoPath = primaryFilm?.VideoPath,
                    PrimaryVideoPositionTime = primaryFilm?.PositionTime,
                    PrimaryVideoPositionFrame = primaryFilm?.PositionFrame,
                    PrimaryVideoPositionDistance = primaryFilm?.PositionDistance,
                    Photos = photos,
                    FilmPositions = filmPositions
                });
            }
        }
        catch (Exception ex)
        {
            source.Connected = false;
            source.Error = $"{ex.GetType().Name}: {ex.Message}";
        }
    }

    return new CadasterManifest
    {
        Meta = new CadasterManifestMeta
        {
            Schema = "cadaster-manifest-v1",
            GeneratedUtc = DateTime.UtcNow.ToString("O"),
            Root = options.Root,
            SourceFdbCount = fdbFiles.Count,
            ConnectedSourceCount = sources.Count(s => s.Connected),
            SampleCount = samples.Count,
            WithPhotoRowCount = samples.Count(s => s.Photos.Count > 0),
            WithPhotoPathCount = samples.Count(s => !string.IsNullOrWhiteSpace(s.PrimaryPhotoPath)),
            WithFilmPositionCount = samples.Count(s => s.FilmPositions.Count > 0),
            WithVideoPathCount = samples.Count(s => !string.IsNullOrWhiteSpace(s.PrimaryVideoPath)),
            DamageSampleCount = samples.Count(s => s.TrainingCategory == "schaden"),
            ComponentSampleCount = samples.Count(s => s.TrainingCategory == "bauteil"),
            MetaSampleCount = samples.Count(s => s.TrainingCategory == "meta"),
            OtherSampleCount = samples.Count(s => s.TrainingCategory == "other")
        },
        Sources = sources,
        Samples = samples
    };
}

static List<CadasterTopologyWriteResult> WriteCadasterTopologyOutputs(
    CliOptions options,
    List<string> fdbFiles,
    string? nativeClient,
    string firebirdCacheOutputDirectory,
    JsonSerializerOptions jsonOptions)
{
    var results = new List<CadasterTopologyWriteResult>();
    var outputRoot = ResolveTopologyOutputRoot(options);
    Directory.CreateDirectory(outputRoot);

    foreach (var fdbPath in fdbFiles)
    {
        try
        {
            var topology = BuildCadasterTopology(options, fdbPath, nativeClient, firebirdCacheOutputDirectory);
            var projectDir = Path.Combine(outputRoot, SanitizePathSegment(topology.ProjectName));
            Directory.CreateDirectory(projectDir);
            var topologyPath = Path.Combine(projectDir, "topology.json");
            File.WriteAllText(topologyPath, JsonSerializer.Serialize(topology, jsonOptions), Encoding.UTF8);
            results.Add(new CadasterTopologyWriteResult(fdbPath, topologyPath, topology.Haltungen.Count, null));
        }
        catch (Exception ex)
        {
            results.Add(new CadasterTopologyWriteResult(fdbPath, null, 0, $"{ex.GetType().Name}: {ex.Message}"));
        }
    }

    return results;
}

static string ResolveTopologyOutputRoot(CliOptions options)
{
    if (!string.IsNullOrWhiteSpace(options.TopologyOutputDirectory))
        return options.TopologyOutputDirectory;

    var sharedToolOutput = Path.GetFullPath(Path.Combine(
        Environment.CurrentDirectory,
        "tools",
        "HaltungTopologyExtractor",
        "output"));
    if (Directory.Exists(Path.GetDirectoryName(sharedToolOutput)))
        return sharedToolOutput;

    return options.OutputDirectory;
}

static CadasterTopologyDocument BuildCadasterTopology(
    CliOptions options,
    string fdbPath,
    string? nativeClient,
    string firebirdCacheOutputDirectory)
{
    var databasePath = PrepareFirebirdDatabasePath(fdbPath, firebirdCacheOutputDirectory);
    using var conn = OpenConnection(databasePath, nativeClient);

    var projectRoot = GuessProjectRoot(fdbPath);
    var projectName = new DirectoryInfo(projectRoot).Name;
    if (string.IsNullOrWhiteSpace(projectName))
        projectName = Path.GetFileNameWithoutExtension(fdbPath);

    var topologyRows = LoadCadasterTopologyRows(conn);
    var stammdatenByPair = LoadCadasterStammdatenPairs(conn);
    var warnings = new List<string>();
    var haltungen = ResolveCadasterHaltungen(stammdatenByPair, topologyRows, warnings);

    if (haltungen.Count == 0)
        warnings.Add("Keine eindeutigen GISOBJECT-Topologiepaare gefunden.");

    return new CadasterTopologyDocument
    {
        GeneratedUtc = DateTime.UtcNow.ToString("O"),
        SourcePath = fdbPath,
        ProjectName = projectName,
        Warnings = warnings,
        Haltungen = haltungen
    };
}

static List<CadasterTopologyHolding> ResolveCadasterHaltungen(
    Dictionary<string, List<CadasterStammdatenPair>> stammdatenByPair,
    List<CadasterRawTopologyPair> topologyRows,
    List<string> globalWarnings)
{
    var haltungen = new List<CadasterTopologyHolding>();
    var stammdatenGroups = stammdatenByPair
        .SelectMany(p => p.Value)
        .GroupBy(p => UnorderedPairKey(p.StartObjName, p.EndObjName))
        .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
        .ToList();

    if (stammdatenGroups.Count > 0)
    {
        foreach (var group in stammdatenGroups)
        {
            var matches = group.ToList();
            var first = matches[0];
            var warnings = new List<string>();
            var directedPairs = matches
                .Select(p => $"{CleanNodeId(p.StartObjName)}-{CleanNodeId(p.EndObjName)}")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var schachtOben = CleanNodeId(first.StartObjName);
            var schachtUnten = CleanNodeId(first.EndObjName);
            var fliessrichtungQuelle = "cadaster_pair_name";

            if (directedPairs.Count > 1)
            {
                var ordered = matches
                    .SelectMany(p => new[] { CleanNodeId(p.StartObjName), CleanNodeId(p.EndObjName) })
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                    .Take(2)
                    .ToList();
                schachtOben = ordered.ElementAtOrDefault(0) ?? schachtOben;
                schachtUnten = ordered.ElementAtOrDefault(1) ?? schachtUnten;
                fliessrichtungQuelle = "unsicher";
                warnings.Add("Mehrere Stammdaten-Richtungen (cadaster) fuer dieselbe Haltung; strikt-Modus.");
            }

            if (matches.Count > 1 && directedPairs.Count == 1)
                warnings.Add($"Mehrere Stammdaten-Zeilen fuer {schachtOben}-{schachtUnten}; erster Eintrag verwendet.");

            if (string.IsNullOrWhiteSpace(schachtOben) || string.IsNullOrWhiteSpace(schachtUnten))
            {
                globalWarnings.Add($"Stammdaten {first.ObjName}: leerer Schachtname, uebersprungen.");
                continue;
            }

            if (fliessrichtungQuelle == "unsicher")
                globalWarnings.Add($"{schachtOben}-{schachtUnten}: Fliessrichtung unsicher, strikt-Modus.");

            var ht = new CadasterTopologyHolding
            {
                HaltungPk = $"GISOBJECT:{first.Id}",
                CanonicalFolderName = $"{schachtOben}-{schachtUnten}",
                SchachtOben = schachtOben,
                SchachtUnten = schachtUnten,
                FliessrichtungQuelle = fliessrichtungQuelle,
                LaengeM = first.LengthM,
                AlternativeHaltungIds = BuildAlternativeHoldingIds(schachtOben, schachtUnten),
                VideoDateinamenAusDb = [],
                Inspektionen = [],
                Warnings = warnings
            };
            ApplyTopologyConventions(ht);
            haltungen.Add(ht);
        }

        return haltungen
            .OrderBy(h => h.CanonicalFolderName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    foreach (var pairName in topologyRows
                 .SelectMany(r => new[] { r.StartObjName, r.EndObjName })
                 .Where(name => TrySplitHoldingPair(name, out _, out _))
                 .Distinct(StringComparer.OrdinalIgnoreCase)
                 .OrderBy(name => name, StringComparer.OrdinalIgnoreCase))
    {
        TrySplitHoldingPair(pairName, out var schachtOben, out var schachtUnten);
        var ht = new CadasterTopologyHolding
        {
            HaltungPk = $"GISOBJECT_FALLBACK:{StableId(pairName)}",
            CanonicalFolderName = $"{schachtOben}-{schachtUnten}",
            SchachtOben = schachtOben,
            SchachtUnten = schachtUnten,
            FliessrichtungQuelle = "cadaster_gis_pair_name",
            LaengeM = null,
            AlternativeHaltungIds = BuildAlternativeHoldingIds(schachtOben, schachtUnten),
            VideoDateinamenAusDb = [],
            Inspektionen = [],
            Warnings = ["Keine Lt/Sc-Stammdatenzeile gefunden; aus GISOBJECT-Paarnamen abgeleitet."]
        };
        ApplyTopologyConventions(ht);
        haltungen.Add(ht);
    }

    return haltungen
        .OrderBy(h => h.CanonicalFolderName, StringComparer.OrdinalIgnoreCase)
        .ToList();
}

// Lokale Konvention: Schaechte mit "10.<ziffern>"-Praefix gehoeren immer
// auf die Downstream-Seite. Spiegelt schacht_oben/schacht_unten + canonical
// in-place. Mirror der Python-Implementierung.
static void ApplyTopologyConventions(CadasterTopologyHolding h)
{
    if (h is null) return;
    var oben = (h.SchachtOben ?? "").Trim();
    var unten = (h.SchachtUnten ?? "").Trim();
    if (!System.Text.RegularExpressions.Regex.IsMatch(oben, @"^10\.\d+$")) return;
    h.SchachtOben = unten;
    h.SchachtUnten = oben;
    h.CanonicalFolderName = $"{unten}-{oben}";
    if (!h.FliessrichtungQuelle.Contains("+10dot_rule"))
        h.FliessrichtungQuelle += "+10dot_rule";
    const string msg = "schacht-Reihenfolge per 10.xxx-Konvention korrigiert";
    if (!h.Warnings.Contains(msg))
        h.Warnings.Add(msg);
}

static List<CadasterRawTopologyPair> LoadCadasterTopologyRows(FbConnection conn)
{
    var rows = new List<CadasterRawTopologyPair>();
    using var cmd = new FbCommand("""
        SELECT startObj.ID, startObj.OBJ_NAME, startObj.DISCRIM, startObj.OBJ_LENGTH,
               endObj.ID, endObj.OBJ_NAME
        FROM GISOBJECT startObj
        JOIN GISOBJECT endObj ON startObj.GISOBJECT_END = endObj.ID
        WHERE startObj.DISCRIM = 'Cn'
          AND startObj.OBJ_NAME IS NOT NULL
          AND endObj.OBJ_NAME IS NOT NULL
        ORDER BY startObj.OBJ_NAME, endObj.OBJ_NAME
        """, conn);
    using var reader = cmd.ExecuteReader();
    while (reader.Read())
    {
        rows.Add(new CadasterRawTopologyPair(
            Id: RequiredLong(reader, 0),
            StartObjName: CleanNodeId(OptionalString(reader, 1) ?? ""),
            Discrim: OptionalString(reader, 2) ?? "",
            LengthM: OptionalDouble(reader, 3),
            EndId: RequiredLong(reader, 4),
            EndObjName: CleanNodeId(OptionalString(reader, 5) ?? "")));
    }

    return rows;
}

static Dictionary<string, List<CadasterStammdatenPair>> LoadCadasterStammdatenPairs(FbConnection conn)
{
    var pairs = new Dictionary<string, List<CadasterStammdatenPair>>(StringComparer.OrdinalIgnoreCase);
    using var cmd = new FbCommand("""
        SELECT ID, OBJ_NAME, DISCRIM, OBJ_LENGTH
        FROM GISOBJECT
        WHERE DISCRIM IN ('Lt','Sc')
          AND OBJ_NAME IS NOT NULL
        ORDER BY OBJ_NAME
        """, conn);
    using var reader = cmd.ExecuteReader();
    while (reader.Read())
    {
        var objName = OptionalString(reader, 1) ?? "";
        if (!TrySplitHoldingPair(objName, out var start, out var end))
            continue;

        var pair = new CadasterStammdatenPair(
            Id: RequiredLong(reader, 0),
            ObjName: objName.Trim(),
            Discrim: OptionalString(reader, 2) ?? "",
            LengthM: OptionalDouble(reader, 3),
            StartObjName: start,
            EndObjName: end);
        var key = UnorderedPairKey(start, end);
        if (!pairs.TryGetValue(key, out var list))
        {
            list = [];
            pairs[key] = list;
        }
        list.Add(pair);
    }

    return pairs;
}

static bool TrySplitHoldingPair(string value, out string start, out string end)
{
    start = "";
    end = "";
    var clean = value.Trim();
    var dash = clean.IndexOf('-');
    if (dash <= 0 || dash >= clean.Length - 1)
        return false;

    start = CleanNodeId(clean[..dash]);
    end = CleanNodeId(clean[(dash + 1)..]);
    return !string.IsNullOrWhiteSpace(start) && !string.IsNullOrWhiteSpace(end);
}

static string UnorderedPairKey(string a, string b)
{
    var parts = new[] { NormalizePairComponent(a), NormalizePairComponent(b) }
        .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
        .ToArray();
    return $"{parts[0]}|{parts[1]}";
}

static string NormalizePairComponent(string value)
{
    var sb = new StringBuilder();
    foreach (var ch in CleanNodeId(value).ToLowerInvariant())
    {
        if (char.IsLetterOrDigit(ch))
            sb.Append(ch);
    }
    return sb.ToString();
}

static string CleanNodeId(string value)
{
    var clean = (value ?? "").Trim().Replace(" ", "");
    if (clean.EndsWith(".0", StringComparison.Ordinal) &&
        clean[..^2].All(char.IsDigit))
    {
        return clean[..^2];
    }
    return clean;
}

static List<string> BuildAlternativeHoldingIds(string schachtOben, string schachtUnten)
{
    var variants = new List<string>();
    AddPairVariants(variants, schachtOben, schachtUnten);
    AddPairVariants(variants, schachtUnten, schachtOben);
    return variants
        .Where(v => !string.IsNullOrWhiteSpace(v))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();
}

static void AddPairVariants(List<string> variants, string a, string b)
{
    if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b))
        return;

    variants.Add($"{a}-{b}");

    var aNoDot = a.Replace(".", "");
    var bNoDot = b.Replace(".", "");
    if (!string.Equals(aNoDot, a, StringComparison.Ordinal) || !string.Equals(bNoDot, b, StringComparison.Ordinal))
        variants.Add($"{aNoDot}-{bNoDot}");

    var aTail = a.Split('.', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? a;
    var bTail = b.Split('.', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? b;
    if (!string.Equals(aTail, a, StringComparison.Ordinal) || !string.Equals(bTail, b, StringComparison.Ordinal))
        variants.Add($"{aTail}-{bTail}");
}

static string SanitizePathSegment(string value)
{
    var invalid = Path.GetInvalidFileNameChars().ToHashSet();
    var cleaned = new string(value.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray()).Trim();
    return string.IsNullOrWhiteSpace(cleaned) ? "cadaster_project" : cleaned;
}

static Dictionary<long, List<CadasterPhotoRef>> LoadPhotoManifestRows(FbConnection conn, MediaLookup lookup)
{
    var byStation = new Dictionary<long, List<CadasterPhotoRef>>();
    using var cmd = conn.CreateCommand();
    cmd.CommandText = """
        SELECT f.ID, f.FOTONAME, f.FILE_EXTENSION, f.FILESIZE, f.STATION_ID, f.SUBACTIVITY_ID, f.FILE_EXISTS, f.RECORDING_DATE, f.SOURCE_FOTO_NAME, mf.NAME
        FROM FOTO f
        LEFT JOIN MEDIUMFILE mf ON mf.FILE_ID = f.ID
        WHERE f.STATION_ID IS NOT NULL
        ORDER BY f.STATION_ID, f.ID
        """;
    using var reader = cmd.ExecuteReader();
    while (reader.Read())
    {
        var stationId = RequiredLong(reader, 4);
        var photoName = OptionalString(reader, 1);
        var extension = OptionalString(reader, 2);
        var mediaFileName = OptionalString(reader, 9);
        var row = new CadasterPhotoRef
        {
            PhotoId = RequiredLong(reader, 0),
            PhotoName = photoName,
            MediaFileName = mediaFileName,
            FileExtension = extension,
            FileSize = OptionalLong(reader, 3),
            StationId = stationId,
            SubActivityId = OptionalLong(reader, 5),
            FileExistsFlag = OptionalBool(reader, 6),
            RecordingDate = OptionalIsoDate(reader, 7),
            SourcePhotoName = OptionalString(reader, 8),
            ResolvedPath = lookup.Resolve(mediaFileName, null) ?? lookup.Resolve(photoName, extension)
        };

        if (!byStation.TryGetValue(stationId, out var list))
        {
            list = [];
            byStation[stationId] = list;
        }
        list.Add(row);
    }
    return byStation;
}

static Dictionary<long, List<CadasterFilmPositionRef>> LoadFilmPositionManifestRows(FbConnection conn, MediaLookup lookup)
{
    var byStation = new Dictionary<long, List<CadasterFilmPositionRef>>();
    using var cmd = conn.CreateCommand();
    cmd.CommandText = """
        SELECT
            fp.ID,
            fp.STATION_ID,
            fp.SHOOTINGSEQUENCE_ID,
            fp.POSITIONDISTANCE,
            fp.POSITIONTIME,
            fp.POSITIONFRAME,
            fp.VIEWINGANGLE,
            fp.VIEWINGDIRECTION,
            fp.VIEWINGZOOM,
            ss.SHOOTINGFILE_ID,
            ss.SEQUENCENAME,
            ss.TIME_START,
            ss.TIME_END,
            ss.FRAME_START,
            ss.FRAME_END,
            ss.VALUE_START,
            ss.VALUE_END,
            sf.FILE_NAME,
            sf.FILE_EXTENSION,
            sf.FILE_EXISTS,
            sf.FILE_SIZE,
            mf.NAME
        FROM FILMPOS fp
        LEFT JOIN SHOOTINGSEQUENCE ss ON ss.ID = fp.SHOOTINGSEQUENCE_ID
        LEFT JOIN SHOOTINGFILE sf ON sf.ID = ss.SHOOTINGFILE_ID
        LEFT JOIN MEDIUMFILE mf ON mf.FILE_ID = sf.ID
        WHERE fp.STATION_ID IS NOT NULL
        ORDER BY fp.STATION_ID, fp.ID
        """;
    using var reader = cmd.ExecuteReader();
    while (reader.Read())
    {
        var stationId = RequiredLong(reader, 1);
        var fileName = OptionalString(reader, 17);
        var extension = OptionalString(reader, 18);
        var mediaFileName = OptionalString(reader, 21);
        var row = new CadasterFilmPositionRef
        {
            FilmPositionId = RequiredLong(reader, 0),
            StationId = stationId,
            ShootingSequenceId = OptionalLong(reader, 2),
            PositionDistance = OptionalDouble(reader, 3),
            PositionTime = OptionalDouble(reader, 4),
            PositionFrame = OptionalLong(reader, 5),
            ViewingAngle = OptionalDouble(reader, 6),
            ViewingDirection = OptionalDouble(reader, 7),
            ViewingZoom = OptionalDouble(reader, 8),
            ShootingFileId = OptionalLong(reader, 9),
            SequenceName = OptionalString(reader, 10),
            SequenceTimeStart = OptionalDouble(reader, 11),
            SequenceTimeEnd = OptionalDouble(reader, 12),
            SequenceFrameStart = OptionalLong(reader, 13),
            SequenceFrameEnd = OptionalLong(reader, 14),
            SequenceValueStart = OptionalDouble(reader, 15),
            SequenceValueEnd = OptionalDouble(reader, 16),
            VideoFileName = fileName,
            MediaFileName = mediaFileName,
            VideoFileExtension = extension,
            VideoFileExistsFlag = OptionalBool(reader, 19),
            VideoFileSize = OptionalLong(reader, 20),
            VideoPath = lookup.Resolve(mediaFileName, null) ?? lookup.Resolve(fileName, extension)
        };

        if (!byStation.TryGetValue(stationId, out var list))
        {
            list = [];
            byStation[stationId] = list;
        }
        list.Add(row);
    }
    return byStation;
}

static List<CadasterStationRow> LoadStationManifestRows(FbConnection conn)
{
    var rows = new List<CadasterStationRow>();
    using var cmd = conn.CreateCommand();
    cmd.CommandText = """
        SELECT
            ID,
            SUBACTIVITY_ID,
            DISTANCE,
            CODE,
            CONTENTCODE,
            REMARK,
            RANGEDAMAGE,
            RANGEDAMAGE_LENGTH,
            OBSERVATION_TYPE,
            OBSERVATION_CLASS,
            CONNECTION_POINT_NAME,
            NUM1,
            NUM2,
            NUM3,
            STR1,
            STR2,
            STR3
        FROM STATION
        ORDER BY ID
        """;
    using var reader = cmd.ExecuteReader();
    while (reader.Read())
    {
        rows.Add(new CadasterStationRow
        {
            StationId = RequiredLong(reader, 0),
            SubActivityId = OptionalLong(reader, 1),
            DistanceM = OptionalDouble(reader, 2),
            Code = OptionalString(reader, 3) ?? "",
            ContentCode = OptionalString(reader, 4),
            Remark = OptionalString(reader, 5),
            RangeDamage = OptionalBool(reader, 6),
            RangeDamageLength = OptionalDouble(reader, 7),
            ObservationType = OptionalLong(reader, 8),
            ObservationClass = OptionalLong(reader, 9),
            ConnectionPointName = OptionalString(reader, 10),
            Num1 = OptionalDouble(reader, 11),
            Num2 = OptionalDouble(reader, 12),
            Num3 = OptionalDouble(reader, 13),
            Str1 = OptionalString(reader, 14),
            Str2 = OptionalString(reader, 15),
            Str3 = OptionalString(reader, 16)
        });
    }
    return rows;
}

static MediaLookup BuildMediaLookup(string projectRoot, string folderName)
{
    var folder = Path.Combine(projectRoot, folderName);
    if (!Directory.Exists(folder))
        folder = projectRoot;

    var paths = EnumerateFilesSafe(folder, "*", SearchOption.AllDirectories);
    return new MediaLookup(paths);
}

static string TrainingCategoryForCode(string? code)
{
    if (string.IsNullOrWhiteSpace(code))
        return "other";

    var upper = code.Trim().ToUpperInvariant();
    var metaCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "BCD", "BCE", "BDA", "BDB", "BDC", "AEC", "AED", "AEF"
    };

    if (metaCodes.Contains(upper))
        return "meta";
    if (upper.StartsWith("BCA", StringComparison.OrdinalIgnoreCase) || upper.StartsWith("BCC", StringComparison.OrdinalIgnoreCase))
        return "bauteil";
    if (upper.StartsWith("BA", StringComparison.OrdinalIgnoreCase) || upper.StartsWith("BB", StringComparison.OrdinalIgnoreCase))
        return "schaden";
    return "other";
}

static long RequiredLong(FbDataReader reader, int index) => Convert.ToInt64(reader.GetValue(index));

static long? OptionalLong(FbDataReader reader, int index)
    => reader.IsDBNull(index) ? null : Convert.ToInt64(reader.GetValue(index));

static double? OptionalDouble(FbDataReader reader, int index)
{
    if (reader.IsDBNull(index))
        return null;
    var value = reader.GetValue(index);
    if (value is DateTime or TimeSpan)
        return null;
    try
    {
        return Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture);
    }
    catch
    {
        return null;
    }
}

static string? OptionalString(FbDataReader reader, int index)
{
    if (reader.IsDBNull(index))
        return null;
    var value = Convert.ToString(reader.GetValue(index))?.Trim();
    return string.IsNullOrWhiteSpace(value) ? null : value;
}

static bool? OptionalBool(FbDataReader reader, int index)
{
    if (reader.IsDBNull(index))
        return null;
    var value = reader.GetValue(index);
    if (value is bool b)
        return b;
    if (value is string s)
        return s.Equals("true", StringComparison.OrdinalIgnoreCase) || s == "1" || s.Equals("Y", StringComparison.OrdinalIgnoreCase);
    return Convert.ToInt64(value) != 0;
}

static string? OptionalIsoDate(FbDataReader reader, int index)
{
    if (reader.IsDBNull(index))
        return null;
    var value = reader.GetValue(index);
    return value is DateTime dt ? dt.ToString("O") : Convert.ToString(value);
}

static object? ReadValue(FbDataReader reader, int index)
{
    if (reader.IsDBNull(index))
        return null;
    var value = reader.GetValue(index);
    if (value is string s)
        return s.Trim();
    return value;
}

static List<CandidateTableReport> FindCandidateTables(List<TableReport> tables, CandidateKind kind)
{
    var result = new List<CandidateTableReport>();
    foreach (var table in tables)
    {
        var score = ScoreTable(table, kind);
        if (score <= 0)
            continue;
        result.Add(new CandidateTableReport(table.Name, table.RowCount, score, table.Columns));
    }

    return result
        .OrderByDescending(t => t.Score)
        .ThenByDescending(t => t.RowCount)
        .ThenBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
        .Take(20)
        .ToList();
}

static int ScoreTable(TableReport table, CandidateKind kind)
{
    var tableName = table.Name.ToUpperInvariant();
    var columns = table.Columns.Select(c => c.ToUpperInvariant()).ToList();
    var score = 0;

    switch (kind)
    {
        case CandidateKind.Media:
            if (ContainsAny(tableName, "PHOTO", "FOTO", "IMAGE", "PIC", "MEDIA", "MM")) score += 6;
            if (columns.Any(c => ContainsAny(c, "FILE", "PATH", "NAME", "DATEI", "PHOTO", "IMAGE"))) score += 4;
            if (columns.Any(c => ContainsAny(c, "OBJ", "HOLD", "HALT", "SECTION", "PIPE"))) score += 2;
            break;
        case CandidateKind.Observation:
            if (ContainsAny(tableName, "OBS", "SCHAD", "DAMAGE", "DEFECT", "INSPECT", "INSPEK")) score += 6;
            if (columns.Any(c => ContainsAny(c, "CODE", "SCHAD", "DAMAGE", "DEFECT", "OBS"))) score += 5;
            if (columns.Any(c => ContainsAny(c, "DIST", "METER", "TIME", "CLOCK", "UHR"))) score += 3;
            break;
        case CandidateKind.CodeLike:
            if (columns.Any(c => ContainsAny(c, "CODE", "SCHAD", "DEFECT", "DAMAGE", "CLASS"))) score += 5;
            if (columns.Any(c => ContainsAny(c, "DIST", "METER", "TIME", "CLOCK", "UHR"))) score += 2;
            break;
    }

    return score;
}

static bool ContainsAny(string text, params string[] keys)
{
    foreach (var key in keys)
    {
        if (text.Contains(key, StringComparison.OrdinalIgnoreCase))
            return true;
    }
    return false;
}

static string QuoteId(string id) => "\"" + id.Replace("\"", "\"\"") + "\"";

static string GuessProjectRoot(string fdbPath)
{
    var dir = Directory.GetParent(fdbPath);
    if (dir is null)
        return "";
    if (string.Equals(dir.Name, "Data", StringComparison.OrdinalIgnoreCase) && dir.Parent is not null)
        return dir.Parent.FullName;
    return dir.FullName;
}

static AggregateReport BuildAggregate(CliOptions options, List<FdbReport> reports, string? nativeClient)
{
    var connected = reports.Where(r => r.Connected).ToList();
    return new AggregateReport(
        Meta: new AggregateMeta(
            Schema: Schema,
            GeneratedUtc: DateTime.UtcNow.ToString("O"),
            Root: options.Root,
            NativeClientPath: nativeClient,
            FdbCount: reports.Count,
            ConnectedCount: connected.Count,
            GisobjectRowCount: reports.Sum(r => r.GisObject?.RowCount ?? 0),
            TopologyPairCount: reports.Sum(r => r.GisObject?.TopologyPairCount ?? 0),
            StammdatenRowCount: reports.Sum(r => r.GisObject?.StammdatenRowCount ?? 0),
            StammdatenWithLengthCount: reports.Sum(r => r.GisObject?.StammdatenWithLengthCount ?? 0),
            StammdatenWithProfileCount: reports.Sum(r => r.GisObject?.StammdatenWithProfileCount ?? 0),
            StationRowCount: reports.Sum(r => r.Station?.RowCount ?? 0),
            StationWithCodeCount: reports.Sum(r => r.Station?.WithCode ?? 0),
            StationWithContentCodeCount: reports.Sum(r => r.Station?.WithContentCode ?? 0),
            StationWithDistanceCount: reports.Sum(r => r.Station?.WithDistance ?? 0),
            StationWithRemarkCount: reports.Sum(r => r.Station?.WithRemark ?? 0),
            FotoRowCount: reports.Sum(r => r.KnownTables?.FotoRows ?? 0),
            FilmPosRowCount: reports.Sum(r => r.KnownTables?.FilmPosRows ?? 0),
            MediumFileRowCount: reports.Sum(r => r.KnownTables?.MediumFileRows ?? 0),
            MediaCandidateTableCount: reports.Sum(r => r.MediaCandidates.Count),
            ObservationCandidateTableCount: reports.Sum(r => r.ObservationCandidates.Count)),
        Fdbs: reports);
}

[DllImport("kernel32.dll", SetLastError = false)]
static extern void ExitProcess(uint exitCode);

[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
static extern bool SetDllDirectory(string? lpPathName);

[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
static extern int GetShortPathName(string lpszLongPath, StringBuilder lpszShortPath, int cchBuffer);

internal enum CandidateKind
{
    Media,
    Observation,
    CodeLike
}

internal sealed record CliOptions(
    string Root,
    string? SingleFdb,
    string OutputDirectory,
    string? FbClient,
    int Max,
    bool Quiet,
    bool ExportManifest,
    bool ExportTopology,
    string? TopologyOutputDirectory);

internal sealed record AggregateReport(AggregateMeta Meta, List<FdbReport> Fdbs);

internal sealed record AggregateMeta(
    string Schema,
    string GeneratedUtc,
    string Root,
    string? NativeClientPath,
    int FdbCount,
    int ConnectedCount,
    long GisobjectRowCount,
    long TopologyPairCount,
    long StammdatenRowCount,
    long StammdatenWithLengthCount,
    long StammdatenWithProfileCount,
    long StationRowCount,
    long StationWithCodeCount,
    long StationWithContentCodeCount,
    long StationWithDistanceCount,
    long StationWithRemarkCount,
    long FotoRowCount,
    long FilmPosRowCount,
    long MediumFileRowCount,
    int MediaCandidateTableCount,
    int ObservationCandidateTableCount);

internal sealed class FdbReport
{
    public string FdbPath { get; set; } = "";
    public string DatabaseConnectionPath { get; set; } = "";
    public long SizeBytes { get; set; }
    public string ProjectRootGuess { get; set; } = "";
    public bool Connected { get; set; }
    public string? Error { get; set; }
    public int TableCount { get; set; }
    public List<TableReport> Tables { get; set; } = [];
    public GisObjectReport? GisObject { get; set; }
    public KnownTablesReport? KnownTables { get; set; }
    public StationReport? Station { get; set; }
    public List<CandidateTableReport> MediaCandidates { get; set; } = [];
    public List<CandidateTableReport> ObservationCandidates { get; set; } = [];
    public List<CandidateTableReport> CodeLikeCandidates { get; set; } = [];
}

internal sealed class TableReport
{
    public string Name { get; set; } = "";
    public long RowCount { get; set; }
    public List<string> Columns { get; set; } = [];
}

internal sealed record CandidateTableReport(
    string Name,
    long RowCount,
    int Score,
    List<string> Columns);

internal sealed record KnownTablesReport(
    long StationRows,
    long FilmPosRows,
    long FotoRows,
    long MediumFileRows,
    long ShootingSequenceRows,
    long SubActivityRows);

internal sealed record StationReport(
    long RowCount,
    long WithCode,
    long WithContentCode,
    long WithDistance,
    long WithRemark,
    long WithRangeDamage,
    long WithRangeDamageLength,
    long WithObservationType,
    long WithObservationClass,
    long WithAnyNum,
    long WithAnyStr,
    List<ValueCountReport> TopCodes,
    List<Dictionary<string, object?>> Samples);

internal sealed record ValueCountReport(string? Value, long Count);

internal sealed record GisObjectReport(
    long RowCount,
    Dictionary<string, long> DiscrimCounts,
    long TopologyPairCount,
    long StammdatenRowCount,
    long StammdatenWithLengthCount,
    long StammdatenWithProfileCount,
    List<Dictionary<string, object?>> Samples);

internal sealed record CadasterTopologyWriteResult(string SourceFdb, string? TopologyPath, int HaltungCount, string? Error)
{
    public bool Written => string.IsNullOrWhiteSpace(Error) && !string.IsNullOrWhiteSpace(TopologyPath);
}

internal sealed record CadasterRawTopologyPair(
    long Id,
    string StartObjName,
    string Discrim,
    double? LengthM,
    long EndId,
    string EndObjName);

internal sealed record CadasterStammdatenPair(
    long Id,
    string ObjName,
    string Discrim,
    double? LengthM,
    string StartObjName,
    string EndObjName);

internal sealed class CadasterTopologyDocument
{
    [JsonPropertyName("schema")]
    public string Schema { get; set; } = "haltung-topology-v1";

    [JsonPropertyName("generated_utc")]
    public string GeneratedUtc { get; set; } = "";

    [JsonPropertyName("source_type")]
    public string SourceType { get; set; } = "cadaster-db-firebird";

    [JsonPropertyName("source_path")]
    public string SourcePath { get; set; } = "";

    [JsonPropertyName("project_name")]
    public string ProjectName { get; set; } = "";

    [JsonPropertyName("warnings")]
    public List<string> Warnings { get; set; } = [];

    [JsonPropertyName("haltungen")]
    public List<CadasterTopologyHolding> Haltungen { get; set; } = [];
}

internal sealed class CadasterTopologyHolding
{
    [JsonPropertyName("haltung_pk")]
    public string HaltungPk { get; set; } = "";

    [JsonPropertyName("canonical_folder_name")]
    public string CanonicalFolderName { get; set; } = "";

    [JsonPropertyName("schacht_oben")]
    public string SchachtOben { get; set; } = "";

    [JsonPropertyName("schacht_unten")]
    public string SchachtUnten { get; set; } = "";

    [JsonPropertyName("fliessrichtung_quelle")]
    public string FliessrichtungQuelle { get; set; } = "";

    [JsonPropertyName("laenge_m")]
    public double? LaengeM { get; set; }

    [JsonPropertyName("alternative_haltung_ids")]
    public List<string> AlternativeHaltungIds { get; set; } = [];

    [JsonPropertyName("video_dateinamen_aus_db")]
    public List<string> VideoDateinamenAusDb { get; set; } = [];

    [JsonPropertyName("inspektionen")]
    public List<object> Inspektionen { get; set; } = [];

    [JsonPropertyName("warnings")]
    public List<string> Warnings { get; set; } = [];
}

internal sealed class MediaLookup
{
    private readonly Dictionary<string, string> _byFileName = new(StringComparer.OrdinalIgnoreCase);

    public MediaLookup(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            var name = Path.GetFileName(path);
            if (!_byFileName.ContainsKey(name))
                _byFileName[name] = path;
        }
    }

    public string? Resolve(string? fileName, string? extension)
    {
        foreach (var candidate in CandidateNames(fileName, extension))
        {
            if (_byFileName.TryGetValue(candidate, out var path))
                return path;
        }
        return null;
    }

    private static IEnumerable<string> CandidateNames(string? fileName, string? extension)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            yield break;

        var cleanName = fileName.Trim();
        yield return cleanName;
        var baseName = Path.GetFileName(cleanName);
        if (!string.Equals(baseName, cleanName, StringComparison.OrdinalIgnoreCase))
            yield return baseName;

        var ext = NormalizeExtension(extension);
        if (!string.IsNullOrWhiteSpace(ext) && string.IsNullOrWhiteSpace(Path.GetExtension(cleanName)))
        {
            yield return cleanName + ext;
            if (!string.Equals(baseName, cleanName, StringComparison.OrdinalIgnoreCase))
                yield return baseName + ext;
        }
    }

    private static string? NormalizeExtension(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
            return null;
        var ext = extension.Trim();
        return ext.StartsWith('.') ? ext : "." + ext;
    }
}

internal sealed class CadasterManifest
{
    public CadasterManifestMeta Meta { get; set; } = new();
    public List<CadasterManifestSource> Sources { get; set; } = [];
    public List<CadasterManifestSample> Samples { get; set; } = [];
}

internal sealed class CadasterManifestMeta
{
    public string Schema { get; set; } = "";
    public string GeneratedUtc { get; set; } = "";
    public string Root { get; set; } = "";
    public int SourceFdbCount { get; set; }
    public int ConnectedSourceCount { get; set; }
    public int SampleCount { get; set; }
    public int WithPhotoRowCount { get; set; }
    public int WithPhotoPathCount { get; set; }
    public int WithFilmPositionCount { get; set; }
    public int WithVideoPathCount { get; set; }
    public int DamageSampleCount { get; set; }
    public int ComponentSampleCount { get; set; }
    public int MetaSampleCount { get; set; }
    public int OtherSampleCount { get; set; }
}

internal sealed class CadasterManifestSource
{
    public string SourceFdb { get; set; } = "";
    public string ProjectRoot { get; set; } = "";
    public bool Connected { get; set; }
    public string? Error { get; set; }
    public int StationCount { get; set; }
    public int PhotoRowCount { get; set; }
    public int PhotoPathCount { get; set; }
    public int FilmPositionRowCount { get; set; }
    public int VideoPathCount { get; set; }
}

internal sealed class CadasterManifestSample
{
    public string SampleId { get; set; } = "";
    public string Source { get; set; } = "";
    public string SourceFdb { get; set; } = "";
    public string ProjectRoot { get; set; } = "";
    public long StationId { get; set; }
    public long? SubActivityId { get; set; }
    public string Code { get; set; } = "";
    public string? ContentCode { get; set; }
    public string TrainingCategory { get; set; } = "other";
    public double? DistanceM { get; set; }
    public string? Remark { get; set; }
    public bool? RangeDamage { get; set; }
    public double? RangeDamageLength { get; set; }
    public long? ObservationType { get; set; }
    public long? ObservationClass { get; set; }
    public string? ConnectionPointName { get; set; }
    public double? Num1 { get; set; }
    public double? Num2 { get; set; }
    public double? Num3 { get; set; }
    public string? Str1 { get; set; }
    public string? Str2 { get; set; }
    public string? Str3 { get; set; }
    public string? PrimaryPhotoPath { get; set; }
    public string? PrimaryVideoPath { get; set; }
    public double? PrimaryVideoPositionTime { get; set; }
    public long? PrimaryVideoPositionFrame { get; set; }
    public double? PrimaryVideoPositionDistance { get; set; }
    public List<CadasterPhotoRef> Photos { get; set; } = [];
    public List<CadasterFilmPositionRef> FilmPositions { get; set; } = [];
}

internal sealed class CadasterPhotoRef
{
    public long PhotoId { get; set; }
    public string? PhotoName { get; set; }
    public string? MediaFileName { get; set; }
    public string? FileExtension { get; set; }
    public long? FileSize { get; set; }
    public long StationId { get; set; }
    public long? SubActivityId { get; set; }
    public bool? FileExistsFlag { get; set; }
    public string? RecordingDate { get; set; }
    public string? SourcePhotoName { get; set; }
    public string? ResolvedPath { get; set; }
}

internal sealed class CadasterFilmPositionRef
{
    public long FilmPositionId { get; set; }
    public long StationId { get; set; }
    public long? ShootingSequenceId { get; set; }
    public double? PositionDistance { get; set; }
    public double? PositionTime { get; set; }
    public long? PositionFrame { get; set; }
    public double? ViewingAngle { get; set; }
    public double? ViewingDirection { get; set; }
    public double? ViewingZoom { get; set; }
    public long? ShootingFileId { get; set; }
    public string? SequenceName { get; set; }
    public double? SequenceTimeStart { get; set; }
    public double? SequenceTimeEnd { get; set; }
    public long? SequenceFrameStart { get; set; }
    public long? SequenceFrameEnd { get; set; }
    public double? SequenceValueStart { get; set; }
    public double? SequenceValueEnd { get; set; }
    public string? VideoFileName { get; set; }
    public string? MediaFileName { get; set; }
    public string? VideoFileExtension { get; set; }
    public bool? VideoFileExistsFlag { get; set; }
    public long? VideoFileSize { get; set; }
    public string? VideoPath { get; set; }
}

internal sealed class CadasterStationRow
{
    public long StationId { get; set; }
    public long? SubActivityId { get; set; }
    public double? DistanceM { get; set; }
    public string Code { get; set; } = "";
    public string? ContentCode { get; set; }
    public string? Remark { get; set; }
    public bool? RangeDamage { get; set; }
    public double? RangeDamageLength { get; set; }
    public long? ObservationType { get; set; }
    public long? ObservationClass { get; set; }
    public string? ConnectionPointName { get; set; }
    public double? Num1 { get; set; }
    public double? Num2 { get; set; }
    public double? Num3 { get; set; }
    public string? Str1 { get; set; }
    public string? Str2 { get; set; }
    public string? Str3 { get; set; }
}
