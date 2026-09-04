using System.Globalization;
using System.Text.RegularExpressions;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Export.Geonis;

/// <summary>
/// Baut den Rueckschrieb-Plan aus Projektdaten und Katasterindex.
///
/// Grundregeln (bewusst streng, weil GEONIS kein Rueckgaengig kennt):
///   * Nur Objekte mit eindeutiger Bezeichnung UND vorhandener OBJ_ID im Kataster.
///   * Nur Werte, die im Programm wirklich gesetzt sind. Leer heisst nie "loeschen".
///   * Nur Werte, die sich vom Katasterstand unterscheiden.
///   * Nicht lesbare oder im Kataster unbekannte Werte werden nicht geraten, sondern als
///     Hinweis protokolliert.
/// </summary>
public sealed class Sia405ExportPlanBuilder : ISia405ExportPlanBuilder
{
    /// <summary>Feldname der Schachtnummer im Schachtdatensatz.</summary>
    public const string FeldSchachtnummer = "Schachtnummer";

    /// <summary>Feldname des Schachtmasses im Schachtdatensatz.</summary>
    public const string FeldSchachtDimension = "Dimension";

    /// <summary>Laengengrenze fuer Bemerkungen (SIA405 fuehrt Bemerkung als TEXT*80).</summary>
    public const int MaxBemerkungLaenge = 80;

    /// <summary>Zerlegt einen Zustandswert wie "Z3" in Praefix und Endziffer.</summary>
    private static readonly Regex ZustandMuster = new(@"^(?<praefix>\D*)(?<ziffer>\d)$", RegexOptions.CultureInvariant);

    public Sia405ExportPlan Erstelle(Project projekt, Sia405KatasterIndex kataster, Sia405ExportOptionen optionen)
    {
        ArgumentNullException.ThrowIfNull(projekt);
        ArgumentNullException.ThrowIfNull(kataster);
        ArgumentNullException.ThrowIfNull(optionen);

        var objekte = new List<Sia405ExportObjekt>();
        var hinweise = new List<Sia405ExportHinweis>();
        var rohrprofile = new Dictionary<string, Sia405ExportObjekt>(StringComparer.Ordinal);
        var datum = FormatiereDatum(optionen.AenderungsDatum, kataster.LetzteAenderungBeispiel);

        foreach (var record in projekt.Data)
            VerarbeiteHaltung(record, kataster, datum, objekte, hinweise, rohrprofile);

        foreach (var schacht in projekt.SchaechteData)
            VerarbeiteSchacht(schacht, kataster, datum, objekte, hinweise);

        // Rohrprofile zuerst: sie werden von den Haltungen referenziert.
        var alle = new List<Sia405ExportObjekt>(rohrprofile.Values);
        alle.AddRange(objekte);

        return new Sia405ExportPlan
        {
            KatasterQuelle = optionen.KatasterQuelle,
            AenderungsDatum = optionen.AenderungsDatum,
            Modell = kataster.Modell,
            Objekte = alle,
            Hinweise = hinweise,
            AttributReihenfolge = kataster.AttributReihenfolge
        };
    }

