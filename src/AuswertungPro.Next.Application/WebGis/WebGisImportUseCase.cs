using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.WebGis;

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
            };
            e.Felder[WebGisImportPlanBuilder.TypAaFeld] = AkteFeld(projekt, h.Id, "haltung", WebGisImportPlanBuilder.TypAaFeld);
            e.Felder[WebGisImportPlanBuilder.MaterialgruppeFeld(WebGisObjektart.Haltung)] = AkteFeld(projekt, h.Id, "haltung",
                WebGisImportPlanBuilder.MaterialgruppeFeld(WebGisObjektart.Haltung));
            foreach (var karte in WebGisHandwertKarte.Felder)
                if (karte.Objektart == WebGisObjektart.Haltung)
                    e.Felder[karte.SewerStudioFeld] = new WebGisImportFeld(
                        h.GetFieldValue(karte.SewerStudioFeld), IstErsetzbar(h.FieldMeta.GetValueOrDefault(karte.SewerStudioFeld)));
            plan.Positionen.Add(await BaueEineAsync(projekt, plan, e, ct).ConfigureAwait(false));
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
                Normschacht = AbwasserbauwerkVokabular.Klasse(
                    s.GetFieldValue(SchachtFeldnamen.Feld(s, FieldKeys.ShaftStructureType)),
                    s.GetFieldValue(SchachtFeldnamen.Feld(s, WebGisBegriffe.SchachtFunktion))) == "Normschacht",
            };
            e.Felder[WebGisImportPlanBuilder.MaterialgruppeFeld(WebGisObjektart.Schacht)] = AkteFeld(projekt, s.Id, "schacht",
                WebGisImportPlanBuilder.MaterialgruppeFeld(WebGisObjektart.Schacht));
            foreach (var karte in WebGisHandwertKarte.Felder)
            {
                if (karte.Objektart != WebGisObjektart.Schacht) continue;
                var name = SchachtFeldnamen.Feld(s, karte.SewerStudioFeld);
                e.Felder[karte.SewerStudioFeld] = new WebGisImportFeld(
                    s.GetFieldValue(name), IstErsetzbar(s.FieldMeta.GetValueOrDefault(name)));
            }
            plan.Positionen.Add(await BaueEineAsync(projekt, plan, e, ct).ConfigureAwait(false));
        }

        plan.Hinweise.Add($"{plan.Positionen.Count} Objekte gelesen: {plan.Uebernehmbare} mit Uebernahme, {plan.Gesperrte} gesperrt.");
        if (plan.Positionen.Count > 1 && plan.Positionen.TrueForAll(p => p.Sperren.Exists(s => s.StartsWith("Im WebGIS nicht eindeutig gefunden", StringComparison.Ordinal))))
            plan.Hinweise.Add("Kein einziger Name wurde im WebGIS eindeutig gefunden. Bitte WebGIS-Anmeldung und Suche prüfen; die genaue Ursache ist damit noch nicht belegt.");
        return plan;
    }

    private async Task<WebGisImportPosition> BaueEineAsync(Project projekt, WebGisImportPlan plan, WebGisImportEingabe e, CancellationToken ct)
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
            var p = new WebGisImportPosition { Objektart = e.Objektart, Bezeichnung = e.Bezeichnung, RecordId = e.RecordId };
            p.Sperren.Add("Lesefehler: " + ex.Message);
            return p;
        }
        var pos = WebGisImportPlanBuilder.Baue(e, stand);
        if (stand is not null && pos.Sperren.Count == 0)
            await PlaneSanierungenAsync(projekt, plan, e, stand, pos, ct).ConfigureAwait(false);
        return pos;
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
            gueltigeEltern.Add((pos.Objektart, pos.RecordId));
            if (!pos.Uebernehmbar) continue;
            var geaendert = false;
            foreach (var a in pos.Aenderungen)
            {
                if (a.Feld == WebGisImportPlanBuilder.FeldWebGisGlobalId)
                {
                    if (!string.Equals(a.Neu, pos.GlobalId, StringComparison.OrdinalIgnoreCase)) continue;
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
                if (a.Feld == WebGisImportPlanBuilder.MaterialgruppeFeld(pos.Objektart))
                {
                    if (SchreibeAkteGruppe(projekt, pos, a)) geaendert = true;
                    continue;
                }
                if (pos.Objektart == WebGisObjektart.Haltung && haltungen.TryGetValue(pos.RecordId, out var h))
                {
                    if (a.Feld == WebGisImportPlanBuilder.FeldLaenge)
                        continue; // Laenge nie aus dem WebGIS (23.09.2026) — auch nicht aus einem alten Plan
                    else if (h.FuelleLeeresFeld(a.Feld, a.Neu, FieldSource.Kataster))
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

    /// <summary>Wert eines Felds der Wurzelakte; ersetzbar, wenn nicht von Hand gesetzt.</summary>
    private static WebGisImportFeld AkteFeld(Project projekt, Guid id, string art, string feldId)
    {
        var wert = projekt.Objektakten.FirstOrDefault(a => a.Id == id && a.Art == art)?.Werte.GetValueOrDefault(feldId);
        return new WebGisImportFeld(wert?.Text ?? string.Empty, wert is null || !wert.VonHand);
    }

    /// <summary>
    /// Materialgruppe in die Wurzelakte (legt sie bei Bedarf an), wie ein Import ohne Handmarke.
    /// Nur wenn der Wert seit der Vorschau gleich und nicht von Hand gesetzt ist.
    /// </summary>
    private static bool SchreibeAkteGruppe(Project projekt, WebGisImportPosition pos, WebGisImportAenderung a)
    {
        var art = pos.Objektart == WebGisObjektart.Haltung ? "haltung" : "schacht";
        var gibtEs = pos.Objektart == WebGisObjektart.Haltung
            ? projekt.Data.Any(r => r.Id == pos.RecordId)
            : projekt.SchaechteData.Any(r => r.Id == pos.RecordId);
        var eintrag = WebGisImportPlanBuilder.GruppenEintrag(a.Feld, a.Neu);
        if (!gibtEs || eintrag is null) return false;

        var akte = projekt.Objektakten.FirstOrDefault(x => x.Id == pos.RecordId && x.Art == art);
        var bisher = akte?.Werte.GetValueOrDefault(a.Feld);
        if (bisher is { VonHand: true }) return false;
        if (!string.Equals((bisher?.Text ?? string.Empty).Trim(), (a.Alt ?? string.Empty).Trim(), StringComparison.Ordinal)) return false;
        if (akte is null)
        {
            akte = new ObjektAkte { Id = pos.RecordId, Art = art };
            projekt.Objektakten.Add(akte);
        }
        akte.Werte[a.Feld] = new ObjektFeldWert
        {
            Text = eintrag.Label, KatalogId = FieldCatalog.Objektfelder.Feld(a.Feld).KatalogId,
            Originalcode = eintrag.OriginalCode, LokalerEintrag = eintrag.Index, VonHand = false, GeaendertUtc = DateTime.UtcNow,
        };
        projekt.Version = Math.Max(projekt.Version, 3); // Objektakten verlangen Format 3
        return true;
    }

    /// <summary>
    /// Das WebGIS darf einen vorhandenen Wert nur ersetzen, wenn er aus einem Kataster stammt
    /// (GeoShop, QGIS, XTF) und nicht von Hand gesetzt ist — WebGIS vor GeoShop (23.09.2026).
    /// </summary>
    public static bool IstErsetzbar(FieldMetadata? meta)
        => meta is { UserEdited: false }
           && meta.Source is FieldSource.Kataster or FieldSource.Xtf or FieldSource.Xtf405 or FieldSource.Ili;

    /// <summary>Konfliktschutz: seit der Vorschau unveraendert UND weiterhin ersetzbar.</summary>
    private static bool DarfErsetzen(string aktuell, FieldMetadata? meta, WebGisImportAenderung a)
        => a.Alt is not null
           && string.Equals(aktuell.Trim(), a.Alt.Trim(), StringComparison.Ordinal)
           && IstErsetzbar(meta);
}
