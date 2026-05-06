using AuswertungPro.Next.Application.Ai;

namespace AuswertungPro.Next.UI.Ai;

public sealed class NoopAiSuggestionPlausibilityService : IAiSuggestionPlausibilityService
{
    public AiSuggestionResult ApplyChecks(AiSuggestionResult suggestion, ObservationContext context)
        => suggestion;
}
