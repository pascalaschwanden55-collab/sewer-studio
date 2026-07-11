using System;
using System.Collections.Generic;

namespace AuswertungPro.Next.Domain.Models;

/// <summary>
/// Persistierbarer Schnappschuss einer zentralen KI-Entscheidung. Die Domain speichert
/// nur neutrale Werte und kennt weder die konkrete Policy noch das QualityGate.
/// </summary>
public sealed class AiDecisionAudit
{
    public string Outcome { get; set; } = "";
    public string ReasonCode { get; set; } = "";
    public string Reason { get; set; } = "";
    public string PolicyVersion { get; set; } = "";
    public AiDecisionSignalAudit Signals { get; set; } = new();
    public AiDecisionThresholdAudit Thresholds { get; set; } = new();
    public string? VisionModel { get; set; }
    public string? TextModel { get; set; }
    public string? QualityGateVersion { get; set; }
    public Dictionary<string, double> QualityGateWeights { get; set; } = new(StringComparer.Ordinal);
    public string? QualityGateExplanation { get; set; }
    public DateTimeOffset DecidedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class AiDecisionSignalAudit
{
    public double Confidence { get; set; }
    public string? QualityGate { get; set; }
    public bool? KbAgreement { get; set; }
    public double? EpistemicUncertainty { get; set; }
}

public sealed class AiDecisionThresholdAudit
{
    public double AutoAcceptConfidence { get; set; }
    public double RejectConfidence { get; set; }
    public double MaxEpistemicUncertainty { get; set; }
}

public static class AiDecisionAuditCloner
{
    public static AiDecisionAudit? Clone(AiDecisionAudit? source)
        => source is null
            ? null
            : new AiDecisionAudit
            {
                Outcome = source.Outcome,
                ReasonCode = source.ReasonCode,
                Reason = source.Reason,
                PolicyVersion = source.PolicyVersion,
                Signals = new AiDecisionSignalAudit
                {
                    Confidence = source.Signals.Confidence,
                    QualityGate = source.Signals.QualityGate,
                    KbAgreement = source.Signals.KbAgreement,
                    EpistemicUncertainty = source.Signals.EpistemicUncertainty
                },
                Thresholds = new AiDecisionThresholdAudit
                {
                    AutoAcceptConfidence = source.Thresholds.AutoAcceptConfidence,
                    RejectConfidence = source.Thresholds.RejectConfidence,
                    MaxEpistemicUncertainty = source.Thresholds.MaxEpistemicUncertainty
                },
                VisionModel = source.VisionModel,
                TextModel = source.TextModel,
                QualityGateVersion = source.QualityGateVersion,
                QualityGateWeights = new Dictionary<string, double>(
                    source.QualityGateWeights,
                    StringComparer.Ordinal),
                QualityGateExplanation = source.QualityGateExplanation,
                DecidedAtUtc = source.DecidedAtUtc
            };
}
