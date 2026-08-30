using System.Globalization;
using System.Text;
using System.Xml;
using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.Infrastructure.Map;

/// <summary>
/// Streamt grosse SIA405-XTF-Dateien und speichert den reduzierten
/// TSV-Zwischenspeicher atomar. Gegenstueck zu
/// <see cref="HaltungCadastreTableFileStore"/>, nur fuer Schaechte.
/// </summary>
public sealed class SchachtCadastreTableFileStore : ISchachtCadastreTableStore
{
    public IEnumerable<CadastreSchacht> Extract(string xtfPath)
    {
        // Die Fachdaten stehen am Normschacht, die Koordinaten am
        // gleichnamigen Abwasserknoten. Ein zweiter Durchgang waere bei
        // mehreren hundert Megabyte teuer, deshalb beides in einem Zug
        // sammeln und am Ende ueber die Bezeichnung zusammenfuehren.
        var fach = new Dictionary<string, CadastreSchacht>(StringComparer.OrdinalIgnoreCase);
        var lagen = new Dictionary<string, (double Ost, double Nord)>(StringComparer.OrdinalIgnoreCase);

        var settings = new XmlReaderSettings
        {
            IgnoreWhitespace = true,
            IgnoreComments = true,
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null
        };
        using var reader = XmlReader.Create(xtfPath, settings);

        var inSchacht = false;
        var inKnoten = false;
        string? bezeichnung = null;
        string? funktion = null;
        string? material = null;
        string? dimension1 = null;
        string? dimension2 = null;
        string? status = null;
        double? ost = null;
        double? nord = null;

        // ReadElementContentAsString() bewegt den Reader bereits weiter —
        // skipRead verhindert ein zweites reader.Read() danach.
        var skipRead = false;

        while (skipRead || reader.Read())
        {
            skipRead = false;

            if (reader.NodeType == XmlNodeType.Element)
            {
                var local = reader.LocalName;

                if (local.EndsWith(".Normschacht", StringComparison.Ordinal))
                {
                    inSchacht = true;
                    bezeichnung = funktion = material = dimension1 = dimension2 = status = null;
                }
                else if (local.EndsWith(".Abwasserknoten", StringComparison.Ordinal))
                {
                    inKnoten = true;
                    bezeichnung = null;
                    ost = null;
                    nord = null;
                }
                else if ((inSchacht || inKnoten) && local == "Bezeichnung" && bezeichnung == null)
                {
                    bezeichnung = reader.ReadElementContentAsString();
                    skipRead = true;
                }
                else if (inSchacht && local == "Funktion" && funktion == null)
                {
                    funktion = reader.ReadElementContentAsString();
                    skipRead = true;
                }
                else if (inSchacht && local == "Material" && material == null)
                {
                    material = reader.ReadElementContentAsString();
                    skipRead = true;
                }
                else if (inSchacht && local == "Dimension1" && dimension1 == null)
                {
                    dimension1 = reader.ReadElementContentAsString();
                    skipRead = true;
                }
                else if (inSchacht && local == "Dimension2" && dimension2 == null)
                {
                    dimension2 = reader.ReadElementContentAsString();
                    skipRead = true;
                }
                else if (inSchacht && local == "Status" && status == null)
                {
                    status = reader.ReadElementContentAsString();
                    skipRead = true;
                }
                else if (inKnoten && local == "C1" && ost == null)
                {
                    var roh = reader.ReadElementContentAsString();
                    skipRead = true;
                    if (double.TryParse(roh, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                        ost = v;
                }
                else if (inKnoten && local == "C2" && nord == null)
                {
                    var roh = reader.ReadElementContentAsString();
                    skipRead = true;
                    if (double.TryParse(roh, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                        nord = v;
                }
            }
            else if (reader.NodeType == XmlNodeType.EndElement)
            {
                var local = reader.LocalName;

                if (local.EndsWith(".Normschacht", StringComparison.Ordinal))
                {
                    if (!string.IsNullOrWhiteSpace(bezeichnung))
                    {
                        fach[bezeichnung!.Trim()] = new CadastreSchacht(
                            bezeichnung.Trim(),
                            Clean(funktion),
                            Clean(material),
                            Clean(dimension1),
                            Clean(dimension2),
                            Clean(status),
                            null,
                            null);
                    }

                    inSchacht = false;
                    bezeichnung = null;
                }
                else if (local.EndsWith(".Abwasserknoten", StringComparison.Ordinal))
                {
                    // Nur die Lage des Knotens zaehlt. Ein Deckel ist ein
                    // Geschwister-Element und wird nie betreten.
                    if (!string.IsNullOrWhiteSpace(bezeichnung) && ost.HasValue && nord.HasValue)
                        lagen[bezeichnung!.Trim()] = (ost.Value, nord.Value);

                    inKnoten = false;
                    bezeichnung = null;
                    ost = null;
                    nord = null;
                }
            }
        }

        foreach (var (name, schacht) in fach)
        {
            yield return lagen.TryGetValue(name, out var lage)
                ? schacht with { Ost = lage.Ost, Nord = lage.Nord }
                : schacht;
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
            writer.WriteLine(SchachtCadastreExtractor.TableHeader);

            foreach (var schacht in Extract(xtfPath))
            {
                writer.WriteLine(string.Join('\t',
                    Escape(schacht.Bezeichnung),
                    Escape(schacht.Funktion),
                    Escape(schacht.Material),
                    Escape(schacht.Dimension1),
                    Escape(schacht.Dimension2),
                    Escape(schacht.Status),
                    Zahl(schacht.Ost),
                    Zahl(schacht.Nord)));
                count++;
            }
        }, new UTF8Encoding(false));

        return count;
    }

    public IReadOnlyList<CadastreSchacht> ReadTable(string tablePath)
    {
        var result = new List<CadastreSchacht>();
        foreach (var line in File.ReadLines(tablePath))
        {
            if (line.Length == 0 || line[0] == '#')
                continue;
            if (line.StartsWith("Bezeichnung\t", StringComparison.Ordinal))
                continue;

            var parts = line.Split('\t');
            if (parts.Length < 1 || string.IsNullOrWhiteSpace(parts[0]))
                continue;

            result.Add(new CadastreSchacht(
                parts[0],
                parts.Length > 1 ? NullIfEmpty(parts[1]) : null,
                parts.Length > 2 ? NullIfEmpty(parts[2]) : null,
                parts.Length > 3 ? NullIfEmpty(parts[3]) : null,
                parts.Length > 4 ? NullIfEmpty(parts[4]) : null,
                parts.Length > 5 ? NullIfEmpty(parts[5]) : null,
                parts.Length > 6 ? Koordinate(parts[6]) : null,
                parts.Length > 7 ? Koordinate(parts[7]) : null));
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

    private static string Zahl(double? value)
        => value?.ToString("R", CultureInfo.InvariantCulture) ?? string.Empty;

    private static double? Koordinate(string value)
        => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)
            ? v
            : null;
}
