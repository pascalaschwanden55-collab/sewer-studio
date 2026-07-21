using System.IO;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Infrastructure.Media;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.Tests;

public sealed class MediaSearchApplyControllerTests
{
    [Fact]
    public void Apply_verarbeitet_nur_markierte_Found_und_Ambiguous_Zeilen()
    {
        var found = Record("Found");
        var ambiguous = Record("Ambiguous");
        var notFound = Record("NotFound");
        var linked = Record("Linked");
        var uncheckedRecord = Record("Unchecked");
        var rows = new[]
        {
            Row(found, true, MediaMatchStatus.Found, @"C:\Medien\found.mp4", MediaMatchStatus.Found, @"C:\Medien\found.pdf"),
            Row(ambiguous, true, MediaMatchStatus.Ambiguous, @"C:\Medien\ambiguous.mp4", MediaMatchStatus.Ambiguous, @"C:\Medien\ambiguous.pdf"),
            Row(notFound, true, MediaMatchStatus.NotFound, @"C:\Medien\missing.mp4", MediaMatchStatus.NotFound, @"C:\Medien\missing.pdf"),
            Row(linked, true, MediaMatchStatus.AlreadyLinked, @"C:\Medien\linked.mp4", MediaMatchStatus.AlreadyLinked, @"C:\Medien\linked.pdf"),
            Row(uncheckedRecord, false, MediaMatchStatus.Found, @"C:\Medien\unchecked.mp4", MediaMatchStatus.Found, @"C:\Medien\unchecked.pdf")
        };

        var result = MediaSearchApplyController.Apply(rows, projectFilePath: null);

        Assert.Equal(2, result.VideoCount);
        Assert.Equal(2, result.PdfCount);
        Assert.Equal(0, result.FotoCount);
        Assert.True(result.Applied);
        Assert.Equal(@"C:\Medien\found.mp4", found.GetFieldValue(FieldKeys.Link));
        Assert.Equal(@"C:\Medien\ambiguous.pdf", ambiguous.GetFieldValue(FieldKeys.PdfPath));
        Assert.Equal("", notFound.GetFieldValue(FieldKeys.Link));
        Assert.Equal("", linked.GetFieldValue(FieldKeys.PdfPath));
        Assert.Equal("", uncheckedRecord.GetFieldValue(FieldKeys.Link));
    }

    [Fact]
    public void Apply_speichert_interne_Videos_relativ_externe_absolut_und_PDF_unveraendert()
    {
        var projectRoot = @"C:\Projekte\Demo";
        var projectFile = Path.Combine(projectRoot, "Projektdateien", "projekt.json");
        var insideVideo = Path.Combine(projectRoot, "Videos", "innen.mp4");
        var outsideVideo = @"D:\Quelle\aussen.mp4";
        var insidePdf = Path.Combine(projectRoot, "PDF", "bericht.pdf");
        var inside = Record("Inside");
        var outside = Record("Outside");

        var result = MediaSearchApplyController.Apply(
            new[]
            {
                Row(inside, true, MediaMatchStatus.Found, insideVideo, MediaMatchStatus.Found, insidePdf),
                Row(outside, true, MediaMatchStatus.Found, outsideVideo)
            },
            projectFile);

        Assert.Equal(2, result.VideoCount);
        Assert.Equal(1, result.PdfCount);
        Assert.Equal("Videos/innen.mp4", inside.GetFieldValue(FieldKeys.Link));
        Assert.Equal(outsideVideo, outside.GetFieldValue(FieldKeys.Link));
        Assert.Equal(insidePdf, inside.GetFieldValue(FieldKeys.PdfPath));
    }

    [Fact]
    public void Apply_zaehlt_Video_und_PDF_auch_wenn_manuell_geschuetzte_Felder_unveraendert_bleiben()
    {
        var record = Record("Geschuetzt");
        record.SetFieldValue(FieldKeys.Link, "manuelles-video", FieldSource.Manual, userEdited: true);
        record.SetFieldValue(FieldKeys.PdfPath, "manuelles-pdf", FieldSource.Manual, userEdited: true);

        var result = MediaSearchApplyController.Apply(
            new[]
            {
                Row(
                    record,
                    true,
                    MediaMatchStatus.Found,
                    @"C:\Medien\neu.mp4",
                    MediaMatchStatus.Found,
                    @"C:\Medien\neu.pdf")
            },
            projectFilePath: null);

        Assert.Equal("manuelles-video", record.GetFieldValue(FieldKeys.Link));
        Assert.Equal("manuelles-pdf", record.GetFieldValue(FieldKeys.PdfPath));
        Assert.Equal(1, result.VideoCount);
        Assert.Equal(1, result.PdfCount);
        Assert.True(result.Applied);
    }

