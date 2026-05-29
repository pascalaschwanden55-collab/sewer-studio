using Microsoft.Win32;
using System.IO;
using System.Windows;

namespace AuswertungPro.Next.UI;

public sealed class DialogService : IDialogService
{
    public void Info(string message, string title = "Hinweis")
        => MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);

    public void Warn(string message, string title = "Warnung")
        => MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);

    public void Error(string message, string title = "Fehler")
        => MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);

    public bool Confirm(string message, string title = "Bestaetigung")
        => MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question)
            == MessageBoxResult.Yes;

    public DialogConfirm ConfirmCancel(string message, string title = "Bestaetigung")
        => MessageBox.Show(message, title, MessageBoxButton.YesNoCancel, MessageBoxImage.Question) switch
        {
            MessageBoxResult.Yes => DialogConfirm.Yes,
            MessageBoxResult.No => DialogConfirm.No,
            _ => DialogConfirm.Cancel
        };

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
}
