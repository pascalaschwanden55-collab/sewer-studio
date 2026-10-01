using System.Security.Cryptography;
using System.Text.Json;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Application.UseCases.Import.Quellen;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Infrastructure.Import.Xtf.VsaKek;

/// <summary>Eine Schachtbegehung samt ihren Schachtschaeden.</summary>
internal sealed record XtfSchachtUntersuchung(
    string Nummer,
    string Zeitpunkt,
    string Operateur,
    string Erfassungsart,
    IReadOnlyList<ProtocolEntry> Eintraege,
    string? ImportFingerprint = null);

/// <summary>
/// Was eine VSA-KEK-Datei fachlich hergibt — nach Bauwerksart getrennt.
/// <paramref name="Weitere"/> sind die Untersuchungen einer Haltung neben ihrer
/// Haupt-Untersuchung (siehe <see cref="VsaKekUntersuchungsWahl"/>).
/// </summary>
internal sealed record XtfVsaKekErgebnis(
    List<HaltungRecord> Haltungen,
    List<XtfSchachtUntersuchung> Schaechte,
    List<XtfOffeneUntersuchung> Offene,
    int Untersuchungen,
    List<XtfWeitereUntersuchung> Weitere);

/// <summary>
/// Schritt 3 des VSA-KEK-Imports: bildet die eingeordneten Untersuchungen ab. Hier stehen
/// die fachlichen Regeln — welche Angabe der Haupt-Untersuchung in welches Haltungsfeld geht
/// (Name, Laenge, Datum, Schaechte, Befunde, Primaerschaeden, Video, Richtung, Bemerkung),
/// wie Schachtschaeden zu Protokollzeilen werden und der Importbeleg. Alle Werte tragen
/// <see cref="FieldSource.Xtf"/>; Handwertschutz und Protokollablage uebernimmt danach die
/// Uebernahme ins Projekt.
/// </summary>
internal static class VsaKekAbbildung
{
    public static XtfVsaKekErgebnis Baue(
        VsaKekBezuege bezuege,
        IReadOnlyList<VsaKekUntersuchungsWahl.Gruppe<VsaKekUntersuchung>> gruppen,
        string sourcePath,
        string modellName)
    {
        ArgumentNullException.ThrowIfNull(bezuege);
        ArgumentNullException.ThrowIfNull(gruppen);

        var schaechte = bezuege.Schachtbegehungen.Select(BaueSchachtbegehung).ToList();

        // Nur die Haupt-Untersuchung liefert Felder, Befunde und Primaere_Schaeden; jede
        // weitere wird nach der Uebernahme als Protokollfassung abgelegt.
        var records = new List<HaltungRecord>();
        var weitere = new List<XtfWeitereUntersuchung>();
        foreach (var gruppe in gruppen)
        {
            bezuege.BefundeJeUntersuchung.TryGetValue(gruppe.Haupt.Tid, out var findings);
            bezuege.VideoJeUntersuchung.TryGetValue(gruppe.Haupt.Tid, out var videoLink);
            var record = BaueHaltung(gruppe.Haupt, findings, videoLink, sourcePath, modellName);
            if (Gegenvideo(gruppe, bezuege.VideoJeUntersuchung, videoLink) is { } gegenvideo)
                record.SetFieldValue("Link_G", gegenvideo, FieldSource.Xtf, userEdited: false);
            records.Add(record);

            foreach (var w in gruppe.Weitere)
            {
                bezuege.BefundeJeUntersuchung.TryGetValue(w.Tid, out var befunde);
                bezuege.VideoJeUntersuchung.TryGetValue(w.Tid, out var video);
                weitere.Add(new XtfWeitereUntersuchung(
                    gruppe.Haltung, gruppe.Haupt.Tid, gruppe.Haupt.Zeitpunkt, findings?.Count ?? 0,
                    w.Tid, w.Zeitpunkt, befunde ?? new List<VsaFinding>(), video,
                    // Gleiche Rechnung wie der Schacht-Fingerabdruck: nur Dateiinhalt, keine Pfade.
                    Importbeleg(w)));
            }
        }

        return new XtfVsaKekErgebnis(records, schaechte, bezuege.Offene.ToList(), bezuege.Untersuchungen, weitere);
    }

