using AuswertungPro.Next.Application.Ai.Training;

namespace AuswertungPro.Next.Infrastructure.Ai.Training;

/// <summary>Kompatibilitaetsfassade; die Dateiarbeit liegt im Instanzdienst.</summary>
public static class TrainingSamplesStore
{
    private static readonly TrainingSampleFileStore Default = new();

    public static ITrainingSampleStore Current => Default;
    public static string DefaultPath => Default.StoragePath;
    public static string EffectiveEvalSetRoot => Default.EffectiveEvalSetRoot;

    [Obsolete("Die Trainingssample-Fassade ist unveraenderbar. Abhaengigkeit direkt uebergeben.")]
    public static void Use(TrainingSampleFileStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        throw new NotSupportedException(
            "Die Trainingssample-Fassade kann nicht mehr global ersetzt werden.");
    }

    public static void ConfigureEvalProtection(string? evalSetRoot) =>
        Default.ConfigureEvalProtection(evalSetRoot);

    public static Task<List<TrainingSample>> LoadAsync() => Current.LoadAsync();
    public static Task SaveAsync(List<TrainingSample> samples) => Current.SaveAsync(samples);
    public static Task MergeAndSaveAsync(List<TrainingSample> samples) => Current.MergeAndSaveAsync(samples);
    public static Task MergeOrUpdateAsync(IEnumerable<TrainingSample> samples) => Current.MergeOrUpdateAsync(samples);
}
