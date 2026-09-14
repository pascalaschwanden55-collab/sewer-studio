using AuswertungPro.Next.Application.UseCases.Xtf;
using AuswertungPro.Next.Application.Xtf;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Der Ablauf hinter «Paket für GEONIS erstellen»: beide Fassungen pruefen, einmal bestaetigen
/// lassen, beide schreiben. Ein halbes Paket darf nie liegen bleiben — der Empfaenger wuerde es
/// sonst versehentlich verschicken.
/// </summary>
public sealed class XtfKatasterPaketUseCaseTests
{
    private sealed class FakeDienst : IXtfNeuExportService
    {
        public List<XtfNeuExportRequest> Aufrufe { get; } = [];
        public Func<XtfNeuExportRequest, XtfNeuExportResult>? Antwort { get; set; }
        public XtfNeuExportResult Erzeuge(XtfNeuExportRequest request)
        {
            Aufrufe.Add(request);
            return Antwort?.Invoke(request)
                ?? new XtfNeuExportResult(true, "In die Datei: 1 Haltungen, 1 Schaechte (9 Objekte insgesamt).", null,
                    Path.Combine(request.ZielOrdner, request.NurAenderungen ? "P_Aenderungen.xtf" : "P.xtf"),
                    Aenderungen: request.NurAenderungen ? [new XtfAenderungsZeile("Normschacht B", "Bemerkung", "alt", "neu")] : null);
        }
    }

    private sealed class FakeAblage : IXtfPaketAblage
    {
        public XtfPaketOrt? Begonnen { get; private set; }
        public bool Verworfen { get; private set; }
        public bool AllesEntfernt { get; set; } = true;
        public string? Liesmich { get; private set; }
        public XtfPaketOrt Beginne(string zielordner, string projektname)
            => Begonnen = new(Path.Combine(zielordner, "Paket"), Path.Combine(zielordner, "Paket", "1"), Path.Combine(zielordner, "Paket", "2"));
        public XtfPaketInhalte? Inhalte { get; private set; }
        public string Schliesse(XtfPaketOrt ort, XtfPaketInhalte inhalte)
        {
            if (SchliesseWirft is not null) throw new IOException(SchliesseWirft());
            Inhalte = inhalte; Liesmich = inhalte.Liesmich; return ort.Paketordner + ".zip";
        }
        public Func<string>? SchliesseWirft { get; set; }
        public bool Verwirf(XtfPaketOrt ort) { Verworfen = true; return AllesEntfernt; }
    }

    private static XtfExportActions Aktionen(Func<XtfExportVorschau, bool> bestaetige, List<XtfExportVorschau>? fehler = null)
        => new(() => [], bestaetige, v => fehler?.Add(v));

    private static XtfNeuExportRequest Anfrage() => new(new Project { Name = "P" }, "Z:\\ziel");

    [Fact]
    public void Erfolgsfall_schreibt_beide_Fassungen_und_packt_das_Paket()
    {
        var dienst = new FakeDienst(); var ablage = new FakeAblage();
        var vorschauen = new List<XtfExportVorschau>();

        var r = XtfKatasterPaketUseCase.Execute(dienst, ablage, Anfrage(),
            Aktionen(v => { vorschauen.Add(v); return true; }));

        Assert.True(r.Geschrieben);
        Assert.Contains(".zip", r.Meldung, StringComparison.Ordinal);
        // Zweimal pruefen, zweimal schreiben.
        Assert.Equal(4, dienst.Aufrufe.Count);
        Assert.Equal(2, dienst.Aufrufe.Count(a => a.NurPruefen));
        var geschrieben = dienst.Aufrufe.Where(a => !a.NurPruefen).ToArray();
        var aenderung = Assert.Single(geschrieben, a => a.NurAenderungen);
        var voll = Assert.Single(geschrieben, a => !a.NurAenderungen);
        Assert.True(aenderung.MitZusatzangaben);
        Assert.False(voll.MitZusatzangaben); // reine Normdatei ohne unser Modell
        Assert.NotEqual(aenderung.ZielOrdner, voll.ZielOrdner);
        Assert.False(ablage.Verworfen);
        Assert.Contains("P", ablage.Liesmich!, StringComparison.Ordinal);
        // Die eine Vorschau zeigt die Feldauftraege und nennt beide Fassungen.
        var vorschau = Assert.Single(vorschauen);
        Assert.True(vorschau.HatZeilen);
        Assert.Contains("zwei Fassungen", vorschau.Zusammenfassung, StringComparison.Ordinal);
    }

    [Fact]
    public void Abgelehnte_Vorschau_legt_nichts_an()
    {
        var dienst = new FakeDienst(); var ablage = new FakeAblage();
        var r = XtfKatasterPaketUseCase.Execute(dienst, ablage, Anfrage(), Aktionen(_ => false));

        Assert.False(r.Geschrieben);
        Assert.Null(ablage.Begonnen);
        Assert.DoesNotContain(dienst.Aufrufe, a => !a.NurPruefen);
    }

    [Fact]
    public void Scheiternde_Pruefung_der_reinen_Normdatei_schreibt_gar_nichts()
    {
        var dienst = new FakeDienst
        {
            Antwort = a => a.NurAenderungen
                ? new(true, "In die Datei: 1 Haltungen, 1 Schaechte (9 Objekte insgesamt).", null, null)
                : new(false, "Bericht", "Pflichtangabe fehlt", null)
        };
        var ablage = new FakeAblage(); var fehler = new List<XtfExportVorschau>();

        var r = XtfKatasterPaketUseCase.Execute(dienst, ablage, Anfrage(), Aktionen(_ => true, fehler));

        Assert.False(r.Geschrieben);
        Assert.Null(ablage.Begonnen);
        Assert.Contains("Pflichtangabe fehlt", Assert.Single(fehler).Zusammenfassung, StringComparison.Ordinal);
    }

