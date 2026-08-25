using System;
using System.IO;

using AuswertungPro.Next.Application.Dossiers;

namespace AuswertungPro.Next.UI.Services;

/// <summary>
/// Isoliert alle Planbearbeitungen eines Vorschaufensters.
///
/// Import, Drehen und Zuschneiden schreiben ausschliesslich in
/// <see cref="WorkFolder"/>. Erst beim Uebernehmen wird die zuletzt gewaehlte
/// Datei sicher in den Dossierordner kopiert. Verwerfen loescht nur den
/// eindeutigen Arbeitsordner dieser Sitzung.
/// </summary>
internal sealed class DossierPlanWorkSession : IDisposable
{
    private readonly string _workFolder;
    private bool _disposed;

    public DossierPlanWorkSession()
        : this(Path.Combine(
            Path.GetTempPath(),
            "SewerStudio",
            "DossierPlanPreview"))
    {
    }

    /// <summary>Eigener Temporaer-Stamm fuer fokussierte Tests.</summary>
    internal DossierPlanWorkSession(string temporaryRoot)
    {
        if (string.IsNullOrWhiteSpace(temporaryRoot))
            throw new ArgumentException("Der temporaere Stammordner fehlt.", nameof(temporaryRoot));

        var root = Path.GetFullPath(temporaryRoot);
        Directory.CreateDirectory(root);

        _workFolder = Path.Combine(root, "session-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_workFolder);
    }

    public string WorkFolder => _workFolder;

    /// <summary>
    /// Veroeffentlicht eine Arbeitsdatei unter einem freien PNG-Namen.
    /// Ein unveraenderter Plan ausserhalb des Arbeitsordners wird unveraendert
    /// zurueckgegeben. Vorhandene Zieldateien werden nie ueberschrieben.
    /// </summary>
    public DossierPlanPublicationResult Publish(
        IDossierPlanPublicationService publications,
        string projectRoot,
        string? imagePath,
        string targetFolder)
    {
        ArgumentNullException.ThrowIfNull(publications);

        if (_disposed)
        {
            return DossierPlanPublicationResult.Failed(
                "Die Planbearbeitung ist bereits geschlossen.");
        }

        if (string.IsNullOrWhiteSpace(imagePath))
            return DossierPlanPublicationResult.Existing(string.Empty);

        string sourcePath;
        try
        {
            sourcePath = Path.GetFullPath(imagePath);
        }
        catch (Exception ex)
        {
            return DossierPlanPublicationResult.Failed(
                "Der Planpfad ist ungueltig: " + ex.Message);
        }

        // Ein bereits gespeicherter, nicht bearbeiteter Plan gehoert nicht der
        // Sitzung. Er darf weder kopiert noch beim Schliessen geloescht werden.
        if (!IsBelow(sourcePath, _workFolder))
            return DossierPlanPublicationResult.Existing(imagePath);

        if (!File.Exists(sourcePath))
        {
            return DossierPlanPublicationResult.Failed(
                "Die bearbeitete Plandatei wurde nicht gefunden.");
        }

        if (string.IsNullOrWhiteSpace(targetFolder))
            return DossierPlanPublicationResult.Failed("Es ist kein Zielordner bekannt.");

        try
        {
            var fullTargetFolder = Path.GetFullPath(targetFolder);

            // Sonst wuerde Dispose die gerade veroeffentlichte Datei wieder
            // entfernen. Im echten Ablauf sind beide Ordner immer getrennt.
            if (IsSameOrBelow(fullTargetFolder, _workFolder))
            {
                return DossierPlanPublicationResult.Failed(
                    "Der Dossierordner darf nicht im temporaeren Arbeitsordner liegen.");
            }

            return publications.Publish(projectRoot, sourcePath, fullTargetFolder);
        }
        catch (Exception ex)
        {
            return DossierPlanPublicationResult.Failed(
                "Der bearbeitete Plan konnte nicht uebernommen werden: " + ex.Message);
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        try
        {
            if (Directory.Exists(_workFolder))
                Directory.Delete(_workFolder, recursive: true);
        }
        catch
        {
            // Das Schliessen des Fensters darf wegen einer blockierten
            // Temporaerdatei nicht fehlschlagen. Fremde Ordner werden nie
            // angefasst; beim naechsten Temporaer-Aufraeumen kann sie weg.
        }
    }

    private static bool IsSameOrBelow(string path, string folder)
        => string.Equals(
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)),
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder)),
                StringComparison.OrdinalIgnoreCase)
            || IsBelow(path, folder);

    private static bool IsBelow(string path, string folder)
    {
        var relative = Path.GetRelativePath(
            Path.GetFullPath(folder),
            Path.GetFullPath(path));

        return !string.Equals(relative, ".", StringComparison.Ordinal)
            && !Path.IsPathRooted(relative)
            && !string.Equals(relative, "..", StringComparison.Ordinal)
            && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            && !relative.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal);
    }
}
