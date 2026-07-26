using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.QualityGate;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Infrastructure.Telemetry;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AuswertungPro.Next.Infrastructure.Ai.Pipeline;

/// <summary>Zustand eines journalierten Frames im Checkpoint-Journal.</summary>
public enum CheckpointFrameKind
{
    /// <summary>Frame wurde inferiert und via TemporalFindingDeduplicator.Update(...) uebernommen.</summary>
    Update = 0,
    /// <summary>Frame wurde normal ohne Befund uebersprungen (AdvanceAll).</summary>
    Advance = 1,
    /// <summary>Transport-, Modell- oder Verarbeitungsfehler: Frame muss erneut inferiert werden.</summary>
    RetryRequired = 2
}

/// <summary>
/// Append-only Checkpoint-Journal der Multi-Model-Videoanalyse (JSONL pro Video,
/// neben der Pipeline-Trace-Datei). Ermoeglicht die Fortsetzung eines abgebrochenen
/// Laufs: Bereits journalierte Frames werden beim erneuten Lauf nicht erneut inferiert.
/// Journal-Fehler werden nur geloggt und duerfen die Analyse nie abbrechen.
///
/// Integritaetsregeln (Programmintegritaet vor Bequemlichkeit):
/// - Jeder bearbeitete Frame schreibt genau einen Zustand (update/advance/retry_required).
/// - Ein Resume uebernimmt nur einen lueckenlosen, gueltigen Anfang ab Frame 1.
///   retry_required beendet den verwendbaren Bereich — ab dort wird neu inferiert,
///   spaetere Zeilen werden nicht uebersprungen, sondern abgeschnitten.
/// - Fehlende/doppelte/ruecklaufende Frame-Nummern, unbekannte Zeilentypen oder eine
///   beschaedigte mittlere Zeile verwerfen das Resume vollstaendig (frischer Start +
///   sichtbare Logwarnung). Nur eine unvollstaendige letzte Zeile wird sicher gekuerzt.
/// </summary>
public interface IAnalysisCheckpointJournal
{
    /// <summary>
    /// Oeffnet das Journal des Videos. Ein gueltiges Journal OHNE completed-Marker und
    /// mit identischer Video-Identitaet (Pfad, Dateigroesse, LastWriteTimeUtc,
    /// stepSeconds) wird zum Fortsetzen geoeffnet; sonst wird frisch ueberschrieben.
    /// </summary>
    Task<AnalysisCheckpointState> OpenAsync(string videoPath, double stepSeconds, CancellationToken ct = default);

    /// <summary>Haengt einen Frame-Record an (Flush pro Append).</summary>
    Task AppendFrameAsync(AnalysisCheckpointFrame frame, CancellationToken ct = default);

    /// <summary>Schreibt den Abschluss-Record "completed" (letzte Zeile bei normalem Ende).</summary>
    Task CompleteAsync(CancellationToken ct = default);
}

/// <summary>Ein journalierter Frame-Zustand samt den das Gate ueberstehenden Findings (nur bei update).</summary>
public sealed record AnalysisCheckpointFrame(
    CheckpointFrameKind Kind,
    int FrameIndex,
    double TimeSec,
    double Meter,
    string? MeterSource,
    bool IsMeterEstimated,
    EvidenceVector? Evidence,
    IReadOnlyList<EnhancedFinding> Findings);

/// <summary>Ergebnis des Journal-Opens: zu replayende Frames (update/advance) und letzter Index.</summary>
public sealed record AnalysisCheckpointState(
    int LastFrameIndex,
    IReadOnlyList<AnalysisCheckpointFrame> Frames)
{
    public static readonly AnalysisCheckpointState Empty = new(0, Array.Empty<AnalysisCheckpointFrame>());

    public bool HasResume => Frames.Count > 0;
}

