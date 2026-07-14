using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerControlSettingsViewTests
{
    [Fact]
    public void ApplyInitial_verteilt_den_gesamten_Zustand_auf_Player_und_Bedienelemente()
    {
        StaTestRunner.Run(() =>
        {
            var fixture = new Fixture();

            fixture.View.ApplyInitial(new PlayerControlSettingsState(64, true, 0.72d));

            Assert.Equal(64d, fixture.VolumeSlider.Value);
            Assert.Equal("64%", fixture.VolumeText.Text);
            Assert.Equal([64], fixture.AppliedVolumes);
            Assert.True(fixture.MuteButton.IsChecked);
            Assert.Equal("\uE74F", fixture.MuteIcon.Text);
            Assert.Equal("Ton einschalten", fixture.MuteButton.ToolTip);
            Assert.Equal([true], fixture.AppliedMuteStates);
            Assert.Equal(0.72d, fixture.OverlaySlider.Value);
            Assert.Equal("72%", fixture.OverlayText.Text);
            Assert.Equal(0.72d, fixture.CodingOverlay.Opacity);
            Assert.Equal(0.72d, fixture.DetectionOverlay.Opacity);
        });
    }

    [Fact]
    public void ApplyVolume_aktualisiert_Lautstaerke_und_Mute_gemeinsam()
    {
        StaTestRunner.Run(() =>
        {
            var fixture = new Fixture();

            fixture.View.ApplyVolume(new PlayerVolumeState(0, true));

            Assert.Equal("0%", fixture.VolumeText.Text);
            Assert.Equal([0], fixture.AppliedVolumes);
            Assert.True(fixture.MuteButton.IsChecked);
            Assert.Equal([true], fixture.AppliedMuteStates);
        });
    }

    private sealed class Fixture
    {
        public Slider VolumeSlider { get; } = new() { Minimum = 0, Maximum = 100 };
        public TextBlock VolumeText { get; } = new();
        public ToggleButton MuteButton { get; } = new();
        public TextBlock MuteIcon { get; } = new();
        public Slider OverlaySlider { get; } = new() { Minimum = 0.35d, Maximum = 1d };
        public TextBlock OverlayText { get; } = new();
        public Canvas CodingOverlay { get; } = new();
        public Canvas DetectionOverlay { get; } = new();
        public List<int> AppliedVolumes { get; } = [];
        public List<bool> AppliedMuteStates { get; } = [];
        public PlayerControlSettingsView View { get; }

        public Fixture()
        {
            View = new PlayerControlSettingsView(
                VolumeSlider,
                VolumeText,
                MuteButton,
                MuteIcon,
                OverlaySlider,
                OverlayText,
                CodingOverlay,
                DetectionOverlay,
                AppliedVolumes.Add,
                AppliedMuteStates.Add);
        }
    }
}
