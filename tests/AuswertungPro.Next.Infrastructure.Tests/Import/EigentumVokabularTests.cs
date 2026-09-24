using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

/// <summary>
/// Das Eigentum entscheidet in beiden Excel-Berichten ueber die Farbe. Die Vorlagen
/// pruefen auf exakte Gleichheit — seit 2026-08-31 tragen sie je eine Regel fuer den
/// amtlichen Begriff UND fuer die Kurzform: ="Abwasser Uri", ="AWU", ="Kanton Uri",
/// ="Kanton", ="Bund", ="Gemeinde", ="Privat".
///
/// Deshalb darf der eingelesene Wert stehen bleiben. Frueher uebersetzte das Vokabular
/// "Abwasser Uri" nach "AWU", weil nur die Kurzform gefaerbt wurde; das schrieb eine
/// Kundenangabe um. Gespeicherte Kurzformen aus Altprojekten bleiben gueltig.
/// </summary>
public sealed class EigentumVokabularTests
{
    [Theory]
    [InlineData("Abwasser Uri", "Abwasser Uri")]
    [InlineData("abwasser uri", "Abwasser Uri")]
    [InlineData("Abwasser Uri (AWU)", "Abwasser Uri")]
    [InlineData("AWU", "Abwasser Uri")]
    [InlineData("Kanton Uri", "Kanton Uri")]
    [InlineData("Kanton", "Kanton Uri")]
    [InlineData("Privat", "Privat")]
    [InlineData("privat", "Privat")]
    [InlineData("Gemeinde", "Gemeinde")]
    [InlineData("Bund", "Bund")]
    public void Bekannte_Schreibweisen_werden_zum_amtlichen_Begriff(string gelesen, string erwartet)
    {
        Assert.Equal(erwartet, EigentumVokabular.Normalisieren(gelesen));
    }

    [Fact]
    public void Jeder_Begriff_der_App_faerbt_die_Excel_Zelle()
    {
        // Der eigentliche Punkt: Was das Vokabular ausgibt, muss die Vorlage faerben.
        // Geprueft wird gegen ExcelReportStyle.Eigentuemer und nicht gegen eine hier
        // abgeschriebene Liste — dieser Vertrag ist seinerseits an die ausgelieferte
        // Datei gebunden (Kostenblock_enthaelt_jeden_aufgefuehrten_Eigentuemer liest
        // die SUMIF-Formeln aus Haltungen.xlsx). So faellt eine spaetere Aenderung an
        // der Vorlage hier auf, statt still eine farblose Spalte zu erzeugen.
        var gefaerbt = Next.Infrastructure.Export.Excel.ExcelReportStyle.Eigentuemer
            .Select(r => r.Wert)
            .ToArray();

        foreach (var wert in EigentumVokabular.Auswahl.Where(w => w.Length > 0))
            Assert.Contains(wert, gefaerbt);

        // Und die Gegenrichtung: Jede Farbe der Vorlage muss erreichbar bleiben.
        // Die Kurzformen stehen nicht mehr zur Auswahl, sind aber weiterhin
        // lesbar — ihr normalisierter Begriff traegt dieselbe Farbe. Seit
        // 24.09.2026 faerbt die Vorlage auch die WebGIS-Namen, die das Holen setzt.
        foreach (var wert in gefaerbt)
            Assert.True(
                EigentumVokabular.Auswahl.Contains(EigentumVokabular.Normalisieren(wert))
                || WebGisOrganisationen.Finde(wert) is not null,
                $"\"{wert}\" ist weder Auswahl noch WebGIS-Organisation.");
    }

    [Fact]
    public void Ein_unbekannter_Eigentuemer_bleibt_lesbar_stehen()
    {
        // Eine Korporation oder Genossenschaft ist eine echte Angabe. Sie zu loeschen
        // waere schlimmer, als sie ohne Farbe stehen zu lassen.
        Assert.Equal("Korporation Uri", EigentumVokabular.Normalisieren("Korporation Uri"));
        Assert.Equal("", EigentumVokabular.Normalisieren(null));
        Assert.Equal("", EigentumVokabular.Normalisieren("   "));
    }

    [Fact]
    public void Der_AWU_Filter_erkennt_den_normalisierten_Wert_weiterhin()
    {
        // OwnershipAwuFilter entscheidet, welche Schaechte ins NPK-135-Leistungs-
        // verzeichnis kommen. Er darf durch die Uebersetzung nichts verlieren.
        Assert.True(Next.Infrastructure.Costs.OwnershipAwuFilter.IsAwu(
            EigentumVokabular.Normalisieren("Abwasser Uri")));
        Assert.True(Next.Infrastructure.Costs.OwnershipAwuFilter.IsAwu(
            EigentumVokabular.Normalisieren("AWU")));
        Assert.False(Next.Infrastructure.Costs.OwnershipAwuFilter.IsAwu(
            EigentumVokabular.Normalisieren("Privat")));
    }

    // Der Name geht zeichengleich in die XTF — nur der Typ wird bestimmt. Gemessen am
    // Abwassernetz des Kantons Uri: 27 Eigentuemerwerte plus der leere.
    [Theory]
    [InlineData("Abwasser Uri", "Abwasserverband")]
    [InlineData("AWU", "Abwasserverband")]
    [InlineData("Kanton Uri", "Kanton")]
    [InlineData("Privat", "Privat")]
    [InlineData("unbekannt", "Privat")]
    [InlineData("ASTRA - Bundesamt für Strassen", "Bund")]
    [InlineData("Korporation Uri", "Genossenschaft_Korporation")]
    [InlineData("Meliorationsgenossenschaft Reussebene Uri", "Genossenschaft_Korporation")]
    [InlineData("Meliorationsgesellschaft Seedorf", "Genossenschaft_Korporation")]
    [InlineData("Altdorf (UR)", "Gemeinde")]
    [InlineData("Bürglen (UR)", "Gemeinde")]
    [InlineData("Seedorf (UR)", "Gemeinde")]
    [InlineData("Flüelen", "Gemeinde")]
    [InlineData("Göschenen", "Gemeinde")]
    [InlineData("Unterschächen", "Gemeinde")]
    [InlineData("Wassen", "Gemeinde")]
    public void Jeder_Bestandswert_bekommt_seinen_Organisationstyp(string eigentuemer, string typ)
        => Assert.Equal(typ, EigentumVokabular.NachOrganisationstyp(eigentuemer));

