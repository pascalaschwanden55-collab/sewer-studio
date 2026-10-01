using System.IO;
using System.Xml.Linq;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DpiManifestTests
{
    [Fact]
    public void UI_Manifest_setzt_PerMonitorV2_mit_Rueckfall_und_behaelt_den_Benutzerkontext()
    {
        var root = TestRepoPaths.FindRepositoryRoot();
        var manifestPath = Path.Combine(root, "src", "AuswertungPro.Next.UI", "app.manifest");
        var manifest = XDocument.Load(manifestPath);
        XNamespace v3 = "urn:schemas-microsoft-com:asm.v3";
        XNamespace dpi2016 = "http://schemas.microsoft.com/SMI/2016/WindowsSettings";
        XNamespace dpi2005 = "http://schemas.microsoft.com/SMI/2005/WindowsSettings";

        var windowsSettings = Assert.Single(manifest.Descendants(v3 + "windowsSettings"));
        Assert.Equal("PerMonitorV2,PerMonitor", windowsSettings.Element(dpi2016 + "dpiAwareness")?.Value);
        Assert.Equal("true/pm", windowsSettings.Element(dpi2005 + "dpiAware")?.Value);

        var executionLevel = Assert.Single(manifest.Descendants(v3 + "requestedExecutionLevel"));
        Assert.Equal("asInvoker", executionLevel.Attribute("level")?.Value);
        Assert.Equal("false", executionLevel.Attribute("uiAccess")?.Value);

        var project = File.ReadAllText(Path.Combine(root, "src", "AuswertungPro.Next.UI", "AuswertungPro.Next.UI.csproj"));
        Assert.Contains("<ApplicationManifest>app.manifest</ApplicationManifest>", project, StringComparison.Ordinal);
    }
}
