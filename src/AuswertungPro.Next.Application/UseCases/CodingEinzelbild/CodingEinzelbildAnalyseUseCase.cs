using AuswertungPro.Next.Application.Ai;

namespace AuswertungPro.Next.Application.UseCases.CodingEinzelbild;

/// <summary>Was die Anzeige während des Ablaufs erfahren soll. Text und Farbe wählt das UI.</summary>
public enum CodingEinzelbildMeldungsArt
{
    BildWirdAufgenommen,
    BildNichtExtrahierbar,
    DateneinblendungErkannt,
    ModelleLaufen,
    Modellfehler
}

public sealed record CodingEinzelbildMeldung(CodingEinzelbildMeldungsArt Art, string? Fehler = null);

public enum CodingEinzelbildAusgang
{
    /// <summary>Kein Mehrmodell-Dienst: nichts geschieht.</summary>
    KeinMehrmodell,

    /// <summary>Abbruchquelle fehlt, etwa nach dem Abbau beim Schliessen: nichts geschieht.</summary>
    KeineAbbruchquelle,
    KeinBild,
    BildNichtBereit,
    Modellfehler,
    GrenzeBehandelt,
    StrukturBehandelt,
    ErgebnisBehandelt
}

public sealed record CodingEinzelbildAnfrage<TModell>(
    TModell? Mehrmodell,
    CancellationTokenSource? Abbruchquelle,
    double AufnahmeSekunden)
    where TModell : class;

/// <summary>
/// Anschlüsse des Ablaufs. <typeparamref name="TAnalyse"/> ist das Ergebnis der Mehrmodell-Analyse
/// (im Player <c>SingleFrameResult</c>); der Ablauf liest davon nur den Fehlertext.
/// </summary>
public sealed record CodingEinzelbildSchritte<TModell, TAnalyse>(
    Action<CodingEinzelbildMeldung> Melden,
    // Bild
    Func<CancellationToken, Task<byte[]?>> BildAufnehmenAsync,
    Action<byte[], double> AnalysebildMerken,
    Func<byte[], double, CancellationToken, Task<double?>> OsdMeterLesenAsync,
    Action<LiveDetection> BildbereitschaftAktualisieren,
    Func<bool> IstBildBereit,
    // Sitzung und Position
    Func<bool> HatCodiersitzung,
    Func<double> EndmeterLesen,
    Func<int?> NennweiteLesen,
    Func<double?, double?, CodingMeterResolution> MeterAufloesen,
    // Auswertung
    Func<TModell, byte[], CodingMultiModelClassifierInput, CancellationToken, Task<TAnalyse>> AnalysierenAsync,
    Func<TAnalyse, string?> FehlerLesen,
    Func<TAnalyse, CodingAnalyzedFrameEvidence, Task<bool>> GrenzeBehandelnAsync,
    Func<TAnalyse, CodingAnalyzedFrameEvidence, bool> StrukturBehandeln,
    Action<TAnalyse, CodingAnalyzedFrameEvidence> ErgebnisBehandeln)
    where TModell : class;

/// <summary>Auswertung eines bereits aufgenommenen Bildes; die Behandler kennen ihren Beleg selbst.</summary>
public sealed record CodingEinzelbildAuswertung<TAnalyse>(
    Func<byte[], CodingMultiModelClassifierInput, CancellationToken, Task<TAnalyse>> AnalysierenAsync,
    Func<TAnalyse, string?> FehlerLesen,
    Action<CodingEinzelbildMeldung> Melden,
    Func<TAnalyse, Task<bool>> GrenzeBehandelnAsync,
    Func<TAnalyse, bool> StrukturBehandeln,
    Action<TAnalyse> ErgebnisBehandeln);

/// <summary>Fachliches Ergebnis: wie der Ablauf endete, mit welchem Beleg und welcher Analyse.</summary>
public sealed record CodingEinzelbildErgebnis<TAnalyse>(
    CodingEinzelbildAusgang Ausgang,
    CodingAnalyzedFrameEvidence? Beleg,
    TAnalyse? Analyse);