    // "Abwasser Uri" ist ein Zweckverband, kein Kanton. Der alte Kantonsexport traegt
    // dort "Kanton"; Abwasser Uri hat das am 2026-09-02 korrigiert.
    [Fact]
    public void Abwasser_Uri_ist_ein_Abwasserverband()
        => Assert.Equal("Abwasserverband", EigentumVokabular.NachOrganisationstyp("Abwasser Uri"));

    // Fail-closed: Wofuer kein Typ belegt ist, entsteht keine Organisation.
    [Theory]
    [InlineData("Schwyz")]
    [InlineData("Familie Muster")]
    [InlineData("")]
    public void Ein_unbekannter_Eigentuemer_bekommt_keinen_Typ(string eigentuemer)
        => Assert.Null(EigentumVokabular.NachOrganisationstyp(eigentuemer));

    // Entscheid Pascal 24.09.2026: «so wie es im WebGIS ist». Das WebGIS fuehrt den Typ in seiner
    // Organisationsliste selbst — die Haltungsmaske zeigt «Name (Typ)», die Schachtmaske den Namen.
    [Theory]
    [InlineData("AWU_von_privat", "Abwasserverband")]
    [InlineData("AWU_von_privat (Abwasserverband)", "Abwasserverband")]
    [InlineData("AWU_von_oeffentlich", "Abwasserverband")]
    [InlineData("AWU_von_oeffentlich (Abwasserverband)", "Abwasserverband")]
    [InlineData("oeff_Rechtl_Koerperschaften", "Genossenschaft_Korporation")]
    [InlineData("Meliorationsgen. Seedorf (Genossenschaft/Kooperation)", "Genossenschaft_Korporation")]
    [InlineData("RUAG", "Privat")]
    [InlineData("Bund Astra (Bund)", "Bund")]
    [InlineData("Kanton Uri (Kanton)", "Kanton")]
    [InlineData("Bürglen (Gemeinde)", "Gemeinde")]
    public void Eine_webgis_organisation_traegt_den_typ_aus_dem_webgis(string eigentuemer, string typ)
        => Assert.Equal(typ, EigentumVokabular.NachOrganisationstyp(eigentuemer));

    // Jeder Eintrag der WebGIS-Liste, gelesen aus dem Objektaktenkatalog (Haltungsmaske «Name (Typ)»).
    [Fact]
    public void Jede_organisation_der_webgis_liste_bekommt_ihren_typ()
    {
        var feld = FieldCatalog.Objektfelder.Feld("haltung.owner");
        var eintraege = FieldCatalog.Objektfelder.Auswahl(feld.KatalogId)!.Eintraege
            .Where(e => !string.IsNullOrWhiteSpace(e.Label)).ToList();
        Assert.True(eintraege.Count >= 30);
        var sia = new Dictionary<string, string>
        {
            ["Abwasserverband"] = "Abwasserverband", ["Bund"] = "Bund", ["Kanton"] = "Kanton",
            ["Gemeinde"] = "Gemeinde", ["Privat"] = "Privat",
            ["Genossenschaft/Kooperation"] = "Genossenschaft_Korporation",
            ["Unbekannt"] = "Privat", // SIA405 kennt kein «unbekannt» — dieselbe erzwungene Wahl wie bisher
        };
        foreach (var e in eintraege)
        {
            var klammer = e.Label.LastIndexOf(" (", StringComparison.Ordinal);
            var name = e.Label[..klammer];
            var webgisTyp = e.Label[(klammer + 2)..^1];
            Assert.Equal(sia[webgisTyp], EigentumVokabular.NachOrganisationstyp(e.Label));
            Assert.Equal(sia[webgisTyp], EigentumVokabular.NachOrganisationstyp(name));
        }
    }

    // In der XTF heisst die Organisation wie im WebGIS, ohne den angehaengten Typ: Haltung und Schacht
    // meinen mit «AWU_von_privat (Abwasserverband)» und «AWU_von_privat» dieselbe Organisation.
    [Theory]
    [InlineData("AWU_von_privat (Abwasserverband)", "AWU_von_privat")]
    [InlineData("AWU_von_privat", "AWU_von_privat")]
    [InlineData("Kanton Uri (Kanton)", "Kanton Uri")]
    [InlineData("Bürglen (Gemeinde)", "Bürglen")]
    public void Eine_webgis_organisation_heisst_wie_im_webgis(string eigentuemer, string name)
        => Assert.Equal(name, EigentumVokabular.Normalisieren(eigentuemer));

    // Die Faltung dient nur dem Vergleich. Der Name selbst darf sie nie zu sehen bekommen.
    [Theory]
    [InlineData("Bürglen (UR)")]
    [InlineData("Unterschächen")]
    [InlineData("ASTRA - Bundesamt für Strassen")]
    public void Der_Name_bleibt_zeichengleich(string eigentuemer)
        => Assert.Equal(eigentuemer, EigentumVokabular.Normalisieren(eigentuemer));
}
