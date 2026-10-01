using System.IO;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>Bewegung dort, wo sie Klickbarkeit und Seitenaufbau sichtbar macht.</summary>
public sealed class DesignAuditFeedbackTests
{
    [Fact]
    public void Fotokarten_heben_sich_beim_Zeigen_an()
    {
        var xaml = File.ReadAllText(RepoFile("src", "AuswertungPro.Next.UI", "Controls", "PhotoGalleryPanel.xaml"));

        Assert.Contains("xmlns:ui=\"clr-namespace:AuswertungPro.Next.UI\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ui:HoverFx.Lift=\"True\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Dossier_Cockpit_baut_sich_gestaffelt_auf()
    {
        var xaml = File.ReadAllText(RepoFile("src", "AuswertungPro.Next.UI", "Views", "Pages", "DossiersPage.xaml"));

        Assert.Contains("<Grid Margin=\"0\" ui:EntranceFx.Stagger=\"True\">", xaml, StringComparison.Ordinal);
    }
}
