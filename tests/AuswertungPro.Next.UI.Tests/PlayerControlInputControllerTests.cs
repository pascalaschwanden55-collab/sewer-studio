using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerControlInputControllerTests
{
    [Fact]
    public void Initialize_uebernimmt_Einstellungen_und_aktuelle_Geschwindigkeit()
    {
        StaTestRunner.Run(() =>
        {
            var fixture = new Fixture
            {
                PlayerRate = 1.5f
            };
            fixture.Store.PlayerVolume = 64;
            fixture.Store.PlayerMuted = true;
            fixture.Store.PlayerOverlayOpacity = 0.72d;

            fixture.Controller.Initialize();

            Assert.True(fixture.Controller.IsEnabled);
            Assert.Equal(64d, fixture.VolumeSlider.Value);
            Assert.True(fixture.MuteButton.IsChecked);
            Assert.Equal(0.72d, fixture.OverlaySlider.Value);
            Assert.Equal("1.5x", fixture.RateText.Text);
            Assert.Equal(1.5d, fixture.SpeedSlider.Value);
            Assert.Equal(0, fixture.Store.SaveCalls);
        });
    }

    [Fact]
    public void Eingaben_vor_Initialize_werden_ignoriert()
    {
        StaTestRunner.Run(() =>
        {
            var fixture = new Fixture();

            fixture.Controller.SetSpeed(2f);
            fixture.Controller.SetVolume(25d);
            fixture.Controller.SetMuted(true);
            fixture.Controller.SetOverlayOpacity(0.5d);

            Assert.False(fixture.Controller.IsEnabled);
            Assert.Empty(fixture.RequestedRates);
            Assert.Equal(0, fixture.Store.SaveCalls);
        });
    }

    [Fact]
    public void Eingaben_nach_Initialize_werden_angewendet_und_gespeichert()
    {
        StaTestRunner.Run(() =>
        {
            var fixture = new Fixture();
            fixture.Controller.Initialize();

            fixture.Controller.SetSpeed(2f);
            fixture.Controller.SetVolume(25d);
            fixture.Controller.SetMuted(true);
            fixture.Controller.SetOverlayOpacity(0.5d);

            Assert.Equal([2f], fixture.RequestedRates);
            Assert.Equal(25, fixture.Store.PlayerVolume);
            Assert.True(fixture.Store.PlayerMuted);
            Assert.Equal(0.5d, fixture.Store.PlayerOverlayOpacity);
            Assert.Equal(3, fixture.Store.SaveCalls);
        });
    }

    [Fact]
    public void ChangeSpeed_berechnet_neue_Geschwindigkeit_aus_aktuellem_Wert()
    {
        StaTestRunner.Run(() =>
        {
            var fixture = new Fixture
            {
                PlayerRate = 1.5f
            };
            fixture.Controller.Initialize();

            fixture.Controller.ChangeSpeed(0.5f);

            Assert.Equal([2f], fixture.RequestedRates);
        });
    }

    private sealed class Fixture
    {
        public Store Store { get; } = new();
        public Slider VolumeSlider { get; } = new() { Minimum = 0, Maximum = 100 };
        public TextBlock VolumeText { get; } = new();
        public ToggleButton MuteButton { get; } = new();
        public TextBlock MuteIcon { get; } = new();
        public Slider OverlaySlider { get; } = new() { Minimum = 0.35d, Maximum = 1d };
        public TextBlock OverlayText { get; } = new();
        public Canvas CodingOverlay { get; } = new();
        public Canvas DetectionOverlay { get; } = new();
        public TextBlock RateText { get; } = new();
        public Slider SpeedSlider { get; } = new() { Minimum = 0.25d, Maximum = 8d, Value = 1d };
        public List<float> RequestedRates { get; } = [];
        public float PlayerRate { get; set; } = 1f;
        public PlayerControlInputController Controller { get; }

        public Fixture()
        {
            var playbackHost = new PlayerPlaybackControlHost(
                readIsPlaying: () => false,
                setPause: _ => { },
                play: () => { },
                stop: () => { },
                readRate: () => PlayerRate,
                setRate: rate =>
                {
                    RequestedRates.Add(rate);
                    PlayerRate = rate;
                    return 0;
                },
                readVolume: () => 80,
                setVolume: _ => { },
                readMute: () => false,
                setMute: _ => { },
                shouldStartPlayback: () => false,
                playPath: _ => { });
            var settingsView = new PlayerControlSettingsView(
                VolumeSlider,
                VolumeText,
                MuteButton,
                MuteIcon,
                OverlaySlider,
                OverlayText,
                CodingOverlay,
                DetectionOverlay,
                _ => { },
                _ => { });
            var speedControls = new PlayerSpeedControls(
                RateText,
                SpeedSlider,
                new ToggleButton(),
                new ToggleButton(),
                new ToggleButton(),
                new ToggleButton(),
                new ToggleButton(),
                new ToggleButton());

            Controller = new PlayerControlInputController(
                new PlayerControlSettingsController(Store),
                settingsView,
                playbackHost,
                speedControls,
                _ => { });
        }
    }

    private sealed class Store : IPlayerControlSettingsStore
    {
        public int PlayerVolume { get; set; } = 80;
        public bool PlayerMuted { get; set; }
        public double PlayerOverlayOpacity { get; set; } = 1d;
        public int SaveCalls { get; private set; }

        public void Save() => SaveCalls++;
    }
}
