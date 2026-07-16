using System.Text;
using System.Xml.Linq;
using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.Infrastructure.Import.Xtf;

/// <summary>Liest die XML- oder Textquelle eines M150-Imports.</summary>
public interface IM150SourceFileReader
{
    XDocument LoadXml(string path);

    string ReadUtf8Text(string path);
}

/// <summary>
/// Kapselt den sicheren XML-Zugriff und den nur bei Bedarf verwendeten Text-Rueckfall.
/// </summary>
public sealed class M150XmlTextFileReader : IM150SourceFileReader
{
    private readonly ISafeXmlDocumentLoader _xmlLoader;

    public M150XmlTextFileReader()
        : this(new SafeXmlDocumentLoader())
    {
    }

    public M150XmlTextFileReader(ISafeXmlDocumentLoader xmlLoader)
    {
        _xmlLoader = xmlLoader ?? throw new ArgumentNullException(nameof(xmlLoader));
    }

    public XDocument LoadXml(string path)
        => _xmlLoader.Load(path, LoadOptions.PreserveWhitespace);

    public string ReadUtf8Text(string path)
        => File.ReadAllText(path, Encoding.UTF8);
}

/// <summary>Kompatible Fassade fuer bestehende statische M150-Importwege.</summary>
public static class M150SourceFileReader
{
    private static readonly IM150SourceFileReader Default = new M150XmlTextFileReader();

    public static IM150SourceFileReader Current => Default;

    [Obsolete("Die M150-Quelldatei-Fassade ist unveraenderbar. Abhaengigkeit direkt uebergeben.")]
    public static void Use(IM150SourceFileReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        throw new NotSupportedException(
            "Die M150-Quelldatei-Fassade kann nicht mehr global ersetzt werden.");
    }
}
