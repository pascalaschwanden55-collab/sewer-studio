using AuswertungPro.Next.Application.Import;

namespace AuswertungPro.Next.Infrastructure.Import.Pdf;

/// <summary>Prueft PDF-Dateien gegen das konfigurierbare Groessenbudget.</summary>
public sealed class PdfFileSafetyService : IPdfFileSafetyChecker
{
    public long ResolveMaxBytes()
        => PdfImportSafetyPolicy.ResolveMaxBytes(
            Environment.GetEnvironmentVariable(PdfImportSafetyPolicy.MaxBytesEnvVar));

    public PdfFileSafetyResult CheckFileBudget(string pdfPath, long? maxBytes = null)
    {
        if (string.IsNullOrWhiteSpace(pdfPath))
            return new PdfFileSafetyResult(false, "PDF-Pfad fehlt.");

        var limit = maxBytes ?? ResolveMaxBytes();
        if (limit <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxBytes),
                "Maximale PDF-Groesse muss positiv sein.");
        }

        var file = new FileInfo(pdfPath);
        if (!file.Exists)
            return new PdfFileSafetyResult(false, $"PDF nicht gefunden: {pdfPath}");

        if (file.Length > limit)
        {
            var actualMb = file.Length / 1024d / 1024d;
            var maxMb = limit / 1024d / 1024d;
            return new PdfFileSafetyResult(
                false,
                $"PDF ist zu gross ({actualMb:F1} MB, Limit {maxMb:F1} MB): {pdfPath}");
        }

        return new PdfFileSafetyResult(true, null);
    }

    public void ThrowIfFileTooLarge(string pdfPath, long? maxBytes = null)
    {
        var check = CheckFileBudget(pdfPath, maxBytes);
        if (!check.Allowed)
            throw new InvalidDataException(check.Message);
    }
}
