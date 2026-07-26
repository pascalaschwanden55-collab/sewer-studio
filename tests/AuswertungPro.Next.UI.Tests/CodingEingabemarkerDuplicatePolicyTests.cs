using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingEingabemarkerDuplicatePolicyTests
{
    [Fact]
    public void FindDuplicate_matches_one_time_code_independent_of_meter()
    {
        var existing = new[]
        {
            CreateEvent("BCD", meter: 0.0)
        };

        var duplicate = CodingEingabemarkerDuplicatePolicy.FindDuplicate(
            existing,
            "BCD",
            currentMeter: 12.5);

        Assert.Same(existing[0], duplicate);
    }

    [Fact]
    public void FindDuplicate_matches_regular_code_inside_meter_window()
    {
        var existing = new[]
        {
            CreateEvent("BCA", meter: 5.0)
        };

        var duplicate = CodingEingabemarkerDuplicatePolicy.FindDuplicate(
            existing,
            "BCA",
            currentMeter: 5.9);

        Assert.Same(existing[0], duplicate);
    }

    [Fact]
    public void FindDuplicate_ignores_regular_code_outside_meter_window()
    {
        var existing = new[]
        {
            CreateEvent("BCA", meter: 5.0)
        };

        var duplicate = CodingEingabemarkerDuplicatePolicy.FindDuplicate(
            existing,
            "BCA",
            currentMeter: 6.0);

        Assert.Null(duplicate);
    }

    private static CodingEvent CreateEvent(string code, double meter)
        => new()
        {
            Entry = new ProtocolEntry { Code = code },
            MeterAtCapture = meter
        };
}
