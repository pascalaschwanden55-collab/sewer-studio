# Datenaustausch SewerStudio ↔ GEONIS (Trigonet) — Stand 4. September 2026

Vollständige Beschreibung der Ausgangslage, der Befunde und der Umsetzung vom
4. September 2026. Geschrieben für Pascal Aschwanden (Abwasser Uri) als Grundlage
für das Gespräch mit Andreas Sidler (Trigonet AG) und Kilian (GEONIS-Betreuung).

---

## 1. Worum es geht

Ziel ist ein Kreislauf ohne Doppelarbeit:

1. Aus GEONIS kommen die Haltungen und Schächte einer Kanal-TV-Kampagne als XTF-Datei
   (SIA 405, Version 2020_1).
2. Der Operateur importiert die Datei in sein Aufnahmesystem (WinCan), nimmt auf und
   korrigiert die Angaben vor Ort.
3. SewerStudio importiert die Aufnahme. Pascal prüft und beurteilt jede Haltung und
   korrigiert Angaben, die falsch oder unvollständig sind.
4. SewerStudio erzeugt eine neue XTF-Datei mit dem geprüften Stand.
5. Trigonet importiert diese Datei mit einer FME-Workbench in die GEONIS-Datenbank.
   Bestehende Objekte werden aktualisiert.

Damit müssen die Attribute nicht mehr von Hand parallel im WebGIS nachgetragen werden.

## 2. Die Mail von Andreas Sidler (Trigonet)

Andreas hat die Testdatei geprüft und einen Prozess vorgeschlagen:

- Import über eine FME-Workbench, gestartet per Drag & Drop im Browser, direkt in die
  Datenbank, ohne weiteren Dialog. Optional ein Änderungsprotokoll.
- Annahmen: Die Datei ist bereits geprüft, GEONIS validiert nicht mehr. Kein Rückgängig.
  Beschriftungen müssen nach dem Import mit einer GEONIS-Funktion aktualisiert werden.
  Die zu übernehmenden Attribute werden einmal festgelegt.
- Aufwand: rund ein Arbeitstag inklusive Tests und Einführung.

Zwei fachliche Punkte zur Datei:

- **Haltung:** GEONIS führt neben der lichten Höhe auch die Breite. Deshalb soll beim
  Rohrprofil das Attribut `HoehenBreitenverhaeltnis` mitgegeben werden, beim Kreisprofil
  der Wert 1.
- **Primärschlüssel:** Die Kennungen (TID) der Datei passen nicht zur Datenbank.
  Andreas fragte, ob der Abgleich über die Bezeichnung laufen soll, und warnte vor über
  1000 Duplikaten bei den Bezeichnungen.
- **Schacht:** sollte so klappen.

## 3. Warum die Testdatei nicht passte

