using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.DataPage;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.DataPage;

internal sealed class SchaechteFileActionController
{
    private readonly ISchachtFileTargetResolver _fileTargets;
    private readonly ISafeShellOpenService _shellOpen;
    private readonly IExplorerRevealService _explorerReveal;
    private readonly IDialogService _dialogs;

    internal SchaechteFileActionController(
        ISchachtFileTargetResolver fileTargets,
        ISafeShellOpenService shellOpen,
        IExplorerRevealService explorerReveal,
        IDialogService dialogs)
    {
        _fileTargets = fileTargets ?? throw new ArgumentNullException(nameof(fileTargets));
        _shellOpen = shellOpen ?? throw new ArgumentNullException(nameof(shellOpen));
        _explorerReveal = explorerReveal ?? throw new ArgumentNullException(nameof(explorerReveal));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
    }

    internal void OpenProtocol(SchachtRecord? record, string? projectFilePath)
    {
        if (record is null)
        {
            _dialogs.Info(
                "Keine Zeile ausgewählt. Bitte direkt auf eine Zeile rechtsklicken.",
                "Protokoll");
            return;
        }

        var pdfPath = _fileTargets.ResolvePdfPath(record, projectFilePath);
        if (string.IsNullOrWhiteSpace(pdfPath))
        {
            var schacht = SchaechteColumnPolicy.GetSchachtNumber(record);
            _dialogs.Info(
                string.IsNullOrWhiteSpace(schacht)
                    ? "Kein Schachtprotokoll-PDF verknüpft."
                    : $"Kein Schachtprotokoll-PDF verknüpft für Schacht {schacht}.",
                "Protokoll");
            return;
        }

        if (!_shellOpen.TryOpen(pdfPath, out var error))
            _dialogs.Error($"PDF konnte nicht geöffnet werden:\n{error}", "Protokoll");
    }

    internal void RevealContainingFolder(SchachtRecord? record, string? projectFilePath)
    {
        if (record is null)
        {
            _dialogs.Info(
                "Keine Zeile ausgewählt. Bitte direkt auf eine Zeile rechtsklicken.",
                "Ordner");
            return;
        }

        var target = _fileTargets.ResolveExplorerTarget(record, projectFilePath);
        if (string.IsNullOrWhiteSpace(target))
        {
            var schacht = SchaechteColumnPolicy.GetSchachtNumber(record);
            _dialogs.Info(
                string.IsNullOrWhiteSpace(schacht)
                    ? "Kein Datei- oder Ordnerpfad verknüpft."
                    : $"Kein Datei- oder Ordnerpfad verknüpft für Schacht {schacht}.",
                "Ordner");
            return;
        }

        if (!_explorerReveal.TryReveal(target, out var error))
            _dialogs.Error($"Ordner konnte nicht geöffnet werden:\n{error}", "Ordner");
    }
}
