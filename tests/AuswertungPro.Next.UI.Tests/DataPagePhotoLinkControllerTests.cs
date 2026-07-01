using AuswertungPro.Next.UI.DataPage;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataPagePhotoLinkControllerTests
{
    [Fact]
    public void BuildOpenPlan_returns_noop_for_blank_path()
    {
        var plan = DataPagePhotoLinkController.BuildOpenPlan(
            " ",
            @"C:\Projekt\Projektdateien\projekt.json",
            (_, _) => throw new InvalidOperationException("Soll nicht aufloesen"),
            _ => throw new InvalidOperationException("Soll nicht pruefen"));

        Assert.Equal(DataPagePhotoLinkStatus.Noop, plan.Status);
    }

    [Fact]
    public void BuildOpenPlan_opens_resolved_project_relative_path()
    {
        var plan = DataPagePhotoLinkController.BuildOpenPlan(
            "Fotos/Haltungen/H1/bild.jpg",
            @"C:\Projekt\Projektdateien\projekt.json",
            (raw, projectPath) => projectPath is null ? null : @"C:\Projekt\Fotos\Haltungen\H1\bild.jpg",
            path => path.EndsWith("bild.jpg", StringComparison.OrdinalIgnoreCase));

        Assert.Equal(DataPagePhotoLinkStatus.Open, plan.Status);
        Assert.Equal(@"C:\Projekt\Fotos\Haltungen\H1\bild.jpg", plan.ResolvedPath);
    }

    [Fact]
    public void BuildOpenPlan_reports_missing_when_resolved_file_does_not_exist()
    {
        var plan = DataPagePhotoLinkController.BuildOpenPlan(
            "Fotos/Haltungen/H1/fehlt.jpg",
            @"C:\Projekt\Projektdateien\projekt.json",
            (_, _) => @"C:\Projekt\Fotos\Haltungen\H1\fehlt.jpg",
            _ => false);

        Assert.Equal(DataPagePhotoLinkStatus.Missing, plan.Status);
        Assert.Equal("Fotos/Haltungen/H1/fehlt.jpg", plan.RawPath);
    }
}
