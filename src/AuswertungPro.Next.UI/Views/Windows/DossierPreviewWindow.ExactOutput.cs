using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Threading;

using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Application.Dossiers.Preview;
using AuswertungPro.Next.Infrastructure.Dossiers;
using AuswertungPro.Next.UI.Views.Rendering;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class DossierPreviewWindow
{
    private const uint PreviewRenderWidth = 1800;

    private readonly CancellationTokenSource _previewLifetime = new();
    private readonly DossierExactPreviewState _exactPreviewState = new();
    private IReadOnlyDictionary<int, DossierOutputPreviewTablePageMapping>
        _tableCellMappings =
            new Dictionary<int, DossierOutputPreviewTablePageMapping>();
    private DispatcherTimer? _previewDelay;
    private byte[]? _previewPdfBytes;
    private bool _previewRefreshRunning;

    private void StarteEchteVorschau()
    {
        // Der getippte Text steht sofort im Blatt; diese Pause bestimmt nur,
        // wann das ECHTE Bild nachgezogen wird. Mit dem wiederverwendeten
        // LibreOffice-Profil dauert eine Umwandlung rund eine Sekunde statt
        // 2,4 — deshalb darf die Pause kuerzer sein. Weiteres Tippen waehrend
        // einer laufenden Umwandlung stapelt sich nicht: Der Lauf merkt sich
        // die neueste Fassung und rechnet nur diese.
        _previewDelay = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(300)
        };
        _previewDelay.Tick += OnPreviewDelayElapsed;
        Closed += (_, _) =>
        {
            _previewDelay?.Stop();
            _previewLifetime.Cancel();
            _previewLifetime.Dispose();
        };

        _exactPreviewState.RequestOutputRefresh();
        ZeigeAktualisierungshinweis("Die genaue Ausgabevorschau wird erstellt …");
        AktualisiereVorschauFreigabe();
        _ = AktualisiereEchteVorschauAsync();
    }

    private void FordereEchteVorschauAn()
    {
        _exactPreviewState.RequestOutputRefresh();
        ZeigeAktualisierungshinweis("Die genaue Ausgabe wird aktualisiert …");
        AktualisiereVorschauFreigabe();
        StatusText.Text = "Änderung übernommen — genaues Blatt folgt …";

        if (_previewDelay is null)
            return;

        _previewDelay.Stop();
        _previewDelay.Start();
    }

    private async void OnPreviewDelayElapsed(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;
        _previewDelay?.Stop();
        await AktualisiereEchteVorschauAsync();
    }

    private async Task AktualisiereEchteVorschauAsync()
    {
        if (_previewRefreshRunning || _previewLifetime.IsCancellationRequested)
            return;

        _previewRefreshRunning = true;
        try
        {
            while (!_previewLifetime.IsCancellationRequested
                   && _exactPreviewState.NeedsOutputRefresh)
            {
                var version = _exactPreviewState.RequestedOutputVersion;
                StatusText.Text = "Genaue Word/PDF-Ausgabe wird erstellt …";

                var request = _request with
                {
                    Area = DossierDeepCopy.Of(_area),
                    Dossier = DossierDeepCopy.Of(_dossier)
                };

                DossierOutputPreviewResult result;
                try
                {
                    result = await _outputPreview
                        .CreateAsync(request, _previewLifetime.Token)
                        .ConfigureAwait(true);
                }
                catch (OperationCanceledException) when (_previewLifetime.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    // Falls waehrend des fehlgeschlagenen Laufs weitergeschrieben
                    // wurde, gehoert der Fehler nicht zum neuesten Stand. Dann wird
                    // direkt die aktuelle Fassung erzeugt.
                    if (version != _exactPreviewState.RequestedOutputVersion)
                        continue;

                    _exactPreviewState.TryCompleteOutput(version, success: false);
                    ZeigeAusgabefehler(
                        "Die Ausgabevorschau konnte nicht aktualisiert werden: " + ex.Message);
                    return;
                }

                if (_previewLifetime.IsCancellationRequested)
                    return;

                // Während Word arbeitete, wurde weitergeschrieben. Das alte
                // Ergebnis wird nie kurz eingeblendet; direkt folgt der letzte Stand.
                if (version != _exactPreviewState.RequestedOutputVersion)
                    continue;

                if (!result.Success || result.PdfBytes is null)
                {
                    _exactPreviewState.TryCompleteOutput(version, success: false);
                    ZeigeAusgabefehler(result.Message);
                    return;
                }

                if (!_exactPreviewState.TryCompleteOutput(version, success: true))
                    continue;

                _previewPdfBytes = result.PdfBytes;
                AktualisiereVorschauFreigabe();
                UebernehmeAusgabeseiten(result.Pages, result.Anchors);
                StatusText.Text = result.Message;
            }
        }
        catch (OperationCanceledException) when (_previewLifetime.IsCancellationRequested)
        {
            // Das Fenster wurde geschlossen.
        }
        catch (Exception ex)
        {
            var version = _exactPreviewState.RequestedOutputVersion;
            _exactPreviewState.TryCompleteOutput(version, success: false);
            ZeigeAusgabefehler(
                "Die Ausgabevorschau konnte nicht aktualisiert werden: " + ex.Message);
        }
        finally
        {
            _previewRefreshRunning = false;
        }
    }

    private void UebernehmeAusgabeseiten(
        IReadOnlyList<DossierOutputPreviewPage> pages,
        IReadOnlyList<DossierPdfFieldAnchor>? anchors = null)
    {
        var vorherigeSeite = (PageList.SelectedItem as DossierOutputPreviewNavigationItem)
            ?.OutputPage.Number;
        var navigation = DossierOutputPreviewInteractionMapper.BuildNavigation(
            pages,
            _templateNavigation,
            _dossier,
            _values,
            ZeilenFuer);
        // Die benannten Ziele aus den Word-Textmarken gehen mit: Wo eine Marke
        // vorliegt, ist die Zelle exakt bestimmt - auch wenn sie leer ist oder
        // ihr Text in vielen Zellen gleich lautet. Fehlen sie, gilt unveraendert
        // der bisherige Weg ueber den Text.
        _tableCellMappings = DossierOutputPreviewTableCellMapper.Build(
            navigation,
            _felder.Ziele,
            _fields,
            _values,
            _dossier,
            ZeilenFuer,
            anchors);
        var view = new ListCollectionView(navigation.ToList());
        view.GroupDescriptions.Add(new PropertyGroupDescription(
            nameof(DossierOutputPreviewNavigationItem.ChapterTitle)));
        PageList.ItemsSource = view;

        var selection = vorherigeSeite is { } number
            ? navigation.FirstOrDefault(item => item.OutputPage.Number == number)
            : null;
        PageList.SelectedItem = selection ?? navigation.FirstOrDefault();
    }

    private async Task ZeichneEchteSeiteAsync(DossierOutputPreviewNavigationItem item)
    {
        var renderVersion = _exactPreviewState.BeginPageRender();
        _render = null;
        Sheet.Child = DossierExactPreviewPageRenderer.CreateNotice(
            $"Seite {item.OutputPage.Number} wird angezeigt …",
            pageSized: true);
        AktualisiereVorschauFreigabe();

        if (_previewPdfBytes is null)
            return;

        try
        {
            var bitmap = await _previewPages.RenderAsync(
                _previewPdfBytes,
                item.OutputPage.Number - 1,
                PreviewRenderWidth,
                _previewLifetime.Token).ConfigureAwait(true);

            if (!_exactPreviewState.IsCurrentPageRender(renderVersion)
                || !ReferenceEquals(PageList.SelectedItem, item)
                || _previewLifetime.IsCancellationRequested)
            {
                return;
            }

            var sichtbareZiele = DossierOutputPreviewInteractionMapper.TargetsForPages(
                _felder.Ziele,
                item.EditorPages);
            var candidates = DossierOutputPreviewInteractionMapper.BuildCandidates(
                sichtbareZiele,
                _fields,
                _values,
                _dossier,
                ZeilenFuer);
            var hits = DossierOutputPreviewHitMatcher.Match(
                item.OutputPage.Words,
                candidates);
            _tableCellMappings.TryGetValue(
                item.OutputPage.Number,
                out var tableMapping);
            if (tableMapping is not null)
            {
                hits = DossierOutputPreviewTableCellMapper.RemoveMappedTableTargets(
                    hits,
                    tableMapping.ReplacedPhysicalTargets);
            }
            hits = DossierTocChapterPageClickMapper.AddPageTargets(
                item.OutputPage,
                hits,
                DossierTocChapterPageClickMapper.ChapterTitles(item.EditorPages));
            var emptyCellAreas = (tableMapping?.Areas ?? [])
                .Concat(DossierOutputPreviewEmptyRowCellMapper.Build(
                    item.OutputPage,
                    item.EditorPages,
                    sichtbareZiele,
                    ZeilenFuer,
                    hits).Where(area => tableMapping is null
                    || !tableMapping.ReplacedPhysicalTargets.Contains(area.Target)))
                .Concat(DossierOutputPreviewEmptyFixedCellMapper.Build(
                    item.OutputPage,
                    item.EditorPages,
                    sichtbareZiele,
                    _fields,
                    hits,
                    key => _dossier.TextOverrides.TryGetValue(key, out var own)
                        ? own
                        : key))
                .ToList();
            var planTarget = DossierOutputPreviewInteractionMapper.ContainsPlanLocation(
                    item.OutputPage,
                    item.EditorPages)
                    ? DossierPreviewTarget.Field("Uebersichtsplan")
                    : (DossierPreviewTarget?)null;
            var render = DossierExactPreviewPageRenderer.Render(
                bitmap,
                item.OutputPage,
                hits,
                planTarget,
                emptyCellAreas);

            if (!_exactPreviewState.TryCompletePage(renderVersion, success: true))
                return;

            _render = render;
            Sheet.Child = render.Root;
            AktualisiereVorschauFreigabe();

            if (_aktivesFeld is not null)
                Hervorheben(_aktivesFeld.Value, blinken: false);

            if (_fitPage)
                PasseGanzeSeiteEin();
        }
        catch (OperationCanceledException) when (_previewLifetime.IsCancellationRequested)
        {
            // Das Fenster wurde geschlossen.
        }
        catch (Exception ex)
        {
            if (_exactPreviewState.TryCompletePage(renderVersion, success: false))
            {
                var message = "Seite konnte nicht angezeigt werden: " + ex.Message;
                _render = null;
                Sheet.Child = DossierExactPreviewPageRenderer.CreateNotice(
                    message,
                    pageSized: true);
                StatusText.Text = message;
                AktualisiereVorschauFreigabe();
            }
        }
    }

    private void ZeigeAusgabefehler(string message)
    {
        _previewPdfBytes = null;
        _render = null;
        PageList.SelectedItem = null;
        PageList.ItemsSource = null;
        FieldsHeader.Text = "Felder — aktueller Arbeitsstand";
        Sheet.Child = DossierExactPreviewPageRenderer.CreateNotice(
            message,
            pageSized: true);
        StatusText.Text = message;
        AktualisiereVorschauFreigabe();
    }

    private void ZeigeAktualisierungshinweis(string message)
    {
        _render = null;
        Sheet.Child = DossierExactPreviewPageRenderer.CreateNotice(
            message,
            pageSized: true);
    }

    private void AktualisiereVorschauFreigabe()
    {
        Sheet.IsHitTestVisible = _exactPreviewState.CanInteractWithPage;
        AcceptButton.IsEnabled = _exactPreviewState.CanAccept;
    }

}
