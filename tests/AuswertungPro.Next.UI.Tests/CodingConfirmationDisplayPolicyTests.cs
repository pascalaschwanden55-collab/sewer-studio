using System.Windows.Media;
using AuswertungPro.Next.Application.Ai.QualityGate;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingConfirmationDisplayPolicyTests
{
    [Theory]
    [InlineData(TrafficLight.Green, 0x22, 0xC5, 0x5E)]
    [InlineData(TrafficLight.Yellow, 0xF5, 0x9E, 0x0B)]
    [InlineData(TrafficLight.Red, 0xEF, 0x44, 0x44)]
    public void AmpelColor_maps_quality_gate_to_existing_colors(TrafficLight trafficLight, byte r, byte g, byte b)
    {
        Assert.Equal(Color.FromRgb(r, g, b), CodingConfirmationDisplayPolicy.AmpelColor(Gate(trafficLight)));
    }

    [Theory]
    [InlineData(TrafficLight.Green, "QualityGate: Grün (kritisch)")]
    [InlineData(TrafficLight.Yellow, "QualityGate: Gelb")]
    [InlineData(TrafficLight.Red, "QualityGate: Rot")]
    public void QualityGateStatusText_keeps_existing_labels(TrafficLight trafficLight, string expected)
    {
        Assert.Equal(expected, CodingConfirmationDisplayPolicy.QualityGateStatusText(Gate(trafficLight)));
    }

    [Theory]
    [InlineData(TrafficLight.Green, "Kritischer Befund — bitte bestätigen oder korrigieren.")]
    [InlineData(TrafficLight.Yellow, "KI ist unsicher — bitte prüfen.")]
    [InlineData(TrafficLight.Red, "KI hat geringe Sicherheit — bitte Code korrigieren oder verwerfen.")]
    public void ConfirmationDetail_keeps_existing_user_guidance(TrafficLight trafficLight, string expected)
    {
        Assert.Equal(expected, CodingConfirmationDisplayPolicy.ConfirmationDetail(Gate(trafficLight)));
    }

    private static QualityGateResult Gate(TrafficLight trafficLight)
        => new(0.7, trafficLight, new Dictionary<string, double>(), "test");
}
