using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

/// <summary>
/// Byte-Builder fuer formatgetreue Visual-FoxPro-DBF/FPT-Testdateien
/// (Version 0x30, CP1252, Backlink) — gemeinsam genutzt von DbfTableTests
/// und den KINS-DBF-Anreicherungs-Tests.
/// </summary>
internal sealed class DbfTestFileBuilder
{
    private static readonly Encoding Cp1252 = ErzeugeCp1252();
    private readonly List<(string Name, char Typ, int Laenge, int Dez)> _felder = new();
    private readonly List<(byte[] Daten, bool Geloescht)> _records = new();

    private static Encoding ErzeugeCp1252()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(1252);
    }

    public DbfTestFileBuilder Feld(string name, char typ, int laenge, int dez = 0)
    {
        _felder.Add((name, typ, laenge, dez));
        return this;
    }

    public DbfTestFileBuilder Record(Action<DbfTestRecordBuilder> fuellen, bool geloescht = false)
    {
        var rb = new DbfTestRecordBuilder(_felder, Cp1252);
        fuellen(rb);
        _records.Add((rb.Bytes(), geloescht));
        return this;
    }

    public void Schreiben(string pfad)
    {
        var recLen = 1 + _felder.Sum(f => f.Laenge);
        var headerLen = 32 + _felder.Count * 32 + 1 + 263; // VFP: Deskriptoren + 0x0D + Backlink

        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);

        // Header (32 Bytes)
        w.Write((byte)0x30);                    // Visual FoxPro
        w.Write((byte)26); w.Write((byte)6); w.Write((byte)24); // Datum JJ/MM/TT
        w.Write(_records.Count);                // Record-Anzahl
        w.Write((ushort)headerLen);
        w.Write((ushort)recLen);
        w.Write(new byte[20]);                  // reserviert

        // Felddeskriptoren (je 32 Bytes)
        foreach (var f in _felder)
        {
            var name = new byte[11];
            Encoding.ASCII.GetBytes(f.Name).CopyTo(name, 0);
            w.Write(name);
            w.Write((byte)f.Typ);
            w.Write(new byte[4]);               // Displacement
            w.Write((byte)f.Laenge);
            w.Write((byte)f.Dez);
            w.Write(new byte[14]);              // reserviert
        }
        w.Write((byte)0x0D);                    // Terminator
        w.Write(new byte[263]);                 // VFP-Backlink

        foreach (var (daten, geloescht) in _records)
        {
            w.Write((byte)(geloescht ? '*' : ' '));
            w.Write(daten);
        }
        w.Write((byte)0x1A);                    // EOF-Marker

        File.WriteAllBytes(pfad, ms.ToArray());
    }

    /// <summary>Schreibt eine FPT mit genau einem Textblock; liefert dessen Blocknummer.</summary>
    public static int SchreibeFpt(string pfad, string text)
    {
        const int blockGroesse = 64;
        var daten = Cp1252.GetBytes(text);

        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);

        // FPT-Header (512 Bytes): Blockgroesse Big-Endian an Offset 6
        var header = new byte[512];
        header[6] = (byte)(blockGroesse >> 8);
        header[7] = (byte)(blockGroesse & 0xFF);
        w.Write(header);

        // Erster Block hinter dem Header
        var blockNr = 512 / blockGroesse;
        w.Write(new byte[] { 0, 0, 0, 1 });                     // Typ 1 = Text (Big-Endian)
        w.Write(new byte[] {
            (byte)(daten.Length >> 24), (byte)(daten.Length >> 16),
            (byte)(daten.Length >> 8), (byte)daten.Length });   // Laenge (Big-Endian)
        w.Write(daten);

        File.WriteAllBytes(pfad, ms.ToArray());
        return blockNr;
    }
}

internal sealed class DbfTestRecordBuilder
{
    private readonly List<(string Name, char Typ, int Laenge, int Dez)> _felder;
    private readonly Encoding _enc;
    private readonly MemoryStream _ms = new();
    private int _index;

    public DbfTestRecordBuilder(List<(string Name, char Typ, int Laenge, int Dez)> felder, Encoding enc)
    {
        _felder = felder;
        _enc = enc;
    }

    public DbfTestRecordBuilder Text(string wert)
    {
        var laenge = _felder[_index++].Laenge;
        var bytes = new byte[laenge];
        Array.Fill(bytes, (byte)' ');
        var raw = _enc.GetBytes(wert);
        Array.Copy(raw, bytes, Math.Min(raw.Length, laenge));
        _ms.Write(bytes);
        return this;
    }

    public DbfTestRecordBuilder Int32(int wert)
    {
        _index++;
        _ms.Write(BitConverter.GetBytes(wert));
        return this;
    }

    public byte[] Bytes() => _ms.ToArray();
}
