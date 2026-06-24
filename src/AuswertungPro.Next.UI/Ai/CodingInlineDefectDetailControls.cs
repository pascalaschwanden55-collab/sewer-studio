using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AuswertungPro.Next.UI.Player;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Ai;

public sealed class CodingInlineDefectDetailControls
{
    private readonly TextBlock TxtInlineDetailCode;
    private readonly TextBlock TxtInlineDetailDesc;
    private readonly TextBlock TxtInlineDetailDistance;
    private readonly TextBlock TxtInlineDetailConfidence;
    private readonly TextBlock TxtInlineDetailStatus;
    private readonly Image ImgInlineEvidencePreview;
    private readonly TextBlock TxtInlineEvidencePreviewStatus;
    private readonly Button BtnInlineAccept;
    private readonly Button BtnInlineReject;
    private readonly Border CodingDefectDetailInline;
    private readonly ColumnDefinition ColDefectDetail;

    public CodingInlineDefectDetailControls(
        TextBlock txtInlineDetailCode,
        TextBlock txtInlineDetailDesc,
        TextBlock txtInlineDetailDistance,
        TextBlock txtInlineDetailConfidence,
        TextBlock txtInlineDetailStatus,
        Image imgInlineEvidencePreview,
        TextBlock txtInlineEvidencePreviewStatus,
        Button btnInlineAccept,
        Button btnInlineReject,
        Border codingDefectDetailInline,
        ColumnDefinition colDefectDetail)
    {
        TxtInlineDetailCode = txtInlineDetailCode;
        TxtInlineDetailDesc = txtInlineDetailDesc;
        TxtInlineDetailDistance = txtInlineDetailDistance;
        TxtInlineDetailConfidence = txtInlineDetailConfidence;
        TxtInlineDetailStatus = txtInlineDetailStatus;
        ImgInlineEvidencePreview = imgInlineEvidencePreview;
        TxtInlineEvidencePreviewStatus = txtInlineEvidencePreviewStatus;
        BtnInlineAccept = btnInlineAccept;
        BtnInlineReject = btnInlineReject;
        CodingDefectDetailInline = codingDefectDetailInline;
        ColDefectDetail = colDefectDetail;
    }

    public void Apply(CodingInlineDefectDetailState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        TxtInlineDetailCode.Text = state.CodeText;
        TxtInlineDetailDesc.Text = state.DescriptionText;
        TxtInlineDetailDistance.Text = state.DistanceText;
        TxtInlineDetailConfidence.Text = state.ConfidenceText;
        TxtInlineDetailConfidence.Foreground = state.Confidence.HasValue
            ? CodingSessionViewModel.GetConfidenceBrush(state.Confidence.Value)
            : new SolidColorBrush(PlayerStatusColors.Muted);
        BtnInlineAccept.Visibility = state.CanAct ? Visibility.Visible : Visibility.Collapsed;
        BtnInlineReject.Visibility = state.CanAct ? Visibility.Visible : Visibility.Collapsed;
        TxtInlineDetailStatus.Text = state.StatusText;
        CodingDefectDetailInline.Visibility = Visibility.Visible;
        ColDefectDetail.Width = new GridLength(300);
    }

    public void Hide()
    {
        ImgInlineEvidencePreview.Source = null;
        ImgInlineEvidencePreview.Visibility = Visibility.Collapsed;
        TxtInlineEvidencePreviewStatus.Visibility = Visibility.Visible;
        CodingDefectDetailInline.Visibility = Visibility.Collapsed;
        ColDefectDetail.Width = new GridLength(0);
    }
}
