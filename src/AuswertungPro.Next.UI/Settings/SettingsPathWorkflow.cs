using System;
using System.IO;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Settings;

public sealed record SettingsOpenFolderResult(bool Success, string? Error);

public sealed record SettingsOpenFolderRequest(
    string? Path,
    IDialogService Dialogs,
    Action<string> CreateDirectory,
    Func<string, SettingsOpenFolderResult> TryOpen);

public static class SettingsPathWorkflow
{
    public static string? SelectPdfToText(IDialogService dialogs)
        => dialogs.OpenFile(
            "pdftotext.exe waehlen",
            "pdftotext.exe|pdftotext.exe|Alle Dateien|*.*");

    public static string? SelectProjectPath(IDialogService dialogs, string? projectPath)
    {
        var currentName = string.IsNullOrWhiteSpace(projectPath)
            ? "Projekt"
            : Path.GetFileNameWithoutExtension(projectPath);

        return dialogs.SaveFile(
            "Projektpfad waehlen",
            "Projekt (*.json)|*.json",
            ".json",
            currentName);
    }

    public static string? SelectVideoFolder(IDialogService dialogs, string? currentPath)
        => dialogs.SelectFolder("Video-Ordner (Haltungen) waehlen", currentPath);

    public static string? SelectProjectsRoot(IDialogService dialogs, string? currentPath)
        => dialogs.SelectFolder("Projekte-Verzeichnis waehlen", currentPath);

    public static string? SelectKantonUriXtfDirectory(IDialogService dialogs, string? currentPath)
        => dialogs.SelectFolder("XTF-Ordner Kanton Uri waehlen", currentPath);

    public static void OpenFolder(string? path, IDialogService dialogs)
        => OpenFolder(new SettingsOpenFolderRequest(
            path,
            dialogs,
            value => Directory.CreateDirectory(value),
            TryOpenWithShell));

    public static void OpenFolder(SettingsOpenFolderRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            if (string.IsNullOrWhiteSpace(request.Path))
                return;

            request.CreateDirectory(request.Path);

            var result = request.TryOpen(request.Path);
            if (!result.Success)
                throw new InvalidOperationException(result.Error ?? "Unbekannter Fehler");
        }
        catch (Exception ex)
        {
            request.Dialogs.Error(
                $"Ordner konnte nicht geoeffnet werden:\n{ex.Message}",
                "SewerStudio");
        }
    }

    private static SettingsOpenFolderResult TryOpenWithShell(string path)
        => SafeShellOpen.TryOpen(path, out var error)
            ? new SettingsOpenFolderResult(true, null)
            : new SettingsOpenFolderResult(false, error);
}
