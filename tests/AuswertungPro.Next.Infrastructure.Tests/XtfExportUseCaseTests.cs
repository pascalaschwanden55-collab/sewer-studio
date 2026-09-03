using AuswertungPro.Next.Application.UseCases.Xtf;
using AuswertungPro.Next.Application.Xtf;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Der Ablauf hinter beiden XTF-Knoepfen, ohne Oberflaeche: pruefen, bei fehlender Quelle
/// fragen, Vorschau bestaetigen lassen, erst dann schreiben. Vor der Bestaetigung wird
/// nie geschrieben; eine gescheiterte Pruefung fragt gar nicht erst nach Bestaetigung.
/// </summary>
public sealed class XtfExportUseCaseTests
{
    [Fact]
    public void Gescheiterte_Pruefung_zeigt_den_Fehler_und_fragt_nicht_nach_Bestaetigung()
    {
        var dienst = new RevisionFake { Pruefung = new XtfRevisionExportResult(false, "Bericht mit offen:", "q.xtf: offene Faelle — die Pruefung ist nicht bestanden.", []) };
        var aktionen = new AktionenFake { Bestaetigung = true };

        var ergebnis = XtfAktualisierenUseCase.Execute(dienst, Anfrage(), aktionen.Actions);

        Assert.False(ergebnis.Geschrieben);
        Assert.Null(aktionen.Vorschau);
        Assert.NotNull(aktionen.Fehler);
        Assert.True(aktionen.Fehler!.IstFehler);
        Assert.Equal("q.xtf: offene Faelle — die Pruefung ist nicht bestanden.", aktionen.Fehler.Zusammenfassung);
        Assert.Equal("Bericht mit offen:", aktionen.Fehler.Details);
        Assert.All(dienst.Requests, r => Assert.True(r.NurPruefen));
    }

    [Fact]
    public void Abgelehnte_Vorschau_schreibt_nichts()
    {
        var dienst = new RevisionFake { Pruefung = Gepruefte() };
        var aktionen = new AktionenFake { Bestaetigung = false };

        var ergebnis = XtfAktualisierenUseCase.Execute(dienst, Anfrage(), aktionen.Actions);

        Assert.False(ergebnis.Geschrieben);
        Assert.Equal("Abgebrochen — nichts geschrieben.", ergebnis.Meldung);
        Assert.NotNull(aktionen.Vorschau);
        Assert.Equal("1 Objekt geändert · 0 neu · 0 entfernt", aktionen.Vorschau!.Zusammenfassung);
        Assert.Single(dienst.Requests);
        Assert.True(dienst.Requests[0].NurPruefen);
    }

    [Fact]
    public void Bestaetigte_Vorschau_schreibt_und_nennt_den_Ordner()
    {
        var dienst = new RevisionFake
        {
            Pruefung = Gepruefte(),
            Schreiben = new XtfRevisionExportResult(true, "geschrieben", null, [@"C:\Ausgabe\XTF-Revision_1\q.xtf"])
        };
        var aktionen = new AktionenFake { Bestaetigung = true };

        var ergebnis = XtfAktualisierenUseCase.Execute(dienst, Anfrage(), aktionen.Actions);

        Assert.True(ergebnis.Geschrieben);
        Assert.Equal(@"C:\Ausgabe\XTF-Revision_1", ergebnis.Ordner);
        Assert.Equal("Katasterdaten aktualisiert: 1 Datei geschrieben.", ergebnis.Meldung);
        Assert.Equal(2, dienst.Requests.Count);
        Assert.False(dienst.Requests[1].NurPruefen);
    }

    [Fact]
    public void Fehlende_Projektquelle_wird_erfragt_und_fuer_Pruefung_und_Schreiben_verwendet()
    {
        var dienst = new RevisionFake
        {
            Pruefung = Gepruefte(),
            OhneQuelle = new XtfRevisionExportResult(false, "", "keine Quelle", [], QuelleFehlt: true),
            Schreiben = new XtfRevisionExportResult(true, "", null, [@"C:\A\XTF-Revision_1\q.xtf"])
        };
        var aktionen = new AktionenFake { Bestaetigung = true, Quellen = [@"C:\Extern\q.xtf"] };

        var ergebnis = XtfAktualisierenUseCase.Execute(dienst, Anfrage(), aktionen.Actions);

        Assert.True(ergebnis.Geschrieben);
        Assert.Equal(3, dienst.Requests.Count);
        Assert.Null(dienst.Requests[0].Quelldateien);
        Assert.Equal([@"C:\Extern\q.xtf"], dienst.Requests[1].Quelldateien);
        Assert.Equal([@"C:\Extern\q.xtf"], dienst.Requests[2].Quelldateien);
    }