    [Fact]
    public void Apply_laesst_fruehere_Aenderungen_bestehen_wenn_eine_spaetere_Zeile_fehlschlaegt()
    {
        var first = Record("Erste");
        var invalid = Record("Defekt");
        invalid.FieldMeta = null!;

        Assert.Throws<NullReferenceException>(() => MediaSearchApplyController.Apply(
            new[]
            {
                Row(first, true, MediaMatchStatus.Found, @"C:\Medien\erste.mp4"),
                Row(invalid, true, MediaMatchStatus.Found, @"C:\Medien\defekt.mp4")
            },
            projectFilePath: null));

        Assert.Equal(@"C:\Medien\erste.mp4", first.GetFieldValue(FieldKeys.Link));
        Assert.Equal("", invalid.GetFieldValue(FieldKeys.Link));
    }

    [Fact]
    public void Apply_ordnen_Fotos_naechstem_aktiven_Eintrag_bis_eins_Meter_zu()
    {
        var first = Entry(10);
        var deleted = Entry(10.7, deleted: true);
        var second = Entry(20);
        var record = RecordWithProtocol("H-1", first, deleted, second);
        var commaPhoto = @"C:\Fotos\schaden_10,8m.jpg";
        var boundaryPhoto = @"C:\Fotos\schaden_21.0m.jpg";

        var result = MediaSearchApplyController.Apply(
            new[] { Row(record, true, fotoPaths: [commaPhoto, boundaryPhoto]) },
            projectFilePath: null);

        Assert.Equal(2, result.FotoCount);
        Assert.Equal(new[] { commaPhoto }, first.FotoPaths);
        Assert.Empty(deleted.FotoPaths);
        Assert.Equal(new[] { boundaryPhoto }, second.FotoPaths);
    }

    [Fact]
    public void Apply_ignoriert_Foto_Dubletten_ohne_Beachtung_der_Schreibweise()
    {
        var existingPath = @"C:\Fotos\Schaden_10m.jpg";
        var entry = Entry(10);
        entry.FotoPaths.Add(existingPath);
        var record = RecordWithProtocol("H-1", entry);

        var result = MediaSearchApplyController.Apply(
            new[] { Row(record, true, fotoPaths: [@"c:\fotos\SCHADEN_10M.JPG"]) },
            projectFilePath: null);

        Assert.Equal(0, result.FotoCount);
        Assert.False(result.Applied);
        Assert.Equal(new[] { existingPath }, entry.FotoPaths);
    }

    [Fact]
    public void Apply_verwendet_bei_fehlendem_Meter_Match_den_ersten_aktiven_Eintrag()
    {
        var first = Entry(5);
        var second = Entry(50);
        var record = RecordWithProtocol("H-1", first, second);
        var photo = @"C:\Fotos\schaden_100m.jpg";

        var result = MediaSearchApplyController.Apply(
            new[] { Row(record, true, fotoPaths: [photo]) },
            projectFilePath: null);

        Assert.Equal(1, result.FotoCount);
        Assert.Equal(new[] { photo }, first.FotoPaths);
        Assert.Empty(second.FotoPaths);
    }

