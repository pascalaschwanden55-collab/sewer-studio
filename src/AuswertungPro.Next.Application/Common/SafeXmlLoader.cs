using System.IO;
using System.Xml;
using System.Xml.Linq;

namespace AuswertungPro.Next.Application.Common;

/// <summary>
/// Audit 2026-04-23 L4: zentraler Loader fuer XML-Dokumente mit explizit
/// deaktivierter DTD-Verarbeitung. Schuetzt vor XXE-Attacken (XML External
/// Entities) wenn die XML-Quelle aus externer Hand kommt — z.B. importierte
/// XTF/SIA-405-Dateien aus fremden Buchhaltungs-Tools.
///
/// .NET-Default fuer <see cref="XDocument.Load(string)"/> ist defensiv (DTD
/// prohibited), aber NICHT explizit. Dieser Helper macht die Hardening-Konfig
/// sichtbar und uebersteht zukuenftige .NET-Versions-Drifts.
/// </summary>
public static class SafeXmlLoader
{
    private static readonly XmlReaderSettings _settings = new()
    {
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null, // verhindert externe Entity-Aufloesung
    };

    /// <summary>
    /// Laedt ein XML-Dokument von einem Pfad mit DTD prohibited + XmlResolver=null.
    /// Wirft <see cref="XmlException"/> wenn das Dokument eine DTD-Deklaration enthaelt.
    /// </summary>
    public static XDocument Load(string path)
    {
        using var stream = File.OpenRead(path);
        using var reader = XmlReader.Create(stream, _settings);
        return XDocument.Load(reader);
    }

    /// <summary>
    /// Laedt mit zusaetzlichen <see cref="LoadOptions"/> (z.B. PreserveWhitespace fuer XTF).
    /// </summary>
    public static XDocument Load(string path, LoadOptions options)
    {
        using var stream = File.OpenRead(path);
        using var reader = XmlReader.Create(stream, _settings);
        return XDocument.Load(reader, options);
    }
}
