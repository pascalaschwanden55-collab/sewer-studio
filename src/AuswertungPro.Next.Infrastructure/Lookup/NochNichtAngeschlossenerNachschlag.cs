using System;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Lookup;

namespace AuswertungPro.Next.Infrastructure.Lookup;

/// <summary>
/// Platzhalter fuer eine Quelle, die es noch nicht gibt. Meldet den Grund im
/// Klartext, statt stillschweigend "nicht gefunden" zu liefern — sonst haelt
/// der Bearbeiter eine fehlende Anbindung fuer eine Datenluecke.
///
/// Wird ersetzt, sobald der Grundbuch-Anbieter angeschlossen ist.
/// </summary>
public sealed class NochNichtAngeschlossenerNachschlag : IFeldWertNachschlag
{
    private readonly string _grund;

    public NochNichtAngeschlossenerNachschlag(string grund)
        => _grund = grund ?? throw new ArgumentNullException(nameof(grund));

    public Task<FeldNachschlagErgebnis> SucheAsync(
        FeldNachschlagAnfrage anfrage, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(anfrage);
        return Task.FromResult<FeldNachschlagErgebnis>(
            new FeldNachschlagErgebnis.NichtGefunden(_grund));
    }
}
