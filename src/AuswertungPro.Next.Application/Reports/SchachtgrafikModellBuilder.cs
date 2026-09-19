using System.Globalization;
using System.Text.RegularExpressions;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Lookup;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Application.Reports;

/// <summary>
/// Baut das <see cref="SchachtgrafikModell"/> aus Datensatz, Haltungen der Seite, Katalog und dem
/// optionalen <see cref="SchachtgrafikZusatz"/> (Lage, Koten). Reine Regeln, WPF-frei, lesend.
///
/// Was woher kommt:
/// - Tiefe: Feld <c>Schachttiefe</c> (Protokoll); sonst Deckelkote minus Sohlenkote (Kataster).
/// - Anschluesse: zuerst die Anschlusstabelle (<see cref="SchachtRecord.Anschluesse"/>), dazu die
///   Haltungen der Seite ueber <see cref="SchachtHaltungsseite"/>. Tabellenzeile und Haltung
///   werden ueber Seite (Aus/Ein), Durchmesser (<see cref="DnSpiel"/>) und Tiefe
///   (<see cref="TiefenSpielM"/>, aus Deckelkote minus Punktkote) verbunden — nur wenn genau
///   eine Haltung passt. Eine Zeile ohne Projekthaltung bekommt die Richtung einer
///   Katasterleitung am Schachtpunkt (gleiche Seite, gleicher Durchmesser); Katasterleitungen
///   ohne Zeile erscheinen als «nur im Kataster».
/// - Kennungen zaehlen je Typ (A1, E1, E2 …) wie die Skizze des Inspekteurs (74 von 74
///   Uri-Protokollen) und wie SchachtPro; die Tabellennummer bleibt in
///   <see cref="SchachtgrafikAnschluss.Nr"/>.
/// - Richtung je Haltung aus der Lage; ohne Lage die Uhrlage der Tabelle (SchachtPro, Skizze).
///   Kote je Haltung aus den Koten; fehlt die Tabellentiefe, ergibt Deckelkote minus
///   Rohrsohlenkote die Tiefe (Quelle «Kataster»).
/// - Schaeden: je Protokolleintrag einer, verortet ueber <see cref="SchachtSchadenOrtRegel"/>.
///   Eine Bemerkung «Einlauf 3 ausgebrochen» wird zum Schaden am DRITTEN Einlauf (Kennung E3),
///   «Anschluss 3» am Anschluss mit Tabellennummer 3 — nur wenn es ihn gibt.
/// Nichts davon schreibt zurueck; nichts wird geraten — offene Stellen stehen in den Hinweisen.
/// </summary>
public static class SchachtgrafikModellBuilder
{
    /// <summary>
    /// Durchmesser gelten als gleich, wenn sie hoechstens 5 % (mindestens 5 mm) auseinanderliegen:
    /// Die Kopie fuehrt 148 mm, das Protokoll DN 150.
    /// </summary>
    internal const double DnSpiel = 0.05;

    /// <summary>
    /// Tiefen gelten als gleich, wenn sie hoechstens 30 cm auseinanderliegen (80409: Protokoll
    /// 3.38 m ab Deckel, Kataster 3.46 m). Ein Hausanschluss bei 0.60 m ist damit nie die
    /// Hauptleitung bei 3.40 m, auch wenn beide denselben Durchmesser haben.
    /// </summary>
    internal const decimal TiefenSpielM = 0.30m;

