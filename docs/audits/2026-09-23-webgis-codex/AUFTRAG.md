# Prüfauftrag: WebGIS holen und senden (SewerStudio ↔ GEONIS/Trigonet)

Auftraggeber: Pascal (Kanalinspekteur, entwickelt SewerStudio allein, kein Informatiker).
Repo: `C:\Sewer-Studio_KI_5.0`, Branch `feature/webgis-uebertragung`.

## 1. Worum es geht

SewerStudio (WPF, .NET 10) tauscht Haltungs- und Schachtdaten direkt mit dem WebGIS
des Kantons Uri aus (GEONIS WebOffice, Attribut-Editor, betrieben von Trigonet).

- **Holen** (WebGIS → SewerStudio): füllt Felder in SewerStudio und speichert je Objekt
  die WebGIS-GlobalID.
- **Senden** (SewerStudio → WebGIS): schreibt Zustand, Sanierungsbedarf, Bemerkung,
  Handkorrekturen und Sanierungsmassnahmen **direkt in die Trigonet-Datenbank**.

Ein falscher Wert, ein falsch zugeordnetes Objekt oder ein versehentlich geleertes
Feld ist dort ein echter Schaden. Die Funktion muss **absolut zuverlässig und
abgesichert** sein. Prüfe den ganzen Weg gründlich und belege jeden Befund mit
Datei und Zeile, einem Test oder einer Stelle in einem echten Bericht.
**Vertraue keiner Dokumentation (CLAUDE.md, docs/) — prüfe jede Aussage am Code.**

## 2. Verbindliche Regeln des Auftraggebers

Prüfe für jede Regel, ob der Code sie überall einhält. Jede Verletzung ist ein Befund.

1. **Kanalfirmen-Daten werden nie überschrieben** (Entscheid Pascal 23.09.2026). Pascal
   bekommt Inspektionsdaten von Kanalfirmen (WinCan, IBAK, KINS, M150, PDF-Protokolle,
   VSA-KEK- oder SIA405-XTF). Diese Werte bleiben in SewerStudio. **Weder das Holen aus dem
   WebGIS noch der GeoShop-Abgleich noch „Leere Felder aus QGIS" überschreiben sie.** Alle
   drei **ergänzen nur leere Felder**. Das gilt ausdrücklich auch für die Haltungslänge.
