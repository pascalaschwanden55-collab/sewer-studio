using System;
using System.Threading.Tasks;
using System.Windows;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Helpers;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private async Task SubmitEingabemarker()
    {
        var keyword = TxtEingabemarker.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(keyword)) return;

        CodingEingabemarkerPopupControls.Hide(EingabemarkerPopup);
        _eingabemarkerPhase = EingabemarkerPhase.Analyzing;

        var codeHint = ResolveEingabemarkerCodeHint(keyword);

        try
        {
            if (_codingSessionHost.HasViewModel && codeHint != null)
            {
                var checkMeter = _codingOsdMeterController.LastMeter ?? _codingSessionHost.CurrentMeter;
                var existingDup = CodingEingabemarkerDuplicatePolicy.FindDuplicate(
                    _codingSessionHost.Events,
                    codeHint,
                    checkMeter);
                if (existingDup != null)
                {
                    SetCodingAiState(
                        $"{codeHint} bereits vorhanden bei {existingDup.MeterAtCapture:F2}m - Duplikat",
                        PlayerStatusColors.Warning, "");
                    return;
                }
            }

            if (codeHint != null && _codingSessionHost.HasViewModel && _codingSessionRuntimeOwner.Service != null)
            {
                var meter = _codingOsdMeterController.LastMeter ?? _codingSessionHost.CurrentMeter;
                var videoTime = _codingSessionHost.CurrentVideoTime ?? _playerTimelineHost.CurrentTimeOrZero;
                var label = LookupVsaLabel(codeHint) ?? keyword;

                var draft = CodingEingabemarkerEventFactory.CreateAccepted(
                    codeHint,
                    label,
                    keyword,
                    meter,
                    videoTime);

                var fotoPath = CodingCaptureSnapshot(draft.Entry);
                CodingProtocolEntryPhotoPathAppender.AddIfPresent(draft.Entry, fotoPath);

                var ev = CodingEingabemarkerEventAppender.Apply(draft, _codingSessionHost.CurrentOverlay, _codingSessionRuntimeOwner.Service);
                RefreshCodingEventsList();
                UpdateToolBadge();
                PersistSingleEventAsTrainingSample(ev).SafeFireAndForget("TrainingSaveSingle");
                SetCodingAiState($"{codeHint} {label} bei {meter:F2}m eingetragen",
                    PlayerStatusColors.Success, "");
            }
            else
            {
                SetCodingAiState($"KI analysiert: \"{keyword}\" ...",
                    PlayerStatusColors.Warning, "Qwen analysiert");
                await RunCodingAnalysisAsync(
                    $"Eingabemarker: {keyword}",
                    disableAnalyzeButton: true,
                    keywordHint: keyword,
                    codeHint: null);
            }
        }
        catch (Exception ex)
        {
            SetCodingAiState($"Fehler: {ex.Message}", PlayerStatusColors.Error, "");
        }
        finally
        {
            CancelEingabemarker();
        }
    }
}
