using System.Text.RegularExpressions;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Nova-Etappe 2b, Task 1 (Inventar 4.3): Tabellenkopf in Grossbuchstaben, Zahlenspalten
/// rechtsbuendig, NR nur in "Alle Spalten". Reiner Text-Waechter wie
/// <see cref="DesignAuditNovaPaletteTests"/>; laedt keine echten WPF-Ressourcen.
/// </summary>
public sealed class DesignAuditNovaTabelleTests
{
    [Theory]
    [InlineData("ThemeLight.xaml")]
    [InlineData("Theme.xaml")]
    public void Tabellenkopf_schreibt_gross(string datei)
    {
        var xaml = DesignAuditNovaPaletteTests.Xaml(datei);
        var header = Regex.Match(xaml, "<Style TargetType=\"\\{x:Type DataGridColumnHeader\\}\">[\\s\\S]*?\n    </Style>").Value;
        Assert.True(header.Length > 0, $"{datei}: DataGridColumnHeader-Stil nicht gefunden");

        // Kapitaelchen greifen mit der Programmschrift nicht (Prototyp: echte Grossbuchstaben,
        // Letter-Spacing). Die alte Regel darf nicht mehr im Kopf-Template stehen.
        Assert.DoesNotContain("Typography.Capitals", header);

        // Nur String-Koepfe werden umgewandelt: eine typgebundene DataTemplate fuer sys:String,
        // die den GrossbuchstabenConverter verwendet. Ein nicht-textueller Kopf durchlaeuft
        // diese Vorlage nicht und behaelt den bisherigen ContentPresenter ohne ContentTemplate.
        Assert.Contains("DataType=\"{x:Type sys:String}\"", header);
        Assert.Contains("GrossbuchstabenConverter", header);
        Assert.Contains("Converter={StaticResource Grossbuchstaben}", header);
        Assert.Contains("<ContentPresenter VerticalAlignment=\"Center\"/>", header);
        Assert.Contains("FontSize=\"{DynamicResource TextXS}\"", header);
        Assert.Contains("Foreground=\"{DynamicResource MutedBrush}\"", header);
    }
}
