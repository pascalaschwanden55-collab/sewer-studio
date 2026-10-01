# WebGIS-Begriffe in SewerStudio — Entwurf

Stand 23.09.2026. Entscheide Pascal vom selben Tag. Branch `feature/webgis-uebertragung`.

## Ziel

Die Daten, die Pascal in SewerStudio ändert, gehen direkt in die Trigonet-Datenbank hinter dem
WebGIS (GEONIS Uri). Deshalb gilt:

> **Ein Feld, das ins WebGIS geht, enthält nur einen Wert aus der WebGIS-Auswahlliste — zeichengenau.**
> In SewerStudio darf in diesen Feldern kein Begriff stehen, den es im WebGIS nicht gibt.

Heute führen diese Felder die SIA405-Normbegriffe (`in_Betrieb`, `PAA.Sammelkanal`,
`Fertigbetonelement`, `Kreisprofil`). Das Holen übersetzt WebGIS-Werte in diese Begriffe, und das
Senden faltet sie ungefähr zurück. Beides entfällt.

## Entscheide (Pascal, 23.09.2026)

1. Massstab ist die WebGIS-Liste. Der XTF-Export ist Nebensache: Er übersetzt in die Norm, wo das
   eindeutig geht. Der SIA405-Export lässt ein Feld ohne Norm weg und meldet es (wie heute).
   **Der DSS-Export bleibt fail-closed:** Ein Wert ohne Norm sperrt die Lieferung mit Meldung, wie
   heute bei offenen Materialzuordnungen (`DssMaterialZuordnung`). Ein stilles Weglassen könnte dort
   eine Änderung verlieren (Prüfung 23.09.2026).
2. **Zustandsklasse bleibt intern die Ziffer 0–4.** Zum WebGIS ist sie schon exakt 1:1 zugeordnet
   (2 = «Mittlere Mängel (Z2)», `WebGisFeldkarte`). Über 60 Programmteile rechnen mit der Ziffer.
3. **Bestandswerte ohne WebGIS-Gegenstück** bleiben stehen und werden markiert, nie gelöscht. Sie
   erscheinen in einer Korrekturliste und gehen nie ins WebGIS.
4. **Schachtform** ist schon gleich wie im WebGIS (`SchachtformVokabular` = Liste schacht-C08).
   **Masse** (DN, lichte Breite, Dimension 1/2) sind im WebGIS Auswahllisten mit festen Zahlen
   (`WebGisHandwertKarte`: Combo). Eine Zahl, die dort nicht vorkommt, ist nicht sendbar.
   **Entscheid Pascal 23.09.2026 (Variante A):** Die Zahl bleibt, wie gemessen. Gesendet wird sie nur,
   wenn genau diese Zahl in der WebGIS-Liste steht; sonst Hinweis beim Senden (wie heute), und die
   Projektprüfung markiert sie als «nicht sendbar».
5. Haltungslänge, Eigentümer und Betreiber bleiben ausserhalb, wie bisher.
6. **«Unbekannt»** ist ein gültiger WebGIS-Wert und darf gespeichert und gewählt werden. Das Holen
   füllt damit aber weiterhin kein leeres Feld (bestehende Regel, `WebGisImportWert`). In den
   Rundreisetests ist «Unbekannt» deshalb ausgenommen.
7. **Die Senderegeln bleiben unverändert.** Sanierungsbedarf geht nur als «Saniert» und nur mit
   ausgeführter Sanierungsakte ins WebGIS (`WebGisExportPlanBuilder`). Die Bettung steht nicht in
   der `WebGisHandwertKarte` und wird nicht gesendet. Beide Felder werden in Schritt A nur
   gespeichert und angezeigt wie im WebGIS; am Senden ändert sich nichts.

## Quelle der Listen

