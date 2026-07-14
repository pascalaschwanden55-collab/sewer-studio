using System.IO;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SchaechtePageProtocolToolbarTests
{
    [Fact]
    public void SchaechtePage_toolbar_bindet_protocol_import_commands()
    {
        var xaml = File.ReadAllText(TestRepoPaths.RepoFile(
            "src",
            "AuswertungPro.Next.UI",
            "Views",
            "Pages",
            "SchaechtePage.xaml"));

        Assert.Contains("Command=\"{Binding RefreshProtocolCommand}\"", xaml);
        Assert.Contains("Command=\"{Binding ImportProtocolCommand}\"", xaml);
        Assert.Contains("Aktualisieren", xaml);
        Assert.Contains("Protokoll importieren", xaml);
        Assert.Contains("ganzen Ordner einschliesslich Unterordner importieren", xaml);
    }
}
