using System.Xml.Linq;
using AuswertungPro.Next.Infrastructure.Import.Ibak;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

public sealed class XtfStammdatenExtractorTests
{
    [Fact]
    public void ExtractFromFile_LiestSia405StammdatenMitNamespace()
    {
        using var temp = new TempDirectory();
        var path = temp.WriteXtf(
            "stammdaten.xtf",
            BuildXtf(
                material: "R",
                laenge: "12,5",
                hoehe: "299,6",
                breite: "400,4"));

        var data = Assert.Single(XtfStammdatenExtractor.ExtractFromFile(path)).Value;

        Assert.Equal("80638-80631", data.Haltungsname);
        Assert.Equal("Beton", data.Material);
        Assert.Equal(12.5, data.Laenge_m);
        Assert.Equal(300, data.DN_mm);
        Assert.Equal(400, data.Profilbreite_mm);
        Assert.Equal("Mischabwasser", data.Nutzungsart);
    }

    [Fact]
    public void ExtractFromFile_BeschaedigteOderFehlendeDatei_LiefertLeerenIndex()
    {
        using var temp = new TempDirectory();
        var path = temp.WriteXtf("kaputt.xtf", "<TRANSFER><Haltung");

        Assert.Empty(XtfStammdatenExtractor.ExtractFromFile(path));
        Assert.Empty(XtfStammdatenExtractor.ExtractFromFile(path + ".fehlt"));
    }

    [Fact]
    public void BuildIndex_ErgaenztFehlendeFelderAusMehrerenXtfDateien()
    {
        using var temp = new TempDirectory();
        temp.WriteXtf(
            "a.xtf",
            BuildXtf(material: "PE", laenge: null, hoehe: null, breite: null));
        temp.WriteXtf(
            Path.Combine("Unterordner", "b.xtf"),
            BuildXtf(material: null, laenge: "18.75", hoehe: "250", breite: null));
        var messages = new List<string>();

        var index = XtfStammdatenExtractor.BuildIndex(temp.Path, messages);

        var data = Assert.Single(index).Value;
        Assert.Equal("Polyethylen", data.Material);
        Assert.Equal(18.75, data.Laenge_m);
        Assert.Equal(250, data.DN_mm);
        Assert.Contains(messages, message =>
            message.Contains("1 Haltungen aus 2 XTF-Dateien", StringComparison.Ordinal));
    }

    [Fact]
    public void BuildIndex_VerwendetDieEingespritzteXtfQuelle()
    {
        var sourceReader = new RecordingSourceReader(
            XDocument.Parse(BuildXtf("PP", "9.5", "200", null)));

        var index = XtfStammdatenExtractor.BuildIndex(
            "virtueller-export",
            sourceReader);

        var data = Assert.Single(index).Value;
        Assert.Equal("Polypropylen", data.Material);
        Assert.Equal("virtueller-export", sourceReader.EnumeratedRoot);
        Assert.Equal("virtuelle.xtf", sourceReader.LoadedPath);
    }

    private static string BuildXtf(
        string? material,
        string? laenge,
        string? hoehe,
        string? breite)
    {
        static string Element(string name, string? value) =>
            value is null ? string.Empty : $"<{name}>{value}</{name}>";

        return $$"""
            <TRANSFER xmlns="urn:sia405:test">
              <SIA405_Abwasser.Kanal TID="K1">
                <Nutzungsart_Ist>Mischabwasser</Nutzungsart_Ist>
              </SIA405_Abwasser.Kanal>
              <SIA405_Abwasser.Haltung TID="H1">
                <Bezeichnung>80638-80631</Bezeichnung>
                {{Element("LaengeEffektiv", laenge)}}
                {{Element("Lichte_Hoehe", hoehe)}}
                {{Element("Lichte_Breite", breite)}}
                {{Element("Material", material)}}
                <AbwasserbauwerkRef REF="K1" />
              </SIA405_Abwasser.Haltung>
            </TRANSFER>
            """;
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"sewerstudio-xtf-stammdaten-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public string WriteXtf(string relativePath, string content)
        {
            var path = System.IO.Path.Combine(Path, relativePath);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
            return path;
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch
            {
                // Test-Aufraeumfehler duerfen das Ergebnis nicht verdecken.
            }
        }
    }

    private sealed class RecordingSourceReader(XDocument document)
        : IXtfStammdatenSourceReader
    {
        public string? EnumeratedRoot { get; private set; }

        public string? LoadedPath { get; private set; }

        public IReadOnlyList<string> EnumerateXtfFiles(string exportRoot)
        {
            EnumeratedRoot = exportRoot;
            return ["virtuelle.xtf"];
        }

        public XDocument? TryLoadXml(string xtfPath)
        {
            LoadedPath = xtfPath;
            return document;
        }
    }
}
