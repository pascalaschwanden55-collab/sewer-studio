using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Export;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.DataPage;

/// <summary>
/// Fuehrt eine Schachtnummer-Aenderung samt Pfaden, Metadaten und PDF-Texten aus.
/// </summary>
internal static class SchaechteShaftRenameController
{
    internal static bool Apply(
        IShaftRenameService renameService,
        IPdfTextLayerRewriter pdfTextLayerRewrite,
        SchachtRecord record,
        string? oldValue,
        string? newValue,
        string? projectPath,
        Project? project,
        Action<string, string> showError)
    {
        ArgumentNullException.ThrowIfNull(renameService);
        ArgumentNullException.ThrowIfNull(pdfTextLayerRewrite);
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(showError);

        var oldNumber = oldValue ?? string.Empty;
        var newNumber = newValue ?? string.Empty;
        if (string.Equals(oldNumber, newNumber, StringComparison.OrdinalIgnoreCase))
            return true;

        var renameResult = renameService.Rename(record, oldNumber, newNumber, projectPath);
        if (!renameResult.Success)
        {
            showError($"Umbenennen fehlgeschlagen:\n{renameResult.ErrorMessage}", "Umbenennen");
            return false;
        }

        record.SetFieldValue("Schachtnummer", newNumber, FieldSource.Manual, userEdited: true);
        PdfCorrectionMetadata.RegisterShaftRename(project, oldNumber, newNumber);

        var pdfPaths = CollectPdfPaths(record, projectPath);
        if (pdfPaths.Count == 0)
            return true;

        var rewrite = pdfTextLayerRewrite.RewriteIdentifierInPlace(
            pdfPaths,
            oldNumber,
            newNumber);
        if (rewrite.Failed > 0)
        {
            showError(
                $"{rewrite.Failed} Protokoll-PDF(s) konnten nicht aktualisiert werden.\n" +
                "Die bisherigen PDF-Dateien wurden nicht ueberschrieben.",
                "PDF nicht aktualisiert");
        }

        return true;
    }

    internal static List<string> CollectPdfPaths(SchachtRecord record, string? projectPath)
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in new[] { FieldKeys.PdfPath, FieldKeys.PdfAll, FieldKeys.PdfEigen, FieldKeys.Link })
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
