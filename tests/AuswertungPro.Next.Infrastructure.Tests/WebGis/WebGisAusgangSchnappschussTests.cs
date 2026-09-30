using System;
using System.Linq;
using System.Text;
using AuswertungPro.Next.Application.WebGis;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.WebGis;

/// <summary>
/// Wartbarkeitsaudit 30.09.2026, WG-D: Bericht, Log, Ergebnistext, Uebersicht und Vergleichsliste fuer JEDEN
/// Schreibausgang einer Position und einer Massnahme, zeichengenau festgehalten — vor dem Umbau auf die berechnete
/// Eigenschaft «Ausgang» aufgenommen. Weicht der Schnappschuss ab, hat sich eine Ausgabe geaendert.
/// Die Zustaende sind die, die <see cref="WebGisExportUseCase"/> erzeugt; einer ist nur konstruiert
/// (Objekt «geschrieben» ohne Serverbestaetigung) und haelt fest, was die Leser heute daraus machen.
/// </summary>
public sealed class WebGisAusgangSchnappschussTests
{
    private static readonly DateTime Zeit = new(2026, 9, 30, 14, 5, 6);

    private static Guid Id(int n) => new($"00000000-0000-0000-0000-{n:000000000000}");

    private static WebGisExportPosition Objekt(WebGisObjektart art, string name, int id, bool mitAenderung = true)
    {
        var p = new WebGisExportPosition { Objektart = art, Bezeichnung = name, GlobalId = "G-" + name, RecordId = Id(id) };
        if (mitAenderung)
        {
            p.Aenderungen.Add(new WebGisFeldAenderung
            {
                RefId = "r-" + name, Feld = "Zustand", Alt = "102", AltText = "Mittlere Maengel (Z2)", Neu = "104", NeuText = "Keine Maengel (Z4)",
            });
            p.Vergleich.Add(new WebGisFeldVergleich
            {
                Feld = "Zustand", SewerStudio = "4", WebGis = "Z2", Art = WebGisVergleichsArt.Aendern, Nachher = "Z4", RefId = "r-" + name,
            });
        }
        return p;
    }

    private static WebGisSanierungPosition Massnahme(WebGisExportPlan plan, string eltern, int id)
    {
        var p = Objekt(WebGisObjektart.Schacht, eltern, id, mitAenderung: false);
        plan.Positionen.Add(p);
        var s = new WebGisSanierungPosition
        {
            Objektart = WebGisObjektart.Schacht, ElternBezeichnung = eltern, ElternGlobalId = "G-" + eltern, ElternRecordId = Id(id), AkteId = Id(100 + id),
        };
        s.Felder["art"] = "2";
        s.Anzeige.Add("Art: Reparatur (R)");
        s.Anzeige.Add("Status: Ausgeführt");
        plan.Sanierungen.Add(s);
        p.Vergleich.Add(new WebGisFeldVergleich
        {
            Feld = "Sanierungsmassnahme", SewerStudio = "Reparatur", WebGis = "—", Art = WebGisVergleichsArt.Anlegen, Nachher = "Reparatur", Massnahme = s,
        });
        return s;
    }

    /// <summary>Alle Ausgaenge in einem Plan.</summary>
    internal static WebGisExportPlan Plan()
    {
        var plan = new WebGisExportPlan();
        plan.Hinweise.Add("Planhinweis.");

        // Objekte (Haltungen)
        plan.Positionen.Add(Objekt(WebGisObjektart.Haltung, "H-offen", 1));
        var unveraendert = Objekt(WebGisObjektart.Haltung, "H-unveraendert", 2, mitAenderung: false);
        unveraendert.Hinweise.Add("Nur ein Hinweis.");
        plan.Positionen.Add(unveraendert);
        var gesperrt = new WebGisExportPosition { Objektart = WebGisObjektart.Haltung, Bezeichnung = "H-gesperrt", RecordId = Id(3) };
        gesperrt.Sperren.Add("Im WebGIS nicht eindeutig gefunden.");
        plan.Positionen.Add(gesperrt);
        var fehler = Objekt(WebGisObjektart.Haltung, "H-fehler", 4);
        fehler.SchreibFehler = "Objekt wurde im WebGIS seit der Prüfung geändert (Zustand) — nicht geschrieben, bitte neu prüfen.";
        plan.Positionen.Add(fehler);
        var verworfen = Objekt(WebGisObjektart.Haltung, "H-verworfen", 5);
        verworfen.VomServerBestaetigt = true;
        verworfen.SchreibFehler = "Zustand: vom WebGIS nicht übernommen — das Feld steht danach unverändert da.";
        plan.Positionen.Add(verworfen);
        var bestaetigt = Objekt(WebGisObjektart.Haltung, "H-bestaetigt", 6);
        bestaetigt.VomServerBestaetigt = true;
        bestaetigt.Geschrieben = true;
        bestaetigt.Hinweise.Add("Geschrieben, aber nicht nachgeprüft — beim erneuten Lesen kein eindeutiger Treffer.");
        plan.Positionen.Add(bestaetigt);
        var nachgeprueft = Objekt(WebGisObjektart.Haltung, "H-nachgeprueft", 7);
        nachgeprueft.VomServerBestaetigt = true;
        nachgeprueft.Geschrieben = true;
        nachgeprueft.Nachgeprueft = true;
        plan.Positionen.Add(nachgeprueft);
        var ohneServer = Objekt(WebGisObjektart.Haltung, "H-ohneServer", 8); // nur konstruiert
        ohneServer.Geschrieben = true;
        plan.Positionen.Add(ohneServer);

        // Massnahmen (je an einem eigenen Schacht)
        Massnahme(plan, "S-offen", 11);
        var vorhanden = Massnahme(plan, "S-vorhanden", 12);
        vorhanden.Sperren.Add("Im WebGIS bereits vorhanden (Reparatur / Ausgeführt) — nicht doppelt angelegt.");
        var ohneArt = Massnahme(plan, "S-gesperrt", 13);
        ohneArt.Sperren.Add("Art fehlt in der Akte — nicht angelegt.");
        var mFehler = Massnahme(plan, "S-fehler", 14);
        mFehler.SchreibFehler = "Schreibfehler: Serverfehler 500";
        var mBestaetigt = Massnahme(plan, "S-bestaetigt", 15);
        mBestaetigt.Geschrieben = true;
        mBestaetigt.NeueId = "66921";
        var mNachgeprueft = Massnahme(plan, "S-nachgeprueft", 16);
        mNachgeprueft.Geschrieben = true;
        mNachgeprueft.Nachgeprueft = true;
        mNachgeprueft.NeueId = "66922";
        mNachgeprueft.NeueGlobalId = "{NEU-1}";
        var mUngeklaert = Massnahme(plan, "S-ungeklaert", 17);
        mUngeklaert.Geschrieben = true;
        mUngeklaert.NeueId = "66923";
        mUngeklaert.Ungeklaert = "Vom Server bestätigt, aber nicht nachgeprüft — keine neue Zeile. Vor einem neuen Versuch im WebGIS nachsehen.";
        var nichtSchreibbar = new WebGisSanierungPosition
        {
            Objektart = WebGisObjektart.Schacht, ElternBezeichnung = "S-ohneGlobalId", ElternRecordId = Id(18), AkteId = Id(118),
        };
        nichtSchreibbar.Felder["art"] = "2";
        plan.Sanierungen.Add(nichtSchreibbar);
        return plan;
    }

