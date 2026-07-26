using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Vsa;

namespace AuswertungPro.Next.UI.Tests;

public sealed class VsaCodeExplorerResultPanelRendererTests
{
    [Fact]
    public void Apply_blendet_result_aus_und_code_hint_ein_ohne_detailtexte_zu_aendern()
    {
        RunSta(() =>
        {
            var harness = new Harness
            {
                FinalCodeText = "alt-code",
                FinalLabelText = "alt-label",
                WarnText = "alt-warn",
                WarnVisibility = Visibility.Visible
            };

            VsaCodeExplorerResultPanelRenderer.Apply(
                new VsaCodeExplorerResultPanelPresentation(
                    ShowResultPanel: false,
                    ShowCodeHintPanel: true,
                    ShouldUpdateDetailPanels: false,
                    FinalCodeText: "",
                    FinalLabelText: "",
                    WarnText: "",
                    ShowWarn: false),
                harness.Targets);

            Assert.Equal(Visibility.Collapsed, harness.ResultPanel.Visibility);
            Assert.Equal(Visibility.Visible, harness.CodeHintPanel.Visibility);
            Assert.Equal("alt-code", harness.FinalCodeText);
            Assert.Equal("alt-label", harness.FinalLabelText);
            Assert.Equal("alt-warn", harness.WarnText);
            Assert.Equal(Visibility.Visible, harness.WarnVisibility);
        });
    }

    [Fact]
    public void Apply_zeigt_result_und_setzt_detailtexte()
    {
        RunSta(() =>
        {
            var harness = new Harness();

            VsaCodeExplorerResultPanelRenderer.Apply(
                new VsaCodeExplorerResultPanelPresentation(
                    ShowResultPanel: true,
                    ShowCodeHintPanel: false,
                    ShouldUpdateDetailPanels: true,
                    FinalCodeText: "BAB",
                    FinalLabelText: "Riss - laengs",
                    WarnText: "Pruefen",
                    ShowWarn: true),
                harness.Targets);

            Assert.Equal(Visibility.Visible, harness.ResultPanel.Visibility);
            Assert.Equal(Visibility.Collapsed, harness.CodeHintPanel.Visibility);
            Assert.Equal("BAB", harness.FinalCodeText);
            Assert.Equal("Riss - laengs", harness.FinalLabelText);
            Assert.Equal("Pruefen", harness.WarnText);
            Assert.Equal(Visibility.Visible, harness.WarnVisibility);
        });
    }

    [Fact]
    public void Apply_versteckt_warnung_wenn_presentation_keine_warnung_zeigt()
    {
        RunSta(() =>
        {
            var harness = new Harness { WarnVisibility = Visibility.Visible };

            VsaCodeExplorerResultPanelRenderer.Apply(
                new VsaCodeExplorerResultPanelPresentation(
                    ShowResultPanel: true,
                    ShowCodeHintPanel: false,
                    ShouldUpdateDetailPanels: true,
                    FinalCodeText: "BAA",
                    FinalLabelText: "Verformung",
                    WarnText: "",
                    ShowWarn: false),
                harness.Targets);

            Assert.Equal("", harness.WarnText);
            Assert.Equal(Visibility.Collapsed, harness.WarnVisibility);
        });
    }

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
        public StackPanel ResultPanel { get; } = new();
        public Border CodeHintPanel { get; } = new();
        public TextBlock FinalCode { get; } = new();
        public TextBlock FinalLabel { get; } = new();
        public TextBlock Warn { get; } = new();

        public string FinalCodeText
        {
            get => FinalCode.Text;
            set => FinalCode.Text = value;
        }

        public string FinalLabelText
        {
            get => FinalLabel.Text;
            set => FinalLabel.Text = value;
        }

        public string WarnText
        {
            get => Warn.Text;
            set => Warn.Text = value;
        }

        public Visibility WarnVisibility
        {
            get => Warn.Visibility;
            set => Warn.Visibility = value;
        }

        public VsaCodeExplorerResultPanelRenderTargets Targets => new(
            ResultPanel,
            CodeHintPanel,
            FinalCode,
            FinalLabel,
            Warn);
    }
}
