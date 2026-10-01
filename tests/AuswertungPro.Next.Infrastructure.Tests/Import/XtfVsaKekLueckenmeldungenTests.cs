using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import.Common;
using AuswertungPro.Next.Infrastructure.Import.Xtf;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

/// <summary>
/// Was der VSA-KEK-Import keiner Haltung und keinem Schacht zuordnen kann, steht im
/// Importbericht (Auftrag Pascal 01.10.2026, Befunde 5 und 6 aus AP08). Bis dahin zaehlte
/// eine Untersuchung ohne Bezeichnung nur in «N Untersuchungen gelesen», und verwaiste
/// Schaeden und Dateien fielen still weg. Uebernommen wird weiterhin nichts davon.
/// </summary>
public sealed class XtfVsaKekLueckenmeldungenTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "xtf_luecken_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); }
        catch (IOException) { /* Aufraeumen darf den Test nicht faellen. */ }
    }

    [Fact]
    public void Eine_Untersuchung_ohne_Bezeichnung_steht_mit_TID_im_Importbericht()
    {
        var (projekt, stats) = ImportiereReferenz();

        var meldung = Assert.Single(stats.Messages, m => m.Message.Contains("TID refUNTERS8", StringComparison.Ordinal));
        Assert.Equal("Warn", meldung.Level);
        Assert.Equal("XTF", meldung.Context);
        Assert.Contains("Untersuchung ohne Bezeichnung", meldung.Message, StringComparison.Ordinal);
        Assert.Contains("nicht übernommen", meldung.Message, StringComparison.Ordinal);
        // Nur gemeldet: kein Datensatz und kein zusaetzlicher «ungeklaert»-Fall.
        Assert.DoesNotContain(projekt.Data, r => r.XtfHerkunft?.UntersuchungTid == "refUNTERS8");
        Assert.Equal(3, stats.Uncertain);
    }

    private (Project Projekt, ImportStats Stats) ImportiereReferenz()
    {
        Directory.CreateDirectory(_dir);
        var pfad = Path.Combine(_dir, "vsakek-referenz.xtf");
        File.Copy(TestRepoPaths.RepoFile("tests", "Fixtures", "XtfReferenz", "vsakek-referenz.xtf"), pfad);
        return Importiere(pfad);
    }

    private static (Project Projekt, ImportStats Stats) Importiere(string pfad)
    {
        var projekt = new Project { Name = "Test" };
        var stats = new LegacyXtfImportService().ImportXtfFiles(new[] { pfad }, projekt);
        Assert.True(stats.Errors == 0, string.Join(" | ", stats.Messages.Select(m => m.Message)));
        return (projekt, stats);
    }
}
