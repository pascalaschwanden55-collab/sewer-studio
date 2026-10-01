using AuswertungPro.Next.Application.Lookup;
using AuswertungPro.Next.Application.UseCases;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Tests.Lookup;

public sealed class GeoShopNamensschutzTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Einzelabgleich_sperrt_doppelte_Namen_im_ganzen_Projekt(bool schacht)
    {
        var p = new Project();
        var ziel = Hinzu(p, schacht, "A");
        Hinzu(p, schacht, " a ");

        var ergebnis = GeoShopEinzelErgaenzung.Plane(ziel, new Leser(schacht), "test.xtf");

        Assert.False(ergebnis.HatAenderungen);
        Assert.Contains("mehrfach", ergebnis.Text);
        Assert.Null(ziel.Kennungen());
        Assert.Empty(p.Objektakten);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void Nachtraegliche_Dublette_oder_Umbenennung_sperrt_die_Uebernahme(bool schacht, bool umbenennen)
    {
        var p = new Project();
        var ziel = Hinzu(p, schacht, "A");
        var anderes = Hinzu(p, schacht, "B");
        var ergebnis = GeoShopEinzelErgaenzung.Plane(ziel, new Leser(schacht), "test.xtf");
        Assert.True(ergebnis.HatAenderungen);
        if (umbenennen)
        {
            if (anderes.Datensatz is HaltungRecord h) h.SetFieldValue(FieldKeys.HoldingName, "A", FieldSource.Manual, true);
            if (anderes.Datensatz is SchachtRecord s) s.SetFieldValue("Schachtnummer", "A", FieldSource.Manual, true);
        }
        else Hinzu(p, schacht, "A");

        Assert.Throws<InvalidOperationException>(() => GeoShopEinzelErgaenzung.WendeAn(ergebnis, ziel));
        Assert.Null(ziel.Kennungen());
        Assert.Empty(p.Objektakten);
    }

    [Fact]
    public void Gleichnamiger_Schacht_sperrt_eine_eindeutige_Haltung_nicht()
    {
        var p = new Project();
        var ziel = Hinzu(p, false, "A");
        Hinzu(p, true, "A");
        var ergebnis = GeoShopEinzelErgaenzung.Plane(ziel, new Leser(false), "test.xtf");
        Assert.True(ergebnis.HatAenderungen);
        Assert.Equal(1, GeoShopEinzelErgaenzung.WendeAn(ergebnis, ziel));
        Assert.NotNull(ziel.Kennungen());
    }

    private static GeoShopZiel Hinzu(Project p, bool schacht, string name)
    {
        if (schacht)
        {
            var s = new SchachtRecord(); s.SetFieldValue("Schachtnummer", name, FieldSource.Manual, true);
            p.SchaechteData.Add(s); return GeoShopZiel.Fuer(s, p);
        }
        var h = new HaltungRecord(); h.SetFieldValue(FieldKeys.HoldingName, name, FieldSource.Manual, true);
        p.Data.Add(h); return GeoShopZiel.Fuer(h, p);
    }

    private sealed class Leser(bool schacht) : IGeoShopLeser
    {
        public GeoShopBestand Lies(string datei, BauteilArt art, IReadOnlyCollection<string> namen, CancellationToken ct = default)
            => new(art, datei, [new GeoShopBauteil("A", schacht
                ? KatasterKennung.FuerSchacht("A", null, "chTEST0000000006", "chTEST0000000007")
                : KatasterKennung.FuerHaltung("A", null, "chTEST0000000001", "chTEST0000000002",
                    "chTEST0000000003", "A_von", "chTEST0000000004", "A_nach", "chTEST0000000005", "Kreisprofil"),
                new Dictionary<string, string>())]);
    }
}
