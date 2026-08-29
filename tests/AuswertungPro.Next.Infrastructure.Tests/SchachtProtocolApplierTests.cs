using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import.Pdf;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class SchachtProtocolApplierTests
{
    [Fact]
    public void Apply_SetztFelderPdfPfadUndProtokoll()
    {
        var parsed = new LegacyPdfImportService.ParsedSchachtFields(
            "74467", "02.10.2025", "Kontrollschacht",
            "Rund", "1000 mm", "2.35", "BAC", null, "offen", null);
        var damages = new[] { ("Schachtdeckel", "gerissen"), ("Konus", "Riss") };
        var record = new SchachtRecord();

        var imported = SchachtProtocolApplier.Apply(record, "74467", parsed, damages, "C:/x/quelle.pdf");

        Assert.Equal("74467", record.GetFieldValue("Schachtnummer"));
        Assert.Equal("Kontrollschacht", record.GetFieldValue("Funktion"));
        Assert.Equal("Rund", record.GetFieldValue("Schachtform"));
        Assert.Equal("1000 mm", record.GetFieldValue("Dimension"));
        Assert.Equal("2.35", record.GetFieldValue("Schachttiefe"));
        Assert.Equal("C:/x/quelle.pdf", record.GetFieldValue("PDF_Path"));
        Assert.NotNull(record.Protocol);
        Assert.Equal(2, record.Protocol!.Original.Entries.Count);
        Assert.Equal("Schachtdeckel", record.Protocol!.Original.Entries[0].Code);
        Assert.Contains("Schachtnummer", imported);
        Assert.Contains("Schachtform", imported);
        Assert.Contains("Dimension", imported);
        Assert.Contains("Schachttiefe", imported);
        Assert.Contains("Protokoll (2 Beobachtungen)", imported);
    }

    [Fact]
    public void Apply_OhneNeuaufbau_LaesstAltenWertStehen()
    {
        var record = BestehenderSchacht();
        var leeresProtokoll = new LegacyPdfImportService.ParsedSchachtFields(
            "74467", null, null, null, null, null, null, null, null, null);

        SchachtProtocolApplier.Apply(
            record,
            "74467",
            leeresProtokoll,
            Array.Empty<(string, string)>(),
            "C:/x/neu.pdf");

        Assert.Equal("Kontrollschacht", record.GetFieldValue("Funktion"));
        Assert.Equal("alter Hinweis", record.GetFieldValue("Bemerkungen"));
        Assert.NotNull(record.Protocol);
        Assert.Single(record.Protocol!.Current.Entries);
    }

    [Fact]
    public void Apply_MitNeuaufbau_LeertFelderDieImProtokollFehlen()
    {
        var record = BestehenderSchacht();
        var geaendertesProtokoll = new LegacyPdfImportService.ParsedSchachtFields(
            "74467", null, "Absturzschacht", null, null, null, null, null, null, null);

        SchachtProtocolApplier.Apply(
            record,
            "74467",
            geaendertesProtokoll,
            new[] { ("Konus", "Riss") },
            "C:/x/neu.pdf",
            rebuildFromProtocol: true);

        Assert.Equal("Absturzschacht", record.GetFieldValue("Funktion"));
        Assert.Equal("", record.GetFieldValue("Bemerkungen"));
        Assert.Equal("", record.GetFieldValue("Schachtform"));
        Assert.Equal("C:/x/neu.pdf", record.GetFieldValue("PDF_Path"));
        Assert.Single(record.Protocol!.Current.Entries);
        Assert.Equal("Konus", record.Protocol!.Current.Entries[0].Code);
    }

    [Fact]
    public void Apply_MitNeuaufbau_EntferntAlteBeobachtungenAuchOhneNeueSchaeden()
    {
        var record = BestehenderSchacht();
        var leeresProtokoll = new LegacyPdfImportService.ParsedSchachtFields(
            "74467", null, "Kontrollschacht", null, null, null, null, null, null, null);

        SchachtProtocolApplier.Apply(
            record,
            "74467",
            leeresProtokoll,
            Array.Empty<(string, string)>(),
            "C:/x/neu.pdf",
            rebuildFromProtocol: true);

        Assert.NotNull(record.Protocol);
        Assert.Empty(record.Protocol!.Current.Entries);
        Assert.Empty(record.Protocol!.Original.Entries);
    }

    [Fact]
    public void Apply_MitNeuaufbau_BehaeltDenWegZurDatei()
    {
        var record = BestehenderSchacht();
        record.SetFieldValue("Link", @"C:\Projekt\Schaechte\74467\protokoll.pdf");
        var ohneLink = new LegacyPdfImportService.ParsedSchachtFields(
            "74467", null, "Kontrollschacht", null, null, null, null, null, null, null);

        SchachtProtocolApplier.Apply(
            record,
            "74467",
            ohneLink,
            Array.Empty<(string, string)>(),
            "C:/x/neu.pdf",
            rebuildFromProtocol: true);

        Assert.Equal(@"C:\Projekt\Schaechte\74467\protokoll.pdf", record.GetFieldValue("Link"));
    }

    [Fact]
    public void Apply_MitNeuaufbau_ErfindetKeineLeerenZusatzspalten()
    {
        var record = BestehenderSchacht();
        var leeresProtokoll = new LegacyPdfImportService.ParsedSchachtFields(
            "74467", null, "Kontrollschacht", null, null, null, null, null, null, null);

        SchachtProtocolApplier.Apply(
            record,
            "74467",
            leeresProtokoll,
            Array.Empty<(string, string)>(),
            "C:/x/neu.pdf",
            rebuildFromProtocol: true);

        Assert.DoesNotContain("Primaere Schaeden", record.Fields.Keys);
        Assert.DoesNotContain("Status offen/abgeschlossen", record.Fields.Keys);
    }

    [Fact]
    public void Apply_NormalerReimport_BehaeltOriginalUndArchiviertArbeitsstand()
    {
        var record = BestehenderSchacht();
        var geaendert = new LegacyPdfImportService.ParsedSchachtFields(
            "74467", null, "Kontrollschacht", null, null, null, null, null, null, null);

        SchachtProtocolApplier.Apply(
            record,
            "74467",
            geaendert,
            new[] { ("Konus", "Riss") },
            "C:/x/neu.pdf");

        Assert.Equal("Schachtdeckel", Assert.Single(record.Protocol!.Original.Entries).Code);
        Assert.Equal("Schachtdeckel", Assert.Single(record.Protocol.History).Entries[0].Code);
        Assert.Equal("Konus", Assert.Single(record.Protocol.Current.Entries).Code);
    }

    [Fact]
    public void Apply_UeberschreibtKeinAusdruecklichHandgesetztesSchachtfeld()
    {
        var record = BestehenderSchacht();
        record.SetFieldValue("Funktion", "Von Hand geprueft", FieldSource.Manual, userEdited: true);
        var geaendert = new LegacyPdfImportService.ParsedSchachtFields(
            "74467", null, "Absturzschacht", null, null, null, null, null, null, null);

        SchachtProtocolApplier.Apply(
            record,
            "74467",
            geaendert,
            Array.Empty<(string, string)>(),
            "C:/x/neu.pdf");

        Assert.Equal("Von Hand geprueft", record.GetFieldValue("Funktion"));
    }

    /// <summary>Ein Schacht, der bereits aus einem frueheren Protokoll aufgebaut wurde.</summary>
    private static SchachtRecord BestehenderSchacht()
    {
        var record = new SchachtRecord();
        var vorher = new LegacyPdfImportService.ParsedSchachtFields(
            "74467", "02.10.2025", "Kontrollschacht",
            "Rund", "1000 mm", "2.35", null, "alter Hinweis", null, null);
        SchachtProtocolApplier.Apply(
            record,
            "74467",
            vorher,
            new[] { ("Schachtdeckel", "gerissen") },
            "C:/x/alt.pdf");
        return record;
    }

    [Fact]
    public void Apply_MitNeuaufbau_LaesstEinHandgeaendertesFeldStehen()
    {
        // "Protokoll neu aufbauen" leert Felder, zu denen das PDF nichts liefert.
        // Ein von Hand gesetzter Wert darf davon nicht betroffen sein.
        var record = new SchachtRecord();
        record.SetFieldValue("Bemerkungen", "Deckel sitzt schief", FieldSource.Manual, userEdited: true);

        var parsed = new LegacyPdfImportService.ParsedSchachtFields(
            "74467", "02.10.2025", "Kontrollschacht",
            "Rund", "1000 mm", "2.35", "BAC", null, "offen", null);

        SchachtProtocolApplier.Apply(
            record,
            "74467",
            parsed,
            Array.Empty<(string, string)>(),
            "C:/x/quelle.pdf",
            rebuildFromProtocol: true);

        Assert.Equal("Deckel sitzt schief", record.GetFieldValue("Bemerkungen"));
        Assert.True(record.IsUserEdited("Bemerkungen"));
    }
}
