namespace AuswertungPro.Next.UI.Player;

public interface IPlayerControlSettingsStore
{
    int PlayerVolume { get; set; }
    bool PlayerMuted { get; set; }
    double PlayerOverlayOpacity { get; set; }
    void Save();
}

public sealed record PlayerControlSettingsState(
    int Volume,
    bool Muted,
    double OverlayOpacity);

public sealed record PlayerVolumeState(int Volume, bool Muted);

public sealed class PlayerControlSettingsController
{
    private readonly IPlayerControlSettingsStore _store;

    public PlayerControlSettingsController(IPlayerControlSettingsStore store)
        => _store = store ?? throw new ArgumentNullException(nameof(store));

    public PlayerControlSettingsState LoadInitial()
    {
        var volume = NormalizeVolume(_store.PlayerVolume);
        return new PlayerControlSettingsState(
            volume,
            _store.PlayerMuted || volume == 0,
            NormalizeOverlayOpacity(_store.PlayerOverlayOpacity));
    }

    public PlayerVolumeState SetVolume(double requestedVolume)
    {
        var volume = NormalizeVolume(requestedVolume);
        var muted = volume == 0;
        _store.PlayerVolume = volume;
        _store.PlayerMuted = muted;
        _store.Save();
        return new PlayerVolumeState(volume, muted);
    }

    public bool SetMuted(bool muted)
    {
        _store.PlayerMuted = muted;
        _store.Save();
        return muted;
    }

    public double SetOverlayOpacity(double requestedOpacity)
    {
        var opacity = NormalizeOverlayOpacity(requestedOpacity);
        _store.PlayerOverlayOpacity = opacity;
        _store.Save();
        return opacity;
    }

    private static int NormalizeVolume(double volume)
        => Math.Clamp((int)Math.Round(volume), 0, 100);

    private static double NormalizeOverlayOpacity(double opacity)
        => Math.Clamp(opacity, 0.35d, 1d);
}
