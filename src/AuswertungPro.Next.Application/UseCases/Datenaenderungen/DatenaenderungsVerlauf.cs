using System.Collections.Specialized;
using AuswertungPro.Next.Application.DataPage;
using AuswertungPro.Next.Application.UseCases.Objektakten;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.UseCases.Datenaenderungen;

/// <summary>
/// Rueckgaengig/Wiederholen fuer Haltungs- und Schachtdaten (Optik Aufgabe 16). Regeln siehe
/// <see cref="IDatenaenderungsVerlauf"/>. Reine Logik ohne WPF; Aufrufer ist der UI-Thread, nur
/// <see cref="Leere"/> kann auch aus einem Hintergrund-Import kommen (Listenaenderung) und ist
/// deshalb gesperrt.
/// </summary>
public sealed class DatenaenderungsVerlauf : IDatenaenderungsVerlauf
{
    /// <summary>Hoechstens so viele Schritte je Bereich; der aelteste faellt weg.</summary>
    public const int Tiefe = 100;

    // Gruende fuer das Leeren; sie erscheinen im Hinweis «Rückgängig ist nicht mehr möglich: …».
    public const string GrundProjekt = "anderes Projekt geöffnet";
    public const string GrundListe = "Datensätze hinzugefügt, gelöscht oder verschoben";
    public const string GrundUebernahme = "Daten aus GeoShop, QGIS oder dem WebGIS übernommen";
    public const string GrundVorgang = "ein Import, eine Übertragung oder ein Projektwechsel läuft";
    public const string GrundPaket = "Objektakten aus einer Zusatzdatei übernommen";
    public const string GrundFehler = "ein Schritt liess sich nicht vollständig anwenden";

    /// <summary>
    /// Die Felder, die ein Tabellenzellen-Schritt umfasst: das bearbeitete Feld und was genau diese
    /// Eingabe selbst ableitet (Sanieren Ja/Nein zieht die Kosten-/Mengenfelder nach). Schreibt ein
    /// anderer Weg waehrend der offenen Zelle ein anderes Feld desselben Datensatzes, gehoert das nicht
    /// zum Schritt (Fix-Runde 1).
    /// </summary>
    public static IReadOnlyCollection<string> ZellSchrittFelder(string feld)
        => string.Equals(feld, FieldKeys.RenovationDecision, StringComparison.Ordinal)
            ? [feld, .. SanierungCostFieldMapper.CostFieldNames]
            : [feld];

    private readonly object _gate = new();
    private readonly Dictionary<DatenaenderungsBereich, List<VerlaufEintrag>> _rueck = new()
    {
        [DatenaenderungsBereich.Haltungen] = [], [DatenaenderungsBereich.Schaechte] = [],
    };
    private readonly Dictionary<DatenaenderungsBereich, List<VerlaufEintrag>> _vor = new()
    {
        [DatenaenderungsBereich.Haltungen] = [], [DatenaenderungsBereich.Schaechte] = [],
    };
    private Project? _projekt;
    private Erfassung? _offen;
    private bool _wendetAn;

    public event EventHandler? Geaendert;
    public event EventHandler<DatenaenderungsVerlaufGeleertEventArgs>? Geleert;

    /// <summary>Ein Erfassungsbereich ist offen (z. B. eine Zelle im Bearbeitungsmodus): Rueckgaengig wartet.</summary>
    public bool EingabeOffen { get { lock (_gate) return _offen is not null; } }

    public void Binde(Project? projekt)
    {
        lock (_gate)
        {
            if (ReferenceEquals(_projekt, projekt))
                return;
            if (_projekt is not null)
            {
                _projekt.Data.CollectionChanged -= OnListeGeaendert;
                _projekt.SchaechteData.CollectionChanged -= OnListeGeaendert;
            }
            _projekt = projekt;
            if (projekt is not null)
            {
                projekt.Data.CollectionChanged += OnListeGeaendert;
                projekt.SchaechteData.CollectionChanged += OnListeGeaendert;
            }
        }
        Leere(GrundProjekt);
    }

