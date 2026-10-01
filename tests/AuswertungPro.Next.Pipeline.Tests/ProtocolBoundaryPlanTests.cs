using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Rohranfang/Rohrende beim "Uebernehmen" im Codiermodus: Der Rohranfang bei 0.00 m
/// wird still ergaenzt, das Rohrende nur nach Rueckfrage. Ein automatisch gesetztes
/// Rohrende wuerde sonst behaupten, die ganze Haltung sei befahren worden.
/// </summary>
public sealed class ProtocolBoundaryPlanTests
{
    [Fact]
    public void Plan_meldet_beide_Grenzen_als_fehlend()
    {
        var plan = ProtocolBoundaryService.PlanBoundaries([Entry("BABBB", 4.82)], 20.31);

        Assert.True(plan.PipeStartMissing);
        Assert.True(plan.PipeEndMissing);
        Assert.Equal(20.31, plan.PipeEndProposalMeter);
    }

    [Fact]
    public void Plan_meldet_nichts_wenn_beide_Grenzen_vorhanden_sind()
    {
        var plan = ProtocolBoundaryService.PlanBoundaries(
            [Entry("BCD", 0.0), Entry("BABBB", 4.82), Entry("BCE", 20.31)],
            20.31);

        Assert.False(plan.PipeStartMissing);
        Assert.False(plan.PipeEndMissing);
    }

    [Fact]
    public void Ein_Abbruch_BDC_zaehlt_als_Endpunkt()
    {
        var plan = ProtocolBoundaryService.PlanBoundaries(
            [Entry("BCD", 0.0), Entry("BDCC", 9.10)],
            20.31);

        Assert.False(plan.PipeEndMissing);
    }

    [Fact]
    public void Geloeschte_Eintraege_zaehlen_nicht_als_Grenze()
    {
        var deleted = Entry("BCD", 0.0);
        deleted.IsDeleted = true;

        var plan = ProtocolBoundaryService.PlanBoundaries([deleted, Entry("BABBB", 4.82)], 20.31);

        Assert.True(plan.PipeStartMissing);
    }

    [Fact]
    public void Ohne_bekannte_Haltungslaenge_gibt_es_keinen_Vorschlagsmeter()
    {
        var plan = ProtocolBoundaryService.PlanBoundaries([Entry("BABBB", 4.82)], null);

        Assert.True(plan.PipeEndMissing);
        Assert.Null(plan.PipeEndProposalMeter);
    }

    /// <summary>
    /// Fehlt in den Stammdaten jede Laenge, traegt der Codiermodus beim Einstieg
    /// ersatzweise den hoechsten Protokollmeter als "Haltungslaenge_m" nach. Fehlt
    /// zugleich das Rohrende, ist dieser hoechste Meter genau der letzte Befund.
    /// Ein daraus gebautes Rohrende saesse auf dem Schaden - und wuerde dem
    /// Benutzer als "Haltungslaenge" angeboten. Lieber kein Vorschlag als ein
    /// erfundener: Der Benutzer setzt das Rohrende dann von Hand.
    /// </summary>
    [Fact]
    public void Eine_Laenge_auf_dem_letzten_Befund_wird_nicht_als_Rohrende_vorgeschlagen()
    {
        var plan = ProtocolBoundaryService.PlanBoundaries(
            [Entry("BCD", 0.0), Entry("BABBB", 4.82), Entry("BAFAA", 9.88)],
            9.88);

        Assert.True(plan.PipeEndMissing);
        Assert.Null(plan.PipeEndProposalMeter);
    }

    /// <summary>
    /// Codiert der Benutzer Schaeden hinter dem alten Maximum, liegt die
    /// nachgetragene Laenge sogar VOR dem letzten Befund. Auch das ist kein
    /// Rohrende.
    /// </summary>
    [Fact]
    public void Eine_Laenge_vor_dem_letzten_Befund_wird_nicht_vorgeschlagen()
    {
        var plan = ProtocolBoundaryService.PlanBoundaries(
            [Entry("BCD", 0.0), Entry("BABBB", 12.40)],
            9.88);

        Assert.True(plan.PipeEndMissing);
        Assert.Null(plan.PipeEndProposalMeter);
    }

