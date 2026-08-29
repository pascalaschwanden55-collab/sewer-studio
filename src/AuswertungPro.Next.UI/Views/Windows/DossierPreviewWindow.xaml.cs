using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Application.Dossiers.Preview;
using AuswertungPro.Next.Domain.Models.Dossiers;
using AuswertungPro.Next.Infrastructure.Dossiers;
using AuswertungPro.Next.Infrastructure.Dossiers.Preview;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.Views.Rendering;

using System.Windows.Input;

using AuswertungPro.Next.UI.Behaviors;

namespace AuswertungPro.Next.UI.Views.Windows;

/// <summary>
/// Zeigt das Dossier Seite fuer Seite aus der echten Word/PDF-Ausgabe und
/// laesst die Felder der zugeordneten Seite direkt daneben ausfuellen. Nach
/// einer kurzen Schreibpause wird die Ausgabe neu erzeugt; die bearbeitete
/// Stelle bleibt über den PDF-Text direkt anklickbar.
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
    private readonly IDossierPlanPublicationService _planPublications;
    private readonly DossierPlanWorkSession _planWorkSession = new();
    private readonly IDossierOutputPreviewService _outputPreview;
    private readonly IDossierPreviewPageRasterizer _previewPages;

    private readonly DossierPreviewDocument _document;
    private readonly IReadOnlyList<DossierPreviewNavigationItem> _templateNavigation;
    private IReadOnlyList<DossierPreviewField> _fields = Array.Empty<DossierPreviewField>();

    private Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);
    private DossierPreviewRenderResult? _render;
    private IDossierPlanPublication? _publishedPlan;
    private DossierPreviewTarget? _aktivesFeld;

    /// <summary>Die Vorlagenseiten, fuer die die Eingaben gerade gebaut sind.</summary>
    private IReadOnlyList<DossierPreviewPage>? _gebauteSeiten;
    private DossierPreviewFieldPanel _felder = null!;
    private bool _fitPage = true;
    private bool _setztAutomatischenZoom;

    private DossierPreviewWindow(
        DossierExportRequest request,
        DossierAreaSettings area,
        DossierDefinition dossier,
        string templatePath,
        IPlanImageConverter planImages,
        IPlanImageAdjuster planAdjuster,
        IDossierPlanPublicationService planPublications,
        IDossierOutputPreviewService outputPreview,
        IDossierPreviewPageRasterizer previewPages)
    {
        InitializeComponent();

        _planImages = planImages;
        _planAdjuster = planAdjuster;
        _planPublications = planPublications;
        _outputPreview = outputPreview;
        _previewPages = previewPages;

        _area = area;
        _dossier = dossier;
        _snapshot = request.Snapshot;
        _request = request with { Area = area, Dossier = dossier };

        // Die drei Word-Tabellen besitzen auch ohne Fachdaten eine sichtbare
        // Grundzeile. Sie wird vor der ERSTEN exakten Ausgabe angelegt, damit
        // Blatt, Klickziele und rechte Eingaben bereits beim Oeffnen denselben
        // Stand zeigen. Unbenutzte Eingabehilfen werden bei Uebernahme entfernt.
        DossierChangeRows.EnsureStarter(_dossier);
        DossierOwnerRows.EnsureStarter(_dossier);
        DossierTopicRows.EnsureStarter(_area, _dossier);

        _document = DossierPreviewBuilder.Build(templatePath);
        _values = DossierWordTemplateExportService.BuildValues(
            _request,
            DossierPreviewFieldPanel.VerzeichnisStart(_document, _dossier));

        _felder = new DossierPreviewFieldPanel(
            FieldPanel,
            _area,
            _dossier,
            _planWorkSession.WorkFolder,
            _document,
            _planImages,
            _planAdjuster,
            () => _values,
            ZeichneBlatt,
            Betone,
            (ziel, blinken) => Hervorheben(ziel, blinken),
            FindResource,
            text => StatusText.Text = text,
            () => this);
        // Die berechneten Werte sind die Vorgabe jeder Stelle; eine eigene
        // Angabe des Dossiers sticht sie.
        _fields = DossierPreviewFieldCatalog.Build(
            _area, _dossier, key => _values.TryGetValue(key, out var wert) ? wert : string.Empty);

        _templateNavigation = DossierPreviewNavigation.Build(_document.Pages);
        Sheet.Child = DossierExactPreviewPageRenderer.CreateNotice(
            "Die genaue Word/PDF-Vorschau wird erstellt …",
            pageSized: true);
    }

    /// <summary>
    /// Zeigt die Vorschau. Liefert die uebernommenen Angaben zurueck oder null,
    /// wenn der Benutzer verworfen hat.
    /// </summary>
    public static DossierPreviewChoice? ShowFor(
        DossierExportRequest request,
        string templatePath,
        IPlanImageConverter planImages,
        IPlanImageAdjuster planAdjuster,
        IDossierPlanPublicationService planPublications,
        IDossierOutputPreviewService outputPreview,
        IDossierPreviewPageRasterizer previewPages)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(planImages);
        ArgumentNullException.ThrowIfNull(planAdjuster);
        ArgumentNullException.ThrowIfNull(planPublications);
        ArgumentNullException.ThrowIfNull(outputPreview);
        ArgumentNullException.ThrowIfNull(previewPages);

        var area = Kopiere(request.Area);
        var dossier = Kopiere(request.Dossier);

        var window = new DossierPreviewWindow(
            request,
            area,
            dossier,
            templatePath,
            planImages,
            planAdjuster,
            planPublications,
            outputPreview,
            previewPages)
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };

        try
        {
            if (window.ShowDialog() != true)
                return null;

            // Die Vorschau stellt die im Word sichtbare Grundzeile als echte
            // Eingabe bereit. Bleibt sie leer, ist sie kein fachlicher
            // Aenderungseintrag und wird nicht in dossiers.json uebernommen.
            DossierChangeRows.RemoveEmpty(dossier);
            DossierOwnerRows.RemoveEmpty(dossier);
            DossierTopicRows.RemoveEmpty(dossier);

            var publication = window._publishedPlan;
            window._publishedPlan = null;
            return new DossierPreviewChoice(area, dossier, publication);
        }
        finally
        {
            // Falls das Fenster nach dem Kopieren unerwartet nicht als
            // uebernommen endet, bleibt keine verwaiste Plandatei zurueck.
            window._publishedPlan?.Dispose();
            window._publishedPlan = null;
        }
    }

    /// <summary>
    /// Tiefe Kopie. Ein halbtiefer Klon wuerde die Zeilenlisten teilen und
    /// „Verwerfen" wirkungslos machen — dieselbe Regel wie beim Ruecksetzen
    /// nach einem misslungenen Speichern, deshalb derselbe Weg.
    /// </summary>
    private static T Kopiere<T>(T quelle) where T : new()
        => DossierDeepCopy.Of(quelle);

    /// <summary>
    /// Alle Vorlagenseiten des Dossiers, in Dokumentreihenfolge und ohne
    /// Wiederholung. Die Eingabeseite baut daraus einmal ihre Felder.
    /// </summary>
    private IReadOnlyList<DossierPreviewPage> AlleVorlagenseiten()
        => _templateNavigation
            .Select(eintrag => eintrag.Page)
            .Distinct()
            .ToList();

    private async void OnPageSelected(object sender, SelectionChangedEventArgs e)
    {
        _ = sender;
        _ = e;

        if (PageList.SelectedItem is not DossierOutputPreviewNavigationItem item)
            return;

        FieldsHeader.Text = item.OutputPage.IsConditionClassExplanation
            ? $"Erklärblatt Zustandsklassen — Seite {item.OutputPage.Number} · automatisch erzeugt"
            : item.EditorPage is null
                ? $"Beilage — Seite {item.OutputPage.Number} · Original, wird nicht verändert"
                : $"{item.ChapterTitle} — Seite {item.OutputPage.Number}";

        // Die Eingaben gelten fuer das GANZE Word-Dokument, nicht fuer die
        // gewaehlte Seite. Damit findet ein Klick sein Feld auch dann, wenn der
        // Text auf einem anderen Blatt steht — und beim Blaettern bleibt der
        // Cursor stehen, weil nichts neu gebaut wird.
        var alleSeiten = AlleVorlagenseiten();

        if (DossierPreviewFieldRebuild.IstNoetig(_gebauteSeiten, alleSeiten))
        {
            _aktivesFeld = null;
            _gebauteSeiten = alleSeiten;

            _felder.Baue(DossierPreviewFieldCatalog.ForPages(
                _fields,
                alleSeiten,
                _dossier,
                key => _values.TryGetValue(key, out var wert) ? wert : string.Empty));
        }

        await ZeichneEchteSeiteAsync(item);

        if (_fitPage)
            PasseGanzeSeiteEin();
    }

    /// <summary>
    /// Fordert nach einer kurzen Schreibpause eine neue echte Ausgabe an. Das
    /// vermeidet einen Word-Start für jeden einzelnen Buchstaben.
    /// </summary>
    private void ZeichneBlatt()
    {
        _values = DossierWordTemplateExportService.BuildValues(
            _request,
            DossierPreviewFieldPanel.VerzeichnisStart(_document, _dossier));

        // Der Uebersichtsplan ist eine Bildmarke, kein Textwert. Sein Pfad wird
        // genau so aufgeloest wie beim Erzeugen des Dossiers — sonst zeigte die
        // Vorschau eine leere Stelle, wo ein relativ hinterlegter Plan liegt.
        _values["Uebersichtsplan"] =
            DossierWordTemplateExportService.ResolvePlanPath(_request) ?? string.Empty;

        ZeigeSofort();
        FordereEchteVorschauAn();
    }

    /// <summary>Merkt die bearbeitete Stelle und laesst sie aufblinken.</summary>
    private void Betone(DossierPreviewTarget target)
    {
        _aktivesFeld = target;
        Hervorheben(target, blinken: true);
    }

    /// <summary>
    /// Zeigt den gerade getippten Text sofort im Blatt.
    ///
    /// Die echte Word/PDF-Ausgabe braucht auf diesem Rechner ueber zwei
    /// Sekunden; so lange darf man seinen eigenen Text nicht suchen muessen.
    /// Diese Anzeige ist eine Naeherung des Wortlauts und wird vom echten Bild
    /// abgeloest, sobald es da ist.
    /// </summary>
    private void ZeigeSofort()
    {
        if (_render?.Overlay is not { } blatt)
            return;

        if (_aktivesFeld is not { } ziel
            || !_render.Frames.TryGetValue(ziel, out var stellen))
        {
            DossierPreviewLiveText.Entferne(blatt);
            return;
        }

        var text = DossierOutputPreviewInteractionMapper
            .BuildCandidates([ziel], _fields, _values, _dossier, ZeilenFuer)
            .Select(kandidat => kandidat.Text)
            .FirstOrDefault() ?? string.Empty;

        DossierPreviewLiveText.Zeige(blatt, stellen, text);
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

        if (!_exactPreviewState.CanInteractWithPage
            || _render is null
            || e.OriginalSource is not DependencyObject quelle)
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
                    ziele, _felder.Kennt) is { } target
                && _felder.SpringeZu(target))
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
        StarteEchteVorschau();
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
        if (PageList.SelectedItem is not DossierOutputPreviewNavigationItem item)
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
            item.OutputPage.Width * 96d / 72d,
            item.OutputPage.Height * 96d / 72d,
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
        _ = sender;
        _ = e;

        if (!_exactPreviewState.CanAccept)
        {
            StatusText.Text = "Übernehmen ist erst nach einer erfolgreichen aktuellen Vorschau möglich.";
            return;
        }

        var plan = _planWorkSession.Publish(
            _planPublications,
            _request.ProjectRoot,
            _dossier.OverviewPlanPath,
            _request.TargetFolder);

        if (!plan.Success)
        {
            StatusText.Text = plan.Error ?? "Der bearbeitete Plan konnte nicht übernommen werden.";
            return;
        }

        _dossier.OverviewPlanPath = plan.ImagePath ?? string.Empty;
        _publishedPlan = plan.Publication;
        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;

    protected override void OnClosed(EventArgs e)
    {
        try
        {
            // base.OnClosed loest zuerst die vorhandenen Closed-Empfaenger
            // aus. Dadurch wird eine laufende Ausgabevorschau abgebrochen,
            // bevor deren Plan-Arbeitsdateien entfernt werden.
            base.OnClosed(e);
        }
        finally
        {
            _planWorkSession.Dispose();
        }
    }
}
