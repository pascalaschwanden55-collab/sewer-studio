using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Domain.Protocol;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Training;

public class TrainingSamplePlausibilityTests
{
    private static TrainingSample Sample(
        double start = 5,
        double end = 5,
        string? severity = null,
        string? querschnitt = null)
    {
        var sample = new TrainingSample
        {
            Code = "BAB",
            Beschreibung = "Riss laengs am Scheitel",
            MeterStart = start,
            MeterEnd = end
        };

        if (severity is not null || querschnitt is not null)
        {
            sample.CodeMeta = new ProtocolEntryCodeMeta
            {
                Code = "BAB",
                Severity = severity
            };

            if (querschnitt is not null)
                sample.CodeMeta.Parameters["vsa.querschnitt.prozent"] = querschnitt;
        }

        return sample;
    }

    [Fact]
    public void PlausiblesSample_IstPlausibel()
        => Assert.True(TrainingSamplePlausibility.IsFachlichPlausibel(
            Sample(start: 5, end: 8, severity: "3", querschnitt: "40"), out _));

    [Fact]
    public void NegativerMeterstand_IstImplausibel()
        => Assert.False(TrainingSamplePlausibility.IsFachlichPlausibel(
            Sample(start: -1, end: 2), out _));

    [Fact]
    public void InvertierterBereich_IstImplausibel()
        => Assert.False(TrainingSamplePlausibility.IsFachlichPlausibel(
            Sample(start: 8, end: 5), out _));

    [Theory]
    [InlineData("0")]
    [InlineData("6")]
    [InlineData("-2")]
    public void SeverityAusserhalb1Bis5_IstImplausibel(string severity)
        => Assert.False(TrainingSamplePlausibility.IsFachlichPlausibel(
            Sample(severity: severity), out _));

    [Fact]
    public void SeverityNichtGesetzt_WirdNichtGeprueft()
        => Assert.True(TrainingSamplePlausibility.IsFachlichPlausibel(
            Sample(severity: null), out _));

    [Theory]
    [InlineData("150")]
    [InlineData("-5")]
    public void QuerschnittProzentAusserhalb0Bis100_IstImplausibel(string querschnitt)
        => Assert.False(TrainingSamplePlausibility.IsFachlichPlausibel(
            Sample(querschnitt: querschnitt), out _));
}
