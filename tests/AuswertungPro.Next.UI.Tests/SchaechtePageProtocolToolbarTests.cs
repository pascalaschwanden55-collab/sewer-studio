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
        // Optikanalyse 28.09.2026, Aufgabe 7: "Aktualisieren" (unklarer Name) -> "Protokoll neu
        // einlesen", passend zum Tooltip "Verknüpftes Protokoll neu einlesen ...".
        Assert.Contains("Protokoll neu einlesen", xaml);
        Assert.Contains("Protokoll importieren", xaml);
        Assert.Contains("ganzen Ordner einschliesslich Unterordner importieren", xaml);
    }
}
