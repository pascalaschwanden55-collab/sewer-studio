using System;
using System.IO;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import.Xtf;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

/// <summary>
/// Sichert, dass die Inspektionsrichtung aus einer VSA-KEK-XTF (IKAS) ankommt.
///
/// Hintergrund: Der Parser las den Tag &lt;Fliessrichtung&gt; nicht, und ein Kommentar im Code
/// behauptete, XTF-Daten enthielten ihn gar nicht. Echte IKAS-Exporte enthalten ihn sehr wohl —
/// aufgefallen ist es erst an einem Projekt mit 35 leeren Feldern. Es gab keinen Test dafuer.
/// </summary>
public sealed class XtfInspectionDirectionImportTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "xtf_dir_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); }
        catch (IOException) { /* Aufraeumen darf den Test nicht faellen. */ }
    }

    [Theory]
    [InlineData("in_Fliessrichtung", "In Fliessrichtung")]
    [InlineData("gegen_Fliessrichtung", "Gegen Fliessrichtung")]
    public void Inspection_direction_from_vsa_kek_lands_in_the_field(string rohwert, string erwartet)
    {
        var project = ImportSingleHolding(rohwert);

        var record = Assert.Single(project.Data);
        Assert.Equal(erwartet, record.GetFieldValue("Inspektionsrichtung"));
    }

    [Fact]
    public void Imported_direction_is_marked_as_coming_from_xtf()
    {
        var project = ImportSingleHolding("gegen_Fliessrichtung");

        var meta = project.Data[0].FieldMeta["Inspektionsrichtung"];
        Assert.Equal(FieldSource.Xtf, meta.Source);
        Assert.False(meta.UserEdited);
    }

    [Fact]
    public void Missing_direction_tag_leaves_the_field_empty_instead_of_guessing()
    {
        var project = ImportSingleHolding(richtung: null);

        Assert.Equal("", project.Data[0].GetFieldValue("Inspektionsrichtung"));
    }

    [Fact]
    public void An_unknown_direction_value_is_not_invented_into_the_catalog()
    {
        // Fremde Schreibweise: lieber leer lassen als einen Wert erfinden, den die Auswahlliste
        // nicht kennt — genau daran krankt das Rohrmaterial-Feld.
        var project = ImportSingleHolding("voellig_unbekannt");

        Assert.Equal("", project.Data[0].GetFieldValue("Inspektionsrichtung"));
    }

    private Project ImportSingleHolding(string? richtung)
    {
        Directory.CreateDirectory(_dir);
        var path = Path.Combine(_dir, "ikas_vsa_kek.xtf");
        File.WriteAllText(path, BuildVsaKekXtf(richtung));

        var project = new Project { Name = "Test" };
        var service = new LegacyXtfImportService();
        var stats = service.ImportXtfFiles(new[] { path }, project);

        Assert.True(stats.Errors == 0, string.Join(" | ", stats.Messages));
        return project;
    }

    /// <summary>Aufbau wie ein echter IKAS-Export: Knotennamen mit Modellpraefix, Header mit VSA_KEK.</summary>
    private static string BuildVsaKekXtf(string? richtung)
    {
        var richtungTag = richtung is null ? "" : $"<Fliessrichtung>{richtung}</Fliessrichtung>";
        return $"""
        <?xml version="1.0" encoding="utf-8"?>
        <TRANSFER xmlns="http://www.interlis.ch/INTERLIS2.3">
          <HEADERSECTION SENDER="IKAS evolution Office" VERSION="2.3">
            <MODELS>
              <MODEL NAME="VSA_KEK_2020_LV95" VERSION="03.05.2021" URI="http://www.vsa.ch/models" />
            </MODELS>
          </HEADERSECTION>
          <DATASECTION>
            <VSA_KEK_2020_LV95.KEK BID="chB0000000000001">
              <VSA_KEK_2020_LV95.KEK.Untersuchung TID="ch100000002B58D1">
                <Bezeichnung>77467-77463</Bezeichnung>
                <Ausfuehrender>Bruno Balta</Ausfuehrender>
                <Zeitpunkt>20260623</Zeitpunkt>
                <Erfassungsart>Kanalfernsehen</Erfassungsart>
                <Grund>Zustandskontrolle</Grund>
                <Inspizierte_Laenge>3.60</Inspizierte_Laenge>
                <vonPunktBezeichnung>77467</vonPunktBezeichnung>
                <bisPunktBezeichnung>77463</bisPunktBezeichnung>
                {richtungTag}
              </VSA_KEK_2020_LV95.KEK.Untersuchung>
            </VSA_KEK_2020_LV95.KEK>
          </DATASECTION>
        </TRANSFER>
        """;
    }
}
