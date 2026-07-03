using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace AuswertungPro.Next.Infrastructure.Import.Dbf;

/// <summary>Felddeskriptor einer DBF-Tabelle.</summary>
public sealed record DbfFieldDescriptor(string Name, char Type, int Length, int DecimalCount);

/// <summary>
/// Minimaler Leser fuer dBase-/Visual-FoxPro-Tabellen (.DBF, Memos in .FPT).
/// Bewusst ohne Fremdpaket: KINS-DVD-Exporte liefern Stammdaten als
/// FoxPro-DBF (haltung.DBF, schacht.DBF), fuer die es keinen .NET-Treiber gibt.
/// Alle Werte werden als String normalisiert (CP1252); geloeschte Records
/// (Statusbyte '*') werden uebersprungen.
/// </summary>
public sealed class DbfTable
{
    private static readonly Encoding Cp1252 = ErzeugeCp1252();

    public IReadOnlyList<DbfFieldDescriptor> Fields { get; }
    public IReadOnlyList<IReadOnlyDictionary<string, string>> Rows { get; }

    private DbfTable(
        IReadOnlyList<DbfFieldDescriptor> fields,
        IReadOnlyList<IReadOnlyDictionary<string, string>> rows)
    {
        Fields = fields;
        Rows = rows;
    }

    private static Encoding ErzeugeCp1252()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(1252);
    }

    /// <summary>Liest eine DBF-Datei; eine gleichnamige .FPT daneben wird fuer Memo-Felder genutzt.</summary>
    public static DbfTable Read(string dbfPath)
    {
        var daten = File.ReadAllBytes(dbfPath);
        if (daten.Length < 32)
            throw new InvalidDataException($"DBF-Datei zu kurz fuer einen Header: {dbfPath}");

        // Header: Byte 0 Version, Bytes 4-7 Record-Anzahl, 8-9 Headerlaenge, 10-11 Recordlaenge
        var version = daten[0];
        if (version is not (0x30 or 0x31 or 0x03 or 0x83 or 0xF5 or 0x8B))
            throw new InvalidDataException($"Unbekannte DBF-Version 0x{version:X2}: {dbfPath}");

        var recordCount = BitConverter.ToInt32(daten, 4);
        var headerLaenge = BitConverter.ToUInt16(daten, 8);
        var recordLaenge = BitConverter.ToUInt16(daten, 10);
        if (headerLaenge < 33 || headerLaenge > daten.Length || recordLaenge < 1)
            throw new InvalidDataException($"DBF-Header unplausibel (Header={headerLaenge}, Record={recordLaenge}): {dbfPath}");

        var felder = LiesFelddeskriptoren(daten, dbfPath);

        var memo = FptMemoFile.TryOpen(Path.ChangeExtension(dbfPath, ".FPT"), Cp1252);

        var rows = new List<IReadOnlyDictionary<string, string>>();
        for (var i = 0; i < recordCount; i++)
        {
            long offset = headerLaenge + (long)i * recordLaenge;
            if (offset + recordLaenge > daten.Length)
                break; // Datei kuerzer als deklariert — restliche Records fehlen

            if (daten[offset] == '*')
                continue; // geloeschter Record

            rows.Add(LiesRecord(daten, (int)offset + 1, felder, memo));
        }

        return new DbfTable(felder, rows);
    }

    private static List<DbfFieldDescriptor> LiesFelddeskriptoren(byte[] daten, string dbfPath)
    {
        var felder = new List<DbfFieldDescriptor>();
        var pos = 32;

        // Deskriptoren zu je 32 Bytes bis zum Terminator 0x0D
        while (pos + 32 <= daten.Length && daten[pos] != 0x0D)
        {
            var name = Encoding.ASCII.GetString(daten, pos, 11).TrimEnd('\0', ' ');
            var typ = (char)daten[pos + 11];
            int laenge = daten[pos + 16];
            int dezimal = daten[pos + 17];
            felder.Add(new DbfFieldDescriptor(name, typ, laenge, dezimal));
            pos += 32;
        }

        if (felder.Count == 0)
            throw new InvalidDataException($"DBF ohne Felddeskriptoren: {dbfPath}");

        return felder;
    }

    private static IReadOnlyDictionary<string, string> LiesRecord(
        byte[] daten, int offset, List<DbfFieldDescriptor> felder, FptMemoFile? memo)
    {
        var row = new Dictionary<string, string>(felder.Count, StringComparer.OrdinalIgnoreCase);
        var pos = offset;

        foreach (var feld in felder)
        {
            row[feld.Name] = LiesFeldwert(daten, pos, feld, memo);
            pos += feld.Length;
        }

        return row;
    }

    private static string LiesFeldwert(byte[] daten, int pos, DbfFieldDescriptor feld, FptMemoFile? memo)
    {
        switch (feld.Type)
        {
            case 'C': // Zeichen (CP1252)
                return Cp1252.GetString(daten, pos, feld.Length).TrimEnd(' ', '\0');

            case 'N': // Zahl als ASCII
            case 'F':
                return Encoding.ASCII.GetString(daten, pos, feld.Length).Trim(' ', '\0');

            case 'D': // Datum JJJJMMTT, roh weitergeben
                return Encoding.ASCII.GetString(daten, pos, feld.Length).Trim(' ', '\0');

            case 'L': // Logisch: T/F/Y/N/? roh
                return ((char)daten[pos]).ToString().Trim('?', ' ', '\0');

            case 'I': // VFP: Int32 little-endian binaer
                return feld.Length == 4
                    ? BitConverter.ToInt32(daten, pos).ToString(CultureInfo.InvariantCulture)
                    : string.Empty;

            case 'B': // VFP: Double little-endian binaer
                return feld.Length == 8
                    ? BitConverter.ToDouble(daten, pos).ToString(CultureInfo.InvariantCulture)
                    : string.Empty;

            case 'M': // Memo: Blocknummer → FPT
                var blockNr = LiesMemoBlockNummer(daten, pos, feld.Length);
                return memo is null ? string.Empty : memo.LiesText(blockNr);

            default: // unbekannte Typen (T/Y/G/0/...) nicht interpretieren
                return string.Empty;
        }
    }

    private static int LiesMemoBlockNummer(byte[] daten, int pos, int laenge)
    {
        // VFP: 4 Bytes binaer little-endian; dBase III: 10 Zeichen ASCII
        if (laenge == 4)
            return BitConverter.ToInt32(daten, pos);

        var text = Encoding.ASCII.GetString(daten, pos, laenge).Trim(' ', '\0');
        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var nr) ? nr : 0;
    }
}
