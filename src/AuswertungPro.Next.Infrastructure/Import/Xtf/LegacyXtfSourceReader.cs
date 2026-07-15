using System.Text;
using System.Xml.Linq;
using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.Infrastructure.Import.Xtf;

internal sealed record LegacyXtfSource(
    XDocument Document,
    bool IsSia405,
    bool IsVsa);

/// <summary>Liest Formathinweise und XML-Inhalt einer alten XTF-Quelldatei.</summary>
internal sealed class LegacyXtfSourceReader
{
    private readonly ISafeXmlDocumentLoader _xmlLoader;

    public LegacyXtfSourceReader(ISafeXmlDocumentLoader xmlLoader)
    {
        _xmlLoader = xmlLoader ?? throw new ArgumentNullException(nameof(xmlLoader));
    }

    public LegacyXtfSource Read(string path)
    {
        using var reader = new StreamReader(path, Encoding.UTF8);
        var buffer = new char[4096];
        var length = reader.Read(buffer, 0, buffer.Length);
        var header = new string(buffer, 0, length);

        return new LegacyXtfSource(
            _xmlLoader.Load(path, LoadOptions.PreserveWhitespace),
            header.Contains("SIA405", StringComparison.OrdinalIgnoreCase),
            header.Contains("VSA_KEK", StringComparison.OrdinalIgnoreCase));
    }
}
