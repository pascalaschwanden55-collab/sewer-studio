using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Animation;

using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Application.Dossiers.Preview;
using AuswertungPro.Next.Domain.Models.Dossiers;
using AuswertungPro.Next.Infrastructure.Dossiers;
using AuswertungPro.Next.Infrastructure.Dossiers.Preview;
using AuswertungPro.Next.UI.Views.Rendering;

using System.Windows.Input;

using AuswertungPro.Next.UI.Behaviors;

namespace AuswertungPro.Next.UI.Views.Windows;

/// <summary>
/// Zeigt das Dossier Seite fuer Seite so, wie es die Vorlage anordnet, und
/// laesst die Felder dieser Seite direkt daneben ausfuellen. Jede Eingabe
/// erscheint sofort im Blatt; die bearbeitete Stelle blinkt rot auf.
///
/// Gearbeitet wird auf einer KOPIE der Angaben. Erst "Übernehmen" schreibt
/// zurueck — sonst waere "Verwerfen" eine Luege.
/// </summary>
public partial class DossierPreviewWindow : Window
{
    private readonly DossierAreaSettings _area;
    private readonly DossierDefinition _dossier;
    private readonly DossierSnapshot _snapshot;
    private readonly DossierExportRequest _request;
    private readonly IPlanImageConverter _planImages;
    private readonly IPlanImageAdjuster _planAdjuster;

    private readonly DossierPreviewDocument _document;
    private IReadOnlyList<DossierPreviewField> _fields = Array.Empty<DossierPreviewField>();

