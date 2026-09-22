using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.WebGis;

/// <summary>
/// Baut den WebGIS-Exportplan eines Projekts und fuehrt ihn wahlweise aus.
/// Standard ist der Probelauf (nichts wird geschrieben). Reine Orchestrierung
/// ueber <see cref="IGeonisWebGisClient"/>; kein direkter Netz- oder Dateizugriff.
/// </summary>
public sealed class WebGisExportUseCase
{
    private readonly IGeonisWebGisClient _client;

    public WebGisExportUseCase(IGeonisWebGisClient client)
        => _client = client ?? throw new ArgumentNullException(nameof(client));

    /// <summary>
    /// Liest je Objekt den frischen WebGIS-Stand und baut den Plan. Schreibt nichts.
    /// </summary>
    public async Task<WebGisExportPlan> BauePlanAsync(Project projekt, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(projekt);
        var plan = new WebGisExportPlan();
        var kataloge = new Dictionary<WebGisObjektart, WebGisSanierungKatalog?>();

        foreach (var h in projekt.Data)
        {
            var e = new WebGisObjektEingabe
            {
                Objektart = WebGisObjektart.Haltung,
                Bezeichnung = h.GetFieldValue(FieldKeys.HoldingName),
                RecordId = h.Id,
                Zustandsklasse = h.GetFieldValue(FieldKeys.ConditionClass),
                Bemerkung = h.GetFieldValue(FieldKeys.Remarks),
                Baujahr = h.GetFieldValue("Baujahr"),
                Saniert = WebGisSaniertKriterium.IstSaniert(projekt.Objektakten, h.Id),
                Handwerte = Handwerte(h.FieldMeta, h.GetFieldValue),
            };
            var (pos, stand) = await BaueEineAsync(e, ct).ConfigureAwait(false);
            plan.Positionen.Add(pos);
            await BaueSanierungenAsync(plan, projekt, e, stand, kataloge, ct).ConfigureAwait(false);
        }

        foreach (var s in projekt.SchaechteData)
        {
            var nameFeld = SchachtFeldnamen.Feld(s, "Schachtnummer");
            var e = new WebGisObjektEingabe
            {
                Objektart = WebGisObjektart.Schacht,
                Bezeichnung = s.GetFieldValue(nameFeld),
                RecordId = s.Id,
                Zustandsklasse = s.GetFieldValue(SchachtFeldnamen.Feld(s, "Zustandsklasse")),
                Bemerkung = s.GetFieldValue(SchachtFeldnamen.Feld(s, "Bemerkungen")),
                Saniert = WebGisSaniertKriterium.IstSaniert(projekt.Objektakten, s.Id),
                Handwerte = Handwerte(s.FieldMeta, s.GetFieldValue),
            };
            var (pos, stand) = await BaueEineAsync(e, ct).ConfigureAwait(false);
            plan.Positionen.Add(pos);
            await BaueSanierungenAsync(plan, projekt, e, stand, kataloge, ct).ConfigureAwait(false);
        }

        plan.Hinweise.Add($"{plan.Positionen.Count} Objekte geprueft: {plan.Schreibbare} mit Aenderung, {plan.Gesperrte} gesperrt.");
        plan.Hinweise.Add($"{plan.Sanierungen.Count} Sanierungsmassnahmen geplant: {plan.SanierungenSchreibbar} anzulegen, {plan.SanierungenGesperrt} gesperrt.");
        return plan;
    }

