using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerControlSettingsControllerTests
{
    [Fact]
    public void LoadInitial_normalisiert_gespeicherte_Werte_ohne_zu_speichern()
    {
        var store = new Store { PlayerVolume = 130, PlayerOverlayOpacity = 0.1d };
        var controller = new PlayerControlSettingsController(store);

        var state = controller.LoadInitial();

        Assert.Equal(100, state.Volume);
        Assert.False(state.Muted);
        Assert.Equal(0.35d, state.OverlayOpacity);
        Assert.Equal(0, store.SaveCalls);
    }

    [Fact]
    public void LoadInitial_schaltet_bei_Lautstaerke_null_stumm()
    {
        var controller = new PlayerControlSettingsController(new Store
        {
            PlayerVolume = 0,
            PlayerMuted = false
        });

        Assert.True(controller.LoadInitial().Muted);
    }

    [Theory]
    [InlineData(44.6d, 45, false)]
    [InlineData(-5d, 0, true)]
    public void SetVolume_normalisiert_synchronisiert_Mute_und_speichert_einmal(
        double requested,
        int expectedVolume,
        bool expectedMuted)
    {
        var store = new Store { PlayerMuted = true };
        var controller = new PlayerControlSettingsController(store);

        var state = controller.SetVolume(requested);

        Assert.Equal(expectedVolume, state.Volume);
        Assert.Equal(expectedMuted, state.Muted);
        Assert.Equal(expectedVolume, store.PlayerVolume);
        Assert.Equal(expectedMuted, store.PlayerMuted);
        Assert.Equal(1, store.SaveCalls);
    }

    [Fact]
    public void SetMuted_uebernimmt_Wert_und_speichert_einmal()
    {
        var store = new Store();
        var controller = new PlayerControlSettingsController(store);

        var muted = controller.SetMuted(true);

        Assert.True(muted);
        Assert.True(store.PlayerMuted);
        Assert.Equal(1, store.SaveCalls);
    }

    [Theory]
    [InlineData(0.1d, 0.35d)]
    [InlineData(0.72d, 0.72d)]
    [InlineData(2d, 1d)]
    public void SetOverlayOpacity_begrenzt_Wert_und_speichert_einmal(
        double requested,
        double expected)
    {
        var store = new Store();
        var controller = new PlayerControlSettingsController(store);

        var opacity = controller.SetOverlayOpacity(requested);

        Assert.Equal(expected, opacity);
        Assert.Equal(expected, store.PlayerOverlayOpacity);
        Assert.Equal(1, store.SaveCalls);
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
