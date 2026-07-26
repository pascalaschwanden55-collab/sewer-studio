namespace AuswertungPro.Next.UI.Ai.Coding;

public static class CodingHaltungslaengeEnsureServiceFactory
{
    public static CodingHaltungslaengeEnsureService Create()
        => new(
            CodingHaltungslaengeResolver.TryEnsureFromKnownSources,
            () => Microsoft.VisualBasic.Interaction.InputBox(
                "Haltungslaenge konnte nicht ermittelt werden.\n" +
                "Bitte Haltungslaenge in Meter eingeben (z.B. 45.3):",
                "Haltungslaenge eingeben",
                ""));
}
