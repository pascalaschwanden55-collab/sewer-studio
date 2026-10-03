using AuswertungPro.Next.Application.Export;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import.Kins;
using AuswertungPro.Next.Infrastructure.Import.Xtf.VsaKek;
using static AuswertungPro.Next.Infrastructure.Tests.XtfDssExportTests;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Testnetz T6 (Deepscan 02.10.2026): Schreibstellen, deren Klassenname in keinem Test vorkam,
/// bewahren ein Handfeld (<c>UserEdited</c>, auch bewusst leer). Je Klasse mindestens ein Test.
/// </summary>
public sealed class HandwertSchreibstellenTests
{
    // ---- KatasterFeldschutz ----

    private static FieldMetadata KatasterMeta(string feld) => new() { FieldName = feld, Source = FieldSource.Kataster };

    [Fact]
    public void KatasterFeldschutz_Handwert_wird_nie_blockiert_auch_nicht_bewusst_leer()
    {
        var meta = KatasterMeta("Material");
        Assert.False(KatasterFeldschutz.Pruefe("Material", meta, "Beton", "", FieldSource.Unknown, hand: true));
        Assert.Null(meta.Conflict);
    }

    [Fact]
    public void KatasterFeldschutz_unbekannte_Herkunft_ersetzt_keinen_Katasterwert_und_meldet_Konflikt()
    {
        var meta = KatasterMeta("Material");
        Assert.True(KatasterFeldschutz.Pruefe("Material", meta, "Beton", "PVC", FieldSource.Unknown, hand: false));
        Assert.Equal("KatasterwertGeschuetzt", meta.Conflict!["Reason"]!.GetValue<string>());
    }

    [Fact]
    public void KatasterFeldschutz_vermessene_Lage_nur_durch_Kataster_Grundbuch_oder_Hand_ersetzbar()
    {
        Assert.True(KatasterFeldschutz.Pruefe("Koordinate_East", KatasterMeta("Koordinate_East"), "2690000", "2691000", FieldSource.Xtf, hand: false));
        Assert.False(KatasterFeldschutz.Pruefe("Koordinate_East", KatasterMeta("Koordinate_East"), "2690000", "2691000", FieldSource.Kataster, hand: false));
        Assert.False(KatasterFeldschutz.Pruefe("Koordinate_East", KatasterMeta("Koordinate_East"), "2690000", "2691000", FieldSource.Xtf, hand: true));
    }

    [Fact]
    public void KatasterFeldschutz_am_Datensatz_Handwert_ersetzt_Katasterwert_unbekannte_Herkunft_nicht()
    {
        var h = new HaltungRecord();
        h.SetFieldValue(FieldKeys.PipeMaterial, "Beton", FieldSource.Kataster, userEdited: false);

        h.SetFieldValue(FieldKeys.PipeMaterial, "PVC", FieldSource.Unknown, userEdited: false);
        Assert.Equal("Beton", h.GetFieldValue(FieldKeys.PipeMaterial));

        h.SetFieldValue(FieldKeys.PipeMaterial, "", FieldSource.Manual, userEdited: true);
        Assert.Equal("", h.GetFieldValue(FieldKeys.PipeMaterial));
        Assert.True(h.FieldMeta[FieldKeys.PipeMaterial].UserEdited);
    }

    // ---- FieldMetadataKopie ----

    [Fact]
    public void FieldMetadataKopie_bewahrt_Handmarke_und_ist_unabhaengig_vom_Original()
    {
        var original = new FieldMetadata { FieldName = "X", Source = FieldSource.Manual, UserEdited = true };
        var kopie = FieldMetadataKopie.Von(original);

        Assert.True(kopie.UserEdited);
        Assert.NotSame(original, kopie);
        original.UserEdited = false;
        Assert.True(kopie.UserEdited);
    }

    [Fact]
    public void FieldMetadataKopie_Gleich_unterscheidet_die_Handmarke()
    {
        var a = new FieldMetadata { FieldName = "X", UserEdited = true };
        var b = new FieldMetadata { FieldName = "X", UserEdited = false };
        Assert.False(FieldMetadataKopie.Gleich(a, b, mitZeitstempel: false));
        Assert.True(FieldMetadataKopie.Gleich(a, FieldMetadataKopie.Von(a), mitZeitstempel: true));
    }

    // ---- HoldingExcelExportSnapshotFactory ----

    [Fact]
    public void HoldingExcelExportSnapshotFactory_Kopie_traegt_Handmarke_und_bewusst_leeren_Wert_ohne_das_Original_zu_aendern()
    {
        var projekt = new Project { Name = "P" };
        var h = new HaltungRecord();
        h.SetFieldValue(FieldKeys.PipeMaterial, "", FieldSource.Manual, userEdited: true);
        projekt.Data.Add(h);

        var kopie = HoldingExcelExportSnapshotFactory.Create(projekt);
        var kopiert = Assert.Single(kopie.Data);
        Assert.True(kopiert.FieldMeta[FieldKeys.PipeMaterial].UserEdited);
        Assert.Equal("", kopiert.GetFieldValue(FieldKeys.PipeMaterial));

        // Aenderung an der Kopie (Kostenfelder nachziehen) beruehrt das geoeffnete Projekt nicht.
        kopiert.FieldMeta[FieldKeys.PipeMaterial].UserEdited = false;
        kopiert.Fields[FieldKeys.PipeMaterial] = "Beton";
        Assert.True(h.FieldMeta[FieldKeys.PipeMaterial].UserEdited);
        Assert.Equal("", h.GetFieldValue(FieldKeys.PipeMaterial));
    }

