using System;
using System.Windows;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using UiServiceProvider = AuswertungPro.Next.UI.ServiceProvider;

namespace AuswertungPro.Next.UI.Ai.Coding;

public sealed class CodingProtocolPreviewWorkflowService
{
    private readonly Func<int, bool> _confirmPreview;
    private readonly Func<Project?> _getCurrentProject;
    private readonly Func<string?, string?> _resolveProjectFolder;
    private readonly Action<Window?, HaltungRecord, Project, UiServiceProvider, string?, string?, Action> _showPreviewWindow;

    public CodingProtocolPreviewWorkflowService(
        Func<int, bool> confirmPreview,
        Func<Project?> getCurrentProject,
        Func<string?, string?> resolveProjectFolder,
        Action<Window?, HaltungRecord, Project, UiServiceProvider, string?, string?, Action> showPreviewWindow)
    {
        _confirmPreview = confirmPreview ?? throw new ArgumentNullException(nameof(confirmPreview));
        _getCurrentProject = getCurrentProject ?? throw new ArgumentNullException(nameof(getCurrentProject));
        _resolveProjectFolder = resolveProjectFolder ?? throw new ArgumentNullException(nameof(resolveProjectFolder));
        _showPreviewWindow = showPreviewWindow ?? throw new ArgumentNullException(nameof(showPreviewWindow));
    }

    public bool TryShow(
        Window? owner,
        HaltungRecord record,
        ProtocolDocument doc,
        UiServiceProvider serviceProvider,
        string? videoPath,
        string? lastProjectPath,
        Action markDirty)
    {
        if (!_confirmPreview(doc.Current.Entries.Count))
            return false;

        var project = _getCurrentProject();
        if (project == null)
            return false;

        var projectFolder = _resolveProjectFolder(lastProjectPath);
        _showPreviewWindow(owner, record, project, serviceProvider, videoPath, projectFolder, markDirty);
        return true;
    }
}
