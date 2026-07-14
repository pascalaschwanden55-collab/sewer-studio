using System.IO;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Projects;

namespace AuswertungPro.Next.Infrastructure.Import;

/// <summary>
/// Verbindliche Projekt-Ordnerstruktur fuer den Ein-Knopf-Import.
/// Alle Ordnernamen sind hier als Konstanten definiert und duerfen von keiner
/// anderen Klasse hartcodiert werden.
/// </summary>
public static class ProjectStructure
{
    private static readonly IProjectStructureInitializer DefaultInitializer =
        new ProjectStructureInitializer();

    // --- Ordner-Konstanten (verbindlich) ---

    /// <summary>Wurzel-Ordner fuer alle importierten Rohdaten.</summary>
    public const string Importdateien = "Importdateien";

    /// <summary>Unterordner fuer SQLite-/Access-Datenbanken (unter Importdateien).</summary>
    public const string Datenbanken = "Datenbanken";

    /// <summary>Unterordner fuer XTF-Austauschformat-Dateien (unter Importdateien).</summary>
    public const string XtfDir = "XTF";

    /// <summary>Unterordner fuer PDF-Protokolldateien (unter Importdateien).</summary>
    public const string PdfDir = "PDF";

    /// <summary>Unterordner fuer TXT/CSV-Exportdateien (unter Importdateien).</summary>
    public const string TxtDir = "TXT";

    /// <summary>Ordner fuer haltungsweise verteilte Dateien.</summary>
    public const string HaltungenVerteilt = "Haltungen_Verteilt";

    /// <summary>Ordner fuer schachtweise verteilte Dateien.</summary>
    public const string SchaechteVerteilt = "Schächte_Verteilt";

    /// <summary>Ordner fuer importierte Plan-PDFs.</summary>
    public const string Plaene = "Pläne";

    /// <summary>Wurzel-Ordner fuer Foto-Sammlungen.</summary>
    public const string Fotos = "Fotos";

    /// <summary>Unterordner fuer Haltungs-Fotos (unter Fotos).</summary>
    public const string FotosHaltungen = "Haltungen";

    /// <summary>Unterordner fuer Schacht-Fotos (unter Fotos).</summary>
    public const string FotosSchaechte = "Schächte";

    /// <summary>Ordner fuer Projekt-Metadateien und Konfigurations-Snapshots.</summary>
    public const string Projektdateien = "Projektdateien";

    /// <summary>Interner Ordner fuer Import-Berichte (nicht fuer Endanwender sichtbar).</summary>
    public const string ImportReports = "__IMPORT_REPORTS";

    /// <summary>Interner Ordner fuer Wiederherstellungspunkte vor destruktiven Operationen.</summary>
    public const string RestorePoints = "__RESTORE_POINTS";

    // --- Ordner-Erzeugung ---

    /// <summary>
    /// Legt die vollstaendige Projektordner-Struktur an.
    /// Idempotent: bestehende Ordner werden nicht angefasst.
    /// </summary>
    /// <param name="projectFolder">Absoluter Pfad zum Projektstammordner.</param>
    public static void EnsureCreated(string projectFolder)
        => DefaultInitializer.EnsureCreated(projectFolder);

    // --- Pfad-Helfer ---

    /// <summary>
    /// Gibt den Haltungs-Verteilt-Unterordner fuer ein sanitisiertes Segment zurueck.
    /// Beispiel: proj\Haltungen_Verteilt\&lt;san&gt;
    /// </summary>
    /// <param name="projectFolder">Absoluter Projektstammordner.</param>
    /// <param name="san">Sanitisierter Segmentname (via SanitizePathSegment).</param>
    public static string HaltungVerteiltDir(string projectFolder, string san)
        => Path.Combine(projectFolder, HaltungenVerteilt,
            ProjectPathResolver.SanitizePathSegment(san));

    /// <summary>
    /// Gibt den Schacht-Verteilt-Unterordner fuer ein sanitisiertes Segment zurueck.
    /// Beispiel: proj\Schächte_Verteilt\&lt;san&gt;
    /// </summary>
    public static string SchachtVerteiltDir(string projectFolder, string san)
        => Path.Combine(projectFolder, SchaechteVerteilt,
            ProjectPathResolver.SanitizePathSegment(san));

    /// <summary>
    /// Gibt den zentralen Ordner fuer importierte Plan-PDFs zurueck.
    /// Beispiel: proj\Pläne
    /// </summary>
    public static string PlaeneDir(string projectFolder)
        => Path.Combine(projectFolder, Plaene);

    /// <summary>
    /// Gibt den Fotos-Haltungs-Unterordner fuer ein sanitisiertes Segment zurueck.
    /// Beispiel: proj\Fotos\Haltungen\&lt;san&gt;
    /// </summary>
    public static string FotosHaltungDir(string projectFolder, string san)
        => Path.Combine(projectFolder, Fotos, FotosHaltungen,
            ProjectPathResolver.SanitizePathSegment(san));

    /// <summary>
    /// Gibt den Fotos-Schacht-Unterordner fuer ein sanitisiertes Segment zurueck.
    /// Beispiel: proj\Fotos\Schächte\&lt;san&gt;
    /// </summary>
    public static string FotosSchachtDir(string projectFolder, string san)
        => Path.Combine(projectFolder, Fotos, FotosSchaechte,
            ProjectPathResolver.SanitizePathSegment(san));

    /// <summary>
    /// Gibt den Importdateien-Unterordner fuer einen bestimmten Subtyp zurueck.
    /// Gueltiger Wert fuer subKind: Datenbanken, XTF, PDF, TXT (Konstanten verwenden).
    /// Beispiel: proj\Importdateien\&lt;subKind&gt;
    /// </summary>
    public static string ImportdateienDir(string projectFolder, string subKind)
        => Path.Combine(projectFolder, Importdateien, subKind);
}