    // ---- KinsHoldingNameNormalizer ----

    [Fact]
    public void KinsHoldingNameNormalizer_benennt_einen_von_Hand_gesetzten_Namen_nicht_um()
    {
        var projekt = new Project { Name = "P" };
        var h = new HaltungRecord();
        h.SetFieldValue(FieldKeys.HoldingName, "10", FieldSource.Manual, userEdited: true);
        h.SetFieldValue("Schacht_oben", "A", FieldSource.Xtf, userEdited: false);
        h.SetFieldValue("Schacht_unten", "B", FieldSource.Xtf, userEdited: false);
        projekt.Data.Add(h);

        var ergebnis = KinsHoldingNameNormalizer.Apply(projekt);

        Assert.Equal("10", h.GetFieldValue(FieldKeys.HoldingName));
        Assert.True(h.FieldMeta[FieldKeys.HoldingName].UserEdited);
        Assert.Equal(0, ergebnis.Umbenannt);
    }

    [Fact]
    public void KinsHoldingNameNormalizer_entfernt_kein_Duplikat_mit_Handwert()
    {
        var projekt = new Project { Name = "P" };
        var normalisiert = new HaltungRecord();
        normalisiert.SetFieldValue(FieldKeys.HoldingName, "A-B", FieldSource.Xtf, userEdited: false);
        normalisiert.SetFieldValue(KinsHoldingNameNormalizer.BezeichnungsFeld, "10", FieldSource.Legacy, userEdited: false);
        var frisch = new HaltungRecord();
        frisch.SetFieldValue(FieldKeys.HoldingName, "10", FieldSource.Xtf, userEdited: false);
        frisch.SetFieldValue("Schacht_oben", "A", FieldSource.Xtf, userEdited: false);
        frisch.SetFieldValue("Schacht_unten", "B", FieldSource.Xtf, userEdited: false);
        frisch.SetFieldValue(FieldKeys.PipeMaterial, "", FieldSource.Manual, userEdited: true);
        projekt.Data.Add(normalisiert);
        projekt.Data.Add(frisch);

        var ergebnis = KinsHoldingNameNormalizer.Apply(projekt);

        Assert.Equal(0, ergebnis.DuplikateEntfernt);
        Assert.Contains(frisch, projekt.Data);
    }

    // ---- DssProfilBearbeitung ----

    [Fact]
    public void DssProfilBearbeitung_bewusst_leere_Profilbreite_wird_geliefert_statt_des_alten_Verhaeltnisses()
    {
        var p = Projekt();
        var h = p.Data[0];
        const string profil = "chTEST0000000020";
        p.Objektakten[0].Quellen.Single(q => q.Klasse == "Haltung").Referenzen["RohrprofilRef"] = profil;
        p.Objektakten[0].Quellen.Add(Quelle("Rohrprofil", profil, new() { ["Bezeichnung"] = "Ei", ["Profiltyp"] = "Eiprofil", ["HoehenBreitenverhaeltnis"] = "1.50" }));
        h.SetFieldValue(FieldKeys.ProfileType, "Eiprofil", FieldSource.Manual, true);
        h.SetFieldValue(FieldKeys.ClearWidthMm, "", FieldSource.Manual, true);

        WithExport(p, doc =>
        {
            var neu = Objekt(doc, "Haltung").Elements().Single(e => e.Name.LocalName == "RohrprofilRef").Attribute("REF")!.Value;
            Assert.NotEqual(profil, neu);
            Assert.DoesNotContain("1.50", Objekt(doc, "Rohrprofil", neu).Elements().Select(e => e.Value));
        });
        Assert.True(h.FieldMeta[FieldKeys.ClearWidthMm].UserEdited);
    }

    // ---- VsaKekAbbildung ----

    [Fact]
    public void VsaKekAbbildung_leert_ein_von_Hand_gesetztes_Link_G_nicht()
    {
        var ziel = new HaltungRecord();
        ziel.SetFieldValue("Link_G", @"Videos\hand.mp4", FieldSource.Xtf, userEdited: true);

        VsaKekAbbildung.EntferneVeraltetesGegenvideo(ziel, new HaltungRecord());

        Assert.Equal(@"Videos\hand.mp4", ziel.GetFieldValue("Link_G"));
    }

    [Fact]
    public void VsaKekAbbildung_leert_ein_automatisches_XTF_Link_G_ohne_Gegenbefahrung()
    {
        var ziel = new HaltungRecord();
        ziel.SetFieldValue("Link_G", @"Videos\auto.mp4", FieldSource.Xtf, userEdited: false);

        VsaKekAbbildung.EntferneVeraltetesGegenvideo(ziel, new HaltungRecord());

        Assert.Equal("", ziel.GetFieldValue("Link_G"));
    }
}
