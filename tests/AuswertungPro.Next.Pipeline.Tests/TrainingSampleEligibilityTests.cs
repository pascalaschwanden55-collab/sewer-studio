using System;
using AuswertungPro.Next.Application.Ai.Training;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class TrainingSampleEligibilityTests
{
    [Theory]
    [InlineData("31.12.2021", false, TrainingSampleEligibility.LegacyBeforeCutoffReason)]
    [InlineData("01.01.2022", true, null)]
    [InlineData("2022-01-01", true, null)]
    [InlineData("Aufnahmen: 04.12.14 - 05.12.14", false, TrainingSampleEligibility.LegacyBeforeCutoffReason)]
    [InlineData("GEP Aufnahmen Altdorf 2025", true, null)]
    public void Evaluate_nutzt_2022_als_harten_Stichtag(string rawDate, bool expectedEligible, string? expectedReason)
    {
        var parsed = TrainingSampleEligibility.TryParseInspectionDate(rawDate);

        Assert.NotNull(parsed);
        var result = TrainingSampleEligibility.Evaluate(parsed);

        Assert.Equal(expectedEligible, result.IsEligible);
        Assert.Equal(expectedReason, result.Reason);
    }

    [Fact]
    public void Evaluate_sperrt_unbekanntes_Datum()
    {
        var result = TrainingSampleEligibility.Evaluate((DateTime?)null);

        Assert.False(result.IsEligible);
        Assert.Equal(TrainingSampleEligibility.MissingInspectionDateReason, result.Reason);
    }
}
