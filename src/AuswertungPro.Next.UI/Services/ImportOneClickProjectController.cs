using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Services;

internal sealed record ImportOneClickProjectActions(
    Func<string?> GetProjectFolder,
    Func<Project> GetProject,
    object CollectionLock,
    Func<bool> SaveProject,
    Action<string> SetProgress,
    Action<string> AppendSummary,
    Action<string> AppendDetails);

/// <summary>Steuert den vollständigen Ein-Knopf-Import eines Kanalfernseh-Projekts.</summary>
internal sealed class ImportOneClickProjectController
{
    private readonly IDialogService _dialogs;
    private readonly Func<IOneClickProjectImportService> _createImporter;
    private readonly IOneClickImportReportWriter _reportWriter;

    public ImportOneClickProjectController(
        IDialogService dialogs,
        Func<IOneClickProjectImportService> createImporter,
        IOneClickImportReportWriter reportWriter)
    {
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _createImporter = createImporter ?? throw new ArgumentNullException(nameof(createImporter));
        _reportWriter = reportWriter ?? throw new ArgumentNullException(nameof(reportWriter));
    }

    public async Task ExecuteAsync(ImportOneClickProjectActions actions)
    {
        ArgumentNullException.ThrowIfNull(actions);

        var projectFolder = actions.GetProjectFolder();
        if (string.IsNullOrWhiteSpace(projectFolder))
        {
            _dialogs.Info(
                "Bitte zuerst ein Projekt anlegen/speichern.",
                "Import Kanalfernseh-Projekt");
            return;
        }

        var sourceFolder = _dialogs.SelectFolder(
            "Quellordner der Kanalfernsehdaten waehlen (WinCan-, IKAS- oder KINS-Projektordner)",
            null);
        if (string.IsNullOrWhiteSpace(sourceFolder))
            return;

        actions.SetProgress(
            "Kanalfernseh-Projekt importieren: erkennen → archivieren → parsen → verteilen...");
        var context = new ImportRunContext(
            CancellationToken.None,
            null,
            new ImportRunLog(),
            collectionLock: actions.CollectionLock);
        var result = await Task.Run(() =>
            _createImporter().Import(sourceFolder, projectFolder, actions.GetProject(), context));
        actions.SetProgress(string.Empty);

        if (result.Format is OneClickProjectImportFormat.Unknown or OneClickProjectImportFormat.Ambiguous)
        {
            var hint = string.Join("\n", result.Messages.Take(6));
            _dialogs.Info(
                $"Format nicht eindeutig erkannt ({result.Format}).\n{hint}\n\n"
                + "Nutze ggf. die manuellen Import-Knoepfe (WinCan/XTF/PDF/IBAK/KINS).",
                "Import Kanalfernseh-Projekt");
            return;
        }

        _ = actions.SaveProject();
        _reportWriter.TryWrite(projectFolder, result);

        var summary = $"Import abgeschlossen ({result.Format}):"
            + $"\n  {result.Found} Haltungen ({result.Created} neu, {result.Updated} aktualisiert)"
            + $"\n  {result.Errors} Fehler, {result.Conflicts} Feld-Konflikte"
            + "\n  Rohdaten archiviert, Filme/Fotos verteilt (Report in __IMPORT_REPORTS\\)";
        actions.AppendSummary("\n" + summary);
        if (result.Messages.Count > 0)
        {
            actions.AppendDetails(
                "\n\nKanalfernseh-Import:\n" + string.Join("\n", result.Messages.Take(80)));
        }

        _dialogs.Info(summary, "Import Kanalfernseh-Projekt");
    }
}
