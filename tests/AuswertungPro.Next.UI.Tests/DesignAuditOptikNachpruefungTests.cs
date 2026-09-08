using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Waechter der optischen Nachpruefung vom 08.09.2026
/// (docs/reviews/2026-09-08-redesign-gesamtaudit/OPTIK-NACHPRUEFUNG.md). Jeder Test haelt
/// einen dort belegten Befund fest, damit er nicht zurueckfaellt. Reine Quelltext-Pruefungen
/// nach dem Muster der uebrigen DesignAudit*-Klassen.
/// </summary>
public sealed class DesignAuditOptikNachpruefungTests
{
    private static string Ui(params string[] teile)
        => File.ReadAllText(RepoFile(new[] { "src", "AuswertungPro.Next.UI" }.Concat(teile).ToArray()));

    /// <summary>O16: Die Zustandsklasse ist eine Marke mit Tintenregel, kein gelber Text auf Weiss.</summary>
    [Fact]
    public void Schattenauswertung_zeigt_die_Zustandsklasse_als_Marke_mit_Tintenregel()
    {
        var xaml = Ui("Views", "Pages", "SchattenauswertungPage.xaml");
        Assert.Contains("ZustandsklasseInkConverter", xaml);
        Assert.DoesNotContain("Foreground=\"{Binding MenschKlasse, Converter={StaticResource ZustandsklasseBrush}}\"", xaml);
        Assert.DoesNotContain("Foreground=\"{Binding SchattenKlasse, Converter={StaticResource ZustandsklasseBrush}}\"", xaml);
        Assert.Equal(2, Regex.Matches(xaml, "CornerRadius=\"\\{DynamicResource RadiusChip\\}\"").Count);
    }
}
