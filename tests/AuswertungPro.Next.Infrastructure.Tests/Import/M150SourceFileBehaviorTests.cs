using System.Text;
using System.Xml.Linq;
using AuswertungPro.Next.Infrastructure.Import.Xtf;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

/// <summary>
/// Schuetzt das bestehende M150-Verhalten beim Lesen von XML- und Textdateien.
/// </summary>
public sealed class M150SourceFileBehaviorTests
{
    [Fact]
    public void ParseM150File_BeschaedigtesXml_WirdAlsUtf8TextAusgewertet()
    {
        var path = CreateTempFile("M150 ungueltig < Haltung 80638-80631 am 15.07.2026");

        try
        {
            var records = M150MdbImportHelper.ParseM150File(path, out var warnings);

            var record = Assert.Single(records);
            Assert.Equal("80638-80631", record.GetFieldValue("Haltungsname"));
            Assert.Equal("15.07.2026", record.GetFieldValue("Datum_Jahr"));
            Assert.Contains(
                warnings,
                warning => warning.StartsWith(
                    "M150 XML konnte nicht direkt gelesen werden:",
                    StringComparison.Ordinal));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void GetM150XmlNodeCounts_ZaehltHgUndHiOhneNamespaceAbhaengigkeit()
    {
        var path = CreateTempFile(
            """
            <M150 xmlns="urn:test">
              <HG><HI /></HG>
              <HG><HI /><HI /></HG>
            </M150>
            """);

        try
        {
            var counts = M150MdbImportHelper.GetM150XmlNodeCounts(path);

            Assert.Equal(2, counts.HgCount);
            Assert.Equal(3, counts.HiCount);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ParseM150File_LiestTextNurWennXmlKeineHaltungEnthaelt()
    {
        var reader = new RecordingSourceReader(
            XDocument.Parse("<M150><Info>ohne Haltung</Info></M150>"),
            "Haltung 80638-80631 am 15.07.2026");

        var records = M150MdbImportHelper.ParseM150File(
            "wird-nicht-geoeffnet.m150",
            reader,
            out var warnings);

        Assert.Single(records);
        Assert.Empty(warnings);
        Assert.Equal(1, reader.XmlReadCount);
        Assert.Equal(1, reader.TextReadCount);
    }

    [Fact]
    public void M150XmlTextFileReader_LiestXmlUndUtf8Text()
    {
        var path = CreateTempFile("<M150><Bemerkung>Grösse</Bemerkung></M150>");

        try
        {
            var reader = new M150XmlTextFileReader();

            var document = reader.LoadXml(path);
            var text = reader.ReadUtf8Text(path);

            Assert.Equal("Grösse", document.Root?.Element("Bemerkung")?.Value);
            Assert.Contains("Grösse", text, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string CreateTempFile(string content)
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"sewerstudio-m150-source-{Guid.NewGuid():N}.m150");
        File.WriteAllText(path, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return path;
    }

    private sealed class RecordingSourceReader(XDocument xml, string text)
        : IM150SourceFileReader
    {
        public int XmlReadCount { get; private set; }

        public int TextReadCount { get; private set; }

        public XDocument LoadXml(string path)
        {
            XmlReadCount++;
            return xml;
        }

        public string ReadUtf8Text(string path)
        {
            TextReadCount++;
            return text;
        }
    }
}
