using System.Collections.Generic;
using System.Linq;

using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Application.Dossiers.Preview;
using AuswertungPro.Next.Domain.Models.Dossiers;

using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Dossiers;

/// <summary>
/// Mehrere Vorlagenkapitel auf einem einzigen Ausgabeblatt.
///
/// Genau das passiert im echten Dossier, sobald ein Kapitel kurz ist: Ohne
/// gewählten Übersichtsplan rutscht „Eigentumsverhältnisse" auf dasselbe Blatt
/// wie „Übersichtsplan Werkleitungen". Die Zuordnung lieferte dafür nur EINE
/// Editorseite — die mit dem stärkeren Textbeleg. Die Felder des anderen
/// Kapitels waren damit unerreichbar, und ausgerechnet dort sitzt die Auswahl
/// des Plans. Wer keinen Plan hat, kommt also nicht an den Knopf, der ihn
/// einfügen würde.
/// </summary>
public sealed class DossierOutputPreviewMultiChapterTests
{
    private static DossierPreviewPage Seite(int nummer, string kapitel, params string[] texte)
        => new(
            nummer,
            kapitel,
            new DossierPreviewGeometry(794, 1123, DossierPreviewEdges.Zero),
            texte.Select(text => (DossierPreviewBlock)new DossierPreviewParagraph(
                [DossierPreviewRun.Literal(text, DossierPreviewRunFormat.Default)],
                DossierPreviewParagraphFormat.Default))
                .ToList(),
            []);

    private static IReadOnlyList<DossierOutputPreviewNavigationItem> Navigation(
        params string[] blattTexte)
    {
        var plan = Seite(3, "Übersichtsplan Werkleitungen", "Übersichtsplan Werkleitungen");
        var eigentum = Seite(4, "Eigentumsverhältnisse", "Eigentumsverhältnisse", "Haus Nr.");

        var templates = new[]
        {
            new DossierPreviewNavigationItem(
                "Übersichtsplan Werkleitungen", "Seite 3", plan),
            new DossierPreviewNavigationItem(
                "Eigentumsverhältnisse", "Seite 4", eigentum)
        };

        // Die Zuordnung liest die WORTE der Seite, nicht ihren Fliesstext —
        // sie stammen aus der PDF-Wortlage.
        var blaetter = blattTexte
            .Select((text, index) => new DossierOutputPreviewPage(
                index + 1,
                595,
                842,
                text,
                text.Split(' ')
                    .Select(wort => new DossierOutputPreviewWord(wort, 0, 0, 0, 0))
                    .ToList()))
            .ToList();

        return DossierOutputPreviewInteractionMapper.BuildNavigation(
            blaetter,
            templates,
            new DossierDefinition(),
            new Dictionary<string, string>(),
            _ => Array.Empty<IReadOnlyDictionary<string, string>>());
    }

    [Fact]
    public void Ein_Blatt_mit_zwei_Kapiteln_bedient_beide_Editorseiten()
    {
        var navigation = Navigation(
            "Übersichtsplan Werkleitungen Eigentumsverhältnisse Haus Nr.");

        var blatt = Assert.Single(navigation);

        Assert.Equal(2, blatt.EditorPages.Count);
        Assert.Equal(
            ["Übersichtsplan Werkleitungen", "Eigentumsverhältnisse"],
            blatt.EditorPages.Select(seite => seite.Title));
    }

    [Fact]
    public void Ein_Blatt_je_Kapitel_bleibt_bei_seinem_eigenen()
    {
        var navigation = Navigation(
            "Übersichtsplan Werkleitungen",
            "Eigentumsverhältnisse Haus Nr.");

        Assert.Equal(2, navigation.Count);
        Assert.Equal("Übersichtsplan Werkleitungen",
            Assert.Single(navigation[0].EditorPages).Title);
        Assert.Equal("Eigentumsverhältnisse",
            Assert.Single(navigation[1].EditorPages).Title);
    }

    [Fact]
    public void Eine_Fortsetzungsseite_wiederholt_ihr_Kapitel_nicht_doppelt()
    {
        var navigation = Navigation(
            "Eigentumsverhältnisse Haus Nr.",
            "Eigentumsverhältnisse Haus Nr.");

        Assert.Equal(
            "Eigentumsverhältnisse",
            Assert.Single(navigation[1].EditorPages).Title);
    }

    [Fact]
    public void Die_benennende_Seite_ist_eine_der_gezeigten()
    {
        // Bestandsschutz: Aufrufer, die nur eine Seite kennen, bekommen
        // weiterhin die am staerksten belegte — sie muss aber unter den
        // gezeigten sein, sonst zeigte der Kopf ein anderes Kapitel als die
        // Felder darunter.
        var navigation = Navigation(
            "Übersichtsplan Werkleitungen Eigentumsverhältnisse Haus Nr.");

        Assert.Contains(navigation[0].EditorPage, navigation[0].EditorPages);
    }

    // ── Kein Kapitel darf verlorengehen ───────────────────────────────────

