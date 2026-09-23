using System;
using System.Collections.Generic;

namespace AuswertungPro.Next.Application.WebGis;

/// <summary>Eingabe fuer den Planbau — bewusst von HaltungRecord/SchachtRecord entkoppelt.</summary>
public sealed class WebGisObjektEingabe
{
    public required WebGisObjektart Objektart { get; init; }
    public required string Bezeichnung { get; init; }
    public Guid RecordId { get; init; }
    public string? GespeicherteGlobalId { get; init; }
    public string? Zustandsklasse { get; init; }
    public string? Bemerkung { get; init; }
    /// <summary>Baujahr (Haltung und Schacht), wird nur gesetzt wenn im WebGIS leer.</summary>
    public string? Baujahr { get; init; }
    /// <summary>Saniert laut Sanierungs-Akte (nicht laut Bemerkungstext).</summary>
    public bool Saniert { get; init; }
    /// <summary>Von Hand geaenderte Felder (FieldMeta.UserEdited): SewerStudio-Feldname -> Text.</summary>
    public Dictionary<string, string> Handwerte { get; init; } = new(StringComparer.Ordinal);
    /// <summary>
    /// Werte der Kanalfirma (Ist-Zustand, nicht von Hand): SewerStudio-Feldname -> Text. Sie werden nur als
    /// Vorschlag gezeigt, nie automatisch geschrieben (Entscheid Pascal 23.09.2026 abends).
    /// </summary>
    public Dictionary<string, string> Kanalfirmenwerte { get; init; } = new(StringComparer.Ordinal);
}

