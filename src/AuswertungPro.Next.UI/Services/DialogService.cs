using Microsoft.Win32;
using System;
using System.IO;
using System.Windows;

namespace AuswertungPro.Next.UI;

public sealed class DialogService : IDialogService
{
    public string? OpenFile(string title, string filter, string? initialDirectory = null)
    {
        var dlg = new OpenFileDialog { Title = title, Filter = filter };
        if (!string.IsNullOrWhiteSpace(initialDirectory) && Directory.Exists(initialDirectory))
            dlg.InitialDirectory = initialDirectory;
        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }

    public string[] OpenFiles(string title, string filter)
    {
        var dlg = new OpenFileDialog { Title = title, Filter = filter, Multiselect = true };
        return dlg.ShowDialog() == true ? dlg.FileNames : Array.Empty<string>();
    }

    public string? SaveFile(string title, string filter, string? defaultExt = null, string? defaultFileName = null)
    {
        var dlg = new SaveFileDialog
        {
            Title = title,
            Filter = filter,
            DefaultExt = defaultExt ?? "",
            FileName = string.IsNullOrWhiteSpace(defaultFileName) ? "" : defaultFileName
        };
        if (!string.IsNullOrWhiteSpace(defaultExt)) dlg.AddExtension = true;
        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }

    public string? SelectFolder(string title, string? initialPath = null)
    {
        var dlg = new OpenFileDialog
        {
            Title = title,
            CheckFileExists = false,
            CheckPathExists = true,
            ValidateNames = false,
            FileName = "Ordner auswaehlen"
        };
        if (!string.IsNullOrWhiteSpace(initialPath) && Directory.Exists(initialPath))
            dlg.InitialDirectory = initialPath;
        if (dlg.ShowDialog() != true)
            return null;
        var folder = Path.GetDirectoryName(dlg.FileName);
        return string.IsNullOrWhiteSpace(folder) ? null : folder;
    }

    // ── Phase 4.1: Window-Show ───────────────────────────────────────────

    /// <summary>
    /// Setzt das Owner-Property auf das aktuelle MainWindow (sofern vorhanden),
    /// damit modale Dialoge ueber dem Hauptfenster zentriert erscheinen und nicht
    /// unter ihm verschwinden.
    /// </summary>
    private static void TryAttachOwner(Window window)
    {
        try
        {
            var owner = System.Windows.Application.Current?.MainWindow;
            if (owner is not null && !ReferenceEquals(owner, window))
                window.Owner = owner;
        }
        catch
        {
            // best effort — Owner-Verkabelung darf den Dialog-Aufruf nicht crashen
        }
    }

    public bool? ShowDialog(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        TryAttachOwner(window);
        return window.ShowDialog();
    }

    public bool? ShowDialog(Func<Window> windowFactory)
    {
        ArgumentNullException.ThrowIfNull(windowFactory);
        var window = windowFactory();
        return ShowDialog(window);
    }

    public void Show(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        TryAttachOwner(window);
        window.Show();
    }

    public MessageBoxResult ShowMessage(
        string text,
        string title,
        MessageBoxButton buttons = MessageBoxButton.OK,
        MessageBoxImage image = MessageBoxImage.Information)
    {
        var owner = System.Windows.Application.Current?.MainWindow;
        return owner is not null
            ? MessageBox.Show(owner, text, title, buttons, image)
            : MessageBox.Show(text, title, buttons, image);
    }
}
