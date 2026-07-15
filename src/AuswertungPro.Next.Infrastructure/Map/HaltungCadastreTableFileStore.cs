using System.Text;
using System.Xml;
using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.Infrastructure.Map;

/// <summary>Liest und schreibt die schnelle Haltungs-Katastertabelle.</summary>
public interface IHaltungCadastreTableStore
{
    IEnumerable<CadastreHaltung> Extract(string xtfPath);

    int BuildTable(string xtfPath, string outTablePath);

    IReadOnlyList<CadastreHaltung> ReadTable(string tablePath);

    bool IsTableFresh(string tablePath, string xtfPath);
}

/// <summary>
/// Streamt grosse SIA405-XTF-Dateien und speichert den reduzierten TSV-Zwischenspeicher atomar.
/// </summary>
public sealed class HaltungCadastreTableFileStore : IHaltungCadastreTableStore
{
    public IEnumerable<CadastreHaltung> Extract(string xtfPath)
    {
        var settings = new XmlReaderSettings
        {
            IgnoreWhitespace = true,
            IgnoreComments = true,
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null
        };
        using var reader = XmlReader.Create(xtfPath, settings);

        var inHaltung = false;
        string? bezeichnung = null;
        string? laenge = null;
        string? lichteHoehe = null;
        string? material = null;
        var skipRead = false;

        while (skipRead || reader.Read())
        {
            skipRead = false;

            if (reader.NodeType == XmlNodeType.Element)
            {
                var local = reader.LocalName;
                if (local.EndsWith(".Haltung", StringComparison.Ordinal))
                {
                    inHaltung = true;
                    bezeichnung = null;
                    laenge = null;
                    lichteHoehe = null;
                    material = null;
                }
                else if (inHaltung && local == "Bezeichnung" && bezeichnung == null)
                {
                    bezeichnung = reader.ReadElementContentAsString();
                    skipRead = true;
                }
                else if (inHaltung && local == "LaengeEffektiv" && laenge == null)
                {
                    laenge = reader.ReadElementContentAsString();
                    skipRead = true;
                }
                else if (inHaltung && local == "Lichte_Hoehe" && lichteHoehe == null)
                {
                    lichteHoehe = reader.ReadElementContentAsString();
                    skipRead = true;
                }
                else if (inHaltung && local == "Material" && material == null)
                {
                    material = reader.ReadElementContentAsString();
                    skipRead = true;
                }
            }
            else if (reader.NodeType == XmlNodeType.EndElement
                     && reader.LocalName.EndsWith(".Haltung", StringComparison.Ordinal))
            {
                if (!string.IsNullOrWhiteSpace(bezeichnung))
                {
                    var (shaftA, shaftB) = HaltungCadastreExtractor.SplitShaftPair(bezeichnung);
                    yield return new CadastreHaltung(
                        bezeichnung.Trim(),
                        shaftA,
                        shaftB,
                        Clean(laenge),
                        Clean(lichteHoehe),
                        Clean(material));
                }

                inHaltung = false;
                bezeichnung = null;
                laenge = null;
                lichteHoehe = null;
                material = null;
            }
        }
    }

    public int BuildTable(string xtfPath, string outTablePath)
    {
        var directory = Path.GetDirectoryName(outTablePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var count = 0;
        var sourceInfo = new FileInfo(xtfPath);
        AtomicTextFileWriter.Write(outTablePath, writer =>
        {
            writer.WriteLine(
                $"# source={xtfPath}\tbytes={sourceInfo.Length}\tmtimeUtc={sourceInfo.LastWriteTimeUtc:O}");
            writer.WriteLine(HaltungCadastreExtractor.TableHeader);

            foreach (var haltung in Extract(xtfPath))
            {
                writer.WriteLine(string.Join('\t',
                    Escape(haltung.Bezeichnung),
                    Escape(haltung.ShaftA),
                    Escape(haltung.ShaftB),
                    Escape(haltung.Laenge),
                    Escape(haltung.LichteHoehe),
                    Escape(haltung.Material)));
                count++;
            }
        }, new UTF8Encoding(false));

        return count;
    }

    public IReadOnlyList<CadastreHaltung> ReadTable(string tablePath)
    {
        var result = new List<CadastreHaltung>();
        foreach (var line in File.ReadLines(tablePath))
        {
            if (line.Length == 0 || line[0] == '#')
                continue;
            if (line.StartsWith("Bezeichnung\t", StringComparison.Ordinal))
                continue;

            var parts = line.Split('\t');
            if (parts.Length < 3)
                continue;

            result.Add(new CadastreHaltung(
                parts[0],
                parts[1],
                parts[2],
                parts.Length > 3 ? NullIfEmpty(parts[3]) : null,
                parts.Length > 4 ? NullIfEmpty(parts[4]) : null,
                parts.Length > 5 ? NullIfEmpty(parts[5]) : null));
        }

        return result;
    }

    public bool IsTableFresh(string tablePath, string xtfPath)
    {
        if (!File.Exists(tablePath) || !File.Exists(xtfPath))
            return false;

        try
        {
            var firstLine = File.ReadLines(tablePath).FirstOrDefault();
            if (firstLine is null || !firstLine.StartsWith('#'))
                return false;

            var sourceInfo = new FileInfo(xtfPath);
            return firstLine.Contains($"bytes={sourceInfo.Length}", StringComparison.Ordinal)
                   && firstLine.Contains(
                       $"mtimeUtc={sourceInfo.LastWriteTimeUtc:O}",
                       StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string Escape(string? value)
        => (value ?? string.Empty).Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ');

    private static string? NullIfEmpty(string value)
        => string.IsNullOrEmpty(value) ? null : value;
}