/// <summary>
/// Baut den Exportplan je Objekt aus SewerStudio-Eingabe und frisch gelesenem
/// WebGIS-Stand. Reine Regel: kein Netz, kein Schreiben.
///
/// Feste Regeln (Entscheid Pascal 21.09.2026):
/// - Kein Laengenfeld wird je geschrieben.
/// - Sanierungsbedarf = "Saniert" nur bei vorhandener ausgefuehrter Sanierungs-Akte.
/// - Bemerkung wird zusammengefuehrt, nie ueberschrieben.
/// - Vor dem Schreiben ist der WebGIS-Stand frisch zu lesen (macht der Ablauf).
/// </summary>
public static class WebGisExportPlanBuilder
{
    public static WebGisExportPosition Baue(WebGisObjektEingabe e, WebGisLesestand? stand)
    {
        ArgumentNullException.ThrowIfNull(e);

        var pos = new WebGisExportPosition
        {
            Objektart = e.Objektart,
            Bezeichnung = e.Bezeichnung,
            GlobalId = stand?.GlobalId,
            GespeicherteGlobalId = e.GespeicherteGlobalId,
            RecordId = e.RecordId,
            GelesenerStand = stand is null ? null : new Dictionary<string, string?>(stand.Felder, StringComparer.Ordinal),
        };

        if (stand is null)
        {
            pos.Sperren.Add(WebGisObjektLesen.NichtGefunden(e.GespeicherteGlobalId));
            return pos;
        }
        if (WebGisObjektLesen.NamensAbweichung(e.Bezeichnung, stand) is { } namensSperre)
        {
            pos.Sperren.Add(namensSperre);
            return pos;
        }

        if (!string.IsNullOrWhiteSpace(e.GespeicherteGlobalId)
            && !string.Equals(e.GespeicherteGlobalId, stand.GlobalId, StringComparison.OrdinalIgnoreCase))
        {
            pos.Sperren.Add("Gespeicherte WebGIS-GlobalID weicht vom Suchtreffer ab — Objekt nicht schreiben.");
            return pos;
        }

        var art = e.Objektart;

        // 1) Zustand
        var zCode = WebGisFeldkarte.ZustandCode(e.Zustandsklasse);
        if (zCode is int zc)
        {
            var altZ = stand.Feld(WebGisFeldkarte.ZustandRef(art));
            if (!GleichCode(altZ, zc))
            {
                pos.Aenderungen.Add(new WebGisFeldAenderung
                {
                    RefId = WebGisFeldkarte.ZustandRef(art),
                    Feld = "Zustand",
                    Alt = altZ,
                    AltText = stand.FeldText(WebGisFeldkarte.ZustandRef(art)),
                    Neu = zc.ToString(),
                    NeuText = WebGisFeldkarte.ZustandText(zc),
                });
            }
        }
        else if (!string.IsNullOrWhiteSpace(e.Zustandsklasse))
        {
            pos.Hinweise.Add($"Zustandsklasse '{e.Zustandsklasse}' ist kein bekannter Wert (0..4) — Zustand nicht gesetzt.");
        }

        // 2) Sanierungsbedarf
        if (e.Saniert)
        {
            var altS = stand.Feld(WebGisFeldkarte.SanierungsbedarfRef(art));
            if (!GleichCode(altS, WebGisFeldkarte.SanierungsbedarfSaniert))
            {
                pos.Aenderungen.Add(new WebGisFeldAenderung
                {
                    RefId = WebGisFeldkarte.SanierungsbedarfRef(art),
                    Feld = "Sanierungsbedarf",
                    Alt = altS,
                    AltText = stand.FeldText(WebGisFeldkarte.SanierungsbedarfRef(art)),
                    Neu = WebGisFeldkarte.SanierungsbedarfSaniert.ToString(),
                    NeuText = "Saniert",
                });
            }
            if (zCode is int z && z != 104)
                pos.Hinweise.Add($"Saniert, aber Zustandsklasse ist nicht 4 (Z4) — geliefert wird {WebGisFeldkarte.ZustandText(z)}.");
        }
        else if (NenntSaniert(e.Bemerkung))
        {
            pos.Hinweise.Add("Bemerkung nennt 'saniert', aber es gibt keine ausgefuehrte Sanierungs-Akte — Sanierungsbedarf NICHT gesetzt.");
        }

        // 3) Bemerkung (zusammenfuehren)
        var altBem = stand.Feld(WebGisFeldkarte.BemerkungRef(art));
        var neuBem = WebGisBemerkung.Zusammenfuehren(altBem, e.Bemerkung);
        if (neuBem is not null && !string.Equals(neuBem, altBem ?? string.Empty, StringComparison.Ordinal))
        {
            pos.Aenderungen.Add(new WebGisFeldAenderung
            {
                RefId = WebGisFeldkarte.BemerkungRef(art),
                Feld = "Bemerkung",
                Alt = altBem,
                Neu = neuBem,
                NeuText = neuBem,
            });
        }

        // 4) Baujahr: NIE ueberschreiben (Entscheid Pascal), nur fuellen wenn im WebGIS leer —
        //    bei Haltung UND Schacht («alles gilt auch bei den Schaechten», 23.09.2026).
        if (!string.IsNullOrWhiteSpace(e.Baujahr))
        {
            var altJ = stand.Feld(WebGisFeldkarte.BaujahrRef(art));
            if (string.IsNullOrWhiteSpace(altJ))
            {
                pos.Aenderungen.Add(new WebGisFeldAenderung
                {
                    RefId = WebGisFeldkarte.BaujahrRef(art),
                    Feld = "Baujahr",
                    Alt = altJ,
                    Neu = e.Baujahr!.Trim(),
                    NeuText = e.Baujahr!.Trim(),
                });
            }
        }

        // 5) Handwerte: was der Bearbeiter in SewerStudio von Hand gesetzt hat, geht 1:1 ins WebGIS —
        //    Combo ueber den Katalog der Maske, fehlender Klartext sperrt das Objekt nicht, nur das Feld
        //    (Hinweis). Felder ohne WebGIS-Zuordnung werden genannt, damit nichts stumm verloren geht.
        Handwerte(e, stand, pos);

        // 6) Werte der Kanalfirma (Ist-Zustand), die vom WebGIS abweichen: nur als Vorschlag zum Anhaken,
        //    nie automatisch (Entscheid Pascal 23.09.2026 abends).
        Kanalfirmenwerte(e, stand, pos);

        return pos;
    }