    private static readonly Regex BemerkungAnschluss = new(
        @"^\s*(?<art>Einlauf|Auslauf|Anschluss)\s*(?<nr>\d{1,2})\s*[:\-]?\s*(?<t>\S.*?)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static SchachtgrafikModell Baue(
        SchachtRecord record,
        IReadOnlyList<HaltungRecord>? haltungen,
        ICodeCatalogProvider? catalog,
        SchachtgrafikZusatz? zusatz,
        string markenfarbe)
    {
        ArgumentNullException.ThrowIfNull(record);
        var lage = zusatz?.Lage;
        var koten = zusatz?.Koten;
        var hinweise = new List<string>();

        var schachtnummer = Leer(Wert(record, "Schachtnummer"));
        var tiefe = ParseTiefeMeter(Wert(record, "Schachttiefe"));
        var tiefeQuelle = tiefe is null ? null : "Protokoll";
        if (tiefe is null && koten?.Tiefe is { } katasterTiefe)
        {
            tiefe = katasterTiefe;
            tiefeQuelle = "Kataster";
        }
        else if (tiefe is { } t && koten?.Tiefe is { } kt && Math.Abs(t - kt) > 0.05m)
        {
            hinweise.Add($"Kataster: Tiefe {kt.ToString("0.00", CultureInfo.InvariantCulture)} m (Protokoll {t.ToString("0.00", CultureInfo.InvariantCulture)} m)");
        }

        var dimension1 = ParseMm(Wert(record, FieldKeys.ShaftDimension1Mm));
        var dimension2 = ParseMm(Wert(record, FieldKeys.ShaftDimension2Mm));
        var deckel = ParseMm(Wert(record, "Deckeldurchmesser"));
        var steigeisen = Steighilfe(Wert(record, "Steighilfe"));

        var anschluesse = Anschluesse(record, haltungen, schachtnummer, lage, koten, hinweise);
        var schaeden = Schaeden(record, catalog, anschluesse, markenfarbe);

        if (!string.IsNullOrWhiteSpace(zusatz?.LageHinweis))
            hinweise.Add(zusatz.LageHinweis.Trim());
        if (tiefe is null)
            hinweise.Add("Tiefe nicht erfasst: Schnitt ohne Massstab");
        if (dimension1 is null || dimension2 is null)
            hinweise.Add("Innenmasse nicht erfasst: Schachtkoerper schematisch");
        else if (dimension1 != dimension2)
            hinweise.Add("Ausrichtung des Ovals nicht erfasst: lange Achse in Fliessrichtung gezeichnet");
        hinweise.Add(deckel is null ? "Deckel und Konus schematisch" : "Konus schematisch");
        if (steigeisen == true)
            hinweise.Add("Steigeisen vorhanden, Seite nicht erfasst: schematisch links");

        return new SchachtgrafikModell(
            schachtnummer,
            tiefe,
            tiefeQuelle,
            dimension1,
            dimension2,
            Leer(Wert(record, "Schachtform")),
            deckel,
            Leer(Wert(record, "Deckelmaterial")),
            Leer(Wert(record, FieldKeys.UsageType)) ?? Leer(Wert(record, "Medium")),
            steigeisen,
            koten,
            anschluesse,
            schaeden,
            hinweise);
    }

    /// <summary>Eine Haltung der Seite, die an diesem Schacht haengt.</summary>
    private sealed record Kandidat(HaltungRecord H, Haltungsseite Seite, string Name, int? Dn, string? Material);

