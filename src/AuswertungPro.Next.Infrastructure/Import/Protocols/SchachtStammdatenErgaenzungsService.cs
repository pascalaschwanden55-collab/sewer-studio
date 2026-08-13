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
    private static readonly ISchachtProtocolFileLocator Locator = new SchachtProtocolFileLocator();

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
                    var detail = string.IsNullOrWhiteSpace(parsed.Lesehinweis)
                        ? "PDF ist kein Schachtprotokoll."
                        : parsed.Lesehinweis;
                    meldungen.Add($"Schacht {nummer}: {detail} ({Path.GetFileName(pdfPath)}).");
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
        => Locator.Locate(projektOrdner, quelle.PdfPath, quelle.Link, quelle.Schachtnummer)?.PdfPfad;

    private static bool IsComplete(SchachtStammdatenQuelle quelle)
        => !Missing(quelle.Schachtform)
           && !Missing(quelle.Dimension)
           && !Missing(quelle.Schachttiefe);

    private static bool Missing(string? value) => string.IsNullOrWhiteSpace(value);
}
