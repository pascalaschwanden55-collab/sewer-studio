using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class VsaCodeExplorerProgressRendererTests
{
    [Fact]
    public void Apply_setzt_balkenfarben_labels_und_code_vorschau()
    {
        RunSta(() =>
        {
            var harness = new Harness();
            var textSecondary = Brushes.DarkSlateGray;
            var muted = Brushes.LightSlateGray;

            VsaCodeExplorerProgressRenderer.Apply(
                new VsaCodeExplorerProgressPresentation(
                    [
                        new(VsaCodeExplorerProgressBarRole.Success, LabelBold: false, VsaCodeExplorerProgressLabelRole.Secondary),
                        new(VsaCodeExplorerProgressBarRole.Group, LabelBold: false, VsaCodeExplorerProgressLabelRole.Secondary),
                        new(VsaCodeExplorerProgressBarRole.CurrentGroup, LabelBold: true, VsaCodeExplorerProgressLabelRole.Secondary),
                        new(VsaCodeExplorerProgressBarRole.BorderLight, LabelBold: false, VsaCodeExplorerProgressLabelRole.Muted)
                    ],
                    CodePreviewText: "BAB"),
                harness.Targets,
                new VsaCodeExplorerProgressRenderBrushes(
                    SuccessColor: Colors.Green,
                    GroupColor: Colors.Orange,
                    BorderLightColor: Colors.LightGray,
                    TextSecondaryBrush: textSecondary,
                    MutedBrush: muted));

            AssertBarColor(Colors.Green, harness.Bars[0]);
            AssertBarColor(Colors.Orange, harness.Bars[1]);
            AssertBarColor(Color.FromArgb(0x80, Colors.Orange.R, Colors.Orange.G, Colors.Orange.B), harness.Bars[2]);
            AssertBarColor(Colors.LightGray, harness.Bars[3]);
            Assert.Equal(FontWeights.Bold, harness.Labels[2].FontWeight);
            Assert.Equal(FontWeights.Normal, harness.Labels[3].FontWeight);
            Assert.Same(textSecondary, harness.Labels[0].Foreground);
            Assert.Same(muted, harness.Labels[3].Foreground);
            Assert.Equal("BAB", harness.CodePreview.Text);
        });
    }

    private static void AssertBarColor(Color expected, Border border)
    {
        var brush = Assert.IsType<SolidColorBrush>(border.Background);
        Assert.Equal(expected, brush.Color);
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
        public IReadOnlyList<Border> Bars { get; } =
        [
            new Border(),
            new Border(),
            new Border(),
            new Border()
        ];

        public IReadOnlyList<TextBlock> Labels { get; } =
        [
            new TextBlock(),
            new TextBlock(),
            new TextBlock(),
            new TextBlock()
        ];

        public TextBlock CodePreview { get; } = new();

        public VsaCodeExplorerProgressRenderTargets Targets => new(Bars, Labels, CodePreview);
    }
}