    private static List<SchachtgrafikAnschluss> Anschluesse(
        SchachtRecord record,
        IReadOnlyList<HaltungRecord>? haltungen,
        string? schachtnummer,
        SchachtLage? lage,
        SchachtKoten? koten,
        List<string> hinweise)
    {
        var seiten = new List<Kandidat>();
        foreach (var h in haltungen ?? [])
        {
            var seite = SchachtHaltungsseite.Bestimme(h, schachtnummer);
            if (seite == Haltungsseite.Keine)
                continue;
            seiten.Add(new Kandidat(
                h,
                seite,
                (h.GetFieldValue(FieldKeys.HoldingName) ?? "").Trim(),
                ParseMm(h.GetFieldValue(FieldKeys.NominalDiameterMm)),
                Leer(h.GetFieldValue(FieldKeys.PipeMaterial))));
        }

        var tiefen = TiefenJeHaltung(koten);
        var verwendet = new HashSet<HaltungRecord>();
        var verwendeteKataster = new HashSet<SchachtLageLeitung>();
        var liste = new List<SchachtgrafikAnschluss>();
        var tabelle = (record.Anschluesse ?? []).Where(a => a.Nr > 0).OrderBy(a => a.Nr).ToList();

        foreach (var zeile in tabelle)
        {
            var seite = zeile.IstAuslauf ? Haltungsseite.Oben : Haltungsseite.Unten;
            var kandidaten = !string.IsNullOrWhiteSpace(zeile.Haltungsname)
                ? seiten.Where(s => !verwendet.Contains(s.H) && string.Equals(s.Name, zeile.Haltungsname.Trim(), StringComparison.OrdinalIgnoreCase)).ToList()
                : Waehle(seiten.Where(s => !verwendet.Contains(s.H) && s.Seite == seite).ToList(), zeile.DnMm, zeile.TiefeM, tiefen);

            var treffer = kandidaten.Count == 1 ? kandidaten[0] : null;
            if (treffer is not null)
                verwendet.Add(treffer.H);

            // Keine Projekthaltung: vielleicht eine Katasterleitung am Schachtpunkt (Hausanschluss).
            var kataster = treffer is null && string.IsNullOrWhiteSpace(zeile.Haltungsname)
                ? WaehleKataster(lage, verwendeteKataster, zeile.IstAuslauf, zeile.DnMm)
                : null;
            if (kataster is not null)
                verwendeteKataster.Add(kataster);

            liste.Add(Erzeuge(
                zeile.Nr,
                zeile.IstAuslauf,
                zeile.DnMm ?? treffer?.Dn ?? kataster?.DnMm,
                zeile.TiefeM,
                zeile.TiefeM is null ? null : "Protokoll",
                zeile.Material ?? treffer?.Material ?? kataster?.Material,
                treffer?.Name ?? kataster?.Name ?? Leer(zeile.Haltungsname),
                imProjekt: treffer is not null,
                lage,
                koten,
                uhrGrad: SchachtUhrlage.Grad(zeile.Uhr),
                azimutDirekt: kataster?.AzimutGrad,
                nurImKataster: false));
        }

        var naechsteNr = tabelle.Count > 0 ? tabelle.Max(a => a.Nr) + 1 : 1;
        foreach (var s in seiten.Where(s => !verwendet.Contains(s.H)).OrderBy(s => s.Seite == Haltungsseite.Oben ? 0 : 1).ThenBy(s => s.Name, StringComparer.OrdinalIgnoreCase))
        {
            // Projekthaltung ohne Zeile: Richtung aus der Lage. Kennt die Kopie den Namen nicht,
            // hilft eine Katasterleitung derselben Seite mit gleichem Durchmesser — das wird gesagt.
            var kataster = lage is not null && !lage.AzimutJeHaltung.ContainsKey(s.Name)
                ? WaehleKataster(lage, verwendeteKataster, s.Seite == Haltungsseite.Oben, s.Dn)
                : null;
            if (kataster is not null)
            {
                verwendeteKataster.Add(kataster);
                hinweise.Add($"Richtung von {s.Name} aus der Katasterleitung {kataster.Name} (Name weicht ab)");
            }

            liste.Add(Erzeuge(
                naechsteNr++,
                s.Seite == Haltungsseite.Oben,
                s.Dn,
                null,
                null,
                s.Material,
                s.Name,
                imProjekt: true,
                lage,
                koten,
                uhrGrad: null,
                azimutDirekt: kataster?.AzimutGrad,
                nurImKataster: false));
        }

        // Katasterleitungen ohne Zeile und ohne Projekthaltung: Hausanschluesse, die das Protokoll
        // nicht nennt. Sie sind vermessen, also gezeigt — und als «nur im Kataster» gekennzeichnet.
        foreach (var w in (lage?.WeitereLeitungen ?? []).Where(w => !verwendeteKataster.Contains(w)))
        {
            liste.Add(Erzeuge(
                naechsteNr++,
                !w.EndetImSchacht,
                w.DnMm,
                null,
                null,
                w.Material,
                w.Name,
                imProjekt: false,
                lage,
                koten,
                uhrGrad: null,
                azimutDirekt: w.AzimutGrad,
                nurImKataster: true));
        }

        liste = Nummeriere(liste);

        foreach (var a in liste.Where(a => !a.ImProjekt))
        {
            hinweise.Add(a.NurImKataster
                ? $"{a.Kennung} nur im Kataster ({a.Haltungsname}), nicht im Protokoll und nicht im Projekt"
                : a.AzimutGrad is not null && !string.IsNullOrWhiteSpace(a.Haltungsname)
                    ? $"{a.Kennung} steht im Protokoll, aber nicht im Projekt (Kataster: {a.Haltungsname})"
                    : $"{a.Kennung} steht im Protokoll, aber nicht im Projekt");
        }

        if (liste.Count > 0 && !liste.Any(HatRichtung))
        {
            hinweise.Add("Richtungen nicht erfasst: Grundriss schematisch");
        }
        else
        {
            foreach (var a in liste.Where(a => !HatRichtung(a)))
                hinweise.Add($"Richtung nicht erfasst: {a.Kennung}");
            if (liste.Any(a => a.AzimutGrad is null && a.UhrGrad is not null))
                hinweise.Add("Richtungen teilweise aus der Uhrlage des Protokolls (nicht vermessen)");
        }

        return liste;
    }

