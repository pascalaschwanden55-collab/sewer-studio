using System;
using System.Collections.Generic;
using AuswertungPro.Next.Application.WebGis;
using AuswertungPro.Next.Domain.Models;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.WebGis;

/// <summary>
/// Wartbarkeitsaudit 30.09.2026, WG-B: Eigentuemer und Betreiber fuehrt das WebGIS — in zwei Richtungen mit
/// verschiedener Bedeutung und verschiedener Feldmenge. Diese Tests halten das Verhalten JE RICHTUNG mit
/// Eigentuemer UND Betreiber fest (gruen auf dem Stand vor dem Umbau).
/// Senden (Entscheid Pascal 23.09.2026): nie ins WebGIS, gefaltet «Eigentuemer», «Eigentümer», «Betreiber».
/// Holen (Entscheid Pascal 24.09.2026 abends): der WebGIS-Wert ersetzt auch die Handeingabe — als Tabellenfeld nur
/// «Eigentuemer», der Betreiber laeuft ueber die Wurzelakte (haltung.operator / schacht.betreiber).
/// </summary>
public sealed class WebGisFuehrungsfelderTests
{
    private static WebGisLesestand SchachtStand() => new()
    {
        GlobalId = "G1", Bezeichnung = "80461",
        Felder = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [WebGisFeldkarte.SchachtZustandRef] = "103",
            [WebGisFeldkarte.SchachtSanierungsbedarfRef] = "104",
            [WebGisFeldkarte.SchachtBemerkungRef] = "",
        },
    };

    private static WebGisObjektEingabe Schacht() => new()
    {
        Objektart = WebGisObjektart.Schacht, Bezeichnung = "80461", RecordId = Guid.NewGuid(), Zustandsklasse = "3",
    };

    // ---- Die beiden Regeln selbst (nach dem Umbau; Feldmengen je Richtung wie vorher) ----

    [Theory]
    [InlineData("Eigentuemer", true)]
    [InlineData("Eigentümer", true)]
    [InlineData("eigentümer", true)]
    [InlineData("Betreiber", true)]
    [InlineData("BETREIBER", true)]
    [InlineData("Datenherr", false)]
    [InlineData("Status", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Nie_senden_gilt_fuer_eigentuemer_und_betreiber_gefaltet(string? feld, bool erwartet)
        => Assert.Equal(erwartet, WebGisFuehrungsfelder.NieSenden(feld));

    [Theory]
    [InlineData("Eigentuemer", true)]
    [InlineData("Eigentümer", false)]
    [InlineData("eigentuemer", false)]
    [InlineData("Betreiber", false)]
    [InlineData("haltung.operator", false)]
    [InlineData("schacht.betreiber", false)]
    [InlineData("Status", false)]
    public void Holen_ueberschreibt_hand_nur_beim_eigentuemer_als_tabellenfeld(string feld, bool erwartet)
        => Assert.Equal(erwartet, WebGisFuehrungsfelder.HolenUeberschreibtHand(feld));

    // ---- Senden ----

    [Fact]
    public void Senden_eigentuemer_und_betreiber_von_hand_gehen_nie_ins_webgis()
    {
        var e = Schacht();
        e.Handwerte["Eigentuemer"] = "Abwasser Uri";
        e.Handwerte["Eigentümer"] = "Gemeinde";
        e.Handwerte["Betreiber"] = "Kanton";

        var pos = WebGisExportPlanBuilder.Baue(e, SchachtStand());

        Assert.Empty(pos.Aenderungen);
        Assert.Contains("Eigentuemer: im WebGIS führend — «Abwasser Uri» wird nicht übertragen.", pos.Hinweise);
        Assert.Contains("Eigentümer: im WebGIS führend — «Gemeinde» wird nicht übertragen.", pos.Hinweise);
        Assert.Contains("Betreiber: im WebGIS führend — «Kanton» wird nicht übertragen.", pos.Hinweise);
    }

    [Fact]
    public void Senden_eigentuemer_und_betreiber_der_kanalfirma_werden_kein_vorschlag()
    {
        var e = Schacht();
        e.Kanalfirmenwerte["Eigentümer"] = "Privat";
        e.Kanalfirmenwerte["Betreiber"] = "Privat";

        var pos = WebGisExportPlanBuilder.Baue(e, SchachtStand());

        Assert.Empty(pos.Aenderungen);
        Assert.Empty(pos.Vorschlaege);
        Assert.DoesNotContain(pos.Hinweise, h => h.Contains("Eigentümer", StringComparison.Ordinal) || h.Contains("Betreiber", StringComparison.Ordinal));
    }

    // ---- Holen ----

    private static (Project Projekt, HaltungRecord Haltung) Projekt()
    {
        var h = new HaltungRecord();
        h.SetFieldValue(FieldKeys.HoldingName, "H1", FieldSource.Manual, false);
        var p = new Project();
        p.Data.Add(h);
        return (p, h);
    }

    private static WebGisImportPlan Plan(HaltungRecord h, params WebGisImportAenderung[] aenderungen)
    {
        var pos = new WebGisImportPosition { Objektart = WebGisObjektart.Haltung, Bezeichnung = "H1", RecordId = h.Id, GlobalId = "G1" };
        pos.Aenderungen.AddRange(aenderungen);
        var plan = new WebGisImportPlan();
        plan.Positionen.Add(pos);
        return plan;
    }

    [Fact]
    public void Holen_ersetzt_eigentuemer_und_betreiber_von_hand()
    {
        var (p, h) = Projekt();
        h.SetFieldValue(FieldKeys.Owner, "Privat", FieldSource.Manual, true);
        p.Objektakten.Add(new ObjektAkte { Id = h.Id, Art = "haltung" });
        p.Objektakten[0].Werte["haltung.operator"] = new ObjektFeldWert { Text = "Privat (Privat)", VonHand = true };

        var n = WebGisImportUseCase.Uebernimm(Plan(h,
            new WebGisImportAenderung { Feld = FieldKeys.Owner, Alt = "Privat", Neu = "AWU_von_privat", Grund = "Test" },
            new WebGisImportAenderung { Feld = "haltung.operator", Alt = "Privat (Privat)", Neu = "AWU_von_privat (Abwasserverband)", Grund = "Test" }), p);

        Assert.Equal(1, n);
        Assert.Equal("AWU_von_privat", h.GetFieldValue(FieldKeys.Owner));
        Assert.False(h.FieldMeta[FieldKeys.Owner].UserEdited);
        Assert.Equal("AWU_von_privat (Abwasserverband)", p.Objektakten[0].Werte["haltung.operator"].Text);
        Assert.False(p.Objektakten[0].Werte["haltung.operator"].VonHand);
    }

    [Fact]
    public void Holen_ersetzt_als_tabellenfeld_nur_den_eigentuemer_nicht_den_betreiber()
    {
        // Heute: «Betreiber» und «Eigentümer» (mit Umlaut) als Tabellenfeld eines Holen-Plans werden nicht geschrieben,
        // auch keine Handeingabe ersetzt. Der Betreiber kommt beim Holen nur ueber die Wurzelakte.
        var (p, h) = Projekt();
        h.SetFieldValue("Betreiber", "Privat", FieldSource.Manual, true);

        var n = WebGisImportUseCase.Uebernimm(Plan(h,
            new WebGisImportAenderung { Feld = "Betreiber", Alt = "Privat", Neu = "AWU_von_privat", Grund = "Test" },
            new WebGisImportAenderung { Feld = "Eigentümer", Alt = null, Neu = "AWU_von_privat", Grund = "Test" }), p);

        Assert.Equal(0, n);
        Assert.Equal("Privat", h.GetFieldValue("Betreiber"));
        Assert.True(h.FieldMeta["Betreiber"].UserEdited);
        Assert.Equal(string.Empty, h.GetFieldValue(FieldKeys.Owner));
    }

    [Fact]
    public void Holen_ersetzt_eine_andere_handeingabe_nicht()
    {
        // Gegenprobe: nur die Fuehrungsfelder weichen; eine andere Handeingabe bleibt.
        var (p, h) = Projekt();
        h.SetFieldValue(FieldKeys.OperatingStatus, "Ausser Betrieb", FieldSource.Manual, true);

        var n = WebGisImportUseCase.Uebernimm(Plan(h,
            new WebGisImportAenderung { Feld = FieldKeys.OperatingStatus, Alt = "Ausser Betrieb", Neu = "In Betrieb", Grund = "Test" }), p);

        Assert.Equal(0, n);
        Assert.Equal("Ausser Betrieb", h.GetFieldValue(FieldKeys.OperatingStatus));
    }
}
