using System;
using System.Windows;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai;

public sealed class CodingPhotoViewerWorkflowService
{
    private readonly Func<string?, string> _resolveProjectFolder;
    private readonly Action<Window, CodingEvent, string> _showViewer;

    public CodingPhotoViewerWorkflowService(
        Func<string?, string> resolveProjectFolder,
        Action<Window, CodingEvent, string> showViewer)
    {
        _resolveProjectFolder = resolveProjectFolder ?? throw new ArgumentNullException(nameof(resolveProjectFolder));
        _showViewer = showViewer ?? throw new ArgumentNullException(nameof(showViewer));
    }

    public void Show(Window owner, CodingEvent codingEvent, string? lastProjectPath)
    {
        var projectFolder = _resolveProjectFolder(lastProjectPath);
        _showViewer(owner, codingEvent, projectFolder);
    }
}
