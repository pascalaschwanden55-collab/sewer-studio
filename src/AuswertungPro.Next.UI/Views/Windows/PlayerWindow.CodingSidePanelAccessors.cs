using System.Windows.Controls;
using System.Windows.Documents;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;
using AuswertungPro.Next.UI.Helpers;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private Border CodingSidePanel => CodingSidePanelControl.CodingSidePanel;
    private TextBlock TxtQualityGateStatus => CodingSidePanelControl.TxtQualityGateStatus;
    private Button BtnRunCodingProtocolMatch => CodingSidePanelControl.BtnRunCodingProtocolMatch;
    private TextBlock TxtCodingProtocolMatchSummary => CodingSidePanelControl.TxtCodingProtocolMatchSummary;
    private Button BtnAcceptGreenCodingMatches => CodingSidePanelControl.BtnAcceptGreenCodingMatches;
    private ColumnDefinition ColDefectDetail => CodingSidePanelControl.ColDefectDetail;
    private Run RunCodingDefectCount => CodingSidePanelControl.RunCodingDefectCount;
    private Run RunCodingOpenCount => CodingSidePanelControl.RunCodingOpenCount;
    private ListBox LstCodingEvents => CodingSidePanelControl.LstCodingEvents;
    private Border CodingDefectDetailInline => CodingSidePanelControl.CodingDefectDetailInline;
    private TextBlock TxtInlineDetailCode => CodingSidePanelControl.TxtInlineDetailCode;
    private TextBlock TxtInlineDetailDesc => CodingSidePanelControl.TxtInlineDetailDesc;
    private TextBlock TxtInlineDetailDistance => CodingSidePanelControl.TxtInlineDetailDistance;
    private TextBlock TxtInlineDetailConfidence => CodingSidePanelControl.TxtInlineDetailConfidence;
    private TextBlock TxtInlineDetailStatus => CodingSidePanelControl.TxtInlineDetailStatus;
    private Image ImgInlineEvidencePreview => CodingSidePanelControl.ImgInlineEvidencePreview;
    private TextBlock TxtInlineEvidencePreviewStatus => CodingSidePanelControl.TxtInlineEvidencePreviewStatus;
    private Button BtnInlineAccept => CodingSidePanelControl.BtnInlineAccept;
    private Button BtnInlineReject => CodingSidePanelControl.BtnInlineReject;
    private Run RunImportDefectCount => CodingSidePanelControl.RunImportDefectCount;
    private ListBox LstImportEvents => CodingSidePanelControl.LstImportEvents;
    private TextBlock TxtCodingCalibDn => CodingSidePanelControl.TxtCodingCalibDn;
    private TextBlock TxtCodingCalibStatus => CodingSidePanelControl.TxtCodingCalibStatus;
    private TextBlock TxtCodingQ1 => CodingSidePanelControl.TxtCodingQ1;
    private TextBlock TxtCodingQ2 => CodingSidePanelControl.TxtCodingQ2;
    private TextBlock TxtCodingClock => CodingSidePanelControl.TxtCodingClock;
    private TextBlock TxtCodingArc => CodingSidePanelControl.TxtCodingArc;
    private Button BtnCodingSelectCode => CodingSidePanelControl.BtnCodingSelectCode;
    private TextBlock TxtCodingSelectedCode => CodingSidePanelControl.TxtCodingSelectedCode;
    private Button BtnCodingCreateEvent => CodingSidePanelControl.BtnCodingCreateEvent;
    private TextBlock TxtCodingStatAiCriteriaMet => CodingSidePanelControl.TxtCodingStatAiCriteriaMet;
    private TextBlock TxtCodingStatHumanAccepted => CodingSidePanelControl.TxtCodingStatHumanAccepted;
    private TextBlock TxtCodingStatHumanCorrected => CodingSidePanelControl.TxtCodingStatHumanCorrected;
    private TextBlock TxtCodingStatRejected => CodingSidePanelControl.TxtCodingStatRejected;
    private TextBlock TxtCodingStatOpen => CodingSidePanelControl.TxtCodingStatOpen;
    private TextBlock TxtCodingStatAvgAiConfidence => CodingSidePanelControl.TxtCodingStatAvgAiConfidence;

    private void WireCodingSidePanelEvents()
        => PlayerCodingSidePanelEventBinder.Bind(
            CodingSidePanelControl,
            new PlayerCodingSidePanelEventHandlers(
                CodingTakePhoto: CodingTakePhoto_Click,
                CodingEventsPreviewMouseRightButtonDown: CodingEvents_PreviewMouseRightButtonDown,
                CodingEventsDoubleClick: CodingEvents_DoubleClick,
                CodingEventsSelectionChanged: CodingEvents_SelectionChanged,
                CodingEventEdit: CodingEventEdit_Click,
                CodingEventShowPhotos: CodingEventShowPhotos_Click,
                CodingEventCloseStretch: CodingEventCloseStretch_Click,
                CodingEventSeek: CodingEventSeek_Click,
                CodingEventDelete: CodingEventDelete_Click,
                CodingAcceptDefect: (_, _) => _codingInlineDefectController
                    .AcceptAsync()
                    .SafeFireAndForget("TrainingSaveAcceptInline"),
                CodingEditDefect: (_, _) => _codingInlineDefectController
                    .EditAsync()
                    .SafeFireAndForget("TrainingSaveEditInline"),
                CodingRejectDefect: (_, _) => _codingInlineDefectController.Reject(),
                ImportEventsDoubleClick: (_, _) => _codingProtocolMatchController.SeekSelectedImportEvent(),
                ImportConfirm: ImportConfirm_Click,
                ImportSeek: (_, _) => _codingProtocolMatchController.SeekSelectedImportEvent(),
                CodingSelectCode: CodingSelectCode_Click,
                CodingCreateEvent: CodingCreateEvent_Click,
                CodingProtocolMatch: (_, _) => _codingProtocolMatchController.RunMatch(),
                CodingAcceptGreenMatches: CodingAcceptGreenMatches_Click,
                ImportShowPhotos: ImportShowPhotos_Click,
                ImportEdit: ImportEdit_Click,
                ImportConfirmToBrain: ImportConfirmToBrain_Click,
                SuggestionsDoubleClick: (_, _) => SuggestionSeek_Click(this, new System.Windows.RoutedEventArgs()),
                SuggestionSeek: SuggestionSeek_Click,
                SuggestionConfirm: SuggestionConfirm_Click,
                SuggestionReject: SuggestionReject_Click));

    private void InitializeCodingSidePanelControllers()
    {
        PlayerCodingSidePanelControllerInitializer.Initialize(
            _codingSidePanelControllers,
            CodingSidePanelControl,
            new CodingSidePanelControllerActions(
                RefreshEvents: RefreshCodingEventsList,
                SelectCreatedEvent: ev => _codingSidePanelControllers.EventsList.SelectEvent(ev),
                CancelSchema: () => _codingSchemaManager.Cancel(),
                ClearCurrentOverlay: _codingSessionHost.ClearCurrentOverlay,
                ClearSelectedCode: _codingSessionHost.ClearSelectedCode,
                RedrawCanvas: () => RedrawCodingCanvas(includeManualOverlay: false),
                ClearSelectedCodeText: () => CodingSelectedCodeControls.Clear(TxtCodingSelectedCode),
                DisableCreateEvent: () => CodingOverlayInputControls.SetCreateEventEnabled(BtnCodingCreateEvent, false),
                ClearOverlayInfo: () => UpdateCodingOverlayInfo(null)));
    }
}