    private static string OhneZeitstempel(string details)
    {
        var zeilen = details.Replace("\r\n", "\n").Split('\n').ToList();
        zeilen.RemoveAt(1); // «dd.MM.yyyy HH:mm» der Berichtszeit
        return string.Join("\n", zeilen);
    }

    internal static string Schnappschuss(WebGisExportPlan plan)
    {
        var sb = new StringBuilder();
        void Z(string s) => sb.Append(s.Replace("\r\n", "\n")).Append('\n');

        Z("## Details Vorschau");
        Z(OhneZeitstempel(WebGisExportBericht.Details(plan, mitErgebnis: false)));
        Z("## Details Ergebnis");
        Z(OhneZeitstempel(WebGisExportBericht.Details(plan, mitErgebnis: true)));
        Z("## Ergebnis");
        Z(WebGisExportBericht.Ergebnis(plan));
        Z("## Log");
        Z(WebGisExportBericht.Log(plan, Zeit, "tester"));
        Z("## LogStart/Abschluss/Abbruch");
        Z(WebGisExportBericht.LogStart(Zeit, "tester", plan));
        Z(WebGisExportBericht.LogAbschluss(Zeit, plan));
        Z(WebGisExportBericht.LogAbbruch(Zeit, "Grund", plan));
        Z("## LogGeplant");
        foreach (var p in plan.Positionen) Z(WebGisExportBericht.LogGeplant(p, Zeit));
        foreach (var s in plan.Sanierungen) Z(WebGisExportBericht.LogGeplant(s, Zeit));

        Z("## Vorschau");
        var v = WebGisExportBericht.Vorschau(plan);
        Z($"{v.Titel} | {v.Zusammenfassung} | IstFehler={v.IstFehler}");
        foreach (var zeile in v.Zeilen) Z($"Z {zeile.Objekt} | {zeile.Feld} | {zeile.Alt} | {zeile.Neu}");
        foreach (var w in v.Warnungen) Z("W " + w);

        foreach (var ergebnis in new[] { false, true })
        {
            var u = WebGisUebersicht.Aus(plan, ergebnis);
            Z($"## Uebersicht ergebnis={ergebnis}");
            Z($"{u.Titel} | {u.Kopfzeile} | mitAenderung={u.ObjekteMitAenderung} geschrieben={u.Geschrieben} neu={u.NeueMassnahmen} "
              + $"gesperrt={u.Gesperrt} hinweis={u.MitHinweis} nichts={u.NichtsZuTun} vorschlaege={u.Vorschlaege.Count}");
            foreach (var o in u.Objekte)
            {
                Z($"O {o.Objekt} | {o.Objektart} | {o.RecordId} | {o.Kurz} | sperre={o.HatSperre}");
                foreach (var z in o.Zeilen) Z($"  {z.Art} | {z.Feld} | {z.Alt} | {z.Neu}");
            }
            foreach (var m in u.Sammelmeldungen) Z("S " + m);
        }

        Z("## Vergleichsanzeige");
        foreach (var o in WebGisVergleichsanzeige.Objekte(plan))
        {
            Z($"O {o.ArtText} {o.Name} | {o.Chip} | {o.ChipTon} | {o.Untertitel} | geschrieben={o.IstGeschrieben} schreibbar={o.Schreibbar} gesperrt={o.Gesperrt} n={o.AnzahlAenderungen}");
            foreach (var z in o.Zeilen) Z($"  {z.Feld} | {z.SewerStudio} | {z.WebGis} | {z.Aktion} | {z.Grund} | {z.Ton}");
        }
        foreach (var nachher in new[] { false, true })
        {
            Z($"## Schreibliste nachDemSchreiben={nachher}");
            foreach (var z in WebGisVergleichsanzeige.Schreibliste(plan, nachher))
                Z($"{z.Objekt} | {z.Feld} | {z.Vorher} | {z.Nachher} | {z.Ergebnis} | {z.Ton}");
        }
        return sb.ToString();
    }

    [Fact]
    public void Alle_ausgaenge_ergeben_dieselben_texte()
    {
        var ist = Schnappschuss(Plan());
        var datei = Environment.GetEnvironmentVariable("WEBGIS_SCHNAPPSCHUSS_AUSGABE");
        if (!string.IsNullOrEmpty(datei)) System.IO.File.WriteAllText(datei, ist);
        Assert.Equal(Erwartet.Replace("\r\n", "\n"), ist);
    }

