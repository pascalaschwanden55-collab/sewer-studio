namespace AuswertungPro.Next.Infrastructure.Import;

/// <summary>
/// Reicht Fortschritt synchron weiter. Anders als <see cref="Progress{T}"/> wechselt er nicht
/// den Thread; nur der aeussere UI-Kanal des Imports tut das.
/// </summary>
internal sealed class SynchronerFortschritt<T>(Action<T> melden) : IProgress<T>
{
    public void Report(T value) => melden(value);
}
