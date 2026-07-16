using System;
using System.IO;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Infrastructure.Common;

namespace AuswertungPro.Next.UI.Settings;

public sealed record SettingsOpenFolderResult(bool Success, string? Error);

public sealed record SettingsOpenFolderRequest(
    string? Path,
    IDialogService Dialogs,
    Action<string> CreateDirectory,
    Func<string, SettingsOpenFolderResult> TryOpen);

public static class SettingsPathWorkflow
{
    private static readonly IFolderOpenService DefaultFolderOpen =
        new FolderOpenService(new SafeShellOpenService());

    internal static IFolderOpenService CompatibilityService
        => DefaultFolderOpen;

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

    public static string? SelectAbwasserkatasterXtfPath(IDialogService dialogs, string? currentPath)
        => dialogs.OpenFile(
            "Abwasserkataster-XTF waehlen",
            "XTF-Dateien (*.xtf)|*.xtf|Alle Dateien|*.*",
            InitialDirectoryFromFilePath(currentPath));

    public static string? SelectKantonUriXtfDirectory(IDialogService dialogs, string? currentPath)
        => dialogs.SelectFolder("XTF-Ordner Kanton Uri waehlen", currentPath);

    public static void OpenFolder(string? path, IDialogService dialogs)
        => OpenFolder(path, dialogs, CompatibilityService);

    internal static void OpenFolder(
        string? path,
        IDialogService dialogs,
        IFolderOpenService folderOpen)
    {
        ArgumentNullException.ThrowIfNull(dialogs);
        ArgumentNullException.ThrowIfNull(folderOpen);

        OpenFolderCore(
            path,
            dialogs,
            value =>
            {
                var result = folderOpen.EnsureAndOpen(value);
                return new SettingsOpenFolderResult(result.Success, result.Error);
            });
    }

    public static void OpenFolder(SettingsOpenFolderRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        OpenFolderCore(
            request.Path,
            request.Dialogs,
            value =>
            {
                request.CreateDirectory(value);
                return request.TryOpen(value);
            });
    }

    private static void OpenFolderCore(
        string? path,
        IDialogService dialogs,
        Func<string, SettingsOpenFolderResult> open)
    {
        ArgumentNullException.ThrowIfNull(dialogs);
        ArgumentNullException.ThrowIfNull(open);

        try
        {
            if (string.IsNullOrWhiteSpace(path))
                return;

            var result = open(path);
            if (!result.Success)
                throw new InvalidOperationException(result.Error ?? "Unbekannter Fehler");
        }
        catch (Exception ex)
        {
            dialogs.Error(
                $"Ordner konnte nicht geoeffnet werden:\n{UserError.DescribeAndReport(ex, "Ordner oeffnen")}",
                "SewerStudio");
        }
    }

    private static string? InitialDirectoryFromFilePath(string? path)
        => string.IsNullOrWhiteSpace(path) ? null : Path.GetDirectoryName(path);
}
