using AuswertungPro.Next.Application.Ai.Workbench;
using AuswertungPro.Next.UI.ViewModels;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingStudioPdfSourcePresentationTests
{
    [Fact]
    public void Format_zeigt_Haltung_Datum_Quelle_Code_und_Strecke()
    {
        var item = new WorkbenchItem(
            @"C:\temp\foto.jpg",
            "999001-90327",
            1.6,
            4.6,
            "999001-90327",
            VideoPath: null,
            PipeDiameterMm: 300,
            IsStreckenschaden: true,
            SourceSuggestion: new WorkbenchSourceSuggestion(
                "BABBC",
                "Riss, komplexe Rissbildung von 10 Uhr bis 2 Uhr",
                "haltung.pdf",
                new string('a', 64),
                3,
                "IMG-1.jpg",
                "time_meter_text"))
        {
            InspectionDate = new DateTime(2023, 11, 23),
        };

        var text = TrainingStudioPdfSourcePresentation.Format(item);

        Assert.Contains("Haltung 999001-90327", text, StringComparison.Ordinal);
        Assert.Contains("Datum 23.11.2023", text, StringComparison.Ordinal);
        Assert.Contains("haltung.pdf", text, StringComparison.Ordinal);
        Assert.Contains("1.60", text, StringComparison.Ordinal);
        Assert.Contains("4.60 m", text, StringComparison.Ordinal);
        Assert.Contains("BABBC", text, StringComparison.Ordinal);
        Assert.Contains("Riss, komplexe Rissbildung", text, StringComparison.Ordinal);
    }
}
