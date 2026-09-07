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

    private ICodingSuggestionRegistry? _codingSuggestionRegistry;

    /// <summary>
    /// Sitzungsgedaechtnis der Vorabdurchlauf-Ergebnisse fuer die Uebersicht (Karte
    /// "KI-Vorabdurchlauf", Nova-Etappe 2) — bewusst Singleton je Programmlauf, wie
    /// <see cref="CodingSuggestionScan"/> lazy statt im Konstruktor von ServiceProvider.cs
    /// erzeugt (MaintainabilityFitnessTests: hoechstens 1000 Zeilen je Produktivdatei).
    /// </summary>
    public ICodingSuggestionRegistry CodingSuggestionRegistry
        => _codingSuggestionRegistry ??= new CodingSuggestionRegistry();
}
