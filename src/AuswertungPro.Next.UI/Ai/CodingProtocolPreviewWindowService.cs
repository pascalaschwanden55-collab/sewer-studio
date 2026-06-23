using System;
using System.Windows;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Views;
using UiServiceProvider = AuswertungPro.Next.UI.ServiceProvider;

namespace AuswertungPro.Next.UI.Ai;

public sealed class CodingProtocolPreviewWindowService
{
    private readonly Func<HaltungRecord, Project, UiServiceProvider, string?, string?, Action, Window> _createWindow;
    private readonly Func<Window, bool?> _showDialog;

    public CodingProtocolPreviewWindowService()
        : this(
            (record, project, serviceProvider, videoPath, projectFolder, markDirty) =>
                new ProtocolObservationsWindow(record, project, serviceProvider, videoPath, projectFolder, markDirty),
            window => window.ShowDialog())
    {
    }

    public CodingProtocolPreviewWindowService(
        Func<HaltungRecord, Project, UiServiceProvider, string?, string?, Action, Window> createWindow,
        Func<Window, bool?> showDialog)
    {
        _createWindow = createWindow ?? throw new ArgumentNullException(nameof(createWindow));
        _showDialog = showDialog ?? throw new ArgumentNullException(nameof(showDialog));
    }

    public void Show(
        Window owner,
        HaltungRecord record,
        Project project,
        UiServiceProvider serviceProvider,
        string? videoPath,
        string? projectFolder,
        Action markDirty)
    {
        var dlg = _createWindow(record, project, serviceProvider, videoPath, projectFolder, markDirty);
        dlg.Owner = owner;
        _showDialog(dlg);
    }
}
