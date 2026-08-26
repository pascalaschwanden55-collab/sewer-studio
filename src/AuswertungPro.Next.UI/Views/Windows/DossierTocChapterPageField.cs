using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

using AuswertungPro.Next.Application.Dossiers.Preview;
using AuswertungPro.Next.Domain.Models.Dossiers;

namespace AuswertungPro.Next.UI.Views.Windows;

/// <summary>
/// Das Eingabefeld für die Seitenzahl einer Kapitelzeile des
/// Inhaltsverzeichnisses.
///
/// Die drei Kapitelzeilen holen ihre Seitenzahl aus einem Word-Feld und waren
/// deshalb als einzige nicht änderbar, während die Beilagenzeilen darunter eine
/// freie Seitenzahl haben. Dieses Feld schliesst die Lücke.
///
/// Leer heisst: Word rechnet weiter selbst. Sobald etwas darin steht — auch ein
/// bewusst geleertes Feld —, ersetzt die eigene Angabe das Word-Feld. Nur die
/// angefasste Zeile verliert ihre Automatik, die übrigen behalten sie.
///
/// Eigene Klasse und nicht im Vorschaufenster: dessen partielle Klasse hatte
/// die zulässige Grösse bereits erreicht, und der Wächter darüber besteht zu
/// Recht darauf, Verantwortung auszulagern statt die Grenze anzuheben.
/// </summary>
internal static class DossierTocChapterPageField
{
    /// <summary>
    /// Die Titel der Kapitelzeilen dieser Seite. Nur sie holen ihre Seitenzahl
    /// aus einem Word-Feld und brauchen deshalb ein eigenes Feld dafuer.
    /// </summary>
    public static IReadOnlyList<string> ChapterTitles(DossierPreviewPage page)
        => page is null
            ? Array.Empty<string>()
            : DossierTocChapterPageClickMapper.ChapterTitles([page]);

    /// <param name="kleiner">
    /// Baut einen kleinen Knopf im Stil der übrigen Vorschau-Werkzeuge:
    /// Beschriftung, Hinweis, Wirkung.
    /// </param>
    /// <summary>
    /// Das Feld — oder nichts Sichtbares, wenn dieser Text gar keine
    /// Kapitelzeile ist. Die Entscheidung liegt hier und nicht beim Aufrufer,
    /// damit die Vorschau davon nichts wissen muss.
    /// </summary>
    public static UIElement CreateFor(
        DossierDefinition dossier,
        string titel,
        IReadOnlyList<string> chapterTitles,
        Func<string, string, Action, Button> kleiner,
        Action zeichneBlatt,
        Action<DossierPreviewTarget, UIElement> merke,
        Action<DossierPreviewTarget> betone)
    {
        var match = chapterTitles
            .Select((title, rowIndex) => (title, rowIndex))
            .FirstOrDefault(item => string.Equals(
                item.title,
                titel,
                StringComparison.Ordinal));

        if (match.title is null)
            return new StackPanel();

        var target = DossierTocChapterPageClickMapper.PageTarget(titel);
        return Create(
            dossier,
            titel,
            kleiner,
            zeichneBlatt,
            () => betone(target),
            input => merke(target, input));
    }

    public static UIElement Create(
        DossierDefinition dossier,
        string titel,
        Func<string, string, Action, Button> kleiner,
        Action zeichneBlatt,
        Action betone,
        Action<UIElement>? merke = null)
    {
        ArgumentNullException.ThrowIfNull(dossier);
        ArgumentNullException.ThrowIfNull(kleiner);
        ArgumentNullException.ThrowIfNull(zeichneBlatt);
        ArgumentNullException.ThrowIfNull(betone);

        dossier.TocChapterPages ??= new Dictionary<string, string>();

        var zeile = new StackPanel { Margin = new Thickness(0, 6, 0, 0) };
        zeile.Children.Add(new TextBlock
        {
            Text = "Seite",
            Margin = new Thickness(0, 0, 0, 3)
        });

        var eigen = dossier.TocChapterPages.TryGetValue(titel, out var wert);

        var feld = new TextBox
        {
            Text = eigen ? wert ?? string.Empty : string.Empty,
            Width = 70,
            HorizontalAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(6, 4, 6, 4),
            FontFamily = new FontFamily("Arial"),
            ToolTip = "Leer lassen heisst: Word rechnet die Seitenzahl selbst. "
                + "Eine eigene Angabe ersetzt die Rechnung — auch eine leere, "
                + "dann steht dort gar keine Zahl."
        };
        merke?.Invoke(feld);

        var hinweis = new TextBlock
        {
            FontStyle = FontStyles.Italic,
            Margin = new Thickness(0, 3, 0, 0),
            TextWrapping = TextWrapping.Wrap
        };

        void ZeigeHerkunft()
            => hinweis.Text = dossier.TocChapterPages.ContainsKey(titel)
                ? "Eigene Angabe — Word rechnet diese Zeile nicht mehr nach."
                : "Von Word gerechnet.";

        var zurueck = kleiner("Wieder von Word", "Die eigene Seitenzahl verwerfen", () =>
        {
            dossier.TocChapterPages.Remove(titel);
            feld.Text = string.Empty;
            ZeigeHerkunft();
            zeichneBlatt();
            betone();
        });

        zurueck.Margin = new Thickness(0, 4, 0, 0);
        zurueck.HorizontalAlignment = HorizontalAlignment.Left;

        feld.GotKeyboardFocus += (_, _) => betone();
        feld.TextChanged += (_, _) =>
        {
            dossier.TocChapterPages[titel] = feld.Text;
            ZeigeHerkunft();
            zurueck.Visibility = Visibility.Visible;
            zeichneBlatt();
        };

        ZeigeHerkunft();
        zurueck.Visibility = dossier.TocChapterPages.ContainsKey(titel)
            ? Visibility.Visible
            : Visibility.Collapsed;

        zeile.Children.Add(feld);
        zeile.Children.Add(hinweis);
        zeile.Children.Add(zurueck);
        return zeile;
    }
}
