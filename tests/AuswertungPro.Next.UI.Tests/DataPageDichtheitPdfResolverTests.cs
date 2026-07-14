using System;
using System.IO;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.DataPage;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Aufloesung der verteilten Dichtheitspruefungsprotokolle einer Haltung
/// fuer den Kontextmenuepunkt (Schaltzentrale: Play/Protokoll/Dichtheit an einem Ort).
/// </summary>
public sealed class DataPageDichtheitPdfResolverTests : IDisposable
{
    private readonly string _projectDir;

    public DataPageDichtheitPdfResolverTests()
    {
        _projectDir = Path.Combine(Path.GetTempPath(), "DpResolverTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_projectDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_projectDir, recursive: true); } catch { }
    }

    private static HaltungRecord Haltung(string name)
    {
        var record = new HaltungRecord();
        record.SetFieldValue("Haltungsname", name, FieldSource.Xtf, userEdited: false);
        return record;
    }

    private string LegeDpPdfAn(string haltung, string dateiName)
    {
        var dir = Path.Combine(_projectDir, "Haltungen_Verteilt", haltung);
        Directory.CreateDirectory(dir);
        var pfad = Path.Combine(dir, dateiName);
        File.WriteAllText(pfad, "%PDF");
        return pfad;
    }

    [Fact]
    public void Resolve_FindetDpProtokolle_NeuesteZuerst()
    {
        var alt = LegeDpPdfAn("58951-58950", "20260501_58951-58950_DP.pdf");
        var neu = LegeDpPdfAn("58951-58950", "20260622_58951-58950_DP.pdf");
        LegeDpPdfAn("58951-58950", "20260622_58951-58950.pdf"); // Kanalfernseh-Protokoll: NICHT dabei

        var gefunden = DataPageDichtheitPdfResolver.Resolve(Haltung("58951-58950"), _projectDir);

        Assert.Equal(2, gefunden.Count);
        Assert.Equal(neu, gefunden[0]);
        Assert.Equal(alt, gefunden[1]);
    }

    [Fact]
    public void Resolve_OhneDpDateien_LiefertLeer()
    {
        LegeDpPdfAn("58951-58950", "20260622_58951-58950.pdf"); // nur TV-Protokoll

        var gefunden = DataPageDichtheitPdfResolver.Resolve(Haltung("58951-58950"), _projectDir);

        Assert.Empty(gefunden);
    }

    [Fact]
    public void Resolve_FindetDpProtokoll_UnterKonfiguriertenUeberordnern()
    {
        var dir = Path.Combine(
            _projectDir, "Haltungen_Verteilt", "Altdorf", "2026", "58951-58950");
        Directory.CreateDirectory(dir);
        var erwartet = Path.Combine(dir, "20260622_58951-58950_DP.pdf");
        File.WriteAllText(erwartet, "%PDF");

        var gefunden = DataPageDichtheitPdfResolver.Resolve(Haltung("58951-58950"), _projectDir);

        Assert.Equal([erwartet], gefunden);
    }

    [Fact]
    public void Resolve_BeruecksichtigtExternesKonfiguriertesZiel()
    {
        var externRoot = Path.Combine(_projectDir, "Extern");
        var dir = Path.Combine(externRoot, "Altdorf", "2026", "58951-58950");
        Directory.CreateDirectory(dir);
        var erwartet = Path.Combine(dir, "20260622_58951-58950_DP.pdf");
        File.WriteAllText(erwartet, "%PDF");

        var gefunden = DataPageDichtheitPdfResolver.Resolve(
            Haltung("58951-58950"),
            projectFolder: null,
            configuredRoot: externRoot);

        Assert.Equal([erwartet], gefunden);
    }

    [Fact]
    public void Resolve_MitNullEingaben_LiefertLeer_OhneFehler()
    {
        Assert.Empty(DataPageDichtheitPdfResolver.Resolve(null, _projectDir));
        Assert.Empty(DataPageDichtheitPdfResolver.Resolve(Haltung("58951-58950"), null));
        Assert.Empty(DataPageDichtheitPdfResolver.Resolve(Haltung(""), _projectDir));
    }
}
