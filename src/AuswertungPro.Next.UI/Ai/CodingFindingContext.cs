using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Ai;

namespace AuswertungPro.Next.UI.Ai;

public sealed class CodingFindingContext
{
    private readonly Func<IEnumerable<CodingEvent>?> _sessionEvents;
    private readonly Func<IEnumerable<CodingEvent>?> _viewEvents;
    private readonly Func<IEnumerable<CodingEvent>> _importEvents;
    private readonly Func<LiveFrameFinding, double, IEnumerable<CodingEvent>, string?> _codeResolver;
    private readonly Func<string, string?> _labelLookup;
    private readonly Action<string>? _trace;

    public CodingFindingContext(
        Func<IEnumerable<CodingEvent>?> sessionEvents,
        Func<IEnumerable<CodingEvent>?> viewEvents,
        Func<IEnumerable<CodingEvent>> importEvents,
        Func<LiveFrameFinding, double, IEnumerable<CodingEvent>, string?> codeResolver,
        Func<string, string?> labelLookup,
        Action<string>? trace = null)
    {
        ArgumentNullException.ThrowIfNull(sessionEvents);
        ArgumentNullException.ThrowIfNull(viewEvents);
        ArgumentNullException.ThrowIfNull(importEvents);
        ArgumentNullException.ThrowIfNull(codeResolver);
        ArgumentNullException.ThrowIfNull(labelLookup);

        _sessionEvents = sessionEvents;
        _viewEvents = viewEvents;
        _importEvents = importEvents;
        _codeResolver = codeResolver;
        _labelLookup = labelLookup;
        _trace = trace;
    }

    public static CodingFindingContext CreateDefault(
        Func<IEnumerable<CodingEvent>?> sessionEvents,
        Func<IEnumerable<CodingEvent>?> viewEvents,
        Func<IEnumerable<CodingEvent>> importEvents,
        Action<string>? trace = null)
        => new(
            sessionEvents,
            viewEvents,
            importEvents,
            CodingFindingCodeResolver.Resolve,
            VsaCodeResolver.LookupLabel,
            trace);

    public IReadOnlyList<LiveFrameFinding> FilterValid(
        IReadOnlyList<LiveFrameFinding> findings,
        double currentMeter)
        => CodingFindingFilterPolicy.FilterValid(
            findings,
            currentMeter,
            ResolveCode,
            _sessionEvents(),
            _viewEvents(),
            _trace);

    public string? ResolveCode(LiveFrameFinding finding, double currentMeter)
        => _codeResolver(finding, currentMeter, _importEvents());

    public string? LookupLabel(string code) => _labelLookup(code);

    public bool IsKnown(LiveFrameFinding finding, double meter)
        => CodingKnownFindingPolicy.IsKnown(
            finding,
            meter,
            _sessionEvents(),
            _viewEvents());
}