    [Fact]
    public void Ohne_gewaehlte_Quelle_endet_der_Lauf_ohne_Schreiben()
    {
        var dienst = new RevisionFake { OhneQuelle = new XtfRevisionExportResult(false, "", "keine Quelle", [], QuelleFehlt: true) };
        var aktionen = new AktionenFake { Quellen = [] };

        var ergebnis = XtfAktualisierenUseCase.Execute(dienst, Anfrage(), aktionen.Actions);

        Assert.False(ergebnis.Geschrieben);
        Assert.Equal("Abgebrochen — keine Original-XTF gewählt.", ergebnis.Meldung);
        Assert.Single(dienst.Requests);
    }

    [Fact]
    public void Neuexport_zeigt_Bericht_als_Vorschau_und_schreibt_erst_nach_Bestaetigung()
    {
        var dienst = new NeuFake
        {
            Pruefung = new XtfNeuExportResult(true, "Projekt: Test\n\nIn die Datei: 1 Haltungen, 1 Schaechte", null, null),
            Schreiben = new XtfNeuExportResult(true, "ok", null, @"C:\A\Test export_1.xtf")
        };
        var aktionen = new AktionenFake { Bestaetigung = true };

        var ergebnis = XtfNeuErstellenUseCase.Execute(dienst, new XtfNeuExportRequest(new Project(), @"C:\A"), aktionen.Actions);

        Assert.True(ergebnis.Geschrieben);
        Assert.Equal(@"C:\A", ergebnis.Ordner);
        Assert.Equal("Neue XTF erstellt: Test export_1.xtf", ergebnis.Meldung);
        Assert.NotNull(aktionen.Vorschau);
        Assert.Empty(aktionen.Vorschau!.Zeilen);
        Assert.Equal("In die Datei: 1 Haltungen, 1 Schaechte", aktionen.Vorschau.Zusammenfassung);
        Assert.Equal(2, dienst.Requests.Count);
    }

    private static XtfAktualisierenRequest Anfrage()
        => new(new Project(), @"C:\P\Projektdateien\projekt.json", @"C:\Ausgabe");

    private static XtfRevisionExportResult Gepruefte()
        => new(true, "q.xtf: 1 geaendert, 0 neu, 0 entfernt, 3 unveraendert.", null, [],
            Plaene: [new XtfRevisionPlan("q.xtf",
                [new XtfRevisionPosition(XtfRevisionAenderung.Geaendert, "t", "", "78998-79002", "", null,
                    [new XtfRevisionFeld("Material", "Steinzeug", "Zement")], Objekt: "Haltung")], [])]);

    private sealed class AktionenFake
    {
        public bool Bestaetigung { get; init; }
        public IReadOnlyList<string> Quellen { get; init; } = [];
        public XtfExportVorschau? Vorschau { get; private set; }
        public XtfExportVorschau? Fehler { get; private set; }

        public XtfExportActions Actions => new(
            () => Quellen,
            v => { Vorschau = v; return Bestaetigung; },
            f => Fehler = f);
    }

    private sealed class RevisionFake : IXtfRevisionExportService
    {
        public XtfRevisionExportResult? Pruefung { get; init; }
        public XtfRevisionExportResult? OhneQuelle { get; init; }
        public XtfRevisionExportResult? Schreiben { get; init; }
        public List<XtfRevisionExportRequest> Requests { get; } = [];

        public IReadOnlyList<XtfProjektkopie> FindeProjektkopien(string? projektPfad) => [];

        public XtfRevisionExportResult Erzeuge(XtfRevisionExportRequest request)
        {
            Requests.Add(request);
            if (OhneQuelle is not null && request.Quelldateien is null or { Count: 0 })
                return OhneQuelle;
            if (request.NurPruefen)
                return Pruefung ?? throw new InvalidOperationException("keine Pruefung vorgesehen");
            return Schreiben ?? throw new InvalidOperationException("Schreiben war nicht vorgesehen");
        }
    }

    private sealed class NeuFake : IXtfNeuExportService
    {
        public XtfNeuExportResult? Pruefung { get; init; }
        public XtfNeuExportResult? Schreiben { get; init; }
        public List<XtfNeuExportRequest> Requests { get; } = [];

        public XtfNeuExportResult Erzeuge(XtfNeuExportRequest request)
        {
            Requests.Add(request);
            return request.NurPruefen
                ? Pruefung ?? throw new InvalidOperationException("keine Pruefung vorgesehen")
                : Schreiben ?? throw new InvalidOperationException("Schreiben war nicht vorgesehen");
        }
    }
}