    /// <summary>
    /// INTERLIS 2.3 kennt DATE (yyyymmdd) und XMLDate (yyyy-mm-dd). Wir uebernehmen die
    /// Schreibweise der Quelldatei; ohne Beispiel gilt die XMLDate-Schreibweise.
    /// </summary>
    internal static string FormatiereDatum(DateOnly datum, string? beispielAusKataster)
    {
        var beispiel = (beispielAusKataster ?? string.Empty).Trim();
        return Regex.IsMatch(beispiel, @"^\d{8}$")
            ? datum.ToString("yyyyMMdd", CultureInfo.InvariantCulture)
            : datum.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    private static void VerarbeiteHaltung(
        HaltungRecord record,
        Sia405KatasterIndex kataster,
        string datum,
        List<Sia405ExportObjekt> objekte,
        List<Sia405ExportHinweis> hinweise,
        Dictionary<string, Sia405ExportObjekt> rohrprofile)
    {
        var name = record.GetFieldValue(FieldKeys.HoldingName).Trim();
        if (name.Length == 0)
            return;

        var dnText = record.GetFieldValue(FieldKeys.NominalDiameterMm).Trim();
        var material = record.GetFieldValue(FieldKeys.PipeMaterial).Trim();
        var zustand = record.GetFieldValue(FieldKeys.ConditionClass).Trim();
        var bemerkung = Einzeilig(record.GetFieldValue(FieldKeys.Remarks));

        // Ohne beurteilte Werte gibt es nichts zurueckzuschreiben — dann auch keinen Hinweis,
        // sonst waere das Protokoll fuer ein grosses Projekt unlesbar.
        if (dnText.Length == 0 && material.Length == 0 && zustand.Length == 0 && bemerkung.Length == 0)
            return;

        var key = Sia405NameKey.Normalize(name);
        if (kataster.MehrdeutigeHaltungen.Contains(key))
        {
            hinweise.Add(new Sia405ExportHinweis(
                name, "Die Bezeichnung kommt im Kataster mehrfach vor. Ohne eindeutigen Schluessel wird nichts geschrieben."));
            return;
        }

        if (!kataster.Haltungen.TryGetValue(key, out var katasterHaltung))
        {
            hinweise.Add(new Sia405ExportHinweis(name, "Im Kataster nicht gefunden."));
            return;
        }

        if (string.IsNullOrWhiteSpace(katasterHaltung.ObjId))
        {
            hinweise.Add(new Sia405ExportHinweis(
                name, "Im Kataster ohne OBJ_ID. Ohne stabilen Schluessel wird nichts geschrieben."));
            return;
        }

        var haltungsAenderungen = new List<Sia405AttributAenderung>();

        var dn = Sia405MassParser.LiesMillimeter(dnText);
        if (dnText.Length > 0 && dn is null)
        {
            hinweise.Add(new Sia405ExportHinweis(name, $"DN '{dnText}' ist nicht eindeutig lesbar. Nicht uebernommen."));
        }
        else if (dn.HasValue)
        {
            var hoehe = dn.Value.ToString(CultureInfo.InvariantCulture);
            if (!GleicherZahlwert(katasterHaltung.LichteHoehe, hoehe))
                haltungsAenderungen.Add(new Sia405AttributAenderung("Lichte_Hoehe", katasterHaltung.LichteHoehe, hoehe));

            var breite = BerechneBreite(dn.Value, katasterHaltung, kataster);
            if (breite.HasValue)
            {
                var breiteText = breite.Value.ToString(CultureInfo.InvariantCulture);
                if (!GleicherZahlwert(katasterHaltung.LichteBreite, breiteText))
                    haltungsAenderungen.Add(new Sia405AttributAenderung("Lichte_Breite", katasterHaltung.LichteBreite, breiteText));
            }
            else
            {
                hinweise.Add(new Sia405ExportHinweis(
                    name,
                    "Lichte_Breite nicht bestimmbar: im Kataster fehlt das Hoehen-Breiten-Verhaeltnis des Rohrprofils. Nur die Hoehe wird geliefert."));
            }
        }

        if (material.Length > 0)
        {
            if (kataster.MaterialVokabular.TryGetValue(material.ToUpperInvariant(), out var katasterSchreibweise))
            {
                if (!string.Equals((katasterHaltung.Material ?? string.Empty).Trim(), katasterSchreibweise, StringComparison.Ordinal))
                    haltungsAenderungen.Add(new Sia405AttributAenderung("Material", katasterHaltung.Material, katasterSchreibweise));
            }
            else
            {
                hinweise.Add(new Sia405ExportHinweis(
                    name, $"Material '{material}' kommt im Kataster nicht vor. Schreibweise unbekannt, nicht uebernommen."));
            }
        }

        if (haltungsAenderungen.Count > 0)
        {
            haltungsAenderungen.Add(new Sia405AttributAenderung("Letzte_Aenderung", null, datum));
            objekte.Add(new Sia405ExportObjekt
            {
                Art = Sia405ObjektArt.Haltung,
                Klasse = "Haltung",
                Tid = katasterHaltung.Tid,
                ObjId = katasterHaltung.ObjId!,
                Bezeichnung = katasterHaltung.Bezeichnung,
                Aenderungen = haltungsAenderungen
            });

            NimmRohrprofilAuf(name, katasterHaltung, kataster, rohrprofile, hinweise);
        }

        VerarbeiteKanal(name, katasterHaltung, kataster, zustand, bemerkung, datum, objekte, hinweise);
    }

    private static void VerarbeiteKanal(
        string name,
        Sia405KatasterHaltung katasterHaltung,
        Sia405KatasterIndex kataster,
        string zustand,
        string bemerkung,
        string datum,
        List<Sia405ExportObjekt> objekte,
        List<Sia405ExportHinweis> hinweise)
    {
        if (zustand.Length == 0 && bemerkung.Length == 0)
            return;

        Sia405KatasterKanal? kanal = null;
        if (!string.IsNullOrWhiteSpace(katasterHaltung.KanalTid))
            kataster.KanaeleNachTid.TryGetValue(katasterHaltung.KanalTid!, out kanal);

        if (kanal is null || string.IsNullOrWhiteSpace(kanal.ObjId))
        {
            hinweise.Add(new Sia405ExportHinweis(
                name,
                "Zustand und Bemerkung nicht uebernommen: der zugehoerige Kanal ist im Kataster nicht auffindbar oder hat keine OBJ_ID."));
            return;
        }

        var aenderungen = new List<Sia405AttributAenderung>();

        if (zustand.Length > 0)
        {
            var ergebnis = BestimmeZustand(zustand, kataster.ZustandVokabular);
            if (ergebnis.Hinweis is not null)
                hinweise.Add(new Sia405ExportHinweis(name, ergebnis.Hinweis));
            if (ergebnis.Wert is not null
                && !string.Equals((kanal.BaulicherZustand ?? string.Empty).Trim(), ergebnis.Wert, StringComparison.Ordinal))
            {
                aenderungen.Add(new Sia405AttributAenderung("Baulicher_Zustand", kanal.BaulicherZustand, ergebnis.Wert));
            }
        }

        if (bemerkung.Length > 0)
        {
            if (bemerkung.Length > MaxBemerkungLaenge)
            {
                hinweise.Add(new Sia405ExportHinweis(
                    name, $"Bemerkung ist laenger als {MaxBemerkungLaenge} Zeichen. Nicht uebernommen."));
            }
            else if (!string.Equals((kanal.Bemerkung ?? string.Empty).Trim(), bemerkung, StringComparison.Ordinal))
            {
                aenderungen.Add(new Sia405AttributAenderung("Bemerkung", kanal.Bemerkung, bemerkung));
            }
        }

        if (aenderungen.Count == 0)
            return;

        aenderungen.Add(new Sia405AttributAenderung("Letzte_Aenderung", null, datum));
        objekte.Add(new Sia405ExportObjekt
        {
            Art = Sia405ObjektArt.Kanal,
            Klasse = "Kanal",
            Tid = kanal.Tid,
            ObjId = kanal.ObjId!,
            Bezeichnung = kanal.Bezeichnung ?? name,
            Aenderungen = aenderungen
        });
    }

    private static void VerarbeiteSchacht(
        SchachtRecord record,
        Sia405KatasterIndex kataster,
        string datum,
        List<Sia405ExportObjekt> objekte,
        List<Sia405ExportHinweis> hinweise)
    {
        var nummer = record.GetFieldValue(FeldSchachtnummer).Trim();
        if (nummer.Length == 0)
            return;

        var dimensionText = record.GetFieldValue(FeldSchachtDimension).Trim();
        var zustand = record.GetFieldValue(FieldKeys.ConditionClass).Trim();
        var bemerkung = Einzeilig(record.GetFieldValue(FieldKeys.Remarks));

        if (dimensionText.Length == 0 && zustand.Length == 0 && bemerkung.Length == 0)
            return;

        var key = Sia405NameKey.Normalize(nummer);
        if (kataster.MehrdeutigeSchaechte.Contains(key))
        {
            hinweise.Add(new Sia405ExportHinweis(
                nummer, "Die Bezeichnung kommt im Kataster mehrfach vor. Ohne eindeutigen Schluessel wird nichts geschrieben."));
            return;
        }

        if (!kataster.Schaechte.TryGetValue(key, out var katasterSchacht))
        {
            hinweise.Add(new Sia405ExportHinweis(nummer, "Im Kataster nicht als Normschacht gefunden."));
            return;
        }

        if (string.IsNullOrWhiteSpace(katasterSchacht.ObjId))
        {
            hinweise.Add(new Sia405ExportHinweis(
                nummer, "Im Kataster ohne OBJ_ID. Ohne stabilen Schluessel wird nichts geschrieben."));
            return;
        }

        var aenderungen = new List<Sia405AttributAenderung>();

        if (dimensionText.Length > 0)
        {
            var mass = Sia405MassParser.LiesSchachtmass(dimensionText);
            if (mass is null)
            {
                hinweise.Add(new Sia405ExportHinweis(
                    nummer, $"Dimension '{dimensionText}' ist nicht eindeutig lesbar. Nicht uebernommen."));
            }
            else
            {
                var d1 = mass.Value.Dimension1.ToString(CultureInfo.InvariantCulture);
                var d2 = mass.Value.Dimension2.ToString(CultureInfo.InvariantCulture);
                if (!GleicherZahlwert(katasterSchacht.Dimension1, d1))
                    aenderungen.Add(new Sia405AttributAenderung("Dimension1", katasterSchacht.Dimension1, d1));
                if (!GleicherZahlwert(katasterSchacht.Dimension2, d2))
                    aenderungen.Add(new Sia405AttributAenderung("Dimension2", katasterSchacht.Dimension2, d2));
            }
        }

        if (zustand.Length > 0)
        {
            var ergebnis = BestimmeZustand(zustand, kataster.ZustandVokabular);
            if (ergebnis.Hinweis is not null)
                hinweise.Add(new Sia405ExportHinweis(nummer, ergebnis.Hinweis));
            if (ergebnis.Wert is not null
                && !string.Equals((katasterSchacht.BaulicherZustand ?? string.Empty).Trim(), ergebnis.Wert, StringComparison.Ordinal))
            {
                aenderungen.Add(new Sia405AttributAenderung("Baulicher_Zustand", katasterSchacht.BaulicherZustand, ergebnis.Wert));
            }
        }

        if (bemerkung.Length > 0)
        {
            if (bemerkung.Length > MaxBemerkungLaenge)
            {
                hinweise.Add(new Sia405ExportHinweis(
                    nummer, $"Bemerkung ist laenger als {MaxBemerkungLaenge} Zeichen. Nicht uebernommen."));
            }
            else if (!string.Equals((katasterSchacht.Bemerkung ?? string.Empty).Trim(), bemerkung, StringComparison.Ordinal))
            {
                aenderungen.Add(new Sia405AttributAenderung("Bemerkung", katasterSchacht.Bemerkung, bemerkung));
            }
        }

        if (aenderungen.Count == 0)
            return;

        aenderungen.Add(new Sia405AttributAenderung("Letzte_Aenderung", null, datum));
        objekte.Add(new Sia405ExportObjekt
        {
            Art = Sia405ObjektArt.Normschacht,
            Klasse = "Normschacht",
            Tid = katasterSchacht.Tid,
            ObjId = katasterSchacht.ObjId!,
            Bezeichnung = katasterSchacht.Bezeichnung,
            Aenderungen = aenderungen
        });
    }

    /// <summary>
    /// Liefert das Rohrprofil der Haltung unveraendert mit, damit GEONIS die Breite aus dem
    /// Hoehen-Breiten-Verhaeltnis ableiten kann. Fehlt das Verhaeltnis bei einem Kreisprofil,
    /// wird es auf 1 gesetzt — das ist der einzige Fall, in dem der Wert eindeutig ist.
    /// </summary>
    private static void NimmRohrprofilAuf(
        string name,
        Sia405KatasterHaltung katasterHaltung,
        Sia405KatasterIndex kataster,
        Dictionary<string, Sia405ExportObjekt> rohrprofile,
        List<Sia405ExportHinweis> hinweise)
    {
        if (string.IsNullOrWhiteSpace(katasterHaltung.RohrprofilTid)
            || !kataster.RohrprofileNachTid.TryGetValue(katasterHaltung.RohrprofilTid!, out var profil))
        {
            hinweise.Add(new Sia405ExportHinweis(
                name, "Im Kataster ohne Rohrprofil-Verweis. Die Datei enthaelt fuer diese Haltung kein Rohrprofil."));
            return;
        }

        if (rohrprofile.ContainsKey(profil.Tid))
            return;

        var aenderungen = new List<Sia405AttributAenderung>();
        if (string.IsNullOrWhiteSpace(profil.HoehenBreitenverhaeltnis) && IstKreisprofil(profil.Profiltyp))
            aenderungen.Add(new Sia405AttributAenderung("HoehenBreitenverhaeltnis", profil.HoehenBreitenverhaeltnis, "1.0"));

        rohrprofile[profil.Tid] = new Sia405ExportObjekt
        {
            Art = Sia405ObjektArt.Rohrprofil,
            Klasse = "Rohrprofil",
            Tid = profil.Tid,
            ObjId = profil.ObjId ?? string.Empty,
            Bezeichnung = profil.Bezeichnung ?? string.Empty,
            Aenderungen = aenderungen
        };
    }

    private static bool IstKreisprofil(string? profiltyp)
        => !string.IsNullOrWhiteSpace(profiltyp)
           && profiltyp!.Contains("kreis", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Breite aus Hoehe und Hoehen-Breiten-Verhaeltnis des Rohrprofils (Breite = Hoehe / Verhaeltnis).
    /// Ersatzregel: Fuehrt das Kataster fuer diese Haltung bereits gleiche Hoehe und Breite, ist das
    /// Profil rund oder quadratisch und die Breite entspricht der Hoehe.
    /// </summary>
    private static int? BerechneBreite(int hoeheMm, Sia405KatasterHaltung katasterHaltung, Sia405KatasterIndex kataster)
    {
        if (!string.IsNullOrWhiteSpace(katasterHaltung.RohrprofilTid)
            && kataster.RohrprofileNachTid.TryGetValue(katasterHaltung.RohrprofilTid!, out var profil)
            && TryLiesDezimal(profil.HoehenBreitenverhaeltnis, out var verhaeltnis)
            && verhaeltnis > 0m)
        {
            var breite = Math.Round(hoeheMm / verhaeltnis, MidpointRounding.AwayFromZero);
            if (breite > 0m && breite <= Sia405MassParser.MaxMillimeter)
                return (int)breite;
            return null;
        }

        if (!string.IsNullOrWhiteSpace(katasterHaltung.LichteBreite)
            && GleicherZahlwert(katasterHaltung.LichteHoehe, katasterHaltung.LichteBreite))
        {
            return hoeheMm;
        }

        return null;
    }

    /// <summary>
    /// Bildet die Zustandsklasse des Programms auf den Katasterwert von Baulicher_Zustand ab.
    ///
    /// Die Schreibweise wird nicht erfunden, sondern aus der Katasterdatei abgeleitet: zuerst ein
    /// Wert mit derselben Endziffer, sonst das gemeinsame Praefix der vorkommenden Werte
    /// (Z0..Z4 -> "Z"). Passt beides nicht, wird nichts geschrieben.
    /// </summary>
    private static WertErgebnis BestimmeZustand(string zustandsklasse, IReadOnlySet<string> vokabular)
    {
        if (!int.TryParse(zustandsklasse, NumberStyles.Integer, CultureInfo.InvariantCulture, out var stufe)
            || stufe < 0 || stufe > 4)
        {
            return new WertErgebnis(
                null,
                $"Zustandsklasse '{zustandsklasse}' hat im Kataster keine Entsprechung (moeglich sind 0 bis 4). Nicht uebernommen.");
        }

        var ziffer = stufe.ToString(CultureInfo.InvariantCulture);

        foreach (var wert in vokabular)
        {
            var treffer = ZustandMuster.Match(wert);
            if (treffer.Success && string.Equals(treffer.Groups["ziffer"].Value, ziffer, StringComparison.Ordinal))
                return new WertErgebnis(wert, null);
        }

        if (vokabular.Count == 0)
        {
            return new WertErgebnis(
                "Z" + ziffer,
                "Baulicher_Zustand kommt im Kataster noch nirgends vor. Die Schreibweise Z0 bis Z4 ist mit GEONIS zu bestaetigen.");
        }

        string? praefix = null;
        foreach (var wert in vokabular)
        {
            var treffer = ZustandMuster.Match(wert);
            if (!treffer.Success)
                return new WertErgebnis(
                    null,
                    $"Zustandsklasse {stufe} nicht uebernommen: die Werte von Baulicher_Zustand im Kataster folgen keinem erkennbaren Muster.");

            var gefunden = treffer.Groups["praefix"].Value;
            if (praefix is null)
                praefix = gefunden;
            else if (!string.Equals(praefix, gefunden, StringComparison.Ordinal))
                return new WertErgebnis(
                    null,
                    $"Zustandsklasse {stufe} nicht uebernommen: im Kataster kommen verschiedene Schreibweisen von Baulicher_Zustand vor.");
        }

        return new WertErgebnis(praefix + ziffer, null);
    }

    /// <summary>Vergleicht zwei Zahlwerte inhaltlich ("150" und "150.0" sind gleich).</summary>
    internal static bool GleicherZahlwert(string? links, string? rechts)
    {
        var l = (links ?? string.Empty).Trim();
        var r = (rechts ?? string.Empty).Trim();

        if (TryLiesDezimal(l, out var lz) && TryLiesDezimal(r, out var rz))
            return lz == rz;

        return string.Equals(l, r, StringComparison.Ordinal);
    }

    private static bool TryLiesDezimal(string? text, out decimal wert)
        => decimal.TryParse(
            (text ?? string.Empty).Trim().Replace(',', '.'),
            NumberStyles.Number,
            CultureInfo.InvariantCulture,
            out wert);

    /// <summary>Macht aus einem mehrzeiligen Feld eine Zeile — INTERLIS-TEXT kennt keine Zeilenumbrueche.</summary>
    internal static string Einzeilig(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var einzeilig = Regex.Replace(text.Replace("\r\n", "\n", StringComparison.Ordinal), @"\s*\n\s*", " / ");
        return Regex.Replace(einzeilig, @"\s{2,}", " ").Trim();
    }

    private sealed record WertErgebnis(string? Wert, string? Hinweis);
}
