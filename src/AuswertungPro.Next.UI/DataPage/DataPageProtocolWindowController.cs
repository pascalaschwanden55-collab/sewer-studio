using AuswertungPro.Next.Domain.Models;
using System.IO;

namespace AuswertungPro.Next.UI.DataPage;

public sealed record DataPageProtocolWindowRequest(
    HaltungRecord Record,
    Project Project,
    string? ResolvedVideoPath,
    string? ProjectFolder,
    Action MarkDirty);

public sealed class DataPageProtocolWindowController
{
    private readonly Func<Project> _getProject;
    private readonly Func<string?> _getLastProjectPath;
    private readonly Func<string?, string?> _resolveExistingPath;
    private readonly Action<DataPageProtocolWindowRequest> _showProtocolWindow;
    private readonly Action _markDirty;
    private readonly Action<HaltungRecord> _syncObservationsToHoldingFields;
    private readonly Action<HaltungRecord> _refreshSelectedProtocolEntriesIfSelected;

    public DataPageProtocolWindowController(
        Func<Project> getProject,
        Func<string?> getLastProjectPath,
        Func<string?, string?> resolveExistingPath,
        Action<DataPageProtocolWindowRequest> showProtocolWindow,
        Action markDirty,
        Action<HaltungRecord> syncObservationsToHoldingFields,
        Action<HaltungRecord> refreshSelectedProtocolEntriesIfSelected)
    {
        _getProject = getProject ?? throw new ArgumentNullException(nameof(getProject));
        _getLastProjectPath = getLastProjectPath ?? throw new ArgumentNullException(nameof(getLastProjectPath));
        _resolveExistingPath = resolveExistingPath ?? throw new ArgumentNullException(nameof(resolveExistingPath));
        _showProtocolWindow = showProtocolWindow ?? throw new ArgumentNullException(nameof(showProtocolWindow));
        _markDirty = markDirty ?? throw new ArgumentNullException(nameof(markDirty));
        _syncObservationsToHoldingFields = syncObservationsToHoldingFields ?? throw new ArgumentNullException(nameof(syncObservationsToHoldingFields));
        _refreshSelectedProtocolEntriesIfSelected = refreshSelectedProtocolEntriesIfSelected ?? throw new ArgumentNullException(nameof(refreshSelectedProtocolEntriesIfSelected));
    }

    public void Open(HaltungRecord? record)
    {
        if (record is null)
            return;

        var projectPath = _getLastProjectPath();
        var projectFolder = string.IsNullOrWhiteSpace(projectPath)
            ? null
            : (AuswertungPro.Next.Application.Common.ProjectFileLocator.ProjectRootFromFile(projectPath)
               ?? Path.GetDirectoryName(projectPath));
        var resolvedVideoPath = _resolveExistingPath(record.GetFieldValue("Link"));

        _showProtocolWindow(new DataPageProtocolWindowRequest(
            record,
            _getProject(),
            resolvedVideoPath,
            projectFolder,
            _markDirty));

        _syncObservationsToHoldingFields(record);
        _refreshSelectedProtocolEntriesIfSelected(record);
    }
}
