using System;
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.WebGis;

/// <summary>
/// Baut aus einer ausgefuehrten Sanierungs-Objektakte den Plan fuer eine neue
/// Sanierungsmassnahme im WebGIS. Reine Regel: kein Netz, kein Schreiben.
///
/// Regeln (Entscheid Pascal 21.09.2026, "1:1, 100% identisch"):
/// - Jeder Klartext der Akte muss im WebGIS-Katalog vorkommen; fehlt er, wird die
///   Massnahme GESPERRT (nichts Halbes anlegen) und der fehlende Katalogwert gemeldet.
/// - Leere Akte-Felder bleiben im WebGIS leer.
/// - Sanierungsjahr wird als 01.01.&lt;Jahr&gt; geliefert (WebGIS fuehrt ein Datum).
/// - Bereits vorhandene Massnahme mit gleicher Art/Status/Verfahren und nicht nachweislich anderem Jahr ->
///   nicht doppelt anlegen (<see cref="WebGisMassnahmenVergleich"/>).
/// - Verfahren wird gegen die von der Art abhaengige Liste aufgeloest (Reparatur:
///   Vermoertelung, Roboterverfahren, ...; Renovierung: Schlauchverfahren, ...).
/// </summary>
public static class WebGisSanierungPlanBuilder
{
    public static WebGisSanierungPosition Baue(
        ObjektAkte akte, WebGisObjektart art, string elternBezeichnung,
        WebGisLesestand? stand, WebGisSanierungKatalog? katalog, Guid elternRecordId = default)
    {
        ArgumentNullException.ThrowIfNull(akte);

        var pos = new WebGisSanierungPosition
        {
            Objektart = art,
            ElternBezeichnung = elternBezeichnung,
            ElternGlobalId = stand?.GlobalId,
            ElternRecordId = elternRecordId,
            AkteId = akte.Id,
        };

        if (stand is null)
        {
            pos.Sperren.Add("Elternobjekt im WebGIS nicht eindeutig gefunden.");
            return pos;
        }
        if (katalog is null)
        {
            pos.Sperren.Add("WebGIS-Katalog der Sanierungsmaske nicht lesbar.");
            return pos;
        }

        // Bezeichnung (Freitext), nur wenn die Akte einen Namen traegt.
        var name = Wert(akte, WebGisSanierungFeldkarte.AkteName);
        if (name.Length > 0)
        {
            pos.Felder[WebGisSanierungFeldkarte.BezeichnungRef] = name;
            pos.Anzeige.Add($"Bezeichnung: {name}");
        }

        // Combos: Klartext -> WebGIS-Schluessel, ohne Raten. Verfahren haengt von der Art ab.
        var klartexte = new Dictionary<string, string>(StringComparer.Ordinal);
        string? artKey = null;
        foreach (var (akteKey, refId, feld) in WebGisSanierungFeldkarte.ComboFelder)
        {
            var text = Wert(akte, akteKey);
            if (text.Length == 0) continue;
            var filter = refId == WebGisSanierungFeldkarte.VerfahrenRef ? artKey : null;
            var key = katalog.Schluessel(refId, text, filter);
            if (refId == WebGisSanierungFeldkarte.ArtRef) artKey = key;
            if (key is null)
            {
                var wo = filter is null ? "im WebGIS-Katalog" : $"im WebGIS-Katalog fuer Art '{klartexte.GetValueOrDefault("Art")}'";
                pos.Sperren.Add($"{feld} '{text}' fehlt {wo} — Wert dort ergaenzen lassen (Trigonet), sonst keine 1:1-Uebernahme.");
                continue;
            }
            pos.Felder[refId] = key;
            pos.Anzeige.Add($"{feld}: {text}");
            klartexte[feld] = text;
        }

        // Ohne Art gibt es keinen Subtyp und keine Verfahrensliste: Akte in SewerStudio ergaenzen.
        if (artKey is null && Wert(akte, WebGisSanierungFeldkarte.AkteArt).Length == 0)
            pos.Sperren.Add("Art fehlt in der Sanierungs-Akte (Reparatur/Renovierung/Erneuerung) — in SewerStudio ergaenzen.");

        // Sanierungsjahr -> Datum 01.01.JJJJ
        var jahr = Wert(akte, WebGisSanierungFeldkarte.AkteJahr);
        if (jahr.Length > 0)
        {
            var datum = WebGisSanierungFeldkarte.JahrAlsDatum(jahr);
            if (datum is null)
                pos.Sperren.Add($"Sanierungsjahr '{jahr}' ist kein gueltiges Jahr (JJJJ).");
            else
            {
                pos.Felder[WebGisSanierungFeldkarte.SanierungsjahrRef] = datum;
                pos.Anzeige.Add($"Sanierungsjahr: 01.01.{jahr} (WebGIS fuehrt ein Datum; SewerStudio nur das Jahr)");
            }
        }

        if (pos.Felder.Count == 0 && pos.Sperren.Count == 0)
            pos.Sperren.Add("Akte ohne uebertragbare Werte.");

        // Doppelte vermeiden: gleiche Art + Status + Verfahren bereits vorhanden, und das Jahr ist nicht
        // nachweislich ein anderes (Reparatur 2020 und 2026 sind zwei Massnahmen).
        var artText = klartexte.GetValueOrDefault("Art") ?? string.Empty;
        var statusText = klartexte.GetValueOrDefault("Status") ?? string.Empty;
        var verfText = klartexte.GetValueOrDefault("Verfahren") ?? string.Empty;
        var andereJahre = new List<string>();
        foreach (var z in stand.Sanierungen)
        {
            if (!WebGisMassnahmenVergleich.GleicherInhalt(z, artText, statusText, verfText)) continue;
            if (WebGisMassnahmenVergleich.NachweislichAndereJahre(z.Jahr, jahr))
            {
                andereJahre.Add(z.Jahr!);
                continue;
            }
            pos.Sperren.Add($"Im WebGIS bereits vorhanden ({artText} / {statusText} / {verfText}) — nicht doppelt angelegt.");
            andereJahre.Clear();
            break;
        }
        if (andereJahre.Count > 0)
            pos.Hinweise.Add($"Im WebGIS steht schon {artText} / {statusText} / {verfText} aus {string.Join(", ", andereJahre.Distinct())} "
                + $"— diese Massnahme ist von {jahr} und wird als eigene angelegt.");

        return pos;
    }

    private static string Wert(ObjektAkte akte, string key)
        => akte.Werte.TryGetValue(key, out var w) && w is not null ? (w.Text ?? string.Empty).Trim() : string.Empty;

}