/// <summary>
/// Dateibasierte Implementierung. Ablage ueber <see cref="ITelemetryPathResolver"/>
/// (gleicher Ordner wie der Pipeline-Trace), Dateiname stabil aus dem Videopfad
/// gehasht, damit ein Folgelauf dasselbe Journal findet.
/// </summary>
public sealed class AnalysisCheckpointJournal : IAnalysisCheckpointJournal
{
    /// <summary>Dateinamen-Muster aller Checkpoint-Journale im Telemetrie-Ordner.</summary>
    public const string FilePattern = "analysis_checkpoint_*.jsonl";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly ITelemetryPathResolver _paths;
    private readonly ILogger _logger;
    private string? _path;
    private bool _active;

    public AnalysisCheckpointJournal(ILogger? logger = null)
        : this(TelemetryPathResolver.Current, logger)
    {
    }

    public AnalysisCheckpointJournal(ITelemetryPathResolver paths, ILogger? logger = null)
    {
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        _logger = logger ?? NullLogger.Instance;
    }

    public async Task<AnalysisCheckpointState> OpenAsync(
        string videoPath, double stepSeconds, CancellationToken ct = default)
    {
        _path = ResolveJournalPath(videoPath);
        if (_path is null)
            return AnalysisCheckpointState.Empty;

        var (fileSize, lastWriteUtc) = ProbeVideoIdentity(videoPath);

        try
        {
            if (File.Exists(_path))
            {
                var read = ReadJournalStrict(await File.ReadAllBytesAsync(_path, ct).ConfigureAwait(false));

                if (read.InvalidReason is not null)
                {
                    // Integritaetsverletzung: diesem Journal darf nicht vertraut werden.
                    // Frisch starten (Header ueberschreibt) und sichtbar warnen.
                    _logger.LogWarning(
                        "Checkpoint-Journal {Path} ist ungueltig ({Reason}) — Resume verworfen, Lauf startet frisch.",
                        _path, read.InvalidReason);
                }
                else if (!read.Completed
                         && read.Header is not null
                         && IdentityMatches(read.Header, videoPath, fileSize, lastWriteUtc, stepSeconds)
                         && read.PrefixFrames.Count > 0)
                {
                    // Fortsetzen: nur der lueckenlose update/advance-Anfang wird uebernommen.
                    // Ein vorhandener stale Rest (ab retry_required bzw. gekuerzter Absturz-Schweif)
                    // wird abgeschnitten, damit neue Appends sauber anschliessen.
                    TruncateTo(read.PrefixEndOffset);
                    _active = true;
                    return new AnalysisCheckpointState(
                        read.PrefixFrames[^1].FrameIndex, read.PrefixFrames);
                }
                // completed, Identitaets-Mismatch oder kein verwendbarer Frame: frisch ueberschreiben.
            }

            await WriteHeaderAsync(videoPath, fileSize, lastWriteUtc, stepSeconds, ct).ConfigureAwait(false);
            _active = true;
            return AnalysisCheckpointState.Empty;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Journal ist reine Best-Effort-Infrastruktur: ohne Journal weiterlaufen.
            _active = false;
            _logger.LogWarning(ex, "Checkpoint-Journal konnte nicht geoeffnet werden ({Path}) — Analyse laeuft ohne Journal.", _path);
            return AnalysisCheckpointState.Empty;
        }
    }