    /// <summary>
    /// Das Video der Gegenbefahrung fuer <c>Link_G</c> — dort fuehrt auch der WinCan-Import
    /// (<c>Befahrungsrollen</c>) und die Kanalverteilung das Gegeninspektions-Video. Seit
    /// 01.10.2026. Nur wenn genau eine weitere Untersuchung belegt aus der Gegenrichtung der
    /// Haupt-Untersuchung kommt (beide Fliessrichtungen bekannt und verschieden) und ein
    /// eigenes Video hat; zwei Kandidaten werden nicht geraten. Ein von Hand gesetztes
    /// <c>Link_G</c> schuetzt die Uebernahme ins Projekt (wie bei <c>Link</c>). Das Video
    /// bleibt zusaetzlich an der Protokollfassung der Gegenbefahrung.
    /// </summary>
    private static string? Gegenvideo(
        VsaKekUntersuchungsWahl.Gruppe<VsaKekUntersuchung> gruppe,
        IReadOnlyDictionary<string, string> videos,
        string? hauptvideo)
    {
        var hauptrichtung = Richtung(gruppe.Haupt.Fliessrichtung);
        if (hauptrichtung == 0)
            return null;

        var kandidaten = gruppe.Weitere
            .Where(w => Richtung(w.Fliessrichtung) == -hauptrichtung)
            .Where(w => GleicheKampagne(gruppe.Haupt.Zeitpunkt, w.Zeitpunkt))
            .Select(w => videos.TryGetValue(w.Tid, out var video) ? video : null)
            .Where(video => !string.IsNullOrWhiteSpace(video)
                            && !string.Equals(video, hauptvideo, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return kandidaten.Count == 1 ? kandidaten[0] : null;
    }

    /// <summary>
    /// Entscheid Pascal 01.10.2026: Eine Gegenbefahrung gehoert zur selben Kampagne, beide
    /// Untersuchungen liegen hoechstens 30 Tage auseinander. Eine Befahrung aus der Gegenrichtung
    /// aus einem anderen Jahr ist keine Gegenbefahrung. Ohne glaubwuerdiges Datum (fehlt,
    /// unlesbar, WinCan-Platzhalter) wird nicht geraten.
    /// </summary>
    private static bool GleicheKampagne(string? hauptZeitpunkt, string? weitererZeitpunkt)
    {
        var haupt = UntersuchungsAuswahl.Glaubwuerdig(VsaKekUntersuchungsWahl.LiesZeitpunkt(hauptZeitpunkt));
        var weitere = UntersuchungsAuswahl.Glaubwuerdig(VsaKekUntersuchungsWahl.LiesZeitpunkt(weitererZeitpunkt));
        return haupt is { } h && weitere is { } w && Math.Abs((h - w).TotalDays) <= GegenbefahrungHoechstabstandTage;
    }

    private const int GegenbefahrungHoechstabstandTage = 30;

    /// <summary>+1 in, -1 gegen Fliessrichtung, 0 unbekannt.</summary>
    private static int Richtung(string? fliessrichtung)
        => XtfValueNormalizer.NormalizeInspectionDirection(fliessrichtung) switch
        {
            "In Fliessrichtung" => 1,
            "Gegen Fliessrichtung" => -1,
            _ => 0
        };

    /// <summary>
    /// Vollstaendigkeit einer Haltungsuntersuchung fuer <see cref="VsaKekUntersuchungsWahl"/>.
    /// Abgebrochen heisst: ein Kanalschaden mit Abbruchcode BDC* (die Datei kennt kein eigenes
    /// Abbruchfeld). Die Strecke ist <c>Inspizierte_Laenge</c>, ohne sie die groesste
    /// Schadensdistanz.
    /// </summary>
    public static VsaKekUntersuchungsWahl.Merkmale Merkmale(VsaKekUntersuchung u)
    {
        var abgebrochen = u.Schaeden.Any(s => (s.Schadencode ?? "").Trim()
            .StartsWith(ProtocolBoundaryService.AbortPrefix, StringComparison.OrdinalIgnoreCase));
        var laenge = XtfValueNormalizer.TryParseDouble(u.InspizierteLaenge, out var inspiziert)
            ? inspiziert
            : u.Schaeden.Select(s => XtfValueNormalizer.TryParseDouble(s.Distanz, out var d) ? d : 0.0).DefaultIfEmpty(0.0).Max();
        return new VsaKekUntersuchungsWahl.Merkmale(abgebrochen, laenge, u.Zeitpunkt);
    }

    /// <summary>
    /// Der gespeicherte <c>ImportFingerprint</c> einer Untersuchung: SHA-256 ueber ihre
    /// JSON-Form samt Schaeden, ohne Pfade, also unabhaengig vom Lauf. Siehe
    /// <see cref="VsaKekUntersuchung"/> — die Form der Klasse ist Teil gespeicherter Projekte.
    /// </summary>
    private static string Importbeleg(VsaKekUntersuchung u)
        => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(u)));