    private static bool HatRichtung(SchachtgrafikAnschluss a) => a.AzimutGrad is not null || a.UhrGrad is not null;

    /// <summary>Kennungen je Typ in Listenreihenfolge: A1, A2 … und E1, E2 … — wie Skizze und SchachtPro.</summary>
    private static List<SchachtgrafikAnschluss> Nummeriere(List<SchachtgrafikAnschluss> liste)
    {
        var auslauf = 0;
        var einlauf = 0;
        return liste.Select(a => a with { TypNr = a.IstAuslauf ? ++auslauf : ++einlauf }).ToList();
    }

    /// <summary>
    /// Engt die Haltungen einer Seite auf die passende ein: zuerst nach Durchmesser, dann nach
    /// Tiefe (Deckelkote minus Punktkote gegen die Tabellentiefe). Jeder Schritt engt nur ein,
    /// wenn danach noch etwas uebrig bleibt; eine einzelne Haltung der Seite bleibt Kandidatin,
    /// auch ohne Durchmesser — sie ist die einzige Moeglichkeit.
    /// </summary>
    private static List<Kandidat> Waehle(List<Kandidat> kandidaten, int? dn, decimal? tiefe, IReadOnlyDictionary<string, decimal> tiefen)
    {
        if (kandidaten.Count <= 1)
            return kandidaten;

        var passend = kandidaten;
        if (dn is { } d)
        {
            var gleicheDn = passend.Where(k => PasstDn(k.Dn, d)).ToList();
            if (gleicheDn.Count > 0)
                passend = gleicheDn;
        }

        if (passend.Count > 1 && tiefe is { } t)
        {
            var gleicheTiefe = passend
                .Where(k => tiefen.TryGetValue(k.Name, out var kt) && Math.Abs(kt - t) <= TiefenSpielM)
                .ToList();
            if (gleicheTiefe.Count > 0)
                passend = gleicheTiefe;
        }

        return passend;
    }

    /// <summary>
    /// Die eine Katasterleitung, die zu Seite und Durchmesser passt; bei mehreren keine.
    /// Nennt die Zeile einen Durchmesser, muss auch die Leitung einen passenden haben —
    /// eine Leitung ohne Durchmesser ist dann keine Kandidatin.
    /// </summary>
    private static SchachtLageLeitung? WaehleKataster(SchachtLage? lage, HashSet<SchachtLageLeitung> verwendet, bool istAuslauf, int? dn)
    {
        if (lage is null)
            return null;

        var passend = lage.WeitereLeitungen
            .Where(w => !verwendet.Contains(w) && w.EndetImSchacht == !istAuslauf)
            .ToList();
        if (dn is { } d)
            passend = passend.Where(w => PasstDn(w.DnMm, d)).ToList();

        return passend.Count == 1 ? passend[0] : null;
    }

    internal static bool PasstDn(int? kandidat, int dn)
        => kandidat is { } k && Math.Abs(k - dn) <= Math.Max(5d, dn * DnSpiel);

