using System.Text;
using AuswertungPro.Next.Infrastructure.Import.Pdf;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class PdfFormFieldExtractorTests
{
    [Fact]
    public void InteraktivesTextfeld_LiefertNamenWertUndSeitennummer()
    {
        var directory = Directory.CreateTempSubdirectory().FullName;
        var pdfPath = Path.Combine(directory, "formular.pdf");
        try
        {
            WriteInteractiveTextFieldPdf(pdfPath);

            var entries = PdfFormFieldExtractor.GetPageFieldEntries(pdfPath, pageNumber: 1);
            var directEntries = new PdfFormFieldReaderService()
                .GetPageFieldEntries(pdfPath, pageNumber: 1);

            var entry = Assert.Single(entries);
            Assert.Equal(entries, directEntries);
            Assert.Equal(1, entry.PageNumber);
            Assert.Equal("Schachtnummer", entry.PartialName);
            Assert.Equal("Schacht Nr.", entry.AlternateName);
            Assert.Equal("MapName", entry.MappingName);
            Assert.Equal("[Schachtnummer, 74467]", entry.Value);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void FehlendeDateiUndUngueltigeSeite_LiefernLeereListe()
    {
        Assert.Empty(PdfFormFieldExtractor.GetPageFieldEntries("nicht-vorhanden.pdf", 1));
        Assert.Empty(PdfFormFieldExtractor.GetPageFieldEntries("nicht-vorhanden.pdf", 0));
    }

    [Fact]
    public void BeschaedigtePdf_LiefertLeereListeStattFehler()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "kein PDF");

            var entries = PdfFormFieldExtractor.GetPageFieldEntries(path, 1);

            Assert.Empty(entries);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static void WriteInteractiveTextFieldPdf(string path)
    {
        var objects = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R /AcroForm 5 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << >> /Annots [6 0 R] /Contents 4 0 R >>",
            "<< /Length 0 >>\nstream\n\nendstream",
            "<< /Fields [6 0 R] /NeedAppearances true >>",
            "<< /Type /Annot /Subtype /Widget /FT /Tx /T (Schachtnummer) /TU (Schacht Nr.) /TM (MapName) /V (74467) /Rect [50 700 200 720] /P 3 0 R >>"
        };

        var pdf = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int> { 0 };
        for (var index = 0; index < objects.Length; index++)
        {
            offsets.Add(Encoding.ASCII.GetByteCount(pdf.ToString()));
            pdf.Append(index + 1).Append(" 0 obj\n")
                .Append(objects[index]).Append("\nendobj\n");
        }

        var xrefOffset = Encoding.ASCII.GetByteCount(pdf.ToString());
        pdf.Append("xref\n0 ").Append(objects.Length + 1).Append('\n')
            .Append("0000000000 65535 f \n");
        foreach (var offset in offsets.Skip(1))
            pdf.Append(offset.ToString("D10")).Append(" 00000 n \n");

        pdf.Append("trailer\n<< /Size ").Append(objects.Length + 1)
            .Append(" /Root 1 0 R >>\nstartxref\n")
            .Append(xrefOffset).Append("\n%%EOF\n");
        File.WriteAllText(path, pdf.ToString(), Encoding.ASCII);
    }
}