    /// <summary>
    /// Die Vorlagenseiten mit dem Text, der auf ihnen steht. Die
    /// Verzeichnisseite traegt die Kapiteltitel selbst — genau das macht sie
    /// zum Inhaltsverzeichnis, und genau das macht sie zur Falle.
    /// </summary>
    private static readonly (string Titel, string Text)[] AlleKapitel =
    [
        ("Deckblatt", "Deckblatt Liegenschaft"),
        ("Inhaltsverzeichnis", "Inhaltsverzeichnis Übersichtsplan Werkleitungen "
            + "Eigentumsverhältnisse Informationen Sanierung Protokolle"),
        ("Übersichtsplan Werkleitungen", "Übersichtsplan Werkleitungen"),
        ("Eigentumsverhältnisse", "Eigentumsverhältnisse Haus Nr."),
        ("Informationen Sanierung", "Informationen Sanierung Aktennotiz")
    ];

    private static IReadOnlyList<DossierPreviewNavigationItem> AlleVorlagen()
        => AlleKapitel
            .Select((kapitel, index) => new DossierPreviewNavigationItem(
                kapitel.Titel,
                $"Seite {index + 1}",
                Seite(index + 1, kapitel.Titel, kapitel.Text.Split(' '))))
            .ToList();

    private static IReadOnlyList<DossierOutputPreviewNavigationItem> NavigationFuer(
        IReadOnlyList<(string Text, bool IstBeilage)> blaetter)
        => DossierOutputPreviewInteractionMapper.BuildNavigation(
            blaetter
                .Select((blatt, index) => new DossierOutputPreviewPage(
                    index + 1,
                    595,
                    842,
                    blatt.Text,
                    blatt.Text.Split(' ')
                        .Select(wort => new DossierOutputPreviewWord(wort, 0, 0, 0, 0))
                        .ToList(),
                    IsAttachment: blatt.IstBeilage))
                .ToList(),
            AlleVorlagen(),
            new DossierDefinition(),
            new Dictionary<string, string>(),
            _ => Array.Empty<IReadOnlyDictionary<string, string>>());

    private static void KeinKapitelFehlt(
        IReadOnlyList<DossierOutputPreviewNavigationItem> navigation)
    {
        var erreichbar = navigation
            .SelectMany(blatt => blatt.EditorPages)
            .Select(seite => seite.Title)
            .ToHashSet();

        var fehlend = AlleKapitel
            .Select(kapitel => kapitel.Titel)
            .Where(titel => !erreichbar.Contains(titel))
            .ToList();

        Assert.True(
            fehlend.Count == 0,
            "Nicht erreichbar: " + string.Join(" · ", fehlend));
    }

    [Fact]
    public void Ein_Blatt_je_Kapitel_laesst_kein_Kapitel_aus()
    {
        KeinKapitelFehlt(NavigationFuer(
            AlleKapitel.Select(kapitel => (kapitel.Text, false)).ToList()));
    }

    [Fact]
    public void Alles_auf_einem_einzigen_Blatt_laesst_kein_Kapitel_aus()
    {
        KeinKapitelFehlt(NavigationFuer(
            [(string.Join(" ", AlleKapitel.Select(kapitel => kapitel.Text)), false)]));
    }

    [Fact]
    public void Ein_Kapitel_ohne_eigenen_Text_bleibt_erreichbar()
    {
        // Der Uebersichtsplan ohne gewaehlten Plan ist genau dieser Fall: Auf
        // dem Blatt steht nichts, was ihn belegt.
        KeinKapitelFehlt(NavigationFuer(
        [
            ("Deckblatt Liegenschaft", false),
            ("Inhaltsverzeichnis", false),
            ("Eigentumsverhältnisse Haus Nr.", false),
            ("Informationen Sanierung Aktennotiz", false)
        ]));
    }

    [Fact]
    public void Beilagen_am_Ende_verschlucken_kein_Kapitel()
    {
        // Die Protokolle haengen hinten dran. Ein Kapitel, das erst nach dem
        // letzten Dossierblatt zugeordnet wuerde, ginge sonst verloren.
        KeinKapitelFehlt(NavigationFuer(
        [
            ("Deckblatt Liegenschaft", false),
            ("Inhaltsverzeichnis", false),
            ("Original", true),
            ("Original", true)
        ]));
    }

    [Fact]
    public void Das_Inhaltsverzeichnis_verschluckt_die_Kapitel_nicht()
    {
        // Die Falle: Auf dem Verzeichnisblatt STEHEN alle Kapitelnamen. Ein
        // Blatt, das seine Kapitel nach Textbeleg nach vorn einsammelt, nimmt
        // dort alles mit — und die spaeteren Blaetter gehen leer aus. Genau so
        // landeten die Felder von „Informationen Sanierung" neben einem Blatt,
        // das Kapitel 1 und 2 zeigt.
        var navigation = NavigationFuer(
        [
            ("Deckblatt Liegenschaft", false),
            (AlleKapitel[1].Text, false),
            ("Übersichtsplan Werkleitungen Eigentumsverhältnisse Haus Nr.", false),
            ("Informationen Sanierung Aktennotiz", false)
        ]);

        KeinKapitelFehlt(navigation);

        var verzeichnisblatt = navigation[1];
        Assert.Equal(
            ["Inhaltsverzeichnis"],
            verzeichnisblatt.EditorPages.Select(seite => seite.Title));

        // Das Blatt mit Kapitel 1 und 2 muss auch deren Felder tragen —
        // darunter die Auswahl des Uebersichtsplans.
        Assert.Equal(
            ["Übersichtsplan Werkleitungen", "Eigentumsverhältnisse"],
            navigation[2].EditorPages.Select(seite => seite.Title));

        Assert.Equal(
            ["Informationen Sanierung"],
            navigation[3].EditorPages.Select(seite => seite.Title));
    }
}
