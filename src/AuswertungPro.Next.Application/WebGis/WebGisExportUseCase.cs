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
    /// <summary>Nachgeladene Gruppenlisten je Objektart, Subtyp und Feld/Gruppe — gilt fuer einen Planlauf.</summary>
    private readonly Dictionary<string, List<(string Key, string Text)>?> _gruppenListen = new(StringComparer.Ordinal);

    public WebGisExportUseCase(IGeonisWebGisClient client)
        => _client = client ?? throw new ArgumentNullException(nameof(client));

    /// <summary>
    /// «Jetzt schreiben»: baut den Plan frisch und uebertraegt die Haken der bestaetigten Vorschau
    /// («Kanalfirma weicht ab») — nur fuer denselben Wert am selben Objekt (<see cref="WebGisVorschlagAuswahl"/>).
    /// Ohne Vorschau ist nichts angehakt.
    /// </summary>
    public async Task<WebGisExportPlan> BaueFrischenPlanAsync(
        Project projekt, WebGisExportPlan? bestaetigt, CancellationToken ct = default)
    {
        var plan = await BauePlanAsync(projekt, ct).ConfigureAwait(false);
        WebGisVorschlagAuswahl.UebertrageAuf(bestaetigt, plan);
        return plan;
    }

    /// <summary>
    /// Liest je Objekt den frischen WebGIS-Stand und baut den Plan. Schreibt nichts.
    /// </summary>
    public async Task<WebGisExportPlan> BauePlanAsync(Project projekt, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(projekt);
        _gruppenListen.Clear(); // jeder Plan liest die Listen einmal frisch
        var plan = new WebGisExportPlan();
        var kataloge = new Dictionary<WebGisObjektart, WebGisSanierungKatalog?>();
        var gelesen = new List<(WebGisObjektEingabe Eingabe, WebGisExportPosition Position, WebGisLesestand? Stand)>();

        // Pruefung 22.09.2026, C3: Erst das ganze Abbild bauen, dann aufs Netz warten. Nach dem ersten Warten laeuft
        // der Plan auf einem anderen Thread weiter, waehrend im nicht-modalen Fenster Haltungen/Schaechte entstehen
        // oder verschwinden — die lebenden Listen darf er dann nicht mehr anfassen.
        var eingaben = new List<WebGisObjektEingabe>();
        foreach (var h in projekt.Data)
        {
            eingaben.Add(new WebGisObjektEingabe
            {
                Objektart = WebGisObjektart.Haltung,
                Bezeichnung = h.GetFieldValue(FieldKeys.HoldingName),
                RecordId = h.Id,
                GespeicherteGlobalId = h.WebGisGlobalId,
                Zustandsklasse = OhneKatasterWert(h.FieldMeta, FieldKeys.ConditionClass, h.GetFieldValue(FieldKeys.ConditionClass)),
                Bemerkung = OhneKatasterWert(h.FieldMeta, FieldKeys.Remarks, h.GetFieldValue(FieldKeys.Remarks)),
                Baujahr = OhneKatasterWert(h.FieldMeta, "Baujahr", h.GetFieldValue("Baujahr")),
                Saniert = WebGisSaniertKriterium.IstSaniert(projekt.Objektakten, h.Id),
                Handwerte = Handwerte(h.FieldMeta, h.GetFieldValue),
                Kanalfirmenwerte = Kanalfirmenwerte(h.FieldMeta, h.GetFieldValue),
            });
        }

        foreach (var s in projekt.SchaechteData)
        {
            var nameFeld = SchachtFeldnamen.Feld(s, "Schachtnummer");
            eingaben.Add(new WebGisObjektEingabe
            {
                Objektart = WebGisObjektart.Schacht,
                Bezeichnung = s.GetFieldValue(nameFeld),
                RecordId = s.Id,
                GespeicherteGlobalId = s.WebGisGlobalId,
                Zustandsklasse = OhneKatasterWert(s.FieldMeta, SchachtFeldnamen.Feld(s, "Zustandsklasse"),
                    s.GetFieldValue(SchachtFeldnamen.Feld(s, "Zustandsklasse"))),
                Bemerkung = OhneKatasterWert(s.FieldMeta, SchachtFeldnamen.Feld(s, "Bemerkungen"),
                    s.GetFieldValue(SchachtFeldnamen.Feld(s, "Bemerkungen"))),
                Baujahr = OhneKatasterWert(s.FieldMeta, SchachtFeldnamen.Feld(s, "Baujahr"),
                    s.GetFieldValue(SchachtFeldnamen.Feld(s, "Baujahr"))),
                Saniert = WebGisSaniertKriterium.IstSaniert(projekt.Objektakten, s.Id),
                Handwerte = Handwerte(s.FieldMeta, s.GetFieldValue),
                Kanalfirmenwerte = Kanalfirmenwerte(s.FieldMeta, s.GetFieldValue),
            });
        }
        var sanierungsakten = WebGisAktenAbbild.Sanierungen(projekt.Objektakten);

        foreach (var e in eingaben)
        {
            var (pos, stand) = await BaueEineAsync(e, ct).ConfigureAwait(false);
            plan.Positionen.Add(pos);
            gelesen.Add((e, pos, stand));
        }

        // Erst wenn alle Objekte gelesen sind, laesst sich sehen, ob zwei auf dasselbe WebGIS-Objekt
        // zeigen — Massnahmen deshalb erst danach und nur fuer eindeutig zugeordnete Objekte.
        SperreDoppelteZuordnungen(plan.Positionen);
        foreach (var (e, pos, stand) in gelesen)
            if (pos.Sperren.Count == 0)
                await BaueSanierungenAsync(plan, sanierungsakten, e, stand, kataloge, ct).ConfigureAwait(false);

        plan.Hinweise.Add($"{plan.Positionen.Count} Objekte geprueft: {plan.Schreibbare} mit Aenderung, {plan.Gesperrte} gesperrt.");
        if (plan.Positionen.Count > 1 && plan.Positionen.TrueForAll(p => p.Sperren.Exists(s => s.StartsWith("Im WebGIS nicht eindeutig gefunden", StringComparison.Ordinal))))
            plan.Hinweise.Add("Kein einziger Name wurde im WebGIS eindeutig gefunden. Bitte WebGIS-Anmeldung und Suche prüfen; die genaue Ursache ist damit noch nicht belegt.");
        plan.Hinweise.Add($"{plan.Sanierungen.Count} Sanierungsmassnahmen geplant: {plan.SanierungenSchreibbar} anzulegen, {plan.SanierungenGesperrt} gesperrt.");
        return plan;
    }

    /// <summary>
    /// Eindeutig heisst auch: Ein WebGIS-Objekt gehoert zu genau EINEM SewerStudio-Objekt (Entscheid
    /// Pascal 23.09.2026). Zeigen zwei darauf — dieselbe Haltung doppelt im Projekt, oder dieselbe
    /// GlobalID an zwei Datensaetzen —, wird keines geschrieben; sonst mischten sich zwei Staende in
    /// ein Katasterobjekt. Gezaehlt wird die gefundene GlobalID, ersatzweise die gespeicherte.
    /// </summary>
    private static void SperreDoppelteZuordnungen(IReadOnlyList<WebGisExportPosition> positionen)
        => WebGisEindeutigkeit.SperreDoppelte(positionen, p => p.Objektart, p => p.GlobalId ?? p.GespeicherteGlobalId,
            p => p.Bezeichnung, (p, grund) => p.Sperren.Add(grund), "keines wird geschrieben.");

    /// <summary>Von Hand geaenderte Felder (UserEdited) mit ihrem aktuellen Text.</summary>
    private static Dictionary<string, string> Handwerte(
        IReadOnlyDictionary<string, FieldMetadata> meta, Func<string, string> wert)
    {
        var d = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (feld, m) in meta)
        {
            if (!m.UserEdited || m.Source == FieldSource.Kataster) continue;
            var v = wert(feld);
            if (!string.IsNullOrWhiteSpace(v)) d[feld] = v;
        }
        return d;
    }

    /// <summary>
    /// Werte der Kanalfirma (Ist-Zustand, nicht von Hand) mit ihrem Text — sie werden nur vorgeschlagen,
    /// wenn sie vom WebGIS abweichen, nie automatisch geschrieben (Entscheid Pascal 23.09.2026 abends).
    /// </summary>
    private static Dictionary<string, string> Kanalfirmenwerte(
        IReadOnlyDictionary<string, FieldMetadata> meta, Func<string, string> wert)
    {
        var d = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (feld, m) in meta)
        {
            if (m.UserEdited || !FieldSourceRegeln.IstKanalfirma(m.Source)) continue;
            var v = wert(feld);
            if (!string.IsNullOrWhiteSpace(v)) d[feld] = v;
        }
        return d;
    }

    /// <summary>Katasterwerte (auch aus GeoShop) sind keine Quelle fuer das Schreiben ins WebGIS.</summary>
    private static string? OhneKatasterWert(IReadOnlyDictionary<string, FieldMetadata> meta, string feld, string wert)
        => meta.TryGetValue(feld, out var herkunft) && herkunft.Source == FieldSource.Kataster ? null : wert;

    /// <summary>
    /// Je ausgefuehrter Sanierungs-Akte des Objekts eine Sanierungsmassnahme planen.
    /// Der Katalog wird je Objektart einmal gelesen (am ersten gefundenen Objekt).
    /// </summary>
    private async Task BaueSanierungenAsync(
        WebGisExportPlan plan, IReadOnlyList<ObjektAkte> sanierungsakten, WebGisObjektEingabe e, WebGisLesestand? stand,
        Dictionary<WebGisObjektart, WebGisSanierungKatalog?> kataloge, CancellationToken ct)
    {
        var akten = WebGisSaniertKriterium.AusgefuehrteAkten(sanierungsakten, e.RecordId);
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
        // Auch die Werte der Kanalfirma (Audit A09, 23.09.2026): Sonst fehlt eine abweichende Materialangabe in
        // einer anderen WebGIS-Gruppe still in der Haekchenliste. Ein Handwert auf demselben Feld geht vor.
        var werte = new List<KeyValuePair<string, string>>(e.Handwerte);
        foreach (var kv in e.Kanalfirmenwerte)
            if (!e.Handwerte.ContainsKey(kv.Key)) werte.Add(kv);
        foreach (var (feldName, text) in werte)
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

            // Nennt der Text keine Gruppe («Polypropylen»), werden die Listen ALLER Gruppen geladen;
            // der Planbau nimmt die Gruppe nur, wenn genau eine den Wert fuehrt (23.09.2026).
            var gruppen = new List<string>();
            if (hauptKey is not null) gruppen.Add(hauptKey);
            else if (haupt is not null)
                foreach (var (k, _) in haupt)
                    if (k.Length > 0 && k != stand.Feld(karte.HauptRefId)) gruppen.Add(k);

            foreach (var gruppe in gruppen)
            {
                var schluessel = WebGisLesestand.GruppenSchluessel(karte.RefId, gruppe);
                if (stand.KatalogeNachGruppe.ContainsKey(schluessel)) continue;
                // Die Liste einer Gruppe ist fuer alle Objekte gleicher Art und gleichen Subtyps dieselbe: im
                // Lauf nur einmal holen (Material der Kanalfirma steht an fast jedem Objekt).
                var cacheSchluessel = $"{e.Objektart}|{stand.Subtyp}|{schluessel}";
                if (_gruppenListen.TryGetValue(cacheSchluessel, out var gemerkt))
                {
                    if (gemerkt is not null) stand.KatalogeNachGruppe[schluessel] = new List<(string Key, string Text)>(gemerkt);
                    continue;
                }
                try
                {
                    var liste = await _client.LeseKatalogListeAsync(e.Objektart, karte.RefId, gruppe, stand.Subtyp, ct).ConfigureAwait(false);
                    _gruppenListen[cacheSchluessel] = liste is null ? null : new List<(string Key, string Text)>(liste);
                    if (liste is not null) stand.KatalogeNachGruppe[schluessel] = new List<(string Key, string Text)>(liste);
                }
                catch (WebGisAntwortException ex)
                {
                    // Ein Serverfehler beim optionalen Nachladen betrifft nur dieses Feld: Es bleibt beim
                    // Hinweis «nicht im Katalog» — der Lauf und die uebrigen Felder gehen weiter.
                    hinweise.Add($"{karte.Anzeige} «{wert}»: Liste der Gruppe konnte nicht nachgeladen werden — {ex.Message}");
                    break;
                }
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
            stand = await WebGisObjektLesen.LiesAsync(_client, e.Objektart, e.Bezeichnung, e.GespeicherteGlobalId, ct).ConfigureAwait(false);
            if (stand is not null) nachladeHinweise = await ErgaenzeGruppenKatalogeAsync(e, stand, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        catch (WebGisSitzungException) { throw; } // Sitzung weg: abbrechen, nicht 52x "Lesefehler"
        catch (Exception ex)
        {
            var p = new WebGisExportPosition
            {
                Objektart = e.Objektart, Bezeichnung = e.Bezeichnung, RecordId = e.RecordId,
                GespeicherteGlobalId = e.GespeicherteGlobalId,
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
    /// (Konfliktschutz). Angehakte Vorschlaege der Kanalfirma gehoeren zum Plan und werden hier zu
    /// Aenderungen; nicht Angehaktes geht nie hinaus.
    /// <paramref name="vorObjekt"/>/<paramref name="vorMassnahme"/> laufen unmittelbar VOR jedem Schreibversuch;
    /// eine Ausnahme dort stoppt den Lauf, bevor etwas gesendet wird (Log als Voraussetzung, WG03).
    /// </summary>
    public async Task FuehreAusAsync(
        WebGisExportPlan plan, bool probelauf = true, CancellationToken ct = default,
        Action<WebGisExportPosition>? nachObjekt = null, Action<WebGisSanierungPosition>? nachMassnahme = null,
        Action<WebGisExportPosition>? vorObjekt = null, Action<WebGisSanierungPosition>? vorMassnahme = null)
    {
        ArgumentNullException.ThrowIfNull(plan);
        WebGisVorschlagAuswahl.UebernimmGewaehlte(plan);

        foreach (var pos in plan.Positionen)
        {
            if (!pos.Schreibbar) continue;
            if (probelauf) { pos.Hinweise.Add("Probelauf — nicht geschrieben."); continue; }
            vorObjekt?.Invoke(pos);
            // Ein vom Server bestaetigtes Schreiben bleibt bestaetigt, auch wenn danach etwas scheitert (A06).
            await GeschuetztAsync(pos, p => SchreibeEineAsync(p, ct), (p, f) => { if (!p.Geschrieben) p.SchreibFehler = f; },
                nachObjekt, ct).ConfigureAwait(false);
        }

        await LegeSanierungenAnAsync(plan, probelauf, ct, nachMassnahme, vorMassnahme).ConfigureAwait(false);
    }

    /// <summary>
    /// Genau ein Objekt schreiben: frisch lesen, Ausgangswerte pruefen (Schluessel gegen
    /// Schluessel — nie Klartext gegen Schluessel, das sperrte in Buerglen acht Objekte
    /// faelschlich), dann genau die geplanten Felder senden.
    /// </summary>
    private async Task SchreibeEineAsync(WebGisExportPosition pos, CancellationToken ct)
    {
        var stand = await WebGisObjektLesen.LiesAsync(_client, pos.Objektart, pos.Bezeichnung, pos.GespeicherteGlobalId, ct).ConfigureAwait(false);
        if (stand is null || !string.Equals(stand.GlobalId, pos.GlobalId, StringComparison.OrdinalIgnoreCase))
        {
            pos.SchreibFehler = "Objekt beim erneuten Lesen nicht mehr eindeutig — uebersprungen.";
            return;
        }
        // Zwischen Plan und Schreiben kann das Objekt im WebGIS umbenannt worden sein.
        if (WebGisObjektLesen.NamensAbweichung(pos.Bezeichnung, stand) is { } namensSperre)
        {
            pos.SchreibFehler = namensSperre + " Nicht geschrieben.";
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

        // Ganzer Stand samt Aenderungsdatum (Pascal 23.09.2026): Hat jemand das Objekt seit der Pruefung
        // bearbeitet — auch an einem Feld, das der Plan nicht anfasst —, wird nicht geschrieben.
        if (pos.GelesenerStand is not null)
        {
            var abweichend = WebGisStandVergleich.Abweichungen(pos.GelesenerStand, stand.Felder);
            if (abweichend.Count > 0)
            {
                var namen = string.Join(", ", abweichend.Take(5).Select(r => WebGisStandVergleich.Anzeigename(pos.Objektart, r)))
                            + (abweichend.Count > 5 ? $" und {abweichend.Count - 5} weitere" : string.Empty);
                pos.SchreibFehler = $"Objekt wurde im WebGIS seit der Prüfung geändert ({namen}) — nicht geschrieben, bitte neu prüfen.";
                return;
            }
        }

        // Eigentum, Betreiber, Laenge, Baujahr, GlobalID, Objekt-ID: nie ueberschreiben (Pascal 23.09.2026).
        var verstoesse = WebGisGeschuetzteFelder.Verstoesse(pos.Objektart, felder, stand.Feld);
        if (verstoesse.Count > 0)
        {
            pos.SchreibFehler = string.Join(" ", verstoesse);
            return;
        }

        // Der eben gepruefte Stand geht bis zum letzten Lesen im Client mit (Audit A04, 23.09.2026).
        var res = await _client.SchreibeAsync(pos.Objektart, pos.GlobalId!, felder, ct, stand.Felder).ConfigureAwait(false);
        if (!res.Erfolg) { pos.SchreibFehler = res.Fehler; return; }
        pos.VomServerBestaetigt = true;
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
            nachher = await WebGisObjektLesen.LiesAsync(_client, pos.Objektart, pos.Bezeichnung, pos.GespeicherteGlobalId, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Bestaetigt ist bestaetigt: Bericht und Zaehlung duerfen das Schreiben nicht verschweigen (A06).
            pos.Geschrieben = true;
            pos.Hinweise.Add("Geschrieben (vom Server bestätigt), aber nicht nachgeprüft — Lauf abgebrochen.");
            throw;
        }
        catch (WebGisSitzungException)
        {
            pos.Geschrieben = true;
            pos.Hinweise.Add("Geschrieben (vom Server bestätigt), aber nicht nachgeprüft — WebGIS-Sitzung abgelaufen.");
            throw;
        }
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
        T pos, Func<T, Task> schritt, Action<T, string> fehler, Action<T>? melde, CancellationToken ct)
    {
        try
        {
            await schritt(pos).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // Kein Abbruch des Benutzers, sondern die Zeitueberschreitung des HttpClient: Der Ausgang dieses
            // Schritts ist offen. Melden, damit er im Log steht, dann den Lauf beenden — der Server antwortet nicht.
            fehler(pos, "Zeitüberschreitung beim WebGIS — Ausgang offen, bitte neu prüfen.");
            melde?.Invoke(pos);
            throw;
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
        WebGisExportPlan plan, bool probelauf, CancellationToken ct, Action<WebGisSanierungPosition>? nachMassnahme,
        Action<WebGisSanierungPosition>? vorMassnahme = null)
    {
        // Elternobjekte, deren Stand in diesem Lauf schon geprueft wurde: Eine eigene Massnahme kann das
        // Aenderungsdatum des Elternobjekts setzen und darf die naechste Massnahme nicht sperren.
        var geprueft = new HashSet<Guid>();
        foreach (var san in plan.Sanierungen)
        {
            if (!san.Schreibbar) continue;
            if (probelauf) { san.Hinweise.Add("Probelauf — nicht angelegt."); continue; }
            var eltern = plan.Positionen.Find(p => p.RecordId == san.ElternRecordId && p.Objektart == san.Objektart);

            // Audit A05 (23.09.2026): Sollte das Elternobjekt geschrieben werden und hat der Server das nicht
            // bestaetigt (Fremdaenderung, Schutzfeld, Namensfehler, Serverfehler), bekommt es auch keine Massnahme.
            if (eltern is { Schreibbar: true, VomServerBestaetigt: false })
            {
                san.SchreibFehler = "Elternobjekt nicht geschrieben (" + (eltern.SchreibFehler ?? "kein Ergebnis")
                                    + ") — Massnahme nicht angelegt, bitte neu prüfen.";
                nachMassnahme?.Invoke(san);
                continue;
            }
            // Wurde das Elternobjekt eben bestaetigt geschrieben, ist sein Stand soeben geprueft worden.
            var vergleich = eltern is { Schreibbar: false } && !geprueft.Contains(san.ElternRecordId) ? eltern.GelesenerStand : null;
            vorMassnahme?.Invoke(san);
            await GeschuetztAsync(san, s => LegeEineAnAsync(s, eltern?.GespeicherteGlobalId, vergleich, ct),
                (s, f) => { if (!s.Geschrieben) s.SchreibFehler = f; }, nachMassnahme, ct).ConfigureAwait(false);
            if (san.Geschrieben) geprueft.Add(san.ElternRecordId);
        }
    }

    private async Task LegeEineAnAsync(
        WebGisSanierungPosition san, string? gespeicherteElternGlobalId,
        IReadOnlyDictionary<string, string?>? vergleichsStand, CancellationToken ct)
    {
        var stand = await WebGisObjektLesen.LiesAsync(_client, san.Objektart, san.ElternBezeichnung, gespeicherteElternGlobalId, ct).ConfigureAwait(false);
        if (stand is null || !string.Equals(stand.GlobalId, san.ElternGlobalId, StringComparison.OrdinalIgnoreCase))
        {
            san.SchreibFehler = "Elternobjekt beim erneuten Lesen nicht mehr eindeutig — uebersprungen.";
            return;
        }
        if (WebGisObjektLesen.NamensAbweichung(san.ElternBezeichnung, stand) is { } namensSperre)
        {
            san.SchreibFehler = namensSperre + " Massnahme nicht angelegt.";
            return;
        }
        // Elternobjekt ohne eigene Feldaenderung: Auch hier gilt «geaendert heisst neu pruefen» (A05).
        if (vergleichsStand is not null)
        {
            var abweichend = WebGisStandVergleich.Abweichungen(vergleichsStand, stand.Felder);
            if (abweichend.Count > 0)
            {
                var namen = string.Join(", ", abweichend.Take(5).Select(r => WebGisStandVergleich.Anzeigename(san.Objektart, r)));
                san.SchreibFehler = $"Elternobjekt wurde im WebGIS seit der Prüfung geändert ({namen}) — Massnahme nicht angelegt, bitte neu prüfen.";
                return;
            }
        }
        if (stand.Sanierungen.Count > 0)
        {
            var artText = Anzeige(san, "Art"); var statusText = Anzeige(san, "Status"); var verfText = Anzeige(san, "Verfahren");
            var jahr = san.Felder.GetValueOrDefault(WebGisSanierungFeldkarte.SanierungsjahrRef);
            if (stand.Sanierungen.Exists(z => WebGisMassnahmenVergleich.Gleich(z, artText, statusText, verfText, jahr)))
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

}
