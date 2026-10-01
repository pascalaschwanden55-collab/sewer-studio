using System;
using System.Collections.Generic;
using System.Threading;

namespace AuswertungPro.Next.Application.Lookup;

/// <summary>Unveraenderte XTF-Kennungen und bereits normalisierte Projektwerte.</summary>
public sealed record GeoShopBauteil(
    string Name, KatasterKennung Kennungen, IReadOnlyDictionary<string, string> Felder,
    string? VonSchacht = null, string? NachSchacht = null, string? Fehler = null, string? Hinweis = null,
    IReadOnlyList<AuswertungPro.Next.Domain.Models.ObjektQuellbeleg>? Quellen = null);

public sealed record GeoShopBestand(
    BauteilArt Art, string Quelle, IReadOnlyList<GeoShopBauteil> Bauteile);

public interface IGeoShopLeser
{
    IReadOnlyDictionary<string, string> LiesEigentuemer(string datei)
        => throw new NotSupportedException("Dieser Leser unterstützt keine Eigentümerdatei.");
    /// <summary>Liest nur die angefragten Namen samt Gegenrichtung und Objektverbund. Keine Schreibzugriffe.</summary>
    GeoShopBestand Lies(string datei, BauteilArt art, IReadOnlyCollection<string> namen,
        CancellationToken cancellationToken = default);
}
