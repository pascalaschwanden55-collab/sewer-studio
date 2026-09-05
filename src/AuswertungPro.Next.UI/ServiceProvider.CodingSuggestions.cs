using AuswertungPro.Next.Application.UseCases.CodingSuggestions;

namespace AuswertungPro.Next.UI;

public sealed partial class ServiceProvider
{
    private ICodingSuggestionScanService? _codingSuggestionScan;

    /// <summary>
    /// Vorabdurchlauf des Codiermodus. Baut auf den zwei Training-Studio-Diensten
    /// auf; der Bogen-Kandidat ist in Application fest gepinnt.
    /// </summary>
    public ICodingSuggestionScanService CodingSuggestionScan
        => _codingSuggestionScan ??= new CodingSuggestionScanService(
            BendSuggestionScan,
            PipeEndSuggestionScan,
            CodingSuggestionExposure);
}
