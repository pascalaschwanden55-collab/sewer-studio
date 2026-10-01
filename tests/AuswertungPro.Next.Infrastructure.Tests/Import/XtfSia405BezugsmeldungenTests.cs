using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import.Common;
using AuswertungPro.Next.Infrastructure.Import.Xtf;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

/// <summary>
/// Fehlende Bezuege im SIA405-Haltungsimport stehen im Importbericht (Auftrag Pascal
/// 01.10.2026, Befund 2 aus AP08). Bis dahin fielen ein Kanal-, Rohrprofil-,
/// Haltungspunkt- oder Organisationsverweis ins Leere und eine Haltung ohne Namen still
/// weg. Die Werte bleiben unveraendert; neu ist nur die Meldung. Ein Organisationsverweis
/// ausserhalb der Datei ist nach Norm erlaubt (EXTERNAL) und darum keine Warnung.
/// </summary>
public sealed class XtfSia405BezugsmeldungenTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "xtf_bezuege_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); }
        catch (IOException) { /* Aufraeumen darf den Test nicht faellen. */ }
    }

    [Theory]
    [InlineData("refHALTUNG3", "Kanalverweis refKANALFEHLT")]
    [InlineData("refHALTUNG3", "Rohrprofilverweis refPROFILFEHLT")]
    [InlineData("refHALTUNG3", "Haltungspunktverweis refPUNKTFEHLT (oben)")]
    [InlineData("refHALTUNG4", "Eigentümer-Verweis refORGOHNENAME")]
    [InlineData("refHALTUNG5", "Haltung ohne Bezeichnung")]
    public void Ein_fehlender_Bezug_steht_als_Warnung_mit_TID_im_Importbericht(string tid, string verweis)
    {
        var (_, stats) = Importiere("sia405-referenz.xtf");

        var meldung = Assert.Single(stats.Messages, m => m.Message.Contains($"TID {tid}", StringComparison.Ordinal)
                                                         && m.Message.Contains(verweis, StringComparison.Ordinal));
        Assert.Equal("Warn", meldung.Level);
        Assert.Equal("XTF405", meldung.Context);
    }

    [Fact]
    public void Ein_Profilverweis_ins_Leere_nennt_den_Rueckfall_auf_die_Lichte_Breite_und_aendert_keinen_Wert()
    {
        var (projekt, stats) = Importiere("sia405-referenz.xtf");

        Assert.Contains(stats.Messages, m => m.Message.Contains("refPROFILFEHLT", StringComparison.Ordinal)
                                             && m.Message.Contains("Lichte_Breite", StringComparison.Ordinal));
        var haltung = Assert.Single(projekt.Data, r => r.GetFieldValue(FieldKeys.HoldingName) == "102-103");
        Assert.Equal("500", haltung.GetFieldValue(FieldKeys.ClearWidthMm));
        Assert.Equal("", haltung.GetFieldValue("Schacht_oben"));
        Assert.DoesNotContain(projekt.Data, r => r.GetFieldValue(FieldKeys.CadastreObjectId) == "refHALTUNG5");
    }

    [Fact]
    public void Eine_Organisation_ausserhalb_der_Datei_ist_kein_Fehler_sondern_ein_Hinweis()
    {
        var (_, stats) = Importiere("sia405-referenz.xtf");

        Assert.DoesNotContain(stats.Messages, m => m.Level != "Info" && m.Message.Contains("refORGFEHLT", StringComparison.Ordinal));
        var hinweis = Assert.Single(stats.Messages, m => m.Message.Contains("refORGFEHLT", StringComparison.Ordinal));
        Assert.Contains("ausserhalb der Datei", hinweis.Message, StringComparison.Ordinal);
        Assert.Contains("Datenlieferant", hinweis.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Eine_Haltung_ohne_TID_steht_als_Warnung_im_Importbericht_und_bleibt_uebersprungen()
    {
        // Seit 01.10.2026 (zweite Runde): Der Leser ueberspringt sie wie bisher, aber nicht mehr still.
        var (projekt, stats) = Importiere("sia405-referenz.xtf");

        var meldung = Assert.Single(stats.Messages, m => m.Message.Contains("ohne-tid", StringComparison.Ordinal));
        Assert.Equal("Warn", meldung.Level);
        Assert.Equal("XTF405", meldung.Context);
        Assert.Equal("Haltung \"ohne-tid\" ohne TID nicht übernommen: Ohne Objektkennung lässt sie sich nicht eindeutig zuordnen.",
            meldung.Message);
        Assert.DoesNotContain(projekt.Data, r => r.GetFieldValue(FieldKeys.HoldingName) == "ohne-tid");
    }

    [Fact]
    public void Vollstaendige_Bezuege_erzeugen_keine_Meldung()
    {
        var (_, stats) = Importiere("sia405-bezuege.xtf");

        Assert.DoesNotContain(stats.Messages, m => m.Level == "Warn");
        Assert.DoesNotContain(stats.Messages, m => m.Message.Contains("ausserhalb der Datei", StringComparison.Ordinal));
    }

    private (Project Projekt, ImportStats Stats) Importiere(string datei)
    {
        Directory.CreateDirectory(_dir);
        var pfad = Path.Combine(_dir, datei);
        File.Copy(TestRepoPaths.RepoFile("tests", "Fixtures", "XtfReferenz", datei), pfad);

        var projekt = new Project { Name = "Test" };
        var stats = new LegacyXtfImportService().ImportXtfFiles(new[] { pfad }, projekt);
        return (projekt, stats);
    }
}
