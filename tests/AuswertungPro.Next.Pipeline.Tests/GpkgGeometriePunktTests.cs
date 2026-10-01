using System.Buffers.Binary;
using AuswertungPro.Next.Application.Xtf;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>GeoPackage-Blob mit einem Punkt (Schachtpunkt der QGIS-Kopie) lesen.</summary>
public sealed class GpkgGeometriePunktTests
{
    [Fact]
    public void Ein_Punkt_wird_gelesen()
    {
        var blob = Blob(WkbPunkt(2692630.471, 1192370.448));

        var punkt = GpkgGeometrie.Punkt(blob);

        Assert.NotNull(punkt);
        Assert.Equal(2692630.471, punkt!.Ost, 3);
        Assert.Equal(1192370.448, punkt.Nord, 3);
    }

    [Fact]
    public void Ein_Punkt_mit_Hoehe_liefert_Ost_und_Nord()
    {
        var blob = Blob(WkbPunkt(10, 20, z: 495.15));

        var punkt = GpkgGeometrie.Punkt(blob);

        Assert.Equal(new XtfPunkt(10, 20), punkt);
    }

    [Fact]
    public void Ein_Punkt_mit_Envelope_wird_gelesen()
    {
        var blob = Blob(WkbPunkt(1, 2), envelopeArt: 1);

        Assert.Equal(new XtfPunkt(1, 2), GpkgGeometrie.Punkt(blob));
    }

    [Fact]
    public void Eine_Linie_ist_kein_Punkt_und_umgekehrt()
    {
        var linie = Blob(WkbLinie((0, 0), (5, 5)));

        Assert.Null(GpkgGeometrie.Punkt(linie));
        Assert.Null(GpkgGeometrie.Linie(Blob(WkbPunkt(1, 1))));
        Assert.Equal(2, GpkgGeometrie.Linie(linie)!.Count);
    }

    [Fact]
    public void Muell_liefert_null()
    {
        Assert.Null(GpkgGeometrie.Punkt(null));
        Assert.Null(GpkgGeometrie.Punkt([1, 2, 3]));
        Assert.Null(GpkgGeometrie.Punkt("GPxx"u8.ToArray()));
    }

    internal static byte[] Blob(byte[] wkb, int envelopeArt = 0)
    {
        var envelopeBytes = envelopeArt switch { 0 => 0, 1 => 32, 2 => 48, 3 => 48, 4 => 64, _ => 0 };
        var kopf = new byte[8 + envelopeBytes];
        kopf[0] = (byte)'G';
        kopf[1] = (byte)'P';
        kopf[2] = 0;
        kopf[3] = (byte)(0x01 | (envelopeArt << 1));
        BinaryPrimitives.WriteInt32LittleEndian(kopf.AsSpan(4, 4), 2056);
        return [.. kopf, .. wkb];
    }

    internal static byte[] WkbPunkt(double x, double y, double? z = null)
    {
        var typ = z is null ? 1u : 1001u;
        var b = new byte[1 + 4 + 16 + (z is null ? 0 : 8)];
        b[0] = 1;
        BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(1, 4), typ);
        BinaryPrimitives.WriteDoubleLittleEndian(b.AsSpan(5, 8), x);
        BinaryPrimitives.WriteDoubleLittleEndian(b.AsSpan(13, 8), y);
        if (z is { } hoehe)
            BinaryPrimitives.WriteDoubleLittleEndian(b.AsSpan(21, 8), hoehe);
        return b;
    }

    internal static byte[] WkbLinie(params (double X, double Y)[] punkte)
    {
        var b = new byte[1 + 4 + 4 + punkte.Length * 16];
        b[0] = 1;
        BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(1, 4), 2);
        BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(5, 4), (uint)punkte.Length);
        for (var i = 0; i < punkte.Length; i++)
        {
            BinaryPrimitives.WriteDoubleLittleEndian(b.AsSpan(9 + i * 16, 8), punkte[i].X);
            BinaryPrimitives.WriteDoubleLittleEndian(b.AsSpan(17 + i * 16, 8), punkte[i].Y);
        }

        return b;
    }
}
