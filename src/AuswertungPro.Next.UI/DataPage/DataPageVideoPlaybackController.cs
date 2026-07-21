using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.DataPage;

public sealed record DataPageVideoPlaybackRequest(
    string Path,
    PlayerWindowOptions Options,
    PlayerDamageOverlayData? DamageOverlay,
    HaltungRecord Record);

public sealed class DataPageVideoPlaybackController
{
    private readonly IDialogService _dialogs;
    private readonly Func<HaltungRecord, string?> _ensureVideoPath;
    private readonly Func<PlayerWindowOptions> _getOptions;
    private readonly Func<HaltungRecord, PlayerDamageOverlayData?> _buildDamageOverlay;
    private readonly Action<DataPageVideoPlaybackRequest> _showPlayer;
    private readonly Func<Exception, string, string?> _writeStartErrorLog;

    public DataPageVideoPlaybackController(
        IDialogService dialogs,
        Func<HaltungRecord, string?> ensureVideoPath,
        Func<PlayerWindowOptions> getOptions,
        Func<HaltungRecord, PlayerDamageOverlayData?> buildDamageOverlay,
        Action<DataPageVideoPlaybackRequest> showPlayer,
        Func<Exception, string, string?> writeStartErrorLog)
    {
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _ensureVideoPath = ensureVideoPath ?? throw new ArgumentNullException(nameof(ensureVideoPath));
        _getOptions = getOptions ?? throw new ArgumentNullException(nameof(getOptions));
        _buildDamageOverlay = buildDamageOverlay ?? throw new ArgumentNullException(nameof(buildDamageOverlay));
        _showPlayer = showPlayer ?? throw new ArgumentNullException(nameof(showPlayer));
        _writeStartErrorLog = writeStartErrorLog ?? throw new ArgumentNullException(nameof(writeStartErrorLog));
    }

    public void Play(HaltungRecord? record)
    {
        if (record is null)
            return;

        PlayResolved(record, _ensureVideoPath(record));
    }

    internal void PlayCounterInspection(
        HaltungRecord? record,
        Func<string?, string?> resolveExistingPath)
    {
        if (record is null)
            return;

        ArgumentNullException.ThrowIfNull(resolveExistingPath);

        var path = resolveExistingPath(record.GetFieldValue("Link_G"));
        if (string.IsNullOrWhiteSpace(path))
        {
            _dialogs.Info(
                "Für diese Haltung ist keine Gegeninspektion vorhanden.",
                "Gegeninspektion");
            return;
        }

        PlayResolved(record, path);
    }

    /// <summary>
    /// Spielt einen BEREITS aufgelösten Videopfad ab (z.B. Gegeninspektions-Video aus Link_G).
    /// </summary>
    public void PlayResolved(HaltungRecord? record, string? path)
    {
        if (record is null || string.IsNullOrWhiteSpace(path))
            return;

        try
        {
            var options = _getOptions();
            var damageOverlay = _buildDamageOverlay(record);
            _showPlayer(new DataPageVideoPlaybackRequest(path, options, damageOverlay, record));
        }
        catch (Exception ex)
        {
            var logPath = _writeStartErrorLog(ex, path);
            var nativeHint = ex.Message.Contains("native side", StringComparison.OrdinalIgnoreCase)
                ? "\n\nHinweis: Bitte pruefen, ob 'VideoLAN.LibVLC.Windows' fuer dieses Projekt/Plattform installiert ist."
                : string.Empty;
            var userMessage = UserError.Describe(ex);
            var msg = logPath is null
                ? $"Video konnte nicht gestartet werden:\n{userMessage}{nativeHint}\n\nTechnische Details konnten nicht gespeichert werden."
                : $"Video konnte nicht gestartet werden:\n{userMessage}{nativeHint}\n\nDetails gespeichert in:\n{logPath}";
            _dialogs.Error(msg, "Video");
        }
    }
}
