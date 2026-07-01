using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public static class PlayerCodingSidePanelControllerInitializer
{
    public static void Initialize(
        CodingSidePanelControllerSet controllers,
        PlayerCodingSidePanel sidePanel,
        CodingSidePanelControllerActions actions)
    {
        ArgumentNullException.ThrowIfNull(controllers);
        ArgumentNullException.ThrowIfNull(sidePanel);
        ArgumentNullException.ThrowIfNull(actions);

        controllers.Initialize(
            new CodingSidePanelControllerControls(
                CodingEvents: sidePanel.LstCodingEvents,
                CodingDefectCount: sidePanel.RunCodingDefectCount,
                CodingOpenCount: sidePanel.RunCodingOpenCount,
                CodingStatAutoAccepted: sidePanel.TxtCodingStatAutoAccepted,
                CodingStatPending: sidePanel.TxtCodingStatPending,
                CodingStatReviewRequired: sidePanel.TxtCodingStatReviewRequired,
                CodingStatAvgConfidence: sidePanel.TxtCodingStatAvgConfidence,
                InlineDetailCode: sidePanel.TxtInlineDetailCode,
                InlineDetailDescription: sidePanel.TxtInlineDetailDesc,
                InlineDetailDistance: sidePanel.TxtInlineDetailDistance,
                InlineDetailConfidence: sidePanel.TxtInlineDetailConfidence,
                InlineDetailStatus: sidePanel.TxtInlineDetailStatus,
                InlineEvidencePreview: sidePanel.ImgInlineEvidencePreview,
                InlineEvidencePreviewStatus: sidePanel.TxtInlineEvidencePreviewStatus,
                InlineAccept: sidePanel.BtnInlineAccept,
                InlineReject: sidePanel.BtnInlineReject,
                DefectDetailInline: sidePanel.CodingDefectDetailInline,
                DefectDetailColumn: sidePanel.ColDefectDetail),
            actions);
    }
}
