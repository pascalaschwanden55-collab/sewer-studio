using System.Windows.Controls;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerPositionInputControllerTests
{
    [Fact]
    public void SeekToSlider_liest_Bedienelement_und_bewegt_Player()
    {
        StaTestRunner.Run(() =>
        {
            var fixture = new Fixture();

            var moved = fixture.Controller.SeekToSlider();

            Assert.True(moved);
            Assert.Equal(["time:30000", "update"], fixture.Calls);
        });
    }

    [Fact]
    public void UpdateSeekPreview_zeigt_Vorschau_und_startet_ScrubTimer_beim_Ziehen()
    {
        StaTestRunner.Run(() =>
        {
            var fixture = new Fixture();

            var updated = fixture.Controller.UpdateSeekPreview(
                isDragging: true,
                isScrubTimerEnabled: false,
                startScrubTimer: () => fixture.Calls.Add("start"));

            Assert.True(updated);
            Assert.Equal("00:30", fixture.CurrentTimeText.Text);
            Assert.Equal("02:00", fixture.DurationText.Text);
            Assert.Equal(["start"], fixture.Calls);
        });
    }

    [Fact]
    public void ScrubSeekToSlider_bewegt_Player_und_aktualisiert_Vorschau()
    {
        StaTestRunner.Run(() =>
        {
            var fixture = new Fixture();

            var moved = fixture.Controller.ScrubSeekToSlider();

            Assert.True(moved);
            Assert.Equal(["time:30000"], fixture.Calls);
            Assert.Equal("00:30", fixture.CurrentTimeText.Text);
        });
    }

    private sealed class Fixture
    {
        public Slider PositionSlider { get; } = new()
        {
            Minimum = 0d,
            Maximum = 100d,
            Value = 25d
        };
        public TextBlock CurrentTimeText { get; } = new();
        public TextBlock DurationText { get; } = new();
        public List<string> Calls { get; } = [];
        public PlayerPositionInputController Controller { get; }

        public Fixture()
        {
            var timeline = new PlayerTimelineHost(
                readTimeMilliseconds: () => 0L,
                readLengthMilliseconds: () => 120_000L,
                seekMilliseconds: value => Calls.Add($"time:{value}"),
                setPositionRatio: value => Calls.Add($"position:{value:0.##}"));
            var controls = new PlayerPositionControls(
                PositionSlider,
                CurrentTimeText,
                DurationText);

            Controller = new PlayerPositionInputController(
                PositionSlider,
                timeline,
                controls,
                updateUi: () => Calls.Add("update"));
        }
    }
}
