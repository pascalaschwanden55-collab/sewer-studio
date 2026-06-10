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

    // ── Hysterese (Pilot 2026-06-10: 6x BAJ am selben Meter durch Flattern) ──

    [Fact]
    public void HystereseHaeltCodeBeiStehenderKameraUeberKipperHinweg()
    {
        var voting = new TemporalCodeVotingService(windowSize: 3, minAgreement: 2, meterRadius: 1.5);

        voting.RegisterAndVote("BAJ", 0.42);
        Assert.Equal("BAJ", voting.RegisterAndVote("BAJ", 0.43));   // bestaetigt
        // Kamera steht, einzelne Frames kippen weg — Code muss aktiv bleiben
        Assert.Equal("BAJ", voting.RegisterAndVote(null, 0.43));
        Assert.Equal("BAJ", voting.RegisterAndVote("BAJ", 0.43));
        Assert.Equal("BAJ", voting.RegisterAndVote(null, 0.43));
        Assert.Equal("BAJ", voting.RegisterAndVote(null, 0.43));    // BAJ noch im 3er-Fenster
    }

    [Fact]
    public void HystereseFaelltWennFensterDenCodeVerliert()
    {
        var voting = new TemporalCodeVotingService(windowSize: 3, minAgreement: 2, meterRadius: 1.5);

        voting.RegisterAndVote("BAJ", 0.42);
        voting.RegisterAndVote("BAJ", 0.43);
        voting.RegisterAndVote(null, 0.43);
        voting.RegisterAndVote(null, 0.43);
        // Fenster = [BAJ, null, null] -> letzter BAJ faellt jetzt raus
        Assert.Null(voting.RegisterAndVote(null, 0.43));
    }

    [Fact]
    public void HystereseFaelltWennKameraWeiterfaehrt()
    {
        var voting = new TemporalCodeVotingService(windowSize: 3, minAgreement: 2, meterRadius: 1.5);

        voting.RegisterAndVote("BAJ", 0.42);
        Assert.Equal("BAJ", voting.RegisterAndVote("BAJ", 0.43));
        // 3m weiter: andere Stelle, kein Festhalten
        Assert.Null(voting.RegisterAndVote(null, 3.50));
    }

    [Fact]
    public void NeuerCodeVerdraengtAltenErstMitEigenerMehrheit()
    {
        var voting = new TemporalCodeVotingService(windowSize: 3, minAgreement: 2, meterRadius: 1.5);

        voting.RegisterAndVote("BAJ", 0.42);
        Assert.Equal("BAJ", voting.RegisterAndVote("BAJ", 0.43));
        // Erster BCC-Frame: noch keine Mehrheit -> BAJ haelt
        Assert.Equal("BAJ", voting.RegisterAndVote("BCC", 0.45));
        // Zweiter BCC-Frame: Mehrheit -> Wechsel
        Assert.Equal("BCC", voting.RegisterAndVote("BCC", 0.47));
    }

    [Fact]
    public void ResetLoeschtAuchDieHysterese()
    {
        var voting = new TemporalCodeVotingService(windowSize: 3, minAgreement: 2, meterRadius: 1.5);

        voting.RegisterAndVote("BAJ", 0.42);
        voting.RegisterAndVote("BAJ", 0.43);
        voting.Reset();
        Assert.Null(voting.RegisterAndVote(null, 0.43));
    }
}
