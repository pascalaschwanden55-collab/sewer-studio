using System.Xml.Linq;
using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.Infrastructure.Import.Xtf;

/// <summary>Liest Haltungszuordnungen aus einer XTF-Datei.</summary>
public interface IXtfHoldingFileReader
{
    List<XtfHoldingInfo> ParseHoldingsFromXtf(string xtfPath);
}

/// <summary>
/// Kapselt Dateipruefung und sicheres XML-Laden fuer die PDF-Verteilung.
/// </summary>
public sealed class XtfHoldingFileReader : IXtfHoldingFileReader
{
    private readonly ISafeXmlDocumentLoader _xmlLoader;

    public XtfHoldingFileReader()
        : this(new SafeXmlDocumentLoader())
    {
    }

    public XtfHoldingFileReader(ISafeXmlDocumentLoader xmlLoader)
    {
        _xmlLoader = xmlLoader ?? throw new ArgumentNullException(nameof(xmlLoader));
    }

    public List<XtfHoldingInfo> ParseHoldingsFromXtf(string xtfPath)
    {
        var result = new List<XtfHoldingInfo>();
        if (!File.Exists(xtfPath))
            return result;

        XDocument document;
        try
        {
            document = _xmlLoader.Load(xtfPath);
        }
        catch (Exception)
        {
            // Eine kaputte XTF darf den Import einer gueltigen PDF nicht abbrechen.
            return result;
        }

        var kanalElements = document.Descendants()
            .Where(element => element.Name.LocalName.EndsWith("Kanal"));
        foreach (var kanal in kanalElements)
        {
            var haltung = kanal.Attribute("Haltung")?.Value ?? string.Empty;
            if (string.IsNullOrWhiteSpace(haltung))
                continue;

            result.Add(new XtfHoldingInfo
            {
                HaltungId = haltung,
                SchachtOben = kanal.Attribute("SchachtOben")?.Value ?? string.Empty,
                SchachtUnten = kanal.Attribute("SchachtUnten")?.Value ?? string.Empty
            });
        }

        return result;
    }
}
