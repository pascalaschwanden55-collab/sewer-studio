using System;
using System.IO;
using AuswertungPro.Next.Application.Reports;

namespace AuswertungPro.Next.UI.Services;

/// <summary>
/// Liest das Logo fuer Berichte aus den Programmeinstellungen
/// (<see cref="AppSettings.BerichtsLogoPfad"/>). Der Wert wird bei jedem Zugriff neu
/// gelesen, damit eine Aenderung in den Einstellungen ohne Programmneustart beim
/// naechsten Export wirkt (gleiches Muster wie <c>AppSettingsProtocolPdfLayoutSettings</c>).
/// Ein Lesefehler oder eine fehlende Datei darf nie einen Export stoppen — dann gibt
/// es eben kein Logo.
/// </summary>
public sealed class AppSettingsBerichtsMarke : IBerichtsMarke
{
    private readonly Func<string?> _readConfiguredPath;
    private readonly string _appBaseDirectory;
    private readonly Func<string, bool> _fileExists;

    public AppSettingsBerichtsMarke(AppSettings settings)
        : this(() => settings.BerichtsLogoPfad, AppContext.BaseDirectory, File.Exists)
    {
        ArgumentNullException.ThrowIfNull(settings);
    }

    internal AppSettingsBerichtsMarke(
        Func<string?> readConfiguredPath,
        string appBaseDirectory,
        Func<string, bool> fileExists)
    {
        _readConfiguredPath = readConfiguredPath ?? throw new ArgumentNullException(nameof(readConfiguredPath));
        _appBaseDirectory = appBaseDirectory ?? throw new ArgumentNullException(nameof(appBaseDirectory));
        _fileExists = fileExists ?? throw new ArgumentNullException(nameof(fileExists));
    }

    public string? LogoPfad => SafeResolve();

    private string? SafeResolve()
    {
        try
        {
            return BerichtsLogoResolver.Resolve(_readConfiguredPath(), _appBaseDirectory, _fileExists);
        }
        catch
        {
            return null;
        }
    }
}