    /// <summary>
    /// Der Plan muss sagen, WARUM er nichts vorschlaegt. "Keine Laenge bekannt" und
    /// "Laenge taugt nicht als Rohrende" sind verschiedene Lagen und brauchen
    /// verschiedene Saetze im Dialog. Ohne diese Unterscheidung behauptet das
    /// Programm, die Haltungslaenge sei unbekannt, obwohl sie in den Stammdaten
    /// steht - im Codiermodus sogar immer, denn ohne Laenge startet er nicht.
    /// </summary>
    [Fact]
    public void Eine_verworfene_Laenge_wird_mit_ihrem_Grund_gemeldet()
    {
        var plan = ProtocolBoundaryService.PlanBoundaries(
            [Entry("BCD", 0.0), Entry("BAFAA", 20.31)],
            20.31);

        Assert.Null(plan.PipeEndProposalMeter);
        Assert.Equal(20.31, plan.RejectedLengthM);
        Assert.Equal(20.31, plan.LastObservationM);
    }

    /// <summary>
    /// Mit ZWEI verschiedenen Zahlen, sonst beweist der Test nichts: Wuerden beide
    /// Felder aus derselben Quelle gefuellt, faellt das bei gleichen Werten nicht auf.
    /// Hier liegt die Laenge VOR dem letzten Befund - der Streckenschaden reicht
    /// weiter als die Haltung angeblich lang ist.
    /// </summary>
    [Fact]
    public void Verworfene_Laenge_und_letzte_Beobachtung_sind_zwei_verschiedene_Werte()
    {
        var strecke = Entry("BAFAA", 5.00);
        strecke.MeterEnd = 18.50;
        strecke.IsStreckenschaden = true;

        var plan = ProtocolBoundaryService.PlanBoundaries([Entry("BCD", 0.0), strecke], 12.00);

        Assert.Null(plan.PipeEndProposalMeter);
        Assert.Equal(12.00, plan.RejectedLengthM);
        Assert.Equal(18.50, plan.LastObservationM);
    }

    /// <summary>Ist das Rohrende vorhanden, gibt es nichts zu verwerfen.</summary>
    [Fact]
    public void Bei_vorhandenem_Rohrende_wird_nichts_verworfen_gemeldet()
    {
        var plan = ProtocolBoundaryService.PlanBoundaries(
            [Entry("BCD", 0.0), Entry("BAFAA", 20.31), Entry("BCE", 20.31)],
            20.31);

        Assert.False(plan.PipeEndMissing);
        Assert.Null(plan.RejectedLengthM);
    }

    [Fact]
    public void Ohne_jede_Laenge_gibt_es_auch_keinen_verworfenen_Wert()
    {
        var plan = ProtocolBoundaryService.PlanBoundaries([Entry("BABBB", 4.82)], null);

        Assert.Null(plan.PipeEndProposalMeter);
        Assert.Null(plan.RejectedLengthM);
    }

    [Fact]
    public void Ein_gueltiger_Vorschlag_meldet_keinen_verworfenen_Wert()
    {
        var plan = ProtocolBoundaryService.PlanBoundaries([Entry("BABBB", 4.82)], 20.31);

        Assert.Equal(20.31, plan.PipeEndProposalMeter);
        Assert.Null(plan.RejectedLengthM);
    }

    /// <summary>Eine echte Stammdatenlaenge liegt hinter allen Beobachtungen.</summary>
    [Fact]
    public void Eine_Laenge_hinter_dem_letzten_Befund_bleibt_ein_gueltiger_Vorschlag()
    {
        var plan = ProtocolBoundaryService.PlanBoundaries(
            [Entry("BCD", 0.0), Entry("BABBB", 4.82), Entry("BAFAA", 9.88)],
            20.31);

        Assert.True(plan.PipeEndMissing);
        Assert.Equal(20.31, plan.PipeEndProposalMeter);
    }

