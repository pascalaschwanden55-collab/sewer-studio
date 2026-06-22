using System.Globalization;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingOpenStretchDamagePromptBuilderTests
{
    [Fact]
    public void Build_lists_open_stretch_damage_events_and_close_meter()
    {
        using var _ = new CultureScope(CultureInfo.InvariantCulture);
        var events = new[]
        {
            Event("BAB", "Riss laengs", 1.25),
            Event("BAJ", "Versatz", 2.5)
        };

        var prompt = CodingOpenStretchDamagePromptBuilder.Build(events, currentMeter: 3.75);

        Assert.Contains("kein MeterEnde", prompt);
        Assert.Contains("\u2022 BAB \u2013 Riss laengs", prompt);
        Assert.Contains("Start: 1.25m", prompt);
        Assert.Contains("\u2022 BAJ \u2013 Versatz", prompt);
        Assert.Contains("Start: 2.50m", prompt);
        Assert.Contains("bei 3.75m geschlossen werden?", prompt);
    }

    private static CodingEvent Event(string code, string description, double meter)
        => new()
        {
            Entry = new ProtocolEntry
            {
                Code = code,
                Beschreibung = description,
                IsStreckenschaden = true
            },
            MeterAtCapture = meter
        };

    private sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo _originalCulture = CultureInfo.CurrentCulture;
        private readonly CultureInfo _originalUiCulture = CultureInfo.CurrentUICulture;

        public CultureScope(CultureInfo culture)
        {
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
        }

        public void Dispose()
        {
            CultureInfo.CurrentCulture = _originalCulture;
            CultureInfo.CurrentUICulture = _originalUiCulture;
        }
    }
}