    private static void Handwerte(WebGisObjektEingabe e, WebGisLesestand stand, WebGisExportPosition pos)
    {
        foreach (var (feldName, text) in e.Handwerte)
        {
            if (WebGisHandwertKarte.IstEigeneRegel(feldName)) continue;
            if (WebGisHandwertKarte.NichtFuerKataster(feldName)) continue;
            var wert = (text ?? string.Empty).Trim();
            if (wert.Length == 0) continue; // Leeren wird nicht uebertragen (nichts loeschen)

            if (WebGisHandwertKarte.WebGisFuehrt(feldName))
            {
                pos.Hinweise.Add($"{feldName}: im WebGIS führend — «{wert}» wird nicht übertragen.");
                continue;
            }

            var karte = WebGisHandwertKarte.Finde(e.Objektart, feldName);
            if (karte is null)
            {
                pos.Hinweise.Add($"Handwert «{feldName} = {wert}» hat kein WebGIS-Feld — nicht übertragen.");
                continue;
            }

            PlaneWert(e, stand, karte, feldName, wert, pos.Aenderungen, pos.Hinweise,
                refId => pos.Aenderungen.Exists(a => string.Equals(a.RefId, refId, StringComparison.Ordinal)));
        }
    }

    /// <summary>
    /// Werte der Kanalfirma, die vom WebGIS abweichen, als Vorschlag — mit derselben Umrechnung wie die
    /// Handwerte, aber ohne Hinweise (was nicht passt, meldet die Projektpruefung) und nur fuer Felder,
    /// die kein Handwert schon belegt. Ein Handwert geht immer vor.
    /// </summary>
    private static void Kanalfirmenwerte(WebGisObjektEingabe e, WebGisLesestand stand, WebGisExportPosition pos)
    {
        foreach (var (feldName, text) in e.Kanalfirmenwerte)
        {
            if (e.Handwerte.ContainsKey(feldName)) continue;
            if (WebGisHandwertKarte.IstEigeneRegel(feldName) || WebGisHandwertKarte.NichtFuerKataster(feldName)
                || WebGisHandwertKarte.WebGisFuehrt(feldName)) continue;
            var wert = (text ?? string.Empty).Trim();
            if (wert.Length == 0) continue;
            var karte = WebGisHandwertKarte.Finde(e.Objektart, feldName);
            if (karte is null) continue;

            var aenderungen = new List<WebGisFeldAenderung>();
            PlaneWert(e, stand, karte, feldName, wert, aenderungen, hinweise: null,
                refId => pos.Aenderungen.Exists(a => string.Equals(a.RefId, refId, StringComparison.Ordinal))
                         || pos.Vorschlaege.Exists(v => v.Aenderungen.Exists(a => string.Equals(a.RefId, refId, StringComparison.Ordinal))));
            if (aenderungen.Count == 0) continue;

            pos.Vorschlaege.Add(new WebGisVorschlag
            {
                Feld = feldName, Anzeige = karte.Anzeige, AltText = aenderungen[0].AltText ?? aenderungen[0].Alt,
                NeuText = wert, Aenderungen = aenderungen,
            });
        }
    }

