using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import.Pdf;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Das Uri-Schachtprotokoll ist ein Kaestchenformular: Jede Zeile von «Zustand der Bauteile»
/// nennt den ganzen Wortvorrat, angekreuzt ist nur, was eine Marke (●/✔) traegt. Anlass
/// (19.09.2026): Schacht 80409 stand mit 27 Schaeden im Projekt, das Protokoll kreuzt vier an.
/// Die Vorlage ist der echte Text von Seite 1 (pdftotext -layout, Visum anonymisiert).
/// </summary>
public sealed class SchachtProtocolKaestchenformularTests
{
    private static string Vorlage80409()
        => File.ReadAllText(Path.Combine(
            TestRepoPaths.RepoRoot(), "tests", "Fixtures", "Schachtprotokolle", "80409_seite1_layout.txt"));

    [Fact]
    public void Das_Uri_Formular_wird_als_Kaestchenformular_erkannt()
    {
        Assert.True(SchachtProtocolKaestchenformular.IstKaestchenformular(
            SchachtProtocolParser.NormalizePdfText(Vorlage80409())));
    }

    [Fact]
    public void Ein_SchachtPro_Protokoll_ist_kein_Kaestchenformular()
    {
        var text = string.Join("\n",
            "Schachtprotokoll Schacht Nr. 22152",
            "ZUSTAND DER SCHACHTBAUTEILE",
            "Konus                      Infiltration • Fugen mangelhaft verputzt",
            "Leiter                     fehlt");

        Assert.False(SchachtProtocolKaestchenformular.IstKaestchenformular(text));
    }

    [Fact]
    public void Nur_die_vier_markierten_Schaeden_werden_gelesen()
    {
        var eintraege = SchachtProtocolParser.ParseSchachtDamageEntries(Vorlage80409());

        Assert.Equal(
            new[]
            {
                ("Konus", "Verkalkung"),
                ("Bankett", "ausgebrochen"),
                ("Bankett", "Ablagerungen"),
                ("Durchlaufrinne", "Ablagerungen"),
            },
            eintraege.Select(e => (e.Component, e.Damage)).ToArray());
    }

    [Fact]
    public void Eine_Marke_bindet_an_das_folgende_Wort_nicht_an_das_vorangehende()
    {
        var text = string.Join("\n",
            "Schachtprotokoll  Nr. 1",
            "Zustand der Bauteile                                   Mängelfrei",
            "Bankett                 gerissen         ● ausgebrochen      korrodiert        ● Ablagerungen",
            "Durchlaufrinne          gerissen           ausgebrochen      korrodiert          Ablagerungen",
            "Anschlüsse");

        var eintraege = SchachtProtocolParser.ParseSchachtDamageEntries(text);

        Assert.Equal(
            new[] { ("Bankett", "ausgebrochen"), ("Bankett", "Ablagerungen") },
            eintraege.Select(e => (e.Component, e.Damage)).ToArray());
    }

    [Fact]
    public void Die_Zeile_Anschluss_beendet_den_Zustandsabschnitt_nicht()
    {
        var text = string.Join("\n",
            "Schachtprotokoll  Nr. 1",
            "Zustand der Bauteile                                   Mängelfrei",
            "Anschluss               gerissen            ● ausgebrochen                mangelhaft eingebunden",
            "Verkalkung              Konus              Schachtrohr     ✔ Bankett             Gerinne           Anschluss",
            "Fremdwasser             Konus              Schachtrohr       Bankett           ✔ Gerinne           Anschluss",
            "Anschlüsse",
            "Nr     Aus/Ein   DN mm     Tiefe m          Material");

        var eintraege = SchachtProtocolParser.ParseSchachtDamageEntries(text);

        Assert.Equal(
            new[]
            {
                ("Bankett", "Verkalkung"),
                ("Durchlaufrinne", "Fremdwasser"),
                ("Anschluss", "ausgebrochen"),
            },
            eintraege.Select(e => (e.Component, e.Damage)).ToArray());
    }

