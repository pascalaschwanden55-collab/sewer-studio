using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Domain.Models;

/// <summary>Vorheriger vollstaendiger Beleg einer automatischen Vorschlagskorrektur.
/// AiContext enthaelt hier keine weitere History; die Liste bleibt flach.</summary>
public sealed record CodingProposalEvidenceSnapshot(
    ProtocolEntry Entry, OverlayGeometry? Overlay, CodingEventAiContext AiContext,
    double MeterAtCapture, TimeSpan VideoTimestamp);
