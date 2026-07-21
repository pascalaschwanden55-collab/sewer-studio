using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Application.Reports;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Infrastructure.Import;

/// <summary>Bindet die bestehende Protokoll-Neuerzeugung an den Application-Vertrag an.</summary>
public sealed class ProtocolRegenerationAdapter : IProtocolRegenerationService, IProtocolSingleRegenerationService
{
    private readonly IProtocolPdfExporter _pdfExporter;

    public ProtocolRegenerationAdapter(IProtocolPdfExporter? pdfExporter = null)
        => _pdfExporter = pdfExporter ?? new ProtocolPdfExporter();

    public ProtocolRegenerationResult RegenerateAll(
        Project project,
        string projectFolder,
        ICodeCatalogProvider? codeCatalog = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        var messages = new List<string>();
        int generated = 0, errors = 0;

        foreach (var record in project.Data.ToList())
        {
            var haltung = record.GetFieldValue(FieldKeys.HoldingName)?.Trim();
            if (string.IsNullOrWhiteSpace(haltung) || record.Protocol is null)
                continue;

            try
            {
                RegenerateOne(project, projectFolder, record, record.Protocol, codeCatalog);
                generated++;
            }
            catch (Exception ex)
            {
                errors++;
                messages.Add($"Protokoll {haltung}: {ex.Message}");
            }
        }

        if (generated > 0)
        {
            project.ModifiedAtUtc = DateTime.UtcNow;
            project.Dirty = true;
        }

        return new ProtocolRegenerationResult(generated, errors, messages);
    }

    public string? RegenerateOne(
        Project project,
        string projectFolder,
        HaltungRecord record,
        ProtocolDocument document,
        ICodeCatalogProvider? codeCatalog = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(document);

        var haltung = record.GetFieldValue(FieldKeys.HoldingName)?.Trim();
        if (string.IsNullOrWhiteSpace(haltung))
            return null;

        var sanitizedHolding = ProjectPathResolver.SanitizePathSegment(haltung);
        var directory = ProjectStructure.HaltungVerteiltDir(projectFolder, sanitizedHolding);
        Directory.CreateDirectory(directory);

        var stamp = KanalImportDistributor.ResolveDateStamp(record);
        var logo = Path.Combine(AppContext.BaseDirectory, "Assets", "Brand", "abwasser-uri-logo.png");
        var options = new HaltungsprotokollPdfOptions
        {
            IncludePhotos = true,
            CodeCatalog = codeCatalog,
            LogoPathAbs = File.Exists(logo) ? logo : null
        };

        var pdf = _pdfExporter.BuildHaltungsprotokollPdf(
            project,
            record,
            document,
            projectFolder,
            options);
        var destination = Path.Combine(directory, $"{stamp}_{sanitizedHolding}_E.pdf");
        // Atomar schreiben: erst Temp im Zielordner (gleiches Volume -> File.Move ist atomar),
        // dann verschieben. Ein Absturz mitten im direkten Schreiben hinterliess sonst ein halbes
        // _E.pdf unter dem finalen Namen (und der naechste Lauf koennte es als "vorhanden" werten).
        var tempPath = destination + $".{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllBytes(tempPath, pdf);
            File.Move(tempPath, destination, overwrite: true);
        }
        catch
        {
            try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { /* Temp-Rest ist unkritisch */ }
            throw;
        }

        record.SetFieldValue(
            FieldKeys.PdfEigen,
            ProjectPathResolver.MakeRelative(destination, projectFolder),
            FieldSource.Legacy,
            userEdited: false);

        return destination;
    }
}
