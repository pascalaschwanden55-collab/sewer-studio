using System;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Lookup;

namespace AuswertungPro.Next.Application.UseCases;

/// <summary>
/// Waehlt anhand des Feldnamens die zustaendige Quelle und reicht deren
/// Ergebnis unveraendert weiter. Schreibt selbst nichts — die Uebernahme
/// bleibt eine bewusste Entscheidung des Bearbeiters.
///
/// Ein Feld ohne Quelle wird gar nicht erst abgefragt. Beim Grundbuch ist das
/// keine Feinheit: Jede unnoetige Abfrage zaehlt gegen die Drosselung.
/// </summary>
public sealed class FeldNachschlagUseCase
{
    private readonly IFeldWertNachschlag _kataster;
    private readonly IFeldWertNachschlag _grundbuch;

    public FeldNachschlagUseCase(IFeldWertNachschlag kataster, IFeldWertNachschlag grundbuch)
    {
        _kataster = kataster ?? throw new ArgumentNullException(nameof(kataster));
        _grundbuch = grundbuch ?? throw new ArgumentNullException(nameof(grundbuch));
    }

    public Task<FeldNachschlagErgebnis> SucheAsync(
        FeldNachschlagAnfrage anfrage, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(anfrage);

        var quelle = FeldQuellenTabelle.QuelleFuer(anfrage.Feldname);
        if (quelle is null)
        {
            return Task.FromResult<FeldNachschlagErgebnis>(
                new FeldNachschlagErgebnis.NichtGefunden(
                    $"Fuer das Feld {anfrage.Feldname} gibt es keine Quelle."));
        }

        return quelle == FeldQuelle.Kataster
            ? _kataster.SucheAsync(anfrage, ct)
            : _grundbuch.SucheAsync(anfrage, ct);
    }
}
