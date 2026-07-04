using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class VsaCodeExplorerClockPanelRendererTests
{
    [Fact]
    public void Apply_versteckt_panel_wenn_presentation_hidden_ist()
    {
        RunSta(() =>
        {
            var harness = new Harness();

            VsaCodeExplorerClockPanelRenderer.Apply(
                HiddenPresentation(),
                harness.Targets);

            Assert.Equal(Visibility.Collapsed, harness.ClockPanel.Visibility);
            Assert.Equal("", harness.SingleValue);
            Assert.Equal("", harness.RangeFrom);
            Assert.Equal("", harness.RangeTo);
        });
    }

    [Fact]
    public void Apply_baut_single_panel_mit_hint_presets_und_werten()
    {
        RunSta(() =>
        {
            var harness = new Harness();

            VsaCodeExplorerClockPanelRenderer.Apply(
                new VsaCodeExplorerClockPanelPresentation(
                    ShowPanel: true,
                    Title: "LAGE AM UMFANG (PUNKT)",
                    Hint: "Nur Punkt",
                    ShowHint: true,
                    ShowSinglePanel: true,
                    ShowRangePanel: false,
                    UsageHint: "Klick = Punkt",
                    ShowRightPreset: false,
                    ShowGesamtPreset: false,
                    ClockBisText: "00",
                    ClockSingleValue: "6",
                    ClockRangeFrom: null,
                    ClockRangeTo: null,
                    TransferText: "Transfer: 06 00"),
                harness.Targets);

            Assert.Equal(Visibility.Visible, harness.ClockPanel.Visibility);
            Assert.Equal("LAGE AM UMFANG (PUNKT)", harness.Title.Text);
            Assert.Equal("Nur Punkt", harness.Hint.Text);
            Assert.Equal(Visibility.Visible, harness.Hint.Visibility);
            Assert.Equal(Visibility.Visible, harness.SinglePanel.Visibility);
            Assert.Equal(Visibility.Collapsed, harness.RangePanel.Visibility);
            Assert.Equal("Klick = Punkt", harness.UsageHint.Text);
            Assert.Equal(Visibility.Collapsed, harness.RightPreset.Visibility);
            Assert.Equal(Visibility.Collapsed, harness.GesamtPreset.Visibility);
            Assert.Equal("00", harness.ClockBis.Text);
            Assert.Equal("6", harness.SingleValue);
            Assert.Equal("Transfer: 06 00", harness.Transfer.Text);
        });
    }

    [Fact]
    public void Apply_baut_range_panel_und_setzt_nur_vorhandene_pickerwerte()
    {
        RunSta(() =>
        {
            var harness = new Harness
            {
                SingleValue = "alt",
                RangeFrom = "alt-von",
                RangeTo = "alt-bis"
            };

            VsaCodeExplorerClockPanelRenderer.Apply(
                new VsaCodeExplorerClockPanelPresentation(
                    ShowPanel: true,
                    Title: "LAGE AM UMFANG (VON-BIS)",
                    Hint: null,
                    ShowHint: false,
                    ShowSinglePanel: false,
                    ShowRangePanel: true,
                    UsageHint: "1. Klick = Von",
                    ShowRightPreset: true,
                    ShowGesamtPreset: true,
                    ClockBisText: null,
                    ClockSingleValue: null,
                    ClockRangeFrom: "",
                    ClockRangeTo: "9",
                    TransferText: null),
                harness.Targets);

            Assert.Equal(Visibility.Visible, harness.ClockPanel.Visibility);
            Assert.Equal("LAGE AM UMFANG (VON-BIS)", harness.Title.Text);
            Assert.Equal(Visibility.Collapsed, harness.Hint.Visibility);
            Assert.Equal(Visibility.Collapsed, harness.SinglePanel.Visibility);
            Assert.Equal(Visibility.Visible, harness.RangePanel.Visibility);
            Assert.Equal(Visibility.Visible, harness.RightPreset.Visibility);
            Assert.Equal(Visibility.Visible, harness.GesamtPreset.Visibility);
            Assert.Equal("alt", harness.SingleValue);
            Assert.Equal("", harness.RangeFrom);
            Assert.Equal("9", harness.RangeTo);
            Assert.Equal("", harness.Transfer.Text);
        });
    }

    private static VsaCodeExplorerClockPanelPresentation HiddenPresentation()
        => new(
            ShowPanel: false,
            Title: "",
            Hint: null,
            ShowHint: false,
            ShowSinglePanel: false,
            ShowRangePanel: false,
            UsageHint: "",
            ShowRightPreset: false,
            ShowGesamtPreset: false,
            ClockBisText: null,
            ClockSingleValue: null,
            ClockRangeFrom: null,
            ClockRangeTo: null,
            TransferText: null);

    private static void RunSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null)
            throw failure;
    }

    private sealed class Harness
    {
        public Border ClockPanel { get; } = new() { Visibility = Visibility.Visible };
        public TextBlock Title { get; } = new();
        public TextBlock Hint { get; } = new();
        public StackPanel SinglePanel { get; } = new();
        public StackPanel RangePanel { get; } = new();
        public TextBlock UsageHint { get; } = new();
        public Button RightPreset { get; } = new();
        public Button GesamtPreset { get; } = new();
        public TextBox ClockBis { get; } = new();
        public TextBlock Transfer { get; } = new();
        public string SingleValue { get; set; } = "";
        public string RangeFrom { get; set; } = "";
        public string RangeTo { get; set; } = "";

        public VsaCodeExplorerClockPanelRenderTargets Targets => new(
            ClockPanel,
            Title,
            Hint,
            SinglePanel,
            RangePanel,
            UsageHint,
            RightPreset,
            GesamtPreset,
            ClockBis,
            value => SingleValue = value,
            value => RangeFrom = value,
            value => RangeTo = value,
            Transfer);
    }
}