    /// <summary>Tiefe ab Deckel-OK je Haltung aus dem Kataster: Deckelkote minus Kote des Haltungspunkts.</summary>
    private static IReadOnlyDictionary<string, decimal> TiefenJeHaltung(SchachtKoten? koten)
    {
        var tiefen = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        if (koten?.Deckel is not { } deckel)
            return tiefen;

        foreach (var (name, kote) in koten.JeHaltung)
        {
            if (deckel > kote)
                tiefen[name] = deckel - kote;
        }

        return tiefen;
    }

    private static SchachtgrafikAnschluss Erzeuge(
        int nr,
        bool istAuslauf,
        int? dn,
        decimal? tiefe,
        string? tiefeQuelle,
        string? material,
        string? haltungsname,
        bool imProjekt,
        SchachtLage? lage,
        SchachtKoten? koten,
        double? uhrGrad,
        double? azimutDirekt,
        bool nurImKataster)
    {
        var azimut = azimutDirekt;
        decimal? kote = null;
        if (!string.IsNullOrWhiteSpace(haltungsname))
        {
            if (lage is not null && lage.AzimutJeHaltung.TryGetValue(haltungsname, out var a))
                azimut = a;
            if (koten is not null && koten.JeHaltung.TryGetValue(haltungsname, out var k))
                kote = k;
        }

        if (tiefe is null && kote is { } rohrsohle && koten?.Deckel is { } deckel && deckel > rohrsohle)
        {
            tiefe = deckel - rohrsohle;
            tiefeQuelle = "Kataster";
        }

        return new SchachtgrafikAnschluss(nr, istAuslauf, dn, tiefe, tiefeQuelle, material, haltungsname, azimut, kote, imProjekt, 0, uhrGrad, nurImKataster);
    }

    private static List<SchachtgrafikSchaden> Schaeden(
        SchachtRecord record,
        ICodeCatalogProvider? catalog,
        IReadOnlyList<SchachtgrafikAnschluss> anschluesse,
        string markenfarbe)
    {
        var roh = new List<(SchachtBauteil Bauteil, int? AnschlussNr, string Kategorie, string Tooltip)>();
        foreach (var eintrag in (record.Protocol?.Current?.Entries ?? []).Where(e => !e.IsDeleted))
        {
            roh.Add((
                SchachtSchadenOrtRegel.BestimmeBauteil(eintrag),
                SchachtSchadenOrtRegel.AnschlussNr(eintrag),
                SchachtSchadenKategorieRegel.Bestimme(eintrag.Code, eintrag.Beschreibung),
                Hinweistext(eintrag, catalog)));
        }

        foreach (var (art, nr, text) in BemerkungenZuAnschluessen(record))
        {
            // «Einlauf 3» ist der dritte Einlauf (Kennung E3, wie in der Skizze), «Auslauf 2» der
            // zweite Auslauf, «Anschluss 3» die Tabellennummer 3.
            var ziel = art switch
            {
                "einlauf" => anschluesse.FirstOrDefault(a => !a.IstAuslauf && a.TypNr == nr),
                "auslauf" => anschluesse.FirstOrDefault(a => a.IstAuslauf && a.TypNr == nr),
                _ => anschluesse.FirstOrDefault(a => a.Nr == nr),
            };
            if (ziel is null)
                continue;
            roh.Add((
                SchachtBauteil.Anschluss,
                ziel.Nr,
                SchachtSchadenKategorieRegel.KategorieAusText(text) ?? "default",
                $"{ziel.Kennung}: {text} (Bemerkung)"));
        }

        return roh
            .Select((r, i) => (r, i))
            .OrderBy(x => (int)x.r.Bauteil)
            .ThenBy(x => x.i)
            .Select((x, i) => new SchachtgrafikSchaden(
                i + 1,
                x.r.Bauteil,
                x.r.AnschlussNr,
                x.r.Kategorie,
                DamageSymbolClassifier.GetDamageSymbolColor(x.r.Kategorie, markenfarbe),
                x.r.Tooltip))
            .ToList();
    }

