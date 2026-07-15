using System.Xml.Linq;
using AuswertungPro.Next.Application.Common;
using SafeXtfFileEnumeration = AuswertungPro.Next.Infrastructure.Common.SafeFileEnumeration;

namespace AuswertungPro.Next.Infrastructure.Import.Ibak;

/// <summary>Liefert XTF-Dateien und ihre sicher gelesenen XML-Dokumente.</summary>
public interface IXtfStammdatenSourceReader
{
    IReadOnlyList<string> EnumerateXtfFiles(string exportRoot);

    XDocument? TryLoadXml(string xtfPath);
}

/// <summary>
/// Kapselt rekursive Dateisuche, Existenzpruefung und geschuetztes XML-Laden.
/// </summary>
public sealed class XtfStammdatenSourceReader : IXtfStammdatenSourceReader
{
    public IReadOnlyList<string> EnumerateXtfFiles(string exportRoot)
    {
        if (string.IsNullOrWhiteSpace(exportRoot) || !Directory.Exists(exportRoot))
            return Array.Empty<string>();

        try
        {
            return SafeXtfFileEnumeration
                .EnumerateFilesSafe(exportRoot, "*.xtf", recursive: true)
                .ToList();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    public XDocument? TryLoadXml(string xtfPath)
    {
        if (string.IsNullOrWhiteSpace(xtfPath) || !File.Exists(xtfPath))
            return null;

        try
        {
            return SafeXmlLoader.Load(xtfPath);
        }
        catch
        {
            return null;
        }
    }
}
