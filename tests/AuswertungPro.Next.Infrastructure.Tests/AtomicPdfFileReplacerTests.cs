using AuswertungPro.Next.Infrastructure.Import.Pdf;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class AtomicPdfFileReplacerTests
{
    [Fact]
    public void Gueltige_Pdf_ersetzt_Ziel_und_sichert_Original()
    {
        var directory = Directory.CreateTempSubdirectory().FullName;
        var target = Path.Combine(directory, "ziel.pdf");
        var generated = Path.Combine(directory, "neu.pdf");
        var originalBytes = CreatePdf("alte Fassung");
        var generatedBytes = CreatePdf("neue Fassung");
        File.WriteAllBytes(target, originalBytes);
        File.WriteAllBytes(generated, generatedBytes);

        try
        {
            AtomicPdfFileReplacer.ReplaceValidated(generated, target);

            Assert.Equal(generatedBytes, File.ReadAllBytes(target));
            Assert.Equal(originalBytes, File.ReadAllBytes(target + ".bak"));
        }
        finally
        {
            try { Directory.Delete(directory, recursive: true); } catch { }
        }
    }

    [Fact]
    public void Instanzdienst_ersetzt_Ziel_und_sichert_Original()
    {
        var directory = Directory.CreateTempSubdirectory().FullName;
        var target = Path.Combine(directory, "ziel.pdf");
        var generated = Path.Combine(directory, "neu.pdf");
        var originalBytes = CreatePdf("alte Fassung");
        var generatedBytes = CreatePdf("neue Fassung");
        File.WriteAllBytes(target, originalBytes);
        File.WriteAllBytes(generated, generatedBytes);

        try
        {
            var service = new AtomicPdfFileReplacementService(new PdfFileSafetyService());
            service.ReplaceValidated(generated, target);

            Assert.Equal(generatedBytes, File.ReadAllBytes(target));
            Assert.Equal(originalBytes, File.ReadAllBytes(target + ".bak"));
        }
        finally
        {
            try { Directory.Delete(directory, recursive: true); } catch { }
        }
    }

    [Fact]
    public void Fallback_StelltOriginalWiederHer_WennNeueDateiNichtEingesetztWerdenKann()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"pdfreplace-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        var target = Path.Combine(dir, "ziel.pdf");
        var staged = Path.Combine(dir, "neu.pdf");
        var backup = target + ".bak";
        File.WriteAllText(target, "alt");
        File.WriteAllText(staged, "neu");
        var moveCount = 0;

        try
        {
            Assert.Throws<IOException>(() =>
                AtomicPdfFileReplacer.ReplaceExistingPreservingOriginal(
                    staged,
                    target,
                    backup,
                    replace: (_, _, _) => throw new PlatformNotSupportedException(),
                    move: (source, destination, overwrite) =>
                    {
                        moveCount++;
                        if (moveCount == 2)
                            throw new IOException("simulierter Schreibfehler");
                        File.Move(source, destination, overwrite);
                    }));

            Assert.Equal("alt", File.ReadAllText(target));
            Assert.Equal("neu", File.ReadAllText(staged));
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { }
        }
    }

    private static byte[] CreatePdf(string text)
    {
        using var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var page = builder.AddPage(PageSize.A4);
        page.AddText(text, 12, new PdfPoint(40, 780), font);
        return builder.Build();
    }
}
