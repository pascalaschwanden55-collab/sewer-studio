using System.IO;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingStudioPdfFolderImportWiringTests
{
    [Fact]
    public void TrainingStudio_bietet_Mehrfachordner_und_den_Application_Stapelweg_an()
    {
        var xaml = File.ReadAllText(RepoFile(
            "src",
            "AuswertungPro.Next.UI",
            "Views",
            "Windows",
            "TrainingStudioWindow.xaml"));
        var window = File.ReadAllText(RepoFile(
            "src",
            "AuswertungPro.Next.UI",
            "Views",
            "Windows",
            "TrainingStudioWindow.xaml.cs"));

        Assert.Contains("LoadPdfFolders_Click", xaml, StringComparison.Ordinal);
        Assert.Contains("new Microsoft.Win32.OpenFolderDialog", window, StringComparison.Ordinal);
        Assert.Contains("Multiselect = true", window, StringComparison.Ordinal);
        Assert.Contains("_pdfReviewBatchImport.ImportFoldersAsync", window, StringComparison.Ordinal);
    }

    [Fact]
    public void TrainingStudio_bindet_Eval_Schutz_vor_Einzel_und_Ordnerimport()
    {
        var factory = File.ReadAllText(RepoFile(
            "src",
            "AuswertungPro.Next.UI",
            "Services",
            "TrainingStudioWindowDependencyFactory.cs"));

        Assert.Contains(
            "new TrainingPdfReviewProtectedImportService",
            factory,
            StringComparison.Ordinal);
        Assert.Contains(
            "new TrainingPdfReviewBatchImportUseCase",
            factory,
            StringComparison.Ordinal);
        Assert.Contains(
            "EvalContaminationSetProvider.Load",
            factory,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Zentral_registrierter_PDF_Import_ist_geschuetzt_und_der_Raw_Reader_bleibt_intern()
    {
        var provider = File.ReadAllText(RepoFile(
            "src",
            "AuswertungPro.Next.UI",
            "ServiceProvider.cs"));
        var factory = File.ReadAllText(RepoFile(
            "src",
            "AuswertungPro.Next.UI",
            "Services",
            "TrainingStudioWindowDependencyFactory.cs"));

        Assert.Contains(
            "internal ITrainingPdfReviewImportService TrainingPdfReviewReader",
            provider,
            StringComparison.Ordinal);
        Assert.Contains(
            "TrainingPdfReviews = new TrainingPdfReviewProtectedImportService",
            provider,
            StringComparison.Ordinal);
        Assert.Contains(
            "services?.TrainingPdfReviewReader",
            factory,
            StringComparison.Ordinal);
    }

    [Fact]
    public void TrainingStudio_haelt_grosse_Ordnerimporte_bedienbar_und_abbrechbar()
    {
        var xaml = File.ReadAllText(RepoFile(
            "src",
            "AuswertungPro.Next.UI",
            "Views",
            "Windows",
            "TrainingStudioWindow.xaml"));
        var window = File.ReadAllText(RepoFile(
            "src",
            "AuswertungPro.Next.UI",
            "Views",
            "Windows",
            "TrainingStudioWindow.xaml.cs"));

        Assert.Contains("x:Name=\"PdfSourceToolbar\"", xaml, StringComparison.Ordinal);
        Assert.Contains("<WrapPanel", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"PdfImportCancelButton\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Click=\"CancelPdfImport_Click\"", xaml, StringComparison.Ordinal);
        Assert.Contains("VirtualizingPanel.IsVirtualizing=\"True\"", xaml, StringComparison.Ordinal);
        Assert.Contains("VirtualizingPanel.VirtualizationMode=\"Recycling\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ConverterParameter=160", xaml, StringComparison.Ordinal);

        Assert.Contains("SetPdfImportUiBusy(true)", window, StringComparison.Ordinal);
        Assert.Contains("SetPdfImportUiBusy(false)", window, StringComparison.Ordinal);
        Assert.Contains("_pdfImportCts?.Cancel()", window, StringComparison.Ordinal);
        Assert.Contains("TrainingStudioWindow_PreviewKeyDown", window, StringComparison.Ordinal);
    }

    [Fact]
    public void TrainingStudio_verdrahtet_die_persistente_90er_Goldpruefung()
    {
        var xaml = File.ReadAllText(RepoFile(
            "src",
            "AuswertungPro.Next.UI",
            "Views",
            "Windows",
            "TrainingStudioWindow.xaml"));
        var window = File.ReadAllText(RepoFile(
            "src",
            "AuswertungPro.Next.UI",
            "Views",
            "Windows",
            "TrainingStudioWindow.xaml.cs"));
        var factory = File.ReadAllText(RepoFile(
            "src",
            "AuswertungPro.Next.UI",
            "Services",
            "TrainingStudioWindowDependencyFactory.cs"));

        Assert.Contains("Command=\"{Binding LoadGoldQualityReviewCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("QueueTotalCount", xaml, StringComparison.Ordinal);
        Assert.Contains("goldQualityReview: dependencies.GoldQualityReview", window, StringComparison.Ordinal);
        Assert.Contains("new GoldQualityReviewSnapshotProvider", factory, StringComparison.Ordinal);
        Assert.Contains("new GoldQualityReviewSessionFileStore", factory, StringComparison.Ordinal);
        Assert.Contains("services?.TrainingExportRegistry", factory, StringComparison.Ordinal);
    }

    [Fact]
    public void TrainingStudio_zeigt_nach_dem_Goldspeichern_beide_Mehrfachobjekt_Entscheidungen()
    {
        var xaml = File.ReadAllText(RepoFile(
            "src",
            "AuswertungPro.Next.UI",
            "Views",
            "Windows",
            "TrainingStudioWindow.xaml"));

        Assert.Contains("Weiteres Ereignis auf diesem Bild", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding AddAnotherEventCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"Bild fertig\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding FinishImageCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("IsAwaitingImageCompletion", xaml, StringComparison.Ordinal);
        Assert.Contains("IsAnnotationEntryEnabled", xaml, StringComparison.Ordinal);
    }
}