    private void OnListeGeaendert(object? sender, NotifyCollectionChangedEventArgs e) => Leere(GrundListe);

    public IDisposable Erfasse(HaltungRecord datensatz, string? feld = null, IEnumerable<string>? nurFelder = null)
        => Oeffne(DatenaenderungsBereich.Haltungen, [DatensatzZugriff.Fuer(datensatz)], feld, null, null, nurFelder);

    public IDisposable Erfasse(SchachtRecord datensatz, string? feld = null, IEnumerable<string>? nurFelder = null)
        => Oeffne(DatenaenderungsBereich.Schaechte, [DatensatzZugriff.Fuer(datensatz)], feld, null, null, nurFelder);

    public IDisposable ErfasseMehrere(IEnumerable<HaltungRecord> datensaetze, string beschreibung)
        => Oeffne(DatenaenderungsBereich.Haltungen, datensaetze.Select(DatensatzZugriff.Fuer).ToList(), null, beschreibung, null);

    public IDisposable ErfasseMehrere(IEnumerable<SchachtRecord> datensaetze, string beschreibung)
        => Oeffne(DatenaenderungsBereich.Schaechte, datensaetze.Select(DatensatzZugriff.Fuer).ToList(), null, beschreibung, null);

    public IDisposable ErfasseObjektakte(ObjektaktenBearbeitung bearbeitung, string? feld = null)
    {
        ArgumentNullException.ThrowIfNull(bearbeitung);
        var p = bearbeitung.Projekt;
        DatensatzZugriff? zugriff = bearbeitung.Art == "haltung"
            ? p.Data.FirstOrDefault(r => r.Id == bearbeitung.WurzelId) is { } h ? DatensatzZugriff.Fuer(h) : null
            : p.SchaechteData.FirstOrDefault(r => r.Id == bearbeitung.WurzelId) is { } s ? DatensatzZugriff.Fuer(s) : null;
        if (zugriff is null)
            return LeererBereich.Instanz;
        return Oeffne(zugriff.Bereich, [zugriff], feld, null, bearbeitung);
    }

    private IDisposable Oeffne(DatenaenderungsBereich bereich, IReadOnlyList<DatensatzZugriff> zugriffe,
        string? feld, string? beschreibung, ObjektaktenBearbeitung? akte, IEnumerable<string>? nurFelder = null)
    {
        lock (_gate)
        {
            if (_wendetAn || _projekt is null)
                return LeererBereich.Instanz;
            // Ein innerer Bereich (abhaengiges Feld, Auswahlfeld in einer offenen Zelle) wird Teil
            // des aeusseren: eine Benutzeraktion, ein Schritt.
            if (_offen is not null && !ReferenceEquals(_offen.Projekt, _projekt))
                _offen = null;
            var erfassung = _offen ?? (_offen = new Erfassung(this, _projekt, bereich, feld, beschreibung));
            foreach (var z in zugriffe)
                erfassung.Merke(z, nurFelder);
            if (akte is not null)
                erfassung.MerkeAkten(akte);
            erfassung.Offen++;
            return new Griff(erfassung);
        }
    }

    private void Schliesse(Erfassung erfassung)
    {
        string? sperrgrund = null;
        lock (_gate)
        {
            if (--erfassung.Offen > 0)
                return;
            if (ReferenceEquals(_offen, erfassung))
                _offen = null;
            if (erfassung.Verworfen || !ReferenceEquals(erfassung.Projekt, _projekt))
                return;
            var eintrag = erfassung.BaueEintrag(out sperrgrund);
            if (eintrag is not null)
            {
                var stapel = _rueck[eintrag.Bereich];
                stapel.Add(eintrag);
                if (stapel.Count > Tiefe)
                    stapel.RemoveAt(0);
                _vor[eintrag.Bereich].Clear();
            }
            else if (sperrgrund is null)
            {
                return;
            }
        }
        if (sperrgrund is not null)
            Leere(sperrgrund);
        else
            Geaendert?.Invoke(this, EventArgs.Empty);
    }

