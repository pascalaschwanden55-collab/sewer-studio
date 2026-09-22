using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.WebGis;

/// <summary>
/// WebGIS -> SewerStudio: liest je Objekt den WebGIS-Stand, plant Laenge (immer) und
/// Baujahr (nur wenn leer) und uebernimmt nach Bestaetigung in die Records.
/// Die Laenge wird als Katasterwert mit Handwert-Schutz geschrieben (Source Kataster,
/// userEdited true), damit der naechste Kanalfernseh-Import sie nicht mehr ueberschreibt.
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
                Laenge = h.GetFieldValue(FieldKeys.HoldingLengthMeters),
                Baujahr = h.GetFieldValue(WebGisImportPlanBuilder.FeldBaujahr),
            };
            plan.Positionen.Add(await BaueEineAsync(e, ct).ConfigureAwait(false));
        }
        foreach (var s in projekt.SchaechteData)
        {
            var e = new WebGisImportEingabe
            {
                Objektart = WebGisObjektart.Schacht,
                Bezeichnung = s.GetFieldValue(SchachtFeldnamen.Feld(s, "Schachtnummer")),
                RecordId = s.Id,
                Baujahr = s.GetFieldValue(SchachtFeldnamen.Feld(s, WebGisImportPlanBuilder.FeldBaujahr)),
            };
            plan.Positionen.Add(await BaueEineAsync(e, ct).ConfigureAwait(false));
        }

        plan.Hinweise.Add($"{plan.Positionen.Count} Objekte gelesen: {plan.Uebernehmbare} mit Uebernahme, {plan.Gesperrte} gesperrt.");
        return plan;
    }

    private async Task<WebGisImportPosition> BaueEineAsync(WebGisImportEingabe e, CancellationToken ct)
    {
        WebGisLesestand? stand;
        try
        {
            stand = await _client.LeseAsync(e.Objektart, e.Bezeichnung, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        catch (WebGisSitzungException) { throw; }
        catch (Exception ex)
        {
            var p = new WebGisImportPosition { Objektart = e.Objektart, Bezeichnung = e.Bezeichnung, RecordId = e.RecordId };
            p.Sperren.Add("Lesefehler: " + ex.Message);
            return p;
        }
        return WebGisImportPlanBuilder.Baue(e, stand);
    }

    /// <summary>Uebernimmt die geplanten Werte in die Records des Projekts. Liefert die Zahl geaenderter Objekte.</summary>
    public static int Uebernimm(WebGisImportPlan plan, Project projekt)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(projekt);
        var haltungen = new Dictionary<Guid, HaltungRecord>();
        foreach (var h in projekt.Data) haltungen[h.Id] = h;
        var schaechte = new Dictionary<Guid, SchachtRecord>();
        foreach (var s in projekt.SchaechteData) schaechte[s.Id] = s;

        var n = 0;
        foreach (var pos in plan.Positionen)
        {
            if (!pos.Uebernehmbar) continue;
            var geaendert = false;
            foreach (var a in pos.Aenderungen)
            {
                if (pos.Objektart == WebGisObjektart.Haltung && haltungen.TryGetValue(pos.RecordId, out var h))
                {
                    if (a.Feld == WebGisImportPlanBuilder.FeldLaenge)
                    {
                        // Regel 1: WebGIS-Laenge ersetzt jeden Wert, auch Handwerte; danach geschuetzt.
                        h.SetFieldValue(FieldKeys.HoldingLengthMeters, a.Neu, FieldSource.Kataster, userEdited: true);
                        geaendert = true;
                    }
                    else if (h.FuelleLeeresFeld(a.Feld, a.Neu, FieldSource.Kataster))
                        geaendert = true;
                }
                else if (pos.Objektart == WebGisObjektart.Schacht && schaechte.TryGetValue(pos.RecordId, out var s))
                {
                    if (s.FuelleLeeresFeld(SchachtFeldnamen.Feld(s, a.Feld), a.Neu, FieldSource.Kataster))
                        geaendert = true;
                }
            }
            pos.Uebernommen = geaendert;
            if (geaendert) n++;
        }
        return n;
    }
}