2. **Katasterwerte darf das WebGIS ersetzen.** Werte, die selbst aus einem GeoShop- oder
   QGIS-Abgleich stammen und nicht von Hand gesetzt sind, dürfen durch den WebGIS-Wert
   ersetzt werden („WebGIS vor GeoShop"). Kanalfirmen-Werte fallen nie darunter.
3. **Handeingaben nie ersetzen**, auch nicht ein bewusst von Hand geleertes Feld.
4. **GlobalID:** Jede Haltung und jeder Schacht bekommt die GlobalID des WebGIS, damit die
   Zuordnung sicher ist. Sie wird nur bei **genau einem** Suchtreffer mit **exakt gleichem
   Namen** gespeichert und danach **nie überschrieben**. Ist sie gespeichert, laufen alle
   Lese- und Schreibschritte nur über sie (nie über den Namen, auch nicht als Rückfall).
   Der Name in der WebGIS-Maske muss dann exakt passen, sonst Sperre. OBJECTID und
   GeoShop-/SIA405-Kennungen (`ch24gwkd…`, `ch23h1a4…`, `chSST…`) sind **keine** GlobalID.
5. **Felder identisch:** Jedes Feld, das zwischen SewerStudio und WebGIS ausgetauscht wird,
   muss in beiden Systemen dieselbe Bedeutung, denselben Datentyp, dieselbe Einheit und
   **dieselbe Auswahlliste mit zeichengenau denselben Einträgen** haben. In SewerStudio
   darf kein Begriff gespeichert oder auswählbar sein, den es im WebGIS nicht gibt
   (Groß-/Kleinschreibung, Umlaute, Kürzel in Klammern wie „Kreisprofil (K)" zählen mit).
6. **Haltungslänge:** Die Länge der Kanalfirma (des Operateurs) bleibt in SewerStudio.
   Sie wird **weder** aus dem WebGIS geholt **noch** ins WebGIS geschrieben.
7. **Eigentum und Betreiber** ändert das Programm **nie**, in keine Richtung.
8. **Baujahr** nur füllen, wenn es leer ist (in beide Richtungen). Ein im WebGIS
   eingetragenes Baujahr wird nie überschrieben.
8a. **Im WebGIS nie überschrieben** (Entscheid Pascal 23.09.2026): Eigentum, Betreiber,
    Haltungslänge (alle Längenfelder), Baujahr, GlobalID, Objekt-ID (OBJECTID) und die
    Bezeichnung, an der das Objekt erkannt wird.
8b. **Eindeutig identifiziert:** Zurückgeschrieben wird nur in ein WebGIS-Objekt, das genau
    zu **einem** SewerStudio-Objekt gehört. Zeigen zwei SewerStudio-Objekte auf dieselbe
    GlobalID, wird keines geschrieben.
8c. **Änderungsdatum:** Jedes WebGIS-Objekt trägt „Geändert am (UTC)". Weicht der Stand
    beim Schreiben (samt Änderungsdatum) vom zuvor gelesenen ab, wird nicht geschrieben,
    sondern neu geprüft.
9. **Zustandsklasse** bleibt in SewerStudio die Ziffer 0–4 und geht als WebGIS-Code 100–104.
   **Sanierungsbedarf „Saniert"** nur bei einer ausgeführten Sanierungsakte, nie aus dem
   Bemerkungstext. **Bemerkung** wird zusammengeführt, nie überschrieben.
10. **Masse** (DN, Breite, Höhe, Schachtmasse): Die gemessene Zahl bleibt. Gesendet wird
    nur, was exakt in der WebGIS-Liste steht; der Rest wird gemeldet.
11. **Senden** nur nach einer Vorschau. Vor jedem Schreiben frisch lesen; hat sich im WebGIS
    seit der Vorschau etwas geändert, erscheint eine neue Vorschau statt eines Schreibens.
    **Nie ein Feld leeren** (Achtung: `saveData` mit `value:null` leert ein Feld im WebGIS —
    am 21.09.2026 so real passiert bei 44 Objekten).
12. **Kein Raten.** Mehrdeutig, unbekannt oder unsicher heisst: nichts schreiben, Objekt
    oder Feld sperren und den Grund sichtbar melden.

## 3. Harte Grenzen für dich

- **Nicht ins WebGIS schreiben, nicht anmelden, keinen Browser öffnen, keine Live-Aufrufe.**
  Was nur live prüfbar ist, kommt auf die Liste in Abschnitt 6.4.
- **Kundenprojekte unter `D:\Projekte` nur lesen.** Dort nichts ändern, auch keine Kopie
  und keine `.bak` anlegen.
- **Keine Änderungen am Produktcode (`src/`), keine Commits, kein Push.** Beweistests darfst
  du neu anlegen unter `tests/AuswertungPro.Next.Infrastructure.Tests/WebGis/Pruefung20260923/`.
  Sie dürfen rot sein, wenn sie einen Fehler belegen; nenne jeden im Bericht.
- **SewerStudio nicht starten.** Läuft es, ist `bin\Debug` gesperrt: dann mit
  `-o .tmp/codex-webgis` bauen und testen.
- In isolierten WPF-Tests nach `new App()` **nie die Dispatcher-Warteschlange pumpen**
  (`PushFrame`, `Dispatcher.Invoke`, Timer). Sonst läuft `App.OnStartup` im Testprozess,
  und ein echter Fehlerdialog erscheint auf Pascals Bildschirm (23.09.2026 passiert).
- Keine neuen NuGet-Pakete.

## 4. Wo du anfängst

Code:
- `src/AuswertungPro.Next.Application/WebGis/` — alle Regeln: `WebGisFeldkarte`,
  `WebGisHandwertKarte` (die ausgetauschten Felder mit refIds), `WebGisExportPlanBuilder`,
  `WebGisExportUseCase`, `WebGisImportPlanBuilder`, `WebGisImportUseCase` (darin
  `IstErsetzbar`), `WebGisImportWert`, `WebGisObjektLesen`, `WebGisPlanVergleich`,
  `WebGisBemerkung`, `WebGisSaniertKriterium`, `WebGisSanierung*`, `WebGisImportBericht`,
  `WebGisExportBericht`, `WebGisUebersicht`.
- `src/AuswertungPro.Next.Infrastructure/WebGis/` — `GeonisWebGisClient` (Suche, Lesen,
  `saveData`, `LiesJsonOderSitzungsfehler`, `AntwortAuswerten`),
  `GeonisWebGisClient.Sanierung.cs`, `PlaywrightWebGisAnmeldung`, `WebGisZugang`.
- Domain: `WebGisBegriffe`, `WebGisWerteliste`, `FieldCatalog` (`GetComboItems`),
  `Objektakten.Katalog.json`, `HaltungRecord`/`SchachtRecord` (`SetFieldValue`,
  `FuelleLeeresFeld`, `WebGisGlobalId`), `FieldSource`, `FieldMetadata`.
- Umwandlung beim Laden: `ProjectVocabularyNormalizer`.
- Auswahllisten der Oberfläche: `DataPageDropdownOptionSets`, `SchachtNormoptionen`,
  `SanierungsbedarfOptionen`, `SchaechteColumnPolicy`, Objektakte (`ObjektFeldViewModel`).
- UI: `ExportPageViewModel.WebGis.cs`, `WebGisHolenAblauf`, `WebGisHolenWindow`,
  `WebGisVorschauWindow`, `ServiceProvider.WebGis.cs`.
- Projektprüfung: `ProjektPruefregeln` (Meldung „ist kein WebGIS-Begriff").
- Import-Wege der Kanalfirmen, wegen der `FieldSource`: `LegacyXtfImportService`
  (inkl. `.VsaKek.cs`, `.Schaechte.cs`), `M150MdbImportHelper`, WinCan, IBAK, KINS,
  PDF-Import, `MergeEngine`; GeoShop/QGIS: `GeoShopZiel`, `GeoShopAttributZuordnung`,
  `QgisFeldKarte`, `LeereFelderAnwender`.

Tests: `tests/AuswertungPro.Next.Infrastructure.Tests/WebGis/` sowie
`WebGisHolenFensterUiTests` im UI-Testprojekt.

Dokumente (nur als Hinweis, nicht als Wahrheit): Abschnitt „WebGIS-Export" in `CLAUDE.md`,
`docs/audits/2026-09-22-webgis/BEHEBUNG.md`,
`docs/superpowers/specs/2026-09-23-webgis-begriffe-design.md`,
`docs/reviews/2026-09-11-objektakten/WEBGIS-LAYOUT-KOMPLETT.md`,
`docs/reviews/2026-09-12-webgis/DROPDOWN-ABGLEICH.md`, `MATERIAL-ENTSCHEIDUNGEN.md`.

Echte Belege (nur lesen):
- `D:\Projekte\Sanierungsabnahme_Zone_5.01_GKS_Bürglen\__WebGIS_Export\`:
  `Feldzuordnung_SewerStudio_WebGIS_v2.md` (Feldinventur der echten Masken),
  `webgis_stand_20260921.json` (gelesener WebGIS-Stand), `WebGIS_Log.txt`,
  `WebGIS_Vorschau_*.txt`, `WebGIS_Ergebnis_*.txt` (echte Schreibläufe 21./22.09.).
- `D:\Projekte\Zone 1.15\__WebGIS_Export\WebGIS_Holen-Vorschau_*.txt`
  (Holen-Läufe 23.09., zuletzt 182 Objekte, 10 gesperrt).
- GEONIS-Kopie vom Dezember 2024 (nur zum Vergleich von Namen und GlobalIDs):
  `D:\QGIS_V4.2\Layer\Kataster_Kennungen_GEONIS_2024-12.gpkg`.

## 5. Was du prüfen sollst

### 5.1 Feldabgleich (Kern des Auftrags)
Für **jedes** Feld, das geholt oder gesendet wird (alle Einträge der `WebGisHandwertKarte`
und `WebGisFeldkarte`, Materialgruppe der Objektakte, GlobalID, Bezeichnung,
Sanierungsmassnahme), halte fest und vergleiche:
- WebGIS: refId, Feldtyp (Auswahl, Zahl, Text, Datum), Einheit, Nachkommastellen,
  Textlänge, Pflichtfeld, vollständige Auswahlliste (Schlüssel **und** Text) laut
  Inventur v2, `webgis_stand_20260921.json` und `Objektakten.Katalog.json`.
- SewerStudio: Feldschlüssel, Typ, Einheit, gespeicherte Schreibweise, und die Auswahlliste
  **an jeder Stelle, an der man den Wert wählen kann** (Tabelle, Kurzansicht, Formular,
  Objektakte). Alle Stellen müssen dieselbe Liste zeigen.
- Richtung: nur holen, nur senden, beides.
- Abweichungen: fehlende oder zusätzliche Einträge, andere Schreibweise (Umlaut, `ae`,
  Kürzel, Gross/Klein), Einheitenfehler (m gegen mm), Rundung, Dezimaltrenner,
  abhängige Listen (Materialgruppe → Materialdetail, Art → Verfahren, Bauwerksart →
  Funktion).
- **Rundreise:** Ein Wert, der geholt wird, muss beim Senden unverändert als derselbe
  WebGIS-Schlüssel zurückgehen. Belege das je Feld mit einem Test (Holen → Senden ergibt
  keine Änderung).

### 5.2 Wer gewinnt? (Herkunft der Werte)
Erstelle eine Tabelle: je Feld und je Herkunft (Kanalfirma über WinCan / IBAK / KINS / PDF /
VSA-KEK-XTF / SIA405-XTF, GeoShop, QGIS, Handeingabe, bewusst leer, WebGIS) — was passiert
beim Holen, was beim Senden?

**Zwei bekannte Verstösse gegen Regel 1, gezielt prüfen und vollständig beschreiben:**

1. `WebGisImportUseCase.IstErsetzbar` lässt Werte der Herkunft `Kataster`, `Xtf`, `Xtf405`
   und `Ili` durch den WebGIS-Wert ersetzen. `Xtf` vergeben aber Kanalfirmen-Importe
   (`LegacyXtfImportService.VsaKek.cs`, `M150MdbImportHelper`), und `Xtf405` vergibt der
   SIA405-XTF-Import (`LegacyXtfImportService.cs`), egal ob die Datei von einer Kanalfirma
   oder vom Kanton kommt. Kläre für jede `FieldSource`, welche Import-Wege sie vergeben, ob
   man heute Kanalfirma und Kataster überhaupt unterscheiden kann, welche Werte fälschlich
   ersetzt würden, und schlage eine sichere Unterscheidung vor.
2. `GeoShopAbgleichPlanBuilder.ImmerAusXtf` ersetzt beim GeoShop-Abgleich die
   Haltungslänge **immer** durch den GeoShop-Wert, auch einen von Hand gesetzten
   (älterer Entscheid vom 11.09.2026, durch Regel 1 und 6 überholt). Prüfe alle Wege dahin
   (Gesamtabgleich, Einzelabgleich in der Objektakte, Feldvergleich `GeoShopImportVergleich`).

Prüfe ausserdem: Im GeoShop-Feldvergleich sind abweichende Werte zwar abgewählt, lassen
sich aber von Hand anwählen. Darf man so einen Kanalfirmen-Wert überschreiben? Melde es als
Frage an Pascal, nicht als Fehler. Und: Ergänzen QGIS-Nachfüllen und Katasterkennungen
wirklich nur leere Felder?

Prüfe ausserdem beim Senden: Welche Werte gehen heute ins WebGIS (nur Handeingaben?
auch Protokoll- oder Kanalfirmenwerte?), und passt das zu den Regeln?

### 5.3 GlobalID und Objektzuordnung
- Wird die GlobalID nur bei genau einem Treffer mit exakt gleichem Namen gespeichert? Was
  passiert bei doppelten Namen im WebGIS (in GEONIS tragen tausende Haltungsnamen mehr als
  ein Objekt), Gegenrichtung („A-B" gegen „B-A"), Leerzeichen, führenden Nullen,
  Präfixen wie `07.`?
- Wird eine gespeicherte GlobalID irgendwo überschrieben, gelöscht oder durch eine andere
  Kennung ersetzt (Import, GeoShop-Abgleich, Umbenennen, Kopieren, Laden alter Projekte)?
- Laufen nach dem Speichern wirklich **alle** Wege (Vorschau, Frischlesen vor dem Schreiben,
  Nachkontrolle, Sanierungsmassnahmen, erneutes Holen) nur über die GlobalID?
- Was passiert, wenn eine Haltung in SewerStudio umbenannt wird, nachdem sie ihre GlobalID
  bekommen hat?
- Die 10 gesperrten Haltungen aus Zone 1.15 (u. a. `81156-81157`, `81157-81162`,
  `80671-80658`, `80452-80397`, `638880-81157`): Prüfe gegen die GEONIS-Kopie, ob die
  Sperre richtig ist und ob der gemeldete Grund stimmt.

### 5.4 Schreibschutz beim Senden
- Kann irgendein Weg ein Feld im WebGIS leeren (`null`, leerer Text, fehlender Schlüssel,
  leerer Handwert, abgewählte Auswahl)?
- Werden immer nur die geänderten Komponenten gesendet, und stimmt das Format
  (`{value, refId, missingValue}`, Auswahlwert als Schlüssel-Text)?
- Konfliktschutz: Vergleicht er Schlüssel gegen Schlüssel? Erkennt er fremde Änderungen
  seit der Vorschau? Schreibt „Jetzt schreiben" nur den bestätigten Plan?
- Geht die Haltungslänge oder Eigentum/Betreiber in irgendeinem Plan, einer Karte oder
  einem Payload hinaus? Suche auch nach indirekten Wegen (refIds, Objektakte, Sammelfelder).
- Seit 23.09.2026 abends sperrt `WebGisGeschuetzteFelder` (in `WebGisExportUseCase.SchreibeEineAsync`
  und `GeonisWebGisClient.SchreibeAsync`) die Schutzfelder aus Regel 8a und alles, was nicht auf der
  Freigabeliste steht. Prüfe: Stimmen die refIds (aus der Inventur v2, nicht live geprüft)? Fehlt ein
  Schutzfeld (z.B. «Länge effektiv» `6cd4e6f1…`, «Erstellt/Geändert am/von»)? Kann ein Weg daran
  vorbei (Sanierungsmassnahmen über `ErstelleSanierungAsync`, Kopfdaten im `saveData`-Payload)?
- Eindeutigkeit (Regel 8b): `SperreDoppelteZuordnungen` sperrt doppelte GlobalIDs im Export. Das
  Holen speichert die GlobalID aber noch ohne diese Prüfung — kann es dieselbe GlobalID an zwei
  Datensätze schreiben?
- Änderungsdatum (Regel 8c): `WebGisExportPosition.GelesenerStand` vergleicht ALLE Maskenfelder.
  Gibt es Felder, die sich ohne Bearbeitung zwischen zwei Lesungen ändern (dann sperrte alles)?
  Deckt der Vergleich auch Änderungen ab, die nicht in den Komponenten stehen (Geometrie)?
- Bemerkung: Ist das Zusammenführen bei wiederholten Läufen stabil, oder wächst der Text
  bei jedem Lauf?
- Gilt eine Antwort des Servers nur mit echtem Erfolgsnachweis als geschrieben?
- Was passiert bei abgelaufener Sitzung, Netzfehler oder Abbruch mitten im Lauf? Bleibt
  ein halber Stand ohne Meldung zurück?

### 5.5 Holen
- Werden nur erlaubte Felder gefüllt oder ersetzt (siehe 5.2)? Bleibt „bewusst leer" leer?
- Prüft „Übernehmen" je Feld nochmals, ob der Wert seit der Vorschau unverändert ist?
- Wird jeder geholte Wert als WebGIS-Begriff gespeichert, zeichengenau?
- **Bekannte offene Stellen, bitte bewerten:** Das Holen schreibt beim Material heute
  SewerStudio-Begriffe (z. B. „Fertigbetonelement", „Ortsbeton") statt der WebGIS-Schreibweise.
  Die Funktion hierarchisch steht als „PAA.Sammelkanal", im WebGIS als „Sammelkanal".
  „Liegenschaftsentwässerung" passt am Schacht zu PAA und SAA und wird deshalb nicht
  übernommen. Schlage für Material und Funktion hierarchisch je eine vollständige
  Zuordnungstabelle vor (bisheriger Begriff → WebGIS-Begriff, mit Beleg). **Entscheide nichts
  davon selbst** — Pascal bestätigt die Tabellen.
- Sanierungsmassnahmen aus dem WebGIS: richtige Akte, keine Doppel, kein geratenes Verfahren?

### 5.6 Anmeldung und Sitzung
- Werden Anmeldedaten irgendwo gespeichert oder geloggt? Bleibt die Sitzung nach „Abmelden"
  bestehen? Wird der Server-Host geprüft, bevor Sitzungsdaten gesendet werden?
- Wird ein Sitzungsfehler immer als solcher erkannt (auch eine ADFS-Anmeldeseite statt JSON)?

### 5.7 Oberfläche und Bedienung
- Sind gesperrte und nicht zugeordnete Objekte und Werte in beiden Fenstern (Holen und
  Prüfen/Schreiben) klar sichtbar?
- Kann während eines Schreiblaufs das Projekt gewechselt, gespeichert oder geschlossen
  werden (es gibt keinen `IShellOperationGuard` für WebGIS)?
- Stimmen die Zahlen in Kopf, Kacheln, Bericht und Log mit dem tatsächlich Geschriebenen?

### 5.8 Tests
- Baue die Lösung und führe alle WebGIS-Tests aus. Nenne Anzahl grün und rot.
- Im UI-Testprojekt sind schon vorher rund 16 Tests rot (u. a.
  `DesignAuditPlayerCodingSidePanelTests`, `ExportPageViewModelDependencyTests`,
  `ArchitectureDriftRatchet`, `SilentCatchGuard`, `MaintainabilityFitness`). Melde sie nicht
  als neue Befunde, prüfe aber, ob einer davon WebGIS betrifft.
- Welche Regeln aus Abschnitt 2 hat **kein** Test abgedeckt? Schreibe für jede Lücke einen
  Beweistest (siehe Abschnitt 3).

## 6. Was du ablieferst

Eine Datei `docs/audits/2026-09-23-webgis-codex/BERICHT.md` mit:

1. **Kurzfassung für Pascal** (höchstens 15 Zeilen, einfaches Deutsch, keine Fachwörter):
   Ist der Weg sicher? Was muss vor dem nächsten Schreiben ins WebGIS behoben werden?
2. **Befunde**, nach Schwere sortiert:
   - **Kritisch** — kann falsche oder leere Werte in die Trigonet-Datenbank schreiben, das
     falsche Objekt treffen oder Kanalfirmen-Werte bzw. Handeingaben überschreiben.
   - **Hoch** — verletzt eine Regel aus Abschnitt 2 ohne direkten Schaden im WebGIS
     (z. B. Begriff ohne WebGIS-Gegenstück wird gespeichert, stiller Datenverlust).
   - **Mittel** — Sperre oder Meldung fehlt oder ist unklar.
   - **Niedrig** — Kosmetik.
   Je Befund: Datei:Zeile, was passiert, Beleg (Test, Bericht, Codezeile), Beispiel mit
   echten Daten, Vorschlag zur Behebung.
3. **Feldtabelle** aus 5.1 (ein Feld pro Zeile, Abweichungen deutlich markiert).
4. **Herkunftstabelle** aus 5.2.
5. **Nur live prüfbar:** Liste der Punkte, die sich ohne Anmeldung nicht klären lassen
   (z. B. Namens-refIds der Maske, ob `saveData` Gruppe und Detail in einem Aufruf annimmt,
   Lesen einer Sanierungsmassnahme über `AWZ_UNTERHALT`, Breite/Höhe der Haltung an einem
   Eiprofil). Je Punkt: genaue Schritte, wie Pascal es im Browser prüfen kann, **ohne**
   etwas zu verändern.
6. **Testergebnis** (Befehl, Anzahl grün/rot, neue Beweistests mit Erwartung).
7. **Vorschläge für Zuordnungstabellen** Material und Funktion hierarchisch (siehe 5.5),
   klar als „zur Bestätigung durch Pascal" gekennzeichnet.

Schreibe den Bericht auf Deutsch. Wo du etwas nicht belegen konntest, schreibe
„nicht belegt" statt einer Vermutung.