    /// <summary>
    /// Ein SewerStudio-Wert -> die WebGIS-Aenderungen dafuer (in <paramref name="ziel"/>). Hinweise nur, wenn
    /// <paramref name="hinweise"/> gesetzt ist. <paramref name="belegt"/>: refIds, die schon eine Aenderung haben.
    /// </summary>
    private static void PlaneWert(WebGisObjektEingabe e, WebGisLesestand stand, WebGisHandwertFeld karte, string feldName, string wert,
        List<WebGisFeldAenderung> ziel, List<string>? hinweise, Func<string, bool> belegt)
    {
        bool Belegt(string refId) => belegt(refId) || ziel.Exists(a => string.Equals(a.RefId, refId, StringComparison.Ordinal));

        if (karte.Typ == WebGisHandwertTyp.Text)
        {
            var alt = stand.Feld(karte.RefId);
            if (Belegt(karte.RefId)) return;
            if (!GleicherText(alt, wert))
                ziel.Add(new WebGisFeldAenderung { RefId = karte.RefId, Feld = karte.Anzeige, Alt = alt, Neu = wert, NeuText = wert });
            return;
        }

        stand.Kataloge.TryGetValue(karte.RefId, out var katalog);

        // WebGIS-Begriffe (Schritt A, 23.09.2026): nur ein zeichengenauer WebGIS-Begriff geht hinaus —
        // keine Faltung, keine Punkt-Regel. Was die Projektpruefung als «kein WebGIS-Begriff» meldet,
        // wird so auch nie gesendet; ein doppelter Eintrag in der Maskenliste sperrt das Feld.
        if (karte.HauptRefId is null
            && AuswertungPro.Next.Domain.Models.WebGisBegriffe.Fuer(e.Objektart == WebGisObjektart.Schacht, feldName) is { } webgisListe)
        {
            if (!webgisListe.Kennt(wert))
            {
                hinweise?.Add($"{karte.Anzeige} «{wert}» ist kein WebGIS-Begriff — nicht übertragen (siehe Projektprüfung).");
                return;
            }
            var treffer = (katalog ?? new List<(string Key, string Text)>())
                .Where(k => string.Equals(k.Text, wert, StringComparison.Ordinal)).Select(k => k.Key).Distinct().ToList();
            if (treffer.Count != 1)
            {
                hinweise?.Add(treffer.Count == 0
                    ? $"{karte.Anzeige} «{wert}» steht nicht in der Liste der WebGIS-Maske — nicht übertragen."
                    : $"{karte.Anzeige} «{wert}» steht mehrfach in der WebGIS-Liste — nicht übertragen (bei Trigonet klären).");
                return;
            }
            FuegeComboAn(ziel, Belegt, stand, karte.RefId, karte.Anzeige, treffer[0], wert);
            return;
        }

        // Paarfeld (Material-Detail + Material): Die Gruppe kommt aus dem Hauptteil des
        // Texts ("Beton, Fertigteil" -> "Beton"). Zeigt die Maske gerade eine andere Gruppe,
        // fuehrt ihre Detail-Liste den Wert nicht; dann zaehlt die vom Ablauf nachgeladene
        // Liste der Zielgruppe (KatalogeNachGruppe).
        string? hauptKey = null;
        var haupt = WebGisHandwertKarte.Hauptteil(wert);
        if (karte.HauptRefId is not null)
        {
            stand.Kataloge.TryGetValue(karte.HauptRefId, out var hauptKatalog);
            hauptKey = WebGisHandwertKarte.Schluessel(hauptKatalog, haupt);
            if (hauptKey is not null
                && stand.KatalogeNachGruppe.TryGetValue(WebGisLesestand.GruppenSchluessel(karte.RefId, hauptKey), out var gruppenListe))
                katalog = gruppenListe;
        }

        var key = WebGisHandwertKarte.Schluessel(katalog, wert);

        // Kein Gruppenname im Text und nicht in der aktuellen Liste: die Gruppe, deren nachgeladene
        // Liste den Wert fuehrt — nur wenn es genau EINE ist (wie im Browser: erst Gruppe, dann Detail).
        if (key is null && karte.HauptRefId is not null && hauptKey is null)
        {
            var treffer = GruppenMitWert(stand, karte.RefId, wert);
            if (treffer.Count > 1)
            {
                hinweise?.Add($"{karte.Anzeige} «{wert}» steht im WebGIS in mehreren Gruppen — Feld nicht übertragen (Gruppe in SewerStudio angeben, z.B. «Gruppe, {wert}»).");
                return;
            }
            if (treffer.Count == 1)
            {
                (hauptKey, key) = treffer[0];
                stand.Kataloge.TryGetValue(karte.HauptRefId, out var hk);
                haupt = TextZu(hk, hauptKey) ?? hauptKey;
            }
        }

        if (key is null && karte.HauptRefId is not null && hauptKey is not null && haupt == wert)
        {
            // Nur die Hauptkategorie bekannt ("Beton"): ins Hauptfeld, Detail bleibt.
            FuegeComboAn(ziel, Belegt, stand, karte.HauptRefId, karte.Anzeige, hauptKey, wert);
            return;
        }
        if (key is null)
        {
            hinweise?.Add($"{karte.Anzeige} «{wert}» ist im WebGIS-Katalog nicht vorhanden — Feld nicht übertragen (Wert in SewerStudio oder Katalog bei Trigonet angleichen).");
            return;
        }

        FuegeComboAn(ziel, Belegt, stand, karte.RefId, karte.Anzeige, key, wert);

        // Detail gesetzt -> Hauptkategorie mitziehen ("Beton, Fertigteil" -> Material "Beton").
        if (karte.HauptRefId is not null && hauptKey is not null)
            FuegeComboAn(ziel, Belegt, stand, karte.HauptRefId, karte.Anzeige + " (Hauptkategorie)", hauptKey, haupt);
    }

