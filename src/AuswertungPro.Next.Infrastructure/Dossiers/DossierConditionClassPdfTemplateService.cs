using AuswertungPro.Next.Application.Dossiers;

using UglyToad.PdfPig;

namespace AuswertungPro.Next.Infrastructure.Dossiers;

/// <summary>
/// Liefert die vom Benutzer freigegebene, feste PDF-Vorlage bytegleich aus.
/// Eine fehlende oder veraenderte ungueltige Vorlage wird sichtbar abgewiesen.
/// </summary>
internal sealed class DossierConditionClassPdfTemplateService
    : IDossierConditionClassPdfService
{
    private readonly string _templatePath;
    private readonly Lazy<byte[]> _pdf;

    public DossierConditionClassPdfTemplateService(string templatePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(templatePath);
        _templatePath = Path.GetFullPath(templatePath);
        _pdf = new Lazy<byte[]>(
            ReadAndValidate,
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public byte[] CreatePdf() => (byte[])_pdf.Value.Clone();

    private byte[] ReadAndValidate()
    {
        if (!File.Exists(_templatePath))
        {
            throw new FileNotFoundException(
                "Die feste PDF-Vorlage fuer die Zustandsklassen fehlt.",
                _templatePath);
        }

        var pdf = File.ReadAllBytes(_templatePath);
        try
        {
            using var document = PdfDocument.Open(pdf);
            if (document.NumberOfPages != DossierConditionClassDefinitions.PdfRequiredPageCount)
            {
                throw new InvalidDataException(
                    "Die feste Zustandsklassen-Vorlage muss genau eine PDF-Seite enthalten.");
            }

            var text = string.Join(
                " ",
                document.GetPage(1).GetWords().Select(word => word.Text));
            if (!text.Contains(
                    DossierConditionClassDefinitions.PdfRequiredPageMarker,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "Die feste Zustandsklassen-Vorlage besitzt nicht ihre Pflichtblatt-Marke.");
            }

            return pdf;
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidDataException(
                "Die feste Zustandsklassen-Vorlage ist keine lesbare PDF-Datei.",
                ex);
        }
    }
}
