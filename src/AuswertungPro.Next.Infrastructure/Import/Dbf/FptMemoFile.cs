using System;
using System.IO;
using System.Text;

namespace AuswertungPro.Next.Infrastructure.Import.Dbf;

/// <summary>
/// Liest Memo-Bloecke aus einer FoxPro-.FPT-Datei.
/// Header: Blockgroesse als UInt16 Big-Endian an Offset 6.
/// Block: 4 Bytes Typ (Big-Endian, 1 = Text) + 4 Bytes Laenge (Big-Endian) + Inhalt.
/// </summary>
internal sealed class FptMemoFile
{
    private readonly byte[] _daten;
    private readonly int _blockGroesse;
    private readonly Encoding _encoding;

    private FptMemoFile(byte[] daten, int blockGroesse, Encoding encoding)
    {
        _daten = daten;
        _blockGroesse = blockGroesse;
        _encoding = encoding;
    }

    /// <summary>Oeffnet die FPT neben der DBF; null wenn keine existiert oder unlesbar ist.</summary>
    public static FptMemoFile? TryOpen(string fptPath, Encoding encoding)
    {
        try
        {
            if (!File.Exists(fptPath))
                return null;

            var daten = File.ReadAllBytes(fptPath);
            if (daten.Length < 8)
                return null;

            var blockGroesse = (daten[6] << 8) | daten[7];
            if (blockGroesse <= 0)
                return null;

            return new FptMemoFile(daten, blockGroesse, encoding);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Liefert den Text des Blocks; leer bei ungueltiger Nummer oder Nicht-Text-Block.</summary>
    public string LiesText(int blockNummer)
    {
        if (blockNummer <= 0)
            return string.Empty;

        long offset = (long)blockNummer * _blockGroesse;
        if (offset + 8 > _daten.Length)
            return string.Empty;

        var typ = LiesBigEndianInt32(offset);
        var laenge = LiesBigEndianInt32(offset + 4);
        if (typ != 1 || laenge < 0)
            return string.Empty;

        var start = offset + 8;
        var verfuegbar = Math.Min(laenge, _daten.Length - start);
        if (verfuegbar <= 0)
            return string.Empty;

        return _encoding.GetString(_daten, (int)start, (int)verfuegbar).TrimEnd('\0');
    }

    private int LiesBigEndianInt32(long offset)
        => (_daten[offset] << 24) | (_daten[offset + 1] << 16) | (_daten[offset + 2] << 8) | _daten[offset + 3];
}
