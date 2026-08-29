using System;
using System.IO;
using System.Linq;

using AuswertungPro.Next.Infrastructure.Dossiers;
using AuswertungPro.Next.UI.Views.Windows;

using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DossierPageSelectionWindowTests
{
    [Fact]
    public void Zustandsklassen_Erklaerblatt_wird_als_Pflichtblatt_erkannt()
    {
        var pdf = new DossierConditionClassPdfService(
            templateAssetFolder: Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")))
            .CreatePdf();

        var pflichtblaetter = DossierPageSelectionWindow.FindePflichtblaetter(pdf);

        Assert.Equal([1], pflichtblaetter.OrderBy(seite => seite));
    }

    [Fact]
    public void Normale_Seite_mit_gleicher_Ueberschrift_ist_kein_Pflichtblatt()
    {
        using var speicher = new MemoryStream();
        using (var bauer = new PdfDocumentBuilder(speicher))
        {
            var schrift = bauer.AddStandard14Font(Standard14Font.Helvetica);
            bauer.AddPage(595, 842).AddText(
                "Zustandsklassen Z0 bis Z4",
                12,
                new UglyToad.PdfPig.Core.PdfPoint(50, 700),
                schrift);
        }

        var pflichtblaetter = DossierPageSelectionWindow.FindePflichtblaetter(
            speicher.ToArray());

        Assert.Empty(pflichtblaetter);
    }
}
