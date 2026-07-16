using AuswertungPro.Next.Application.Ai.Training;

namespace AuswertungPro.Next.Infrastructure.Ai.Training;

/// <summary>
/// Kompatibilitaetsfassade fuer bestehende Aufrufer. Die Dateiarbeit liegt im
/// <see cref="ITrainingCenterSettingsStore"/>.
/// </summary>
public static class TrainingCenterSettingsStore
{
    private static readonly ITrainingCenterSettingsStore Default = new TrainingCenterSettingsFileStore();

    public static string DefaultPath => Current.StoragePath;

    public static ITrainingCenterSettingsStore Current => Default;

    [Obsolete("Globaler Austausch wurde entfernt. Den Dienst per Konstruktor uebergeben.")]
    public static void Use(ITrainingCenterSettingsStore store) =>
        throw new NotSupportedException(
            "Der globale Speicher fuer Trainings-Einstellungen kann nicht mehr ausgetauscht werden. " +
            "ITrainingCenterSettingsStore bitte per Konstruktor uebergeben.");

    public static Task<TrainingCenterSettings> LoadAsync() =>
        Current.LoadAsync();

    public static Task SaveAsync(TrainingCenterSettings settings) =>
        Current.SaveAsync(settings);
}
