using AuswertungPro.Next.Application.Ai.Training;

namespace AuswertungPro.Next.Infrastructure.Ai.Training;

/// <summary>
/// Kompatibilitaetsfassade fuer bestehende Aufrufer. Die Dateiarbeit liegt im
/// <see cref="ISelfTrainingHistoryStore"/>.
/// </summary>
public static class SelfTrainingHistoryStore
{
    private static ISelfTrainingHistoryStore _current = new SelfTrainingHistoryFileStore();

    public static string DefaultPath => Current.StoragePath;

    public static ISelfTrainingHistoryStore Current => Volatile.Read(ref _current);

    /// <summary>Verbindet die Fassade mit der zentral aufgebauten Dienstinstanz.</summary>
    public static void Use(ISelfTrainingHistoryStore store) =>
        Volatile.Write(ref _current, store ?? throw new ArgumentNullException(nameof(store)));

    public static Task<List<SelfTrainingRunSnapshot>> LoadAsync() =>
        Current.LoadAsync();

    public static Task AppendRunAsync(SelfTrainingRunSnapshot run) =>
        Current.AppendRunAsync(run);
}
