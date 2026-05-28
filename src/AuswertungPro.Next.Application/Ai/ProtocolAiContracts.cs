using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AuswertungPro.Next.Application.Ai;

public sealed record AiInput(
    string ProjectFolderAbs,
    string? HaltungId,
    double? Meter,
    string? ExistingCode,
    string? ExistingText,
    IReadOnlyList<string> AllowedCodes,
    string? VideoPathAbs = null,
    TimeSpan? Zeit = null,
    IReadOnlyList<string>? ImagePathsAbs = null,
    string? XtfSnippet = null
);

public sealed record AiSuggestion(
    string? SuggestedCode,
    double Confidence,
    string? Reason,
    IReadOnlyList<string> Flags)
{
    public string? ReasonShort => Reason;
}

public interface IProtocolAiService
{
    Task<AiSuggestion?> SuggestAsync(AiInput input, CancellationToken ct = default);
}

public sealed class NoopProtocolAiService : IProtocolAiService
{
    public Task<AiSuggestion?> SuggestAsync(AiInput input, CancellationToken ct = default)
        => Task.FromResult<AiSuggestion?>(null);
}
