namespace AuswertungPro.Next.UI.ViewModels.Protocol;

public sealed record AiSuggestion(
    string? SuggestedCode,
    double Confidence,
    string? Reason,
    IReadOnlyList<string> Flags)
{
    public string? ReasonShort => Reason; // optionaler Alias
}
