using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Models.Dossiers;

namespace AuswertungPro.Next.Application.Dossiers;

/// <summary>
/// Liest und schreibt "&lt;Projekt&gt;\Dossiers\dossiers.json".
/// </summary>
public interface IDossierStore
{
    /// <summary>
    /// Laedt die Dossiers eines Projekts. Eine fehlende Datei ergibt ein leeres
    /// Dokument. Eine vorhandene, aber unlesbare Datei wird NICHT als leer
    /// behandelt, sondern ueber das Backup wiederhergestellt; scheitert auch
    /// das, wirft die Methode. Bei einer lesbaren Datei werden fehlende, bereits
    /// gespeicherte Liegenschaftsordner nachgezogen, ohne die JSON zu veraendern.
    /// </summary>
    Task<DossierDocument> LoadAsync(string projectRoot, CancellationToken ct = default);

    /// <summary>
    /// Kompatibler projektbezogener Ladeweg. Fehlende Dossierordner werden
    /// nachgezogen; Haltungs- und Schachtlisten entstehen nur noch ueber die
    /// ausdruecklichen Erstellen-Aktionen im Dossier-Cockpit.
    /// </summary>
    Task<DossierDocument> LoadAsync(
        string projectRoot,
        Project project,
        CancellationToken ct = default)
        => LoadAsync(projectRoot, ct);

    /// <summary>
    /// Speichert atomar mit Backup. Bei einer neuen Liegenschaft legt der
    /// produktive Speicher auch das feste Zustandsklassenblatt in ihren Ordner.
    /// </summary>
    Task SaveAsync(string projectRoot, DossierDocument document, CancellationToken ct = default);

    /// <summary>
    /// Kompatibler projektbezogener Speicherweg. Dynamische Bauteillisten werden
    /// beim Speichern bewusst nicht automatisch erzeugt.
    /// </summary>
    Task SaveAsync(
        string projectRoot,
        DossierDocument document,
        Project project,
        CancellationToken ct = default)
        => SaveAsync(projectRoot, document, ct);
}

/// <summary>Ergebnis einer Word-Erzeugung.</summary>
public sealed record DossierWordExportResult(
    bool Success,
    string? FilePath,
    string Message);

/// <summary>
/// Erzeugt die Word-Datei eines Dossiers aus der Vorlage
/// "Export_Vorlage\Eigentuemerdossier.docx".
/// </summary>
public interface IDossierWordExportService
{
    Task<DossierWordExportResult> ExportAsync(
        DossierExportRequest request,
        CancellationToken ct = default);
}

/// <summary>Alles, was fuer eine Dossier-Ausgabe gebraucht wird.</summary>
public sealed record DossierExportRequest(
    Project Project,
    string ProjectRoot,
    DossierAreaSettings Area,
    DossierDefinition Dossier,
    DossierSnapshot Snapshot,
    string TargetFolder);

/// <summary>Eine gesammelte Beilage.</summary>
public sealed record DossierAttachment(
    string FileName,
    string SourcePath,
    DossierAttachmentKind Kind,
    string HoldingName);

/// <summary>Herkunft einer Beilage.</summary>
public enum DossierAttachmentKind
{
    /// <summary>Unveraendertes Original-PDF des Kanalunternehmers.</summary>
    OriginalProtocol = 0,

    /// <summary>Von SewerStudio erzeugtes Protokoll, weil kein Original vorlag.</summary>
    GeneratedProtocol = 1,

    /// <summary>Weder Original noch Rueckfall moeglich.</summary>
    Missing = 2
}

/// <summary>Ergebnis des Beilagen-Sammelns.</summary>
public sealed record DossierAttachmentResult(
    IReadOnlyList<DossierAttachment> Attachments,
    IReadOnlyList<string> Warnings)
{
    public int MissingCount
    {
        get
        {
            var count = 0;
            foreach (var attachment in Attachments)
            {
                if (attachment.Kind == DossierAttachmentKind.Missing)
                    count++;
            }

            return count;
        }
    }
}

/// <summary>
/// Sammelt die TV-Protokolle der Dossier-Haltungen in den Beilagen-Ordner:
/// zuerst das importierte Original, sonst ein selbst erzeugtes Protokoll.
/// </summary>
public interface IDossierAttachmentService
{
    Task<DossierAttachmentResult> CollectAsync(
        DossierExportRequest request,
        CancellationToken ct = default);
}

/// <summary>Ergebnis der PDF-Zusammenfuehrung.</summary>
public sealed record DossierPdfAssemblyResult(
    bool Success,
    string? FilePath,
    string Message);

/// <summary>
/// Fuehrt die Word-Datei und die Beilagen zu einem Gesamt-PDF zusammen.
/// </summary>
public interface IDossierPdfAssemblyService
{
    /// <param name="waehleSeiten">
    /// Wird zwischen „zusammengefuehrt" und „geschrieben" gefragt und bekommt
    /// das fertige PDF. Zurueck kommen die Seitennummern (1-basiert), die NICHT
    /// in die Datei sollen — oder <c>null</c> fuer Abbruch. Ohne Rueckfrage
    /// wird alles geschrieben. Die automatisch erzeugte Seite des
    /// Erklaeranhangs ist ein Pflichtblatt und wird nie entfernt.
    /// </param>
    Task<DossierPdfAssemblyResult> AssembleAsync(
        string dossierFolder,
        Func<byte[], CancellationToken, Task<IReadOnlySet<int>?>>? waehleSeiten = null,
        CancellationToken ct = default);
}

/// <summary>
/// Erzeugt das feste Erklaerblatt zu Zustandsklassen und zeitlicher Orientierung.
/// Dasselbe PDF wird fuer Vorschau, Gesamt-PDF, Muster und die Kopie im
/// Liegenschaftsordner verwendet.
/// </summary>
public interface IDossierConditionClassPdfService
{
    byte[] CreatePdf();
}

/// <summary>Erzeugt die datenbasierte Haltungsliste eines Eigentuemerdossiers.</summary>
public interface IDossierHoldingListPdfService
{
    byte[] CreatePdf(DossierHoldingListPdfModel model);
}
