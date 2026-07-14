using System.Collections.Generic;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Infrastructure.Import;

/// <summary>
/// Erzeugt am ENDE der Bearbeitung („Protokoll neu generieren") je Haltung das programm-EIGENE Protokoll
/// (mit eingebetteten Fotos, Suffix <c>_E</c>) in den Haltungsordner und verlinkt es RELATIV als
/// <c>PDF_Eigen</c>. Das ORIGINAL-Protokoll (<c>PDF_Path</c>) bleibt unangetastet — beide sind getrennt
/// öffenbar.
///
/// Das eigene Protokoll ist immer aktuell (Haltungsnummer, DN, Befunde). Der Dateiname ist fest
/// (<c>JJJJMMTT_&lt;H&gt;_E.pdf</c>) und wird bei jeder Regenerierung überschrieben, damit stets nur die
/// aktuellste Version in der Verteilung liegt.
///
/// WICHTIG: Fotos müssen bereits projekt-relativ verteilt sein (der Exporter bettet nur existierende,
/// projekt-relative Fotos ein). Beim Ein-Knopf-Import ist das nach der Medienverteilung der Fall.
/// </summary>
public static class ProtocolRegenerationService
{
    private static readonly ProtocolRegenerationAdapter Default = new();

    public sealed record Result(int Generated, int Errors, IReadOnlyList<string> Messages);

    /// <summary>
    /// Generiert für alle Haltungen mit Protokoll das eigene <c>_E</c>-Protokoll in die Verteilung.
    /// </summary>
    public static Result RegenerateAll(Project project, string projectFolder, ICodeCatalogProvider? codeCatalog = null)
    {
        var result = Default.RegenerateAll(project, projectFolder, codeCatalog);
        return new Result(result.Generated, result.Errors, result.Messages);
    }

    /// <summary>
    /// Erzeugt das eigene <c>_E</c>-Protokoll fuer EINE Haltung in ihren Verteil-Ordner
    /// (<c>Haltungen_Verteilt\&lt;H&gt;\JJJJMMTT_&lt;H&gt;_E.pdf</c>), verlinkt es relativ als
    /// <c>PDF_Eigen</c> und gibt den absoluten Zielpfad zurueck. Fester Name -> ueberschreibt die
    /// vorherige Version (immer aktuell). Gibt <c>null</c> zurueck, wenn kein Haltungsname vorliegt.
    /// Setzt NICHT <c>project.Dirty</c> (das entscheidet der Aufrufer).
    /// </summary>
    public static string? RegenerateOne(
        Project project,
        string projectFolder,
        HaltungRecord record,
        ProtocolDocument doc,
        ICodeCatalogProvider? codeCatalog = null)
        => Default.RegenerateOne(project, projectFolder, record, doc, codeCatalog);
}
