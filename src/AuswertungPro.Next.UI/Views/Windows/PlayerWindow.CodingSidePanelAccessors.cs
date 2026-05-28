using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private Border CodingSidePanel => CodingSidePanelControl.CodingSidePanel;
    private TextBlock TxtQualityGateStatus => CodingSidePanelControl.TxtQualityGateStatus;
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
    private Button BtnInlineAccept => CodingSidePanelControl.BtnInlineAccept;
    private Button BtnInlineReject => CodingSidePanelControl.BtnInlineReject;
    private Run RunImportDefectCount => CodingSidePanelControl.RunImportDefectCount;
    private ListBox LstImportEvents => CodingSidePanelControl.LstImportEvents;
    private Border CodingDefectDetailPanel => CodingSidePanelControl.CodingDefectDetailPanel;
    private SolidColorBrush CodingDefectDetailBorderBrush => CodingSidePanelControl.CodingDefectDetailBorderBrush;
    private TextBlock TxtCodingDetailCode => CodingSidePanelControl.TxtCodingDetailCode;
    private TextBlock TxtCodingDetailSeverity => CodingSidePanelControl.TxtCodingDetailSeverity;
    private TextBlock TxtCodingDetailDescription => CodingSidePanelControl.TxtCodingDetailDescription;
    private TextBlock TxtCodingDetailDistance => CodingSidePanelControl.TxtCodingDetailDistance;
    private TextBlock TxtCodingDetailClock => CodingSidePanelControl.TxtCodingDetailClock;
    private TextBlock TxtCodingDetailConfidence => CodingSidePanelControl.TxtCodingDetailConfidence;
    private TextBlock TxtCodingDetailStatus => CodingSidePanelControl.TxtCodingDetailStatus;
    private Grid CodingDefectActionGrid => CodingSidePanelControl.CodingDefectActionGrid;
    private Button BtnCodingAcceptDefect => CodingSidePanelControl.BtnCodingAcceptDefect;
    private Button BtnCodingEditDefect => CodingSidePanelControl.BtnCodingEditDefect;
    private Button BtnCodingRejectDefect => CodingSidePanelControl.BtnCodingRejectDefect;
    private TextBlock TxtCodingCalibDn => CodingSidePanelControl.TxtCodingCalibDn;
    private TextBlock TxtCodingCalibStatus => CodingSidePanelControl.TxtCodingCalibStatus;
    private TextBlock TxtCodingQ1 => CodingSidePanelControl.TxtCodingQ1;
    private TextBlock TxtCodingQ2 => CodingSidePanelControl.TxtCodingQ2;
    private TextBlock TxtCodingClock => CodingSidePanelControl.TxtCodingClock;
    private TextBlock TxtCodingArc => CodingSidePanelControl.TxtCodingArc;
    private Button BtnCodingSelectCode => CodingSidePanelControl.BtnCodingSelectCode;
    private TextBlock TxtCodingSelectedCode => CodingSidePanelControl.TxtCodingSelectedCode;
    private Button BtnCodingCreateEvent => CodingSidePanelControl.BtnCodingCreateEvent;
    private TextBlock TxtCodingStatAutoAccepted => CodingSidePanelControl.TxtCodingStatAutoAccepted;
    private TextBlock TxtCodingStatPending => CodingSidePanelControl.TxtCodingStatPending;
    private TextBlock TxtCodingStatReviewRequired => CodingSidePanelControl.TxtCodingStatReviewRequired;
    private TextBlock TxtCodingStatAvgConfidence => CodingSidePanelControl.TxtCodingStatAvgConfidence;

    private void WireCodingSidePanelEvents()
    {
        CodingSidePanelControl.CodingTakePhotoRequested += CodingTakePhoto_Click;
        CodingSidePanelControl.CodingEventsPreviewMouseRightButtonDownRequested += CodingEvents_PreviewMouseRightButtonDown;
        CodingSidePanelControl.CodingEventsDoubleClickRequested += CodingEvents_DoubleClick;
        CodingSidePanelControl.CodingEventsSelectionChangedRequested += CodingEvents_SelectionChanged;
        CodingSidePanelControl.CodingEventEditRequested += CodingEventEdit_Click;
        CodingSidePanelControl.CodingEventShowPhotosRequested += CodingEventShowPhotos_Click;
        CodingSidePanelControl.CodingEventCloseStretchRequested += CodingEventCloseStretch_Click;
        CodingSidePanelControl.CodingEventSeekRequested += CodingEventSeek_Click;
        CodingSidePanelControl.CodingEventDeleteRequested += CodingEventDelete_Click;
        CodingSidePanelControl.CodingAcceptDefectRequested += CodingAcceptDefect_Click;
        CodingSidePanelControl.CodingEditDefectRequested += CodingEditDefect_Click;
        CodingSidePanelControl.CodingRejectDefectRequested += CodingRejectDefect_Click;
        CodingSidePanelControl.ImportEventsDoubleClickRequested += ImportEvents_DoubleClick;
        CodingSidePanelControl.ImportConfirmRequested += ImportConfirm_Click;
        CodingSidePanelControl.ImportSeekRequested += ImportSeek_Click;
        CodingSidePanelControl.CodingSelectCodeRequested += CodingSelectCode_Click;
        CodingSidePanelControl.CodingCreateEventRequested += CodingCreateEvent_Click;
    }
}
