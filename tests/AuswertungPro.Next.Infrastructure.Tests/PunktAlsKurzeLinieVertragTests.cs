using System.Linq;
using AuswertungPro.Next.Application.Lookup;
using AuswertungPro.Next.Infrastructure.Dossiers.Lookup;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Der Parzellendienst verwirft jeden WKT-Eintrag ohne LINESTRING-Praefix
/// stillschweigend — es gibt keine Fehlermeldung, die Abfrage wird einfach
/// nicht gestellt und das Ergebnis ist leer.
///
/// Genau daran ist der Schacht-Nachschlag am 2026-08-30 gescheitert: Jede
/// Suche meldete "An dieser Lage liegt keine Parzelle", obwohl der Dienst nie
/// gefragt wurde. Ein Test gegen einen nachgebauten Dienst war dafuer blind —
/// deshalb prueft dieser hier den ECHTEN Vertrag.
/// </summary>
public sealed class PunktAlsKurzeLinieVertragTests
{
    [Fact]
    public void Der_Parzellendienst_nimmt_die_gebaute_Linie_wirklich_an()
    {
        var wkt = PunktAlsKurzeLinie.Baue(2692606.892, 1192380.717);

        var teile = UriParcelWfsClient.ExtrahiereLinienkoerper([wkt]).ToList();

        Assert.Single(teile);
        Assert.Contains("2692606.392", teile[0], System.StringComparison.Ordinal);
        Assert.Contains("2692607.392", teile[0], System.StringComparison.Ordinal);
        Assert.Contains("1192380.717", teile[0], System.StringComparison.Ordinal);
    }

    [Fact]
    public void Ohne_Praefix_verschwindet_der_Eintrag_spurlos()
    {
        // Das ist die Falle, dokumentiert als Test: kein Fehler, kein Hinweis,
        // nur ein leeres Ergebnis.
        var ohnePraefix = "(2692606.392 1192380.717, 2692607.392 1192380.717)";

        var teile = UriParcelWfsClient.ExtrahiereLinienkoerper([ohnePraefix]).ToList();

        Assert.Empty(teile);
    }
}
