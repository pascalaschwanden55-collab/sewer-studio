using System;
using System.Windows;

namespace AuswertungPro.Next.UI;

/// <summary>
/// Phase 4.1: Erweiterung um Window-Show- und MessageBox-Wrapper, damit
/// ViewModels keine Window-Klassen mehr direkt instanziieren muessen.
/// </summary>
public interface IDialogService
{
    // ── File-Dialoge (bestand) ───────────────────────────────────────────
    string? OpenFile(string title, string filter, string? initialDirectory = null);
    string? SaveFile(string title, string filter, string? defaultExt = null, string? defaultFileName = null);
    string[] OpenFiles(string title, string filter);
    string? SelectFolder(string title, string? initialPath = null);

    // ── Phase 4.1: Window-Show ───────────────────────────────────────────

    /// <summary>Zeigt ein modales Window, das vom Aufrufer konstruiert wurde.</summary>
    bool? ShowDialog(Window window);

    /// <summary>Zeigt ein modales Window via Factory (z.B. fuer Konstruktor-Argumente).</summary>
    bool? ShowDialog(Func<Window> windowFactory);

    /// <summary>Zeigt ein nicht-modales Window an.</summary>
    void Show(Window window);

    /// <summary>MessageBox-Wrapper, damit VM-Code keinen direkten MessageBox.Show-Aufruf braucht.</summary>
    MessageBoxResult ShowMessage(
        string text,
        string title,
        MessageBoxButton buttons = MessageBoxButton.OK,
        MessageBoxImage image = MessageBoxImage.Information,
        MessageBoxResult defaultResult = MessageBoxResult.None);
}
