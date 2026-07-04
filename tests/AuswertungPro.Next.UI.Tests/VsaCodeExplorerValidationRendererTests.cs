using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class VsaCodeExplorerValidationRendererTests
{
    [Fact]
    public void Apply_aktiviert_button_und_blendet_leere_validierung_aus()
    {
        RunSta(() =>
        {
            var harness = new Harness
            {
                ValidationText = "alt",
                ValidationVisibility = Visibility.Visible
            };

            VsaCodeExplorerValidationRenderer.Apply(
                new VsaCodeExplorerValidationPresentation(
                    CanApply: true,
                    ValidationText: "",
                    ShowValidation: false),
                harness.Targets);

            Assert.True(harness.ApplyButton.IsEnabled);
            Assert.Equal("", harness.ValidationText);
            Assert.Equal(Visibility.Collapsed, harness.ValidationVisibility);
        });
    }

    [Fact]
    public void Apply_deaktiviert_button_und_zeigt_validierung()
    {
        RunSta(() =>
        {
            var harness = new Harness();

            VsaCodeExplorerValidationRenderer.Apply(
                new VsaCodeExplorerValidationPresentation(
                    CanApply: false,
                    ValidationText: "Bitte Code auswaehlen.",
                    ShowValidation: true),
                harness.Targets);

            Assert.False(harness.ApplyButton.IsEnabled);
            Assert.Equal("Bitte Code auswaehlen.", harness.ValidationText);
            Assert.Equal(Visibility.Visible, harness.ValidationVisibility);
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
        public Button ApplyButton { get; } = new();
        public TextBlock Validation { get; } = new();

        public string ValidationText
        {
            get => Validation.Text;
            set => Validation.Text = value;
        }

        public Visibility ValidationVisibility
        {
            get => Validation.Visibility;
            set => Validation.Visibility = value;
        }

        public VsaCodeExplorerValidationRenderTargets Targets => new(ApplyButton, Validation);
    }
}
