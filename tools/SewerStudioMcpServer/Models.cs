using System.Text.Json;
using System.Text.Json.Serialization;

namespace AuswertungPro.Tools.SewerStudioMcpServer;

public sealed record HaltungSummary(
    [property: JsonPropertyName("case_id")] string CaseId,
    [property: JsonPropertyName("folder_path")] string FolderPath,
    [property: JsonPropertyName("relative_path")] string RelativePath,
    [property: JsonPropertyName("has_pdf")] bool HasPdf,
    [property: JsonPropertyName("has_video")] bool HasVideo,
    [property: JsonPropertyName("frame_count")] int FrameCount);

public sealed record ProtocolEntriesResult(
    [property: JsonPropertyName("case_id")] string CaseId,
    [property: JsonPropertyName("folder_path")] string? FolderPath,
    [property: JsonPropertyName("pdf_path")] string? PdfPath,
    [property: JsonPropertyName("error")] string? Error,
    [property: JsonPropertyName("entries")] IReadOnlyList<ProtocolEntryDto> Entries);

public sealed record ProtocolEntryDto(
    [property: JsonPropertyName("index")] int Index,
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("beschreibung")] string Beschreibung,
    [property: JsonPropertyName("meter_start")] double? MeterStart,
    [property: JsonPropertyName("meter_end")] double? MeterEnd,
    [property: JsonPropertyName("is_streckenschaden")] bool IsStreckenschaden,
    [property: JsonPropertyName("mpeg")] string? Mpeg,
    [property: JsonPropertyName("zeit_seconds")] double? ZeitSeconds,
    [property: JsonPropertyName("severity")] string? Severity);

public sealed record DiagnosticReport(
    [property: JsonPropertyName("output_dir")] string OutputDir,
    [property: JsonPropertyName("entries_csv_path")] string? EntriesCsvPath,
    [property: JsonPropertyName("haltungen_json_path")] string? HaltungenJsonPath,
    [property: JsonPropertyName("has_entries_csv")] bool HasEntriesCsv,
    [property: JsonPropertyName("has_haltungen_json")] bool HasHaltungenJson,
    [property: JsonPropertyName("haltungen")] IReadOnlyList<JsonElement> Haltungen,
    [property: JsonPropertyName("entries")] IReadOnlyList<IReadOnlyDictionary<string, string>> Entries);

public sealed record TrainingSampleDto(
    [property: JsonPropertyName("sample_id")] string SampleId,
    [property: JsonPropertyName("case_id")] string CaseId,
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("beschreibung")] string Beschreibung,
    [property: JsonPropertyName("meter_start")] double MeterStart,
    [property: JsonPropertyName("meter_end")] double MeterEnd,
    [property: JsonPropertyName("is_streckenschaden")] bool IsStreckenschaden,
    [property: JsonPropertyName("time_seconds")] double TimeSeconds,
    [property: JsonPropertyName("frame_path")] string FramePath,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("match_level")] string? MatchLevel,
    [property: JsonPropertyName("ki_code")] string? KiCode,
    [property: JsonPropertyName("source_type")] string? SourceType,
    [property: JsonPropertyName("kb_index_state")] string KbIndexState);

// Ein einzelner schwacher VSA-Code aus einem Benchmark-Lauf (korrekt/gesamt + Quote).
public sealed record BenchmarkWeakCode(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("correct")] int Correct,
    [property: JsonPropertyName("total")] int Total,
    [property: JsonPropertyName("accuracy")] double Accuracy);

// Kurzinfo zu einem verfuegbaren Benchmark-Lauf fuer die Uebersicht/den Vergleich.
public sealed record BenchmarkRunInfo(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("modified_utc")] string ModifiedUtc,
    [property: JsonPropertyName("headline_accuracy")] double? HeadlineAccuracy);

// Normalisiertes Ergebnis des neuesten Benchmark-Laufs aus docs/benchmarks.
public sealed record LatestBenchmarkResult(
    [property: JsonPropertyName("benchmarks_dir")] string BenchmarksDir,
    [property: JsonPropertyName("found")] bool Found,
    [property: JsonPropertyName("file_name")] string? FileName,
    [property: JsonPropertyName("file_path")] string? FilePath,
    [property: JsonPropertyName("modified_utc")] string? ModifiedUtc,
    [property: JsonPropertyName("format")] string Format,
    [property: JsonPropertyName("frames")] int? Frames,
    [property: JsonPropertyName("exact_accuracy")] double? ExactAccuracy,
    [property: JsonPropertyName("findings_accuracy")] double? FindingsAccuracy,
    [property: JsonPropertyName("leer_accuracy")] double? LeerAccuracy,
    [property: JsonPropertyName("model_or_weights")] string? ModelOrWeights,
    [property: JsonPropertyName("eval_root")] string? EvalRoot,
    [property: JsonPropertyName("weakest_codes")] IReadOnlyList<BenchmarkWeakCode> WeakestCodes,
    [property: JsonPropertyName("raw")] JsonElement? Raw,
    [property: JsonPropertyName("available_runs")] IReadOnlyList<BenchmarkRunInfo> AvailableRuns,
    [property: JsonPropertyName("note")] string? Note);