Die gesendete Datei `Seilergasse Test export_20260903_174518.xtf` war ein
**Neu-Export** („Neue eigenständige XTF"), nicht die Aktualisierung einer
Originaldatei. Das steht im Dateikopf: „Vollständiger Neu-Export aus SewerStudio".
Alle Kennungen beginnen mit `chSST`. Das sind von SewerStudio selbst abgeleitete
Kennungen, sie existieren in keiner anderen Datenbank. In der Mail an Andreas stand
„Objektkennungen beibehalten", das traf für diese Datei nicht zu.

Für das Projekt Seilergasse gibt es zudem keine importierte Original-XTF. Der Weg
„Bestehende Katasterdaten aktualisieren" hätte die Kennungen der Originaldatei behalten,
war aber ohne Original nicht möglich.

## 4. Die Kennungen: Wer nennt eine Haltung wie?

Der Kern des Problems ist die Identität. Dieselbe Haltung 78998-79002 trägt je nach
System eine andere Nummer:

| System | Kennung der Haltung 78998-79002 | Beständig? |
|---|---|---|
| GEONIS-Datenbank, Feld `SIA405_ID` | `ch23h1a4uL3A2Sjp` | ja, die Kennung für den XTF-Austausch |
| GEONIS-Datenbank, `OBJECTID` | 75394 | interne Nummer der aktuellen Datenbank, nur in der WebGIS-Maske sichtbar |
| GEONIS-Datenbank, `GlobalId` | {04C6464F-…} | ja, intern, geht nie in eine Datei |
| Lisag-Dienst geo.ur.ch (WFS), Feld `obj_id` | 866789 (Kopie Nov. 2025), heute 867034 | nein, wird bei jedem Upload neu vergeben |
| WinCan-Export | `ch010wcsKA000001` | nein, Zähler je Export |
| SewerStudio Neu-Export (bisher) | `chSST039E27FAE55` | ja, aber nur innerhalb SewerStudio |

Belege aus der GEONIS-Kopie vom Dezember 2024
(`D:\Fachwissen\ArcGis\Stand_Dezember_2024_uri_abwasser.gdb\uri_abwasser.gdb`):

| Tabelle | Datensätze | davon mit `SIA405_ID` |
|---|---|---|
| Haltungen (`AWK_HALTUNG`) | 102'317 | 102'291 |
| Schächte/Knoten (`AWK_ABWASSERKNOTEN`) | 123'096 | 123'033 |
| Kanäle (`AWK_KANAL`) | 101'851 | 101'823 |

Der GEONIS-eigene FME-Export (`SIA405_Abwasser_2015_export.fmw`, Transformer
`GN_ILI_SIA405_OBJ_ID`) schreibt genau diese `SIA405_ID` als TID in die XTF und legt für
neue Objekte eine an. Der vorhandene Import gleicht am Ende über `GLOBALID` ab; die
Auflösung TID → `SIA405_ID` → `GLOBALID` muss Trigonet deshalb bestätigen und testen.
Damit ist die Antwort auf Andreas' Frage: **Abgleich über die volle 16-stellige
SIA405_ID, nicht über die Bezeichnung.**

Eine Haltung ist in SIA 405 ein Verbund aus mehreren Objekten. Für die Seilergasse
finden sich in der GEONIS-Kopie alle Kennungen:

| XTF-Objekt | GEONIS-Kennung |
|---|---|
| Kanal | `ch23h1a46oVbkGmT` |
| Haltung 78998-79002 | `ch23h1a4uL3A2Sjp` |
| Haltungspunkt von (GEONIS-Name `A75394`) | `ch23h1a4CNjzeqBU` |
| Haltungspunkt nach (GEONIS-Name `E75394`) | `ch23h1a44Op5RVY5` |
| Rohrprofil (Typ „unbekannt") | `ch23h1a43obhLa8B` |
| Schacht 78998 als Bauwerk (Normschacht) | `ch23h1a4Umcgr2UF` |
| Schacht 78998 als Abwasserknoten | `ch23h1a4ftlGdbHU` |
| Eigentümer „Privat" (Organisation) | `ch20p3q400000094` |

## 5. Abgleich über den Namen: Möglich, aber nicht als einziger Weg

Der Haltungsname (z. B. 78998-79002) ist in QGIS, GEONIS und SewerStudio gleich.
Andreas' Warnung vor „über 1000 Duplikaten" wurde nachgezählt:

| | Objekte mit echter Nummer | davon Name mehrfach vergeben |
|---|---|---|
| Haltungen | 79'049 | 814 (389 Namen, rund 1 %) |
| Schächte | rund 120'500 | 1'162 (467 Namen, rund 1 %) |

Der grösste Teil der Duplikate sind Platzhalter, die in einer Aufnahme nie vorkommen:
`u-u` (4'394 Haltungen), `-u`, `reserve` (373 Schächte), `GPS0001` bis `GPS0004`.
Die Gemeinde hilft als Zusatzschlüssel kaum, sie ist bei den meisten Duplikaten leer.

Regel daraus: In SewerStudio taugt der Name, um die GEONIS-Kennung aus der Kopie
nachzuschlagen, aber **nur bei genau einem Treffer**. Für den FME-Import in Phase 1
gilt dagegen: **kein Namensersatz**. Die Datei trägt die volle Kennung; eine Kennung,
die GEONIS nicht kennt, wird protokolliert und übersprungen.

## 6. Was in `D:\Fachwissen\ArcGis` gefunden wurde

- **`GEONIS_AWU_2022`**: die GEONIS-Konfiguration mit FME-Workbenches. Darin ein
  **SIA405-2015-Import** (`SIA405_Abwasser_2015_import.fmw`): Die Kernobjekte
  (Haltung, Schacht, Kanal) laufen im UPDATE-Modus mit Abgleich über `GLOBALID` und ohne
  Geometrie-Aktualisierung, `OBJ_ID` wird zu `SIA405_ID`; einzelne Nebenobjekte sind als
  INSERT-Ziele oder mit Geometrie angelegt.
  Andreas' Aussage „keine Schnittstelle" bezieht sich vermutlich auf das Modell 2020_1
  oder darauf, dass der Import nicht eingerichtet ist. Sein Arbeitstag wäre damit eher
  eine Anpassung als ein Neubau.
- **Zuordnungstabellen** (`SIA405_Abwasser_2015_export.xlsx`): GEONIS-Codes je Feld,
  z. B. BaulicherZustand 100–104 = Z0–Z4, Sanierungsbedarf 101 dringend … 105 keiner,
  **106 Saniert wird als „keiner" exportiert**; Profiltyp 2 = Kreisprofil, 105 =
  Rechteckprofil; Material zweistufig (Gruppe + Art), fünf Betonarten fallen im Export
  alle auf `Beton_unbekannt`.
- **Trigonet-Fehlerdatenbank vom 30.05.2025**: 6'635 Haltungen und 2'204 Knoten mit
  doppelter Bezeichnung, 11'660 Haltungen „Profiltyp/Höhe/Breite prüfen", 168 „rund,
  Dimension falsch".
- **`Aktualisierung_Beschriftungen.xml`**: die GEONIS-Regeln, die nach einem Import die
  Beschriftungen nachziehen. Das ist der Punkt, den Andreas „noch klären" wollte.

## 7. Die Datendienste: Was Lisag, WebGIS und geo.ur.ch sind

| Begriff | Was es ist | Richtung |
|---|---|---|
| WebGIS (geohost.ch, persönlicher Zugang, Synergis WebOffice) | Oberfläche von Trigonet direkt auf der GEONIS-Datenbank; zeigt u. a. die OBJECTID | Was Pascal dort eingibt, steht sofort in GEONIS |
| Lisag (geo.ur.ch) | WMS/WFS-Dienste für QGIS; eine veröffentlichte Kopie | nur lesen, Stand vom letzten Upload (20.08.2026) |
| SewerStudio-XTF über FME | Sammelweg für viele Haltungen nach einer Aufnahme | schreibt in GEONIS, nach Prüfung |

Live abgefragt am 4. September 2026:

- Der WFS-Layer `leitungen:abw_abwasserknoten` trägt für **alle 129'623 Knoten** ein Feld
  `xtf_id`. Es ist die GEONIS-Kennung mit anderem Präfix: Schacht 78998 heisst dort
  `ch24gwkdftlGdbHU`, in GEONIS `ch23h1a4ftlGdbHU`. Die letzten acht Zeichen sind das
  Objekt, die ersten acht der Absender. Das ist an dreizehn Stichproben beobachtet
  (zwölf Altdorfer Schächte plus Seilergasse), nicht als GEONIS-Regel belegt. Die
  Live-Abfragen sind nicht archiviert. Zusätzlich liefert der Layer die Kennung des
  zugehörigen Bauwerks (`abwasserbauwerkref`).
- Der Layer `leitungen:abw_haltungen` hat **keine** `xtf_id`, nur die wechselnde `obj_id`.
- Der WebOffice-Zugang ist mit ADFS-Anmeldung geschützt und ohne Login nicht
  automatisierbar.

Folgen:

- Pascal hat einen direkten Schreibweg nach GEONIS (WebGIS). Der XTF-Weg ist der
  Sammelweg für ganze Kampagnen.
- **Konfliktschutz ist Pflicht:** Ändert Pascal im WebGIS eine Haltung und importiert
  später eine ältere XTF aus SewerStudio, würde FME die eigene WebGIS-Änderung
  überschreiben. FME muss das Änderungsdatum in GEONIS (`GN_LAST_EDITED_DATE`) gegen
  das Datum der XTF prüfen und bei Konflikt melden statt schreiben. Beleg: Die Haltung
  78998-79002 hatte im Dezember 2024 keine Höhe, Material unbekannt, Zustand Z4. Im
  November 2025 stand im Lisag-Dienst 150 mm, Steinzeug, Z2.

  Was SewerStudio dafür heute liefert und was nicht: Die XTF trägt in
  `Letzte_Aenderung` den Exporttag, nicht den GEONIS-Ausgangsstand. Seit dem
  4. September speichert SewerStudio je Bauteil das GEONIS-Änderungsdatum aus der
  Kopie (`GeonisGeaendert`), es steht aber noch in keiner Datei. Ein Konfliktschutz
  braucht deshalb eine Begleitdatei mit dem Ausgangsstand je Objekt oder eine
  Abmachung, dass FME gegen den Zeitpunkt der Kennungsübernahme prüft. **Das ist
  noch nicht gebaut.**

## 8. Entscheid für den Test

Pascal hat entschieden: Für den Test wird die GEONIS-Kopie vom Dezember 2024 verwendet.
Aus ihr werden **nur die Kennungen** nach SewerStudio übertragen, keine Fachwerte. Die
Kopie ist alt; ihre Werte würden den Projektstand nur verfälschen.

## 9. Was gebaut wurde

### 9.1 Kennungstabelle

`D:\QGIS_V4.2\Layer\Kataster_Kennungen_GEONIS_2024-12.gpkg` (75 MB), erzeugt mit dem
QGIS-Werkzeug `ogr2ogr` aus der GEONIS-Kopie. Sie enthält:

- Tabelle `haltungen`: 102'317 Haltungen mit Bezeichnung, Gemeinde, Status und den
  Kennungen von Haltung, Kanal, beiden Haltungspunkten (samt GEONIS-Namen), Rohrprofil
  (samt Profiltyp-Code und Verhältnis) und Eigentümer.
- Tabelle `schaechte`: 123'096 Knoten mit Bezeichnung, Gemeinde, Bauart und den
  Kennungen von Knoten und Bauwerk.
- Tabelle `herkunft`: Quelle, Stand `2024-12`, Zweck.

Das Bauskript liegt im Sitzungs-Scratchpad (`kataster/bau_kennungen.sh`). Bei einer
neuen Kopie von Kilian lässt sich die Tabelle in wenigen Minuten neu bauen.

### 9.2 Knopf „Katasterkennungen ergänzen"

Je ein Knopf auf der Haltungs- und der Schachtseite neben „Leere Felder aus QGIS".
Ablauf: Tabelle lesen → Plan rechnen → Bericht zeigen → nach Ja schreiben.

Regeln:

- **Nur bei genau einem Treffer.** Direkter Name zuerst, bei Haltungen danach die
  Gegenrichtung (`B-A` für `A-B`, weil das Projekt bei einer Gegenbefahrung den unteren
  Schacht vorn führt). Dann werden die zwei Punktkennungen vertauscht, damit jeder Punkt
  an seinem Schacht bleibt. Schächte kennen keine Richtung.
- **Eine vorhandene Kennung wird nie ersetzt.** Sie kann aus einem neueren GEONIS-Export
  stammen. Der Bericht meldet gleiche und abweichende Kennungen getrennt.
- **Nur Kennungen, keine Fachwerte.**
- **Nur gültige Kennungen** (16 Zeichen, Buchstabe am Anfang, Buchstaben und Ziffern).
  Alles andere wird ignoriert, statt eine ungültige TID zu schreiben.

Bericht bei der Seilergasse nach dem zweiten Klick: „1 tragen diese Kennung bereits".

### 9.3 Datenmodell

Neues typisiertes Objekt `Geonis` an jeder Haltung und jedem Schacht (`GeonisKennungen`):
Haltung, Kanal, VonPunkt, VonPunktBezeichnung, NachPunkt, NachPunktBezeichnung,
Rohrprofil, RohrprofilTyp, Knoten, Bauwerk, RichtungGedreht, Quelle, GeonisGeaendert
(Änderungsdatum des Objekts in GEONIS zum Stand der Kopie) und UebernommenUtc.
Es wird mit dem Projekt gespeichert. Altprojekte laden unverändert.

### 9.4 Zwei getrennte Felder im Formular

| Feld | Inhalt | Beispiel |
|---|---|---|
| Objekt-ID (Lisag) | die Nummer aus dem Lisag-Dienst geo.ur.ch, bleibt wie bisher | 866789 |
| GEONIS-Kennung | die Hauptkennung aus GEONIS, nur Anzeige | ch23h1a4uL3A2Sjp |

Die Schachtseite bekommt die Spalte „GEONIS-Kennung" auch dann, wenn die Excel-Vorlage
sie nicht kennt. Das Feld ist im Formular und in beiden Tabellen schreibgeschützt: Die
Wahrheit liegt im `Geonis`-Objekt, dort liest der Export; eine Handeingabe liefe daran
vorbei. Bei einem Bauteil, das die Kennung schon trägt, aber das Anzeigefeld noch leer
hat, zieht der Knopf nur das Feld nach. Das Feld geht nie als Sachwert in eine XTF.

Eine TID, die ein XTF-Import in `Objekt_ID` abgelegt hat, zählt beim Knopf als vorhandene
Kennung: Widerspricht sie der Kopie, stammt sie aus einer neueren Quelle und gewinnt,
das Bauteil bekommt dann nichts aus der Kopie.

### 9.5 Export „Neue eigenständige XTF" schreibt GEONIS-Kennungen

Trägt ein Bauteil seine GEONIS-Kennungen, schreibt der Export sie als TID: Kanal,
Haltung, beide Haltungspunkte (mit den GEONIS-Namen `A75394`/`E75394`), Normschacht
und Abwasserknoten. Der Bericht zählt: „2 Objekte tragen ihre GEONIS-Kennung aus dem
Kataster". Bauteile ohne Kennung bekommen wie bisher eine SewerStudio-Kennung.

**Rohrprofil:** Ein Rohrprofil wird in GEONIS von vielen Haltungen geteilt (56 Profile
für 102'317 Haltungen). Der Export verwendet die Profilkennung nur, wenn der Profiltyp
gleich ist und das Profil rund ist (kein Verhältnis oder 1). Bei der Seilergasse steht
in GEONIS „unbekannt", im Projekt „Kreisprofil".
Darum bekommt sie ein eigenes Profil, und der Bericht meldet: „Rohrprofil weicht vom
Kataster ab (Kataster: unbekannt, Projekt: Kreisprofil)".

### 9.6 Höhen-Breiten-Verhältnis 1 beim Kreisprofil (Wunsch Andreas)

In beiden Exportwegen umgesetzt:

- **Neu-Export:** Jedes Kreisprofil trägt `HoehenBreitenverhaeltnis = 1`, bei gleichen
  Massen, bei leerer Breite und ohne Masse. Zwei verschiedene Masse am Kreisprofil
  bleiben ein gemeldeter Widerspruch ohne Verhältnis.
- **Bestehende Katasterdaten aktualisieren:** Beim Wechsel auf rund wird das alte
  Verhältnis auf 1 gesetzt statt gelöscht; ein fehlendes wird ergänzt. Nur an
  Profilen, die genau dieser Haltung gehören; ein geteiltes Profil bleibt unangetastet.
- Rechteck- und Eiprofile unverändert: echtes Verhältnis aus Höhe und Breite.

### 9.7 Formular-Auffrischung

Das Formular rechts war eine Momentaufnahme beim Auswählen und zeigte die frisch
geschriebene Kennung nicht. Nach jedem Sammellauf („Katasterkennungen", „Leere Felder
aus QGIS") wird es jetzt neu aufgebaut. Die Schachtseite konnte das schon.

### 9.8 Tests und Dokumentation

- Infrastruktur-Tests: 6'006 bestanden, 6 übersprungen, 0 Fehler. UI-Wächter grün
  (Registrierung 156 → 157). Die vollständige UI-Suite wurde nicht ausgeführt.
- Neue Tests: Planer, Anwender, Bericht, Tabellenleser, Export mit GEONIS-Kennungen,
  Kreisprofil-Verhältnis, Abhängigkeiten, importierte TID, Änderungsdatum.
- Regeln in `CLAUDE.md` festgehalten. Das Bauskript der Kennungstabelle liegt unter
  `tools/KatasterKennungen/bau_kennungen.sh`.
- **Nicht committet.** Alle Änderungen liegen im Arbeitsstand des Zweigs
  `feature/eval-pruefsatz-review`, auch dieses Dokument und das Skript.

## 10. Vorschlag an Trigonet: Sicherheitsregeln für den FME-Import

| Regel | Warum |
|---|---|
| Abgleich über die volle 16-stellige `SIA405_ID` (TID) auf genau ein Objekt; kein Namens- oder Acht-Zeichen-Ersatz | Duplikate treffen sonst die falsche Haltung |
| Nur Update bestehender Objekte; kein Anlegen, kein Löschen, keine Geometrie (Phase 1) | Ein Fehler in der Datei darf keine Objekte erzeugen oder entfernen |
| Feste Feldliste (Whitelist); alles andere ignorieren | SewerStudio schreibt: Kanal (Nutzungsart, Zustand, Funktionen, Verbindungsart, Bettung, Status, Sanierungsbedarf, Baujahr, Bruttokosten, Bemerkung), Haltung (Material, Lichte Höhe, Länge, Lagebestimmung), Rohrprofil (Profiltyp, Verhältnis), Normschacht (Funktion, Material, Dimension 1/2, Zustand, Bemerkung, Status, Sanierungsbedarf, Baujahr) |
| Leer heisst „nicht anfassen"; ein Feld nur schreiben, wenn es sich vom Datenbankwert unterscheidet | Sanierungsbedarf 106 „Saniert" käme sonst als „keiner" zurück; fünf Betonarten fielen auf „Beton unbekannt". Achtung: Der Neu-Export ist ein Voll-Export ohne Ausgangswerte. FME kann damit „unverändert" nicht von „absichtlich geändert" unterscheiden. Dafür braucht es ein Änderungsmanifest aus SewerStudio, das noch nicht existiert. |
| TID nicht gefunden → Protokoll, überspringen | Nie auf Bezeichnung ausweichen |
| Probelauf zuerst (nur Protokoll), dann Echtlauf | Vergleich mit dem SewerStudio-Bericht: gleiche Zeilen, gleiche Werte |
| FME schreibt vor dem Import eine „Vorher-Datei" der betroffenen Objekte | Ersatz für das fehlende Rückgängig; gilt erst als Rückgängig, wenn die Wiederherstellung daraus einmal getestet wurde |
| Konfliktschutz über das Änderungsdatum | Schutz der eigenen WebGIS-Änderungen; der Ausgangsstand je Objekt muss von SewerStudio noch mitgeliefert werden (offen, siehe Kapitel 7) |
| ilivalidator vor jeder Abgabe | automatisch, nicht von Hand |

Offene Punkte an Andreas:

- Bitte bestätigen und testen: Die volle Kennung aus der XTF wird auf `SIA405_ID`
  und von dort auf `GLOBALID` aufgelöst. SewerStudio schreibt die volle
  GEONIS-Kennung mit Präfix `ch23h1a4`.
- Kann der vorhandene 2015-Import auf 2020_1 angepasst werden?

Offen auf Seite SewerStudio:

- Änderungsmanifest zur XTF (Objekt, Feld, Ausgangswert, neuer Wert) und der
  GEONIS-Ausgangsstand je Objekt für den Konfliktschutz.
- Das WinCan-Zuordnungsverhalten und die Live-WFS-Abfragen sind nicht als
  reproduzierbarer Beleg gespeichert.

Wunsch an die Lisag: `xtf_id` auch am Haltungslayer veröffentlichen, wie beim
Knotenlayer. Dann hätte SewerStudio alle Kennungen live und aktuell, ohne alte Kopie.

## 11. Testablauf Seilergasse

1. SewerStudio schliessen, neu bauen, starten.
2. Projekt Seilergasse öffnen.
3. Haltungen: „Katasterkennungen" → Bericht → Ja. Im Feld GEONIS-Kennung steht
   `ch23h1a4uL3A2Sjp`.
4. Schächte: „Katasterkennungen" → Bericht → Ja. Schacht 78998 bekommt
   `ch23h1a4ftlGdbHU`.
5. Speichern.
6. Export → „Neue eigenständige XTF erstellen". Bericht: „2 Objekte tragen ihre
   GEONIS-Kennung aus dem Kataster" plus Rohrprofil-Hinweis.
7. In der Datei prüfen: Haltung `TID="ch23h1a4uL3A2Sjp"`, Knoten `ch23h1a4ftlGdbHU`,
   Rohrprofil mit `<HoehenBreitenverhaeltnis>1</HoehenBreitenverhaeltnis>`.
8. ilivalidator laufen lassen, dann an Andreas.
9. Bei Trigonet: FME-Probelauf ohne Schreiben, Protokoll mit SewerStudio-Bericht
   vergleichen (welche Objekte, welche Felder).
10. Sicherung der betroffenen Objekte, dann Echtlauf.
11. Rücklesen aus GEONIS (WebGIS-Maske oder Export) und Vergleich mit dem
    Projektstand. Nachweis, dass keine weiteren Objekte und keine Geometrien
    verändert wurden.

Bis Schritt 8 ist der Weg im Code umgesetzt; der eigentliche Test in GEONIS
(Schritte 9 bis 11) ist offen.

Stand am Abend des 4. September: Die Haltung trägt ihre Kennung (Projektdatei geprüft),
der Schacht noch nicht, gespeichert wurde zuletzt um 19:28 vor dem Anzeigefeld, eine
neue XTF gibt es noch nicht.

## 12. Antwortentwurf an Andreas

> Sali Andreas
>
> Danke für die schnelle und konkrete Rückmeldung. Der Prozess mit FME passt für mich.
>
> Zum Primärschlüssel: Meine Testdatei war ein Neu-Export mit eigenen Kennungen, sorry
> für die Verwirrung. Ich habe in eurer GEONIS-Konfiguration nachgeschaut: Ihr führt an
> Haltung und Knoten das Feld SIA405_ID, und euer Export schreibt es als TID. Genau
> darüber sollten wir abgleichen, nicht über die Bezeichnung. Mein Programm schreibt
> diese Kennungen jetzt selbst in die Datei; das ist im Code umgesetzt, der Endtest mit
> eurem Import ist noch offen. Ohne Treffer bitte protokollieren und überspringen, kein
> Ersatz über die Bezeichnung.
>
> Das Höhen-Breiten-Verhältnis ist drin, beim Kreisprofil als 1.
>
> Für die Sicherheit schlage ich vor: nur Update bestehender Objekte, feste Attributliste,
> nur geänderte Felder schreiben, unbekannte TID protokollieren und überspringen, zuerst
> ein Probelauf mit Protokoll, FME sichert die betroffenen Objekte vor dem Schreiben als
> XTF, und ein Konfliktschutz über das Änderungsdatum, weil ich parallel im WebGIS
> arbeite. Die Attributliste schicke ich dir.
>
> Zwei Fragen: Könnt ihr bestätigen und testen, dass die volle 16-stellige Kennung aus
> der Datei auf eure SIA405_ID und damit auf die GlobalId aufgelöst wird? Und lässt sich
> euer vorhandener SIA405-2015-Import auf 2020_1 anpassen?
>
> Ein Arbeitstag Initialaufwand ist für mich in Ordnung.
>
> Gruss, Pascal
