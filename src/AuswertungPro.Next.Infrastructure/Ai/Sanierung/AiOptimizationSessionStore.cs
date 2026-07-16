using AuswertungPro.Next.Application.Ai.Sanierung;

namespace AuswertungPro.Next.Infrastructure.Ai.Sanierung;

/// <summary>
/// Kompatibilitaetsfassade fuer bestehende Aufrufer. Die Dateiarbeit liegt im
/// <see cref="IAiOptimizationSessionStore"/>.
/// </summary>
public static class AiOptimizationSessionStore
{
    private static readonly IAiOptimizationSessionStore Default =
        new AiOptimizationSessionFileStore();

    public static string DefaultPath => Current.StoragePath;

    public static IAiOptimizationSessionStore Current => Default;

    [Obsolete("Globaler Austausch wurde entfernt. Den Dienst per Konstruktor uebergeben.")]
    public static void Use(IAiOptimizationSessionStore store) =>
        throw new NotSupportedException(
            "Der globale Speicher fuer KI-Sanierungssitzungen kann nicht mehr ausgetauscht werden. " +
            "IAiOptimizationSessionStore bitte per Konstruktor uebergeben.");

    public static Task SaveAsync(AiOptimizationSession session) =>
        Current.SaveAsync(session);

    public static Task<IReadOnlyList<AiOptimizationSession>> LoadAllAsync() =>
        Current.LoadAllAsync();

    public static Task<IReadOnlyList<AiOptimizationSession>> LoadForHaltungAsync(string haltungId) =>
        Current.LoadForHaltungAsync(haltungId);
}
