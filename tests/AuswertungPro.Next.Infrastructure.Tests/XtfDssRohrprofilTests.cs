using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import.Xtf;
using static AuswertungPro.Next.Infrastructure.Tests.XtfDssExportTests;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Buerglen 14.09.2026: Alle 19 Haltungen bekamen ein neues eigenes Rohrprofil, obwohl es dem
/// gemeinsamen Originalprofil «Kreisprofil 1.00» glich. Ausloeser war eine DN aus dem alten Import.
/// Ein gleiches Profil bleibt der Originalverweis; nur ein wirklich anderes Profil wird neu angelegt.
/// </summary>
public sealed class XtfDssRohrprofilTests
{
    private const string Profil = "chTEST0000000041";

    private static Project MitOriginalprofil(string dn, string breite, string typ)
    {
        var p = Projekt();
        var h = p.Data[0];
        p.Objektakten[0].Quellen.Add(Quelle("Rohrprofil", Profil,
            new() { ["Bezeichnung"] = "Kreisprofil 1.00", ["HoehenBreitenverhaeltnis"] = "1.00", ["Profiltyp"] = "Kreisprofil" }));
        p.Objektakten[0].Quellen.Single(q => q.Klasse == "Haltung").Referenzen["RohrprofilRef"] = Profil;
        h.SetFieldValue(FieldKeys.NominalDiameterMm, dn, FieldSource.Legacy, false);
        h.SetFieldValue(FieldKeys.ProfileType, typ, FieldSource.Kataster, false);
        h.SetFieldValue(FieldKeys.ClearWidthMm, breite, FieldSource.Kataster, false);
        return p;
    }

    [Fact]
    public void Gleiches_Profil_behaelt_den_Originalverweis_und_erzeugt_keine_Kopie()
    {
        var p = MitOriginalprofil("300", "300", "Kreisprofil");
        WithExport(p, doc =>
        {
            Assert.Equal(Profil, Objekt(doc, "Haltung").Elements().Single(e => e.Name.LocalName == "RohrprofilRef").Attribute("REF")!.Value);
            Assert.Single(doc.Descendants().Where(e => e.Name.LocalName.EndsWith(".Rohrprofil", StringComparison.Ordinal)));
        });
        var delta = new XtfNeuExportService().Erzeuge(new(p, "", NurPruefen: true, NurAenderungen: true));
        Assert.True(delta.Ok, delta.Fehler);
        Assert.DoesNotContain("RohrprofilRef", delta.Bericht);
    }

    [Fact]
    public void Anderes_Profil_wird_weiterhin_als_eigenes_Profil_angelegt()
    {
        var p = MitOriginalprofil("300", "200", "Eiprofil");
        p.Data[0].SetFieldValue(FieldKeys.ProfileType, "Eiprofil", FieldSource.Manual, true);
        WithExport(p, doc =>
        {
            var neu = Objekt(doc, "Haltung").Elements().Single(e => e.Name.LocalName == "RohrprofilRef").Attribute("REF")!.Value;
            Assert.NotEqual(Profil, neu);
            Assert.Equal("Eiprofil", Objekt(doc, "Rohrprofil", neu).Elements().Single(e => e.Name.LocalName == "Profiltyp").Value);
            Assert.Equal("Kreisprofil", Objekt(doc, "Rohrprofil", Profil).Elements().Single(e => e.Name.LocalName == "Profiltyp").Value);
        });
    }
}
