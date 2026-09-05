using System.Threading;
using System.Threading.Tasks;

namespace AuswertungPro.Next.Application.Ai;

/// <summary>
/// Zugang zu den freigegebenen Bild-Einordnern des Sidecars (Rohranfang, Rohrende).
///
/// Bewusst ein eigener, kleiner Vertrag neben <see cref="IVisionPipelineClient"/>:
/// Der grosse Vertrag hat ueber ein Dutzend Fakes in Tests und Werkzeugen, und
/// diese Modelle gehoeren nicht in die produktive Analysekette, sondern nur in
/// den Vorabdurchlauf des Training Studios.
/// </summary>
public interface ILernstufeClient
{
    /// <summary>Nennt die freigegebenen Lernstufen samt gemessener Guete.</summary>
    Task<LernstufenResponse> GetLernstufenAsync(CancellationToken ct = default);

    /// <summary>Ordnet EIN Bild ein: Konfidenz fuer das ganze Bild, keine Box.</summary>
    Task<LernstufeResponse> ClassifyLernstufeAsync(LernstufeRequest request, CancellationToken ct = default);
}
