using AuswertungPro.Next.Application.Ai.Sanierung;

namespace AuswertungPro.Next.Infrastructure.Ai.Sanierung;

/// <summary>
/// Kompatibilitaetsfassade fuer bestehende Aufrufer. Die Dateiarbeit liegt im
/// <see cref="IAiOptimizationSessionStore"/>.
/// </summary>
public static class AiOptimizationSessionStore
{
    private static IAiOptimizationSessionStore _current = new AiOptimizationSessionFileStore();

    public static string DefaultPath => Current.StoragePath;

    public static IAiOptimizationSessionStore Current => Volatile.Read(ref _current);

    /// <summary>Verbindet die Fassade mit der zentral aufgebauten Dienstinstanz.</summary>
    public static void Use(IAiOptimizationSessionStore store) =>
        Volatile.Write(ref _current, store ?? throw new ArgumentNullException(nameof(store)));

    public static Task SaveAsync(AiOptimizationSession session) =>
        Current.SaveAsync(session);

    public static Task<IReadOnlyList<AiOptimizationSession>> LoadAllAsync() =>
        Current.LoadAllAsync();

    public static Task<IReadOnlyList<AiOptimizationSession>> LoadForHaltungAsync(string haltungId) =>
        Current.LoadForHaltungAsync(haltungId);
}
