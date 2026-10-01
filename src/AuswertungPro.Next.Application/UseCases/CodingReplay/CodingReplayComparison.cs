namespace AuswertungPro.Next.Application.UseCases.CodingReplay;

/// <summary>Vergleicht sichtbare Codevorschlaege eines Bildes, keine Ereignis-Recall-Messung.</summary>
public enum CodingReplayComparison
{
    NotMeasured,
    ReviewRequired,
    ExactCodePresent,
    SameFamilyPresent,
    ReferenceNotProposed,
    NoDamageCodeProposed,
    DamageCodeOnNegative
}

public static class CodingReplayComparer
{
    // Nur die Auswertung erhaelt eine Referenz. Der Analysator bekommt sie nie.
    public static CodingReplayComparison Compare(string expectedCode, CodingReplayResult? result)
    {
        if (result?.Status != "measured" || result.Observation is null || result.Observation.TechnicalError is not null)
            return CodingReplayComparison.NotMeasured;
        if (result.Observation.Outcome == "ReviewRequired") return CodingReplayComparison.ReviewRequired;
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedCode);
        var expected = expectedCode.Trim().ToUpperInvariant();
        var codes = result.Observation.Events.Select(e => e.Code.Trim().ToUpperInvariant()).ToArray();
        if (expected == "LEER")
            return codes.Any(c => c.StartsWith("BA", StringComparison.Ordinal) || c.StartsWith("BB", StringComparison.Ordinal))
                ? CodingReplayComparison.DamageCodeOnNegative : CodingReplayComparison.NoDamageCodeProposed;
        if (codes.Contains(expected)) return CodingReplayComparison.ExactCodePresent;
        if (expected.Length >= 3 && codes.Any(c => c.Length >= 3 && c[..3] == expected[..3]))
            return CodingReplayComparison.SameFamilyPresent;
        return CodingReplayComparison.ReferenceNotProposed;
    }
}