    /// <summary>Von Hand geaenderte Felder (UserEdited) mit ihrem aktuellen Text.</summary>
    private static Dictionary<string, string> Handwerte(
        IReadOnlyDictionary<string, FieldMetadata> meta, Func<string, string> wert)
    {
        var d = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (feld, m) in meta)
        {
            if (!m.UserEdited) continue;
            var v = wert(feld);
            if (!string.IsNullOrWhiteSpace(v)) d[feld] = v;
        }
        return d;
    }

    /// <summary>
    /// Je ausgefuehrter Sanierungs-Akte des Objekts eine Sanierungsmassnahme planen.
    /// Der Katalog wird je Objektart einmal gelesen (am ersten gefundenen Objekt).
    /// </summary>
    private async Task BaueSanierungenAsync(
        WebGisExportPlan plan, Project projekt, WebGisObjektEingabe e, WebGisLesestand? stand,
        Dictionary<WebGisObjektart, WebGisSanierungKatalog?> kataloge, CancellationToken ct)
    {
        var akten = WebGisSaniertKriterium.AusgefuehrteAkten(projekt.Objektakten, e.RecordId);
        if (akten.Count == 0) return;

        WebGisSanierungKatalog? katalog = null;
        if (stand is not null)
        {
            if (!kataloge.TryGetValue(e.Objektart, out katalog))
            {
                try
                {
                    katalog = await _client.LeseSanierungKatalogAsync(e.Objektart, stand.GlobalId, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    plan.Hinweise.Add($"Sanierungs-Katalog ({e.Objektart}) nicht lesbar: {ex.Message}");
                    katalog = null;
                }
                if (katalog is not null) kataloge[e.Objektart] = katalog;
            }
        }

        foreach (var akte in akten)
            plan.Sanierungen.Add(WebGisSanierungPlanBuilder.Baue(akte, e.Objektart, e.Bezeichnung, stand, katalog, e.RecordId));
    }

    /// <summary>
    /// Laedt fuer Paarfelder (Material-Detail + Material) die Detail-Liste der ZIELgruppe
    /// nach, wenn die Maske gerade eine andere Gruppe zeigt und den Handwert deshalb nicht
    /// fuehrt. Anlass Schacht 59723: «Beton, Fertigteil» ist Code 104 der Gruppe Beton, im
    /// WebGIS stand aber Kunststoff — die Liste der Maske kannte den Wert nicht (22.09.2026).
    /// Nichts wird geraten: Ohne Liste bleibt der Hinweis «nicht im Katalog».
    /// </summary>
    public async Task<IReadOnlyList<string>> ErgaenzeGruppenKatalogeAsync(WebGisObjektEingabe e, WebGisLesestand stand, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(e);
        ArgumentNullException.ThrowIfNull(stand);
        var hinweise = new List<string>();
        foreach (var (feldName, text) in e.Handwerte)
        {
            if (WebGisHandwertKarte.IstEigeneRegel(feldName) || WebGisHandwertKarte.NichtFuerKataster(feldName)
                || WebGisHandwertKarte.WebGisFuehrt(feldName)) continue;
            var karte = WebGisHandwertKarte.Finde(e.Objektart, feldName);
            if (karte is null || karte.HauptRefId is null || karte.Typ != WebGisHandwertTyp.Combo) continue;
            var wert = (text ?? string.Empty).Trim();
            if (wert.Length == 0) continue;

            stand.Kataloge.TryGetValue(karte.RefId, out var detail);
            if (WebGisHandwertKarte.Schluessel(detail, wert) is not null) continue; // die aktuelle Liste fuehrt den Wert

            stand.Kataloge.TryGetValue(karte.HauptRefId, out var haupt);
            var hauptKey = WebGisHandwertKarte.Schluessel(haupt, WebGisHandwertKarte.Hauptteil(wert));
            if (hauptKey is null) continue;

            var schluessel = WebGisLesestand.GruppenSchluessel(karte.RefId, hauptKey);
            if (stand.KatalogeNachGruppe.ContainsKey(schluessel)) continue;
            try
            {
                var liste = await _client.LeseKatalogListeAsync(e.Objektart, karte.RefId, hauptKey, stand.Subtyp, ct).ConfigureAwait(false);
                if (liste is not null) stand.KatalogeNachGruppe[schluessel] = new List<(string Key, string Text)>(liste);
            }
            catch (WebGisAntwortException ex)
            {
                // Ein Serverfehler beim optionalen Nachladen betrifft nur dieses Feld: Es bleibt beim
                // Hinweis «nicht im Katalog» — der Lauf und die uebrigen Felder gehen weiter.
                hinweise.Add($"{karte.Anzeige} «{wert}»: Liste der Gruppe konnte nicht nachgeladen werden — {ex.Message}");
            }
        }
        return hinweise;
    }

    private async Task<(WebGisExportPosition Position, WebGisLesestand? Stand)> BaueEineAsync(
        WebGisObjektEingabe e, CancellationToken ct)
    {
        WebGisLesestand? stand;
        IReadOnlyList<string> nachladeHinweise = Array.Empty<string>();
        try
        {
            stand = await _client.LeseAsync(e.Objektart, e.Bezeichnung, ct).ConfigureAwait(false);
            if (stand is not null) nachladeHinweise = await ErgaenzeGruppenKatalogeAsync(e, stand, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        catch (WebGisSitzungException) { throw; } // Sitzung weg: abbrechen, nicht 52x "Lesefehler"
        catch (Exception ex)
        {
            var p = new WebGisExportPosition
            {
                Objektart = e.Objektart, Bezeichnung = e.Bezeichnung, RecordId = e.RecordId,
            };
            p.Sperren.Add("Lesefehler: " + ex.Message);
            return (p, null);
        }
        var pos = WebGisExportPlanBuilder.Baue(e, stand);
        pos.Hinweise.AddRange(nachladeHinweise);
        return (pos, stand);
    }

    /// <summary>
    /// Fuehrt einen bereits gebauten Plan aus. <paramref name="probelauf"/> true
    /// (Standard) schreibt nichts. Vor jedem Schreiben wird der WebGIS-Stand frisch
    /// gelesen und die Aenderung nur uebernommen, wenn der Ausgangswert noch passt
    /// (Konfliktschutz).
    /// </summary>
    public async Task FuehreAusAsync(
        WebGisExportPlan plan, bool probelauf = true, CancellationToken ct = default,
        Action<WebGisExportPosition>? nachObjekt = null, Action<WebGisSanierungPosition>? nachMassnahme = null)
    {
        ArgumentNullException.ThrowIfNull(plan);

        foreach (var pos in plan.Positionen)
        {
            if (!pos.Schreibbar) continue;
            if (probelauf) { pos.Hinweise.Add("Probelauf — nicht geschrieben."); continue; }
            await GeschuetztAsync(pos, p => SchreibeEineAsync(p, ct), (p, f) => p.SchreibFehler = f, nachObjekt).ConfigureAwait(false);
        }

        await LegeSanierungenAnAsync(plan, probelauf, ct, nachMassnahme).ConfigureAwait(false);
    }

    /// <summary>
    /// Genau ein Objekt schreiben: frisch lesen, Ausgangswerte pruefen (Schluessel gegen
    /// Schluessel — nie Klartext gegen Schluessel, das sperrte in Buerglen acht Objekte
    /// faelschlich), dann genau die geplanten Felder senden.
    /// </summary>
    private async Task SchreibeEineAsync(WebGisExportPosition pos, CancellationToken ct)
    {
        var stand = await _client.LeseAsync(pos.Objektart, pos.Bezeichnung, ct).ConfigureAwait(false);
        if (stand is null || !string.Equals(stand.GlobalId, pos.GlobalId, StringComparison.OrdinalIgnoreCase))
        {
            pos.SchreibFehler = "Objekt beim erneuten Lesen nicht mehr eindeutig — uebersprungen.";
            return;
        }

        var felder = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var a in pos.Aenderungen)
        {
            var jetzt = stand.Feld(a.RefId) ?? string.Empty;
            if (!string.Equals((a.Alt ?? string.Empty).Trim(), jetzt.Trim(), StringComparison.Ordinal))
            {
                var jetztText = stand.FeldText(a.RefId);
                pos.SchreibFehler = $"Feld {a.Feld} wurde im WebGIS seit dem Plan geaendert (jetzt '{jetztText ?? jetzt}') — Objekt gesperrt, bitte neu pruefen.";
                return;
            }
            felder[a.RefId] = a.Neu;
        }

        var res = await _client.SchreibeAsync(pos.Objektart, pos.GlobalId!, felder, ct).ConfigureAwait(false);
        if (!res.Erfolg) { pos.SchreibFehler = res.Fehler; return; }
        await PruefeNachAsync(pos, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Nachkontrolle: Steht der geschriebene Wert danach wirklich im WebGIS?
    ///
    /// Der Server meldet auch dann «gespeichert», wenn er ein Feld gar nicht setzen KANN —
    /// die Schachttiefe etwa rechnet er aus Sohlen- und Deckelkote; fehlen sie, nimmt er den
    /// Wert an und verwirft ihn. In Buerglen stand dreimal «Tiefe [m] – → 3.22 | OK» im Log,
    /// und das Feld war jedes Mal weiterhin leer (Befund 22.09.2026). Ein OK fuer etwas, das
    /// nicht passiert ist, ist schlimmer als ein sichtbarer Fehlschlag.
    ///
    /// Scheitert die Nachkontrolle selbst, bleibt es beim Erfolg mit Hinweis: Der
    /// Schreibvorgang war ja bestaetigt, nur die Gegenprobe fehlt.
    /// </summary>
    private async Task PruefeNachAsync(WebGisExportPosition pos, CancellationToken ct)
    {
        WebGisLesestand? nachher;
        try
        {
            nachher = await _client.LeseAsync(pos.Objektart, pos.Bezeichnung, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        catch (WebGisSitzungException) { throw; }
        catch (Exception ex)
        {
            pos.Geschrieben = true;
            pos.Hinweise.Add("Geschrieben, aber nicht nachgeprüft (Lesefehler: " + ex.Message + ").");
            return;
        }
        if (nachher is null)
        {
            pos.Geschrieben = true;
            pos.Hinweise.Add("Geschrieben, aber nicht nachgeprüft — beim erneuten Lesen kein eindeutiger Treffer.");
            return;
        }

        var verworfen = new List<string>();
        foreach (var a in pos.Aenderungen)
            if (!WebGisExportPlanBuilder.GleicherWert(nachher.Feld(a.RefId), a.Neu))
                verworfen.Add(a.Feld);

        if (verworfen.Count == 0) { pos.Geschrieben = true; return; }

        pos.SchreibFehler =
            string.Join(", ", verworfen) + ": vom WebGIS nicht übernommen — das Feld steht danach unverändert da. "
            + "Bei der Tiefe rechnet das WebGIS aus Sohlen- und Deckelkote; fehlen sie, lässt sie sich nicht setzen.";
    }

    /// <summary>
    /// Einen Schreibschritt ausfuehren und sein Ergebnis SOFORT melden — auch bei Fehler.
    /// Ein einzelner Fehler stoppt die uebrigen nicht. Nur Abbruch und abgelaufene Sitzung
    /// beenden den Lauf, und auch dann wird die betroffene Position vorher gemeldet, damit
    /// im Log steht, wo der Lauf stehengeblieben ist (Pruefung 22.09.2026: vorher entstand
    /// nach einem Abbruch weder Bericht noch Log ueber die bereits geschriebenen Objekte).
    /// </summary>
    private static async Task GeschuetztAsync<T>(
        T pos, Func<T, Task> schritt, Action<T, string> fehler, Action<T>? melde)
    {
        try
        {
            await schritt(pos).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        catch (WebGisSitzungException ex)
        {
            fehler(pos, "WebGIS-Sitzung abgelaufen — nicht geschrieben (" + ex.Message + ").");
            melde?.Invoke(pos);
            throw;
        }
        catch (Exception ex)
        {
            fehler(pos, "Schreibfehler: " + ex.Message);
        }
        melde?.Invoke(pos);
    }

    /// <summary>
    /// Legt die geplanten Sanierungsmassnahmen an. Vor jedem Anlegen wird das
    /// Elternobjekt frisch gelesen: GlobalID muss stimmen und die Massnahme darf
    /// nicht inzwischen (z.B. von Hand) angelegt worden sein.
    /// </summary>
    private async Task LegeSanierungenAnAsync(
        WebGisExportPlan plan, bool probelauf, CancellationToken ct, Action<WebGisSanierungPosition>? nachMassnahme)
    {
        foreach (var san in plan.Sanierungen)
        {
            if (!san.Schreibbar) continue;
            if (probelauf) { san.Hinweise.Add("Probelauf — nicht angelegt."); continue; }
            await GeschuetztAsync(san, s => LegeEineAnAsync(s, ct), (s, f) => s.SchreibFehler = f, nachMassnahme).ConfigureAwait(false);
        }
    }

    private async Task LegeEineAnAsync(WebGisSanierungPosition san, CancellationToken ct)
    {
        var stand = await _client.LeseAsync(san.Objektart, san.ElternBezeichnung, ct).ConfigureAwait(false);
        if (stand is null || !string.Equals(stand.GlobalId, san.ElternGlobalId, StringComparison.OrdinalIgnoreCase))
        {
            san.SchreibFehler = "Elternobjekt beim erneuten Lesen nicht mehr eindeutig — uebersprungen.";
            return;
        }
        if (stand.Sanierungen.Count > 0)
        {
            var artText = Anzeige(san, "Art"); var statusText = Anzeige(san, "Status"); var verfText = Anzeige(san, "Verfahren");
            if (stand.Sanierungen.Exists(z =>
                    Gleich(z.Art, artText) && Gleich(z.Status, statusText) && Gleich(z.Verfahren, verfText)))
            {
                san.SchreibFehler = "Massnahme wurde inzwischen im WebGIS angelegt — nicht doppelt angelegt.";
                return;
            }
        }

        var res = await _client.ErstelleSanierungAsync(san.Objektart, san.ElternGlobalId!, san.Felder, ct).ConfigureAwait(false);
        san.Geschrieben = res.Erfolg;
        san.NeueId = res.NeueId;
        if (!res.Erfolg) san.SchreibFehler = res.Fehler;
    }

    private static string Anzeige(WebGisSanierungPosition san, string feld)
    {
        var praefix = feld + ": ";
        var z = san.Anzeige.Find(a => a.StartsWith(praefix, StringComparison.Ordinal));
        return z is null ? string.Empty : z[praefix.Length..];
    }

    private static bool Gleich(string? a, string b)
        => string.Equals((a ?? string.Empty).Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
}
