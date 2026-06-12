using System;

namespace AuswertungPro.Next.UI.ViewModels;

/// <summary>
/// Seiten mit ungespeichertem Zustand koennen den Seitenwechsel (oder das
/// Schliessen) stoppen, statt ihre Aenderungen stillschweigend zu verlieren
/// (Audit 2026-06-12, W2: Seitenwechsel verwarf dirty Detail-Edits der
/// Sanierungs-Matrix kommentarlos).
/// </summary>
public interface IConfirmLeave
{
    /// <summary>true = verlassen erlaubt; false = auf der Seite bleiben.</summary>
    bool ConfirmLeave();
}

public static class ShellLeaveGuard
{
    public static bool CanLeave(object? currentPage)
        => currentPage is not IConfirmLeave guard || guard.ConfirmLeave();
}

public static class ShellPageLifecycle
{
    public static void DisposeIfReplaced(object? previousPage, object? nextPage)
    {
        if (ReferenceEquals(previousPage, nextPage))
            return;

        if (previousPage is IDisposable disposable)
            disposable.Dispose();
    }
}
