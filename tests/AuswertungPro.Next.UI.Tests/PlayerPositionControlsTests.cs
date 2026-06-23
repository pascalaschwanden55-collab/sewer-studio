using System;
using System.Threading;
using System.Windows.Controls;
using AuswertungPro.Next.UI.Player;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerPositionControlsTests
{
    [Fact]
    public void ApplyPlaybackState_updates_slider_and_time_labels()
    {
        RunOnStaThread(() =>
        {
            var slider = new Slider { Maximum = 100 };
            var currentTime = new TextBlock();
            var duration = new TextBlock();
            var controls = new PlayerPositionControls(slider, currentTime, duration);

            controls.ApplyPlaybackState(currentTimeMs: 30_000, durationMs: 120_000);

            Assert.Equal(25, slider.Value);
            Assert.Equal("00:30", currentTime.Text);
            Assert.Equal("02:00", duration.Text);
        });
    }

    [Fact]
    public void ApplySeekPreview_updates_time_labels_without_moving_slider()
    {
        RunOnStaThread(() =>
        {
            var slider = new Slider { Maximum = 100, Value = 10 };
            var currentTime = new TextBlock();
            var duration = new TextBlock();
            var controls = new PlayerPositionControls(slider, currentTime, duration);

            controls.ApplySeekPreview(ratio: 0.25, durationMs: 120_000);

            Assert.Equal(10, slider.Value);
            Assert.Equal("00:30", currentTime.Text);
            Assert.Equal("02:00", duration.Text);
        });
    }

    [Fact]
    public void ApplyScrubPreview_updates_only_current_time_label()
    {
        RunOnStaThread(() =>
        {
            var slider = new Slider { Maximum = 100 };
            var currentTime = new TextBlock();
            var duration = new TextBlock { Text = "old" };
            var controls = new PlayerPositionControls(slider, currentTime, duration);

            controls.ApplyScrubPreview(ratio: 0.5, durationMs: 120_000);

            Assert.Equal("01:00", currentTime.Text);
            Assert.Equal("old", duration.Text);
        });
    }

    private static void RunOnStaThread(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception is not null)
            throw exception;
    }
}
