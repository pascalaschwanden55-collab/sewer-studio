using System;
using System.Collections.Generic;
using AuswertungPro.Next.Application.UseCases.BendSuggestions;
using AuswertungPro.Next.Application.UseCases.CodingSuggestions;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>Was ein Klick auf "Bestaetigen" ausloest — ohne WPF entschieden.</summary>
public sealed class CodingSuggestionConfirmPolicyTests
{
    private static readonly IReadOnlyList<MeterTrackPoint> Spur =
    [
        new(142.0, 42.10, false),
        new(143.0, 42.35, false),
        new(150.0, 44.00, true)
    ];

    [Fact]
    public void Bogen_oeffnet_das_Codierfenster_mit_BCC_und_dem_Vorschlagsmeter()
    {
        var plan = CodingSuggestionConfirmPolicy.Plan(
            new CodingSuggestion(CodingSuggestionKind.Bogen, 30.0, 9.42, false, 0.9, true, 0.0),
            Spur, activeCodes: [], hasHoldingLength: true);

        Assert.Equal(CodingSuggestionConfirmAction.OpenCodeWindow, plan.Action);
        Assert.Equal("BCC", plan.Code);
        Assert.Equal(9.42, plan.Meter);
        Assert.False(plan.ProposeLength);
    }

    [Fact]
    public void Bogen_mit_geschaetztem_Meter_gibt_keinen_Meter_vor()
    {
        var plan = CodingSuggestionConfirmPolicy.Plan(
            new CodingSuggestion(CodingSuggestionKind.Bogen, 30.0, 9.4, true, 0.9, true, 0.0),
            Spur, [], true);

        Assert.Null(plan.Meter);
    }

    [Fact]
    public void Rohranfang_legt_BCD_bei_null_Meter_an()
    {
        var plan = CodingSuggestionConfirmPolicy.Plan(
            new CodingSuggestion(CodingSuggestionKind.Rohranfang, 4.0, null, false, 0.97, true, 0.85),
            Spur, [], true);

        Assert.Equal(CodingSuggestionConfirmAction.CreateBoundaryEvent, plan.Action);
        Assert.Equal("BCD", plan.Code);
        Assert.Equal(0.0, plan.Meter);
    }

    [Fact]
    public void Ein_vorhandenes_BCD_wird_nicht_doppelt_angelegt()
    {
        var plan = CodingSuggestionConfirmPolicy.Plan(
            new CodingSuggestion(CodingSuggestionKind.Rohranfang, 4.0, null, false, 0.97, true, 0.85),
            Spur, ["BCD", "BAB"], true);

        Assert.Equal(CodingSuggestionConfirmAction.AlreadyPresent, plan.Action);
        Assert.Contains("bereits", plan.Hinweis);
    }

    [Fact]
    public void Rohrende_nimmt_den_Meter_aus_der_Spur_und_schlaegt_die_Laenge_vor_wenn_sie_fehlt()
    {
        var plan = CodingSuggestionConfirmPolicy.Plan(
            new CodingSuggestion(CodingSuggestionKind.Rohrende, 143.4, null, false, 0.91, true, 0.89),
            Spur, [], hasHoldingLength: false);

        Assert.Equal(CodingSuggestionConfirmAction.CreateBoundaryEvent, plan.Action);
        Assert.Equal("BCE", plan.Code);
        Assert.Equal(42.35, plan.Meter);
        Assert.True(plan.ProposeLength);
    }

    [Fact]
    public void Rohrende_mit_vorhandener_Laenge_schlaegt_nichts_vor()
    {
        var plan = CodingSuggestionConfirmPolicy.Plan(
            new CodingSuggestion(CodingSuggestionKind.Rohrende, 143.4, null, false, 0.91, true, 0.89),
            Spur, [], hasHoldingLength: true);

        Assert.Equal(42.35, plan.Meter);
        Assert.False(plan.ProposeLength);
    }

    [Fact]
    public void Rohrende_mit_geschaetztem_Spurwert_schlaegt_nie_eine_Laenge_vor()
    {
        var plan = CodingSuggestionConfirmPolicy.Plan(
            new CodingSuggestion(CodingSuggestionKind.Rohrende, 150.2, null, false, 0.91, true, 0.89),
            Spur, [], hasHoldingLength: false);

        Assert.Null(plan.Meter);
        Assert.False(plan.ProposeLength);
    }

    [Fact]
    public void Rohrende_ohne_Spur_legt_BCE_ohne_Meter_an()
    {
        var plan = CodingSuggestionConfirmPolicy.Plan(
            new CodingSuggestion(CodingSuggestionKind.Rohrende, 143.4, null, false, 0.91, true, 0.89),
            Array.Empty<MeterTrackPoint>(), [], false);

        Assert.Equal(CodingSuggestionConfirmAction.CreateBoundaryEvent, plan.Action);
        Assert.Null(plan.Meter);
        Assert.False(plan.ProposeLength);
    }
}
