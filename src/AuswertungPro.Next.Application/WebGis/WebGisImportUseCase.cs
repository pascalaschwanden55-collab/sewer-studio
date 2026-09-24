using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.WebGis;

/// <summary>Ergebnis von «Übernehmen»: uebernommene Objekte/Akten und gestoppte (im WebGIS seit der Vorschau geaendert).</summary>
public sealed record WebGisHolenErgebnis(int Uebernommen, int Gestoppt);

/// <summary>
/// WebGIS -> SewerStudio: liest je Objekt den WebGIS-Stand, plant Baujahr (nur wenn leer), die Kartenfelder (leer fuellen, Katasterwerte ersetzen: WebGIS vor GeoShop) und die
/// noch fehlenden Sanierungsmassnahmen, und uebernimmt nach Bestaetigung. Schreibt nie ins WebGIS.
/// Die Haltungslaenge wird nie geholt (23.09.2026): In SewerStudio gilt die Laenge des Operateurs.
/// </summary>
public sealed class WebGisImportUseCase
{
    private readonly IGeonisWebGisClient _client;

    public WebGisImportUseCase(IGeonisWebGisClient client)
        => _client = client ?? throw new ArgumentNullException(nameof(client));

    public async Task<WebGisImportPlan> BauePlanAsync(Project projekt, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(projekt);
        var plan = new WebGisImportPlan();
        var gelesen = new List<(WebGisImportEingabe Eingabe, WebGisImportPosition Position, WebGisLesestand? Stand)>();

        foreach (var h in projekt.Data)
        {
            var e = new WebGisImportEingabe
            {
                Objektart = WebGisObjektart.Haltung,
                Bezeichnung = h.GetFieldValue(FieldKeys.HoldingName),
                RecordId = h.Id,
                GespeicherteGlobalId = h.WebGisGlobalId,
                Laenge = h.GetFieldValue(FieldKeys.HoldingLengthMeters),
                Baujahr = h.GetFieldValue(WebGisImportPlanBuilder.FeldBaujahr),
                BaujahrHandwert = h.FieldMeta.GetValueOrDefault(WebGisImportPlanBuilder.FeldBaujahr)?.UserEdited == true,
            };
            e.Felder[WebGisImportPlanBuilder.TypAaFeld] = AkteFeld(projekt, h.Id, "haltung", WebGisImportPlanBuilder.TypAaFeld);
            e.Felder[WebGisImportPlanBuilder.MaterialgruppeFeld(WebGisObjektart.Haltung)] = AkteFeld(projekt, h.Id, "haltung",
                WebGisImportPlanBuilder.MaterialgruppeFeld(WebGisObjektart.Haltung));
            e.Felder[WebGisImportPlanBuilder.BetreiberFeld(WebGisObjektart.Haltung)] = AkteFeld(projekt, h.Id, "haltung",
                WebGisImportPlanBuilder.BetreiberFeld(WebGisObjektart.Haltung));
            foreach (var feld in new[] { WebGisImportPlanBuilder.FeldLaenge, FieldKeys.Owner })
                e.Felder[feld] = new WebGisImportFeld(h.GetFieldValue(feld), IstErsetzbar(h.FieldMeta.GetValueOrDefault(feld)),
                    h.FieldMeta.GetValueOrDefault(feld)?.UserEdited == true);
            foreach (var karte in WebGisHandwertKarte.Felder)
                if (karte.Objektart == WebGisObjektart.Haltung)
                    e.Felder[karte.SewerStudioFeld] = new WebGisImportFeld(
                        h.GetFieldValue(karte.SewerStudioFeld), IstErsetzbar(h.FieldMeta.GetValueOrDefault(karte.SewerStudioFeld)),
                        h.FieldMeta.GetValueOrDefault(karte.SewerStudioFeld)?.UserEdited == true);
            var (pos, stand) = await BaueEineAsync(e, ct).ConfigureAwait(false);
            plan.Positionen.Add(pos);
            gelesen.Add((e, pos, stand));
        }
        foreach (var s in projekt.SchaechteData)
        {
            var e = new WebGisImportEingabe
            {
                Objektart = WebGisObjektart.Schacht,
                Bezeichnung = s.GetFieldValue(SchachtFeldnamen.Feld(s, "Schachtnummer")),
                RecordId = s.Id,
                GespeicherteGlobalId = s.WebGisGlobalId,
                Baujahr = s.GetFieldValue(SchachtFeldnamen.Feld(s, WebGisImportPlanBuilder.FeldBaujahr)),
                BaujahrHandwert = s.FieldMeta.GetValueOrDefault(SchachtFeldnamen.Feld(s, WebGisImportPlanBuilder.FeldBaujahr))?.UserEdited == true,
                Normschacht = AbwasserbauwerkVokabular.Klasse(
                    s.GetFieldValue(SchachtFeldnamen.Feld(s, FieldKeys.ShaftStructureType)),
                    s.GetFieldValue(SchachtFeldnamen.Feld(s, WebGisBegriffe.SchachtFunktion))) == "Normschacht",
            };
            e.Felder[WebGisImportPlanBuilder.MaterialgruppeFeld(WebGisObjektart.Schacht)] = AkteFeld(projekt, s.Id, "schacht",
                WebGisImportPlanBuilder.MaterialgruppeFeld(WebGisObjektart.Schacht));
            e.Felder[WebGisImportPlanBuilder.BetreiberFeld(WebGisObjektart.Schacht)] = AkteFeld(projekt, s.Id, "schacht",
                WebGisImportPlanBuilder.BetreiberFeld(WebGisObjektart.Schacht));
            foreach (var feldId in WebGisImportAktenfelder.Felder(WebGisObjektart.Schacht))
                e.Felder[feldId] = AkteFeld(projekt, s.Id, "schacht", feldId);
            var eigentuemer = SchachtFeldnamen.Feld(s, FieldKeys.Owner);
            e.Felder[FieldKeys.Owner] = new WebGisImportFeld(s.GetFieldValue(eigentuemer),
                IstErsetzbar(s.FieldMeta.GetValueOrDefault(eigentuemer)), s.FieldMeta.GetValueOrDefault(eigentuemer)?.UserEdited == true);
            foreach (var karte in WebGisHandwertKarte.Felder)
            {
                if (karte.Objektart != WebGisObjektart.Schacht) continue;
                var name = SchachtFeldnamen.Feld(s, karte.SewerStudioFeld);
                e.Felder[karte.SewerStudioFeld] = new WebGisImportFeld(
                    s.GetFieldValue(name), IstErsetzbar(s.FieldMeta.GetValueOrDefault(name)),
                    s.FieldMeta.GetValueOrDefault(name)?.UserEdited == true);
            }
            var (pos, stand) = await BaueEineAsync(e, ct).ConfigureAwait(false);
            plan.Positionen.Add(pos);
            gelesen.Add((e, pos, stand));
        }

        // Eindeutig heisst auch beim Holen: ein WebGIS-Objekt gehoert zu genau EINEM Datensatz — sonst
        // bekaemen zwei Datensaetze dieselbe GlobalID. Massnahmen erst danach, nur fuer eindeutige Objekte.
        WebGisEindeutigkeit.SperreDoppelte(plan.Positionen, p => p.Objektart, p => p.GlobalId ?? p.GespeicherteGlobalId,
            p => p.Bezeichnung, (p, grund) => p.Sperren.Add(grund), "keines bekommt Werte oder die GlobalID.");
        foreach (var (e, pos, stand) in gelesen)
            if (stand is not null && pos.Sperren.Count == 0)
                await PlaneSanierungenAsync(projekt, plan, e, stand, pos, ct).ConfigureAwait(false);

        plan.Hinweise.Add($"{plan.Positionen.Count} Objekte gelesen: {plan.Uebernehmbare} mit Uebernahme, {plan.Gesperrte} gesperrt.");
        if (plan.Positionen.Count > 1 && plan.Positionen.TrueForAll(p => p.Sperren.Exists(s => s.StartsWith("Im WebGIS nicht eindeutig gefunden", StringComparison.Ordinal))))
            plan.Hinweise.Add("Kein einziger Name wurde im WebGIS eindeutig gefunden. Bitte WebGIS-Anmeldung und Suche prüfen; die genaue Ursache ist damit noch nicht belegt.");
        return plan;
    }