    /// <summary>
    /// Auch das Ende eines Streckenschadens zaehlt als Beobachtung. Sonst waere
    /// eine Laenge, die genau auf dem Ende der letzten Strecke sitzt, weiterhin
    /// ein Vorschlag.
    /// </summary>
    [Fact]
    public void Das_Ende_eines_Streckenschadens_zaehlt_als_letzte_Beobachtung()
    {
        var strecke = Entry("BAFAA", 4.82);
        strecke.MeterEnd = 9.88;
        strecke.IsStreckenschaden = true;

        var plan = ProtocolBoundaryService.PlanBoundaries(
            [Entry("BCD", 0.0), strecke],
            9.88);

        Assert.Null(plan.PipeEndProposalMeter);
    }

    /// <summary>
    /// <see cref="ProtocolBoundaryService.EnsureBoundaries"/> ist die aeltere,
    /// still ergaenzende Fassung derselben Regel. Sie ist heute ueber keinen
    /// Bedienweg erreichbar (ihre einzigen Aufrufer haengen an
    /// CodingSessionService.CompleteSession, dessen Command an kein XAML
    /// gebunden ist). Genau deshalb muss sie dieselbe Grenze ziehen: Wird sie
    /// spaeter einmal verdrahtet, darf sie kein Rohrende auf dem letzten Befund
    /// erfinden. Zwei Fassungen derselben Regel laufen sonst auseinander.
    /// </summary>
    [Fact]
    public void EnsureBoundaries_erfindet_kein_Rohrende_auf_dem_letzten_Befund()
    {
        var entries = new List<ProtocolEntry> { Entry("BABBB", 4.82), Entry("BAFAA", 9.88) };

        var result = ProtocolBoundaryService.EnsureBoundaries(entries, 9.88);

        Assert.False(result.EndInserted);
        Assert.Null(result.EndEntry);
        Assert.DoesNotContain(entries, e => e.Code == "BCE");
        // Der Rohranfang bleibt unberuehrt - 0.00 m stimmt immer.
        Assert.True(result.RohranfangInserted);
    }

    [Fact]
    public void EnsureBoundaries_setzt_ein_Rohrende_hinter_der_letzten_Beobachtung()
    {
        var entries = new List<ProtocolEntry> { Entry("BABBB", 4.82) };

        var result = ProtocolBoundaryService.EnsureBoundaries(entries, 20.31);

        Assert.True(result.EndInserted);
        Assert.Equal(20.31, result.EndEntry!.MeterStart);
    }

    [Fact]
    public void Rohranfang_wird_vorne_bei_0_eingefuegt()
    {
        var entries = new List<ProtocolEntry> { Entry("BABBB", 4.82) };

        var inserted = ProtocolBoundaryService.InsertPipeStart(entries);

        Assert.Equal("BCD", inserted.Code);
        Assert.Equal(0.0, inserted.MeterStart);
        Assert.Equal(["BCD", "BABBB"], entries.Select(e => e.Code));
    }

    [Fact]
    public void Ein_selbst_gesetzter_Rohranfang_wird_nicht_auf_0_verschoben()
    {
        var entries = new List<ProtocolEntry> { Entry("BCD", 0.35) };

        var plan = ProtocolBoundaryService.PlanBoundaries(entries, 20.31);

        Assert.False(plan.PipeStartMissing);
        Assert.Equal(0.35, entries[0].MeterStart);
    }

    [Fact]
    public void Rohrende_wird_hinten_angefuegt()
    {
        var entries = new List<ProtocolEntry> { Entry("BCD", 0.0), Entry("BABBB", 4.82) };

        var appended = ProtocolBoundaryService.AppendPipeEnd(entries, 20.31);

        Assert.Equal("BCE", appended.Code);
        Assert.Equal(20.31, appended.MeterStart);
        Assert.Equal(["BCD", "BABBB", "BCE"], entries.Select(e => e.Code));
    }

    private static ProtocolEntry Entry(string code, double meter)
        => new() { EntryId = Guid.NewGuid(), Code = code, MeterStart = meter };
}
