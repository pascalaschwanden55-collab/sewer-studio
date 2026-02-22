namespace AuswertungPro.Next.Domain.Vsa;

public sealed record VsaClassificationResult(
    int? EZD, // 0..4 oder null (nicht klassifizierbar)
    int? EZS,
    int? EZB
);
