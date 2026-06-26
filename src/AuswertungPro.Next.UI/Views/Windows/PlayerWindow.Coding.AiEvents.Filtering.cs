using System.Collections.Generic;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    /// <summary>
    /// Filtert und normalisiert KI-Findings.
    /// Nach diesem Schritt gilt fuer jedes Finding:
    /// - VsaCodeHint ist ein gueltiger VSA-Code oder das Finding wurde verworfen.
    /// - Keine "???"-Codes, keine ungeprueften Hint-Werte.
    /// </summary>
    private IReadOnlyList<LiveFrameFinding> FilterValidFindings(IReadOnlyList<LiveFrameFinding> raw, double currentMeter)
    {
        return CodingFindingFilterPolicy.FilterValid(
            raw,
            currentMeter,
            ResolveFindingCodeForCoding,
            _codingSessionRuntimeOwner.Service?.ActiveSession?.Events,
            _codingSessionHost.Events,
            message => PlayerTrace.WriteLine(message));
    }

    /// <summary>Delegiert an VsaCodeResolver.LookupLabel.</summary>
    private static string? LookupVsaLabel(string code) => VsaCodeResolver.LookupLabel(code);

    /// <summary>
    /// Eine Quelle fuer VSA-Code-Aufloesung eines KI-Findings.
    /// Gibt validen VSA-Code oder null zurueck, nie "???".
    /// </summary>
    private string? ResolveFindingCodeForCoding(LiveFrameFinding finding, double currentMeter)
    {
        return CodingFindingCodeResolver.Resolve(finding, currentMeter, _codingImportReferenceEvents.Events);
    }

    private bool IsFindingAlreadyKnown(LiveFrameFinding finding, double meter)
    {
        return CodingKnownFindingPolicy.IsKnown(
            finding,
            meter,
            _codingSessionRuntimeOwner.Service?.ActiveSession?.Events,
            _codingSessionHost.Events);
    }
}
