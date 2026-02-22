namespace AuswertungPro.Next.Domain.Vsa;

public sealed class VsaConditionResult
{
    public VsaRequirement Requirement { get; init; }
    public double? Zustandsnote { get; init; }         // 0.00..4.00
    public double? Abminderung { get; init; }          // A
    public int? WorstEinzelzustand { get; init; }      // EZmin
    public double? Dringlichkeitszahl { get; init; }   // DZ

    public List<string> Notes { get; } = new();
}