    public bool KannRueckgaengig(DatenaenderungsBereich bereich) { lock (_gate) return _rueck[bereich].Count > 0; }
    public bool KannWiederholen(DatenaenderungsBereich bereich) { lock (_gate) return _vor[bereich].Count > 0; }
    public string? RueckgaengigBeschreibung(DatenaenderungsBereich bereich) { lock (_gate) return _rueck[bereich].LastOrDefault()?.Beschreibung; }
    public string? WiederholenBeschreibung(DatenaenderungsBereich bereich) { lock (_gate) return _vor[bereich].LastOrDefault()?.Beschreibung; }

    public DatenaenderungsErgebnis Rueckgaengig(DatenaenderungsBereich bereich)
        => Wende(bereich, rueckwaerts: true);

    public DatenaenderungsErgebnis Wiederholen(DatenaenderungsBereich bereich)
        => Wende(bereich, rueckwaerts: false);

    private DatenaenderungsErgebnis Wende(DatenaenderungsBereich bereich, bool rueckwaerts)
    {
        var titel = rueckwaerts ? "Rückgängig" : "Wiederholen";
        VerlaufEintrag eintrag;
        string? grund;
        lock (_gate)
        {
            var von = rueckwaerts ? _rueck[bereich] : _vor[bereich];
            if (_offen is not null || _wendetAn)
                return new DatenaenderungsErgebnis(false, $"{titel}: Die Eingabe ist noch nicht abgeschlossen.", []);
            if (von.Count == 0)
                return new DatenaenderungsErgebnis(false, $"{titel}: nichts zu tun.", []);
            eintrag = von[^1];
            von.RemoveAt(von.Count - 1);
            grund = Hindernis(eintrag, rueckwaerts);
            _wendetAn = grund is null;
        }
        if (grund is not null)
        {
            // Der Eintrag passt nicht mehr zum Datenstand: verwerfen, nichts schreiben.
            Geaendert?.Invoke(this, EventArgs.Empty);
            return new DatenaenderungsErgebnis(false, $"{titel} nicht möglich: {eintrag.Beschreibung} — {grund}", []);
        }
        string? fehler;
        bool zurueckgesetzt;
        try
        {
            fehler = WendeAn(eintrag, rueckwaerts, out zurueckgesetzt);
        }
        finally
        {
            lock (_gate)
                _wendetAn = false;
        }
        if (fehler is not null)
        {
            // Halb angewendet darf nie wiederholbar sein: Der Eintrag faellt weg. Liess sich der Teil nicht
            // zuruecksetzen, stimmt kein anderer Eintrag mehr sicher - dann ist der ganze Verlauf leer.
            if (!zurueckgesetzt)
                Leere(GrundFehler);
            Geaendert?.Invoke(this, EventArgs.Empty);
            return new DatenaenderungsErgebnis(false, zurueckgesetzt
                ? $"{titel} nicht möglich: {eintrag.Beschreibung} — {fehler} Es wurde nichts geändert."
                : $"{titel} nicht vollständig: {eintrag.Beschreibung} — {fehler} Bitte die Werte prüfen.", []);
        }
        lock (_gate)
            (rueckwaerts ? _vor[bereich] : _rueck[bereich]).Add(eintrag);
        Geaendert?.Invoke(this, EventArgs.Empty);
        return new DatenaenderungsErgebnis(true, $"{titel}: {eintrag.Beschreibung}",
            eintrag.Datensaetze.Select(d => d.Zugriff.Datensatz).ToList());
    }

