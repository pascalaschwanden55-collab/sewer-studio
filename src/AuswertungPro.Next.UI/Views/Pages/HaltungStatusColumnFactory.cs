using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Shapes;
using AuswertungPro.Next.Application.UseCases.NaechsteAufgabe;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.DataPage;

namespace AuswertungPro.Next.UI.Views.Pages;

/// <summary>
/// Nova-Etappe 2b (Inventar 4.3, Prototyp v2): Die vier nur lesenden Statusspalten der
/// Haltungstabelle — KI-Ampel, Pruefung, Video und Protokoll.
///
/// Es sind virtuelle Spalten (<see cref="NovaStatusSpalten"/>): kein Feld, kein Export, keine
/// Layout-Persistenz. Ihr Inhalt kommt vollstaendig aus der WPF-freien Regel
/// <see cref="HaltungZeilenStatus"/>; hier steht nur die Darstellung.
///
/// Video und Protokoll rufen die bereits vorhandenen Befehle der Seite
/// (<c>PlayVideoCommand</c>, <c>OpenOriginalPdfCommand</c>) mit dem Datensatz der Zeile —
/// es entsteht kein zweiter Weg zu Video oder PDF.
/// </summary>
public static class HaltungStatusColumnFactory
{
    private const double AmpelDurchmesser = 9;

    /// <summary>Fluent-Glyph "Play" (E768) fuer den Video-Knopf.</summary>
    private const string VideoGlyph = "";

    public static DataGridTemplateColumn Ki(string header)
        => Spalte(header, StatusInhalt(KiVorlage()), minimalBreite: 96);

    public static DataGridTemplateColumn Pruefung(string header)
        => Spalte(header, StatusInhalt(PruefungVorlage()), minimalBreite: 150);

    public static DataGridTemplateColumn Video(string header)
        => Spalte(header, VideoZelle(), minimalBreite: 64);

    public static DataGridTemplateColumn Protokoll(string header)
        => Spalte(header, ProtokollZelle(), minimalBreite: 78);

    private static DataGridTemplateColumn Spalte(string header, FrameworkElementFactory inhalt, double minimalBreite)
        => new()
        {
            Header = header,
            CellTemplate = Vorlage(inhalt),
            IsReadOnly = true,
            CanUserSort = false,
            Width = DataGridLength.SizeToHeader,
            MinWidth = minimalBreite
        };

    /// <summary>
    /// Eine fertige, versiegelte Zellvorlage. Versiegelt wird gleich beim Bauen: WPF versiegelt
    /// eine Vorlage sonst erst beim ersten Anwenden, und vorher laesst sich ihr Baum nicht
    /// erzeugen (das brauchen die Tests).
    /// </summary>
    private static DataTemplate Vorlage(FrameworkElementFactory inhalt)
    {
        var vorlage = new DataTemplate { VisualTree = inhalt };
        vorlage.Seal();
        return vorlage;
    }

    /// <summary>
    /// Traeger fuer KI und Pruefung: Der Zeilenstatus wird EINMAL je Zelle gerechnet und als
    /// Inhalt weitergereicht; die Vorlage bindet danach nur noch seine Felder. Die MultiBinding
    /// haengt am Datensatz, am Feld "offen/abgeschlossen" und am Protokoll, damit die Zelle
    /// jede fachliche Aenderung mitbekommt.
    /// </summary>
    private static FrameworkElementFactory StatusInhalt(FrameworkElementFactory vorlage)
    {
        var traeger = new FrameworkElementFactory(typeof(ContentControl));
        traeger.SetBinding(
            ContentControl.ContentProperty,
            HaltungZeilenStatusConverter.Bindung(HaltungZeilenStatusConverter.Instance));
        traeger.SetValue(ContentControl.ContentTemplateProperty, Vorlage(vorlage));
        traeger.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        return traeger;
    }

    private static FrameworkElementFactory KiVorlage()
    {
        var zeile = new FrameworkElementFactory(typeof(StackPanel));
        zeile.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
        zeile.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);

