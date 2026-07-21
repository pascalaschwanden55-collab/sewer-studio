using System.IO;
using System.Text.Json;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Import;

namespace AuswertungPro.Next.Infrastructure.Import;

/// <summary>
/// Datei-basierter Transaktions-Marker <c>.import-transaction.json</c> im Projekt-Root,
/// atomar geschrieben ueber <see cref="AtomicTextFileWriter"/>. Lesefehler (fehlend/kaputt)
/// ergeben null statt einer Ausnahme — Recovery entscheidet dann „keine offene Transaktion".
/// </summary>
public sealed class FileImportTransactionJournal : IImportTransactionJournal
{
    public const string MarkerFileName = ".import-transaction.json";

    private static readonly JsonSerializerOptions Opt = new() { WriteIndented = true };

    public void Begin(string projectRoot, ImportTransactionMarker marker)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
        ArgumentNullException.ThrowIfNull(marker);

        var json = JsonSerializer.Serialize(marker, Opt);
        AtomicTextFileWriter.WriteAllText(MarkerPath(projectRoot), json);
    }

    public ImportTransactionMarker? TryRead(string projectRoot)
    {
        if (string.IsNullOrWhiteSpace(projectRoot))
            return null;

        var path = MarkerPath(projectRoot);
        if (!File.Exists(path))
            return null;

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<ImportTransactionMarker>(json, Opt);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public void Clear(string projectRoot)
    {
        if (string.IsNullOrWhiteSpace(projectRoot))
            return;

        try
        {
            var path = MarkerPath(projectRoot);
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Bleibt der Marker liegen, wird er beim naechsten Laden erneut geprueft — kein harter Fehler.
        }
    }

    private static string MarkerPath(string projectRoot)
        => Path.Combine(projectRoot, MarkerFileName);
}