    /// <summary>Warum ein Eintrag nicht mehr anwendbar ist, oder <c>null</c>. Verglichen wird mit dem
    /// Zustand, den der eigene Schritt hinterlassen hat - Zeitstempel ausgenommen.</summary>
    private string? Hindernis(VerlaufEintrag eintrag, bool rueckwaerts)
    {
        if (!ReferenceEquals(eintrag.Projekt, _projekt))
            return "das Projekt ist nicht mehr offen.";
        foreach (var d in eintrag.Datensaetze)
        {
            if (!d.Zugriff.ImProjekt(eintrag.Projekt))
                return "der Datensatz ist nicht mehr im Projekt.";
            foreach (var f in d.Felder)
            {
                var erwartetWert = rueckwaerts ? f.WertNachher : f.WertVorher;
                var erwartetMeta = rueckwaerts ? f.MetaNachher : f.MetaVorher;
                var istWert = d.Zugriff.Fields.TryGetValue(f.Feld, out var w) ? w : null;
                if (!string.Equals(istWert, erwartetWert, StringComparison.Ordinal)
                    || !FieldMetadataKopie.Gleich(d.Zugriff.Meta.GetValueOrDefault(f.Feld), erwartetMeta, mitZeitstempel: false))
                    return $"«{d.Zugriff.Label(f.Feld)}» wurde inzwischen anders geändert.";
            }
        }
        foreach (var a in eintrag.Akten)
        {
            var vorhanden = eintrag.Projekt.Objektakten.Contains(a.Akte);
            if (vorhanden != (rueckwaerts ? a.NachherVorhanden : a.VorherVorhanden))
                return "die Objektakte wurde inzwischen verändert.";
            foreach (var (feld, vorher, nachher) in a.Werte)
                if (!AktenWertKopie.Gleich(a.Akte.Werte.GetValueOrDefault(feld), rueckwaerts ? nachher : vorher))
                    return "die Objektakte wurde inzwischen verändert.";
            var soll = rueckwaerts ? a.VorherVorhanden : a.NachherVorhanden;
            // Eine Akte wird nur entfernt, wenn sie GANZ dem eigenen Stand entspricht - Quellen, Bezuege,
            // Unterlisten, Hauptdeckel, Zusatzdaten und alle Werte (auch von nicht erfassten Wegen wie
            // GeoShop, Zusatzdatei, WebGIS geschrieben). Sonst ginge fremde Arbeit mit verloren.
            if (vorhanden && !soll
                && !string.Equals(AktenJson.Von(a.Akte), rueckwaerts ? a.NachherJson : a.VorherJson, StringComparison.Ordinal))
                return "die Objektakte enthält inzwischen weitere Angaben.";
            // Wieder anfuegen nur, wenn keine Akte mit derselben Kennung (inzwischen neu entstanden) da ist.
            if (!vorhanden && soll && eintrag.Projekt.Objektakten.Any(x => x.Id == a.Akte.Id))
                return "zur Objektakte gibt es inzwischen einen neuen Eintrag.";
        }
        return null;
    }

    /// <summary>
    /// Wendet einen Schritt an: Aktenwerte, An-/Abmelden der Akte, dann die Felder. Scheitert ein Teil
    /// (Ausnahme, etwa aus einer Meldung), werden der angefangene und alle schon angewendeten Teile in
    /// umgekehrter Reihenfolge zurueckgesetzt. Rueckgabe: Fehlertext oder <c>null</c>.
    /// </summary>
    private static string? WendeAn(VerlaufEintrag eintrag, bool rueckwaerts, out bool zurueckgesetzt)
    {
        zurueckgesetzt = true;
        var teile = new List<(Action Anwenden, Action Zuruecksetzen)>();
        foreach (var a in eintrag.Akten)
        {
            var akte = a.Akte;
            foreach (var (feld, vorher, nachher) in a.Werte)
            {
                var ziel = rueckwaerts ? vorher : nachher;
                var bisher = rueckwaerts ? nachher : vorher;
                teile.Add((() => SetzeAktenWert(akte, feld, ziel), () => SetzeAktenWert(akte, feld, bisher)));
            }
            var soll = rueckwaerts ? a.VorherVorhanden : a.NachherVorhanden;
            var ist = rueckwaerts ? a.NachherVorhanden : a.VorherVorhanden;
            if (soll != ist)
            {
                var liste = eintrag.Projekt.Objektakten;
                teile.Add((() => SetzeVorhanden(liste, akte, soll), () => SetzeVorhanden(liste, akte, ist)));
            }
        }
        foreach (var d in eintrag.Datensaetze)
        {
            var zugriff = d.Zugriff;
            foreach (var f in d.Felder)
                teile.Add((
                    () => zugriff.Stelle(f.Feld, rueckwaerts ? f.WertVorher : f.WertNachher, rueckwaerts ? f.MetaVorher : f.MetaNachher),
                    () => zugriff.Stelle(f.Feld, rueckwaerts ? f.WertNachher : f.WertVorher, rueckwaerts ? f.MetaNachher : f.MetaVorher)));
        }

        for (var i = 0; i < teile.Count; i++)
        {
            try
            {
                teile[i].Anwenden();
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                for (var j = i; j >= 0; j--)
                {
                    try { teile[j].Zuruecksetzen(); }
                    catch (Exception) { zurueckgesetzt = false; }
                }
                return ex.Message;
            }
        }
        return null;
    }

