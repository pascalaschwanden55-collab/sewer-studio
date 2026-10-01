using Microsoft.Win32;
using System;
using System.IO;
using System.Windows;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI;

public sealed class DialogService : IDialogService
{
    public void Info(string message, string title = "Hinweis")
        => AufUiThreadOhneErgebnis(
            () => NovaDialog.ZeigeInfo(message, title),
            () => MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information));

    public void Warn(string message, string title = "Warnung")
        => AufUiThreadOhneErgebnis(
            () => NovaDialog.ZeigeWarnung(message, title),
            () => MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning));

    public void Error(string message, string title = "Fehler")
        => AufUiThreadOhneErgebnis(
            () => NovaDialog.ZeigeFehler(message, title),
            () => MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error));

    public bool Confirm(string message, string title = "Bestätigung")
        => AufUiThread(
            () => NovaDialog.ZeigeBestaetigung(message, title),
            () => MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question)
                == MessageBoxResult.Yes);

    public bool ConfirmWarn(string message, string title = "Bestätigung", bool defaultNo = true)
        => AufUiThread(
            () => NovaDialog.ZeigeWarnendeBestaetigung(message, title, defaultNo),
            () => MessageBox.Show(
                message,
                title,
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                defaultNo ? MessageBoxResult.No : MessageBoxResult.Yes)
                == MessageBoxResult.Yes);

    public DialogConfirm ConfirmCancel(string message, string title = "Bestätigung")
        => AufUiThread(
            () => NovaDialog.ZeigeDreiWegeBestaetigung(message, title),
            () => MessageBox.Show(message, title, MessageBoxButton.YesNoCancel, MessageBoxImage.Question) switch
            {
                MessageBoxResult.Yes => DialogConfirm.Yes,
                MessageBoxResult.No => DialogConfirm.No,
                _ => DialogConfirm.Cancel
            });

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
        var dlg = new OpenFolderDialog
        {
            Title = title
        };
        if (!string.IsNullOrWhiteSpace(initialPath) && Directory.Exists(initialPath))
            dlg.InitialDirectory = initialPath;
        return dlg.ShowDialog() == true && !string.IsNullOrWhiteSpace(dlg.FolderName)
            ? dlg.FolderName
            : null;
    }

    /// <summary>
    /// Fuehrt <paramref name="novaAktion"/> auf dem Dispatcher-Thread der Anwendung aus und
    /// marshallt dafuer bei Bedarf ueber <see cref="System.Windows.Threading.Dispatcher.Invoke"/> -
    /// das ist der einzige im Programm erlaubte Ort dafuer. Fehlt <see cref="Application.Current"/>
    /// oder ist dessen Dispatcher bereits heruntergefahren (Kommandozeilen-/Testumgebung), bleibt
    /// der alte MessageBox-Weg als Rueckfall.
    /// </summary>
    private static T AufUiThread<T>(Func<T> novaAktion, Func<T> messageBoxRueckfall)
    {
        var app = System.Windows.Application.Current;
        var dispatcher = app?.Dispatcher;
        if (dispatcher is null || dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
            return messageBoxRueckfall();

        return dispatcher.CheckAccess() ? novaAktion() : dispatcher.Invoke(novaAktion);
    }

    /// <summary>Nicht-wertliefernde Fassung von <see cref="AufUiThread{T}"/> fuer Info/Warn/Error.</summary>
    private static void AufUiThreadOhneErgebnis(Action novaAktion, Action messageBoxRueckfall)
        => AufUiThread<object?>(
            () => { novaAktion(); return null; },
            () => { messageBoxRueckfall(); return null; });
}
