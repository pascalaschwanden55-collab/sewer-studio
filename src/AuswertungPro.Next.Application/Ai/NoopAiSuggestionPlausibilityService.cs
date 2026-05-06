
namespace AuswertungPro.Next.Application.Ai;

public sealed class NoopAiSuggestionPlausibilityService : IAiSuggestionPlausibilityService
{
    public AiSuggestionResult ApplyChecks(AiSuggestionResult suggestion, ObservationContext context)
        => suggestion;
}
