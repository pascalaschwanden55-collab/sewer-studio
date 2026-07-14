using AuswertungPro.Next.Application.DataPage;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.DataPage;

/// <summary>
/// Oeffnet das neueste verteilte Dichtheitspruefungsprotokoll einer Haltung
/// und zeigt verstaendliche Hinweise fuer Leer- und Fehlerfaelle.
/// </summary>
public sealed class DataPageDichtheitPdfController
{
    private readonly IDialogService _dialogs;
    private readonly IDichtheitProtocolFileLocator _files;
    private readonly Func<string?> _getProjectFolder;
    private readonly Func<string?> _getConfiguredRoot;
    private readonly Func<string?, (bool Success, string? Error)> _tryOpen;

    public DataPageDichtheitPdfController(
        IDialogService dialogs,
        IDichtheitProtocolFileLocator files,
        Func<string?> getProjectFolder,
        Func<string?> getConfiguredRoot,
        Func<string?, (bool Success, string? Error)> tryOpen)
    {
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _files = files ?? throw new ArgumentNullException(nameof(files));
        _getProjectFolder = getProjectFolder ?? throw new ArgumentNullException(nameof(getProjectFolder));
        _getConfiguredRoot = getConfiguredRoot ?? throw new ArgumentNullException(nameof(getConfiguredRoot));
        _tryOpen = tryOpen ?? throw new ArgumentNullException(nameof(tryOpen));
    }

    public void Open(HaltungRecord? record)
    {
        var pdfs = _files.FindPdfPaths(record, _getProjectFolder(), _getConfiguredRoot());
        if (pdfs.Count == 0)
        {
            var name = record?.GetFieldValue(FieldKeys.HoldingName) ?? "(unbekannt)";
            _dialogs.Info(
                $"Kein Dichtheitspruefungsprotokoll fuer Haltung '{name}' gefunden.\n" +
                "Dichtheitsprotokolle werden beim Kanalfernseh-Import automatisch verteilt (…_DP.pdf).",
                "Dichtheitspruefung");
            return;
        }

        var result = _tryOpen(pdfs[0]);
        if (!result.Success)
        {
            _dialogs.Warn(
                $"Dichtheitspruefung konnte nicht geoeffnet werden:\n{result.Error}",
                "Dichtheitspruefung");
        }
    }
}