    [Fact]
    public void Scheiterndes_Schreiben_verwirft_das_angefangene_Paket()
    {
        var dienst = new FakeDienst
        {
            Antwort = a => a.NurPruefen
                ? new(true, "In die Datei: 1 Haltungen, 1 Schaechte (9 Objekte insgesamt).", null, null)
                : a.NurAenderungen
                    ? new(true, "Bericht", null, Path.Combine(a.ZielOrdner, "P_Aenderungen.xtf"))
                    : new(false, "Bericht", "Platte voll", null)
        };
        var ablage = new FakeAblage(); var fehler = new List<XtfExportVorschau>();

        var r = XtfKatasterPaketUseCase.Execute(dienst, ablage, Anfrage(), Aktionen(_ => true, fehler));

        Assert.False(r.Geschrieben);
        Assert.NotNull(ablage.Begonnen);
        Assert.True(ablage.Verworfen);
        Assert.Null(ablage.Liesmich);
        Assert.Contains("Platte voll", Assert.Single(fehler).Zusammenfassung, StringComparison.Ordinal);
    }

    [Fact]
    public void Begleitdateien_entstehen_vor_dem_Packen_und_liegen_damit_in_der_Zip()
    {
        var dienst = new FakeDienst(); var ablage = new FakeAblage();
        string? ordnerBeimSchreiben = null;
        var gepacktAls = false;
        var aktionen = new XtfExportActions(() => [], _ => true, _ => { },
            ordner => { ordnerBeimSchreiben = ordner; gepacktAls = ablage.Inhalte is not null; return "\nObjektakten: x.json"; });

        var r = XtfKatasterPaketUseCase.Execute(dienst, ablage, Anfrage(), aktionen);

        Assert.True(r.Geschrieben);
        Assert.Equal(ablage.Begonnen!.Paketordner, ordnerBeimSchreiben);
        Assert.False(gepacktAls); // vor dem Packen
        Assert.Contains("Objektakten: x.json", r.Meldung, StringComparison.Ordinal);
    }

    [Fact]
    public void Gescheiterte_Begleitdatei_verwirft_das_Paket()
    {
        var dienst = new FakeDienst(); var ablage = new FakeAblage(); var fehler = new List<XtfExportVorschau>();
        var aktionen = new XtfExportActions(() => [], _ => true, fehler.Add,
            _ => throw new IOException("Zusatzdatei nicht schreibbar"));

        var r = XtfKatasterPaketUseCase.Execute(dienst, ablage, Anfrage(), aktionen);

        Assert.False(r.Geschrieben);
        Assert.True(ablage.Verworfen);
        Assert.Contains("Begleitdateien konnten nicht geschrieben werden", Assert.Single(fehler).Zusammenfassung, StringComparison.Ordinal);
    }

    [Fact]
    public void Gescheitertes_Packen_verwirft_das_Paket_und_meldet_es()
    {
        var dienst = new FakeDienst(); var fehler = new List<XtfExportVorschau>();
        var ablage = new FakeAblage { SchliesseWirft = () => "Zip nicht schreibbar" };

        var r = XtfKatasterPaketUseCase.Execute(dienst, ablage, Anfrage(), Aktionen(_ => true, fehler));

        Assert.False(r.Geschrieben);
        Assert.True(ablage.Verworfen);
        Assert.Contains("Paket konnte nicht gepackt werden", Assert.Single(fehler).Zusammenfassung, StringComparison.Ordinal);
    }

    [Fact]
    public void Ein_Rest_der_nicht_entfernt_werden_konnte_wird_benannt()
    {
        var dienst = new FakeDienst(); var fehler = new List<XtfExportVorschau>();
        var ablage = new FakeAblage { SchliesseWirft = () => "Zip nicht schreibbar", AllesEntfernt = false };

        XtfKatasterPaketUseCase.Execute(dienst, ablage, Anfrage(), Aktionen(_ => true, fehler));

        var gezeigt = Assert.Single(fehler).Zusammenfassung;
        Assert.Contains("von Hand löschen", gezeigt, StringComparison.Ordinal);
        Assert.Contains("unvollständig", gezeigt, StringComparison.Ordinal);
    }

    [Fact]
    public void Fehlende_Originalquelle_wird_einmal_erfragt_und_dann_beiden_Laeufen_mitgegeben()
    {
        var dienst = new FakeDienst();
        var erster = true;
        dienst.Antwort = a =>
        {
            if (erster) { erster = false; return new(false, "", "Quelle fehlt", null, QuelleFehlt: true); }
            return new(true, "In die Datei: 1 Haltungen, 1 Schaechte (9 Objekte insgesamt).", null,
                Path.Combine(a.ZielOrdner, "P.xtf"));
        };
        var ablage = new FakeAblage();

        var r = XtfKatasterPaketUseCase.Execute(dienst, ablage, Anfrage(),
            new XtfExportActions(() => ["Q:\\original.xtf"], _ => true, _ => { }));

        Assert.True(r.Geschrieben);
        Assert.All(dienst.Aufrufe.Skip(1), a => Assert.Equal(["Q:\\original.xtf"], a.Quelldateien));
    }
}
