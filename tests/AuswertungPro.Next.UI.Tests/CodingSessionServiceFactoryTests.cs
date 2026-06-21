using System;
using System.Collections.Generic;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingSessionServiceFactoryTests
{
    [Fact]
    public void Create_returns_infrastructure_coding_session_service()
    {
        var service = CodingSessionServiceFactory.Create(
            () => null,
            () => new EvalContaminationSets(
                new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                new HashSet<string>(StringComparer.OrdinalIgnoreCase)));

        Assert.IsType<CodingSessionService>(service);
    }

    [Fact]
    public void Create_does_not_load_eval_sets_until_service_needs_them()
    {
        var loaded = false;

        CodingSessionServiceFactory.Create(
            () => null,
            () =>
            {
                loaded = true;
                return new EvalContaminationSets(
                    new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                    new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            });

        Assert.False(loaded);
    }
}
