using AuswertungPro.Next.Application.Ai.Training;

namespace AuswertungPro.Next.Infrastructure.Ai.Training;

/// <summary>
/// Kompatibilitaetsfassade fuer bestehende Aufrufer. Die Dateiarbeit liegt im
/// <see cref="ISelfTrainingHistoryStore"/>.
/// </summary>
public static class SelfTrainingHistoryStore
{
    private static readonly ISelfTrainingHistoryStore Default = new SelfTrainingHistoryFileStore();

    public static string DefaultPath => Current.StoragePath;

    public static ISelfTrainingHistoryStore Current => Default;

    [Obsolete("Globaler Austausch wurde entfernt. Den Dienst per Konstruktor uebergeben.")]
    public static void Use(ISelfTrainingHistoryStore store) =>
        throw new NotSupportedException(
            "Der globale Speicher fuer den Selbsttraining-Verlauf kann nicht mehr ausgetauscht werden. " +
            "ISelfTrainingHistoryStore bitte per Konstruktor uebergeben.");

    public static Task<List<SelfTrainingRunSnapshot>> LoadAsync() =>
        Current.LoadAsync();

    public static Task AppendRunAsync(SelfTrainingRunSnapshot run) =>
        Current.AppendRunAsync(run);
}
