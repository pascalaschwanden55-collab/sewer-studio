using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Import;

namespace AuswertungPro.Next.Infrastructure.Import.Protocols;

/// <summary>
/// Sicherer Nachlauf fuer bestehende Projekte. Veraltete PDF_Path-Werte werden
/// durch Link und die bekannten Schachtordner aufgefangen. Pro PDF wird ein Fehler
/// protokolliert; ein defektes Dokument bricht den Gesamtlauf nicht ab.
/// </summary>
public sealed class SchachtStammdatenErgaenzungsService : ISchachtStammdatenErgaenzungsService
{
    private static readonly string[] KnownSchachtFolders =
    {
        "Schächte_Verteilt",
        "Schaechte_Verteilt",
        "Schächte_1.15",
        "Schaechte_1.15"
    };

    private readonly ISchachtProtocolImportService _protocolImport;

    public SchachtStammdatenErgaenzungsService(ISchachtProtocolImportService protocolImport)
    {
        _protocolImport = protocolImport ?? throw new ArgumentNullException(nameof(protocolImport));
    }

    public SchachtStammdatenErgaenzungsErgebnis Ermitteln(
        string projektOrdner,
        IReadOnlyList<SchachtStammdatenQuelle> schaechte,
        IProgress<SchachtStammdatenErgaenzungsFortschritt>? fortschritt = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projektOrdner);
        ArgumentNullException.ThrowIfNull(schaechte);

        var ergaenzungen = new List<SchachtStammdatenErgaenzung>();
        var meldungen = new List<string>();
        var bereitsVollstaendig = 0;
        var pdfGefunden = 0;
        var pdfNichtGefunden = 0;
        var nichtLesbar = 0;

        for (var index = 0; index < schaechte.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var quelle = schaechte[index];
            var nummer = string.IsNullOrWhiteSpace(quelle.Schachtnummer)
                ? "ohne Nummer"
                : quelle.Schachtnummer.Trim();

            fortschritt?.Report(new SchachtStammdatenErgaenzungsFortschritt(
                index + 1,
                schaechte.Count,
                nummer,
                $"Schacht {nummer}: PDF suchen"));

            if (IsComplete(quelle))
            {
                bereitsVollstaendig++;
                continue;
            }

            var pdfPath = ResolvePdfPath(projektOrdner, quelle);
            if (pdfPath is null)
            {
                pdfNichtGefunden++;
                meldungen.Add($"Schacht {nummer}: keine vorhandene PDF gefunden.");
                continue;
            }

            pdfGefunden++;
            try
            {
                fortschritt?.Report(new SchachtStammdatenErgaenzungsFortschritt(
                    index + 1,
                    schaechte.Count,
                    nummer,
                    $"Schacht {nummer}: PDF lesen"));

                var parsed = _protocolImport.Parse(pdfPath);
                if (!parsed.IstSchachtprotokoll)
                {
                    nichtLesbar++;
                    meldungen.Add($"Schacht {nummer}: PDF ist kein Schachtprotokoll ({Path.GetFileName(pdfPath)}).");
                    continue;
                }

                var form = Missing(quelle.Schachtform) ? parsed.Schachtform : null;
                var dimension = Missing(quelle.Dimension) ? parsed.Dimension : null;
                var tiefe = Missing(quelle.Schachttiefe) ? parsed.Schachttiefe : null;
                if (Missing(form) && Missing(dimension) && Missing(tiefe))
                {
                    meldungen.Add($"Schacht {nummer}: PDF enthaelt keine der fehlenden Stammdaten.");
                    continue;
                }

                ergaenzungen.Add(new SchachtStammdatenErgaenzung(
                    quelle.RecordId,
                    pdfPath,
                    form,
                    dimension,
                    tiefe));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                nichtLesbar++;
                meldungen.Add($"Schacht {nummer}: PDF konnte nicht gelesen werden ({ex.Message}).");
            }
        }

        return new SchachtStammdatenErgaenzungsErgebnis(
            schaechte.Count,
            bereitsVollstaendig,
            pdfGefunden,
            ergaenzungen.Count,
            pdfNichtGefunden,
            nichtLesbar,
            ergaenzungen,
            meldungen);
    }

    internal static string? ResolvePdfPath(string projektOrdner, SchachtStammdatenQuelle quelle)
    {
        foreach (var rawPath in new[] { quelle.PdfPath, quelle.Link })
        {
            var resolved = ResolveStoredPath(projektOrdner, rawPath);
            if (resolved is not null)
                return resolved;
        }

        var safeNumber = ProjectPathResolver.SanitizePathSegment(quelle.Schachtnummer);
        if (safeNumber == "UNKNOWN")
            return null;

        foreach (var baseName in KnownSchachtFolders)
        {
            var numberFolder = Path.Combine(projektOrdner, baseName, safeNumber);
            var match = FindBestPdf(numberFolder, safeNumber);
            if (match is not null)
                return match;
        }

        // Projektnamen koennen abweichende Zusaetze tragen. Deshalb weitere
        // Schacht-Hauptordner kontrolliert pruefen, ohne ausserhalb des Projekts zu suchen.
        try
        {
            foreach (var directory in Directory.EnumerateDirectories(projektOrdner, "*", SearchOption.TopDirectoryOnly))
            {
                var name = Path.GetFileName(directory);
                if (!name.Contains("Schächt", StringComparison.OrdinalIgnoreCase)
                    && !name.Contains("Schaech", StringComparison.OrdinalIgnoreCase))
                    continue;

                var match = FindBestPdf(Path.Combine(directory, safeNumber), safeNumber);
                if (match is not null)
                    return match;
            }
        }
        catch
        {
            // Die direkten Pfade wurden bereits versucht. Ein unlesbarer Zusatzordner
            // darf den Nachlauf nicht fuer alle anderen Schaechte abbrechen.
        }

        return null;
    }

    private static string? ResolveStoredPath(string projektOrdner, string? rawPath)
    {
        var path = rawPath?.Trim();
        if (string.IsNullOrWhiteSpace(path))
            return null;

        try
        {
            if (Path.IsPathRooted(path))
                return File.Exists(path) && IsPdf(path) ? Path.GetFullPath(path) : null;

            var resolved = ProjectPathResolver.ResolveFilePathFromProjectFolder(path, projektOrdner);
            return resolved is not null && IsPdf(resolved) ? resolved : null;
        }
        catch
        {
            return null;
        }
    }

    private static string? FindBestPdf(string numberFolder, string schachtnummer)
    {
        if (!Directory.Exists(numberFolder))
            return null;

        try
        {
            return Directory
                .EnumerateFiles(numberFolder, "*.pdf", SearchOption.AllDirectories)
                .OrderByDescending(path => Path.GetFileNameWithoutExtension(path)
                    .Contains(schachtnummer, StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(File.GetLastWriteTimeUtc)
                .ThenBy(path => path, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    private static bool IsComplete(SchachtStammdatenQuelle quelle)
        => !Missing(quelle.Schachtform)
           && !Missing(quelle.Dimension)
           && !Missing(quelle.Schachttiefe);

    private static bool Missing(string? value) => string.IsNullOrWhiteSpace(value);

    private static bool IsPdf(string path)
        => string.Equals(Path.GetExtension(path), ".pdf", StringComparison.OrdinalIgnoreCase);
}
