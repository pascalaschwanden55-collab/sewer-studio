using System;
using System.Linq;
using AuswertungPro.Next.Application.WebGis;
using AuswertungPro.Next.Domain.Models;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.WebGis;

/// <summary>
/// Eigene Spalten ohne WebGIS-Bezug (Wunsch Pascal 28.09.2026: «sind von mir, wichtig, haben nichts mit dem WebGIS zu
/// tun») werden weder geholt noch gesendet — auch nicht, wenn ein Plan sie faelschlich nennt.
/// </summary>
public sealed class WebGisEigeneSpaltenTests
{
    private const string EigeneSpalte = "Meine_Notiz_Pascal";

    [Fact]
    public void Holen_beschreibt_eine_eigene_spalte_nie_auch_nicht_ueber_einen_falschen_plan()
    {
        var h = new HaltungRecord();
        h.SetFieldValue(FieldKeys.HoldingName, "H1", FieldSource.Manual, false);
        h.SetFieldValue(EigeneSpalte, "wichtig", FieldSource.Manual, true);
        var leer = new HaltungRecord();
        leer.SetFieldValue(FieldKeys.HoldingName, "H2", FieldSource.Manual, false);
        var p = new Project();
        p.Data.Add(h);
        p.Data.Add(leer);

        var plan = new WebGisImportPlan();
        foreach (var (r, alt) in new[] { (h, "wichtig"), (leer, (string?)null) })
        {
            var pos = new WebGisImportPosition
            {
                Objektart = WebGisObjektart.Haltung, Bezeichnung = r.GetFieldValue(FieldKeys.HoldingName), RecordId = r.Id, GlobalId = "G" + r.Id,
            };
            pos.Aenderungen.Add(new WebGisImportAenderung { Feld = EigeneSpalte, Alt = alt, Neu = "aus dem WebGIS", Grund = "Test" });
            plan.Positionen.Add(pos);
        }

        Assert.Equal(0, WebGisImportUseCase.Uebernimm(plan, p));
        Assert.Equal("wichtig", h.GetFieldValue(EigeneSpalte));
        Assert.True(string.IsNullOrEmpty(leer.GetFieldValue(EigeneSpalte)));
    }

    [Fact]
    public void Nur_bekannte_webgis_felder_sind_fuer_das_holen_freigegeben()
    {
        Assert.False(WebGisImportPlanBuilder.DarfTabellenfeldSchreiben(EigeneSpalte));
        Assert.False(WebGisImportPlanBuilder.DarfTabellenfeldSchreiben("Kosten"));
        Assert.True(WebGisImportPlanBuilder.DarfTabellenfeldSchreiben(WebGisImportPlanBuilder.FeldBaujahr));
        Assert.All(WebGisHandwertKarte.Felder, f => Assert.True(WebGisImportPlanBuilder.DarfTabellenfeldSchreiben(f.SewerStudioFeld)));
    }

    [Fact]
    public void Senden_schickt_eine_eigene_spalte_nie_ins_webgis()
    {
        var e = new WebGisObjektEingabe
        {
            Objektart = WebGisObjektart.Haltung, Bezeichnung = "H1", RecordId = Guid.NewGuid(),
            Handwerte = { [EigeneSpalte] = "wichtig" },
            Werte = { [EigeneSpalte] = "wichtig" },
        };
        var stand = new WebGisLesestand
        {
            GlobalId = "G1", Bezeichnung = "H1",
            Felder = new System.Collections.Generic.Dictionary<string, string?>(StringComparer.Ordinal),
        };

        var pos = WebGisExportPlanBuilder.Baue(e, stand);

        Assert.DoesNotContain(pos.Aenderungen, a => a.Neu == "wichtig" || a.Feld == EigeneSpalte);
        Assert.DoesNotContain(pos.Vorschlaege, v => v.Feld == EigeneSpalte);
        Assert.DoesNotContain(pos.Vergleich, v => v.Feld == EigeneSpalte && v.WirdGeschrieben);
    }
}
