using System.Collections;
using System.Linq;
using UglyToad.PdfPig;
using UglyToad.PdfPig.AcroForms;
using UglyToad.PdfPig.AcroForms.Fields;

namespace AuswertungPro.Next.Infrastructure.Import.Pdf;

internal sealed record PdfFormFieldEntry(
    int? PageNumber,
    string? PartialName,
    string? AlternateName,
    string? MappingName,
    string Value);

internal static class PdfFormFieldExtractor
{
    public static IReadOnlyList<PdfFormFieldEntry> GetPageFieldEntries(string pdfPath, int pageNumber)
    {
        if (string.IsNullOrWhiteSpace(pdfPath) || !File.Exists(pdfPath))
            return Array.Empty<PdfFormFieldEntry>();
        if (pageNumber <= 0)
            return Array.Empty<PdfFormFieldEntry>();

        try
        {
            using var document = PdfDocument.Open(pdfPath);
            if (!document.TryGetForm(out var form) || form is null)
                return Array.Empty<PdfFormFieldEntry>();

            var fields = form.GetFieldsForPage(pageNumber)?.ToList();
            if (fields is null || fields.Count == 0)
                return Array.Empty<PdfFormFieldEntry>();

            var entries = new List<PdfFormFieldEntry>();
            foreach (var field in fields)
            {
                if (field is null)
                    continue;

                var values = EnumerateFieldValues(AcroFormExtensions.GetFieldValue(field));
                foreach (var value in values)
                {
                    if (string.IsNullOrWhiteSpace(value))
                        continue;

                    entries.Add(new PdfFormFieldEntry(
                        field.PageNumber,
                        field.Information.PartialName,
                        field.Information.AlternateName,
                        field.Information.MappingName,
                        value.Trim()));
                }
            }

            return entries;
        }
        catch
        {
            return Array.Empty<PdfFormFieldEntry>();
        }
    }

    private static IEnumerable<string> EnumerateFieldValues(object? rawFieldValue)
    {
        if (rawFieldValue is null)
            yield break;

        if (rawFieldValue is string single)
        {
            yield return single;
            yield break;
        }

        if (rawFieldValue is IEnumerable<string> stringSequence)
        {
            foreach (var value in stringSequence)
            {
                if (!string.IsNullOrWhiteSpace(value))
                    yield return value;
            }
            yield break;
        }

        if (rawFieldValue is IEnumerable genericSequence)
        {
            foreach (var item in genericSequence)
            {
                var text = item?.ToString();
                if (!string.IsNullOrWhiteSpace(text))
                    yield return text;
            }
            yield break;
        }

        var fallback = rawFieldValue.ToString();
        if (!string.IsNullOrWhiteSpace(fallback))
            yield return fallback;
    }
}
