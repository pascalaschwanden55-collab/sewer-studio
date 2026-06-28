using System;
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Domain.Models.Costs;

namespace AuswertungPro.Next.Infrastructure.Costs;

/// <summary>
/// Reine statische Berechnungsklasse für die Angebots-Totale.
/// Kaskade: SubTotal -> Rabatt -> Skonto -> NetExclMwst -> MwSt -> Total.
/// Decimal-Arithmetik identisch mit der bisherigen Logik in
/// <see cref="CostCalculationService.CalculateOffer"/> und
/// <see cref="CostCalculationService.CalculateCombinedOffer"/>.
/// </summary>
public static class LegacyOfferTotalsCalculator
{
    /// <summary>
    /// Berechnet die Angebots-Totale aus den übergebenen Positionen und Sätzen.
    /// </summary>
    /// <param name="lines">Bereits berechnete Positionen (Amount kann null sein = Preis fehlt).</param>
    /// <param name="rabattPct">Rabattsatz in Prozent (z. B. 10 für 10 %).</param>
    /// <param name="skontoPct">Skontosatz in Prozent (z. B. 5 für 5 %).</param>
    /// <param name="mwstPct">Mehrwertsteuersatz in Prozent (z. B. 8.1 für 8.1 %).</param>
    /// <param name="currency">Währungskürzel (z. B. "CHF").</param>
    /// <returns>Befülltes <see cref="OfferTotals"/>-Objekt.</returns>
    public static OfferTotals BuildTotals(
        List<OfferLine> lines,
        decimal rabattPct,
        decimal skontoPct,
        decimal mwstPct,
        string currency)
    {
        var subTotal     = Math.Round(lines.Where(l => l.Amount.HasValue).Sum(l => l.Amount!.Value), 2);
        var rabatt       = Math.Round(subTotal * rabattPct / 100m, 2);
        var afterRabatt  = Math.Round(subTotal - rabatt, 2);
        var skonto       = Math.Round(afterRabatt * skontoPct / 100m, 2);
        var netExcl      = Math.Round(afterRabatt - skonto, 2);
        var mwst         = Math.Round(netExcl * mwstPct / 100m, 2);
        var total        = Math.Round(netExcl + mwst, 2);

        return new OfferTotals
        {
            SubTotal      = subTotal,
            RabattPct     = rabattPct,
            Rabatt        = rabatt,
            SkontoPct     = skontoPct,
            Skonto        = skonto,
            NetExclMwst   = netExcl,
            MwstPct       = mwstPct,
            Mwst          = mwst,
            TotalInclMwst = total,
            Currency      = currency
        };
    }
}
