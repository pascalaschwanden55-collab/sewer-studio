using System.IO;
using System.Linq;
using AuswertungPro.Next.UI.ViewModels.Pages;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ProjectScanRootsTests
{
    [Fact]
    public void Resolve_includes_configured_root_and_its_subfolders_marker()
    {
        var roots = ProjectScanRoots.Resolve(@"C:\App", @"E:\MeineProjekte");
        Assert.Contains(@"E:\MeineProjekte", roots);
    }

    [Fact]
    public void Resolve_includes_current_rohdaten_folder()
    {
        var roots = ProjectScanRoots.Resolve(@"C:\App", null);
        Assert.Contains(Path.Combine(@"C:\App", "Rohdaten"), roots);
    }

    [Fact]
    public void Resolve_ignores_blank_configured_root()
    {
        var roots = ProjectScanRoots.Resolve(@"C:\App", "   ");
        Assert.Equal(
            new[]
            {
                Path.Combine(@"C:\App", "Rohdaten"),
                Path.Combine(@"C:\App", "Rohdaten", "Section_PDF")
            },
            roots);
    }
}
