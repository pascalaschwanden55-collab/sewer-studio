using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using AuswertungPro.Next.UI.ViewModels.Pages;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Der WebGIS-Teil der Export-Seite liegt seit 24.09.2026 in <see cref="ExportWebGisBereich"/>. WPF meldet einen
/// falschen Bindungspfad nicht — der Knopf bliebe still ohne Wirkung. Deshalb prueft dieser Test jede WebGIS-Bindung
/// der Export-Seite gegen die echten Eigenschaften.
/// </summary>
public sealed class ExportWebGisBereichTests
{
    private static readonly Regex WebGisBindung = new(@"\{Binding\s+(?:Path=)?(?<pfad>WebGis[A-Za-z0-9_.]*)", RegexOptions.Compiled);

    [Fact]
    public void Export_seite_bindet_den_webgis_bereich_ueber_vorhandene_eigenschaften()
    {
        var xaml = File.ReadAllText(RepoFile("src", "AuswertungPro.Next.UI", "Views", "Pages", "ExportPage.xaml"));
        var pfade = WebGisBindung.Matches(xaml).Select(m => m.Groups["pfad"].Value).ToList();

        Assert.Contains("WebGis.Status", pfade);
        Assert.Contains("WebGis.AnmeldenCommand", pfade);
        Assert.Contains("WebGis.UebertragenCommand", pfade);
        Assert.Contains("WebGis.HolenCommand", pfade);
        Assert.Contains("WebGis.AbmeldenCommand", pfade);

        var seite = typeof(ExportPageViewModel).GetProperty("WebGis", BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(seite);
        Assert.Equal(typeof(ExportWebGisBereich), seite!.PropertyType);

        var falsch = pfade
            .Where(p => !p.StartsWith("WebGis.", System.StringComparison.Ordinal)
                || typeof(ExportWebGisBereich).GetProperty(p["WebGis.".Length..], BindingFlags.Public | BindingFlags.Instance) is null)
            .ToList();
        Assert.True(falsch.Count == 0, "Bindungen ohne Ziel: " + string.Join(", ", falsch));
    }

    [Fact]
    public void Ohne_dienste_sind_alle_webgis_knoepfe_aus()
    {
        var bereich = ExportWebGisBereich.Inaktiv();

        Assert.False(bereich.Angemeldet);
        Assert.Equal("Nicht angemeldet.", bereich.Status);
        Assert.False(bereich.AnmeldenCommand.CanExecute(null));
        Assert.False(bereich.UebertragenCommand.CanExecute(null));
        Assert.False(bereich.HolenCommand.CanExecute(null));
        Assert.False(bereich.AbmeldenCommand.CanExecute(null));
    }
}