    private Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);
    private DossierPreviewRenderResult? _render;
    private DossierPreviewTarget? _aktivesFeld;
    private bool _fitPage = true;
    private bool _setztAutomatischenZoom;

    private DossierPreviewWindow(
        DossierExportRequest request,
        DossierAreaSettings area,
        DossierDefinition dossier,
        string templatePath,
        IPlanImageConverter planImages,
        IPlanImageAdjuster planAdjuster)
    {
        InitializeComponent();

        _planImages = planImages;
        _planAdjuster = planAdjuster;

        _area = area;
        _dossier = dossier;
        _snapshot = request.Snapshot;
        _request = request with { Area = area, Dossier = dossier };

        _document = DossierPreviewBuilder.Build(templatePath);
        _values = DossierWordTemplateExportService.BuildValues(
            _request,
            AktuellerVerzeichnisStart());
        // Die berechneten Werte sind die Vorgabe jeder Stelle; eine eigene
        // Angabe des Dossiers sticht sie.
        _fields = DossierPreviewFieldCatalog.Build(
            _area, _dossier, key => _values.TryGetValue(key, out var wert) ? wert : string.Empty);

        var navigation = DossierPreviewNavigation.Build(_document.Pages);
        var navigationView = new ListCollectionView(navigation.ToList());
        navigationView.GroupDescriptions.Add(new PropertyGroupDescription(
            nameof(DossierPreviewNavigationItem.ChapterTitle)));
        PageList.ItemsSource = navigationView;
        if (navigation.Count > 0)
            PageList.SelectedIndex = 0;
    }

    /// <summary>
    /// Zeigt die Vorschau. Liefert die uebernommenen Angaben zurueck oder null,
    /// wenn der Benutzer verworfen hat.
    /// </summary>
    public static (DossierAreaSettings Area, DossierDefinition Dossier)? ShowFor(
        DossierExportRequest request,
        string templatePath,
        IPlanImageConverter planImages,
        IPlanImageAdjuster planAdjuster)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(planImages);
        ArgumentNullException.ThrowIfNull(planAdjuster);

        var area = Kopiere(request.Area);
        var dossier = Kopiere(request.Dossier);

        var window = new DossierPreviewWindow(
            request, area, dossier, templatePath, planImages, planAdjuster)
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };

        return window.ShowDialog() == true ? (area, dossier) : null;
    }

    /// <summary>
    /// Tiefe Kopie. Ein halbtiefer Klon wuerde die Zeilenlisten teilen und
    /// „Verwerfen" wirkungslos machen — dieselbe Regel wie beim Ruecksetzen
    /// nach einem misslungenen Speichern, deshalb derselbe Weg.
    /// </summary>
    private static T Kopiere<T>(T quelle) where T : new()
        => DossierDeepCopy.Of(quelle);

    private void OnPageSelected(object sender, SelectionChangedEventArgs e)
    {
        if (PageList.SelectedItem is not DossierPreviewNavigationItem item)
            return;

        var seite = item.Page;
        _aktivesFeld = null;
        FieldsHeader.Text = $"{item.ChapterTitle} — Seite {seite.Number}";
        BaueFelder(seite, DossierPreviewFieldCatalog.ForPage(
            _fields,
            seite,
            _dossier,
            key => _values.TryGetValue(key, out var wert) ? wert : string.Empty));

        ZeichneBlatt();

        if (_fitPage)
            PasseGanzeSeiteEin();
    }

    /// <summary>
    /// Zeichnet das Blatt neu — bei JEDER Eingabe. Eine Seite traegt wenige
    /// Bloecke, und nur so wachsen die Tabellen sofort mit ihrem Inhalt.
    ///
    /// Die Felder daneben bleiben dabei stehen: wuerden auch sie neu entstehen,
    /// verloere das Textfeld bei jedem Tastendruck den Fokus.
    /// </summary>
    private void ZeichneBlatt()
    {
        if (PageList.SelectedItem is not DossierPreviewNavigationItem item)
            return;

        var seite = item.Page;

        _values = DossierWordTemplateExportService.BuildValues(
            _request,
            AktuellerVerzeichnisStart());

        // Der Uebersichtsplan ist eine Bildmarke, kein Textwert. Sein Pfad wird
        // genau so aufgeloest wie beim Erzeugen des Dossiers — sonst zeigte die
        // Vorschau eine leere Stelle, wo ein relativ hinterlegter Plan liegt.
        _values["Uebersichtsplan"] =
            DossierWordTemplateExportService.ResolvePlanPath(_request) ?? string.Empty;

        _render = DossierPreviewPageRenderer.Render(
            seite,
            key => _values.TryGetValue(key, out var wert) ? wert : string.Empty,
            ZeilenFuer,
            DossierWordTemplateExportService.EmptyRowText,
            urtext => _dossier.TextOverrides.TryGetValue(urtext, out var eigen) ? eigen : null,
            urtext => Feldformat(
                DossierTopicTextFormatting.LiteralStyleKey(urtext),
                _dossier.TextOverrides.TryGetValue(urtext, out var eigen) ? eigen : urtext));

        Sheet.Child = _render.Root;

        if (_aktivesFeld is not null)
            Hervorheben(_aktivesFeld.Value, blinken: false);
    }

    private IReadOnlyList<IReadOnlyDictionary<string, string>> ZeilenFuer(string key) => key switch
    {
        "Themen" => DossierWordTemplateExportService.BuildTopicRows(_area, _dossier, _values),
        "Eigentuemer" => DossierWordTemplateExportService.BuildOwnerRows(_dossier),
        "Aenderungen" => DossierWordTemplateExportService.BuildChangeRows(_dossier),
        "Haltungen" => DossierWordTemplateExportService.BuildHoldingRows(_snapshot),
        _ => Array.Empty<IReadOnlyDictionary<string, string>>()
    };

    /// <summary>
    /// Ein Klick ins Blatt springt zu der Stelle, an der dieser Text
    /// ausgefuellt wird.
    ///
    /// Gesucht wird von der angeklickten Stelle nach aussen: der innerste
    /// Rahmen gewinnt. Sonst faenge man in einer Tabelle immer die ganze
    /// Zeile statt der Zelle, die unter dem Zeiger liegt.
    /// </summary>
    private void OnBlattGeklickt(object sender, MouseButtonEventArgs e)
    {
        _ = sender;

        if (_render is null || e.OriginalSource is not DependencyObject quelle)
            return;

        var rahmenZuZielen = new Dictionary<Border, List<DossierPreviewTarget>>();
        foreach (var (target, rahmen) in _render.Frames)
        {
            foreach (var einzeln in rahmen)
            {
                if (!rahmenZuZielen.TryGetValue(einzeln, out var ziele))
                    rahmenZuZielen[einzeln] = ziele = new List<DossierPreviewTarget>();

                ziele.Add(target);
            }
        }

        var aktuell = quelle;
        while (aktuell is not null)
        {
            if (aktuell is Border rahmen
                && rahmenZuZielen.TryGetValue(rahmen, out var ziele)
                && DossierPreviewTarget.SelectMostSpecific(
                    ziele, _feldStellen.ContainsKey) is { } target
                && SpringeZuFeld(target))
            {
                e.Handled = true;
                return;
            }

            aktuell = VisualTreeSafe.GetParentSafe(aktuell);
        }
    }

    /// <summary>
    /// Laesst die Stelle im Blatt aufblinken und haelt sie danach dezent
    /// umrandet, solange das Feld bearbeitet wird.
    /// </summary>
    private void Hervorheben(DossierPreviewTarget target, bool blinken)
    {
        if (_render is null)
            return;

        foreach (var rahmen in _render.Frames.Values.SelectMany(l => l).Distinct())
        {
            if (rahmen.Tag is DossierPreviewFrameOrigin urzustand)
            {
                rahmen.BorderBrush = urzustand.BorderBrush;
                rahmen.BorderThickness = urzustand.BorderThickness;
            }

            rahmen.Background = Brushes.Transparent;
        }

        if (!_render.Frames.TryGetValue(target, out var stellen))
            return;

        foreach (var rahmen in stellen)
        {
            rahmen.BorderBrush = new SolidColorBrush(Color.FromRgb(0xE0, 0x40, 0x40));
            rahmen.BorderThickness = new Thickness(
                Math.Max(1, rahmen.BorderThickness.Left),
                Math.Max(1, rahmen.BorderThickness.Top),
                Math.Max(1, rahmen.BorderThickness.Right),
                Math.Max(1, rahmen.BorderThickness.Bottom));

            if (!blinken)
                continue;

            var pinsel = new SolidColorBrush(Colors.Transparent);
            rahmen.Background = pinsel;

            var animation = new ColorAnimation
            {
                To = Color.FromArgb(0x55, 0xE0, 0x40, 0x40),
                Duration = TimeSpan.FromMilliseconds(220),
                AutoReverse = true,
                RepeatBehavior = new RepeatBehavior(3)
            };

            pinsel.BeginAnimation(SolidColorBrush.ColorProperty, animation);
        }
    }

    private void OnZoomChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (SheetZoom is null)
            return;

        if (IsLoaded && !_setztAutomatischenZoom)
            _fitPage = false;

        SheetZoom.ScaleX = e.NewValue;
        SheetZoom.ScaleY = e.NewValue;
        ZoomText.Text = (e.NewValue * 100).ToString("0", System.Globalization.CultureInfo.InvariantCulture) + " %";
    }

    private void OnPreviewLoaded(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        _fitPage = true;
        PasseGanzeSeiteEin();
    }

    private void OnSheetViewportChanged(object sender, SizeChangedEventArgs e)
    {
        _ = sender;
        _ = e;
        if (_fitPage)
            PasseGanzeSeiteEin();
    }

    private void OnFitPage(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        _fitPage = true;
        PasseGanzeSeiteEin();
    }

    /// <summary>
    /// Verkleinert das Blatt so weit, dass oben, unten, links und rechts
    /// gleichzeitig sichtbar bleiben. Der Schieberegler darf danach weiterhin
    /// bewusst auf einen Ausschnitt vergrössern.
    /// </summary>
    private void PasseGanzeSeiteEin()
    {
        if (PageList.SelectedItem is not DossierPreviewNavigationItem item)
            return;

        var viewportWidth = SheetScroll.ViewportWidth > 0
            ? SheetScroll.ViewportWidth
            : SheetScroll.ActualWidth;
        var viewportHeight = SheetScroll.ViewportHeight > 0
            ? SheetScroll.ViewportHeight
            : SheetScroll.ActualHeight;
        var scale = DossierPreviewFitCalculator.Calculate(
            viewportWidth,
            viewportHeight,
            item.Page.Geometry.WidthPx,
            item.Page.Geometry.HeightPx,
            surroundingSpace: 60);
        scale = Math.Clamp(scale, ZoomSlider.Minimum, ZoomSlider.Maximum);

        _setztAutomatischenZoom = true;
        try
        {
            ZoomSlider.Value = scale;
        }
        finally
        {
            _setztAutomatischenZoom = false;
        }
    }

    private void OnAccept(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
}
