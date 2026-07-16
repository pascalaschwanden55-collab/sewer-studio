using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Theme;

/// <summary>
/// Statische Fassade auf den zentralen Statusfarben-Dienst (Muster wie AnimationTokens).
/// Fuer Konsumenten ohne DI-Zugriff (Feature-Builder, Anzeige-Modelle, Renderer).
/// Die App verdrahtet beim Start die registrierte Instanz; Default reicht fuer Tests.
/// </summary>
public static class StatusColors
{
    private static readonly IStatusColorService Default = new StatusColorService();

    public static IStatusColorService Current
    {
        get => Default;
        [Obsolete("Globale Dienstwechsel sind nicht mehr erlaubt. IStatusColorService direkt uebergeben.")]
        set => throw new NotSupportedException(
            "StatusColors.Current ist unveraenderlich. IStatusColorService direkt uebergeben.");
    }
}
