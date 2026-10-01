using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AuswertungPro.Next.Application.Dossiers.Lookup;

/// <summary>
/// Ein Schacht aus dem Abwassernetz des Kantons. <paramref name="Eigentuemer"/>
/// ist der Eigentuemer des BAUWERKS (Privat, Abwasser Uri, Kanton Uri, eine
/// Gemeinde) — nicht der Grundstuecksbesitzer aus dem Grundbuch. Bei manchen
/// Anlagen sind das verschiedene Parteien.
/// </summary>
public sealed record NetworkSchacht(string Bezeichnung, string Eigentuemer)
{
    public string? Funktion { get; init; }
    public string? Material { get; init; }
    public string? Nutzungsart { get; init; }
    public string? Status { get; init; }
}

/// <summary>Liest Schaechte aus dem Abwassernetz des Kantons.</summary>
public interface ISchachtNetzLookup
{
    Task<IReadOnlyList<NetworkSchacht>> FindByNamesAsync(
        IReadOnlyList<string> namen, CancellationToken ct = default);
}
