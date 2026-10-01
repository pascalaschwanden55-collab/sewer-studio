using AuswertungPro.Next.Application.Reports;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Schachtgrafik Stammkarte: Koten aus den GeoShop-Objektakten. Die Werte sind die des
/// Schachts 80409 im Kataster (Deckel 498.620, Sohle 495.150, Einlauf 495.160, Auslauf 495.140).
/// </summary>
public sealed class SchachtKotenQuelleTests
{
    [Fact]
    public void Liest_Deckel_Sohle_und_die_Koten_je_Haltung()
    {
        var (projekt, schacht, haltungen) = Beispiel();

        var koten = SchachtKotenQuelle.Lies(projekt, schacht, haltungen);

        Assert.NotNull(koten);
        Assert.Equal(498.620m, koten!.Deckel);
        Assert.Equal(495.150m, koten.Sohle);
        Assert.Equal(3.470m, koten.Tiefe);
        Assert.Equal(495.160m, koten.JeHaltung["80547-80409"]);
        Assert.Equal(495.140m, koten.JeHaltung["80409-80538"]);
        Assert.False(koten.JeHaltung.ContainsKey("80467-80409"));
    }

    [Fact]
    public void Ohne_Objektakten_gibt_es_nichts()
    {
        var (projekt, schacht, haltungen) = Beispiel();
        projekt.Objektakten.Clear();

        Assert.Null(SchachtKotenQuelle.Lies(projekt, schacht, haltungen));
        Assert.Null(SchachtKotenQuelle.Lies(null, schacht, haltungen));
    }

    [Fact]
    public void Zwei_Deckel_ohne_Hauptdeckel_lassen_die_Deckelkote_offen()
    {
        var (projekt, schacht, haltungen) = Beispiel();
        projekt.Objektakten.Add(new ObjektAkte
        {
            Art = "deckel",
            Bezuege = { schacht.Id },
            Werte = { ["deckel.hoehe"] = new ObjektFeldWert { Text = "498.900" } }
        });

        var koten = SchachtKotenQuelle.Lies(projekt, schacht, haltungen);

        Assert.Null(koten!.Deckel);
        Assert.Equal(495.150m, koten.Sohle);
        Assert.Null(koten.Tiefe);
    }

    [Fact]
    public void Der_Namensweg_ordnet_Haltungen_ohne_Schachtfelder_zu()
    {
        var (projekt, schacht, haltungen) = Beispiel();
        foreach (var h in haltungen)
        {
            h.Fields.Remove("Schacht_oben");
            h.Fields.Remove("Schacht_unten");
        }

        var koten = SchachtKotenQuelle.Lies(projekt, schacht, haltungen);

        Assert.Equal(495.160m, koten!.JeHaltung["80547-80409"]);
        Assert.Equal(495.140m, koten.JeHaltung["80409-80538"]);
    }

    private static (Project Projekt, SchachtRecord Schacht, List<HaltungRecord> Haltungen) Beispiel()
    {
        var projekt = new Project();
        var schacht = new SchachtRecord();
        schacht.SetFieldValue("Schachtnummer", "80409", FieldSource.Manual, false);
        projekt.SchaechteData.Add(schacht);
        projekt.Objektakten.Add(new ObjektAkte
        {
            Id = schacht.Id,
            Art = "schacht",
            Werte = { ["schacht.sohlenhoehe"] = new ObjektFeldWert { Text = "495.150" } }
        });
        projekt.Objektakten.Add(new ObjektAkte
        {
            Art = "deckel",
            Bezuege = { schacht.Id },
            Werte = { ["deckel.hoehe"] = new ObjektFeldWert { Text = "498.620" } }
        });

        var einlauf = Haltung("80547-80409", oben: "80547", unten: "80409");
        var auslauf = Haltung("80409-80538", oben: "80409", unten: "80538");
        var privat = Haltung("80467-80409", oben: "80467", unten: "80409");
        projekt.Data.Add(einlauf);
        projekt.Data.Add(auslauf);
        projekt.Data.Add(privat);
        projekt.Objektakten.Add(new ObjektAkte
        {
            Id = einlauf.Id,
            Art = "haltung",
            Werte =
            {
                ["haltung.fromlevel"] = new ObjektFeldWert { Text = "495.700" },
                ["haltung.tolevel"] = new ObjektFeldWert { Text = "495.160" }
            }
        });
        projekt.Objektakten.Add(new ObjektAkte
        {
            Id = auslauf.Id,
            Art = "haltung",
            Werte =
            {
                ["haltung.fromlevel"] = new ObjektFeldWert { Text = "495.140" },
                ["haltung.tolevel"] = new ObjektFeldWert { Text = "494.170" }
            }
        });
        // Der private DN-100-Anschluss hat im Kataster keine Kote: Akte ohne Werte.
        projekt.Objektakten.Add(new ObjektAkte { Id = privat.Id, Art = "haltung" });

        return (projekt, schacht, [einlauf, auslauf, privat]);
    }

    private static HaltungRecord Haltung(string name, string oben, string unten)
    {
        var h = new HaltungRecord();
        h.SetFieldValue(FieldKeys.HoldingName, name, FieldSource.Manual, false);
        h.SetFieldValue("Schacht_oben", oben, FieldSource.Manual, false);
        h.SetFieldValue("Schacht_unten", unten, FieldSource.Manual, false);
        return h;
    }
}
