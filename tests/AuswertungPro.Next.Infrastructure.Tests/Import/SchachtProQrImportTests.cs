using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import.SchachtPro;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

public sealed partial class SchachtProQrImportTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".png");
    public SchachtProQrImportTests() => File.WriteAllBytes(_path, [1]);
    public void Dispose() => File.Delete(_path);

    [Fact]
    public void GueltigerCode_UebernimmtFelderZustaendeAnschluesseOhneLeerzeile()
    {
        var project = new Project();
        var result = Service(Fixture).ImportImage(_path, project);
        Assert.True(result.Ok, result.ErrorMessage);
        Assert.Equal(1, result.Value!.Created);
        var shaft = Assert.Single(project.SchaechteData);
        Assert.Equal("QR-001", shaft.GetFieldValue("Schachtnummer"));
        Assert.Equal("2.68", shaft.GetFieldValue("Schachttiefe"));
        Assert.Equal("1000", shaft.GetFieldValue(FieldKeys.ShaftDimension1Mm));
        Assert.Equal("2658000.25", shaft.GetFieldValue("Koordinate_East"));
        Assert.Equal(2, shaft.Anschluesse!.Count);
        Assert.Equal("7", shaft.Anschluesse![1].Uhr);
        Assert.Contains(shaft.Protocol!.Current.Entries, e => e.Beschreibung.Contains("Wurzeln"));
        Assert.Contains("QR-Test", project.Metadata["SchachtPro.QR.SP-Test-001"]);
    }

    [Fact]
    public void Wiederimport_BehaeltHandwertGeloeschtenBefundUndIdentitaeten()
    {
        var project = new Project();
        var service = Service(Fixture);
        Assert.True(service.ImportImage(_path, project).Ok);
        var shaft = Assert.Single(project.SchaechteData);
        shaft.SetFieldValue("Schachttiefe", "3.10", FieldSource.Manual, userEdited: true);
        shaft.Protocol!.Current.Entries.Clear();
        var document = shaft.Protocol;
        Assert.True(service.ImportImage(_path, project).Ok);
        Assert.Single(project.SchaechteData);
        Assert.Equal("3.10", shaft.GetFieldValue("Schachttiefe"));
        Assert.Same(document, shaft.Protocol);
        Assert.Empty(shaft.Protocol.Current.Entries);
    }

    [Theory]
    [InlineData("crc")]
    [InlineData("prefix")]
    [InlineData("version")]
    [InlineData("schema")]
    [InlineData("shaft")]
    [InlineData("crs")]
    [InlineData("type")]
    [InlineData("duplicate")]
    [InlineData("oversize")]
    [InlineData("base64")]
    [InlineData("zip")]
    [InlineData("null")]
    [InlineData("truncated")]
    public void BeschaedigterCode_VeraendertProjektNicht(string failure)
    {
        var node = JsonNode.Parse(Json)!;
        var text = Fixture;
        switch (failure)
        {
            case "crc": text = Fixture[..^8] + "00000000"; break;
            case "prefix": text = Fixture.Replace("SPQR1", "SPQR2"); break;
            case "version": node["version"] = 2; text = Encode(node.ToJsonString()); break;
            case "schema": node["schema"] = "fremd"; text = Encode(node.ToJsonString()); break;
            case "shaft": node["protocol"]!["schachtNr"] = " "; text = Encode(node.ToJsonString()); break;
            case "crs": node["protocol"]!["coordinates"]!["crs"] = "EPSG:4326"; text = Encode(node.ToJsonString()); break;
            case "type": node["protocol"]!["masterData"]!["depth"] = 2.68; text = Encode(node.ToJsonString()); break;
            case "duplicate": text = Encode(Json.Replace("\"version\":1", "\"version\":1,\"version\":1")); break;
            case "oversize": text = Encode(new string('x', 65537)); break;
            case "base64": text = "SPQR1:?:00000000"; break;
            case "zip": text = Wrap([1, 2, 3]); break;
            case "null": text = Encode("null"); break;
            case "truncated":
                var base64 = Fixture.Split(':')[1].Replace('-', '+').Replace('_', '/');
                var bytes = Convert.FromBase64String(base64.PadRight((base64.Length + 3) / 4 * 4, '='));
                text = Wrap(bytes[..^4]); break;
        }
        var project = new Project();
        var modified = project.ModifiedAtUtc;
        var dirty = project.Dirty;
        Assert.False(Service(text).ImportImage(_path, project).Ok);
        Assert.Empty(project.SchaechteData);
        Assert.DoesNotContain(project.Metadata.Keys, k => k.StartsWith("SchachtPro.QR."));
        Assert.Equal(modified, project.ModifiedAtUtc);
        Assert.Equal(dirty, project.Dirty);
    }

    [Fact]
    public void MehrereUnterschiedlicheCodes_WerdenNichtWillkuerlichZugeordnet()
    {
        var project = new Project();
        Assert.False(Service(Fixture, Fixture + "x").ImportImage(_path, project).Ok);
        Assert.Empty(project.SchaechteData);
    }

    [Fact]
    public void KeinQr_UndFehlendeDatei_WerdenGemeldet()
    {
        Assert.False(Service("https://example.invalid").ImportImage(_path, new Project()).Ok);
        Assert.False(Service(Fixture).ImportImage(_path + ".missing", new Project()).Ok);
    }

    [Fact]
    public void MehrdeutigeSchachtnummer_SperrtUebernahme()
    {
        var project = new Project();
        for (var i = 0; i < 2; i++)
        {
            var shaft = new SchachtRecord();
            shaft.SetFieldValue("Schachtnummer", "QR-001", FieldSource.Manual, true);
            project.SchaechteData.Add(shaft);
        }
        Assert.False(Service(Fixture).ImportImage(_path, project).Ok);
        Assert.All(project.SchaechteData, s => Assert.Equal("", s.GetFieldValue("Schachttiefe")));
    }

    [Fact]
    public void Abbruch_VorLesen_BleibtAbbruch()
    {
        var context = new ImportRunContext(new CancellationToken(true), null, new ImportRunLog());
        Assert.Throws<OperationCanceledException>(() => Service(Fixture).ImportImage(_path, new Project(), context));
    }

    private static SchachtProQrImportService Service(params string[] texts) => new(new Reader(texts));
    private sealed class Reader(string[] texts) : IQrImageReader
    {
        public IReadOnlyList<string> Read(string path, CancellationToken ct) => texts;
    }
    private static string Encode(string text)
    {
        using var output = new MemoryStream();
        using (var stream = new ZLibStream(output, CompressionLevel.SmallestSize, true))
            stream.Write(Encoding.UTF8.GetBytes(text));
        return Wrap(output.ToArray());
    }
    private static string Wrap(byte[] bytes)
    {
        uint crc = 0xffffffff;
        foreach (var b in bytes)
        {
            crc ^= b;
            for (var i = 0; i < 8; i++) crc = (crc >> 1) ^ ((crc & 1) == 0 ? 0u : 0xedb88320u);
        }
        return "SPQR1:" + Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_') + ":" + (~crc).ToString("X8");
    }
}
