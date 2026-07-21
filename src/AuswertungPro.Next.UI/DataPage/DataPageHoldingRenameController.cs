using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Export;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.DataPage;

/// <summary>
/// Fuehrt eine Haltungsnamen-Aenderung samt Pfaden, Metadaten und PDF-Texten aus.
/// </summary>
internal static class DataPageHoldingRenameController
{
    internal static bool Apply(
        IHoldingRenameService renameService,
        IPdfTextLayerRewriter pdfTextLayerRewrite,
        HaltungRecord record,
        string? oldValue,
        string? newValue,
        string? projectPath,
        Project project,
        Action<string, string> showWarning,
        Action<string, string> showError)
    {
        ArgumentNullException.ThrowIfNull(renameService);
        ArgumentNullException.ThrowIfNull(pdfTextLayerRewrite);
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(showWarning);
        ArgumentNullException.ThrowIfNull(showError);

        var oldName = oldValue ?? string.Empty;
        var newName = newValue ?? string.Empty;
        if (string.Equals(oldName, newName, StringComparison.OrdinalIgnoreCase))
            return true;

        if (project.HasDuplicateHoldingName(newName, record.Id))
        {
            showWarning(
                $"Die Haltungsnummer '{newName.Trim()}' ist bereits vorhanden.",
                "Doppelte Haltungsnummer");
            return false;
        }

        var renameResult = renameService.Rename(record, oldName, newName, projectPath);
        if (!renameResult.Success)
        {
            showError($"Umbenennen fehlgeschlagen:\n{renameResult.ErrorMessage}", "Umbenennen");
            return false;
        }

        record.SetFieldValue(FieldKeys.HoldingName, newName, FieldSource.Manual, userEdited: true);
        PdfCorrectionMetadata.RegisterHoldingRename(project, oldName, newName);

        var pdfPaths = CollectPdfPaths(record, projectPath);
        if (pdfPaths.Count == 0)
            return true;

        var rewrite = pdfTextLayerRewrite.RewriteIdentifierInPlace(pdfPaths, oldName, newName);
        if (rewrite.Failed > 0)
        {
            showError(
                $"{rewrite.Failed} Protokoll-PDF(s) konnten nicht aktualisiert werden.\n" +
                "Die bisherigen PDF-Dateien wurden nicht ueberschrieben.",
                "PDF nicht aktualisiert");
        }

        return true;
    }

    private static List<string> CollectPdfPaths(HaltungRecord record, string? projectPath)
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in new[] { FieldKeys.PdfPath, FieldKeys.PdfAll })
        {
            var raw = record.GetFieldValue(field);
            if (string.IsNullOrWhiteSpace(raw))
                continue;

            foreach (var part in raw.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var resolved = ProjectPathResolver.ResolveFilePath(part.Trim(), projectPath);
                if (!string.IsNullOrWhiteSpace(resolved)
                    && resolved.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                {
                    paths.Add(resolved);
                }
            }
        }

        return paths.ToList();
    }
}
