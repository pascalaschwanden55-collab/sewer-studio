using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;

namespace AuswertungPro.Next.Infrastructure.Map;

/// <summary>
/// Eine Haltung aus dem amtlichen Abwasserkataster (SIA405-XTF), reduziert auf das
/// fuer den Verteil-Abgleich Noetige. Die Haltungs-Bezeichnung ist im Kataster bereits
/// das Schacht-Paar in KORREKTER Reihenfolge ("865-864" = von Schacht 865 nach 864).
/// </summary>
public sealed record CadastreHaltung(
    string Bezeichnung,
    string ShaftA,
    string ShaftB,
    string? Laenge,
    string? LichteHoehe,
    string? Material);

/// <summary>
/// Liest aus einer SIA405-XTF (Abwasserkataster) eine eigenstaendige Haltungs-Tabelle.
/// Streaming via XmlReader, damit die ~600 MB grossen Kataster-Dateien nicht in den RAM muessen.
/// Die extrahierte Tabelle (TSV) ist die "interne Wahrheit" fuer den spaeteren Abgleich,
/// damit nicht bei jeder Verteilung die Riesendatei neu geparst werden muss.
/// </summary>
public static class HaltungCadastreExtractor
{
    public const string TableHeader = "Bezeichnung\tShaftA\tShaftB\tLaenge\tLichteHoehe\tMaterial";

    /// <summary>
    /// Streamt alle Haltungen aus der XTF. Liefert nur Haltungen mit Bezeichnung;
    /// das Schacht-Paar wird aus der Bezeichnung ("A-B") abgeleitet (leer wenn nicht eindeutig).
    /// </summary>
    public static IEnumerable<CadastreHaltung> Extract(string xtfPath)
    {
        var settings = new XmlReaderSettings
        {
            IgnoreWhitespace = true,
            IgnoreComments = true,
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null
        };
        using var reader = XmlReader.Create(xtfPath, settings);

        bool inHaltung = false;
        string? bezeichnung = null;
        string? laenge = null;
        string? lichteHoehe = null;
        string? material = null;

        // ReadElementContentAsString() bewegt den Reader bereits weiter — skipRead
        // verhindert ein zweites reader.Read() danach.
        bool skipRead = false;

        while (skipRead || reader.Read())
        {
            skipRead = false;

            if (reader.NodeType == XmlNodeType.Element)
            {
                var local = reader.LocalName;
                if (local.EndsWith(".Haltung", StringComparison.Ordinal))
                {
                    inHaltung = true;
                    bezeichnung = null; laenge = null; lichteHoehe = null; material = null;
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
                    var (a, b) = SplitShaftPair(bezeichnung!);
                    yield return new CadastreHaltung(bezeichnung!.Trim(), a, b, Clean(laenge), Clean(lichteHoehe), Clean(material));
                }
                inHaltung = false;
                bezeichnung = null; laenge = null; lichteHoehe = null; material = null;
            }
        }
    }

    /// <summary>
    /// Extrahiert die Tabelle und schreibt sie als TSV. Erste Zeile ist eine Metazeile
    /// (# Quelle/Anzahl/Groesse), zweite Zeile der Spaltenkopf. Liefert die Anzahl Haltungen.
    /// </summary>
    public static int BuildTable(string xtfPath, string outTablePath)
    {
        var dir = Path.GetDirectoryName(outTablePath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        var count = 0;
        var fi = new FileInfo(xtfPath);
        using var writer = new StreamWriter(outTablePath, append: false, new UTF8Encoding(false));
        // Metazeile fuer Staleness-Pruefung (Quelle + Groesse + Aenderungszeit).
        writer.WriteLine($"# source={xtfPath}\tbytes={fi.Length}\tmtimeUtc={fi.LastWriteTimeUtc:O}");
        writer.WriteLine(TableHeader);

        foreach (var h in Extract(xtfPath))
        {
            writer.WriteLine(string.Join('\t',
                Escape(h.Bezeichnung), Escape(h.ShaftA), Escape(h.ShaftB),
                Escape(h.Laenge), Escape(h.LichteHoehe), Escape(h.Material)));
            count++;
        }

        return count;
    }

    /// <summary>Liest eine zuvor gebaute TSV-Tabelle zurueck (ueberspringt Meta-/Kopfzeile).</summary>
    public static IReadOnlyList<CadastreHaltung> ReadTable(string tablePath)
    {
        var list = new List<CadastreHaltung>();
        foreach (var line in File.ReadLines(tablePath))
        {
            if (line.Length == 0 || line[0] == '#') continue;
            if (line.StartsWith("Bezeichnung\t", StringComparison.Ordinal)) continue; // Kopfzeile
            var parts = line.Split('\t');
            if (parts.Length < 3) continue;
            list.Add(new CadastreHaltung(
                parts[0], parts[1], parts[2],
                parts.Length > 3 ? NullIfEmpty(parts[3]) : null,
                parts.Length > 4 ? NullIfEmpty(parts[4]) : null,
                parts.Length > 5 ? NullIfEmpty(parts[5]) : null));
        }
        return list;
    }

    /// <summary>Prueft, ob die Tabelle zur aktuellen XTF passt (gleiche Groesse + Aenderungszeit).</summary>
    public static bool IsTableFresh(string tablePath, string xtfPath)
    {
        if (!File.Exists(tablePath) || !File.Exists(xtfPath)) return false;
        try
        {
            var first = File.ReadLines(tablePath).FirstOrDefault();
            if (first is null || !first.StartsWith("#", StringComparison.Ordinal)) return false;
            var fi = new FileInfo(xtfPath);
            return first.Contains($"bytes={fi.Length}", StringComparison.Ordinal)
                   && first.Contains($"mtimeUtc={fi.LastWriteTimeUtc:O}", StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Zerlegt "865-864" bzw. "06.24341-35625" in die zwei Schachtnummern (sonst leer).</summary>
    public static (string A, string B) SplitShaftPair(string bezeichnung)
    {
        var parts = bezeichnung.Trim().Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length == 2 ? (parts[0], parts[1]) : ("", "");
    }

    private static string? Clean(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
    private static string Escape(string? v) => (v ?? "").Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ');
    private static string? NullIfEmpty(string v) => string.IsNullOrEmpty(v) ? null : v;
}
