using System.Buffers.Binary;

namespace AuswertungPro.Next.Application.Xtf;

/// <summary>
/// Liest den Linienzug aus einem GeoPackage-Geometrieblob.
///
/// Ein solcher Blob besteht aus einem eigenen Kopf und dahinter der bekannten
/// WKB-Darstellung (Well-Known Binary, OGC):
///
///   "GP" | Version | Flags | srs_id | [Envelope] | WKB
///
/// Die Flags sagen, wie gross der Envelope ist und in welcher Byte-Reihenfolge der Kopf
/// steht; das WKB dahinter traegt seine eigene Reihenfolge. Beides wird ausgewertet — ein
/// falsch angenommenes Format ergaebe Koordinaten irgendwo auf der Welt statt in der
/// Schweiz, und das faellt in einer XTF nicht zwingend auf.
///
/// Unterstuetzt werden <c>LineString</c> (2) und <c>MultiLineString</c> (5); QGIS legt
/// Leitungen als MultiLineString ab. Alles andere liefert <c>null</c> — lieber keine
/// Geometrie als eine falsche.
///
/// Reine Byte-Logik ohne Dateizugriff.
/// </summary>
public static class GpkgGeometrie
{
    private const int WkbLineString = 2;
    private const int WkbMultiLineString = 5;

    /// <summary>Die Punkte des Linienzugs, oder <c>null</c>, wenn der Blob nicht passt.</summary>
    public static IReadOnlyList<XtfPunkt>? Linie(byte[]? blob)
    {
        if (blob is null || blob.Length < 8 || blob[0] != (byte)'G' || blob[1] != (byte)'P')
            return null;

        var flags = blob[3];
        var kopfIstLittle = (flags & 0x01) != 0;
        var envelopeArt = (flags >> 1) & 0x07;

        var envelopeBytes = envelopeArt switch
        {
            0 => 0,
            1 => 32,   // x, y
            2 => 48,   // x, y, z
            3 => 48,   // x, y, m
            4 => 64,   // x, y, z, m
            _ => -1
        };
        if (envelopeBytes < 0)
            return null;

        // Kopf: 2 Magic + 1 Version + 1 Flags + 4 srs_id
        var stelle = 8 + envelopeBytes;
        _ = kopfIstLittle;

        var punkte = new List<XtfPunkt>();
        return LiesWkb(blob, ref stelle, punkte, tiefe: 0) ? punkte : null;
    }

    private static bool LiesWkb(byte[] b, ref int stelle, List<XtfPunkt> punkte, int tiefe)
    {
        // Eine Geometrie darf sich nicht beliebig tief schachteln.
        if (tiefe > 2 || stelle + 5 > b.Length)
            return false;

        var little = b[stelle] == 1;
        stelle++;

        var typ = LiesUInt32(b, ref stelle, little);
        if (typ is null)
            return false;

        // Hoehere Stellen tragen Z/M-Kennzeichen; nur der Grundtyp zaehlt.
        switch (typ.Value % 1000)
        {
            case WkbLineString:
                return LiesPunkte(b, ref stelle, punkte, little);

            case WkbMultiLineString:
                var anzahl = LiesUInt32(b, ref stelle, little);
                if (anzahl is null or 0 or > 100_000)
                    return false;

                for (var i = 0; i < anzahl.Value; i++)
                {
                    if (!LiesWkb(b, ref stelle, punkte, tiefe + 1))
                        return false;
                }

                return punkte.Count > 0;

            default:
                return false;
        }
    }

    private static bool LiesPunkte(byte[] b, ref int stelle, List<XtfPunkt> punkte, bool little)
    {
        var anzahl = LiesUInt32(b, ref stelle, little);
        if (anzahl is null or 0 or > 1_000_000)
            return false;

        if (stelle + (anzahl.Value * 16) > b.Length)
            return false;

        for (var i = 0; i < anzahl.Value; i++)
        {
            var ost = LiesDouble(b, ref stelle, little);
            var nord = LiesDouble(b, ref stelle, little);
            punkte.Add(new XtfPunkt(ost, nord));
        }

        return true;
    }

    private static uint? LiesUInt32(byte[] b, ref int stelle, bool little)
    {
        if (stelle + 4 > b.Length)
            return null;

        var teil = b.AsSpan(stelle, 4);
        stelle += 4;
        return little
            ? BinaryPrimitives.ReadUInt32LittleEndian(teil)
            : BinaryPrimitives.ReadUInt32BigEndian(teil);
    }

    private static double LiesDouble(byte[] b, ref int stelle, bool little)
    {
        var teil = b.AsSpan(stelle, 8);
        stelle += 8;
        return little
            ? BinaryPrimitives.ReadDoubleLittleEndian(teil)
            : BinaryPrimitives.ReadDoubleBigEndian(teil);
    }
}