    public async Task AppendFrameAsync(AnalysisCheckpointFrame frame, CancellationToken ct = default)
    {
        if (!_active || _path is null)
            return;

        try
        {
            var line = JsonSerializer.Serialize(new JournalLine
            {
                Type = "frame",
                Kind = ToWire(frame.Kind),
                FrameIndex = frame.FrameIndex,
                TimeSec = frame.TimeSec,
                Meter = frame.Meter,
                MeterSource = frame.MeterSource,
                IsMeterEstimated = frame.IsMeterEstimated,
                Evidence = frame.Evidence,
                Findings = frame.Kind == CheckpointFrameKind.Update ? frame.Findings : null
            }, JsonOptions) + Environment.NewLine;

            await File.AppendAllTextAsync(_path, line, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _active = false;   // ab jetzt ohne Journal weiterlaufen (nie die Analyse abbrechen)
            _logger.LogWarning(ex, "Checkpoint-Journal Append fehlgeschlagen ({Path}) — Journal deaktiviert.", _path);
        }
    }

    public async Task CompleteAsync(CancellationToken ct = default)
    {
        if (!_active || _path is null)
            return;

        try
        {
            var line = JsonSerializer.Serialize(
                new JournalLine { Type = "completed", CreatedUtc = DateTime.UtcNow }, JsonOptions)
                + Environment.NewLine;
            await File.AppendAllTextAsync(_path, line, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Checkpoint-Journal Abschluss fehlgeschlagen ({Path}).", _path);
        }
        finally
        {
            _active = false;
        }
    }

    /// <summary>Prozessweite Bremse der Aufraeumregel (siehe <see cref="CleanupCompletedJournals"/>).</summary>
    private static readonly object CleanupLock = new();
    private static DateTime _lastCleanupUtc = DateTime.MinValue;

    /// <summary>Nur fuer Tests: die prozessweite Aufraeum-Bremse zuruecksetzen.</summary>
    internal static void ResetCleanupThrottle()
    {
        lock (CleanupLock)
            _lastCleanupUtc = DateTime.MinValue;
    }

    /// <summary>
    /// Sichere Begrenzung des Journal-Bestands: loescht ausschliesslich Journale, die
    /// streng lesbar sind, einen completed-Marker tragen UND aelter als maxAge sind.
    /// Laufende, unvollstaendige oder beschaedigte Journale werden NIEMALS geloescht.
    /// Liefert die Zahl der geloeschten Dateien.
    /// Kostenbremse: hoechstens ein Lauf pro Tag und Prozess (bei ~3000 Videos kein
    /// Vollscan je Videoanalyse); ein Programmstart setzt die Bremse zurueck.
    /// </summary>
    public static int CleanupCompletedJournals(
        ITelemetryPathResolver paths, TimeSpan maxAge, ILogger? logger = null)
        => CleanupCompletedJournals(paths, maxAge, TimeSpan.FromDays(1), logger);

    /// <summary>
    /// Wie <see cref="CleanupCompletedJournals(ITelemetryPathResolver, TimeSpan, ILogger?)"/>,
    /// mit explizitem Mindestintervall zwischen zwei Laeufen (fuer Tests und Sondersituationen).
    /// Das Alter einer Datei wird VOR dem vollstaendigen Einlesen geprueft — frische
    /// Dateien werden gar nicht geoeffnet.
    /// </summary>
    public static int CleanupCompletedJournals(
        ITelemetryPathResolver paths, TimeSpan maxAge, TimeSpan minInterval, ILogger? logger = null)
        => CleanupCompletedJournals(paths, maxAge, minInterval, logger, fileEnumeration: null);

    /// <summary>
    /// Interne Variante mit Dateiaufzaehlungs-Naht fuer Tests (null = Dateisystem).
    /// Jeder Fehler beim Aufloesen oder Aufzaehlen der Ablage wird nur protokolliert:
    /// die Bereinigung wird uebersprungen, die Videoanalyse laeuft IMMER weiter.
    /// </summary>
    internal static int CleanupCompletedJournals(
        ITelemetryPathResolver paths,
        TimeSpan maxAge,
        TimeSpan minInterval,
        ILogger? logger,
        Func<string, IEnumerable<string>>? fileEnumeration)
    {
        ArgumentNullException.ThrowIfNull(paths);
        lock (CleanupLock)
        {
            try
            {
                if (DateTime.UtcNow - _lastCleanupUtc < minInterval)
                    return 0;   // gebremst: Intervall seit dem letzten Lauf noch nicht abgelaufen

                // Der Resolver liefert nur Dateipfade — den Ordner ueber einen Probe-Namen ableiten.
                var probe = paths.ResolveFile("probe.tmp");
                if (probe is null)
                    return 0;
                var dir = Path.GetDirectoryName(probe);
                if (dir is null || !Directory.Exists(dir))
                    return 0;

                var enumerate = fileEnumeration ?? (d => Directory.EnumerateFiles(d, FilePattern));
                var deleted = 0;
                foreach (var file in enumerate(dir))
                {
                    try
                    {
                        // Alter VOR dem Einlesen pruefen: frische Dateien werden gar nicht geoeffnet.
                        if (DateTime.UtcNow - File.GetLastWriteTimeUtc(file) <= maxAge)
                            continue;   // frisch abgeschlossen: behalten

                        var read = ReadJournalStrict(File.ReadAllBytes(file));
                        if (read.InvalidReason is not null || !read.Completed || read.Header is null)
                            continue;   // offen, unvollstaendig oder beschaedigt: niemals anfassen

                        File.Delete(file);
                        deleted++;
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
                    {
                        logger?.LogWarning(ex, "Checkpoint-Journal Aufraeumen: {File} uebersprungen.", file);
                    }
                }

                _lastCleanupUtc = DateTime.UtcNow;
                if (deleted > 0)
                    logger?.LogInformation("Checkpoint-Journal Aufraeumen: {Count} abgeschlossene Journale entfernt.", deleted);
                return deleted;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                // Gesperrte/unlesbare Ablage: nur die Bereinigung wird uebersprungen,
                // niemals die Analyse. Die Bremse wird bei einem Fehlversuch bewusst
                // NICHT gesetzt — der naechste Lauf darf es erneut versuchen.
                logger?.LogWarning(ex, "Checkpoint-Journal Aufraeumen uebersprungen (Ablage nicht lesbar).");
                return 0;
            }
        }
    }

    private string? ResolveJournalPath(string videoPath)
    {
        // Stabiler Schluessel pro Video (Gross-/Kleinschreibung egal): Folgelaeufe
        // finden dasselbe Journal, ohne den kompletten Pfad im Dateinamen zu tragen.
        var hash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(videoPath.ToUpperInvariant())))[..16];
        return _paths.ResolveFile($"analysis_checkpoint_{hash.ToLowerInvariant()}.jsonl");
    }

    private static (long Size, DateTime? LastWriteUtc) ProbeVideoIdentity(string videoPath)
    {
        try
        {
            var info = new FileInfo(videoPath);
            // Nicht existente Datei (z. B. Test-Seam): Identitaet = Pfad + stepSeconds.
            return info.Exists ? (info.Length, info.LastWriteTimeUtc) : (0L, null);
        }
        catch (Exception)
        {
            return (0L, null);
        }
    }

    private static bool IdentityMatches(
        JournalLine header, string videoPath, long fileSize, DateTime? lastWriteUtc, double stepSeconds)
        => string.Equals(header.VideoPath, videoPath, StringComparison.OrdinalIgnoreCase)
           && header.FileSizeBytes == fileSize
           && header.LastWriteTimeUtc == lastWriteUtc
           && header.StepSeconds == stepSeconds;

    private async Task WriteHeaderAsync(
        string videoPath, long fileSize, DateTime? lastWriteUtc, double stepSeconds, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path!)!);
        var line = JsonSerializer.Serialize(new JournalLine
        {
            Type = "header",
            VideoPath = videoPath,
            FileSizeBytes = fileSize,
            LastWriteTimeUtc = lastWriteUtc,
            StepSeconds = stepSeconds,
            CreatedUtc = DateTime.UtcNow
        }, JsonOptions) + Environment.NewLine;