    private static XtfSchachtUntersuchung BaueSchachtbegehung(VsaKekUntersuchung u)
        => new(
            u.Bezeichnung,
            XtfValueNormalizer.NormalizeDate_yyyymmdd(u.Zeitpunkt),
            u.Operateur,
            u.Erfassungsart,
            BaueSchachteintraege(u),
            Importbeleg(u));

    /// <summary>
    /// Baut den Haltungsdatensatz aus genau einer Untersuchung, ihren Befunden und ihrem Video.
    /// </summary>
    private static HaltungRecord BaueHaltung(VsaKekUntersuchung u, List<VsaFinding>? findings,
        string? videoLink, string sourcePath, string modellName)
    {
        var zeitpunkt = XtfValueNormalizer.NormalizeDate_yyyymmdd(u.Zeitpunkt);
        var primaere = new List<string>();

        if (findings is not null)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var f in findings)
            {
                var code = (f.KanalSchadencode ?? "").Trim().ToUpperInvariant();
                if (code.Length == 0) continue;
                var meter = f.MeterStart;
                var key = $"{code}|{(meter.HasValue ? meter.Value.ToString("F2") : "")}";
                if (!seen.Add(key)) continue;

                var detail = XtfPrimaryDamageFormatter.FormatLine(f);
                if (!string.IsNullOrWhiteSpace(detail))
                    primaere.Add(detail);
            }
        }

        var rec = new HaltungRecord
        {
            // Ankerangabe fuer eine spaetere Revision: aus welcher Datei und welcher
            // Untersuchung diese Haltung stammt.
            XtfHerkunft = new XtfHerkunft
            {
                Datei = Path.GetFileName(sourcePath) ?? "",
                Modell = modellName,
                UntersuchungTid = u.Tid
            }
        };
        rec.SetFieldValue("Haltungsname", u.Bezeichnung, FieldSource.Xtf, userEdited: false);
        if (!string.IsNullOrWhiteSpace(u.InspizierteLaenge)) rec.SetFieldValue("Haltungslaenge_m", u.InspizierteLaenge, FieldSource.Xtf, userEdited: false);
        if (!string.IsNullOrWhiteSpace(zeitpunkt)) rec.SetFieldValue("Datum_Jahr", zeitpunkt, FieldSource.Xtf, userEdited: false);
        // Schacht oben/unten aus der Untersuchung (von-/bisPunktBezeichnung) — VSA_KEK ist Hauptquelle,
        // eine spaetere SIA405-Anreicherung fuellt nur, falls hier leer.
        if (!string.IsNullOrWhiteSpace(u.VonPunkt)) rec.SetFieldValue("Schacht_oben", u.VonPunkt, FieldSource.Xtf, userEdited: false);
        if (!string.IsNullOrWhiteSpace(u.BisPunkt)) rec.SetFieldValue("Schacht_unten", u.BisPunkt, FieldSource.Xtf, userEdited: false);
        if (findings is not null && findings.Count > 0)
            rec.VsaFindings = new List<VsaFinding>(findings);

        // Video-Link aus KEK.Datei (Klasse=Untersuchung) setzen, falls noch kein Link vorhanden
        if (!string.IsNullOrWhiteSpace(videoLink)
            && string.IsNullOrWhiteSpace(rec.GetFieldValue("Link")))
        {
            rec.SetFieldValue("Link", videoLink, FieldSource.Xtf, userEdited: false);
        }

        if (primaere.Count > 0)
        {
            var val = XtfPrimaryDamageFormatter.DeduplicateText(string.Join("\n", primaere));
            rec.SetFieldValue("Primaere_Schaeden", val, FieldSource.Xtf, userEdited: false);
        }

        // NOTE: VSA-Zustandsnote wird NICHT hier berechnet, sondern später durch VsaEvaluationService
        // Die korrekte Berechnung basiert auf VSA-Regeln und allen Schadenscodes pro Haltung

        // maxKlasse wird hier nicht korrekt berechnet - entfernt um falsche Werte zu vermeiden
        // if (maxKlasse > 0)
        // {
        //     rec.SetFieldValue("Zustandsklasse", maxKlasse.ToString(), FieldSource.Xtf, userEdited: false);
        //     rec.SetFieldValue("VSA_Zustandsnote_D", maxKlasse.ToString(), FieldSource.Xtf, userEdited: false);
        // }

        // Inspektionsrichtung aus der Untersuchung (IKAS liefert sie als <Fliessrichtung>).
        var richtung = XtfValueNormalizer.NormalizeInspectionDirection(u.Fliessrichtung);
        if (!string.IsNullOrWhiteSpace(richtung))
            rec.SetFieldValue("Inspektionsrichtung", richtung, FieldSource.Xtf, userEdited: false);

        // Bemerkungen mit Inspektionskontext anreichern. VSA_KEK ist Hauptquelle und darf Bemerkungen
        // setzen. Alle verfuegbaren Kontextangaben einbeziehen (nicht nur wenn Erfassungsart da ist),
        // damit Grund/Witterung/Ausfuehrender/Fahrzeug/Geraet nicht verloren gehen.
        var bemParts = new List<string>();
        if (!string.IsNullOrWhiteSpace(u.Erfassungsart)) bemParts.Add($"Erfassung: {u.Erfassungsart}");
        if (!string.IsNullOrWhiteSpace(u.Grund)) bemParts.Add($"Grund: {u.Grund}");
        if (!string.IsNullOrWhiteSpace(u.Witterung)) bemParts.Add($"Witterung: {u.Witterung}");
        if (!string.IsNullOrWhiteSpace(u.Ausfuehrender)) bemParts.Add($"Ausfuehrender: {u.Ausfuehrender}");
        if (!string.IsNullOrWhiteSpace(u.Fahrzeug)) bemParts.Add($"Fahrzeug: {u.Fahrzeug}");
        if (!string.IsNullOrWhiteSpace(u.Geraet)) bemParts.Add($"Geraet: {u.Geraet}");
        if (bemParts.Count > 0)
        {
            rec.SetFieldValue("Bemerkungen", string.Join(", ", bemParts), FieldSource.Xtf, userEdited: false);
            rec.SetFieldValue("Pruefungsresultat", "", FieldSource.Xtf, userEdited: false);
        }

        return rec;
    }

    /// <summary>
    /// Macht aus den Schachtschaeden einer Begehung Protokolleintraege.
    /// Der Meterwert eines Schachtschadens ist die Tiefe im Schacht, nicht eine
    /// Position entlang einer Haltung — er wird als Punktbefund uebernommen.
    /// </summary>
    private static List<ProtocolEntry> BaueSchachteintraege(VsaKekUntersuchung u)
    {
        var eintraege = new List<ProtocolEntry>();

        foreach (var schaden in u.Schachtschaeden)
        {
            var code = (schaden.Schadencode ?? "").Trim();
            if (code.Length == 0)
                continue;

            var eintrag = new ProtocolEntry
            {
                Code = code,
                Beschreibung = (schaden.Anmerkung ?? "").Trim(),
                Source = ProtocolEntrySource.Imported
            };

            if (XtfValueNormalizer.TryParseDouble(schaden.Distanz, out var tiefe))
            {
                eintrag.MeterStart = tiefe;
                eintrag.MeterEnd = tiefe;
            }

            if (!string.IsNullOrWhiteSpace(schaden.Videozaehlerstand))
                eintrag.Mpeg = schaden.Videozaehlerstand;

            var parameter = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(schaden.Schachtbereich))
                parameter["Schachtbereich"] = schaden.Schachtbereich.Trim();
            if (!string.IsNullOrWhiteSpace(schaden.Quantifizierung1))
                parameter["Quantifizierung1"] = schaden.Quantifizierung1.Trim();
            if (!string.IsNullOrWhiteSpace(schaden.Quantifizierung2))
                parameter["Quantifizierung2"] = schaden.Quantifizierung2.Trim();

            if (parameter.Count > 0 || !string.IsNullOrWhiteSpace(schaden.Einzelschadenklasse))
            {
                eintrag.CodeMeta = new ProtocolEntryCodeMeta
                {
                    Code = code,
                    Parameters = parameter,
                    Severity = string.IsNullOrWhiteSpace(schaden.Einzelschadenklasse)
                        ? null
                        : schaden.Einzelschadenklasse.Trim()
                };
            }

            eintraege.Add(eintrag);
        }

        return eintraege;
    }
}