Die WebGIS-Listen liegen schon im Programm: `Objektakten.Katalog.json`, aus den WebGIS-Masken
erhoben (Katalog-IDs haltung-C.., schacht-C..). Das ist die eine Quelle für Auswahl, Import und
Prüfung. Bei jedem Lesen aus dem WebGIS vergleicht das Programm die Liste der Maske
(`WebGisLesestand.Kataloge`) mit dem Katalog. Fehlt ein Eintrag oder ist einer neu, erscheint ein
Hinweis. Die Liste wird nie still ergänzt.

## Aufbau

- **Die Vokabulare bleiben die eine Stelle**, an der ein Wert erkannt wird. Ihr App-Begriff (der
  gespeicherte Wert) wechselt auf die WebGIS-Beschriftung. SIA-, WinCan- und PDF-Schreibweisen
  bleiben als Lese-Aliase. `NachNorm` bleibt für den XTF-Export und kennt die WebGIS-Beschriftungen
  als Alias, mit Umlaut-Faltung und abgetrennten Kürzeln in Klammern.
- **`ProjectVocabularyNormalizer`** (läuft beim Laden und Speichern) hebt eindeutige Bestandswerte
  auf die WebGIS-Beschriftung, für alle Felder im Umfang, nicht nur die heutigen sechs. Nicht
  eindeutige Werte lässt er stehen und meldet sie. Wichtig: Heute dreht er WebGIS-Beschriftungen
  zurück («Kreisprofil (K)» → «Kreisprofil»). Ohne die Umstellung wäre alles andere umsonst.
- **Holen (`WebGisImportWert`)** speichert den WebGIS-Klartext wörtlich. Einzige Prüfung: Er steht
  in der Katalogliste des Felds.
- **Senden (`WebGisHandwertKarte.Schluessel`)** sucht den Schlüssel über den exakten Klartext. Die
  Faltung bleibt nur als Übergang für Bestandswerte, bis sie angehoben sind, und erzeugt dann
  einen Hinweis.
- **Objektakte (`ObjektaktenBearbeitung.Normalisiere`)** schreibt den WebGIS-Klartext ins
  Speicherfeld. Heute übersetzt sie sechs Felder und lässt die anderen roh. Das wird einheitlich.
- **Auswahllisten** in Tabelle, Kurzansicht, Aufklappliste und Objektakte zeigen nur die
  WebGIS-Liste. Freitext ist in diesen Feldern aus. Ein Bestandswert ausserhalb der Liste wird
  sichtbar markiert angezeigt. Er darf nie leer erscheinen: Die Tabelle bindet heute an
  `SelectedItem` und würde ihn bei der ersten Bedienung überschreiben (`GridDropdownFieldPolicy`).
- **Importe** (PDF, WinCan, IBAK, KINS, SchachtPro, XTF, GeoShop, QGIS) laufen alle durch das
  Vokabular. Wo ein Import heute roh schreibt (XTF-Status und -Hydraulik, WinCan-Schacht, KINS,
  SchachtPro- und PDF-Schachtfunktion, Nachschlagen), wird das Vokabular vorgeschaltet.
  Kein eindeutiger WebGIS-Begriff heisst: Feld leer, Hinweis im Importbericht.
- **Korrekturliste:** Die Projektprüfung (`ProjektPruefregeln`) bekommt die Regel «Wert ohne
  WebGIS-Begriff» mit Sprung zum Feld.

## Leser, die mitgehen müssen

| Leser | heute | nachher |
|---|---|---|
| XTF-Export (`XtfStammdatenPlanBuilder`, `XtfSchachtPlanBuilder`, `XtfNeuPlanBuilder`) | `NachNorm` auf SIA-Begriffe | `NachNorm` kennt die WebGIS-Beschriftungen; ohne Norm: Feld weg + Hinweis |
| DSS-Export (`DssFeldZuordnung`, `DssExportSchema`) | unbekannter Wert sperrt | gleiche Übersetzung; ohne Norm sperrt er weiterhin mit Meldung (fail-closed) |
| VSA-Bewertung (`VsaConditionScorer` B2 Nutzungsart, B4 Funktion) | feste SIA-Zeichenketten | über das Vokabular |
| Farben (`NutzungsartReportColors`), Dossier, Schachtgrafik | über das Vokabular | Alias genügt |
| Kosten (`CostOptimizationEngine`), Excel-Langform (`ExcelMaterialLangform`), Hydraulik | teils feste Zeichenketten | über das Vokabular (Schritt B) |
| GeoShop-Vergleich (`GeoShopFeldWahl`) | exakter Textvergleich | GeoShop-Wert erst übersetzen, dann vergleichen; sonst Scheinabweichungen |
| Bauwerksart aus der Funktion (`AbwasserbauwerkVokabular`) | feste SIA-Funktionsnamen | über das Vokabular |