        await File.WriteAllTextAsync(_path!, line, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Schneidet den stale Rest hinter dem verwendbaren Prefix ab (retry-Schweif des
    /// Abbruchlaufs oder gekuerzte Absturz-Kante), damit neue Appends sauber anschliessen.
    /// </summary>
    private void TruncateTo(long prefixEndOffset)
    {
        using var fs = new FileStream(_path!, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
        if (fs.Length > prefixEndOffset)
            fs.SetLength(prefixEndOffset);
    }

    /// <summary>
    /// Strenger Journal-Reader (byte-genau, Offsets fuer das Kuerzen). Erkennt:
    /// - unvollstaendige letzte Zeile (kein abschliessender Zeilenumbruch): sicher gekuerzt.
    /// - beschaedigte mittlere Zeile, unbekannte Zeilentypen, fehlender/doppelter Header,
    ///   Frame-Zeilen nach completed: InvalidReason (Resume vollstaendig verwerfen).
    /// - fehlende/ungueltige/doppelte/ruecklaufende/lueckenhafte Frame-Nummern sowie
    ///   unendliche Zahlenwerte: InvalidReason (Resume vollstaendig verwerfen).
    /// - retry_required: beendet den verwendbaren Prefix; spaetere Zeilen werden nicht gelesen.
    /// </summary>
    private static JournalRead ReadJournalStrict(byte[] bytes)
    {
        var read = new JournalRead();
        if (bytes.Length == 0)
            return read;

        // Absturz-Kante: ein nicht mit '\n' endender Schweif ist die einzige Stelle,
        // die sicher gekuerzt werden darf. Alles andere muss streng lesbar sein.
        var end = bytes.Length;
        if (bytes[^1] != (byte)'\n')
        {
            var lastNewline = Array.LastIndexOf(bytes, (byte)'\n');
            if (lastNewline < 0)
                return read;   // nur eine halbe Zeile: nichts Verwendbares, auch nichts Beschaedigtes
            end = lastNewline + 1;
        }

        var pos = 0;
        var contentLines = 0;
        var expectedFrame = 1;
        while (pos < end)
        {
            var newline = Array.IndexOf(bytes, (byte)'\n', pos, end - pos);
            var lineEnd = newline < 0 ? end : newline;
            var line = bytes.AsSpan(pos, lineEnd - pos).TrimEnd((byte)'\r');
            pos = newline < 0 ? end : newline + 1;

            if (IsWhiteSpaceOnly(line))
                continue;

            JournalLine? rec;
            try
            {
                rec = JsonSerializer.Deserialize<JournalLine>(line, JsonOptions);
            }
            catch (JsonException)
            {
                read.InvalidReason = $"beschaedigte Zeile {contentLines + 1}";
                return read;
            }

            if (rec?.Type is null)
            {
                read.InvalidReason = $"Zeile {contentLines + 1} ohne Typ";
                return read;
            }

            if (rec.Type == "header")
            {
                if (contentLines > 0 || read.Header is not null)
                {
                    read.InvalidReason = "fehlender oder doppelter Header";
                    return read;
                }
                if (string.IsNullOrWhiteSpace(rec.VideoPath)
                    || rec.StepSeconds is not { } step || !double.IsFinite(step) || step <= 0)
                {
                    read.InvalidReason = "ungueltiger Header";
                    return read;
                }
                read.Header = rec;
            }
            else if (rec.Type == "completed")
            {
                if (read.Completed)
                {
                    read.InvalidReason = "doppelter completed-Marker";
                    return read;
                }
                read.Completed = true;
            }
            else if (rec.Type == "frame")
            {
                if (read.Header is null)
                {
                    read.InvalidReason = "Frame-Zeile vor dem Header";
                    return read;
                }
                if (read.Completed)
                {
                    read.InvalidReason = "Frame-Zeile nach completed";
                    return read;
                }
                if (rec.FrameIndex is not { } index || index <= 0)
                {
                    read.InvalidReason = "fehlende oder ungueltige Frame-Nummer";
                    return read;
                }
                if (rec.TimeSec is { } timeSec && !double.IsFinite(timeSec)
                    || rec.Meter is { } meter && !double.IsFinite(meter))
                {
                    read.InvalidReason = "unendliche Zahlenwerte";
                    return read;
                }
                var kind = FromWire(rec.Kind);
                if (kind is null)
                {
                    read.InvalidReason = $"unbekannter Frame-Zustand '{rec.Kind}'";
                    return read;
                }
                if (index != expectedFrame)
                {
                    read.InvalidReason = index < expectedFrame
                        ? $"doppelte oder ruecklaufende Frame-Nummer {index} (erwartet {expectedFrame})"
                        : $"Luecke in den Frame-Nummern (erwartet {expectedFrame}, gefunden {index})";
                    return read;
                }

                if (kind == CheckpointFrameKind.RetryRequired)
                {
                    // Ab hier muss neu inferiert werden: Prefix endet VOR diesem Frame,
                    // spaetere Zeilen werden weder gelesen noch uebersprungen.
                    read.EndedByRetry = true;
                    break;
                }

                // Pflichtfelder werden NICHT durch Standardwerte erfunden: fehlt einer
                // der Werte, ist die Zeile ungueltig und das Resume wird verworfen.
                if (rec.TimeSec is not { } timeSecValue || !double.IsFinite(timeSecValue)
                    || rec.Meter is not { } meterValue || !double.IsFinite(meterValue))
                {
                    read.InvalidReason = "fehlende oder ungueltige Zeit-/Meterwerte";
                    return read;
                }
                if (rec.IsMeterEstimated is not { } isMeterEstimated)
                {
                    read.InvalidReason = "fehlendes Schaetzflag";
                    return read;
                }
                if (kind.Value == CheckpointFrameKind.Update && rec.Findings is null)
                {
                    read.InvalidReason = "update-Frame ohne Findings-Feld";
                    return read;
                }
                if (kind.Value == CheckpointFrameKind.Update && string.IsNullOrWhiteSpace(rec.MeterSource))
                {
                    read.InvalidReason = "update-Frame ohne Meterquelle";
                    return read;
                }

                read.PrefixFrames.Add(new AnalysisCheckpointFrame(
                    kind.Value,
                    index,
                    timeSecValue,
                    meterValue,
                    rec.MeterSource,
                    isMeterEstimated,
                    rec.Evidence,
                    rec.Findings ?? (IReadOnlyList<EnhancedFinding>)Array.Empty<EnhancedFinding>()));
                read.PrefixEndOffset = pos;
                expectedFrame++;
            }
            else
            {
                read.InvalidReason = $"unbekannter Zeilentyp '{rec.Type}'";
                return read;
            }

            contentLines++;
        }

        return read;
    }

    private static bool IsWhiteSpaceOnly(ReadOnlySpan<byte> line)
    {
        foreach (var b in line)
        {
            if (b is not ((byte)' ' or (byte)'\t'))
                return false;
        }
        return true;
    }

    private static string ToWire(CheckpointFrameKind kind) => kind switch
    {
        CheckpointFrameKind.Update => "update",
        CheckpointFrameKind.Advance => "advance",
        CheckpointFrameKind.RetryRequired => "retry_required",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };

    private static CheckpointFrameKind? FromWire(string? kind) => kind switch
    {
        "update" => CheckpointFrameKind.Update,
        "advance" => CheckpointFrameKind.Advance,
        "retry_required" => CheckpointFrameKind.RetryRequired,
        _ => null
    };

    /// <summary>Strenges Lese-Ergebnis: Header, verwendbarer Prefix und Fehlergrund.</summary>
    private sealed class JournalRead
    {
        public JournalLine? Header;
        public readonly List<AnalysisCheckpointFrame> PrefixFrames = new();
        public long PrefixEndOffset;
        public bool Completed;
        public bool EndedByRetry;
        /// <summary>null = streng gueltig; sonst Grund fuer das vollstaendige Verwerfen des Resume.</summary>
        public string? InvalidReason;
    }

    /// <summary>Flache JSONL-Zeile mit Typ-Diskriminator (header/frame/completed).</summary>
    private sealed class JournalLine
    {
        public string? Type { get; set; }
        public string? Kind { get; set; }
        public string? VideoPath { get; set; }
        public long? FileSizeBytes { get; set; }
        public DateTime? LastWriteTimeUtc { get; set; }
        public double? StepSeconds { get; set; }
        public DateTime? CreatedUtc { get; set; }
        public int? FrameIndex { get; set; }
        public double? TimeSec { get; set; }
        public double? Meter { get; set; }
        public string? MeterSource { get; set; }
        public bool? IsMeterEstimated { get; set; }
        public EvidenceVector? Evidence { get; set; }
        public IReadOnlyList<EnhancedFinding>? Findings { get; set; }
    }
}
