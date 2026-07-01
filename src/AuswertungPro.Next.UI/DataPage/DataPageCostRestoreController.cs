using System;
using System.IO;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.DataPage;

public sealed class DataPageCostRestoreController
{
    private readonly IDialogService _dialogs;
    private readonly Func<HaltungRecord?> _getSelected;
    private readonly Func<string?> _getProjectPath;
    private readonly Func<string, ProjectCostStore> _loadStore;
    private readonly Func<string, string> _getStorePath;
    private readonly Action<HaltungRecord, HoldingCost> _applyCosts;
    private readonly Action<string> _setStatus;

    public DataPageCostRestoreController(
        IDialogService dialogs,
        Func<HaltungRecord?> getSelected,
        Func<string?> getProjectPath,
        Func<string, ProjectCostStore> loadStore,
        Func<string, string> getStorePath,
        Action<HaltungRecord, HoldingCost> applyCosts,
        Action<string> setStatus)
    {
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _getSelected = getSelected ?? throw new ArgumentNullException(nameof(getSelected));
        _getProjectPath = getProjectPath ?? throw new ArgumentNullException(nameof(getProjectPath));
        _loadStore = loadStore ?? throw new ArgumentNullException(nameof(loadStore));
        _getStorePath = getStorePath ?? throw new ArgumentNullException(nameof(getStorePath));
        _applyCosts = applyCosts ?? throw new ArgumentNullException(nameof(applyCosts));
        _setStatus = setStatus ?? throw new ArgumentNullException(nameof(setStatus));
    }

    public void Restore(HaltungRecord? record)
    {
        record ??= _getSelected();
        if (record is null)
            return;

        var holding = (record.GetFieldValue("Haltungsname") ?? "").Trim();
        if (string.IsNullOrWhiteSpace(holding))
        {
            _dialogs.Warn("Haltungsname fehlt in der Zeile.", "Kosten/Massnahmen");
            return;
        }

        var projectPath = _getProjectPath();
        if (string.IsNullOrWhiteSpace(projectPath))
        {
            _dialogs.Info("Projekt bitte zuerst speichern/oeffnen, um Kosten wiederherzustellen.", "Kosten/Massnahmen");
            return;
        }

        var store = _loadStore(projectPath);
        if (!store.ByHolding.TryGetValue(holding, out var cost))
        {
            var dir = Path.GetDirectoryName(projectPath);
            var storePath = string.IsNullOrWhiteSpace(dir) ? "" : _getStorePath(dir);
            _dialogs.Info($"Keine gespeicherten Kosten/Massnahmen gefunden fuer:\n{holding}\n\nDatei:\n{storePath}",
                "Kosten/Massnahmen");
            return;
        }

        _applyCosts(record, cost);
        _setStatus($"Kosten/Maßnahmen wiederhergestellt: {holding}");
    }
}
