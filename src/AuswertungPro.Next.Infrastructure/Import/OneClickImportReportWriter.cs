using System.Text;
using AuswertungPro.Next.Application.Import;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.Infrastructure.Import;

/// <summary>Schreibt Ein-Knopf-Importberichte best-effort in den Projektordner.</summary>
public sealed class OneClickImportReportWriter : IOneClickImportReportWriter
{
    private readonly ILogger _logger;

    public OneClickImportReportWriter(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void TryWrite(string projectFolder, OneClickProjectImportResult result)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectFolder);
        ArgumentNullException.ThrowIfNull(result);

        try
        {
            var guard = new ProjectWritePathGuard(projectFolder);
            var directory = guard.EnsureSafeDirectoryTarget(
                Path.Combine(projectFolder, ProjectStructure.ImportReports));
            Directory.CreateDirectory(directory);
            guard.EnsureSafeDirectoryTarget(directory);
            var path = guard.EnsureSafeFileTarget(
                Path.Combine(directory, $"kanalimport_{DateTime.Now:yyyyMMdd_HHmmss}.txt"));
            var text = new StringBuilder()
                .AppendLine($"Kanalfernseh-Import {DateTime.Now:yyyy-MM-dd HH:mm:ss}")
                .AppendLine($"Format: {result.Format}")
                .AppendLine($"Haltungen: {result.Found} (neu {result.Created}, aktualisiert {result.Updated})")
                .AppendLine($"Fehler: {result.Errors}, Feld-Konflikte: {result.Conflicts}")
                .AppendLine($"Vollstaendigkeit: {OneClickImportVollstaendigkeit.Beschreibe(result)}")
                .AppendLine();

            if (result.Bestand is { } bestand)
            {
                text.AppendLine("Bestand nach dem Import:");
                foreach (var zeile in bestand.Berichtszeilen())
                    text.AppendLine("  " + zeile);
                text.AppendLine();
            }

            // Fehler nach Schritten: eine blosse Gesamtzahl verdeckt, WELCHER Teilschritt
            // etwas verloren hat.
            var bilanzzeilen = result.Fehlerbilanz.Berichtszeilen();
            if (bilanzzeilen.Count > 0)
            {
                text.AppendLine("Fehler nach Schritten:");
                foreach (var zeile in bilanzzeilen)
                    text.AppendLine("  " + zeile);
                text.AppendLine();
            }

            foreach (var message in result.Messages)
                text.AppendLine(message);

            File.WriteAllText(path, text.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ein-Knopf-Importbericht konnte nicht geschrieben werden.");
        }
    }
}
