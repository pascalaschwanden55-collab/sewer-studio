using AuswertungPro.Next.Infrastructure.Dossiers;

namespace AuswertungPro.Next.Infrastructure.Tests.Dossiers;

/// <summary>
/// LibreOffice schreibt die Word-Textmarken NUR dann als benannte Ziele in die
/// PDF, wenn der Filterschalter gesetzt ist. Ohne ihn kommt keine einzige Marke
/// an - gemessen an einem echten Lauf mit der ausgelieferten Vorlage.
///
/// Der Schalter ist damit kein Detail, sondern die Bedingung dafuer, dass die
/// Vorschau ein Feld exakt statt geraten zuordnet.
/// </summary>
public sealed class LibreOfficeZielExportTests
{
    [Fact]
    public void Der_Umwandlungsaufruf_verlangt_die_benannten_Ziele()
    {
        var startInfo = LibreOfficeWriterPdfConverter.CreateStartInfo(
            @"C:\Program Files\LibreOffice\program\soffice.exe",
            @"C:\Dossier\Eigentuemerdossier.docx",
            @"C:\Ausgabe",
            @"C:\Temp\Profil");

        var filter = Assert.Single(
            startInfo.ArgumentList.Where(argument =>
                argument.StartsWith("pdf:writer_pdf_Export", StringComparison.Ordinal)));

        Assert.Contains("ExportBookmarksToPDFDestination", filter, StringComparison.Ordinal);
        Assert.Contains("\"value\":\"true\"", filter, StringComparison.Ordinal);
    }

    [Fact]
    public void Die_Filterangabe_bleibt_ein_einzelnes_Argument()
    {
        // Als mehrere Argumente uebergeben wuerde LibreOffice sie als Dateinamen
        // deuten und die Umwandlung stillschweigend anders ausfuehren.
        var startInfo = LibreOfficeWriterPdfConverter.CreateStartInfo(
            @"C:\Program Files\LibreOffice\program\soffice.exe",
            @"C:\Dossier\Eigentuemerdossier.docx",
            @"C:\Ausgabe",
            @"C:\Temp\Profil");

        var argumente = startInfo.ArgumentList.ToArray();
        var index = Array.FindIndex(argumente, a => a == "--convert-to");

        Assert.True(index >= 0, "Der Umwandlungsbefehl fehlt.");
        Assert.StartsWith("pdf:writer_pdf_Export", argumente[index + 1], StringComparison.Ordinal);
        Assert.DoesNotContain(argumente, a => a == "{");
    }

    [Fact]
    public void Der_bisherige_Aufrufrahmen_bleibt_unveraendert()
    {
        var startInfo = LibreOfficeWriterPdfConverter.CreateStartInfo(
            @"C:\Program Files\LibreOffice\program\soffice.exe",
            @"C:\Dossier\Eigentuemerdossier.docx",
            @"C:\Ausgabe",
            @"C:\Temp\Profil");

        var argumente = startInfo.ArgumentList.ToArray();

        Assert.Contains("--headless", argumente);
        Assert.Contains("--nologo", argumente);
        Assert.Contains(@"C:\Ausgabe", argumente);
        Assert.Contains(@"C:\Dossier\Eigentuemerdossier.docx", argumente);
        Assert.Contains(argumente, a => a.StartsWith("-env:UserInstallation=file:///", StringComparison.Ordinal));
    }
}
