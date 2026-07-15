using System.Xml;
using System.Xml.Linq;

namespace AuswertungPro.Next.Application.Common;

/// <summary>
/// Laedt fremde XML-Dateien mit blockierten DTDs und externen Entitaeten.
/// </summary>
public sealed class SafeXmlDocumentLoader : ISafeXmlDocumentLoader
{
    private static readonly XmlReaderSettings Settings = new()
    {
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null
    };

    public XDocument Load(string path)
    {
        using var stream = File.OpenRead(path);
        using var reader = XmlReader.Create(stream, Settings);
        return XDocument.Load(reader);
    }

    public XDocument Load(string path, LoadOptions options)
    {
        using var stream = File.OpenRead(path);
        using var reader = XmlReader.Create(stream, Settings);
        return XDocument.Load(reader, options);
    }
}