## Schritte

Jeder Schritt ist für sich lauffähig, getestet und in Zone 1.15 mit «Vom WebGIS holen» prüfbar.

### A — einfache Listen

Felder: Status (H+S), Lagebestimmung (H+S), Funktion hydraulisch, Verbindungsart, Bettung/Umhüllung,
Sanierungsbedarf (H+S), Nutzungsart (H+S), Profiltyp, Schachtfunktion.

Zuordnung bisher → WebGIS (eindeutig, wird beim Laden angehoben):

| Feld | bisher | WebGIS |
|---|---|---|
| Status | in_Betrieb · ausser_Betrieb · tot · unbekannt | In Betrieb · Ausser Betrieb · Tot/Aufgehoben, verfüllt · Unbekannt |
| Lagebestimmung | genau · ungenau · unbekannt | Genau · Ungenau · Unbekannt |
| Sanierungsbedarf | dringend · kurzfristig · mittelfristig · langfristig · keiner · unbekannt · Saniert | Dringend · Kurzfristig · Mittelfristig · Langfristig · Keiner · Unbekannt · Saniert |
| Nutzungsart | Niederschlagsabwasser · Bachwasser · andere · unbekannt (übrige gleich) | Regenabwasser · Bachabwasser · Andere · Unbekannt |
| Profiltyp | Kreisprofil · Eiprofil · Maulprofil · offenes Profil · Rechteckprofil · Spezialprofil · unbekannt | Kreisprofil (K) · Eiprofil (E) · Maulprofil (E) · Offenes Profil (OP) · Rechteckprofil (R) · Spezialprofil (S) · Unbekannt (U) |
| Funktion hydraulisch | Duekerleitung · Spuelleitung · andere · unbekannt (übrige gleich) | Dükerleitung · Spülleitung · Andere · Unbekannt |
| Verbindungsart | spiegelgeschweisst · Ueberschiebmuffen · andere · unbekannt (übrige gleich) | Spiegelgeschweisst · Überschiebmuffen · Andere · Unbekannt |
| Bettung | erdverlegt · in_Kanal_aufgehaengt · in_Kanal_einbetoniert · in_Leitungsgang · in_Vortriebsrohr_Beton/_Stahl · SIA_Typ1..4 · andere · unbekannt | Erdverlegt · In Kanal aufgehängt · In Kanal einbetoniert · In Leitungsgang · In Vortriebsrohr Beton/Stahl · SIA Typ1..4 · Andere · Unbekannt |

«Maulprofil (E)» steht so im WebGIS (Kürzel E wie beim Eiprofil) und wird wörtlich übernommen.

**Offen — Pascal bestätigt vor Schritt A:**

| Feld | bisher | Vorschlag WebGIS | Frage |
|---|---|---|---|
| Schachtfunktion | Pumpwerk | Pumpenschacht | gleiche Bedeutung? |
| Schachtfunktion | Trennbauwerk | Trennschacht | gleiche Bedeutung? |
| Schachtfunktion | Absturzbauwerk | Absturzschacht | gleiche Bedeutung? |
| Schachtfunktion | Be-/Entlüftung, Sickerschacht, Spezialbauwerk | — (nicht in der Normschacht-Liste) | markieren; oder gehören sie zu einer anderen WebGIS-Maske (Bauwerksart)? |
| Status | weitere | — | markieren |
| Funktion hydraulisch | Versickerungsleitung | — | markieren |
| Profiltyp | «Andere (A)» im WebGIS | eigener Wert, nicht mehr Spezialprofil | alte Werte bleiben «Spezialprofil (S)» |

