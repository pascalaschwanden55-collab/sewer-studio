using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Theme;

/// <summary>
/// Statische Fassade auf den zentralen Statusfarben-Dienst (Muster wie AnimationTokens).
/// Fuer Konsumenten ohne DI-Zugriff (Feature-Builder, Anzeige-Modelle, Renderer).
/// Die App verdrahtet beim Start die registrierte Instanz; Default reicht fuer Tests.
/// </summary>
public static class StatusColors
{
    public static IStatusColorService Current { get; set; } = new StatusColorService();
}
