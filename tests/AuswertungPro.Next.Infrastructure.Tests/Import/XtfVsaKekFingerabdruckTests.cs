using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import.Xtf;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

/// <summary>
/// Haelt den Importbeleg (<c>ImportFingerprint</c>) von VSA-KEK-Untersuchungen als festen Wert
/// fest (Wartbarkeitspaket AP08b, 30.09.2026, vor dem Umbau aufgenommen).
///
/// Der Beleg ist SHA-256 ueber die JSON-Form der gelesenen Untersuchung samt Kanal- und
/// Normschachtschaeden. Er ist in Projekten gespeichert: Ein Wiederholungsimport erkennt daran
/// eine bereits abgelegte Schachtbegehung oder weitere Untersuchung. Aendern sich Name,
/// Reihenfolge oder Form einer gelesenen Eigenschaft, legt derselbe Import ein Duplikat an.
///
/// Die beiden Werte decken zusammen alle drei gelesenen Objektarten ab: Die Schachtbegehung
/// traegt Normschachtschaeden, die Gegenbefahrung traegt Kanalschaeden.
/// </summary>
public sealed class XtfVsaKekFingerabdruckTests : IDisposable
{
    private const string SchachtbegehungBeleg = "AFF08A6486677C920A397B2B91A40628DDF36D03D35D04B0330BAB9420214A22";
    private const string GegenbefahrungBeleg = "2B4BE85B0897D6BB264B0D538D270455BBA22E221561137E980F3C94C8CBA513";

    private readonly string _lauf = Path.Combine(Path.GetTempPath(), "xtf_fingerabdruck_" + Guid.NewGuid().ToString("N"));

    public XtfVsaKekFingerabdruckTests() => Directory.CreateDirectory(_lauf);

    public void Dispose()
    {
        try { Directory.Delete(_lauf, recursive: true); }
        catch (IOException) { /* Aufraeumen darf den Test nicht faellen. */ }
    }

    [Fact]
    public void Schachtbegehung_behaelt_ihren_gespeicherten_Importbeleg()
    {
        var projekt = Importiere();

        var schacht = Assert.Single(projekt.SchaechteData, s => s.GetFieldValue("Schachtnummer") == "300");
        Assert.NotNull(schacht.Protocol);
        Assert.Equal(SchachtbegehungBeleg, schacht.Protocol!.Original?.ImportFingerprint);
        Assert.Equal(SchachtbegehungBeleg, schacht.Protocol.Current?.ImportFingerprint);
    }

    [Fact]
    public void Weitere_Untersuchung_behaelt_ihren_gespeicherten_Importbeleg()
    {
        var projekt = Importiere();

        var haltung = Assert.Single(projekt.Data, h => h.GetFieldValue("Haltungsname") == "200-201");
        var fassung = Assert.Single(haltung.Protocol!.History);
        Assert.Equal(GegenbefahrungBeleg, fassung.ImportFingerprint);
    }

    private Project Importiere()
    {
        var ziel = Path.Combine(_lauf, "vsakek-referenz.xtf");
        File.Copy(TestRepoPaths.RepoFile("tests", "Fixtures", "XtfReferenz", "vsakek-referenz.xtf"), ziel);

        var projekt = new Project();
        new LegacyXtfImportService().ImportXtfFiles(new[] { ziel }, projekt);
        return projekt;
    }
}
