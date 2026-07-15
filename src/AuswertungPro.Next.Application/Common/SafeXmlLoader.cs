using System.Xml.Linq;

namespace AuswertungPro.Next.Application.Common;

/// <summary>Kompatible statische Fassade fuer bestehende XML-Aufrufer.</summary>
public static class SafeXmlLoader
{
    private static readonly ISafeXmlDocumentLoader DefaultLoader = new SafeXmlDocumentLoader();

    public static XDocument Load(string path)
        => DefaultLoader.Load(path);

    public static XDocument Load(string path, LoadOptions options)
        => DefaultLoader.Load(path, options);
}
