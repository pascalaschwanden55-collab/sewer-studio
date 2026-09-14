using System.Globalization;
using AuswertungPro.Next.Application.Xtf;

namespace AuswertungPro.Next.Application.UseCases.Xtf;

/// <summary>Eine Zeile der Vorschau: welches Objekt, welches Feld, was stand da, was kommt.</summary>
public sealed record XtfVorschauZeile(string Objekt, string Feld, string Alt, string Neu);

/// <summary>
/// Was der Nutzer vor dem Schreiben sieht — oder als Fehler nach einer gescheiterten
/// Pruefung. Eine Zeile Zusammenfassung, eine Tabelle Alt/Neu, hoechstens drei sichtbare
/// Warnungen und der vollstaendige Bericht unter "Details". Reine Darstellung des Plans.
/// </summary>
public sealed record XtfExportVorschau(
    string Titel,
    string Zusammenfassung,
    IReadOnlyList<XtfVorschauZeile> Zeilen,
    IReadOnlyList<string> Warnungen,
    string Details,
    bool IstFehler)
{
    public const int SichtbareWarnungen = 3;
    public const string Leer = "–";

    /// <summary>
    /// Die ersten drei Warnungen; der Rest wird nur gezaehlt und steht in den Details.
    /// Gleichartige Feldluecken werden vorher zusammengefasst — Buerglen meldete 483 Zeilen
    /// fuer knapp zehn Feldarten, sichtbar waren drei beliebige davon.
    /// </summary>
    public IReadOnlyList<string> KurzeWarnungen
    {
        get
        {
            var gebuendelt = Buendle(Warnungen);
            if (gebuendelt.Count <= SichtbareWarnungen)
                return gebuendelt;

            var kurz = gebuendelt.Take(SichtbareWarnungen).ToList();
            kurz.Add($"… und {gebuendelt.Count - SichtbareWarnungen} weitere (siehe Details)");
            return kurz;
        }
    }

    /// <summary>Mehrfach gemeldete Feldluecken werden zu einer Zeile mit Anzahl.</summary>
    private static IReadOnlyList<string> Buendle(IReadOnlyList<string> warnungen)
    {
        var ergebnis = new List<string>();
        var stelle = new Dictionary<string, int>(StringComparer.Ordinal);
        var anzahl = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var warnung in warnungen)
        {
            var feld = Lueckenfeld(warnung);
            if (feld is null) { ergebnis.Add(warnung); continue; }
            if (stelle.TryGetValue(feld, out var pos))
                ergebnis[pos] = $"{feld}: {++anzahl[feld]} Angaben ohne Normfeld in dieser XTF (siehe Details)";
            else { stelle[feld] = ergebnis.Count; anzahl[feld] = 1; ergebnis.Add(warnung); }
        }

        return ergebnis;
    }

    /// <summary>Der Feldname aus «objekt «name»: Feld = „wert“ fehlt in der XTF …»; sonst null.</summary>
    private static string? Lueckenfeld(string warnung)
    {
        if (!warnung.Contains("fehlt in der XTF", StringComparison.Ordinal)) return null;
        var namensende = warnung.IndexOf("»: ", StringComparison.Ordinal);
        if (namensende < 0) return null;
        var rest = warnung[(namensende + 3)..];
        var gleich = rest.IndexOf(" = ", StringComparison.Ordinal);
        return gleich <= 0 ? null : rest[..gleich];
    }

    public bool HatZeilen => Zeilen.Count > 0;
    public bool HatWarnungen => Warnungen.Count > 0;

    /// <summary>Vorschau fuer "Bestehende Katasterdaten aktualisieren" aus den geprueften Plaenen.</summary>
    public static XtfExportVorschau AusRevision(IReadOnlyList<XtfRevisionPlan> plaene, string bericht)
    {
        ArgumentNullException.ThrowIfNull(plaene);

        var geaendert = plaene.Sum(p => p.AnzahlGeaendert);
        var neu = plaene.Sum(p => p.AnzahlNeu);
        var entfernt = plaene.Sum(p => p.AnzahlEntfernt);
        var zusammenfassung =
            $"{geaendert} {(geaendert == 1 ? "Objekt" : "Objekte")} geändert · {neu} neu · {entfernt} entfernt";

        var zeilen = new List<XtfVorschauZeile>();
        foreach (var position in plaene.SelectMany(p => p.Positionen))
        {
            if (position.Art == XtfRevisionAenderung.Unveraendert)
                continue;

            var objekt = Objektname(position);
            if (position.Art == XtfRevisionAenderung.Entfernt)
            {
                zeilen.Add(new XtfVorschauZeile(objekt, "Befund", "vorhanden", "(entfernt)"));
                continue;
            }

            zeilen.AddRange(Feldzeilen(objekt, position.Felder));
        }

        return new XtfExportVorschau(
            "Bestehende Katasterdaten aktualisieren",
            zusammenfassung,
            zeilen,
            plaene.SelectMany(p => p.Warnungen).ToList(),
            bericht ?? "",
            IstFehler: false);
    }

    /// <summary>
    /// Vorschau aus dem Bericht. Eine vollstaendige Lieferung hat keine Alt-Werte und damit
    /// keine Tabelle; eine Aenderungslieferung an Originalkennungen bringt ihre Feldauftraege mit.
    /// </summary>
    public static XtfExportVorschau AusBericht(string titel, string bericht,
        IReadOnlyList<XtfAenderungsZeile>? auftraege = null)
    {
        var zeilen = (bericht ?? "").Split('\n').Select(z => z.TrimEnd('\r')).ToList();
        var zusammenfassung = zeilen.FirstOrDefault(z => z.StartsWith("In die Datei:", StringComparison.Ordinal))
            ?? zeilen.FirstOrDefault(z => !string.IsNullOrWhiteSpace(z))
            ?? "";
        if (auftraege is { Count: > 0 })
            zusammenfassung += $"\n{auftraege.Count} {(auftraege.Count == 1 ? "Feldauftrag" : "Feldaufträge")} an bestehenden Katasterobjekten; die Originalkennungen bleiben erhalten.";
        var luecken = zeilen.Where(z => z.Contains("fehlt in der XTF", StringComparison.Ordinal)).ToArray();
        if (luecken.Length > 0)
        {
            zusammenfassung += $"\nAchtung: {luecken.Length} erfasste Angaben oder Quellobjekte fehlen in dieser XTF als Normfeld.";
            zusammenfassung += bericht?.Contains("Erfasste_Angaben mitgeliefert", StringComparison.Ordinal) == true
                ? " Sie gehen unverändert im Zusatzmodell mit; Einzelheiten stehen im Bericht."
                : " Einzelheiten stehen im Bericht.";
        }

        return new XtfExportVorschau(titel, zusammenfassung, Auftragszeilen(auftraege), luecken, bericht ?? "", IstFehler: false);
    }

    /// <summary>Feldnamen in Klartext; ein geleertes Feld heisst «(entfernt)».</summary>
    private static IReadOnlyList<XtfVorschauZeile> Auftragszeilen(IReadOnlyList<XtfAenderungsZeile>? auftraege)
        => auftraege is null ? [] : auftraege.Select(a => new XtfVorschauZeile(
            a.Objekt,
            a.Feld.StartsWith("Beziehung:", StringComparison.Ordinal) ? "Bauwerksbezug" : Feldname(a.Feld),
            Wert(a.Alt),
            a.Neu.Length == 0 && a.Alt.Length > 0 ? "(entfernt)" : Wert(a.Neu))).ToArray();

    /// <summary>Gescheiterte Pruefung oder gescheitertes Schreiben: kurz oben, der Rest in den Details.</summary>
    public static XtfExportVorschau Fehler(string titel, string kurz, string details)
        => new(titel, kurz, [], [], details ?? "", IstFehler: true);

    private static string Objektname(XtfRevisionPosition position)
    {
        if (!string.IsNullOrWhiteSpace(position.Objekt))
            return $"{position.Objekt} {position.HaltungName}";

        var meter = position.Meter is { } m ? $" bei {m.ToString("0.0", CultureInfo.InvariantCulture)} m" : "";
        return $"Befund {position.Code}{meter} ({position.HaltungName})";
    }

    /// <summary>Felder in Klartext; Dimension1 und Dimension2 desselben Objekts werden zu einer Zeile.</summary>
    private static IEnumerable<XtfVorschauZeile> Feldzeilen(string objekt, IReadOnlyList<XtfRevisionFeld> felder)
    {
        var dim1 = felder.FirstOrDefault(f => f.Name == "Dimension1");
        var dim2 = felder.FirstOrDefault(f => f.Name == "Dimension2");
        if (dim1 is not null && dim2 is not null)
        {
            yield return new XtfVorschauZeile(objekt, "Dimension",
                $"{Wert(dim1.Alt)} × {Wert(dim2.Alt)}", $"{NeuWert(dim1)} × {NeuWert(dim2)}");
        }

        foreach (var feld in felder)
        {
            if (dim1 is not null && dim2 is not null && (feld == dim1 || feld == dim2))
                continue;

            yield return new XtfVorschauZeile(objekt, Feldname(feld.Name), Wert(feld.Alt), NeuWert(feld));
        }
    }

    private static string NeuWert(XtfRevisionFeld feld)
        => feld.Aktion == XtfRevisionFeldAktion.Entfernen ? "(entfernt)" : Wert(feld.Neu);

    private static string Wert(string? wert) => string.IsNullOrWhiteSpace(wert) ? Leer : wert;

    /// <summary>"Lichte_Hoehe" -> "Lichte Höhe", "BaulicherZustand" -> "Baulicher Zustand", "Quantifizierung1" -> "Quantifizierung 1".</summary>
    private static string Feldname(string name)
    {
        var text = name.Replace('_', ' ');
        var mitAbstand = new System.Text.StringBuilder(text.Length + 4);
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (i > 0 && text[i - 1] != ' ' && (char.IsUpper(c) && char.IsLower(text[i - 1]) || char.IsDigit(c) && !char.IsDigit(text[i - 1])))
                mitAbstand.Append(' ');
            mitAbstand.Append(c);
        }

        return mitAbstand.ToString()
            .Replace("Hoehe", "Höhe", StringComparison.Ordinal)
            .Replace("Laenge", "Länge", StringComparison.Ordinal)
            .Replace("Ueber", "Über", StringComparison.Ordinal)
            .Replace("ueber", "über", StringComparison.Ordinal);
    }
}
