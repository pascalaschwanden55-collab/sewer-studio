using AuswertungPro.Next.Application.Import;

namespace AuswertungPro.Next.Infrastructure.HoldingDistribution;

/// <summary>
/// Kompatibilitaetsfassade; die Ordnersuche liegt im injizierbaren Instanzdienst.
/// </summary>
public static class ShaftPdfSelectionExpander
{
    private static readonly IShaftPdfSelectionExpander Default = new ShaftPdfSelectionExpansionService();

    public static IShaftPdfSelectionExpander Current => Default;

    [Obsolete("Die Schacht-PDF-Auswahl-Fassade ist unveraenderbar. Abhaengigkeit direkt uebergeben.")]
    public static void Use(IShaftPdfSelectionExpander expander)
    {
        ArgumentNullException.ThrowIfNull(expander);
        throw new NotSupportedException(
            "Die Schacht-PDF-Auswahl-Fassade kann nicht mehr global ersetzt werden.");
    }

    public static List<string> Expand(IReadOnlyList<string> selectedPdfFiles)
        => Current.Expand(selectedPdfFiles).ToList();
}
