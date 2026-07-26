using System;
using System.Windows;
using System.Windows.Controls;

namespace AuswertungPro.Next.UI.Ai.Vsa;

public sealed record VsaCodeExplorerClockPanelRenderTargets(
    UIElement ClockPanel,
    TextBlock Title,
    TextBlock Hint,
    UIElement SinglePanel,
    UIElement RangePanel,
    TextBlock UsageHint,
    UIElement RightPreset,
    UIElement GesamtPreset,
    TextBox ClockBis,
    Action<string> SetSingleValue,
    Action<string> SetRangeFrom,
    Action<string> SetRangeTo,
    TextBlock Transfer);

public static class VsaCodeExplorerClockPanelRenderer
{
    public static void Apply(
        VsaCodeExplorerClockPanelPresentation presentation,
        VsaCodeExplorerClockPanelRenderTargets targets)
    {
        if (!presentation.ShowPanel)
        {
            targets.ClockPanel.Visibility = Visibility.Collapsed;
            return;
        }

        targets.ClockPanel.Visibility = Visibility.Visible;
        targets.Title.Text = presentation.Title;

        if (presentation.ShowHint)
        {
            targets.Hint.Text = presentation.Hint;
            targets.Hint.Visibility = Visibility.Visible;
        }
        else
        {
            targets.Hint.Visibility = Visibility.Collapsed;
        }

        targets.SinglePanel.Visibility = presentation.ShowSinglePanel ? Visibility.Visible : Visibility.Collapsed;
        targets.RangePanel.Visibility = presentation.ShowRangePanel ? Visibility.Visible : Visibility.Collapsed;
        targets.UsageHint.Text = presentation.UsageHint;
        targets.RightPreset.Visibility = presentation.ShowRightPreset ? Visibility.Visible : Visibility.Collapsed;
        targets.GesamtPreset.Visibility = presentation.ShowGesamtPreset ? Visibility.Visible : Visibility.Collapsed;

        if (presentation.ClockBisText is not null)
            targets.ClockBis.Text = presentation.ClockBisText;

        if (presentation.ClockSingleValue is not null)
            targets.SetSingleValue(presentation.ClockSingleValue);

        if (presentation.ClockRangeFrom is not null)
            targets.SetRangeFrom(presentation.ClockRangeFrom);

        if (presentation.ClockRangeTo is not null)
            targets.SetRangeTo(presentation.ClockRangeTo);

        if (presentation.TransferText is not null)
            targets.Transfer.Text = presentation.TransferText;
    }
}
