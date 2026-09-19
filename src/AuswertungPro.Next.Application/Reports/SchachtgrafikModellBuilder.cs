using System.Globalization;
using System.Text.RegularExpressions;
using AuswertungPro.Next.Application.Common;
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
///   werden ueber Richtung und DN verbunden — nur wenn genau eine Haltung passt.
/// - Richtung je Haltung aus der Lage, Kote je Haltung aus den Koten; fehlt die Tabellentiefe,
///   ergibt Deckelkote minus Rohrsohlenkote die Tiefe (Quelle «Kataster»).
/// - Schaeden: je Protokolleintrag einer, verortet ueber <see cref="SchachtSchadenOrtRegel"/>.
///   Eine Bemerkung «Einlauf 3 ausgebrochen» wird zum Schaden an Anschluss 3, wenn es ihn gibt.
/// Nichts davon schreibt zurueck; nichts wird geraten — offene Stellen stehen in den Hinweisen.
/// </summary>
public static class SchachtgrafikModellBuilder
{
    private static readonly Regex BemerkungAnschluss = new(
        @"^\s*(?:Einlauf|Auslauf|Anschluss)\s*(?<nr>\d{1,2})\s*[:\-]?\s*(?<t>\S.*?)\s*$",
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

    private static List<SchachtgrafikAnschluss> Anschluesse(
        SchachtRecord record,
        IReadOnlyList<HaltungRecord>? haltungen,
        string? schachtnummer,
        Lookup.SchachtLage? lage,
        SchachtKoten? koten,
        List<string> hinweise)
    {
        var seiten = new List<(HaltungRecord H, Haltungsseite Seite, string Name, int? Dn, string? Material)>();
        foreach (var h in haltungen ?? [])
        {
            var seite = SchachtHaltungsseite.Bestimme(h, schachtnummer);
            if (seite == Haltungsseite.Keine)
                continue;
            seiten.Add((
                h,
                seite,
                (h.GetFieldValue(FieldKeys.HoldingName) ?? "").Trim(),
                ParseMm(h.GetFieldValue(FieldKeys.NominalDiameterMm)),
                Leer(h.GetFieldValue(FieldKeys.PipeMaterial))));
        }

        var verwendet = new HashSet<HaltungRecord>();
        var liste = new List<SchachtgrafikAnschluss>();
        var tabelle = (record.Anschluesse ?? []).Where(a => a.Nr > 0).OrderBy(a => a.Nr).ToList();

        foreach (var zeile in tabelle)
        {
            var kandidaten = seiten
                .Where(s => !verwendet.Contains(s.H) && s.Seite == (zeile.IstAuslauf ? Haltungsseite.Oben : Haltungsseite.Unten))
                .ToList();
            if (!string.IsNullOrWhiteSpace(zeile.Haltungsname))
            {
                kandidaten = seiten
                    .Where(s => !verwendet.Contains(s.H) && string.Equals(s.Name, zeile.Haltungsname.Trim(), StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }
            else if (zeile.DnMm is { } dn && kandidaten.Count > 1)
            {
                var gleicheDn = kandidaten.Where(k => k.Dn == dn).ToList();
                if (gleicheDn.Count > 0)
                    kandidaten = gleicheDn;
            }

            var treffer = kandidaten.Count == 1 ? kandidaten[0] : default;
            if (treffer.H is not null)
                verwendet.Add(treffer.H);

            liste.Add(Erzeuge(
                zeile.Nr,
                zeile.IstAuslauf,
                zeile.DnMm ?? treffer.Dn,
                zeile.TiefeM,
                zeile.TiefeM is null ? null : "Protokoll",
                zeile.Material ?? treffer.Material,
                treffer.H is null ? Leer(zeile.Haltungsname) : treffer.Name,
                imProjekt: treffer.H is not null,
                lage,
                koten));
        }

        var naechsteNr = tabelle.Count > 0 ? tabelle.Max(a => a.Nr) + 1 : 1;
        foreach (var s in seiten.Where(s => !verwendet.Contains(s.H)).OrderBy(s => s.Seite == Haltungsseite.Oben ? 0 : 1).ThenBy(s => s.Name, StringComparer.OrdinalIgnoreCase))
        {
            liste.Add(Erzeuge(naechsteNr++, s.Seite == Haltungsseite.Oben, s.Dn, null, null, s.Material, s.Name, imProjekt: true, lage, koten));
        }

        foreach (var a in liste.Where(a => !a.ImProjekt))
            hinweise.Add($"{a.Kennung} steht im Protokoll, aber nicht im Projekt");
        if (lage is null && liste.Count > 0)
            hinweise.Add("Richtungen nicht erfasst: Grundriss schematisch");
        else
        {
            foreach (var a in liste.Where(a => a.AzimutGrad is null))
                hinweise.Add($"Richtung nicht erfasst: {a.Kennung}");
        }

        return liste;
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
        Lookup.SchachtLage? lage,
        SchachtKoten? koten)
    {
        double? azimut = null;
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

        return new SchachtgrafikAnschluss(nr, istAuslauf, dn, tiefe, tiefeQuelle, material, haltungsname, azimut, kote, imProjekt);
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

        foreach (var (nr, text) in BemerkungenZuAnschluessen(record))
        {
            if (!anschluesse.Any(a => a.Nr == nr))
                continue;
            roh.Add((
                SchachtBauteil.Anschluss,
                nr,
                SchachtSchadenKategorieRegel.KategorieAusText(text) ?? "default",
                $"Anschluss {nr}: {text} (Bemerkung)"));
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
    /// den «Bemerkungen: …»-Zeilen der primaeren Schaeden. Nur mit Nummer; alles andere bleibt
    /// Freitext.
    /// </summary>
    private static IEnumerable<(int Nr, string Text)> BemerkungenZuAnschluessen(SchachtRecord record)
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
            var nr = int.Parse(m.Groups["nr"].Value, CultureInfo.InvariantCulture);
            var text = m.Groups["t"].Value.Trim();
            if (gesehen.Add(nr + "|" + text))
                yield return (nr, text);
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