    private const string Erwartet = """
## Details Vorschau
WEBGIS-ÜBERTRAGUNG — VORSCHAU (nichts geschrieben)
Planhinweis.

[ÄNDERN] Haltung H-offen  (GlobalID G-H-offen)
    Zustand: Mittlere Maengel (Z2) → Keine Maengel (Z4)
[UNVERÄNDERT] Haltung H-unveraendert  (GlobalID G-H-unveraendert)
    (Nur ein Hinweis.)
[GESPERRT] Haltung H-gesperrt
    !! Im WebGIS nicht eindeutig gefunden.
[ÄNDERN] Haltung H-fehler  (GlobalID G-H-fehler)
    Zustand: Mittlere Maengel (Z2) → Keine Maengel (Z4)
    !! Objekt wurde im WebGIS seit der Prüfung geändert (Zustand) — nicht geschrieben, bitte neu prüfen.
[ÄNDERN] Haltung H-verworfen  (GlobalID G-H-verworfen)
    Zustand: Mittlere Maengel (Z2) → Keine Maengel (Z4)
    !! Zustand: vom WebGIS nicht übernommen — das Feld steht danach unverändert da.
[ÄNDERN] Haltung H-bestaetigt  (GlobalID G-H-bestaetigt)
    Zustand: Mittlere Maengel (Z2) → Keine Maengel (Z4)
    (Geschrieben, aber nicht nachgeprüft — beim erneuten Lesen kein eindeutiger Treffer.)
[ÄNDERN] Haltung H-nachgeprueft  (GlobalID G-H-nachgeprueft)
    Zustand: Mittlere Maengel (Z2) → Keine Maengel (Z4)
[ÄNDERN] Haltung H-ohneServer  (GlobalID G-H-ohneServer)
    Zustand: Mittlere Maengel (Z2) → Keine Maengel (Z4)
[UNVERÄNDERT] Schacht S-offen  (GlobalID G-S-offen)
[UNVERÄNDERT] Schacht S-vorhanden  (GlobalID G-S-vorhanden)
[UNVERÄNDERT] Schacht S-gesperrt  (GlobalID G-S-gesperrt)
[UNVERÄNDERT] Schacht S-fehler  (GlobalID G-S-fehler)
[UNVERÄNDERT] Schacht S-bestaetigt  (GlobalID G-S-bestaetigt)
[UNVERÄNDERT] Schacht S-nachgeprueft  (GlobalID G-S-nachgeprueft)
[UNVERÄNDERT] Schacht S-ungeklaert  (GlobalID G-S-ungeklaert)

SANIERUNGSMASSNAHMEN
[ANLEGEN] Schacht S-offen  (Akte 00000000)
    Art: Reparatur (R)
    Status: Ausgeführt
[GESPERRT] Schacht S-vorhanden  (Akte 00000000)
    Art: Reparatur (R)
    Status: Ausgeführt
    !! Im WebGIS bereits vorhanden (Reparatur / Ausgeführt) — nicht doppelt angelegt.
[GESPERRT] Schacht S-gesperrt  (Akte 00000000)
    Art: Reparatur (R)
    Status: Ausgeführt
    !! Art fehlt in der Akte — nicht angelegt.
[ANLEGEN] Schacht S-fehler  (Akte 00000000)
    Art: Reparatur (R)
    Status: Ausgeführt
    !! Schreibfehler: Serverfehler 500
[ANLEGEN] Schacht S-bestaetigt  (Akte 00000000)
    Art: Reparatur (R)
    Status: Ausgeführt
[ANLEGEN] Schacht S-nachgeprueft  (Akte 00000000)
    Art: Reparatur (R)
    Status: Ausgeführt
[ANLEGEN] Schacht S-ungeklaert  (Akte 00000000)
    Art: Reparatur (R)
    Status: Ausgeführt
    !! Vom Server bestätigt, aber nicht nachgeprüft — keine neue Zeile. Vor einem neuen Versuch im WebGIS nachsehen.
[ANLEGEN] Schacht S-ohneGlobalId  (Akte 00000000)

## Details Ergebnis
WEBGIS-ÜBERTRAGUNG — ERGEBNIS
Planhinweis.

[OFFEN] Haltung H-offen  (GlobalID G-H-offen)
    Zustand: Mittlere Maengel (Z2) → Keine Maengel (Z4)
[UNVERÄNDERT] Haltung H-unveraendert  (GlobalID G-H-unveraendert)
    (Nur ein Hinweis.)
[GESPERRT] Haltung H-gesperrt
    !! Im WebGIS nicht eindeutig gefunden.
[FEHLER] Haltung H-fehler  (GlobalID G-H-fehler)
    Zustand: Mittlere Maengel (Z2) → Keine Maengel (Z4)
    !! Objekt wurde im WebGIS seit der Prüfung geändert (Zustand) — nicht geschrieben, bitte neu prüfen.
[FEHLER] Haltung H-verworfen  (GlobalID G-H-verworfen)
    Zustand: Mittlere Maengel (Z2) → Keine Maengel (Z4)
    !! Zustand: vom WebGIS nicht übernommen — das Feld steht danach unverändert da.
[GESCHRIEBEN] Haltung H-bestaetigt  (GlobalID G-H-bestaetigt)
    Zustand: Mittlere Maengel (Z2) → Keine Maengel (Z4)
    (Geschrieben, aber nicht nachgeprüft — beim erneuten Lesen kein eindeutiger Treffer.)
[GESCHRIEBEN] Haltung H-nachgeprueft  (GlobalID G-H-nachgeprueft)
    Zustand: Mittlere Maengel (Z2) → Keine Maengel (Z4)
[GESCHRIEBEN] Haltung H-ohneServer  (GlobalID G-H-ohneServer)
    Zustand: Mittlere Maengel (Z2) → Keine Maengel (Z4)
[UNVERÄNDERT] Schacht S-offen  (GlobalID G-S-offen)
[UNVERÄNDERT] Schacht S-vorhanden  (GlobalID G-S-vorhanden)
[UNVERÄNDERT] Schacht S-gesperrt  (GlobalID G-S-gesperrt)
[UNVERÄNDERT] Schacht S-fehler  (GlobalID G-S-fehler)
[UNVERÄNDERT] Schacht S-bestaetigt  (GlobalID G-S-bestaetigt)
[UNVERÄNDERT] Schacht S-nachgeprueft  (GlobalID G-S-nachgeprueft)
[UNVERÄNDERT] Schacht S-ungeklaert  (GlobalID G-S-ungeklaert)

SANIERUNGSMASSNAHMEN
[OFFEN] Schacht S-offen  (Akte 00000000)
    Art: Reparatur (R)
    Status: Ausgeführt
[GESPERRT] Schacht S-vorhanden  (Akte 00000000)
    Art: Reparatur (R)
    Status: Ausgeführt
    !! Im WebGIS bereits vorhanden (Reparatur / Ausgeführt) — nicht doppelt angelegt.
[GESPERRT] Schacht S-gesperrt  (Akte 00000000)
    Art: Reparatur (R)
    Status: Ausgeführt
    !! Art fehlt in der Akte — nicht angelegt.
[FEHLER] Schacht S-fehler  (Akte 00000000)
    Art: Reparatur (R)
    Status: Ausgeführt
    !! Schreibfehler: Serverfehler 500
[ANGELEGT, VOM SERVER BESTÄTIGT (ID 66921, nicht nachgeprüft)] Schacht S-bestaetigt  (Akte 00000000)
    Art: Reparatur (R)
    Status: Ausgeführt
[ANGELEGT UND NACHGEPRÜFT (ID 66922, GlobalID {NEU-1})] Schacht S-nachgeprueft  (Akte 00000000)
    Art: Reparatur (R)
    Status: Ausgeführt
[UNGEKLÄRT (vom Server bestätigt, ID 66923)] Schacht S-ungeklaert  (Akte 00000000)
    Art: Reparatur (R)
    Status: Ausgeführt
    !! Vom Server bestätigt, aber nicht nachgeprüft — keine neue Zeile. Vor einem neuen Versuch im WebGIS nachsehen.
[OFFEN] Schacht S-ohneGlobalId  (Akte 00000000)

## Ergebnis
WebGIS: 3 Objekte geschrieben, 1 Sanierungsmassnahmen angelegt und nachgeprüft, 1 vom Server bestätigt (nicht nachgeprüft), 1 mit ungeklärtem Ausgang (im WebGIS nachsehen, nicht erneut anlegen), 3 fehlgeschlagen (siehe Bericht).
## Log
===== 30.09.2026 14:05:06 | WebGIS-Übertragung | tester | WebGIS: 3 Objekte geschrieben, 1 Sanierungsmassnahmen angelegt und nachgeprüft, 1 vom Server bestätigt (nicht nachgeprüft), 1 mit ungeklärtem Ausgang (im WebGIS nachsehen, nicht erneut anlegen), 3 fehlgeschlagen (siehe Bericht).
30.09.2026 14:05:06 | Haltung H-gesperrt | – | übersprungen | GESPERRT: Im WebGIS nicht eindeutig gefunden.
30.09.2026 14:05:06 | Haltung H-fehler | Zustand | nicht geschrieben | FEHLER: Objekt wurde im WebGIS seit der Prüfung geändert (Zustand) — nicht geschrieben, bitte neu prüfen.
30.09.2026 14:05:06 | Haltung H-verworfen | Zustand | nicht geschrieben | FEHLER: Zustand: vom WebGIS nicht übernommen — das Feld steht danach unverändert da.
30.09.2026 14:05:06 | Haltung H-bestaetigt | Zustand | 102 (Mittlere Maengel (Z2)) → 104 (Keine Maengel (Z4)) | OK
30.09.2026 14:05:06 | Haltung H-nachgeprueft | Zustand | 102 (Mittlere Maengel (Z2)) → 104 (Keine Maengel (Z4)) | OK
30.09.2026 14:05:06 | Haltung H-ohneServer | Zustand | 102 (Mittlere Maengel (Z2)) → 104 (Keine Maengel (Z4)) | OK
30.09.2026 14:05:06 | Schacht S-vorhanden | Sanierungsmassnahme | übersprungen | GESPERRT: Im WebGIS bereits vorhanden (Reparatur / Ausgeführt) — nicht doppelt angelegt.
30.09.2026 14:05:06 | Schacht S-gesperrt | Sanierungsmassnahme | übersprungen | GESPERRT: Art fehlt in der Akte — nicht angelegt.
30.09.2026 14:05:06 | Schacht S-fehler | Sanierungsmassnahme | nicht angelegt (Art: Reparatur, Status: Ausgeführt) | FEHLER: Schreibfehler: Serverfehler 500
30.09.2026 14:05:06 | Schacht S-bestaetigt | Sanierungsmassnahme angelegt (ID 66921) | – → Art: Reparatur, Status: Ausgeführt | BESTÄTIGT, nicht nachgeprüft
30.09.2026 14:05:06 | Schacht S-nachgeprueft | Sanierungsmassnahme angelegt (ID 66922) | – → Art: Reparatur, Status: Ausgeführt | OK, nachgeprüft (GlobalID {NEU-1})
30.09.2026 14:05:06 | Schacht S-ungeklaert | Sanierungsmassnahme vom Server bestätigt (ID 66923) | – → Art: Reparatur, Status: Ausgeführt | UNGEKLÄRT: Vom Server bestätigt, aber nicht nachgeprüft — keine neue Zeile. Vor einem neuen Versuch im WebGIS nachsehen.

## LogStart/Abschluss/Abbruch
===== 30.09.2026 14:05:06 | WebGIS-Übertragung | tester | gestartet: 6 Objekte, 5 Sanierungsmassnahmen
===== 30.09.2026 14:05:06 | abgeschlossen | WebGIS: 3 Objekte geschrieben, 1 Sanierungsmassnahmen angelegt und nachgeprüft, 1 vom Server bestätigt (nicht nachgeprüft), 1 mit ungeklärtem Ausgang (im WebGIS nachsehen, nicht erneut anlegen), 3 fehlgeschlagen (siehe Bericht).
===== 30.09.2026 14:05:06 | ABGEBROCHEN | Grund | bis dahin: WebGIS: 3 Objekte geschrieben, 1 Sanierungsmassnahmen angelegt und nachgeprüft, 1 vom Server bestätigt (nicht nachgeprüft), 1 mit ungeklärtem Ausgang (im WebGIS nachsehen, nicht erneut anlegen), 3 fehlgeschlagen (siehe Bericht).
## LogGeplant
30.09.2026 14:05:06 | Haltung H-offen | Zustand | wird gesendet | GEPLANT
30.09.2026 14:05:06 | Haltung H-unveraendert |  | wird gesendet | GEPLANT
30.09.2026 14:05:06 | Haltung H-gesperrt |  | wird gesendet | GEPLANT
30.09.2026 14:05:06 | Haltung H-fehler | Zustand | wird gesendet | GEPLANT
30.09.2026 14:05:06 | Haltung H-verworfen | Zustand | wird gesendet | GEPLANT
30.09.2026 14:05:06 | Haltung H-bestaetigt | Zustand | wird gesendet | GEPLANT
30.09.2026 14:05:06 | Haltung H-nachgeprueft | Zustand | wird gesendet | GEPLANT
30.09.2026 14:05:06 | Haltung H-ohneServer | Zustand | wird gesendet | GEPLANT
30.09.2026 14:05:06 | Schacht S-offen |  | wird gesendet | GEPLANT
30.09.2026 14:05:06 | Schacht S-vorhanden |  | wird gesendet | GEPLANT
30.09.2026 14:05:06 | Schacht S-gesperrt |  | wird gesendet | GEPLANT
30.09.2026 14:05:06 | Schacht S-fehler |  | wird gesendet | GEPLANT
30.09.2026 14:05:06 | Schacht S-bestaetigt |  | wird gesendet | GEPLANT
30.09.2026 14:05:06 | Schacht S-nachgeprueft |  | wird gesendet | GEPLANT
30.09.2026 14:05:06 | Schacht S-ungeklaert |  | wird gesendet | GEPLANT
30.09.2026 14:05:06 | Schacht S-offen | Sanierungsmassnahme | wird angelegt (Art: Reparatur, Status: Ausgeführt) | GEPLANT
30.09.2026 14:05:06 | Schacht S-vorhanden | Sanierungsmassnahme | wird angelegt (Art: Reparatur, Status: Ausgeführt) | GEPLANT
30.09.2026 14:05:06 | Schacht S-gesperrt | Sanierungsmassnahme | wird angelegt (Art: Reparatur, Status: Ausgeführt) | GEPLANT
30.09.2026 14:05:06 | Schacht S-fehler | Sanierungsmassnahme | wird angelegt (Art: Reparatur, Status: Ausgeführt) | GEPLANT
30.09.2026 14:05:06 | Schacht S-bestaetigt | Sanierungsmassnahme | wird angelegt (Art: Reparatur, Status: Ausgeführt) | GEPLANT
30.09.2026 14:05:06 | Schacht S-nachgeprueft | Sanierungsmassnahme | wird angelegt (Art: Reparatur, Status: Ausgeführt) | GEPLANT
30.09.2026 14:05:06 | Schacht S-ungeklaert | Sanierungsmassnahme | wird angelegt (Art: Reparatur, Status: Ausgeführt) | GEPLANT
30.09.2026 14:05:06 | Schacht S-ohneGlobalId | Sanierungsmassnahme | wird angelegt () | GEPLANT
## Vorschau
WebGIS-Übertragung prüfen | 6 Objekte mit Änderungen, 1 gesperrt · 5 Sanierungsmassnahmen anzulegen, 2 gesperrt. Längen werden nie geschrieben. Gesperrte Objekte werden übersprungen. | IstFehler=False
Z Haltung H-offen | Zustand | Mittlere Maengel (Z2) | Keine Maengel (Z4)
Z Haltung H-fehler | Zustand | Mittlere Maengel (Z2) | Keine Maengel (Z4)
Z Haltung H-verworfen | Zustand | Mittlere Maengel (Z2) | Keine Maengel (Z4)
Z Haltung H-bestaetigt | Zustand | Mittlere Maengel (Z2) | Keine Maengel (Z4)
Z Haltung H-nachgeprueft | Zustand | Mittlere Maengel (Z2) | Keine Maengel (Z4)
Z Haltung H-ohneServer | Zustand | Mittlere Maengel (Z2) | Keine Maengel (Z4)
Z Schacht S-offen | Sanierungsmassnahme (neu) | – | Art: Reparatur (R) · Status: Ausgeführt
Z Schacht S-fehler | Sanierungsmassnahme (neu) | – | Art: Reparatur (R) · Status: Ausgeführt
Z Schacht S-bestaetigt | Sanierungsmassnahme (neu) | – | Art: Reparatur (R) · Status: Ausgeführt
Z Schacht S-nachgeprueft | Sanierungsmassnahme (neu) | – | Art: Reparatur (R) · Status: Ausgeführt
Z Schacht S-ungeklaert | Sanierungsmassnahme (neu) | – | Art: Reparatur (R) · Status: Ausgeführt
W Haltung H-unveraendert: Nur ein Hinweis.
W Haltung H-gesperrt: GESPERRT — Im WebGIS nicht eindeutig gefunden.
W Haltung H-fehler: FEHLER — Objekt wurde im WebGIS seit der Prüfung geändert (Zustand) — nicht geschrieben, bitte neu prüfen.
W Haltung H-verworfen: FEHLER — Zustand: vom WebGIS nicht übernommen — das Feld steht danach unverändert da.
W Haltung H-bestaetigt: Geschrieben, aber nicht nachgeprüft — beim erneuten Lesen kein eindeutiger Treffer.
W Schacht S-vorhanden, Sanierungsmassnahme: GESPERRT — Im WebGIS bereits vorhanden (Reparatur / Ausgeführt) — nicht doppelt angelegt.
W Schacht S-gesperrt, Sanierungsmassnahme: GESPERRT — Art fehlt in der Akte — nicht angelegt.
W Schacht S-fehler, Sanierungsmassnahme: FEHLER — Schreibfehler: Serverfehler 500
W Schacht S-ungeklaert, Sanierungsmassnahme: UNGEKLÄRT — Vom Server bestätigt, aber nicht nachgeprüft — keine neue Zeile. Vor einem neuen Versuch im WebGIS nachsehen.
## Uebersicht ergebnis=False
WebGIS-Übertragung prüfen | 11 Objekte werden geändert · 5 Massnahmen werden angelegt · 6 gesperrt · 1 nur mit Hinweis | mitAenderung=11 geschrieben=3 neu=5 gesperrt=6 hinweis=1 nichts=False vorschlaege=0
O Haltung H-bestaetigt | Haltung | 00000000-0000-0000-0000-000000000006 | 1 Änderung | sperre=False
  Aenderung | Zustand | Mittlere Maengel (Z2) | Keine Maengel (Z4)
  Hinweis | Hinweis |  | Geschrieben, aber nicht nachgeprüft — beim erneuten Lesen kein eindeutiger Treffer.
O Haltung H-fehler | Haltung | 00000000-0000-0000-0000-000000000004 | gesperrt | sperre=True
  Aenderung | Zustand | Mittlere Maengel (Z2) | Keine Maengel (Z4)
  Fehler | Fehler |  | Objekt wurde im WebGIS seit der Prüfung geändert (Zustand) — nicht geschrieben, bitte neu prüfen.
O Haltung H-nachgeprueft | Haltung | 00000000-0000-0000-0000-000000000007 | 1 Änderung | sperre=False
  Aenderung | Zustand | Mittlere Maengel (Z2) | Keine Maengel (Z4)
O Haltung H-offen | Haltung | 00000000-0000-0000-0000-000000000001 | 1 Änderung | sperre=False
  Aenderung | Zustand | Mittlere Maengel (Z2) | Keine Maengel (Z4)
O Haltung H-ohneServer | Haltung | 00000000-0000-0000-0000-000000000008 | 1 Änderung | sperre=False
  Aenderung | Zustand | Mittlere Maengel (Z2) | Keine Maengel (Z4)
O Haltung H-verworfen | Haltung | 00000000-0000-0000-0000-000000000005 | gesperrt | sperre=True
  Aenderung | Zustand | Mittlere Maengel (Z2) | Keine Maengel (Z4)
  Fehler | Fehler |  | Zustand: vom WebGIS nicht übernommen — das Feld steht danach unverändert da.
O Schacht S-bestaetigt | Schacht | 00000000-0000-0000-0000-000000000015 | 1 Änderung | sperre=False
  NeueMassnahme | Sanierungsmassnahme |  | Art: Reparatur (R) · Status: Ausgeführt
O Schacht S-fehler | Schacht | 00000000-0000-0000-0000-000000000014 | gesperrt | sperre=True
  NeueMassnahme | Sanierungsmassnahme |  | Art: Reparatur (R) · Status: Ausgeführt
  Fehler | Sanierungsmassnahme |  | Schreibfehler: Serverfehler 500
O Schacht S-nachgeprueft | Schacht | 00000000-0000-0000-0000-000000000016 | 1 Änderung | sperre=False
  NeueMassnahme | Sanierungsmassnahme |  | Art: Reparatur (R) · Status: Ausgeführt
O Schacht S-offen | Schacht | 00000000-0000-0000-0000-000000000011 | 1 Änderung | sperre=False
  NeueMassnahme | Sanierungsmassnahme |  | Art: Reparatur (R) · Status: Ausgeführt
O Schacht S-ungeklaert | Schacht | 00000000-0000-0000-0000-000000000017 | gesperrt | sperre=True
  NeueMassnahme | Sanierungsmassnahme |  | Art: Reparatur (R) · Status: Ausgeführt
  Fehler | Sanierungsmassnahme (Ausgang ungeklärt) |  | Vom Server bestätigt, aber nicht nachgeprüft — keine neue Zeile. Vor einem neuen Versuch im WebGIS nachsehen.
O Haltung H-gesperrt | Haltung | 00000000-0000-0000-0000-000000000003 | gesperrt | sperre=True
  Sperre | Gesperrt |  | Im WebGIS nicht eindeutig gefunden.
O Schacht S-gesperrt | Schacht | 00000000-0000-0000-0000-000000000013 | gesperrt | sperre=True
  Sperre | Sanierungsmassnahme |  | Art fehlt in der Akte — nicht angelegt.
O Haltung H-unveraendert | Haltung | 00000000-0000-0000-0000-000000000002 | nur Hinweis | sperre=False
  Hinweis | Hinweis |  | Nur ein Hinweis.
S 1 Sanierungsmassnahmen sind im WebGIS bereits vorhanden — nicht doppelt angelegt.
## Uebersicht ergebnis=True
WebGIS-Übertragung — Ergebnis | 3 Objekte geschrieben · 3 Massnahmen angelegt · 6 nicht geschrieben | mitAenderung=11 geschrieben=3 neu=3 gesperrt=6 hinweis=1 nichts=False vorschlaege=0
O Haltung H-bestaetigt | Haltung | 00000000-0000-0000-0000-000000000006 | 1 Änderung | sperre=False
  Erledigt | Zustand | Mittlere Maengel (Z2) | Keine Maengel (Z4)
  Hinweis | Hinweis |  | Geschrieben, aber nicht nachgeprüft — beim erneuten Lesen kein eindeutiger Treffer.
O Haltung H-fehler | Haltung | 00000000-0000-0000-0000-000000000004 | gesperrt | sperre=True
  Aenderung | Zustand | Mittlere Maengel (Z2) | Keine Maengel (Z4)
  Fehler | Fehler |  | Objekt wurde im WebGIS seit der Prüfung geändert (Zustand) — nicht geschrieben, bitte neu prüfen.
O Haltung H-nachgeprueft | Haltung | 00000000-0000-0000-0000-000000000007 | 1 Änderung | sperre=False
  Erledigt | Zustand | Mittlere Maengel (Z2) | Keine Maengel (Z4)
O Haltung H-offen | Haltung | 00000000-0000-0000-0000-000000000001 | 1 Änderung | sperre=False
  Aenderung | Zustand | Mittlere Maengel (Z2) | Keine Maengel (Z4)
O Haltung H-ohneServer | Haltung | 00000000-0000-0000-0000-000000000008 | 1 Änderung | sperre=False
  Erledigt | Zustand | Mittlere Maengel (Z2) | Keine Maengel (Z4)
O Haltung H-verworfen | Haltung | 00000000-0000-0000-0000-000000000005 | gesperrt | sperre=True
  Aenderung | Zustand | Mittlere Maengel (Z2) | Keine Maengel (Z4)
  Fehler | Fehler |  | Zustand: vom WebGIS nicht übernommen — das Feld steht danach unverändert da.
O Schacht S-bestaetigt | Schacht | 00000000-0000-0000-0000-000000000015 | 1 Änderung | sperre=False
  Erledigt | Sanierungsmassnahme |  | Art: Reparatur (R) · Status: Ausgeführt
O Schacht S-fehler | Schacht | 00000000-0000-0000-0000-000000000014 | gesperrt | sperre=True
  NeueMassnahme | Sanierungsmassnahme |  | Art: Reparatur (R) · Status: Ausgeführt
  Fehler | Sanierungsmassnahme |  | Schreibfehler: Serverfehler 500
O Schacht S-nachgeprueft | Schacht | 00000000-0000-0000-0000-000000000016 | 1 Änderung | sperre=False
  Erledigt | Sanierungsmassnahme |  | Art: Reparatur (R) · Status: Ausgeführt
O Schacht S-offen | Schacht | 00000000-0000-0000-0000-000000000011 | 1 Änderung | sperre=False
  NeueMassnahme | Sanierungsmassnahme |  | Art: Reparatur (R) · Status: Ausgeführt
O Schacht S-ungeklaert | Schacht | 00000000-0000-0000-0000-000000000017 | gesperrt | sperre=True
  Erledigt | Sanierungsmassnahme |  | Art: Reparatur (R) · Status: Ausgeführt
  Fehler | Sanierungsmassnahme (Ausgang ungeklärt) |  | Vom Server bestätigt, aber nicht nachgeprüft — keine neue Zeile. Vor einem neuen Versuch im WebGIS nachsehen.
O Haltung H-gesperrt | Haltung | 00000000-0000-0000-0000-000000000003 | gesperrt | sperre=True
  Sperre | Gesperrt |  | Im WebGIS nicht eindeutig gefunden.
O Schacht S-gesperrt | Schacht | 00000000-0000-0000-0000-000000000013 | gesperrt | sperre=True
  Sperre | Sanierungsmassnahme |  | Art fehlt in der Akte — nicht angelegt.
O Haltung H-unveraendert | Haltung | 00000000-0000-0000-0000-000000000002 | nur Hinweis | sperre=False
  Hinweis | Hinweis |  | Nur ein Hinweis.
S 1 Sanierungsmassnahmen sind im WebGIS bereits vorhanden — nicht doppelt angelegt.
## Vergleichsanzeige
O Haltung H-offen | 1 Änderung | Aenderung | 1 Änderung geplant. | geschrieben=False schreibbar=True gesperrt=False n=1
  Zustand | 4 | Z2 | wird geändert | neu: Z4 | Aenderung
O Haltung H-unveraendert | keine Änderung | Gleich | Nichts zu schreiben. | geschrieben=False schreibbar=False gesperrt=False n=0
O Haltung H-gesperrt | gesperrt | Gesperrt | Im WebGIS nicht eindeutig gefunden. | geschrieben=False schreibbar=False gesperrt=True n=0
O Haltung H-fehler | Fehler | Fehler | Objekt wurde im WebGIS seit der Prüfung geändert (Zustand) — nicht geschrieben, bitte neu prüfen. | geschrieben=False schreibbar=True gesperrt=False n=1
  Zustand | 4 | Z2 | nicht geschrieben | Objekt wurde im WebGIS seit der Prüfung geändert (Zustand) — nicht geschrieben, bitte neu prüfen. | Fehler
O Haltung H-verworfen | Fehler | Fehler | Zustand: vom WebGIS nicht übernommen — das Feld steht danach unverändert da. | geschrieben=False schreibbar=True gesperrt=False n=1
  Zustand | 4 | Z2 | nicht geschrieben | Zustand: vom WebGIS nicht übernommen — das Feld steht danach unverändert da. | Fehler
O Haltung H-bestaetigt | geschrieben | Bestaetigt | Geschrieben – vom WebGIS bestätigt. | geschrieben=True schreibbar=False gesperrt=False n=1
  Zustand | 4 | Z4 | im WebGIS bestätigt | nach dem Schreiben zurückgelesen | Bestaetigt
O Haltung H-nachgeprueft | geschrieben | Bestaetigt | Geschrieben – vom WebGIS bestätigt. | geschrieben=True schreibbar=False gesperrt=False n=1
  Zustand | 4 | Z4 | im WebGIS bestätigt | nach dem Schreiben zurückgelesen | Bestaetigt
O Haltung H-ohneServer | geschrieben | Bestaetigt | Geschrieben – die Kontrolle durch Zurücklesen steht noch aus. | geschrieben=True schreibbar=False gesperrt=False n=1
  Zustand | 4 | Z4 | geschrieben | Kontrolle durch Zurücklesen offen | Warnung
O Schacht S-offen | 1 Änderung | Aenderung | 1 Änderung geplant. | geschrieben=False schreibbar=True gesperrt=False n=1
  Sanierungsmassnahme | Reparatur | — | wird angelegt |  | Aenderung
O Schacht S-vorhanden | keine Änderung | Gleich | Nichts zu schreiben. | geschrieben=False schreibbar=False gesperrt=False n=0
  Sanierungsmassnahme | Reparatur | — | wird angelegt |  | Aenderung
O Schacht S-gesperrt | keine Änderung | Gleich | Nichts zu schreiben. | geschrieben=False schreibbar=False gesperrt=False n=0
  Sanierungsmassnahme | Reparatur | — | wird angelegt |  | Aenderung
O Schacht S-fehler | Fehler | Fehler | Schreibfehler: Serverfehler 500 | geschrieben=False schreibbar=True gesperrt=False n=1
  Sanierungsmassnahme | Reparatur | — | nicht angelegt | Schreibfehler: Serverfehler 500 | Fehler
O Schacht S-bestaetigt | geschrieben | Bestaetigt | Geschrieben – vom WebGIS bestätigt. | geschrieben=True schreibbar=False gesperrt=False n=1
  Sanierungsmassnahme | Reparatur | Reparatur | angelegt | vom Server bestätigt (ID 66921), nicht nachgeprüft | Warnung
O Schacht S-nachgeprueft | geschrieben | Bestaetigt | Geschrieben – vom WebGIS bestätigt. | geschrieben=True schreibbar=False gesperrt=False n=1
  Sanierungsmassnahme | Reparatur | Reparatur | angelegt und nachgeprüft | zurückgelesen (ID 66922) | Bestaetigt
O Schacht S-ungeklaert | ungeklärt | Warnung | Vom Server bestätigt, aber nicht nachgeprüft — keine neue Zeile. Vor einem neuen Versuch im WebGIS nachsehen. | geschrieben=True schreibbar=False gesperrt=False n=1
  Sanierungsmassnahme | Reparatur | Reparatur | Ausgang ungeklärt | Vom Server bestätigt, aber nicht nachgeprüft — keine neue Zeile. Vor einem neuen Versuch im WebGIS nachsehen. | Warnung
## Schreibliste nachDemSchreiben=False
Haltung H-offen | Zustand | Z2 | Z4 | geplant | Aenderung
Haltung H-fehler | Zustand | Z2 | Z4 | geplant | Aenderung
Haltung H-verworfen | Zustand | Z2 | Z4 | geplant | Aenderung
Haltung H-bestaetigt | Zustand | Z2 | Z4 | geplant | Aenderung
Haltung H-nachgeprueft | Zustand | Z2 | Z4 | geplant | Aenderung
Haltung H-ohneServer | Zustand | Z2 | Z4 | geplant | Aenderung
Schacht S-offen | Sanierungsmassnahme | — | Reparatur | geplant | Aenderung
Schacht S-fehler | Sanierungsmassnahme | — | Reparatur | geplant | Aenderung
Schacht S-bestaetigt | Sanierungsmassnahme | — | Reparatur | geplant | Aenderung
Schacht S-nachgeprueft | Sanierungsmassnahme | — | Reparatur | geplant | Aenderung
Schacht S-ungeklaert | Sanierungsmassnahme | — | Reparatur | geplant | Aenderung
## Schreibliste nachDemSchreiben=True
Haltung H-offen | Zustand | Z2 | Z4 | nicht versucht | Warnung
Haltung H-fehler | Zustand | Z2 | Z4 | nicht geschrieben: Objekt wurde im WebGIS seit der Prüfung geändert (Zustand) — nicht geschrieben, bitte neu prüfen. | Fehler
Haltung H-verworfen | Zustand | Z2 | Z4 | nicht geschrieben: Zustand: vom WebGIS nicht übernommen — das Feld steht danach unverändert da. | Fehler
Haltung H-bestaetigt | Zustand | Z2 | Z4 | bestätigt | Bestaetigt
Haltung H-nachgeprueft | Zustand | Z2 | Z4 | bestätigt | Bestaetigt
Haltung H-ohneServer | Zustand | Z2 | Z4 | geschrieben, Kontrolle offen | Warnung
Schacht S-offen | Sanierungsmassnahme | — | Reparatur | nicht versucht | Warnung
Schacht S-fehler | Sanierungsmassnahme | — | Reparatur | nicht angelegt: Schreibfehler: Serverfehler 500 | Fehler
Schacht S-bestaetigt | Sanierungsmassnahme | — | Reparatur | angelegt, vom Server bestätigt (ID 66921), nicht nachgeprüft | Warnung
Schacht S-nachgeprueft | Sanierungsmassnahme | — | Reparatur | angelegt und nachgeprüft (ID 66922) | Bestaetigt
Schacht S-ungeklaert | Sanierungsmassnahme | — | Reparatur | Ausgang ungeklärt: Vom Server bestätigt, aber nicht nachgeprüft — keine neue Zeile. Vor einem neuen Versuch im WebGIS nachsehen. | Warnung

""";
}
