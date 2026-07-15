using System.Xml;
using System.Xml.Linq;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Infrastructure.Import.Ibak;
using AuswertungPro.Next.Infrastructure.Import.Xtf;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class SafeXmlLoaderTests
{
    [Fact]
    public void Load_LiestNormalesXmlUndErhaeltOptionalLeerraum()
    {
        using var temp = new TempXmlFile("<root>  <value>Text</value>  </root>");

        var document = new SafeXmlDocumentLoader().Load(temp.Path, LoadOptions.PreserveWhitespace);

        Assert.Equal("root", document.Root?.Name.LocalName);
        Assert.Contains(document.Root!.Nodes(), node => node is XText text && text.Value == "  ");
    }

    [Fact]
    public void Load_BlockiertDtdUndExterneEntities()
    {
        using var temp = new TempXmlFile(
            "<!DOCTYPE root [<!ENTITY external SYSTEM 'file:///C:/Windows/win.ini'>]><root>&external;</root>");

        Assert.Throws<XmlException>(() => SafeXmlLoader.Load(temp.Path));
    }

    [Fact]
    public void M150Reader_NutztInjiziertenSicherenXmlLeser()
    {
        var loader = new RecordingXmlLoader();
        var reader = new M150XmlTextFileReader(loader);

        var document = reader.LoadXml("virtuell.xtf");

        Assert.Equal("virtuell.xtf", loader.Path);
        Assert.Equal(LoadOptions.PreserveWhitespace, loader.Options);
        Assert.Equal("injected", document.Root?.Name.LocalName);
    }

    [Theory]
    [InlineData(typeof(XmlCodeCatalogProvider))]
    [InlineData(typeof(WinCanCatalogDiscoveryService))]
    [InlineData(typeof(XtfHoldingFileReader))]
    [InlineData(typeof(M150XmlTextFileReader))]
    [InlineData(typeof(XtfStammdatenSourceReader))]
    [InlineData(typeof(LegacyXtfImportService))]
    public void XmlDateiAufrufer_AkzeptierenSicherenLeserAlsAbhaengigkeit(Type consumerType)
    {
        var constructors = consumerType.GetConstructors(
            System.Reflection.BindingFlags.Public
            | System.Reflection.BindingFlags.NonPublic
            | System.Reflection.BindingFlags.Instance);

        Assert.Contains(
            constructors,
            constructor => constructor.GetParameters()
                .Any(parameter => parameter.ParameterType == typeof(ISafeXmlDocumentLoader)));
    }

    private sealed class TempXmlFile : IDisposable
    {
        public TempXmlFile(string content)
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"sewerstudio-safe-xml-{Guid.NewGuid():N}.xml");
            File.WriteAllText(Path, content);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (File.Exists(Path))
                File.Delete(Path);
        }
    }

    private sealed class RecordingXmlLoader : ISafeXmlDocumentLoader
    {
        public string? Path { get; private set; }

        public LoadOptions? Options { get; private set; }

        public XDocument Load(string path)
        {
            Path = path;
            Options = LoadOptions.None;
            return new XDocument(new XElement("injected"));
        }

        public XDocument Load(string path, LoadOptions options)
        {
            Path = path;
            Options = options;
            return new XDocument(new XElement("injected"));
        }
    }
}
