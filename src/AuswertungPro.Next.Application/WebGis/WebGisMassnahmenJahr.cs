using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AuswertungPro.Next.Application.WebGis;

/// <summary>
/// Liest das Sanierungsjahr bestehender Massnahmen aus ihrer eigenen Maske nach. Anlass: Lesung am echten
/// WebGIS vom 28.09.2026 (nur lesend) — die erste Spalte der Massnahmenliste am Elternobjekt heisst
/// «Zeitpunkt» und war leer, obwohl die Massnahme selbst «Sanierungsjahr 01.01.2026» trägt. Ohne Nachlesen
/// griff der Jahresvergleich nie: Eine Reparatur 2026 neben einer Reparatur 2020 galt immer als «bereits
/// vorhanden».
///
/// Nur lesend. Gelesen wird nur, wo es für den Vergleich zählt (<paramref name="nurWenn"/>). Ein Lesefehler
/// lässt das Jahr offen — dann gilt die Massnahme wie bisher als vorhanden (kein Doppel). Nur eine
/// abgelaufene Sitzung und ein Abbruch gehen weiter.
/// </summary>
public static class WebGisMassnahmenJahr
{
    public static async Task ErgaenzeAsync(
        IGeonisWebGisClient client, IEnumerable<WebGisSanierungZeile> zeilen,
        Func<WebGisSanierungZeile, bool>? nurWenn = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(zeilen);

        foreach (var zeile in zeilen.ToList())
        {
            if (zeile.Jahr is not null || string.IsNullOrWhiteSpace(zeile.GlobalId)) continue;
            if (nurWenn is not null && !nurWenn(zeile)) continue;
            try
            {
                var massnahme = await client.LeseMassnahmeAsync(zeile.GlobalId!, ct).ConfigureAwait(false);
                zeile.Sanierungsjahr = massnahme?.Feld(WebGisSanierungFeldkarte.SanierungsjahrRef);
            }
            catch (OperationCanceledException) { throw; }
            catch (WebGisSitzungException) { throw; }
            catch (Exception)
            {
                // Jahr bleibt offen: Die Massnahme gilt dann als vorhanden — lieber keine als eine doppelte.
            }
        }
    }
}
