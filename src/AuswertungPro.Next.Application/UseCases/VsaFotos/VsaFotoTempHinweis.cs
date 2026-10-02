namespace AuswertungPro.Next.Application.UseCases.VsaFotos;

/// <summary>
/// Sagt dem Nutzer, warum ein soeben aufgenommenes Befundfoto nur im Temp-Ordner liegt
/// (Deepscan 02.10.2026, R3). Der Rueckfall selbst bleibt wie 12.09.2026 festgelegt:
/// ein Foto im Temp-Ordner ist schlecht, ein geloeschtes schlimmer. Still sein darf er nicht.
/// </summary>
public static class VsaFotoTempHinweis
{
    public const string Titel = "Foto nur vorläufig gespeichert";

    /// <returns><c>null</c>, wenn das Foto nicht im Temp-Ordner liegt; sonst der Hinweistext.</returns>
    public static string? Fuer(string? fotoPfad, string? videoPfad, Func<string, bool> liegtImTemp)
    {
        ArgumentNullException.ThrowIfNull(liegtImTemp);
        if (string.IsNullOrWhiteSpace(fotoPfad) || !liegtImTemp(fotoPfad))
            return null;

        var grund = string.IsNullOrWhiteSpace(videoPfad)
            ? "Kein Video geladen: Das Foto kann nicht neben dem Video abgelegt werden."
            : liegtImTemp(videoPfad)
                ? "Das Video liegt selbst im Temp-Ordner, das Foto daneben deshalb auch."
                : "Das Foto konnte nicht in den Ordner «Fotos» neben dem Video verschoben werden "
                  + "(Ordner gesperrt oder ohne Schreibrecht).";

        return grund
            + " Es liegt nur vorläufig im Temp-Ordner und geht beim Aufräumen von Windows verloren:"
            + Environment.NewLine + Environment.NewLine + fotoPfad
            + Environment.NewLine + Environment.NewLine
            + "Bitte das Video aus dem Projekt laden und das Foto neu aufnehmen.";
    }
}