    private static void SetzeAktenWert(ObjektAkte akte, string feld, ObjektFeldWert? wert)
    {
        if (wert is null) akte.Werte.Remove(feld);
        else akte.Werte[feld] = AktenWertKopie.Von(wert);
    }

    private static void SetzeVorhanden(List<ObjektAkte> liste, ObjektAkte akte, bool vorhanden)
    {
        var ist = liste.Contains(akte);
        if (vorhanden && !ist) liste.Add(akte);
        else if (!vorhanden && ist) liste.Remove(akte);
    }

    public void Leere(string grund)
    {
        bool hatte;
        lock (_gate)
        {
            hatte = _rueck.Values.Concat(_vor.Values).Any(s => s.Count > 0);
            foreach (var s in _rueck.Values.Concat(_vor.Values))
                s.Clear();
            if (_offen is not null)
                _offen.Verworfen = true;
            _offen = null;
        }
        if (!hatte)
            return;
        Geleert?.Invoke(this, new DatenaenderungsVerlaufGeleertEventArgs(grund));
        Geaendert?.Invoke(this, EventArgs.Empty);
    }

    private sealed class Griff(Erfassung erfassung) : IDisposable
    {
        private int _geschlossen;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _geschlossen, 1) == 0)
                erfassung.Verlauf.Schliesse(erfassung);
        }
    }

    private sealed class LeererBereich : IDisposable
    {
        public static readonly LeererBereich Instanz = new();
        public void Dispose() { }
    }

    /// <summary>Ein offener Erfassungsbereich: Schnappschuesse aller betroffenen Datensaetze und Akten.</summary>
    private sealed class Erfassung(DatenaenderungsVerlauf verlauf, Project projekt, DatenaenderungsBereich bereich,
        string? feld, string? beschreibung)
    {
        private readonly List<DatensatzSchnappschuss> _datensaetze = [];
        private readonly Dictionary<ObjektAkte, (bool Vorhanden, Dictionary<string, ObjektFeldWert> Werte)> _akten
            = new(ReferenceEqualityComparer.Instance);
        private HashSet<ObjektAkte>? _aktenVorher;

        public DatenaenderungsVerlauf Verlauf => verlauf;
        public Project Projekt => projekt;
        public int Offen { get; set; }
        public bool Verworfen { get; set; }

        public void Merke(DatensatzZugriff zugriff, IEnumerable<string>? nurFelder)
        {
            var vorhanden = _datensaetze.FirstOrDefault(s => ReferenceEquals(s.Zugriff.Datensatz, zugriff.Datensatz));
            if (vorhanden is not null)
                vorhanden.Erweitere(nurFelder);
            else
                _datensaetze.Add(new DatensatzSchnappschuss(zugriff, nurFelder));
        }

        public void MerkeAkten(ObjektaktenBearbeitung bearbeitung)
        {
            _aktenVorher ??= new HashSet<ObjektAkte>(projekt.Objektakten, ReferenceEqualityComparer.Instance);
            foreach (var akte in bearbeitung.Verbund)
                if (!_akten.ContainsKey(akte))
                    _akten[akte] = (_aktenVorher.Contains(akte), AktenWertKopie.Von(akte.Werte));
        }

        public VerlaufEintrag? BaueEintrag(out string? sperrgrund)
        {
            sperrgrund = null;
            var datensaetze = new List<DatensatzAenderung>();
            foreach (var s in _datensaetze.Where(x => x.Zugriff.ImProjekt(projekt)))
            {
                var unterschiede = s.Unterschiede();
                if (unterschiede.Count == 0)
                    continue;
                if (unterschiede.FirstOrDefault(u => u.WertGeaendert && s.Zugriff.MitDateifolgen(u.Feld)) is { } umbenannt)
                {
                    // Die Umbenennung zieht Ordner und Dateien mit; das laesst sich nicht zuruecknehmen.
                    sperrgrund = $"«{s.Zugriff.Label(umbenannt.Feld)}» geändert (Ordner und Dateien umbenannt)";
                    return null;
                }
                datensaetze.Add(new DatensatzAenderung(s.Zugriff, unterschiede));
            }
            var akten = AktenAenderungen();
            // Nur eine sichtbare Aenderung ist ein Schritt; ein blosses Nachstempeln derselben Werte
            // (Herkunft/Zeit) waere ein Rueckgaengig, bei dem nichts passiert.
            if (!datensaetze.Any(d => d.Felder.Any(f => f.WertGeaendert)) && akten.Count == 0)
                return null;
            var text = beschreibung ?? Beschreibe(datensaetze.Count > 0 ? datensaetze[0] : null);
            return new VerlaufEintrag(projekt, bereich, text, datensaetze, akten);
        }

        private List<AkteAenderung> AktenAenderungen()
        {
            var liste = new List<AkteAenderung>();
            if (_aktenVorher is null)
                return liste;
            // Neu angelegte Akten (die Wurzelakte entsteht beim ersten Schreiben) gehoeren dazu.
            foreach (var neu in projekt.Objektakten.Where(a => !_aktenVorher.Contains(a) && !_akten.ContainsKey(a)).ToList())
                _akten[neu] = (false, new Dictionary<string, ObjektFeldWert>(StringComparer.Ordinal));
            foreach (var (akte, (vorhanden, werte)) in _akten)
            {
                var nachherVorhanden = projekt.Objektakten.Contains(akte);
                var felder = werte.Keys.Union(akte.Werte.Keys, StringComparer.Ordinal)
                    .Where(k => !AktenWertKopie.Gleich(werte.GetValueOrDefault(k), akte.Werte.GetValueOrDefault(k)))
                    .Select(k => (k, werte.GetValueOrDefault(k),
                        akte.Werte.TryGetValue(k, out var n) ? AktenWertKopie.Von(n) : null))
                    .ToList();
                if (felder.Count > 0 || vorhanden != nachherVorhanden)
                    liste.Add(new AkteAenderung(akte, vorhanden, nachherVorhanden, felder,
                        VorherJson: null, // ein Bearbeitungsschritt entfernt nie eine Akte
                        NachherJson: nachherVorhanden ? AktenJson.Von(akte) : null));
            }
            return liste;
        }

        private string Beschreibe(DatensatzAenderung? d)
        {
            if (d is null)
                return (feld ?? "Objektakte").Trim();
            var bekannt = feld is not null && d.Felder.Any(f => f.Feld == feld);
            var name = feld is null
                ? d.Zugriff.Label((d.Felder.FirstOrDefault(f => f.WertGeaendert) ?? d.Felder[0]).Feld)
                : bekannt || d.Zugriff.Fields.ContainsKey(feld) ? d.Zugriff.Label(feld) : feld;
            return $"{name} {d.Zugriff.Name()}".Trim();
        }
    }
}