        zeile.AppendChild(Ampel(KiAmpel.KeineAnalyse, "MutedBrush"));
        zeile.AppendChild(Ampel(KiAmpel.Offen, "WarningBrush"));
        zeile.AppendChild(Ampel(KiAmpel.Geprueft, "SuccessBrush"));
        zeile.AppendChild(Ampel(KiAmpel.Kritisch, "DangerBrush"));

        var text = new FrameworkElementFactory(typeof(TextBlock));
        text.SetBinding(TextBlock.TextProperty, new Binding(nameof(HaltungZeilenStatusErgebnis.AmpelText)));
        text.SetBinding(TextBlock.ForegroundProperty, ZelleTinte());
        text.SetResourceReference(TextBlock.FontSizeProperty, "TextS");
        text.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        text.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
        zeile.AppendChild(text);
        return zeile;
    }

    private static FrameworkElementFactory Ampel(KiAmpel ampel, string pinselSchluessel)
    {
        var punkt = new FrameworkElementFactory(typeof(Ellipse));
        punkt.SetValue(FrameworkElement.WidthProperty, AmpelDurchmesser);
        punkt.SetValue(FrameworkElement.HeightProperty, AmpelDurchmesser);
        punkt.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 6, 0));
        punkt.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        punkt.SetResourceReference(Shape.FillProperty, pinselSchluessel);
        punkt.SetBinding(
            UIElement.VisibilityProperty,
            Aufzaehlung(nameof(HaltungZeilenStatusErgebnis.Ampel), ampel.ToString()));
        return punkt;
    }

    private static FrameworkElementFactory PruefungVorlage()
    {
        var huelle = new FrameworkElementFactory(typeof(Grid));
        huelle.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Left);
        huelle.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        huelle.AppendChild(Kapsel(HaltungPruefstand.Abgeschlossen, "SuccessSubtleBrush", "SuccessBrush"));
        huelle.AppendChild(Kapsel(HaltungPruefstand.KiAnalysiert, "KiSubtleBrush", "KiTextBrush"));
        huelle.AppendChild(Kapsel(HaltungPruefstand.Offen, "SurfaceSubtleBrush", "MutedBrush"));
        return huelle;
    }

    private static FrameworkElementFactory Kapsel(HaltungPruefstand stand, string grundSchluessel, string tinteSchluessel)
    {
        var kapsel = new FrameworkElementFactory(typeof(Border));
        kapsel.SetResourceReference(Border.BackgroundProperty, grundSchluessel);
        kapsel.SetResourceReference(Border.BorderBrushProperty, tinteSchluessel);
        kapsel.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        kapsel.SetResourceReference(Border.CornerRadiusProperty, "RadiusPill");
        kapsel.SetValue(Border.PaddingProperty, new Thickness(8, 1, 8, 1));
        kapsel.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        kapsel.SetBinding(
            UIElement.VisibilityProperty,
            Aufzaehlung(nameof(HaltungZeilenStatusErgebnis.Pruefstand), stand.ToString()));

        var text = new FrameworkElementFactory(typeof(TextBlock));
        text.SetBinding(TextBlock.TextProperty, new Binding(nameof(HaltungZeilenStatusErgebnis.PruefungText)));
        text.SetResourceReference(TextBlock.ForegroundProperty, tinteSchluessel);
        text.SetResourceReference(TextBlock.FontSizeProperty, "TextXS");
        text.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
        text.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
        kapsel.AppendChild(text);
        return kapsel;
    }

    private static FrameworkElementFactory VideoZelle()
    {
        var huelle = new FrameworkElementFactory(typeof(Grid));

        var glyph = new FrameworkElementFactory(typeof(FluentIcon));
        glyph.SetValue(FluentIcon.GlyphProperty, VideoGlyph);

        var knopf = Aktionsknopf(
            "PlayVideoCommand",
            HaltungStatusSichtbarkeitConverter.Video,
            "Video abspielen",
            "Video {0} abspielen");
        knopf.AppendChild(glyph);
        huelle.AppendChild(knopf);
        huelle.AppendChild(Fehlt(HaltungStatusSichtbarkeitConverter.KeinVideo, "kein Video"));
        return huelle;
    }

    private static FrameworkElementFactory ProtokollZelle()
    {
        var huelle = new FrameworkElementFactory(typeof(Grid));

        var beschriftung = new FrameworkElementFactory(typeof(TextBlock));
        beschriftung.SetValue(TextBlock.TextProperty, "PDF");
        beschriftung.SetResourceReference(TextBlock.FontSizeProperty, "TextXS");
        beschriftung.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);

        var knopf = Aktionsknopf(
            "OpenOriginalPdfCommand",
            HaltungStatusSichtbarkeitConverter.Protokoll,
            "Protokoll öffnen",
            "Protokoll {0} öffnen");
        knopf.SetValue(FrameworkElement.WidthProperty, 36d);
        knopf.AppendChild(beschriftung);
        huelle.AppendChild(knopf);
        huelle.AppendChild(Fehlt(HaltungStatusSichtbarkeitConverter.KeinProtokoll, "kein Protokoll"));
        return huelle;
    }

    /// <summary>
    /// Ein Knopf, der den vorhandenen Seitenbefehl mit dem Datensatz der Zeile aufruft. Der
    /// Befehl haengt am DataContext des DataGrid (dem ViewModel), der Parameter an der Zeile.
    /// Tooltip UND vorlesbarer Name werden gesetzt: Ein Glyph laesst sich nicht vorlesen.
    /// </summary>
    private static FrameworkElementFactory Aktionsknopf(
        string befehl,
        string sichtbarkeitsParameter,
        string hinweis,
        string namensMuster)
    {
        var knopf = new FrameworkElementFactory(typeof(Button));
        knopf.SetResourceReference(FrameworkElement.StyleProperty, "IconButton");
        knopf.SetValue(FrameworkElement.ToolTipProperty, hinweis);
        knopf.SetBinding(AutomationProperties.NameProperty, new Binding($"Fields[{FieldKeys.HoldingName}]")
        {
            Mode = BindingMode.OneWay,
            StringFormat = namensMuster
        });
        knopf.SetBinding(ButtonBase.CommandProperty, new Binding($"DataContext.{befehl}")
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(DataGrid), 1)
        });
        knopf.SetBinding(ButtonBase.CommandParameterProperty, new Binding("."));
        knopf.SetBinding(
            UIElement.VisibilityProperty,
            HaltungZeilenStatusConverter.Bindung(HaltungStatusSichtbarkeitConverter.Instance, sichtbarkeitsParameter));
        return knopf;
    }

    /// <summary>Kein Video beziehungsweise kein Protokoll: ein Gedankenstrich mit Hinweis.</summary>
    private static FrameworkElementFactory Fehlt(string sichtbarkeitsParameter, string hinweis)
    {
        var strich = new FrameworkElementFactory(typeof(TextBlock));
        strich.SetValue(TextBlock.TextProperty, "–");
        strich.SetValue(FrameworkElement.ToolTipProperty, hinweis);
        strich.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush");
        strich.SetResourceReference(TextBlock.FontSizeProperty, "TextS");
        strich.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        strich.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        strich.SetBinding(
            UIElement.VisibilityProperty,
            HaltungZeilenStatusConverter.Bindung(HaltungStatusSichtbarkeitConverter.Instance, sichtbarkeitsParameter));
        return strich;
    }

    private static Binding Aufzaehlung(string pfad, string wert)
        => new(pfad)
        {
            Mode = BindingMode.OneWay,
            Converter = AufzaehlungSichtbarkeitConverter.Instance,
            ConverterParameter = wert
        };

    /// <summary>Die Tinte der Zelle; sie folgt Auswahl und Theme.</summary>
    private static Binding ZelleTinte()
        => new("Foreground")
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(DataGridCell), 1)
        };
}
