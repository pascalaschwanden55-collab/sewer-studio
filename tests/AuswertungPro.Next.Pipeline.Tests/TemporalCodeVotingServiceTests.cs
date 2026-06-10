using System;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class TemporalCodeVotingServiceTests
{
    [Fact]
    public void EinzelbildAusreisserWirdNichtBestaetigt()
    {
        var voting = new TemporalCodeVotingService(windowSize: 3, minAgreement: 2, meterRadius: 1.5);

        Assert.Null(voting.RegisterAndVote(null, 10.0));
        Assert.Null(voting.RegisterAndVote("BAB", 10.3));   // einzelner Kipper
        Assert.Null(voting.RegisterAndVote(null, 10.6));
    }

    [Fact]
    public void ZweiKonsistenteFramesBestaetigen()
    {
        var voting = new TemporalCodeVotingService(windowSize: 3, minAgreement: 2, meterRadius: 1.5);

        Assert.Null(voting.RegisterAndVote("BBA", 12.0));        // erster Treffer: noch offen
        Assert.Equal("BBA", voting.RegisterAndVote("BBA", 12.4)); // zweiter Treffer: bestaetigt
    }

    [Fact]
    public void TrefferAusserhalbMeterRadiusZaehlenNicht()
    {
        var voting = new TemporalCodeVotingService(windowSize: 3, minAgreement: 2, meterRadius: 1.5);

        Assert.Null(voting.RegisterAndVote("BAJ", 5.0));
        // gleicher Code, aber 4m weiter — andere Stelle, darf nicht mitstimmen
        Assert.Null(voting.RegisterAndVote("BAJ", 9.0));
    }

    [Fact]
    public void AlteEntscheidungenFallenAusDemFenster()
    {
        var voting = new TemporalCodeVotingService(windowSize: 3, minAgreement: 2, meterRadius: 1.5);

        Assert.Null(voting.RegisterAndVote("BAB", 10.0));
        Assert.Null(voting.RegisterAndVote(null, 10.3));
        Assert.Null(voting.RegisterAndVote(null, 10.6));
        // Der BAB von 10.0 ist jetzt aus dem 3er-Fenster verdraengt
        Assert.Null(voting.RegisterAndVote("BAB", 10.9));
    }

    [Fact]
    public void GrossKleinschreibungWirdNormalisiert()
    {
        var voting = new TemporalCodeVotingService(windowSize: 3, minAgreement: 2, meterRadius: 1.5);

        Assert.Null(voting.RegisterAndVote("bba", 3.0));
        Assert.Equal("BBA", voting.RegisterAndVote("BBA", 3.2));
    }

    [Fact]
    public void ResetLeertDasFenster()
    {
        var voting = new TemporalCodeVotingService(windowSize: 3, minAgreement: 2, meterRadius: 1.5);

        voting.RegisterAndVote("BBA", 1.0);
        voting.Reset();
        Assert.Null(voting.RegisterAndVote("BBA", 1.2)); // nur noch 1 Stimme im Fenster
    }

    [Fact]
    public void UngueltigeParameterWerfen()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TemporalCodeVotingService(windowSize: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TemporalCodeVotingService(windowSize: 3, minAgreement: 4));
    }
}
