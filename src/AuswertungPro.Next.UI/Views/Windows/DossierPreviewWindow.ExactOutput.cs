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
    private DispatcherTimer? _previewDelay;
    private byte[]? _previewPdfBytes;
    private int _previewRequestedVersion;
    private int _previewAppliedVersion;
    private int _pageRenderVersion;
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

        _previewRequestedVersion++;
        _ = AktualisiereEchteVorschauAsync();
    }

    private void FordereEchteVorschauAn()
    {
        _previewRequestedVersion++;
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
                   && _previewAppliedVersion != _previewRequestedVersion)
            {
                var version = _previewRequestedVersion;
                StatusText.Text = "Genaue Word/PDF-Ausgabe wird erstellt …";

                var request = _request with
                {
                    Area = DossierDeepCopy.Of(_area),
                    Dossier = DossierDeepCopy.Of(_dossier)
                };

                var result = await _outputPreview
                    .CreateAsync(request, _previewLifetime.Token)
                    .ConfigureAwait(true);

                if (_previewLifetime.IsCancellationRequested)
                    return;

                // Während Word arbeitete, wurde weitergeschrieben. Das alte
                // Ergebnis wird nie kurz eingeblendet; direkt folgt der letzte Stand.
                if (version != _previewRequestedVersion)
                    continue;

                _previewAppliedVersion = version;
                if (!result.Success || result.PdfBytes is null)
                {
                    StatusText.Text = result.Message;
                    if (_previewPdfBytes is null)
                        Sheet.Child = DossierExactPreviewPageRenderer.CreateNotice(
                            result.Message,
                            pageSized: true);
                    return;
                }

                _previewPdfBytes = result.PdfBytes;
                UebernehmeAusgabeseiten(result.Pages);
                StatusText.Text = result.Message;
            }
        }
        catch (OperationCanceledException) when (_previewLifetime.IsCancellationRequested)
        {
            // Das Fenster wurde geschlossen.
        }
        catch (Exception ex)
        {
            StatusText.Text = "Die Ausgabevorschau konnte nicht aktualisiert werden: " + ex.Message;
        }
        finally
        {
            _previewRefreshRunning = false;
        }
    }

    private void UebernehmeAusgabeseiten(IReadOnlyList<DossierOutputPreviewPage> pages)
    {
        var vorherigeSeite = (PageList.SelectedItem as DossierOutputPreviewNavigationItem)
            ?.OutputPage.Number;
        var navigation = DossierOutputPreviewInteractionMapper.BuildNavigation(
            pages,
            _templateNavigation,
            _dossier,
            _values,
            ZeilenFuer);
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
        if (_previewPdfBytes is null)
            return;

        var renderVersion = ++_pageRenderVersion;
        try
        {
            var bitmap = await _previewPages.RenderAsync(
                _previewPdfBytes,
                item.OutputPage.Number - 1,
                PreviewRenderWidth,
                _previewLifetime.Token).ConfigureAwait(true);

            if (renderVersion != _pageRenderVersion
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
            hits = DossierTocChapterPageClickMapper.AddPageTargets(
                item.OutputPage,
                hits,
                DossierTocChapterPageClickMapper.ChapterTitles(item.EditorPages));
            var planTarget = DossierOutputPreviewInteractionMapper.ContainsPlanLocation(
                    item.OutputPage,
                    item.EditorPages)
                    ? DossierPreviewTarget.Field("Uebersichtsplan")
                    : (DossierPreviewTarget?)null;
            _render = DossierExactPreviewPageRenderer.Render(
                bitmap,
                item.OutputPage,
                hits,
                planTarget);
            Sheet.Child = _render.Root;

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
            StatusText.Text = "Seite konnte nicht angezeigt werden: " + ex.Message;
        }
    }

}