Die WebGIS-Schachtmaske hat einen Subtyp (Bauwerksart). Der Katalog enthält die Funktionsliste der
Normschacht-Maske. Ob Spezialbauwerk und Versickerungsanlage eigene Listen haben, ist nicht erhoben.
Bis das belegt ist, werden Schächte dieser Bauwerksarten bei der Funktion nur markiert, nicht
umgestellt.

### B — Material

Haltung (43 WebGIS-Details) und Schacht (25), jeweils Gruppe und Detail wie im WebGIS. Gespeichert
wird das Detail wörtlich («Beton, Fertigteil (BF)», «Schleuderbeton (SBR)»), die Gruppe in der
Objektakte (`haltung.pipegroup`, `schacht.materialgruppe`). Die Zuordnung der bisherigen Begriffe
(«Normalbeton», «Fertigbetonelement», «Zement», «Steinzeug» …) kommt als eigene Tabelle zur
Bestätigung, bevor B beginnt. XTF: 40 von 43 Haltungsmaterialien und 23 von 25 Schachtmaterialien
haben keine Norm → Feld weg + Hinweis. DSS: `DssMaterialZuordnung` arbeitet schon mit den
WebGIS-Beschriftungen.

### C — Funktion hierarchisch und Typ AA

**Vor Schritt C neu zu klären (Prüfung 23.09.2026):** Typ AA gibt es in beiden Objektakten (Haltung
und Schacht). Der Normschacht hat aber kein XTF-Attribut FunktionHierarchisch. «PAA + Blatt» gilt
deshalb nur für die Haltung. Das WebGIS-Blatt «Rinne» ist kein Wert der SIA-Liste. Der Abschnitt
unten ist die Richtung, nicht die fertige Regel.

Das WebGIS führt zwei Felder: «Typ AA» (PAA/SAA) und die Funktion ohne Präfix («Sammelkanal»,
«Hauptsammelkanal, regional», «Rinne»). SewerStudio führt beides in einem Feld («PAA.Sammelkanal»).
Neu: eigenes Feld Typ AA an Haltung und Schacht, FunktionHierarchisch nur noch das Blatt.
`PAA.Sammelkanal` wird zu Typ AA «PAA» + «Sammelkanal». XTF/DSS setzen «PAA.» + Blatt wieder
zusammen. Die VSA-Bewertung (B4) liest Typ AA und Blatt. Fehlt Typ AA, gibt es keinen erfundenen
Wert: XTF ohne Funktion + Hinweis, VSA wie heute bei unbekannter Funktion.

## Prüfung

- Je Feld ein Test: Jeder Eintrag der Auswahl steht in der WebGIS-Katalogliste und umgekehrt
  (Wächter, analog `DropdownExportierbarkeitTests`, der angepasst wird).
- Rundreise je gesendetem Feld: Bestandswert → Laden (angehoben) → Senden (exakter Schlüssel) → Holen
  (gleicher Text) → keine Änderung. Ausgenommen: «Unbekannt» (Holen füllt damit nichts), Bettung
  und Sanierungsbedarf (nicht bzw. nur als «Saniert» gesendet).
- Nicht eindeutige Bestandswerte bleiben stehen, erscheinen in der Korrekturliste und gehen nie ins
  WebGIS.
- XTF-Rundreise und ilivalidator bleiben grün. Felder ohne Norm fehlen mit Hinweis.
- Abnahme in Zone 1.15: «Vom WebGIS holen» nach Schritt A zeigt für diese Felder keine
  Übersetzungen mehr und keine Hinweise «passt zu keinem SewerStudio-Wert».

## Nicht im Umfang

Zustandsklasse (bleibt Ziffer), Schachtform, Masse, Haltungslänge, Eigentümer, Betreiber, Baujahr,
Bemerkung. Weitere Maskenfelder erst nach Inventur und Prüfung am echten Objekt.
