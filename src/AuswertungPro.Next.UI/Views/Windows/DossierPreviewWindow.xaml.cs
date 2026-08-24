using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Animation;

using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Application.Dossiers.Preview;
using AuswertungPro.Next.Domain.Models.Dossiers;
using AuswertungPro.Next.Infrastructure.Dossiers;
using AuswertungPro.Next.Infrastructure.Dossiers.Preview;
using AuswertungPro.Next.UI.Views.Rendering;

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

    private readonly DossierPreviewDocument _document;
    private IReadOnlyList<DossierPreviewField> _fields = Array.Empty<DossierPreviewField>();

    private Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);
    private DossierPreviewRenderResult? _render;
    private string? _aktivesFeld;

    private DossierPreviewWindow(
        DossierExportRequest request,
        DossierAreaSettings area,
        DossierDefinition dossier,
        string templatePath)
    {
        InitializeComponent();

        _area = area;
        _dossier = dossier;
        _snapshot = request.Snapshot;
        _request = request with { Area = area, Dossier = dossier };

        _document = DossierPreviewBuilder.Build(templatePath);
        _fields = DossierPreviewFieldCatalog.Build(_area, _dossier);

        PageList.ItemsSource = _document.Pages;
        if (_document.Pages.Count > 0)
            PageList.SelectedIndex = 0;
    }

    /// <summary>
    /// Zeigt die Vorschau. Liefert die uebernommenen Angaben zurueck oder null,
    /// wenn der Benutzer verworfen hat.
    /// </summary>
    public static (DossierAreaSettings Area, DossierDefinition Dossier)? ShowFor(
        DossierExportRequest request, string templatePath)
    {
        ArgumentNullException.ThrowIfNull(request);

        var area = Kopiere(request.Area);
        var dossier = Kopiere(request.Dossier);

        var window = new DossierPreviewWindow(request, area, dossier, templatePath)
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };

        return window.ShowDialog() == true ? (area, dossier) : null;
    }

    /// <summary>
    /// Tiefe Kopie ueber JSON. Die Angaben sind reine Datenklassen; ein
    /// halbtiefer Klon wuerde die Zeilenlisten teilen und "Verwerfen"
    /// wirkungslos machen.
    /// </summary>
    private static T Kopiere<T>(T quelle) where T : new()
    {
        var json = JsonSerializer.Serialize(quelle);
        return JsonSerializer.Deserialize<T>(json) ?? new T();
    }

    private void OnPageSelected(object sender, SelectionChangedEventArgs e)
    {
        if (PageList.SelectedItem is not DossierPreviewPage seite)
            return;

        _aktivesFeld = null;
        FieldsHeader.Text = $"Felder auf Seite {seite.Number} — {seite.Title}";
        BaueFelder(DossierPreviewFieldCatalog.ForPage(_fields, seite));
        ZeichneBlatt();
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
        if (PageList.SelectedItem is not DossierPreviewPage seite)
            return;

        _values = DossierWordTemplateExportService.BuildValues(_request);

        // Der Uebersichtsplan ist eine Bildmarke, kein Textwert. Sein Pfad wird
        // genau so aufgeloest wie beim Erzeugen des Dossiers — sonst zeigte die
        // Vorschau eine leere Stelle, wo ein relativ hinterlegter Plan liegt.
        _values["Uebersichtsplan"] =
            DossierWordTemplateExportService.ResolvePlanPath(_request) ?? string.Empty;

        _render = DossierPreviewPageRenderer.Render(
            seite,
            key => _values.TryGetValue(key, out var wert) ? wert : string.Empty,
            ZeilenFuer,
            DossierWordTemplateExportService.EmptyRowText);

        Sheet.Child = _render.Root;

        if (_aktivesFeld is not null)
            Hervorheben(_aktivesFeld, blinken: false);
    }

    private IReadOnlyList<IReadOnlyDictionary<string, string>> ZeilenFuer(string key) => key switch
    {
        "Themen" => DossierWordTemplateExportService.BuildTopicRows(_area, _dossier),
        "Eigentuemer" => DossierWordTemplateExportService.BuildOwnerRows(_dossier),
        "Aenderungen" => DossierWordTemplateExportService.BuildChangeRows(_dossier),
        "Haltungen" => DossierWordTemplateExportService.BuildHoldingRows(_snapshot),
        _ => Array.Empty<IReadOnlyDictionary<string, string>>()
    };

    /// <summary>
    /// Laesst die Stelle im Blatt aufblinken und haelt sie danach dezent
    /// umrandet, solange das Feld bearbeitet wird.
    /// </summary>
    private void Hervorheben(string key, bool blinken)
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

        if (!_render.Frames.TryGetValue(key, out var stellen))
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

        SheetZoom.ScaleX = e.NewValue;
        SheetZoom.ScaleY = e.NewValue;
        ZoomText.Text = (e.NewValue * 100).ToString("0", System.Globalization.CultureInfo.InvariantCulture) + " %";
    }

    private void OnAccept(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
}
