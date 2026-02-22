using AuswertungPro.Next.Infrastructure.Import.Pdf;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class SchachtPdfImportMappingTests
{
    [Fact]
    public void ParseSchachtFields_MapsTemplateRelevantFields()
    {
        var text = string.Join("\n", new[]
        {
            "GEP Aufnahmen Altdorf 2025",
            "Schachtprotokoll   Nr. 74467",
            "Schachttyp Kontrollschacht",
            "Zustand der Bauteile      Maengelfrei",
            "Datum 02/10/2025",
            "Bemerkung ohne Auffaelligkeiten"
        });

        var parsed = LegacyPdfImportService.ParseSchachtFields(text);

        Assert.Equal("74467", parsed.SchachtNummer);
        Assert.Equal("02.10.2025", parsed.Datum);
        Assert.Equal("Kontrollschacht", parsed.Funktion);
        Assert.Equal("Maengelfrei", parsed.PrimaereSchaeden);
        Assert.Equal("ohne Auffaelligkeiten", parsed.Bemerkungen);
    }

    [Fact]
    public void ParseSchachtFields_ExtractsMarkedPrimaryDamages_FromZustandDerBauteile()
    {
        var text = string.Join("\n", new[]
        {
            "Schachtprotokoll Nr. 74467",
            "Zustand der Bauteile",
            "Deckelrahmen gerissen ● ausgebrochen lose",
            "Schachthals gerissen ausgebrochen ● korrodiert Fugen mangelhaft verputzt",
            "Datum 02/10/2025"
        });

        var parsed = LegacyPdfImportService.ParseSchachtFields(text);

        Assert.Contains("Deckelrahmen: ausgebrochen", parsed.PrimaereSchaeden);
        Assert.Contains("Schachthals: korrodiert", parsed.PrimaereSchaeden);
    }

    [Fact]
    public void ParseSchachtFields_ExtractsMarkedPrimaryDamages_WhenMarkerAfterDamage()
    {
        var text = string.Join("\n", new[]
        {
            "Schachtprotokoll Nr. 70001",
            "Zustand der Bauteile",
            "Bankett gerissen ausgebrochen ● korrodiert Ablagerungen"
        });

        var parsed = LegacyPdfImportService.ParseSchachtFields(text);

        Assert.Contains("Bankett: ausgebrochen", parsed.PrimaereSchaeden);
    }

    [Fact]
    public void ParseSchachtFields_ExtractsMarkedPrimaryDamages_ForBracketAndCheckmarkMarkers()
    {
        var text = string.Join("\n", new[]
        {
            "Schachtprotokoll Nr. 70002",
            "Zustand der Bauteile",
            "Leiter/Steigeisen [x] fehlt zu kurz verrostet",
            "Tauchbogen vorhanden ✓ defekt nicht notwendig"
        });

        var parsed = LegacyPdfImportService.ParseSchachtFields(text);

        Assert.Contains("Leiter/Steigeisen: fehlt", parsed.PrimaereSchaeden);
        Assert.Contains("Tauchbogen: defekt", parsed.PrimaereSchaeden);
    }

    [Fact]
    public void ParseSchachtFields_SetsStatusOffen_WhenMarkedDamagesExist()
    {
        var text = string.Join("\n", new[]
        {
            "Schachtprotokoll Nr. 80001",
            "Zustand der Bauteile",
            "Deckelrahmen gerissen ● ausgebrochen lose"
        });

        var parsed = LegacyPdfImportService.ParseSchachtFields(text);

        Assert.Equal("offen", parsed.Status);
    }

    [Fact]
    public void ParseSchachtFields_SetsStatusAbgeschlossen_WhenOnlyMaengelfrei()
    {
        var text = string.Join("\n", new[]
        {
            "Schachtprotokoll Nr. 80002",
            "Zustand der Bauteile Maengelfrei"
        });

        var parsed = LegacyPdfImportService.ParseSchachtFields(text);

        Assert.Equal("Maengelfrei", parsed.PrimaereSchaeden);
        Assert.Equal("abgeschlossen", parsed.Status);
    }

    [Fact]
    public void ParseSchachtFields_PrefersExplicitStatus_WhenStatusLineExists()
    {
        var text = string.Join("\n", new[]
        {
            "Schachtprotokoll Nr. 80003",
            "Zustand der Bauteile",
            "Deckelrahmen gerissen ● ausgebrochen lose",
            "Status offen/abgeschlossen: abgeschlossen"
        });

        var parsed = LegacyPdfImportService.ParseSchachtFields(text);

        Assert.Equal("abgeschlossen", parsed.Status);
    }

    [Fact]
    public void ParseSchachtFields_ListsPrimaryDamagesLineByLine_InComponentOrder()
    {
        var text = string.Join("\n", new[]
        {
            "Schachtprotokoll Nr. 90001",
            "Zustand der Bauteile",
            "Schachthals gerissen ausgebrochen â— korrodiert",
            "Deckelrahmen gerissen â— ausgebrochen lose"
        });

        var parsed = LegacyPdfImportService.ParseSchachtFields(text);

        Assert.NotNull(parsed.PrimaereSchaeden);
        Assert.Contains("\n", parsed.PrimaereSchaeden);

        var lines = parsed.PrimaereSchaeden.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        Assert.True(lines.Length >= 2);

        var firstSchachthals = Array.FindIndex(lines, l => l.StartsWith("Schachthals:", StringComparison.OrdinalIgnoreCase));
        var lastDeckelrahmen = Array.FindLastIndex(lines, l => l.StartsWith("Deckelrahmen:", StringComparison.OrdinalIgnoreCase));

        Assert.True(lastDeckelrahmen >= 0, "Deckelrahmen-Eintrag fehlt.");
        Assert.True(firstSchachthals >= 0, "Schachthals-Eintrag fehlt.");
        Assert.True(lastDeckelrahmen < firstSchachthals, "Deckelrahmen muss vor Schachthals gelistet sein.");
    }
}
