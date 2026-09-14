using System.Globalization;
using System.Text.RegularExpressions;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.UseCases.Objektakten;

/// <summary>Gemeinsamer Schreibweg: Bestandsfelder bleiben am Original, Zusatzwerte in der Akte.</summary>
public sealed class ObjektaktenBearbeitung(Project projekt, Guid wurzelId, string art,
    IObjektaktenListenErgaenzungen? ergaenzungen = null)
{
    private readonly ObjektAkte _leereWurzel = new() { Id = wurzelId, Art = art };
    private IReadOnlyList<ListenErgaenzung>? _ergaenzungen;
    public Project Projekt => projekt;
    public Guid WurzelId => wurzelId;
    public string Art => art;

    /// <summary>Programmweite Listenergaenzungen, einmal je Bearbeitung gelesen. Ohne Speicher leer.</summary>
    public IReadOnlyList<ListenErgaenzung> Ergaenzungen => _ergaenzungen ??= ergaenzungen?.Lade() ?? [];
    public IObjektaktenListenErgaenzungen? ErgaenzungenSpeicher => ergaenzungen;
    public void ErgaenzungenNeuLaden() => _ergaenzungen = null;

    /// <summary>Die Eintraege, die ein Auswahlfeld dieser Akte gerade anbietet: der passende
    /// Katalog (bei abhaengigen Feldern die Gruppe des gewaehlten Elternwerts), darueber die
    /// Ergaenzungen der Fachperson. Leer, wenn ein Elternwert fehlt.</summary>
    public IReadOnlyList<ObjektAuswahl> ErlaubteEintraege(ObjektAkte akte, ObjektFeldDefinition feld)
    {
        var katalog = FieldCatalog.Objektfelder;
        if (feld.KatalogIdJeEltern is { } voll && feld.Elternfeld is not null)
        {
            var code = ElternCode(akte, feld);
            if (code is null) return [];
            var gruppe = katalog.Auswahl(voll)?.Eintraege.Where(e => e.Eltern == code) ?? [];
            return ListenErgaenzungRegel.Anwenden(gruppe, voll, code, Ergaenzungen);
        }
        if (feld.KatalogId is null) return [];
        if (feld.Elternfeld is { } eltern)
        {
            var text = Lies(akte, katalog.Feld(eltern));
            if (!string.Equals(text, feld.BelegterElterntext, StringComparison.OrdinalIgnoreCase)) return [];
        }
        return ListenErgaenzungRegel.Anwenden(katalog.Auswahl(feld.KatalogId)?.Eintraege ?? [], feld.KatalogId, null, Ergaenzungen);
    }
    public ObjektAkte Wurzel => projekt.Objektakten.SingleOrDefault(a => a.Id == wurzelId && a.Art == art)
        ?? _leereWurzel;

    public IReadOnlyList<ObjektAkte> Verbund => new[] { Wurzel }.Concat(projekt.Objektakten
        .Where(a => a.Bezuege.Contains(wurzelId))).ToArray();

    public void PruefeBestand()
    {
        if (art == "haltung" ? !projekt.Data.Any(r => r.Id == wurzelId)
            : art != "schacht" || !projekt.SchaechteData.Any(r => r.Id == wurzelId))
            throw new InvalidOperationException("Das Objekt gehört nicht mehr zum geöffneten Projekt.");
    }

    public string Lies(ObjektAkte akte, ObjektFeldDefinition feld)
    {
        if (ObjektaktenSchachtVererbung.Lies(this, akte, feld, out var geerbt)) return geerbt;
        if (art == "schacht" && akte.Id == wurzelId && SchachtHoehenRechnung.IstHoehenfeld(feld.Id))
            return SchachtHoehenRechnung.Fuer(this).Lies(feld.Id);
        if (art == "schacht" && akte.Id == wurzelId && feld.Id == "schacht.objectid")
            return SchachtObjektId.Anzeige(this, akte).Wert;
        if (feld.Id == "deckel.hauptdeckel") return Wurzel.HauptdeckelId == akte.Id ? "Ja" : "Nein";
        if (feld.Id == "deckel.knoten") return string.Join(", ", akte.Bezuege.Select(Bezugsname));
        if (feld.Id == "schacht.materialgruppe" && !akte.Werte.ContainsKey(feld.Id))
        {
            // Nur eine eindeutige Gruppe des tatsächlich gewählten Materials anzeigen; kein Detail erfinden.
            var material = Lies(akte, FieldCatalog.Objektfelder.Feld("schacht.materialdetail"));
            return FieldCatalog.Objektfelder.Auswahl(feld.KatalogId!)?.Eintraege.SingleOrDefault(e =>
                material.Equals(e.Label, StringComparison.OrdinalIgnoreCase)
                || material.StartsWith(e.Label + ",", StringComparison.OrdinalIgnoreCase))?.Label ?? "";
        }
        if (feld.Speicherfeld is { } key && akte.Id == wurzelId)
        {
            if (art == "haltung") return projekt.Data.Single(r => r.Id == wurzelId).GetFieldValue(key);
            var schacht = projekt.SchaechteData.Single(r => r.Id == wurzelId);
            return schacht.GetFieldValue(SchachtFeldnamen.Feld(schacht, key));
        }
        return akte.Werte.TryGetValue(feld.Id, out var wert) ? wert.Text : "";
    }

    public string Bezugsname(Guid id)
    {
        var h = projekt.Data.FirstOrDefault(r => r.Id == id);
        if (h is not null) return h.GetFieldValue(FieldKeys.HoldingName);
        var s = projekt.SchaechteData.FirstOrDefault(r => r.Id == id);
        return s is null ? id.ToString() : s.GetFieldValue(SchachtFeldnamen.Feld(s, "Schachtnummer"));
    }

    /// <summary>Listen des offenen Objekts, in denen eigene Zeilen angelegt werden duerfen.
    /// Abgeleitet aus dem Katalog - es gibt bewusst keine zweite Aufzaehlung erlaubter Arten.</summary>
    public IEnumerable<ObjektUnterliste> AnlegbareListen => FieldCatalog.Objektfelder.Unterlisten
        .Where(l => l.Art == art && l.DarfAnlegen);

    public bool DarfAnlegen(string unterart) => AnlegbareListen.Any(l => l.ZeigtAufObjektart == unterart);

    public ObjektAkte Neu(string unterart)
    {
        PruefeBestand();
        if (!DarfAnlegen(unterart))
            throw new InvalidOperationException(
                $"An einem Objekt der Art '{art}' gibt es keine Liste, in der '{unterart}' angelegt werden darf.");
        var neu = new ObjektAkte { Art = unterart, Bezuege = [wurzelId] };
        projekt.Objektakten.Add(neu);
        Geaendert();
        return neu;
    }

    public void SetzeHauptdeckel(ObjektAkte deckel)
    {
        PruefeBestand();
        if (art != "schacht" || deckel.Art != "deckel" || !deckel.Bezuege.Contains(wurzelId)
            || !projekt.Objektakten.Contains(deckel)) throw new InvalidOperationException("Dieser Deckel gehört nicht zum Schacht.");
        var wurzel = Sichere(Wurzel);
        wurzel.HauptdeckelId = deckel.Id;
        Geaendert();
    }

    public void Schreibe(ObjektAkte akte, ObjektFeldDefinition feld, string erwartet, string text, ObjektAuswahl? auswahl = null)
    {
        PruefeBestand();
        if (akte.Art != feld.Art || feld.NurLesen || akte.Id != wurzelId && !Verbund.Contains(akte))
            throw new InvalidOperationException("Dieses Feld ist hier nicht bearbeitbar.");
        var aktuell = Lies(akte, feld);
        if (aktuell != erwartet && aktuell != text)
            throw new InvalidOperationException("Der Wert wurde inzwischen geändert. Bitte die Akte neu öffnen; deine Eingabe wurde nicht übernommen.");
        ObjektFeldPruefung.Pruefe(feld, text);
        // Der Eintrag darf aus dem Katalog des Feldes oder aus dessen Katalog je Elternwert
        // stammen; gemerkt wird, aus welchem - so bleibt ein gespeicherter Wert spaeter der
        // richtigen Liste zuzuordnen.
        var katalogId = KatalogDesEintrags(feld, auswahl);
        // Ein eigener Eintrag muss in der aktuell angebotenen Liste stehen; er kommt aus der
        // Ergaenzungsdatei, nicht aus einem Katalog, und darf nicht einfach behauptet werden.
        if (auswahl is not null && (katalogId is null || auswahl.Eigen && !ErlaubteEintraege(akte, feld).Contains(auswahl)))
            throw new InvalidOperationException("Der Auswahlwert gehört nicht zu diesem Katalog.");
        akte = Sichere(akte);
        string? bestandswert = null;
        if (feld.Speicherfeld is { } key && akte.Id == wurzelId)
        {
            bestandswert = Normalisiere(feld, text);
            if (art == "haltung") projekt.Data.Single(r => r.Id == wurzelId).SetFieldValue(key, bestandswert, FieldSource.Manual, true);
            else
            {
                var schacht = projekt.SchaechteData.Single(r => r.Id == wurzelId);
                schacht.SetFieldValue(SchachtFeldnamen.Feld(schacht, key), bestandswert, FieldSource.Manual, true);
            }
        }
        akte.Werte[feld.Id] = new ObjektFeldWert
        {
            Text = text, KatalogId = katalogId,
            Originalcode = auswahl?.OriginalCode, LokalerEintrag = auswahl?.Index,
            Bestandswert = bestandswert, VonHand = true, GeaendertUtc = DateTime.UtcNow
        };
        Geaendert();
        ZieheAbhaengigeFelderNach(akte, feld);
    }

    /// <summary>WebGIS-Verhalten (Entscheid Pascal 12.09.2026): Wechselt der Elternwert, gehoert ein
    /// bisheriges Detail meist nicht mehr zur neuen Gruppe - dann springt es auf den ersten Eintrag
    /// der neuen Liste, statt sichtbar falsch stehen zu bleiben. Drei Faelle bleiben unangetastet:
    /// ein leeres Feld (ein Gruppenwechsel darf keinen Wert erfinden), ein Wert, der auch zur neuen
    /// Gruppe gehoert, und eine leere Kindliste (dort gibt es nichts zu setzen).</summary>
    private void ZieheAbhaengigeFelderNach(ObjektAkte akte, ObjektFeldDefinition eltern)
    {
        foreach (var kind in FieldCatalog.Objektfelder.Felder
                     .Where(f => f.Elternfeld == eltern.Id && f.Art == eltern.Art && !f.NurLesen))
        {
            var aktuell = Lies(akte, kind);
            if (aktuell.Length == 0) continue;
            var erlaubt = ErlaubteEintraege(akte, kind);
            if (erlaubt.Count == 0) continue;
            // Ein Speicherfeld liefert den normalisierten Bestandswert zurueck, nicht den Listentext -
            // darum zaehlt zuerst der gemerkte Originalcode.
            var wert = akte.Werte.GetValueOrDefault(kind.Id);
            if (erlaubt.Any(e => wert?.Originalcode is { Length: > 0 } code && e.OriginalCode == code
                    || e.Label == aktuell || wert is not null && e.Label == wert.Text)) continue;
            Schreibe(akte, kind, aktuell, erlaubt[0].Label, erlaubt[0]);
        }
    }

    /// <summary>Kennung des Katalogs, aus dem ein gewaehlter Eintrag stammt - der Katalog des
    /// Feldes oder sein Katalog je Elternwert. <c>null</c>, wenn er in keinem von beiden steht.</summary>
    public static string? KatalogDesEintrags(ObjektFeldDefinition feld, ObjektAuswahl? auswahl)
    {
        if (auswahl is null) return null;
        var katalog = FieldCatalog.Objektfelder;
        // Ein eigener Eintrag gehoert zur Liste, in der er angeboten wird; ob er dort wirklich
        // steht, prueft Schreibe gegen die aktuell erlaubten Eintraege.
        if (auswahl.Eigen) return feld.KatalogIdJeEltern ?? feld.KatalogId;
        if (feld.KatalogIdJeEltern is { } voll && katalog.Auswahl(voll)?.Eintraege.Contains(auswahl) == true) return voll;
        if (katalog.Auswahl(feld.KatalogId)?.Eintraege.Contains(auswahl) == true) return feld.KatalogId;
        return null;
    }

    /// <summary>Code des aktuell gewaehlten Elternwerts eines abhaengigen Feldes - aus dem
    /// gespeicherten Originalcode, sonst ueber den Text aus dem Eltern-Katalog. <c>null</c>,
    /// wenn kein Elternwert gewaehlt ist oder der Text zu keinem Eintrag passt.</summary>
    public string? ElternCode(ObjektAkte akte, ObjektFeldDefinition feld)
    {
        if (feld.Elternfeld is not { } elternId) return null;
        var eltern = FieldCatalog.Objektfelder.Feld(elternId);
        if (akte.Werte.TryGetValue(elternId, out var wert) && wert.Originalcode is { Length: > 0 } code
            && wert.Text == Lies(akte, eltern))
            return code;
        var text = Lies(akte, eltern);
        if (text.Length == 0) return null;
        var treffer = FieldCatalog.Objektfelder.Auswahl(eltern.KatalogId)?.Eintraege
            .Where(e => string.Equals(e.Label, text, StringComparison.OrdinalIgnoreCase)).Take(2).ToArray() ?? [];
        return treffer.Length == 1 ? treffer[0].OriginalCode : null;
    }

    internal static string Normalisiere(ObjektFeldDefinition feld, string text)
    {
        if (feld.Speicherfeld == FieldKeys.ConditionClass)
        {
            var m = Regex.Match(text, @"\bZ([0-4])\b");
            if (m.Success) return m.Groups[1].Value;
        }
        return feld.Id switch
        {
            "haltung.usage" => NutzungsartVokabular.Normalisieren(text),
            "haltung.material" => MaterialVokabular.Normalisieren(text),
            "haltung.profile" => ProfiltypVokabular.Normalisieren(text),
            "schacht.materialdetail" => SchachtMaterialVokabular.Normalisieren(text),
            "schacht.funktion" => SchachtFunktionVokabular.Normalisieren(text),
            "schacht.form" => SchachtformVokabular.Normalisieren(text),
            _ => text
        };
    }

    private ObjektAkte Sichere(ObjektAkte akte)
    {
        var vorhanden = projekt.Objektakten.SingleOrDefault(a => a.Id == akte.Id);
        if (vorhanden is not null) return vorhanden;
        projekt.Objektakten.Add(akte);
        return akte;
    }

    private void Geaendert()
    {
        // Alte Programmversionen lehnen Format 3 ab, statt die neuen Beziehungen zu verlieren.
        projekt.Version = Math.Max(projekt.Version, 3);
        projekt.Dirty = true;
        projekt.ModifiedAtUtc = DateTime.UtcNow;
    }

    public string BerechneteTiefe()
    {
        if (art == "haltung")
        {
            if (!Zahl(Lies(Wurzel, FieldCatalog.Objektfelder.Feld("haltung.fromlevel")), out var von)
                || !Zahl(Lies(Wurzel, FieldCatalog.Objektfelder.Feld("haltung.tolevel")), out var bis)
                || !Zahl(Lies(Wurzel, FieldCatalog.Objektfelder.Feld("haltung.length")), out var laenge) || laenge <= 0) return "";
            return $"Berechnetes Gefälle: {((von - bis) / laenge * 100).ToString("0.00", CultureInfo.CurrentCulture)} % (Koten Anfang/Ende und Länge)";
        }
        if (art != "schacht") return "";
        var d = Lies(Wurzel, FieldCatalog.Objektfelder.Feld("schacht.deckelhoehe"));
        var s = Lies(Wurzel, FieldCatalog.Objektfelder.Feld("schacht.sohlenhoehe"));
        if (!Zahl(d, out var deckel) || !Zahl(s, out var sohle)) return "";
        return $"Berechnete Tiefe: {(deckel - sohle).ToString("0.00", CultureInfo.CurrentCulture)} m (Deckel − Sohle)"
            + (deckel < sohle ? " · Bitte prüfen: Deckel liegt unter der Sohle." : "");
    }
    private static bool Zahl(string text, out decimal wert)
        => Common.FachzahlParser.TryParseMeasurement(text, out wert);
}
