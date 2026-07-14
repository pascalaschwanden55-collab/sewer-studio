using AuswertungPro.Next.Application.Ai.Training;

namespace AuswertungPro.Next.Infrastructure.Ai.Training;

/// <summary>Kompatibilitaetsfassade; die Dateiarbeit liegt im Instanzdienst.</summary>
public static class TrainingSamplesStore
{
    private static TrainingSampleFileStore _current = new();

    public static ITrainingSampleStore Current => Volatile.Read(ref _current);
    public static string DefaultPath => Volatile.Read(ref _current).StoragePath;
    public static string EffectiveEvalSetRoot => Volatile.Read(ref _current).EffectiveEvalSetRoot;

    public static void Use(TrainingSampleFileStore store) =>
        Volatile.Write(ref _current, store ?? throw new ArgumentNullException(nameof(store)));

    public static void ConfigureEvalProtection(string? evalSetRoot) =>
        Volatile.Read(ref _current).ConfigureEvalProtection(evalSetRoot);

    public static Task<List<TrainingSample>> LoadAsync() => Current.LoadAsync();
    public static Task SaveAsync(List<TrainingSample> samples) => Current.SaveAsync(samples);
    public static Task MergeAndSaveAsync(List<TrainingSample> samples) => Current.MergeAndSaveAsync(samples);
    public static Task MergeOrUpdateAsync(IEnumerable<TrainingSample> samples) => Current.MergeOrUpdateAsync(samples);
}
