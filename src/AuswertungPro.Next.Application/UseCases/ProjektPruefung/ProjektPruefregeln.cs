using System.Globalization;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Application.Media;
using AuswertungPro.Next.Application.Export;
using AuswertungPro.Next.Application.UseCases.Objektakten;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Application.UseCases.ProjektPruefung;

/// <summary>Fuenf lesende Pruefungen. Vorhandene Eingabe- und Hoehenregeln bleiben die einzige Quelle.</summary>
public static class ProjektPruefregeln
{
    /// <param name="tempOrdner">Nur fuer Tests: simulierte Temp-Wurzeln; sonst die echten.</param>
    public static ProjektPruefergebnis Pruefe(Project projekt, Func<string, string?> dateifehler,
        CancellationToken ct = default, IReadOnlyList<string>? tempOrdner = null)
    {
        var punkte = new List<ProjektPruefpunkt>();
        var temp = tempOrdner ?? BefundfotoTempOrt.Standardwurzeln();
        foreach (var h in projekt.Data)
        {
            ct.ThrowIfCancellationRequested();
            var name = h.GetFieldValue(FieldKeys.HoldingName);
            var b = new ObjektaktenBearbeitung(projekt, h.Id, "haltung");
            PruefeObjekt(b, name, h.GetFieldValue, h.Protocol?.Current);
            PruefeWebGis(b, name, false, h.GetFieldValue, "Normschacht");
            var laengeText = h.GetFieldValue(FieldKeys.HoldingLengthMeters);
            var hatLaenge = FachzahlParser.TryParseMeasurement(laengeText, out var laenge) && laenge > 0;
            if (!hatLaenge && (laengeText.Length > 0 || h.Protocol?.Current?.Entries.Any(e => !e.IsDeleted && e.MeterStart.HasValue) == true))
                Add(b, name, ProjektPruefbereich.Meterangaben, "Eine gültige positive Haltungslänge fehlt.", feld: "haltung.length");
            foreach (var e in h.Protocol?.Current?.Entries ?? [])
            {
                if (e.IsDeleted) continue;
                ct.ThrowIfCancellationRequested();
                if (e.MeterStart is { } von && (!double.IsFinite(von) || von < 0)
                    || e.MeterEnd is { } bis && (!double.IsFinite(bis) || bis < 0)
                    || e.MeterEnd.HasValue && (!e.MeterStart.HasValue || e.MeterEnd < e.MeterStart))
                    Add(b, name, ProjektPruefbereich.Meterangaben, $"{e.Code}: Meterbereich ist ungültig.", e.EntryId);
                else if (hatLaenge && Math.Max(e.MeterStart ?? 0, e.MeterEnd ?? 0) > (double)laenge + ImportPlausibilityValidator.MeterTolerance)
                    Add(b, name, ProjektPruefbereich.Meterangaben,
                        $"{e.Code} bei {Math.Max(e.MeterStart ?? 0, e.MeterEnd ?? 0).ToString("0.##", CultureInfo.GetCultureInfo("de-CH"))} m liegt hinter der Haltungslänge {laengeText} m (Toleranz 1 m).", e.EntryId);
            }
        }
        foreach (var s in projekt.SchaechteData)
        {
            ct.ThrowIfCancellationRequested();
            // Ueber alle Schreibweisen: Handwert (auch bewusst leer) vor Importwert (Folgepaket PR #80).
            string Wert(string key) => SchachtFeldnamen.Wert(s, key);
            var name = Wert("Schachtnummer");
            var b = new ObjektaktenBearbeitung(projekt, s.Id, "schacht");
            PruefeObjekt(b, name, Wert, s.Protocol?.Current);
            PruefeWebGis(b, name, true, Wert, AbwasserbauwerkVokabular.Klasse(Wert(FieldKeys.ShaftStructureType), Wert("Funktion")));
            var hoehen = SchachtHoehenRechnung.Fuer(b);
            if (hoehen.Warnung.Length > 0)
                Add(b, name, ProjektPruefbereich.Schachthoehen, hoehen.Warnung, feld: SchachtHoehenRechnung.Tiefenfeld);
        }
        return new(punkte, projekt.Data.Count, projekt.SchaechteData.Count);

        void Add(ObjektaktenBearbeitung b, string name, ProjektPruefbereich bereich, string text,
            Guid? eintrag = null, Guid? akte = null, string? feld = null, string? speicher = null)
            => punkte.Add(new(bereich, b.Art, b.WurzelId, name, text, eintrag, akte, feld, speicher));

        // Seit 23.09.2026: Was ins WebGIS geht, muss ein WebGIS-Begriff sein (Schritt A). Altwerte
        // bleiben stehen; hier werden sie zum Korrigieren gezeigt.
        void PruefeWebGis(ObjektaktenBearbeitung b, string name, bool schacht, Func<string, string> wert, string? klasse)
        {
            IEnumerable<string> felder = schacht
                ? WebGisBegriffe.SchachtFelder.Concat(klasse == "Normschacht"
                    ? new[] { WebGisBegriffe.SchachtFunktion } : Array.Empty<string>())
                : WebGisBegriffe.HaltungFelder;
            foreach (var feld in felder)
            {
                var text = wert(feld).Trim();
                if (text.Length > 0 && !WebGisBegriffe.Fuer(schacht, feld)!.Kennt(text))
                    Add(b, name, ProjektPruefbereich.Eingabefelder,
                        $"{feld}: «{text}» ist kein WebGIS-Begriff und wird nicht ins WebGIS übertragen.", speicher: feld);
            }
            // Entscheid A (23.09.2026): Masse bleiben wie gemessen; nur Zahlen der WebGIS-Liste sind sendbar.
            var masse = schacht
                ? new[] { (Feld: FieldKeys.ShaftDimension1Mm, Katalog: "schacht-C09"), (Feld: FieldKeys.ShaftDimension2Mm, Katalog: "schacht-C09") }
                : new[] { (Feld: FieldKeys.NominalDiameterMm, Katalog: "haltung-C08"), (Feld: FieldKeys.ClearWidthMm, Katalog: "haltung-C08") };
            foreach (var (feld, katalog) in masse)
            {
                var text = wert(feld).Trim();
                var liste = FieldCatalog.Objektfelder.Auswahl(katalog)?.Eintraege.Select(e => e.Label).ToHashSet(StringComparer.Ordinal);
                if (text.Length > 0 && liste is not null && !liste.Contains(text))
                    Add(b, name, ProjektPruefbereich.Eingabefelder,
                        $"{feld}: «{text}» steht nicht in der WebGIS-Liste und kann nicht übertragen werden.", speicher: feld);
            }
        }

        void PruefeObjekt(ObjektaktenBearbeitung b, string name, Func<string, string> wert, ProtocolRevision? revision)
        {
            var gesehen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var key in new[] { FieldKeys.Link, FieldKeys.PdfPath, FieldKeys.PdfEigen, FieldKeys.PdfAll })
            {
                var raw = wert(key);
                IEnumerable<string> pfade = key == FieldKeys.PdfAll ? StoredFileListParser.Parse(raw) : new[] { raw };
                foreach (var pfad in pfade)
                {
                    ct.ThrowIfCancellationRequested();
                    if (string.IsNullOrWhiteSpace(pfad) || !gesehen.Add(pfad.Trim())) continue;
                    var fehler = dateifehler(pfad);
                    if (fehler is not null) Add(b, name, ProjektPruefbereich.Dateien, $"{pfad}: {fehler}", speicher: key);
                }
            }
            foreach (var e in revision?.Entries ?? [])
                if (b.Art == "haltung" && !e.IsDeleted && e.Ai is { Accepted: false })
                    Add(b, name, ProjektPruefbereich.KiBefunde, $"{e.Code}: KI-Vorschlag noch nicht bestätigt.", e.EntryId);
            // Deepscan 02.10.2026, R3: Befundfotos im Temp-Ordner gehen beim Aufräumen verloren
            // (Fall 12.09.2026). Nur diese Fotos werden gelesen, nicht jedes Projektfoto.
            foreach (var e in revision?.Entries ?? [])
            {
                if (b.Art != "haltung" || e.IsDeleted) continue;
                foreach (var foto in e.FotoPaths ?? [])
                {
                    ct.ThrowIfCancellationRequested();
                    if (!BefundfotoTempOrt.LiegtImTemp(foto, temp) || !gesehen.Add(foto.Trim())) continue;
                    var fehler = dateifehler(foto);
                    Add(b, name, ProjektPruefbereich.Dateien, fehler is null
                        ? $"{foto}: Befundfoto liegt im Temp-Ordner und geht beim Aufräumen von Windows verloren. Bitte ins Projekt übernehmen oder neu aufnehmen."
                        : $"{foto}: {fehler} Das Befundfoto lag im Temp-Ordner.", e.EntryId);
                }
            }
            foreach (var a in b.Verbund)
            foreach (var f in ObjektaktenBestandsfelder.Fuer(b, a).Where(f => !f.NurLesen))
            {
                ct.ThrowIfCancellationRequested();
                try { ObjektFeldPruefung.Pruefe(f, b.Lies(a, f)); }
                catch (InvalidOperationException ex)
                { Add(b, name, ProjektPruefbereich.Eingabefelder, $"{a.Art}: {UserError.DescribeInputHint(ex, "Projektprüfung Eingabefeld")}", akte: a.Id, feld: f.Id); }
            }
        }
    }
}
