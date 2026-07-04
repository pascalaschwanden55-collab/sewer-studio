using System;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using AuswertungPro.Next.UI.Player;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerSpeedControlsTests
{
    [Fact]
    public void Update_sets_rate_label_and_checked_speed_button()
    {
        RunOnStaThread(() =>
        {
            var rateText = new TextBlock();
            var speed05 = new ToggleButton();
            var speed1 = new ToggleButton();
            var speed15 = new ToggleButton();
            var speed2 = new ToggleButton();
            var speed4 = new ToggleButton();
            var speed8 = new ToggleButton();
            var speedSlider = CreateSpeedSlider();

            var controls = new PlayerSpeedControls(
                rateText,
                speedSlider,
                speed05,
                speed1,
                speed15,
                speed2,
                speed4,
                speed8);

            controls.Update(1.5f);

            Assert.Equal("1.5x", rateText.Text);
            Assert.Equal(1.5, speedSlider.Value);
            Assert.False(speed05.IsChecked);
            Assert.False(speed1.IsChecked);
            Assert.True(speed15.IsChecked);
            Assert.False(speed2.IsChecked);
            Assert.False(speed4.IsChecked);
            Assert.False(speed8.IsChecked);
        });
    }

    [Fact]
    public void Update_treats_invalid_rate_as_normal_speed()
    {
        RunOnStaThread(() =>
        {
            var rateText = new TextBlock();
            var speed05 = new ToggleButton();
            var speed1 = new ToggleButton();
            var speed15 = new ToggleButton();
            var speed2 = new ToggleButton();
            var speed4 = new ToggleButton();
            var speed8 = new ToggleButton();
            var speedSlider = CreateSpeedSlider();

            var controls = new PlayerSpeedControls(
                rateText,
                speedSlider,
                speed05,
                speed1,
                speed15,
                speed2,
                speed4,
                speed8);

            controls.Update(0f);

            Assert.Equal("1x", rateText.Text);
            Assert.Equal(1.0, speedSlider.Value);
            Assert.False(speed05.IsChecked);
            Assert.True(speed1.IsChecked);
            Assert.False(speed15.IsChecked);
            Assert.False(speed2.IsChecked);
            Assert.False(speed4.IsChecked);
            Assert.False(speed8.IsChecked);
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

    private static Slider CreateSpeedSlider()
        => new()
        {
            Minimum = 0.25,
            Maximum = 8,
            Value = 1
        };
}
