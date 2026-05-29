using System.Collections.Generic;

namespace AuswertungPro.Next.Infrastructure.Ai;

public static class OllamaDeterministicOptions
{
    public const int Seed = 42;
    public const int NumCtx = 12288;

    public static Dictionary<string, object> Create() => new()
    {
        ["temperature"] = 0,
        ["seed"] = Seed,
        ["num_ctx"] = NumCtx
    };
}
