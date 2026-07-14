using AuswertungPro.Next.Application.Ai.Training;

namespace AuswertungPro.Next.Infrastructure.Ai.Training;

/// <summary>
/// Kompatibilitaetsfassade fuer bestehende Aufrufer. Die Dateiarbeit liegt im
/// <see cref="ITrainingCenterSettingsStore"/>.
/// </summary>
public static class TrainingCenterSettingsStore
{
    private static ITrainingCenterSettingsStore _current = new TrainingCenterSettingsFileStore();

    public static string DefaultPath => Current.StoragePath;

    public static ITrainingCenterSettingsStore Current => Volatile.Read(ref _current);

    /// <summary>Verbindet die Fassade mit der zentral aufgebauten Dienstinstanz.</summary>
    public static void Use(ITrainingCenterSettingsStore store) =>
        Volatile.Write(ref _current, store ?? throw new ArgumentNullException(nameof(store)));

    public static Task<TrainingCenterSettings> LoadAsync() =>
        Current.LoadAsync();

    public static Task SaveAsync(TrainingCenterSettings settings) =>
        Current.SaveAsync(settings);
}
