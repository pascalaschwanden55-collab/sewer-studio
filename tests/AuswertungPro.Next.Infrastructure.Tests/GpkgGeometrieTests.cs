using AuswertungPro.Next.Application.Xtf;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Der Linienzug aus einem GeoPackage-Blob. Gemessen an echten Bytes aus der lokalen
/// QGIS-Kopie des Abwassernetzes.
/// </summary>
public sealed class GpkgGeometrieTests
{
    /// <summary>
    /// Die Haltung <c>u-80401</c> aus "Leitungen Lokal.gpkg", unveraendert.
    /// Kopf-Flags 0x03: kleiner Endian, Envelope mit x/y (32 Byte). Danach ein
    /// MultiLineString mit einer Teilgeometrie aus drei Punkten.
    /// </summary>
    private const string EchteHaltung =
        "4750000308080000560E2D72FF8A444175931864018B4441DF4F8DB7BC3132415A643B3FC3313241" +
        "01050000000100000001020000000300000075931864018B44415A643B3FC33132417F6ABCD4008B" +
        "444183C0CA41C0313241560E2D72FF8A4441DF4F8DB7BC313241";

    [Fact]
    public void Ein_echter_Blob_ergibt_die_erwarteten_Landeskoordinaten()
    {
        var punkte = GpkgGeometrie.Linie(Convert.FromHexString(EchteHaltung));

        Assert.NotNull(punkte);
        Assert.Equal(3, punkte!.Count);
        Assert.Equal(2692610.782, punkte[0].Ost, 3);
        Assert.Equal(1192387.247, punkte[0].Nord, 3);
        Assert.Equal(2692606.892, punkte[2].Ost, 3);
        Assert.Equal(1192380.717, punkte[2].Nord, 3);
    }

    [Fact]
    public void Die_Koordinaten_liegen_im_Schweizer_Bereich()
    {
        // LV95 laeuft in der Schweiz von rund 2'480'000 bis 2'840'000 Ost und
        // 1'070'000 bis 1'300'000 Nord. Ein falsch gelesenes Byteformat ergaebe
        // Werte irgendwo auf der Welt — das faellt in einer XTF nicht zwingend auf.
        var punkte = GpkgGeometrie.Linie(Convert.FromHexString(EchteHaltung))!;

        Assert.All(punkte, p =>
        {
            Assert.InRange(p.Ost, 2_480_000, 2_840_000);
            Assert.InRange(p.Nord, 1_070_000, 1_300_000);
        });
    }

    [Fact]
    public void Die_Ausgabe_traegt_drei_Nachkommastellen_mit_Punkt()
    {
        // In der XTF steht immer der Punkt als Dezimaltrenner, unabhaengig von der
        // Spracheinstellung des Rechners.
        var punkt = GpkgGeometrie.Linie(Convert.FromHexString(EchteHaltung))![0];

        Assert.Equal("2692610.782", punkt.OstText);
        Assert.Equal("1192387.247", punkt.NordText);
    }

    [Theory]
    [InlineData("")]                    // leer
    [InlineData("00")]                  // zu kurz
    [InlineData("58500003080800005")]   // falsche Kennung ("XP" statt "GP")
    public void Ein_unpassender_Blob_liefert_nichts(string hex)
    {
        var bytes = hex.Length % 2 == 0 ? Convert.FromHexString(hex) : Convert.FromHexString(hex + "0");

        Assert.Null(GpkgGeometrie.Linie(bytes));
    }

    [Fact]
    public void Ohne_Blob_liefert_der_Leser_nichts()
        => Assert.Null(GpkgGeometrie.Linie(null));

    [Fact]
    public void Ein_abgeschnittener_Blob_liefert_nichts_statt_halber_Punkte()
    {
        var voll = Convert.FromHexString(EchteHaltung);
        var halb = voll[..(voll.Length - 12)];

        Assert.Null(GpkgGeometrie.Linie(halb));
    }

    [Fact]
    public void Ein_Punkt_statt_einer_Linie_wird_abgewiesen()
    {
        // Typ 1 ist WKB-Point. Eine Haltung ist kein Punkt; lieber keine Geometrie
        // als eine, die etwas anderes meint.
        var blob = Convert.FromHexString("47500003" + new string('0', 8) + "0101000000" + new string('0', 32));

        Assert.Null(GpkgGeometrie.Linie(blob));
    }
}