    /// <summary>
    /// Bemerkungen der Form «Einlauf 3 Ausgebrochen» — aus dem Feld <c>Bemerkungen</c> und aus
    /// den «Bemerkungen: …»-Zeilen der primaeren Schaeden. Nur mit Art und Nummer; alles andere
    /// bleibt Freitext.
    /// </summary>
    private static IEnumerable<(string Art, int Nr, string Text)> BemerkungenZuAnschluessen(SchachtRecord record)
    {
        var gesehen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var quellen = new List<string>();
        quellen.AddRange((Wert(record, "Bemerkungen") ?? "").Split('\n'));
        foreach (var zeile in (Wert(record, "Primaere Schaeden") ?? "").Split('\n'))
        {
            var text = zeile.Trim();
            if (text.StartsWith("Bemerkungen:", StringComparison.OrdinalIgnoreCase))
                quellen.Add(text["Bemerkungen:".Length..]);
        }

        foreach (var quelle in quellen)
        {
            var m = BemerkungAnschluss.Match(quelle);
            if (!m.Success)
                continue;
            var art = m.Groups["art"].Value.ToLowerInvariant();
            var nr = int.Parse(m.Groups["nr"].Value, CultureInfo.InvariantCulture);
            var text = m.Groups["t"].Value.Trim();
            if (gesehen.Add(art + "|" + nr + "|" + text))
                yield return (art, nr, text);
        }
    }

    /// <summary>Hinweistext einer Hinweisflaeche: Code, und bei bekanntem Katalog der Klartext.</summary>
    private static string Hinweistext(ProtocolEntry entry, ICodeCatalogProvider? catalog)
    {
        var code = string.IsNullOrWhiteSpace(entry.Code) ? "-" : entry.Code.Trim();
        var klartext = ObservationZustandBuilder.Build(entry, catalog);
        return string.IsNullOrWhiteSpace(klartext) || klartext == "-" ? code : $"{code} — {klartext}";
    }

    /// <summary>«vorhanden», «Leiter», «Steigeisen» = ja; «fehlt», «nicht notwendig», «nein» = nein; leer = nicht erfasst.</summary>
    private static bool? Steighilfe(string? wert)
    {
        var text = (wert ?? "").Trim();
        if (text.Length == 0)
            return null;
        if (text.StartsWith("fehlt", StringComparison.OrdinalIgnoreCase)
            || text.StartsWith("nicht", StringComparison.OrdinalIgnoreCase)
            || text.StartsWith("nein", StringComparison.OrdinalIgnoreCase)
            || text.StartsWith("kein", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Liest die Schachttiefe als Meterwert. Der Vorlagen-/PDF-Import normiert
    /// <c>Schachttiefe</c> bereits auf eine reine Meterzahl ohne Einheit; ein von Hand mit
    /// Einheit erfasster Wert ("2.40 m") wird zusaetzlich toleriert. Andere Einheiten (cm, mm)
    /// werden NICHT geraten — ohne belegte Schreibweise waere eine Umrechnung eine erfundene
    /// Annahme.
    /// </summary>
    internal static decimal? ParseTiefeMeter(string? text)
    {
        var wert = (text ?? string.Empty).Trim();
        if (wert.Length == 0)
            return null;

        if (wert.EndsWith("m", StringComparison.OrdinalIgnoreCase))
            wert = wert[..^1].TrimEnd();

        return FachzahlParser.TryParseMeasurement(wert, out var zahl) && zahl > 0 ? zahl : null;
    }

    /// <summary>Millimeter als ganze Zahl; ein Meterwert unter 20 (z.B. «0.66») wird als Meter gelesen.</summary>
    internal static int? ParseMm(string? text)
    {
        var wert = (text ?? string.Empty).Trim();
        if (wert.Length == 0)
            return null;
        if (wert.EndsWith("mm", StringComparison.OrdinalIgnoreCase))
            wert = wert[..^2].TrimEnd();

        if (!FachzahlParser.TryParseMeasurement(wert, out var zahl) || zahl <= 0)
            return null;

        var mm = zahl < 20m ? zahl * 1000m : zahl;
        return (int)Math.Round(mm, MidpointRounding.AwayFromZero);
    }

    private static string? Wert(SchachtRecord record, string feld)
        => record.GetFieldValue(SchachtFeldnamen.Feld(record, feld));

    private static string? Leer(string? wert)
        => string.IsNullOrWhiteSpace(wert) ? null : wert.Trim();
}
