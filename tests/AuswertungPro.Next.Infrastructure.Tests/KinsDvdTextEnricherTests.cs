using System;
using System.IO;
using System.Linq;
using System.Text;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Infrastructure.Import.Kins;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// kiDVDaten.txt-Anreicherung NACH dem XTF-Import: Video-Timecodes je
/// Beobachtung (Meter-Match), inspizierte Laenge (das XTF liefert immer 0)
/// und Aufnahmedatum. Prioritaet: UserEdit &gt; XTF &gt; TXT — nur Leeres fuellen.
/// </summary>
public sealed class KinsDvdTextEnricherTests : IDisposable
{
    private readonly string _dir;

    public KinsDvdTextEnricherTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "KinsEnricherTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    private string SchreibeKiDvDaten(string inhalt)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var pfad = Path.Combine(_dir, "kiDVDaten.txt");
        File.WriteAllText(pfad, inhalt, Encoding.GetEncoding(1252));
        return pfad;
    }

    private static HaltungRecord ErzeugeXtfRecord(string name, string oben, string unten, string laenge = "0")
    {
        var record = new HaltungRecord();
        record.SetFieldValue("Haltungsname", name, FieldSource.Xtf, userEdited: false);
        record.SetFieldValue("Schacht_oben", oben, FieldSource.Xtf, userEdited: false);
        record.SetFieldValue("Schacht_unten", unten, FieldSource.Xtf, userEdited: false);
        record.SetFieldValue("Haltungslaenge_m", laenge, FieldSource.Xtf, userEdited: false);
        record.Protocol = new ProtocolDocument();
        return record;
    }

    private static ProtocolEntry ImportierterEintrag(double meter, string code)
        => new()
        {
            Source = ProtocolEntrySource.Imported,
            Code = code,
            MeterStart = meter,
            MeterEnd = meter
        };

    private const string StandardTxt =
        "Kanalisation 58951 -> 58950 B 600 @Datei=video.mp4\n" +
        "\t   0.0m Rohranfang  @Pos=0:00:00     \n" +
        "\t  20.5m Inkrustation an der Rohrverbindung 12 Uhr  @Pos=0:06:07     \n" +
        "\t  30.4m Rohrende  @Pos=0:08:51     \n";

    [Fact]
    public void Apply_SetztTimecodesPerMeterMatch_NurAufLeere()
    {
        var pfad = SchreibeKiDvDaten(StandardTxt);
        var project = new Project();
        var record = ErzeugeXtfRecord("58951-58950", "58951", "58950");
        var mitTimecode = ImportierterEintrag(0.0, "BCD");
        mitTimecode.Mpeg = "0:00:59"; // schon vom XTF gesetzt — nicht anfassen
        record.Protocol!.Current.Entries.Add(mitTimecode);
        record.Protocol.Current.Entries.Add(ImportierterEintrag(20.5, "BBBA"));
        project.Data.Add(record);

        var result = KinsDvdTextEnricher.Apply(project, pfad);

        var bbba = record.Protocol.Current.Entries.Single(e => e.Code == "BBBA");
        Assert.Equal("0:06:07", bbba.Mpeg);
        Assert.Equal(new TimeSpan(0, 6, 7), bbba.Zeit);
        Assert.Equal("0:00:59", mitTimecode.Mpeg); // unveraendert
        Assert.True(result.TimecodesGesetzt >= 1);
    }

    [Fact]
    public void Apply_SetztHaltungslaenge_WennXtfNullLiefert()
    {
        var pfad = SchreibeKiDvDaten(StandardTxt);
        var project = new Project();
        var record = ErzeugeXtfRecord("58951-58950", "58951", "58950", laenge: "0");
        project.Data.Add(record);

        KinsDvdTextEnricher.Apply(project, pfad);

        Assert.Equal("30.4", record.GetFieldValue("Haltungslaenge_m"));
    }

    [Fact]
    public void Apply_LaesstUserEditedLaengeUnveraendert()
    {
        var pfad = SchreibeKiDvDaten(StandardTxt);
        var project = new Project();
        var record = ErzeugeXtfRecord("58951-58950", "58951", "58950");
        record.SetFieldValue("Haltungslaenge_m", "42.0", FieldSource.Manual, userEdited: true);
        project.Data.Add(record);

        KinsDvdTextEnricher.Apply(project, pfad);

        Assert.Equal("42.0", record.GetFieldValue("Haltungslaenge_m"));
    }

    [Fact]
    public void Apply_MatchtUeberSchachtFelder_WennHaltungsnameAbweicht()
    {
        // Falls der Namens-Normalizer nichts tat, traegt der Record noch die
        // XTF-Bezeichnung ("10") — der Match laeuft dann ueber Schacht oben/unten.
        var pfad = SchreibeKiDvDaten(StandardTxt);
        var project = new Project();
        var record = ErzeugeXtfRecord("10", "58951", "58950");
        record.Protocol!.Current.Entries.Add(ImportierterEintrag(20.5, "BBBA"));
        project.Data.Add(record);

        var result = KinsDvdTextEnricher.Apply(project, pfad);

        Assert.Equal("0:06:07", record.Protocol.Current.Entries[0].Mpeg);
        Assert.Equal(1, result.TimecodesGesetzt);
    }

    [Fact]
    public void Apply_MehrereBeobachtungenAmGleichenMeter_InDateireihenfolge()
    {
        var txt =
            "Kanalisation 60650 -> 60651 PVC 150 @Datei=v.mp4\n" +
            "\t  19.6m Abbruch Inspektion  @Pos=0:10:44     \n" +
            "\t  19.6m Inspektion erfolgt von der Gegenseite  @Pos=0:10:56     \n";
        var pfad = SchreibeKiDvDaten(txt);
        var project = new Project();
        var record = ErzeugeXtfRecord("60650-60651", "60650", "60651");
        record.Protocol!.Current.Entries.Add(ImportierterEintrag(19.6, "BDCZ"));
        record.Protocol.Current.Entries.Add(ImportierterEintrag(19.6, "BDBF"));
        project.Data.Add(record);

        KinsDvdTextEnricher.Apply(project, pfad);

        Assert.Equal("0:10:44", record.Protocol.Current.Entries[0].Mpeg);
        Assert.Equal("0:10:56", record.Protocol.Current.Entries[1].Mpeg);
    }

    [Fact]
    public void Apply_UnbekannteHaltung_LiefertMeldungStattFehler()
    {
        var pfad = SchreibeKiDvDaten(StandardTxt);
        var project = new Project(); // keine Haltungen

        var result = KinsDvdTextEnricher.Apply(project, pfad);

        Assert.Equal(0, result.TimecodesGesetzt);
        Assert.Contains(result.Messages, m => m.Contains("58951-58950"));
    }

    [Fact]
    public void Apply_SetztDatumAusKiDvInfo_NurWennLeer()
    {
        var pfad = SchreibeKiDvDaten(StandardTxt);
        File.WriteAllText(Path.Combine(_dir, "kiDVinfo.txt"),
            "Medienname: 01-2026\n6460 Altdorf UR\n\nKanalisation\n\nAufnahmen: 24.06.26\n");
        var project = new Project();
        var record = ErzeugeXtfRecord("58951-58950", "58951", "58950");
        project.Data.Add(record);

        KinsDvdTextEnricher.Apply(project, pfad);

        Assert.Equal("24.06.2026", record.GetFieldValue("Datum_Jahr"));
    }
}