    /// <summary>Alle nachgeladenen Gruppenlisten dieses Detailfelds, die den Klartext fuehren: (Gruppe, Detail-Schluessel).</summary>
    private static List<(string Gruppe, string Key)> GruppenMitWert(WebGisLesestand stand, string detailRefId, string wert)
    {
        var praefix = WebGisLesestand.GruppenSchluessel(detailRefId, string.Empty);
        var treffer = new List<(string, string)>();
        foreach (var (schluessel, liste) in stand.KatalogeNachGruppe)
        {
            if (!schluessel.StartsWith(praefix, StringComparison.Ordinal)) continue;
            if (WebGisHandwertKarte.Schluessel(liste, wert) is { } key)
                treffer.Add((schluessel[praefix.Length..], key));
        }
        return treffer;
    }

    private static string? TextZu(IReadOnlyList<(string Key, string Text)>? katalog, string key)
    {
        if (katalog is null) return null;
        foreach (var (k, t) in katalog) if (k == key) return t;
        return null;
    }

    private static void FuegeComboAn(List<WebGisFeldAenderung> ziel, Func<string, bool> belegt, WebGisLesestand stand,
        string refId, string anzeige, string key, string text)
    {
        var alt = stand.Feld(refId);
        if (GleichCode(alt, key)) return;
        // Zwei SewerStudio-Felder koennen auf dasselbe WebGIS-Feld zeigen (DN und lichte Breite);
        // dann zaehlt der erste Treffer, sonst stuenden zwei Aenderungen fuer eine Zelle im Plan.
        if (belegt(refId)) return;
        // Alt = Schluessel (fuer den Konfliktschutz), AltText = Klartext (fuer den Bericht).
        ziel.Add(new WebGisFeldAenderung
        {
            RefId = refId, Feld = anzeige, Alt = alt, AltText = stand.FeldText(refId), Neu = key, NeuText = text,
        });
    }

    /// <summary>
    /// Textfeld-Vergleich. Sind BEIDE Seiten Zahlen, zaehlt der Zahlenwert: Das WebGIS speichert
    /// «1.80» als «1.8» und liefert es so zurueck — zeichengenau verglichen entstuende bei jedem
    /// Lauf dieselbe Scheinaenderung und ein unnoetiger Schreibvorgang (Buerglen, Tiefe 525145,
    /// 22.09.2026). Ist eine Seite keine Zahl, bleibt es beim zeichengenauen Vergleich.
    /// </summary>
    public static bool GleicherWert(string? vorhanden, string neu) => GleicherText(vorhanden, neu);

    private static bool GleicherText(string? vorhanden, string neu)
    {
        var alt = (vorhanden ?? string.Empty).Trim();
        var wert = neu.Trim();
        if (string.Equals(alt, wert, StringComparison.Ordinal)) return true;
        return AlsZahl(alt) is { } a && AlsZahl(wert) is { } b && a == b;
    }

    /// <summary>Zahl mit Punkt oder Komma als Dezimaltrenner; null, wenn der Text keine Zahl ist.</summary>
    private static decimal? AlsZahl(string text)
        => decimal.TryParse(text.Replace(',', '.'), System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : null;

    private static bool GleichCode(string? vorhanden, string code)
        => string.Equals((vorhanden ?? string.Empty).Trim(), code.Trim(), StringComparison.Ordinal);

    private static bool GleichCode(string? vorhanden, int code)
    {
        var v = (vorhanden ?? string.Empty).Trim();
        return v == code.ToString();
    }

    private static bool NenntSaniert(string? bemerkung)
        => (bemerkung ?? string.Empty).TrimStart().StartsWith("saniert", StringComparison.OrdinalIgnoreCase);
}
