using System;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Lookup;

namespace AuswertungPro.Next.Application.UseCases;

/// <summary>
/// Waehlt anhand von Feldname und Bauteilart die zustaendige Quelle und
/// reicht deren Ergebnis unveraendert weiter. Schreibt selbst nichts — die
/// Uebernahme bleibt eine bewusste Entscheidung des Bearbeiters.
///
/// Ein Feld ohne Quelle wird gar nicht erst abgefragt. Beim Grundbuch ist das
/// keine Feinheit: Jede unnoetige Abfrage zaehlt gegen die Drosselung.
/// </summary>
public sealed class FeldNachschlagUseCase
{
    private readonly IFeldWertNachschlag _schachtKataster;
    private readonly IFeldWertNachschlag _grundbuch;
    private readonly IFeldWertNachschlag _haltungKataster;

    public FeldNachschlagUseCase(
        IFeldWertNachschlag schachtKataster,
        IFeldWertNachschlag grundbuch,
        IFeldWertNachschlag? haltungKataster = null)
    {
        _schachtKataster = schachtKataster ?? throw new ArgumentNullException(nameof(schachtKataster));
        _grundbuch = grundbuch ?? throw new ArgumentNullException(nameof(grundbuch));
        _haltungKataster = haltungKataster ?? schachtKataster;
    }

    public Task<FeldNachschlagErgebnis> SucheAsync(
        FeldNachschlagAnfrage anfrage, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(anfrage);

        var quelle = FeldQuellenTabelle.QuelleFuer(anfrage.Feldname, anfrage.Art);
        if (quelle is null)
        {
            return Task.FromResult<FeldNachschlagErgebnis>(
                new FeldNachschlagErgebnis.NichtGefunden(
                    $"Fuer das Feld {anfrage.Feldname} gibt es keine Quelle."));
        }

        if (quelle == FeldQuelle.Grundbuch)
            return _grundbuch.SucheAsync(anfrage, ct);

        return anfrage.Art == BauteilArt.Haltung
            ? _haltungKataster.SucheAsync(anfrage, ct)
            : _schachtKataster.SucheAsync(anfrage, ct);
    }
}