    private async Task<(WebGisImportPosition Position, WebGisLesestand? Stand)> BaueEineAsync(WebGisImportEingabe e, CancellationToken ct)
    {
        WebGisLesestand? stand;
        try
        {
            stand = await WebGisObjektLesen.LiesAsync(_client, e.Objektart, e.Bezeichnung, e.GespeicherteGlobalId, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        catch (WebGisSitzungException) { throw; }
        catch (Exception ex)
        {
            var p = new WebGisImportPosition
            {
                Objektart = e.Objektart, Bezeichnung = e.Bezeichnung, RecordId = e.RecordId, GespeicherteGlobalId = e.GespeicherteGlobalId,
            };
            p.Sperren.Add("Lesefehler: " + ex.Message);
            return (p, null);
        }
        return (WebGisImportPlanBuilder.Baue(e, stand), stand);
    }

    /// <summary>
    /// Je Massnahme der WebGIS-Liste, die in SewerStudio noch keine Akte hat, die Massnahme einzeln
    /// lesen und die Akte planen. Schon vorhandene werden gar nicht erst gelesen.
    /// </summary>
    private async Task PlaneSanierungenAsync(Project projekt, WebGisImportPlan plan, WebGisImportEingabe e,
        WebGisLesestand stand, WebGisImportPosition pos, CancellationToken ct)
    {
        foreach (var zeile in stand.Sanierungen)
        {
            if (string.IsNullOrWhiteSpace(zeile.GlobalId)) continue;
            if (WebGisSanierungImportRegel.SchonVorhanden(projekt.Objektakten, e.RecordId, zeile)) continue;
            WebGisLesestand? massnahme;
            try
            {
                massnahme = await _client.LeseMassnahmeAsync(zeile.GlobalId, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { throw; }
            catch (WebGisSitzungException) { throw; }
            catch (Exception ex)
            {
                pos.Hinweise.Add($"Sanierungsmassnahme {zeile.Art} {zeile.Verfahren} nicht lesbar: {ex.Message}");
                continue;
            }
            if (massnahme is null)
            {
                pos.Hinweise.Add($"Sanierungsmassnahme {zeile.Art} {zeile.Verfahren} ({zeile.GlobalId}) nicht lesbar — nicht übernommen.");
                continue;
            }
            plan.Sanierungen.Add(WebGisSanierungImportRegel.Baue(e.Objektart, e.RecordId, e.Bezeichnung, zeile.GlobalId, massnahme));
        }
    }

    /// <summary>
    /// «Übernehmen» (Entscheid Pascal 23.09.2026 abends): Vorher wird jedes Objekt mit etwas zu uebernehmen
    /// und jede anzulegende Massnahme im WebGIS nochmals gelesen. Weicht der Stand samt Aenderungsdatum von
    /// der Vorschau ab, wird genau dieses Objekt bzw. diese Massnahme nicht uebernommen (Sperre mit Grund);
    /// alle anderen schon. Erst wird alles geprueft, dann geschrieben — eine abgelaufene Sitzung mitten im
    /// Pruefen laesst deshalb nichts halb uebernommen zurueck.
    /// </summary>
    public async Task<WebGisHolenErgebnis> UebernimmGeprueftAsync(WebGisImportPlan plan, Project projekt, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(projekt);
        const string Grund = "Im WebGIS seit der Vorschau geändert — nicht übernommen, bitte neu prüfen.";
        var gestoppt = 0;

        // Audit A07 (23.09.2026): Zeigt inzwischen ein ANDERER Datensatz auf dasselbe WebGIS-Objekt, wird die
        // ganze Position gestoppt — Kennung, Fachwerte und Akten —, nicht nur die Kennungszeile.
        foreach (var pos in plan.Positionen)
        {
            if (pos.Sperren.Count > 0 || string.IsNullOrWhiteSpace(pos.GlobalId)) continue;
            if (!GlobalIdSchonVergeben(projekt, pos.Objektart, pos.RecordId, pos.GlobalId!)) continue;
            pos.Sperren.Add($"Ein anderer Datensatz im Projekt zeigt inzwischen auf {WebGisEindeutigkeit.Kennwort} "
                            + $"(GlobalID {pos.GlobalId}) — nicht übernommen. Doppelten Datensatz im Projekt bereinigen.");
            gestoppt++;
        }

        var elternMitMassnahme = plan.Sanierungen.Where(s => s.Uebernehmbar).Select(s => (s.Objektart, s.ElternRecordId)).ToHashSet();
        foreach (var pos in plan.Positionen)
        {
            if (pos.Sperren.Count > 0 || string.IsNullOrWhiteSpace(pos.GlobalId)) continue;
            if (!pos.Uebernehmbar && !elternMitMassnahme.Contains((pos.Objektart, pos.RecordId))) continue;
            string? abweichung;
            try
            {
                var jetzt = await WebGisObjektLesen.LiesAsync(_client, pos.Objektart, pos.Bezeichnung, pos.GespeicherteGlobalId, ct).ConfigureAwait(false);
                abweichung = jetzt is null || !string.Equals(jetzt.GlobalId, pos.GlobalId, StringComparison.OrdinalIgnoreCase)
                    ? "nicht mehr eindeutig lesbar"
                    : Abweichung(pos.Objektart, pos.GelesenerStand, jetzt.Felder);
            }
            catch (OperationCanceledException) { throw; }
            catch (WebGisSitzungException) { throw; }
            catch (Exception ex) { abweichung = "nicht erneut lesbar: " + ex.Message; }
            if (abweichung is null) continue;
            pos.Sperren.Add($"{Grund} ({abweichung})");
            gestoppt++;
        }

        foreach (var imp in plan.Sanierungen)
        {
            if (!imp.Uebernehmbar) continue;
            string? abweichung;
            try
            {
                var jetzt = await _client.LeseMassnahmeAsync(imp.WebGisGlobalId, ct).ConfigureAwait(false);
                abweichung = jetzt is null ? "nicht mehr lesbar" : Abweichung(imp.Objektart, imp.GelesenerStand, jetzt.Felder);
            }
            catch (OperationCanceledException) { throw; }
            catch (WebGisSitzungException) { throw; }
            catch (Exception ex) { abweichung = "nicht erneut lesbar: " + ex.Message; }
            if (abweichung is null) continue;
            imp.Sperren.Add($"Sanierungsmassnahme: {Grund} ({abweichung})");
            gestoppt++;
        }

        return new WebGisHolenErgebnis(Uebernimm(plan, projekt), gestoppt);
    }

    /// <summary>Kurzbeschreibung der abweichenden Felder; null, wenn der Stand gleich ist.</summary>
    private static string? Abweichung(WebGisObjektart art, IReadOnlyDictionary<string, string?>? vorher, IReadOnlyDictionary<string, string?> jetzt)
    {
        if (vorher is null) return "Stand der Vorschau fehlt";
        var felder = WebGisStandVergleich.Abweichungen(vorher, jetzt);
        if (felder.Count == 0) return null;
        return string.Join(", ", felder.Take(5).Select(r => WebGisStandVergleich.Anzeigename(art, r)))
               + (felder.Count > 5 ? $" und {felder.Count - 5} weitere" : string.Empty);
    }

    /// <summary>Uebernimmt die geplanten Werte in die Records des Projekts und legt die Sanierungsakten an.
    /// Liefert die Zahl geaenderter Objekte plus angelegter Akten.</summary>
    public static int Uebernimm(WebGisImportPlan plan, Project projekt)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(projekt);
        var haltungen = new Dictionary<Guid, HaltungRecord>();
        foreach (var h in projekt.Data) haltungen[h.Id] = h;
        var schaechte = new Dictionary<Guid, SchachtRecord>();
        foreach (var s in projekt.SchaechteData) schaechte[s.Id] = s;

        var n = 0;
        var gueltigeEltern = new HashSet<(WebGisObjektart, Guid)>();
        foreach (var pos in plan.Positionen)
        {
            if (pos.Sperren.Count > 0 || string.IsNullOrWhiteSpace(pos.GlobalId)) continue;
            string? aktuellerName;
            string? gespeicherteId;
            if (pos.Objektart == WebGisObjektart.Haltung && haltungen.TryGetValue(pos.RecordId, out var aktuelleHaltung))
            {
                aktuellerName = aktuelleHaltung.GetFieldValue(FieldKeys.HoldingName);
                gespeicherteId = aktuelleHaltung.WebGisGlobalId;
            }
            else if (pos.Objektart == WebGisObjektart.Schacht && schaechte.TryGetValue(pos.RecordId, out var aktuellerSchacht))
            {
                aktuellerName = aktuellerSchacht.GetFieldValue(SchachtFeldnamen.Feld(aktuellerSchacht, "Schachtnummer"));
                gespeicherteId = aktuellerSchacht.WebGisGlobalId;
            }
            else continue;
            if (!string.Equals(aktuellerName, pos.Bezeichnung, StringComparison.Ordinal)
                || !string.IsNullOrWhiteSpace(gespeicherteId)
                   && !string.Equals(gespeicherteId, pos.GlobalId, StringComparison.OrdinalIgnoreCase))
                continue;
            // Zeigt ein anderer Datensatz auf dasselbe WebGIS-Objekt, bekommt diese Position gar nichts (A07).
            if (GlobalIdSchonVergeben(projekt, pos.Objektart, pos.RecordId, pos.GlobalId!)) continue;
            gueltigeEltern.Add((pos.Objektart, pos.RecordId));
            if (!pos.Uebernehmbar) continue;
            var geaendert = false;
            foreach (var a in pos.Aenderungen)
            {
                if (a.Feld == WebGisImportPlanBuilder.FeldWebGisGlobalId)
                {
                    if (!string.Equals(a.Neu, pos.GlobalId, StringComparison.OrdinalIgnoreCase)) continue;
                    // Nie an einen zweiten Datensatz derselben Art (Entscheid Pascal 23.09.2026): Traegt schon ein
                    // anderer diese GlobalID, bleibt dieser ohne — die Vorschau hat das bereits gesperrt.
                    if (GlobalIdSchonVergeben(projekt, pos.Objektart, pos.RecordId, a.Neu)) continue;
                    if (pos.Objektart == WebGisObjektart.Haltung && haltungen.TryGetValue(pos.RecordId, out var haltung)
                        && string.IsNullOrWhiteSpace(haltung.WebGisGlobalId)
                        && string.Equals(haltung.GetFieldValue(FieldKeys.HoldingName), pos.Bezeichnung, StringComparison.Ordinal))
                    {
                        haltung.SetzeWebGisGlobalId(a.Neu);
                        geaendert = true;
                    }
                    else if (pos.Objektart == WebGisObjektart.Schacht && schaechte.TryGetValue(pos.RecordId, out var schacht)
                             && string.IsNullOrWhiteSpace(schacht.WebGisGlobalId)
                             && string.Equals(schacht.GetFieldValue(SchachtFeldnamen.Feld(schacht, "Schachtnummer")), pos.Bezeichnung, StringComparison.Ordinal))
                    {
                        schacht.SetzeWebGisGlobalId(a.Neu);
                        geaendert = true;
                    }
                    continue;
                }
                if (a.Feld == WebGisImportPlanBuilder.MaterialgruppeFeld(pos.Objektart)
                    || a.Feld == WebGisImportPlanBuilder.BetreiberFeld(pos.Objektart)
                    || WebGisImportAktenfelder.IstAktenfeld(pos.Objektart, a.Feld))
                {
                    if (SchreibeAkteGruppe(projekt, pos, a)) geaendert = true;
                    continue;
                }
                if (pos.Objektart == WebGisObjektart.Haltung && haltungen.TryGetValue(pos.RecordId, out var h))
                {
                    // Haltungslaenge seit 23.09.2026 abends rein informativ: nur in ein leeres Feld (unten
                    // FuelleLeeresFeld); ein vorhandener Wert der Kanalfirma ist nie ersetzbar (IstErsetzbar).
                    if (!SeitVorschauUnveraendert(h.GetFieldValue(a.Feld), h.FieldMeta.GetValueOrDefault(a.Feld), a))
                        continue;
                    if (h.FuelleLeeresFeld(a.Feld, a.Neu, FieldSource.Kataster))
                        geaendert = true;
                    else if (DarfErsetzen(h.GetFieldValue(a.Feld), h.FieldMeta.GetValueOrDefault(a.Feld), a))
                    {
                        h.SetFieldValue(a.Feld, a.Neu, FieldSource.Kataster, userEdited: false);
                        geaendert = true;
                    }
                }
                else if (pos.Objektart == WebGisObjektart.Schacht && schaechte.TryGetValue(pos.RecordId, out var s))
                {
                    var name = SchachtFeldnamen.Feld(s, a.Feld);
                    if (!SeitVorschauUnveraendert(s.GetFieldValue(name), s.FieldMeta.GetValueOrDefault(name), a))
                        continue;
                    // Die WebGIS-Funktionsliste gilt nur fuer den Normschacht — auch wenn die Bauwerksart
                    // erst nach der Vorschau geaendert wurde (Pruefung 23.09.2026).
                    if (a.Feld == WebGisBegriffe.SchachtFunktion
                        && AbwasserbauwerkVokabular.Klasse(s.GetFieldValue(SchachtFeldnamen.Feld(s, FieldKeys.ShaftStructureType)),
                            s.GetFieldValue(name)) != "Normschacht")
                        continue;
                    if (s.FuelleLeeresFeld(name, a.Neu, FieldSource.Kataster))
                        geaendert = true;
                    else if (DarfErsetzen(s.GetFieldValue(name), s.FieldMeta.GetValueOrDefault(name), a)
                             && s.SetFieldValue(name, a.Neu, FieldSource.Kataster, userEdited: false) == FeldSchreibErgebnis.Geschrieben)
                        geaendert = true;
                }
            }
            pos.Uebernommen = geaendert;
            if (geaendert) n++;
        }
        // Sanierungsmassnahmen als Akten (ohne Handmarke, mit WebGIS-Beleg; nie doppelt).
        foreach (var imp in plan.Sanierungen)
            if (gueltigeEltern.Contains((imp.Objektart, imp.ElternRecordId))
                && WebGisSanierungImportRegel.LegeAn(projekt, imp)) n++;
        return n;
    }

    private static bool GlobalIdSchonVergeben(Project projekt, WebGisObjektart art, Guid eigene, string globalId)
        => art == WebGisObjektart.Haltung
            ? projekt.Data.Any(x => x.Id != eigene && string.Equals(x.WebGisGlobalId, globalId, StringComparison.OrdinalIgnoreCase))
            : projekt.SchaechteData.Any(x => x.Id != eigene && string.Equals(x.WebGisGlobalId, globalId, StringComparison.OrdinalIgnoreCase));

    /// <summary>Wert eines Felds der Wurzelakte; ersetzbar, wenn nicht von Hand gesetzt.</summary>
    private static WebGisImportFeld AkteFeld(Project projekt, Guid id, string art, string feldId)
    {
        var wert = projekt.Objektakten.FirstOrDefault(a => a.Id == id && a.Art == art)?.Werte.GetValueOrDefault(feldId);
        return new WebGisImportFeld(wert?.Text ?? string.Empty, wert is null || !wert.VonHand, wert?.VonHand == true);
    }

    /// <summary>
    /// Ein Feld der Wurzelakte (Materialgruppe, Betreiber, Typ AA, die Schachtmaske …; legt die Akte bei Bedarf an),
    /// wie ein Import ohne Handmarke. Nur wenn der Wert seit der Vorschau gleich und nicht von Hand gesetzt ist.
    /// Auswahlfelder ueber ihren Listeneintrag, Zahl- und Textfelder woertlich.
    /// </summary>
    private static bool SchreibeAkteGruppe(Project projekt, WebGisImportPosition pos, WebGisImportAenderung a)
    {
        var art = pos.Objektart == WebGisObjektart.Haltung ? "haltung" : "schacht";
        var gibtEs = pos.Objektart == WebGisObjektart.Haltung
            ? projekt.Data.Any(r => r.Id == pos.RecordId)
            : projekt.SchaechteData.Any(r => r.Id == pos.RecordId);
        var katalogId = FieldCatalog.Objektfelder.Feld(a.Feld).KatalogId;
        var eintrag = katalogId is null ? null : WebGisImportPlanBuilder.GruppenEintrag(a.Feld, a.Neu);
        if (!gibtEs || (katalogId is not null && eintrag is null)) return false;

        var akte = projekt.Objektakten.FirstOrDefault(x => x.Id == pos.RecordId && x.Art == art);
        var bisher = akte?.Werte.GetValueOrDefault(a.Feld);
        if (bisher is { VonHand: true }) return false;
        if (!string.Equals((bisher?.Text ?? string.Empty).Trim(), (a.Alt ?? string.Empty).Trim(), StringComparison.Ordinal)) return false;
        if (akte is null)
        {
            akte = new ObjektAkte { Id = pos.RecordId, Art = art };
            projekt.Objektakten.Add(akte);
        }
        akte.Werte[a.Feld] = eintrag is null
            ? new ObjektFeldWert { Text = a.Neu.Trim(), VonHand = false, GeaendertUtc = DateTime.UtcNow }
            : new ObjektFeldWert
            {
                Text = eintrag.Label, KatalogId = katalogId,
                Originalcode = eintrag.OriginalCode, LokalerEintrag = eintrag.Index, VonHand = false, GeaendertUtc = DateTime.UtcNow,
            };
        projekt.Version = Math.Max(projekt.Version, 3); // Objektakten verlangen Format 3
        return true;
    }

    /// <summary>
    /// Das WebGIS darf einen vorhandenen Wert nur ersetzen, wenn er aus GeoShop oder QGIS stammt
    /// (<see cref="FieldSource.Kataster"/>) und nicht von Hand gesetzt ist — WebGIS vor GeoShop.
    /// Xtf/Xtf405/Ili vergeben auch die Kanalfirmen-Importe (VSA-KEK, M150, SIA405); deren Werte sind
    /// der Ist-Zustand und werden nie ueberschrieben (Entscheid Pascal 23.09.2026 abends).
    /// </summary>
    public static bool IstErsetzbar(FieldMetadata? meta)
        => meta is { UserEdited: false, Source: FieldSource.Kataster };

    /// <summary>
    /// Vor jedem Feld nochmals (Pruefung 23.09.2026): keine Handeingabe — auch nicht bewusst leer
    /// (Entscheid Pascal) — und derselbe Wert wie in der Vorschau. Sonst bleibt das Feld, wie es ist.
    /// </summary>
    private static bool SeitVorschauUnveraendert(string aktuell, FieldMetadata? meta, WebGisImportAenderung a)
        => meta?.UserEdited != true
           && string.Equals(aktuell.Trim(), (a.Alt ?? string.Empty).Trim(), StringComparison.Ordinal);

    /// <summary>
    /// Konfliktschutz: seit der Vorschau unveraendert UND weiterhin ersetzbar. Den Eigentuemer fuehrt das WebGIS
    /// (Entscheid Pascal 24.09.2026): Dort genuegt «keine Handeingabe» — auch ein Wert der Kanalfirma weicht.
    /// </summary>
    private static bool DarfErsetzen(string aktuell, FieldMetadata? meta, WebGisImportAenderung a)
        => a.Alt is not null
           && string.Equals(aktuell.Trim(), a.Alt.Trim(), StringComparison.Ordinal)
           && (IstErsetzbar(meta) || a.Feld == FieldKeys.Owner && meta?.UserEdited != true);
}
