using System;
using System.IO;

using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.Application.Dossiers;

/// <summary>
/// Plant den Ordnernamen eines Eigentuemerdossiers unterhalb von
/// "&lt;Projekt&gt;\Dossiers". Pure Logik ohne Dateisystemzugriff: die
/// Existenzpruefung kommt als Delegate, damit sie testbar bleibt.
/// Gleiches Muster wie <see cref="NewProjectFolderPlanner"/>.
/// </summary>
public static class DossierFolderPlanner
{
    /// <summary>Name des Sammelordners im Projekt.</summary>
    public const string DossierRootFolderName = "Dossiers";

    /// <summary>Name der Ablagedatei mit Gebietsangaben und allen Dossiers.</summary>
    public const string DocumentFileName = "dossiers.json";

    /// <summary>Unterordner fuer die Beilagen eines Dossiers.</summary>
    public const string AttachmentFolderName = "Beilagen";

    /// <summary>Dateiname der erzeugten Word-Datei.</summary>
    public const string WordFileName = "Eigentuemerdossier.docx";

    /// <summary>Dateiname des zusammengefuehrten Gesamt-PDF.</summary>
    public const string CombinedPdfFileName = "Eigentuemerdossier_komplett.pdf";

    /// <summary>Sammelordner "&lt;Projekt&gt;\Dossiers".</summary>
    public static string ResolveRoot(string projectRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
        return Path.Combine(projectRoot, DossierRootFolderName);
    }

    /// <summary>Pfad der Ablagedatei.</summary>
    public static string ResolveDocumentPath(string projectRoot)
        => Path.Combine(ResolveRoot(projectRoot), DocumentFileName);

    /// <summary>
    /// Vollstaendiger Ordner einer einzelnen Liegenschaft. Der gespeicherte
    /// Ordnername muss ein direktes Kind des Dossier-Sammelordners bleiben.
    /// </summary>
    public static string ResolveDossierFolder(string projectRoot, string folderName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(folderName);

        var root = Path.GetFullPath(ResolveRoot(projectRoot));
        var folder = Path.GetFullPath(Path.Combine(root, folderName));
        var parent = Path.GetDirectoryName(folder);

        if (!string.Equals(parent, root, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "Der Dossierordner muss direkt unter dem Ordner 'Dossiers' liegen.",
                nameof(folderName));
        }

        return folder;
    }

    /// <summary>
    /// Bildet aus dem Anzeigenamen einen freien Ordnernamen. Ungueltige Zeichen
    /// werden ersetzt, Kollisionen mit "-2", "-3" ... aufgeloest.
    /// </summary>
    /// <param name="displayName">Anzeigename des Dossiers.</param>
    /// <param name="folderExists">Prueft, ob ein Ordnername bereits belegt ist.</param>
    public static string PlanFolderName(string? displayName, Func<string, bool> folderExists)
    {
        ArgumentNullException.ThrowIfNull(folderExists);

        var safeName = ProjectPathResolver.SanitizePathSegment(displayName);
        var candidate = safeName;

        var counter = 2;
        while (folderExists(candidate))
        {
            candidate = $"{safeName}-{counter}";
            counter++;
        }

        return candidate;
    }

    /// <summary>
    /// Bildet aus einem Wunschdateinamen einen freien Namen im selben Ordner.
    /// Eine bereits vorhandene Datei wird nie ueberschrieben.
    /// </summary>
    public static string PlanFreeFileName(string desiredFileName, Func<string, bool> fileExists)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(desiredFileName);
        ArgumentNullException.ThrowIfNull(fileExists);

        if (!fileExists(desiredFileName))
            return desiredFileName;

        var stem = Path.GetFileNameWithoutExtension(desiredFileName);
        var extension = Path.GetExtension(desiredFileName);

        var counter = 2;
        while (true)
        {
            var candidate = $"{stem}-{counter}{extension}";
            if (!fileExists(candidate))
                return candidate;

            counter++;
        }
    }
}
