using System;

namespace AuswertungPro.Next.Infrastructure.Dossiers.Lookup;

/// <summary>
/// Eine Abfrage an den Kartendienst ist fehlgeschlagen — Netz weg, Zeitgrenze
/// oder ein Fehlerstatus.
///
/// Sie ist ausdruecklich verschieden von "nichts gefunden": Ein leeres Ergebnis
/// heisst, dass es dort nichts gibt; ein Fehlschlag heisst, dass wir es nicht
/// wissen. Wuerden beide gleich behandelt, entstuende ein Dossier mit zu wenigen
/// Leitungen, ohne dass es jemandem auffaellt.
/// </summary>
public sealed class GeoUrRequestFailedException : Exception
{
    public GeoUrRequestFailedException(string message, Exception? inner = null)
        : base(message, inner)
    {
    }
}
