using System.Text.Json;
using AuswertungPro.Next.Application.Export;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Tests fuer die neuen Ziel-/Namensvorlagen-Felder in <see cref="AppSettings"/>
/// (Export-/Verteil-Konfiguration, Etappe 1b): Standardwerte reproduzieren das bisherige
/// flache Ablageschema, und die Konfiguration ueberlebt einen JSON-Roundtrip
/// (identisch zu <c>AppSettings.Save()/Load()</c>).
/// </summary>
public sealed class AppSettingsDistributionConfigTests
{
    [Fact]
    public void Defaults_reproduzieren_bisheriges_flaches_ablageschema()
    {
        var s = new AppSettings();

        // Dateibenennung wie bisher fest verdrahtet: Datum_Haltung bzw. Datum_Schachtnummer.
        Assert.Equal("{Datum}_{Haltung}", s.HaltungDistribution.DateiPattern);
        Assert.Equal("{Datum}_{Schachtnummer}", s.SchachtDistribution.DateiPattern);
        Assert.Equal("{Datum}_{Schachtnummer}", s.DichtheitDistribution.DateiPattern);

        // Ordner-Ebenen standardmaessig leer -> flache Ablage direkt in der Ziel-Wurzel.
        Assert.Equal(string.Empty, s.HaltungDistribution.OrdnerPattern);
        Assert.Equal(string.Empty, s.HaltungDistribution.UnterordnerPattern);
        Assert.Null(s.HaltungDistribution.Root);

        // Excel-Export: schlichte Standard-Dateinamen ohne Ordner-Ebenen.
        Assert.Equal("Haltungen", s.HaltungExport.DateiPattern);
        Assert.Equal("Schaechte", s.SchachtExport.DateiPattern);
    }

    [Fact]
    public void Roundtrip_erhaelt_ziel_wurzel_und_alle_drei_ebenen()
    {
        var s = new AppSettings();
        s.HaltungDistribution.Root = @"D:\Verteilt\Haltungen";
        s.HaltungDistribution.OrdnerPattern = "{Gemeinde}";
        s.HaltungDistribution.UnterordnerPattern = "{Haltung}";
        s.HaltungDistribution.DateiPattern = "{Datum}_{Haltung}";
        s.SchachtExport.Root = @"D:\Export";
        s.SchachtExport.DateiPattern = "Schaechte_{Jahr}";

        var json = JsonSerializer.Serialize(s);
        var back = JsonSerializer.Deserialize<AppSettings>(json)!;

        Assert.Equal(@"D:\Verteilt\Haltungen", back.HaltungDistribution.Root);
        Assert.Equal("{Gemeinde}", back.HaltungDistribution.OrdnerPattern);
        Assert.Equal("{Haltung}", back.HaltungDistribution.UnterordnerPattern);
        Assert.Equal("{Datum}_{Haltung}", back.HaltungDistribution.DateiPattern);
        Assert.Equal(@"D:\Export", back.SchachtExport.Root);
        Assert.Equal("Schaechte_{Jahr}", back.SchachtExport.DateiPattern);
    }
}