    [Fact]
    public void Apply_legt_ohne_aktiven_Eintrag_Protokoll_Revisionen_und_Platzhalter_an()
    {
        var record = Record("H-42");
        var photo = @"C:\Fotos\aufnahme_3.5m.jpg";

        var result = MediaSearchApplyController.Apply(
            new[] { Row(record, true, fotoPaths: [photo]) },
            projectFilePath: null);

        Assert.Equal(1, result.FotoCount);
        Assert.True(result.Applied);
        Assert.NotNull(record.Protocol);
        Assert.Equal("H-42", record.Protocol.HaltungId);
        Assert.Equal("Medien-Import", record.Protocol.Original.Comment);
        Assert.Equal("Arbeitskopie", record.Protocol.Current.Comment);
        Assert.Empty(record.Protocol.Original.Entries);
        var placeholder = Assert.Single(record.Protocol.Current.Entries);
        Assert.Equal("", placeholder.Code);
        Assert.Equal("Foto (automatisch zugeordnet)", placeholder.Beschreibung);
        Assert.Equal(3.5, placeholder.MeterStart);
        Assert.Equal(ProtocolEntrySource.Imported, placeholder.Source);
        Assert.Equal(new[] { photo }, placeholder.FotoPaths);
    }

    [Fact]
    public void Apply_behandelt_nur_geloeschte_Eintraege_wie_keine_aktiven()
    {
        var deleted = Entry(7, deleted: true);
        var record = RecordWithProtocol("H-1", deleted);
        var photo = @"C:\Fotos\ohne_meter.jpg";

        var result = MediaSearchApplyController.Apply(
            new[] { Row(record, true, fotoPaths: [photo]) },
            projectFilePath: null);

        Assert.Equal(1, result.FotoCount);
        Assert.Empty(deleted.FotoPaths);
        Assert.Equal(2, record.Protocol!.Current.Entries.Count);
        Assert.Equal(new[] { photo }, record.Protocol.Current.Entries[1].FotoPaths);
    }

    [Fact]
    public void Apply_leere_oder_abgewaehlte_Zeilen_ergeben_keine_Aenderung()
    {
        var uncheckedRecord = Record("Unchecked");

        var empty = MediaSearchApplyController.Apply([], projectFilePath: null);
        var uncheckedResult = MediaSearchApplyController.Apply(
            new[]
            {
                Row(
                    uncheckedRecord,
                    false,
                    MediaMatchStatus.Found,
                    @"C:\Medien\video.mp4",
                    MediaMatchStatus.Found,
                    @"C:\Medien\bericht.pdf",
                    [@"C:\Fotos\foto_1m.jpg"])
            },
            projectFilePath: null);

        Assert.False(empty.Applied);
        Assert.Equal((0, 0, 0), (empty.VideoCount, empty.PdfCount, empty.FotoCount));
        Assert.False(uncheckedResult.Applied);
        Assert.Equal("", uncheckedRecord.GetFieldValue(FieldKeys.Link));
        Assert.Null(uncheckedRecord.Protocol);
    }

    private static MediaMatchRow Row(
        HaltungRecord record,
        bool apply,
        MediaMatchStatus videoStatus = MediaMatchStatus.NotFound,
        string? videoPath = null,
        MediaMatchStatus pdfStatus = MediaMatchStatus.NotFound,
        string? pdfPath = null,
        List<string>? fotoPaths = null)
    {
        fotoPaths ??= [];
        var match = new MediaMatch(
            record,
            record.GetFieldValue(FieldKeys.HoldingName),
            videoStatus,
            videoPath,
            null,
            pdfStatus,
            pdfPath,
            null,
            fotoPaths.Count > 0 ? MediaMatchStatus.Found : MediaMatchStatus.NotFound,
            fotoPaths,
            apply);
        return new MediaMatchRow(match)
        {
            Apply = apply,
            VideoPath = videoPath,
            PdfPath = pdfPath
        };
    }

    private static HaltungRecord Record(string name)
    {
        var record = new HaltungRecord();
        record.SetFieldValue(FieldKeys.HoldingName, name, FieldSource.Manual, userEdited: false);
        return record;
    }

    private static HaltungRecord RecordWithProtocol(string name, params ProtocolEntry[] entries)
    {
        var record = Record(name);
        record.Protocol = new ProtocolDocument
        {
            HaltungId = name,
            Original = new ProtocolRevision { Comment = "Original" },
            Current = new ProtocolRevision
            {
                Comment = "Arbeitskopie",
                Entries = entries.ToList()
            }
        };
        return record;
    }

    private static ProtocolEntry Entry(double meter, bool deleted = false)
        => new()
        {
            MeterStart = meter,
            IsDeleted = deleted
        };
}