/// <summary>
/// Codiermodus: ein gebundenes Einzelbild analysieren. Der Ablauf nimmt ein Bild auf, bindet Bild,
/// Aufnahmezeit und einmal aufgelösten Meter zu einem Beleg und verteilt das Analyseergebnis an
/// Grenze (BCD/BCE), Struktur (BCA/BCC) oder die allgemeine Ergebnisbehandlung. Fenster, Farben und
/// Dispatcher bleiben ausserhalb; Seite: docs/architektur/player-einzelbild-ablauf.md.
/// Bewusst ohne <c>ConfigureAwait(false)</c>: Die Anschlüsse des Players greifen nach jedem Warten
/// auf die Oberfläche zu und müssen im aufrufenden Kontext weiterlaufen.
/// </summary>
public static class CodingEinzelbildAnalyseUseCase
{
    public static async Task<CodingEinzelbildErgebnis<TAnalyse>> ExecuteAsync<TModell, TAnalyse>(
        CodingEinzelbildAnfrage<TModell> anfrage,
        CodingEinzelbildSchritte<TModell, TAnalyse> schritte)
        where TModell : class
    {
        ArgumentNullException.ThrowIfNull(anfrage);
        ArgumentNullException.ThrowIfNull(schritte);

        // 1. Laufzeit: ohne Dienst oder nach dem Abbau startet nichts. Das Token wird vor dem
        //    ersten Warten gelesen; eine später freigegebene Quelle bricht den Lauf ab.
        if (anfrage.Mehrmodell is not { } mehrmodell)
            return Ende<TAnalyse>(CodingEinzelbildAusgang.KeinMehrmodell);
        if (anfrage.Abbruchquelle is null)
            return Ende<TAnalyse>(CodingEinzelbildAusgang.KeineAbbruchquelle);
        var token = anfrage.Abbruchquelle.Token;
        var sekunden = anfrage.AufnahmeSekunden;

        // 2. Bild aufnehmen und sofort für Goldsample und Rohrende merken, bevor ein Ereignis entsteht.
        schritte.Melden(new(CodingEinzelbildMeldungsArt.BildWirdAufgenommen));
        var bild = await schritte.BildAufnehmenAsync(token);
        if (bild is null || bild.Length == 0)
        {
            schritte.Melden(new(CodingEinzelbildMeldungsArt.BildNichtExtrahierbar));
            return Ende<TAnalyse>(CodingEinzelbildAusgang.KeinBild);
        }

        schritte.AnalysebildMerken(bild, sekunden);
        var osdMeter = await schritte.OsdMeterLesenAsync(bild, sekunden, token);

        // 3. Bilder mit Dateneinblendung (OSD-Vorspann) werden nicht analysiert.
        schritte.BildbereitschaftAktualisieren(new LiveDetection(sekunden, Array.Empty<LiveFrameFinding>(), osdMeter, null));
        if (!schritte.IstBildBereit())
        {
            schritte.Melden(new(CodingEinzelbildMeldungsArt.DateneinblendungErkannt));
            return Ende<TAnalyse>(CodingEinzelbildAusgang.BildNichtBereit);
        }

        schritte.Melden(new(CodingEinzelbildMeldungsArt.ModelleLaufen));

        // 4. Aufnahmebindung: Der Meter wird genau einmal vor der Inferenz aufgelöst. Grenze,
        //    Struktur und Befunde erhalten denselben Beleg; nach der Inferenz wird nichts neu gelesen.
        double? endmeter = schritte.HatCodiersitzung() ? schritte.EndmeterLesen() : null;
        var nennweite = schritte.NennweiteLesen();
        var beleg = CodingAnalyzedFrameEvidence.FromResolution(
            bild,
            TimeSpan.FromSeconds(sekunden),
            schritte.MeterAufloesen(sekunden, osdMeter));
        var eingabe = CodingMultiModelClassifierInputPolicy.Build(nennweite, beleg.Meter, endmeter);

        // 5. Analysieren und verteilen.
        var ergebnis = await AuswertenAsync(
            bild,
            eingabe,
            token,
            new CodingEinzelbildAuswertung<TAnalyse>(
                (analyseBild, analyseEingabe, analyseToken) => schritte.AnalysierenAsync(mehrmodell, analyseBild, analyseEingabe, analyseToken),
                schritte.FehlerLesen,
                schritte.Melden,
                analyse => schritte.GrenzeBehandelnAsync(analyse, beleg),
                analyse => schritte.StrukturBehandeln(analyse, beleg),
                analyse => schritte.ErgebnisBehandeln(analyse, beleg)));
        return ergebnis with { Beleg = beleg };
    }

    /// <summary>
    /// Analysiert ein Bild und verteilt das Ergebnis: Modellfehler zuerst, dann Grenze vor Struktur
    /// vor allgemeiner Ergebnisbehandlung. Auch der CodingReplay-Messhost nutzt diese Reihenfolge.
    /// </summary>
    public static async Task<CodingEinzelbildErgebnis<TAnalyse>> AuswertenAsync<TAnalyse>(
        byte[] bild,
        CodingMultiModelClassifierInput eingabe,
        CancellationToken token,
        CodingEinzelbildAuswertung<TAnalyse> schritte)
    {
        ArgumentNullException.ThrowIfNull(bild);
        ArgumentNullException.ThrowIfNull(eingabe);
        ArgumentNullException.ThrowIfNull(schritte);

        var analyse = await schritte.AnalysierenAsync(bild, eingabe, token);

        if (schritte.FehlerLesen(analyse) is { } fehler)
        {
            schritte.Melden(new(CodingEinzelbildMeldungsArt.Modellfehler, fehler));
            return new(CodingEinzelbildAusgang.Modellfehler, null, analyse);
        }

        if (await schritte.GrenzeBehandelnAsync(analyse))
            return new(CodingEinzelbildAusgang.GrenzeBehandelt, null, analyse);

        if (schritte.StrukturBehandeln(analyse))
            return new(CodingEinzelbildAusgang.StrukturBehandelt, null, analyse);

        schritte.ErgebnisBehandeln(analyse);
        return new(CodingEinzelbildAusgang.ErgebnisBehandelt, null, analyse);
    }

    private static CodingEinzelbildErgebnis<TAnalyse> Ende<TAnalyse>(CodingEinzelbildAusgang ausgang)
        => new(ausgang, null, default);
}
