using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import.Common;
using AuswertungPro.Next.Infrastructure.Import.Xtf;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

/// <summary>
/// Zwei SIA405-Haltungen mit gleicher Bezeichnung, aber verschiedener TID, sind zwei
/// Katasterobjekte. Bis 30.09.2026 landeten beide still im selben Projektdatensatz: Die
/// zweite ueberschrieb Objekt_ID, DN, Material usw., andere Felder blieben von der ersten
/// (Befund 1 aus AP08). Regel seither: Die erste Haltung in Dateireihenfolge wird wie
/// bisher uebernommen, jede weitere mit gleich normalisierter Bezeichnung gar nicht, und
/// der Importbericht nennt sie mit beiden TIDs.
/// </summary>
public sealed class XtfDoppelteHaltungsbezeichnungTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "xtf_doppelt_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); }
        catch (IOException) { /* Aufraeumen darf den Test nicht faellen. */ }
    }

    [Fact]
    public void Eine_zweite_Haltung_mit_gleicher_Bezeichnung_vermischt_nichts()
    {
        var (projekt, _) = Importiere(ZweiHaltungen("100-101", "100 - 101"));

        var haltung = Assert.Single(projekt.Data);
        Assert.Equal("100-101", haltung.GetFieldValue(FieldKeys.HoldingName));
        Assert.Equal("refERSTE", haltung.GetFieldValue(FieldKeys.CadastreObjectId));
        Assert.Equal("600", haltung.GetFieldValue(FieldKeys.NominalDiameterMm));
        Assert.Equal("45.30", haltung.GetFieldValue(FieldKeys.HoldingLengthMeters));
        Assert.Equal("Normalbeton", haltung.GetFieldValue(FieldKeys.PipeMaterial));
        // Nur die zweite Haltung traegt eine Lagebestimmung. Sie darf nicht in den
        // Datensatz der ersten wandern.
        Assert.Equal("", haltung.GetFieldValue(FieldKeys.PositionAccuracy));
    }

    [Fact]
    public void Der_Importbericht_nennt_die_weggelassene_Haltung_mit_beiden_TIDs()
    {
        var (_, stats) = Importiere(ZweiHaltungen("100-101", "100 - 101"));

        var meldung = Assert.Single(stats.Messages, m => m.Message.Contains("kommt zweimal vor", StringComparison.Ordinal));
        Assert.Equal("Warn", meldung.Level);
        Assert.Contains("'100-101'", meldung.Message, StringComparison.Ordinal);
        Assert.Contains("TID refERSTE", meldung.Message, StringComparison.Ordinal);
        Assert.Contains("TID refZWEITE", meldung.Message, StringComparison.Ordinal);
        Assert.Contains("nur die erste übernommen", meldung.Message, StringComparison.Ordinal);
        Assert.Contains(stats.Messages, m => m.Message == "Importiert 1 Haltungen aus doppelt.xtf");
    }

    [Fact]
    public void Verschiedene_Bezeichnungen_bleiben_zwei_Haltungen_ohne_Meldung()
    {
        var (projekt, stats) = Importiere(ZweiHaltungen("100-101", "101-102"));

        Assert.Equal(2, projekt.Data.Count);
        Assert.Equal("refZWEITE", projekt.Data[1].GetFieldValue(FieldKeys.CadastreObjectId));
        Assert.DoesNotContain(stats.Messages, m => m.Message.Contains("kommt zweimal vor", StringComparison.Ordinal));
    }

    private (Project Projekt, ImportStats Stats) Importiere(string xtf)
    {
        Directory.CreateDirectory(_dir);
        var pfad = Path.Combine(_dir, "doppelt.xtf");
        File.WriteAllText(pfad, xtf);

        var projekt = new Project { Name = "Test" };
        var stats = new LegacyXtfImportService().ImportXtfFiles(new[] { pfad }, projekt);
        Assert.True(stats.Errors == 0, string.Join(" | ", stats.Messages.Select(m => m.Message)));
        return (projekt, stats);
    }

    private static string ZweiHaltungen(string ersteBezeichnung, string zweiteBezeichnung) => $"""
        <?xml version="1.0" encoding="utf-8"?>
        <TRANSFER xmlns="http://www.interlis.ch/INTERLIS2.3">
          <HEADERSECTION VERSION="2.3" SENDER="Test">
            <MODELS>
              <MODEL NAME="SIA405_ABWASSER_2020_LV95" />
            </MODELS>
          </HEADERSECTION>
          <DATASECTION>
            <SIA405_ABWASSER_2020_LV95.SIA405_Abwasser BID="refB1">
              <SIA405_ABWASSER_2020_LV95.SIA405_Abwasser.Haltung TID="refERSTE">
                <Bezeichnung>{ersteBezeichnung}</Bezeichnung>
                <LaengeEffektiv>45.30</LaengeEffektiv>
                <Lichte_Hoehe>600</Lichte_Hoehe>
                <Material>Beton_Normalbeton</Material>
              </SIA405_ABWASSER_2020_LV95.SIA405_Abwasser.Haltung>
              <SIA405_ABWASSER_2020_LV95.SIA405_Abwasser.Haltung TID="refZWEITE">
                <Bezeichnung>{zweiteBezeichnung}</Bezeichnung>
                <LaengeEffektiv>12.00</LaengeEffektiv>
                <Lichte_Hoehe>700</Lichte_Hoehe>
                <Material>Steinzeug</Material>
                <Lagebestimmung>genau</Lagebestimmung>
              </SIA405_ABWASSER_2020_LV95.SIA405_Abwasser.Haltung>
            </SIA405_ABWASSER_2020_LV95.SIA405_Abwasser>
          </DATASECTION>
        </TRANSFER>
        """;
}
