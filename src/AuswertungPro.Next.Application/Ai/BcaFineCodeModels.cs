using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AuswertungPro.Next.Application.Ai;

/// <summary>Ein feiner Anschluss-Code-Vorschlag (Bauart) mit Konfidenz.</summary>
public sealed record BcaFineCodeCandidate(string VsaCode, double Confidence);

/// <summary>
/// Ergebnis der feinen Anschluss-Codierung: absteigend sortierte Kandidaten, oder
/// <see cref="IsUncertain"/> = true, wenn keine sichere Bauart bestimmbar war.
/// </summary>
public sealed record BcaFineCodeSuggestion(
    IReadOnlyList<BcaFineCodeCandidate> Candidates,
    bool IsUncertain)
{
    /// <summary>Kein sicherer Feincode — der grobe Code BCA bleibt bestehen.</summary>
    public static BcaFineCodeSuggestion Uncertain { get; } =
        new(Array.Empty<BcaFineCodeCandidate>(), true);
}

/// <summary>
/// Bestimmt zu einem erkannten Anschluss den feinen VSA-Code (Bauart + offen/verschlossen)
/// aus dem Bild. Reiner Zusatz: bei Fehler/Unsicherheit werden leere Kandidaten geliefert.
/// </summary>
public interface IBcaFineCodeClassifier
{
    Task<BcaFineCodeSuggestion> SuggestAsync(string anschlussBildBase64, CancellationToken ct = default);
}
