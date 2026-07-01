using System;
using System.Collections.Generic;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.DataPage;

public sealed class DataPageOriginalPdfController
{
    private readonly IDialogService _dialogs;
    private readonly Func<HaltungRecord, string?> _ensureProtocolPath;
    private readonly Func<string?> _getProjectFolder;
    private readonly Func<HaltungRecord, string, List<string>> _resolveOriginalPdfPaths;
    private readonly Func<string?, (bool Success, string? Error)> _tryOpen;

    public DataPageOriginalPdfController(
        IDialogService dialogs,
        Func<HaltungRecord, string?> ensureProtocolPath,
        Func<string?> getProjectFolder,
        Func<HaltungRecord, string, List<string>> resolveOriginalPdfPaths,
        Func<string?, (bool Success, string? Error)> tryOpen)
    {
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _ensureProtocolPath = ensureProtocolPath ?? throw new ArgumentNullException(nameof(ensureProtocolPath));
        _getProjectFolder = getProjectFolder ?? throw new ArgumentNullException(nameof(getProjectFolder));
        _resolveOriginalPdfPaths = resolveOriginalPdfPaths ?? throw new ArgumentNullException(nameof(resolveOriginalPdfPaths));
        _tryOpen = tryOpen ?? throw new ArgumentNullException(nameof(tryOpen));
    }

    public void Open(HaltungRecord? record)
    {
        if (record is null)
            return;

        var path = _ensureProtocolPath(record);
        if (string.IsNullOrWhiteSpace(path))
        {
            var projectFolder = _getProjectFolder() ?? "";
            var paths = _resolveOriginalPdfPaths(record, projectFolder);
            path = paths.Count > 0 ? paths[0] : null;
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            var name = record.GetFieldValue("Haltungsname") ?? "(unbekannt)";
            _dialogs.Info(
                $"Kein PDF gefunden fuer Haltung '{name}'.\n\nPruefen Sie, ob das Protokoll-PDF in der Verteilung liegt.",
                "Haltungsprotokoll (PDF)");
            return;
        }

        var result = _tryOpen(path);
        if (!result.Success)
            _dialogs.Warn($"PDF konnte nicht geoeffnet werden:\n{result.Error}", "Fehler");
    }

    public static (bool Success, string? Error) TryShellOpen(string? path)
        => SafeShellOpen.TryOpen(path, out var error) ? (true, null) : (false, error);
}
