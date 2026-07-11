using System.Collections.Generic;

namespace AuswertungPro.Next.UI.Services;

/// <summary>
/// Statische Fassade auf den Code-Nutzungszaehler (Muster wie Theme.StatusColors):
/// im Composition Root (ServiceProvider-Konstruktor) verdrahtet, damit UI-Code
/// keinen Service-Locator braucht. Default ist ein Null-Objekt — Tests und
/// Designer schreiben nie in echte Dateien.
/// </summary>
public static class CodeUsageTrackers
{
    public static ICodeUsageTracker Current { get; set; } = new NoopCodeUsageTracker();

    private sealed class NoopCodeUsageTracker : ICodeUsageTracker
    {
        public void Erfasse(string? code) { }
        public IReadOnlyList<CodeUsageEintrag> TopCodes(int n) => [];
        public IReadOnlyList<string> Zuletzt(int n) => [];
    }
}
