using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

internal sealed record PdfCodeScanOptions(
    string CodePrefix,
    string RootPath,
    string? OutPath,
    int? ExpectedHoldings,
    int? ExpectedFindings)
{
    public static bool TryParse(
        string[] arguments,
        out PdfCodeScanOptions? options,
        out string? error)
    {
        options = null;
        error = null;
        if (arguments.Length < 2 || !string.Equals(arguments[0], "--class", StringComparison.Ordinal))
            return false;

        var codePrefix = arguments[1].Trim().ToUpperInvariant();
        if (codePrefix.Length == 0)
        {
            error = "Der Schadencode darf nicht leer sein.";
            return false;
        }

        var rootPath = @"D:\Haltungen";
        string? outPath = null;
        int? expectedHoldings = null;
        int? expectedFindings = null;
        for (var index = 2; index < arguments.Length; index++)
        {
            if (string.Equals(arguments[index], "--root", StringComparison.Ordinal))
            {
                if (++index >= arguments.Length)
                {
                    error = "Nach --root fehlt der Ordner.";
                    return false;
                }
                rootPath = arguments[index];
            }
            else if (string.Equals(arguments[index], "--out", StringComparison.Ordinal))
            {
                if (++index >= arguments.Length)
                {
                    error = "Nach --out fehlt die Datei.";
                    return false;
                }
                outPath = Path.GetFullPath(arguments[index]);
            }
            else if (string.Equals(arguments[index], "--expect-holdings", StringComparison.Ordinal))
            {
                if (++index >= arguments.Length
                    || !int.TryParse(arguments[index], out var parsed)
                    || parsed < 0)
                {
                    error = "Nach --expect-holdings fehlt eine gueltige Zahl.";
                    return false;
                }
                expectedHoldings = parsed;
            }
            else if (string.Equals(arguments[index], "--expect-findings", StringComparison.Ordinal))
            {
                if (++index >= arguments.Length
                    || !int.TryParse(arguments[index], out var parsed)
                    || parsed < 0)
                {
                    error = "Nach --expect-findings fehlt eine gueltige Zahl.";
                    return false;
                }
                expectedFindings = parsed;
            }
            else
            {
                error = $"Unbekannte Option: {arguments[index]}";
                return false;
            }
        }

        if (expectedHoldings.HasValue != expectedFindings.HasValue)
        {
            error = "--expect-holdings und --expect-findings muessen gemeinsam angegeben werden.";
            return false;
        }

        options = new PdfCodeScanOptions(
            codePrefix,
            Path.GetFullPath(rootPath),
            outPath,
            expectedHoldings,
            expectedFindings);
        return true;
    }
}

internal static class PdfCodeScanWriter
{
    public static string Serialize(PdfCodeScanReport report)
        => JsonSerializer.Serialize(
            report,
            new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
            });

    public static void WriteAtomically(string path, string content)
    {
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath)
            ?? throw new InvalidOperationException($"Ausgabeordner fehlt: {fullPath}");
        Directory.CreateDirectory(directory);
        var tempPath = Path.Combine(directory, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(tempPath, content + Environment.NewLine, new UTF8Encoding(false));
            File.Move(tempPath, fullPath, true);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }
}

internal sealed record PdfCodeScanReport(
    int SchemaVersion,
    string Klasse,
    string Stamm,
    DateTime ErstelltUtc,
    int OrdnerGescannt,
    int Treffer,
    ScanSummary Zusammenfassung,
    InventoryCheck? Bestandsabgleich,
    BccSelectionSummary? Messauswahl,
    IReadOnlyList<CodeSummary> Untercodes,
    IReadOnlyList<BccExclusion> Ausschluesse,
    IReadOnlyList<HoldingScanResult> Ergebnisse);

internal sealed record InventoryCheck(
    int ErwarteteHaltungen,
    int ErwarteteBefunde,
    int GefundeneHaltungen,
    int GefundeneBefunde,
    bool Passt);

internal sealed record ScanSummary(
    int HaltungenMitPraefix,
    int BefundeMitPraefix,
    int PdfImportfehler,
    int PdfLesefehler,
    int HaltungenOhneLesbaresPdf,
    int BefundeMitMeterstand,
    int BefundeMitVideozaehlerstand,
    int BefundeMitEindeutigemVideo);

internal sealed record BccSelectionSummary(
    int Haltungen,
    int Befunde,
    int BefundeMitMeterstand,
    int BefundeMitVideozaehlerstand,
    int BefundeMitEindeutigemVideo);

internal sealed record CodeSummary(
    string Code,
    int HaltungenVorAusschluss,
    int BefundeVorAusschluss,
    int HaltungenAuswahl,
    int BefundeAuswahl);

internal sealed record BccExclusion(string Haltung, string Grund);

internal sealed record HoldingScanResult(
    string Haltung,
    string[] Codes,
    int BefundeMitPraefix,
    int Fotos,
    int Pdfs,
    int PdfsOk,
    int PdfsFehler,
    int PdfsLesbar,
    int PdfsLesefehler,
    string[] Videos,
    bool AuswahlGeeignet,
    string? Ausschlussgrund,
    IReadOnlyList<ProtocolPosition> Positionen);

internal sealed record ProtocolPosition(
    string Code,
    double? MeterStart,
    double? MeterEnd,
    string? VideoCounter,
    double? VideoCounterSeconds,
    string VideoCounterSource,
    string PositionSource,
    string SourcePdf,
    int? SourcePage,
    string? VideoPath,
    string VideoMatch,
    bool IstGueltigerBccUntercode);

internal sealed record VideoCounterResolution(TimeSpan? Value, string? DisplayValue, string Source);
internal sealed record VideoMatch(string? Path, string Status);
internal sealed record PdfEvidence(bool Readable, int PhotoCount, bool ContainsMalformedBcc);
