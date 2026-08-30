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
/// Ein Feld ohne Quelle wird gar nicht erst abgefragt. Bei den beiden
/// Netzquellen ist das keine Feinheit: Jede unnoetige Abfrage zaehlt gegen
/// die Drosselung des Kantons.
/// </summary>
public sealed class FeldNachschlagUseCase
{
    private readonly IFeldWertNachschlag _schachtKataster;
    private readonly IFeldWertNachschlag _grundbuch;
    private readonly IFeldWertNachschlag _haltungKataster;
    private readonly IFeldWertNachschlag? _abwassernetz;

    public FeldNachschlagUseCase(
        IFeldWertNachschlag schachtKataster,
        IFeldWertNachschlag grundbuch,
        IFeldWertNachschlag? haltungKataster = null,
        IFeldWertNachschlag? abwassernetz = null)
    {
        _schachtKataster = schachtKataster ?? throw new ArgumentNullException(nameof(schachtKataster));
        _grundbuch = grundbuch ?? throw new ArgumentNullException(nameof(grundbuch));
        _haltungKataster = haltungKataster ?? schachtKataster;
        _abwassernetz = abwassernetz;
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

        switch (quelle)
        {
            case FeldQuelle.Grundbuch:
                return _grundbuch.SucheAsync(anfrage, ct);

            case FeldQuelle.Abwassernetz when _abwassernetz is not null:
                return _abwassernetz.SucheAsync(anfrage, ct);

            case FeldQuelle.Abwassernetz:
                return Task.FromResult<FeldNachschlagErgebnis>(
                    new FeldNachschlagErgebnis.NichtGefunden(
                        "Das Abwassernetz des Kantons ist hier nicht angeschlossen."));

            default:
                return anfrage.Art == BauteilArt.Haltung
                    ? _haltungKataster.SucheAsync(anfrage, ct)
                    : _schachtKataster.SucheAsync(anfrage, ct);
        }
    }
}