    [Fact]
    public void Die_primaeren_Schaeden_zaehlen_vier_Zeilen()
    {
        var felder = SchachtProtocolParser.ParseSchachtFields(Vorlage80409());

        Assert.Equal("80409", felder.SchachtNummer);
        Assert.Equal("3.45", felder.Schachttiefe);
        Assert.Equal(
            new[] { "Konus: Verkalkung", "Bankett: ausgebrochen", "Bankett: Ablagerungen", "Durchlaufrinne: Ablagerungen" },
            felder.PrimaereSchaeden!.Split('\n', StringSplitOptions.RemoveEmptyEntries));
    }

    [Fact]
    public void Der_Zusatz_liest_Anschlusstabelle_Deckel_Medium_Material_und_Steighilfe()
    {
        var zusatz = SchachtProtocolZusatzParser.Parse(Vorlage80409());

        Assert.Equal("Mischabwasser", zusatz.Medium);
        Assert.Equal("Fertigbetonelement", zusatz.MaterialSchacht);
        Assert.Equal("Guss und Beton", zusatz.MaterialDeckel);
        Assert.Equal("660", zusatz.DeckelDurchmesserMm);
        Assert.Equal("vorhanden", zusatz.Steighilfe);
        Assert.Null(zusatz.Tauchbogen);

        Assert.Equal(4, zusatz.Anschluesse.Count);
        var a1 = zusatz.Anschluesse[0];
        Assert.Equal(1, a1.Nr);
        Assert.Equal("Auslauf", a1.Art);
        Assert.Equal(250, a1.DnMm);
        Assert.Equal(3.45m, a1.TiefeM);
        Assert.Equal("Beton", a1.Material);
        Assert.Equal("PDF", a1.Quelle);

        var e3 = zusatz.Anschluesse[2];
        Assert.Equal(3, e3.Nr);
        Assert.Equal("Einlauf", e3.Art);
        Assert.Equal(100, e3.DnMm);
        Assert.Equal(0.60m, e3.TiefeM);
        Assert.Equal("Polyvinylchlorid", e3.Material);
    }

    [Fact]
    public void Leere_Tabellenzeilen_und_fremde_Texte_ergeben_keine_Anschluesse()
    {
        var zusatz = SchachtProtocolZusatzParser.Parse(string.Join("\n",
            "Schachtprotokoll Nr. 2",
            "Anschlüsse",
            "Nr     Aus/Ein   DN mm     Tiefe m          Material",
            "1 -                                   -",
            "Datum 01.01.2026"));

        Assert.Empty(zusatz.Anschluesse);
        Assert.True(zusatz.IstLeer);
    }

    [Fact]
    public void ApplyZusatz_schreibt_Felder_und_Anschluesse_ohne_Handwerte_zu_ueberschreiben()
    {
        var zusatz = SchachtProtocolZusatzParser.Parse(Vorlage80409());
        var ziel = new SchachtRecord();
        ziel.SetFieldValue("Material", "Handwert", FieldSource.Manual, userEdited: true);

        SchachtProtocolApplier.ApplyZusatz(ziel, zusatz, rebuildFromProtocol: false, onlyMissing: false);

        Assert.Equal("Handwert", ziel.GetFieldValue("Material"));
        Assert.Equal("Mischabwasser", ziel.GetFieldValue("Medium"));
        Assert.Equal("Guss und Beton", ziel.GetFieldValue("Deckelmaterial"));
        Assert.Equal("660", ziel.GetFieldValue("Deckeldurchmesser"));
        Assert.Equal("vorhanden", ziel.GetFieldValue("Steighilfe"));
        Assert.NotNull(ziel.Anschluesse);
        Assert.Equal(4, ziel.Anschluesse!.Count);
        Assert.Equal(FieldSource.Pdf, ziel.FieldMeta["Medium"].Source);
        Assert.False(ziel.FieldMeta["Medium"].UserEdited);
    }

    [Fact]
    public void Ein_leerer_Zusatz_schreibt_nichts_und_loescht_keine_Anschluesse()
    {
        var ziel = new SchachtRecord();
        ziel.SetzeAnschluesse([new SchachtAnschluss { Nr = 1, Art = "Auslauf", Quelle = "SchachtPro" }]);

        SchachtProtocolApplier.ApplyZusatz(ziel, SchachtProtocolZusatz.Leer, rebuildFromProtocol: false, onlyMissing: false);

        Assert.Single(ziel.Anschluesse!);
        Assert.Empty(ziel.Fields);
    }
}
