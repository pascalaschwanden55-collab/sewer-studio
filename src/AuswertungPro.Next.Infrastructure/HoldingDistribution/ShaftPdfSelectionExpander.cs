using AuswertungPro.Next.Application.Import;

namespace AuswertungPro.Next.Infrastructure.HoldingDistribution;

/// <summary>
/// Kompatibilitaetsfassade; die Ordnersuche liegt im injizierbaren Instanzdienst.
/// </summary>
public static class ShaftPdfSelectionExpander
{
    private static IShaftPdfSelectionExpander _current = new ShaftPdfSelectionExpansionService();

    public static IShaftPdfSelectionExpander Current => Volatile.Read(ref _current);

    public static void Use(IShaftPdfSelectionExpander expander)
        => Volatile.Write(
            ref _current,
            expander ?? throw new ArgumentNullException(nameof(expander)));

    public static List<string> Expand(IReadOnlyList<string> selectedPdfFiles)
        => Current.Expand(selectedPdfFiles).ToList();
}
