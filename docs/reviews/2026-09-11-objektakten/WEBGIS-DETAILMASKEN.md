# Detailmasken im Objektaktenkatalog

Stand 11.09.2026. Ergänzt den Katalog um die Felder hinter den Aufklapplisten.

Die zwanzig Detailmasken an Haltung und Schacht sind gelesen. Die bisherigen **217 Felder
und 78 Auswahlkataloge mit 1158 Einträgen** sind Zeichen für Zeichen unverändert; ergänzt
sind **428 Felder in elf Objektarten** und **59 Auswahlkataloge**. Der Katalog wird nicht
von Hand gepflegt, sondern aus den erfassten Maskendateien erzeugt:

```bash
python tools/ObjektaktenKatalogBauer/bau_katalog.py [--pruefen]
```

Quelle sind die 27 Maskendateien unter `D:\QGIS_V4.2\GeoShop\webgis-masken\`, gelesen aus
den Maskendefinitionen des Servers (`attributeeditor/getLayout`, `getControlValues`) des
GEONIS Attribute Editors. Der Erfassungsbericht liegt dort als `ERFASSUNG.md`.

## Eine Maske, eine Definition

Jede WebGIS-Maske steht genau einmal im Katalog. Die Listen zeigen darauf, statt sie zu
kopieren.

| Objektart | Felder | erscheint als Liste bei |
|---|---:|---|
| `bauwerksteil` | 19 | Bauwerksteile an Haltung und Schacht |
| `unterhalt` | 18 | Unterhaltsmassnahmen an Haltung und Schacht |
| `dichtheitspruefung` | 28 | Dichtheitsprüfungen an Haltung und Schacht |
| `massnahme` | 30 | GEP Massnahmen an Haltung und Schacht |
| `inspektion_haltung` | 56 | Inspektionen an der Haltung |
| `inspektion_schacht` | 40 | Inspektionen am Schacht |
| `einzugsgebiet` | 61 | Einzugsgebiete SW, RW und MW |
| `absperr_drossel` | 46 (17 geerbt) | Absperr-/Drosselorgane |
| `pumpe` | 60 (19 geerbt) | Pumpen |
| `ueberlauf` | 62 (19 geerbt) | Überläufe |
| `mech_vorreinigung` | 8 | Mechanische Vorreinigung |

Dass zwei Listen dieselbe Maske verwenden dürfen, ist nicht angenommen, sondern geprüft:
Bauwerksteile, Unterhaltsmassnahmen, Dichtheitsprüfungen, GEP Massnahmen und die drei
Einzugsgebiete sind an Haltung und Schacht **Feld für Feld identisch**. Der Erzeuger
bricht ab, wenn das einmal nicht mehr zutrifft.

Sieben Listen bekommen **keine** eigene Maske, weil sie auf vorhandene Datensätze zeigen:

- **Einläufe** und **Ausläufe** sind die anschliessende Haltung — 95 von 95 Feldern
  identisch mit der Haltungsmaske, im WebGIS nur lesend. Sie verweisen auf `haltung`.
  Als eigene Objektart gebaut, entstünde eine zweite Haltung neben der echten.
- **Deckel** und **Hauptdeckel** verweisen auf die bestehende Objektart `deckel`. Der
  Bestand ist feiner als die gelesene Maske: er trennt, was das WebGIS als
  `Breite/Länge [mm]` oder `Rechtswert/Hochwert` zusammenfasst.
- **Sanierungsmassnahmen** verweisen auf `sanierung`. Auch hier ist der Bestand reicher:
  Kosten, Dauer, Ausführende Firma, Dokumente und Sachbearbeiter kommen aus SewerStudio
  und haben im WebGIS kein Gegenstück.

Pumpen, Überläufe und Absperr-/Drosselorgane sind Untertypen der Schachttabelle
`AWK_ABWASSERKNOTEN` (`art_bauwerk` 12, 9, 8). Die 17 bis 19 Felder, die sie mit dem
Schacht teilen, stehen in der Maske, sind aber als `erbtVon: schacht` und nur lesend
gekennzeichnet — sonst stünde dieselbe Schachtangabe an zwei Orten und driftete
auseinander.

Die Ereignismasken führen je Zeilenart eine eigene Maske; der Server liefert für
`AWZ_UNTERHALT` sieben Fassungen. Sie sind zu einem Feldsatz zusammengeführt. Zusammen-
geführt wird über die **Beschriftung**, nicht über die technische Kennung: dieselbe Maske
trägt 43 verschiedene `refId` für 18 Beschriftungen, die Kennung ist also je Fassung
verschieden und taugt nicht als Feldidentität. Felder, die nur bei bestimmten Arten
vorkommen, tragen `nurBeiArt`.

## Wie die Beschriftungen entstehen

Das WebGIS fasst mehrere Eingaben unter einer Beschriftung zusammen; nur die erste trägt
den Text. 96 Felder der benötigten Masken haben deshalb keine eigene Beschriftung. Der
Erzeuger löst das über zwei Regeln aus der Quelle selbst — 56 Felder über die geteilte
Beschriftung, 40 über den Text, der in der Maskendefinition ins Einheitenfeld des
Vorgängers gerutscht ist:

| Vorlage | wird zu |
|---|---|
| `Rechtswert/Hochwert` | `Rechtswert`, `Hochwert` |
| `Bez. Schacht (von/bis)` | `Bez. Schacht (von)`, `Bez. Schacht (bis)` |
| `Zahl vorl./endg.` | `Zahl vorl.`, `Zahl endg.` |
| `Lagebest./-genauigkeit` | `Lagebest.`, `Lagebest.-genauigkeit` |
| `Profiltyp/Breite/Höhe` + `[mm]` | `Profiltyp`, `Breite [mm]`, `Höhe [mm]` |
| `Abwasserknoten SW/geplant` u. a. | `Abwasserknoten SW`, `Abwasserknoten SW geplant` |
| `Entfernung [m]/Fixierung` | `Entfernung [m]`, `Fixierung` |
| `Qan ist [l/s]`, `Überlauffracht [kg/Jahr]` | bleiben ganz — ein Schrägstrich **in** der Einheit trennt nicht |

Die vorletzte Zeile ist eine Kollisionsauflösung: SW, RW und MW ergäben sonst dreimal
`Abwasserknoten geplant`. Zwei Felder gleicher Maske dürfen nicht gleich heissen.

Die Stammregel (`Zahl vorl./endg.` → `Zahl endg.`) greift nur, wenn der zweite Teil eine
Abwandlung ist — klein geschrieben oder abgekürzt. Ein eigener Begriff wie `Fixierung`
oder `Hochwert` bleibt für sich. Ein `[mm]`-Feld hinter `Profiltyp/Breite/Höhe` gibt seine
Einheit an **beide** namenlosen Teile weiter (`Breite [mm]`, `Höhe [mm]`).

**Ein Fehler des Erzeugers, den erst die Gegenprüfung fand:** Bis zum 11.09. nachmittags
trennte er auch am Schrägstrich innerhalb einer Einheit. `Qan ist [l/s]` wurde zu
`Qan ist [l`, `Überlauffracht [kg/Jahr]` zu `Überlauffracht [kg` — 13 Felder in Pumpen,
Überläufen, Bauwerksteil und Einzugsgebiet. Getrennt wird jetzt nur noch ausserhalb von
`[..]` und `(..)`.

## Gegenprüfung mit einer zweiten Ableitung

Die Desktop-Sitzung hat aus denselben Maskendateien ein Gesamtlayout erzeugt
(`WEBGIS-LAYOUT-KOMPLETT.md`, 30 Masken) und dabei die namenlosen Felder unabhängig
abgeleitet. Ein Vergleich Feld für Feld über 410 Felder in zehn Objektarten:

- Die Desktop-Sitzung behält den Verbundtext (`Rechtswert/Hochwert`) als Namen des
  ersten Feldes und schreibt das zweite als `Rechtswert/Hochwert — Hochwert`; der
  Erzeuger trennt in `Rechtswert` und `Hochwert`, wie es der Bestand bei Deckel und
  Schacht auch tut. Das ist Darstellung, kein Widerspruch.
- An drei Stellen liefert das Dokument keinen brauchbaren Namen (`->` bei den
  GEP-Kosten, `Bestanden — Kosten` bei der Dichtheitsprüfung); der Erzeuger hat dort
  `Gesamtkosten effektiv [CHF]`, `Umsetzung effektiv [Jahr]` und `Bestanden`.
- `Bauwerksart` (Erzeuger) gegen `Bauwerksart Förderaggregat` / `… Überlauf` /
  `… Absperr-/Drosselorgan` (Dokument): gleiche Sache, das Dokument hängt den Untertyp an.
- **Ein echter Widerspruch bleibt:** Pumpen Feld 18 und Überläufe Feld 23 heissen beim
  Erzeuger `Tiefe [m]`, im Dokument `Geländehöhe`. Beide sind abgeleitet. Die Rohfolge
  spricht für `Tiefe [m]`: In der Schachtmaske derselben Tabelle steht `Geländehöhe`
  **vor** `Sohlenhöhe` und `Tiefe [m]` **danach**, vor `Ebene` — das namenlose Feld steht
  in beiden Masken danach. Bei den Pumpen ist das Feld vor `Sohlenhöhe` ausserdem schon
  `Baujahr`. Belegt ist keine der beiden Lesarten; siehe die Tabelle der zu bestätigenden
  Beschriftungen.

## Drei Beschriftungen zum Bestätigen

An drei Stellen steht in der Maskendefinition kein Text. Sie sind im Erzeuger unter
`ABGELEITETE_BESCHRIFTUNGEN` einzeln samt Begründung eingetragen und werden bei **jedem**
Lauf gemeldet. Eine Nachfrage in der Desktop-Sitzung ergab drei andere Felder
(`Antrieb/Aufstellung`, `Qan ist [l/s]`, `Überlaufmenge [m³]` — Position 30, 51 und 58,
alle längst beschriftet); die Zählung dort meinte „eigene Felder", hier gilt die
`reihenfolge` der Maskendatei. Zum eindeutigen Nachschlagen die Kennungen:

| Maske | Feld | refId | übernommen als | Begründung |
|---|---|---|---|---|
| Pumpen | 7 | `3e342604-5afe-af6b-d701-e232e4fa6add` | `Bauwerksart` | Überläufe #9 und Absperr-/Drosselorgane #7 tragen an derselben Stelle diese Beschriftung; bei Absperrorganen ist es das Norm-Feld `Art` (Blende, Dammbalken, …). |
| Pumpen | 18 | `e5f9405b-30aa-2607-1734-0724741d00ff` | `Tiefe [m]` | Gleiche Tabelle, gleicher Abschnitt „Daten I", gleiche Nachbarn: in der Schachtmaske steht zwischen `Sohlenhöhe` und `Ebene` das Feld `Tiefe [m]`. Die Desktop-Ableitung sagt `Geländehöhe`. |
| Überläufe | 23 | `0048df7b-67cd-3bde-7a2a-569af9944fcd` | `Tiefe [m]` | dieselbe Begründung; Desktop-Ableitung ebenfalls `Geländehöhe` |

## Die Matrixzellen des Einzugsgebiets

Der Hydraulikteil der Einzugsgebietsmaske ist im WebGIS eine **Matrix**: je Zeile
(`Befestigungsgrad [%]`, `Abflussbeiwert [%]`) sechs Zellen mit den Spaltenköpfen
`SW ¦ RW ¦ MW` links und `Geplant` mit `SW ¦ RW ¦ MW` rechts. Nur die erste Zelle trägt die
Zeilenbeschriftung; die Köpfe stehen als Text zwischen den Zellen. Aus der Maskendefinition
allein liess sich dafür kein Name ableiten — acht Zellen wären ohne Namen geblieben.

Die getrennt erfasste Datei `schacht-einzugsgebiete-beschriftungen.json` (19 Felder,
Zuordnung über `refId`) löst das auf: `Befestigungsgrad [%] RW`, `… MW`,
`… Geplant SW` usw. Der Erzeuger liest sie als Beschriftungsdatei der Objektart und meldet
jede Übernahme. Die erste Zelle jeder Zeile heisst ergänzend `… SW`, weil die Datei die
erste Spalte als SW benennt — eingetragen als begründete Ausnahme. Für die neun übrigen
Felder der Datei stimmt sie mit der Ableitung des Erzeugers überein.

## Abhängige Auswahllisten: ein Katalog je Elternwert

Der Plan vom 10.09. hatte eine abhängige Liste nur so erfasst, wie sie beim damals
gewählten Elternwert angezeigt wurde: `haltung.material` (Materialdetail) kannte nur die
14 Betonsorten, `schacht.materialdetail` nur „Unbekannt". Stand die Materialgruppe auf
Kunststoff, war die Liste leer — daher der Hinweis „Diese Unterliste ist noch nicht
belegt" im Programm.

Die Maskendateien enthalten seit dem 11.09. die Unterlisten **aller** Elternwerte
(`jeElternwert`). Daraus erzeugt der Erzeuger je abhängigem Feld einen **zweiten**
Katalog, dessen Einträge den Code ihres Elternwerts tragen (`eltern`):

| Feld | bisheriger Katalog | Katalog je Elternwert |
|---|---|---|
| `haltung.material` | `haltung-C06`, 14 Einträge (Beton) — **unverändert** | `haltung.material-je-eltern`, 43 Einträge in 6 Gruppen |
| `schacht.materialdetail` | `schacht-C07`, 1 Eintrag — **unverändert** | `schacht.materialdetail-je-eltern`, 25 in 6 Gruppen |
| `bauwerksteil.subart` | Vereinigung aller Gruppen | `bauwerksteil.subart-je-eltern` |
| `einzugsgebiet.versickerung` | Vereinigung aller Gruppen | `einzugsgebiet.versickerung-je-eltern` |

Der Bestand bleibt dabei Byte für Byte; das Bestandsfeld erhält nur die zusätzliche
Kennung `katalogIdJeEltern`. Vor dem Schreiben prüft der Erzeuger, dass jeder Elterncode
im Eltern-Katalog existiert (0–5, gleiche Beschriftungen) und dass die Gruppe des damals
belegten Elternwerts exakt dem alten Katalog entspricht — die Beton-Gruppe der Maske ist
Eintrag für Eintrag `haltung-C06`. Planpaket-Prüfung, Erzeuger-Wächter und
`ObjektaktenTests` bleiben deshalb gültig.

Im Programm (`ObjektFeldViewModel.ErlaubteOptionen`) filtert die Anzeige den vollen
Katalog nach dem **Code** der gewählten Elterngruppe, nicht nach ihrem Text —
`ObjektaktenBearbeitung.ElternCode` liest ihn aus dem gespeicherten Originalcode oder
über den Text aus dem Eltern-Katalog. Ohne Elternwert steht „Zuerst «Materialgruppe»
wählen."; bei einer Gruppe ohne Unterliste (Bauwerksteil-Art `Trockenwetterrinne`) „Für
«…» gibt es keine Unterliste." Ein gespeicherter Wert wird nie gelöscht, nur als
„ausserhalb der Auswahl" gekennzeichnet. `Schreibe` nimmt Einträge aus beiden Katalogen
an und merkt sich, aus welchem (`ObjektFeldWert.KatalogId`); ein Eintrag aus einem fremden
Katalog wird abgewiesen.

Wächter: `ObjektaktenUnterlistenTests` (7 — Beton zeigt dieselben 14 wie bisher,
Kunststoff 11 mit Polyethylen, ohne Elternwert leer mit Hinweis, speichern und
wiedererkennen, Gruppenwechsel behält den Wert, fremder Eintrag abgewiesen, Bauwerksteil-
Subart folgt der Art) und
`ObjektaktenTests.Abhaengige_Felder_haben_einen_Katalog_je_Elternwert_und_der_Bestand_bleibt`.

**Noch offen dazu:** Die WebGIS-Schreibweisen (`Polyethylen (PE)`, `Beton, armiert (BA)`)
werden als Bestandswert gespeichert, aber `MaterialVokabular` kennt sie nicht — für den
XTF-Rückweg fehlt die Zuordnung zum Normbegriff. Das wird gemessen, nicht geraten
(siehe Offen).

## Eigene Listeneinträge (programmweit)

Was der WebGIS-Katalog nicht kennt oder falsch führt, kann die Fachperson selbst
ergänzen — **Rechtsklick auf ein Auswahlfeld → „Liste bearbeiten…"**. Das Fenster zeigt
genau diese Liste, bei abhängigen Feldern nur die Gruppe des gerade gewählten Elternwerts:

- **Hinzufügen** — Text, optional ein Code. Der Eintrag erscheint in der Liste als
  „eigener Eintrag". Ohne Code geht er nie in eine XTF; das ist Schutz, keine Einschränkung.
- **Umbenennen** — der Anzeigetext ändert sich, der WebGIS-Code bleibt. Der Originaltext
  stellt zurück.
- **Ausblenden** — der Eintrag verschwindet aus der Liste; ein gespeicherter Wert bleibt
  lesbar. WebGIS-Einträge werden nie entfernt, nur ausgeblendet.

Gespeichert wird programmweit in `%LocalAppData%\SewerStudio\objektakten-listen-ergaenzungen.json`
(`ObjektaktenListenErgaenzungenStore`, atomar, nach dem Muster der zusätzlichen
Sicherungsordner; von der Vollsicherung erfasst). Eine fehlende Datei ist leer, eine
unlesbare bricht ab, statt still ohne Ergänzungen zu laufen. **Der Katalog selbst wird nie
angefasst** — die Ergänzungen liegen beim Aufbau der Liste darüber.

Aufbau: `IObjektaktenListenErgaenzungen` und die reine Regel `ListenErgaenzungRegel`
(Application) · `ListenErgaenzungBearbeitung` bearbeitet **eine** Liste und lässt alle
anderen unangetastet · `ObjektaktenBearbeitung.ErlaubteEintraege` ist jetzt die **eine**
Stelle, die sagt, was ein Auswahlfeld anbietet (Katalog, Elterngruppe, Ergänzungen) — die
Anzeige liest sie, und `Schreibe` prüft gegen dieselbe Regel: ein eigener Eintrag wird nur
angenommen, wenn er wirklich aus der Ergänzungsdatei stammt, eine blosse Markierung reicht
nicht. Der Speicher ist die 163. Registrierung im `ServiceProvider`; ohne ihn (Tests) gibt
es keine Ergänzungen und keinen Fehler.

Wächter: `ListenErgaenzungTests` (6: Regel, Bearbeitung, Datei-Rundreise, kaputte Datei,
Maske mit eigenem Eintrag, erfundener Eintrag abgewiesen) und
`ListenErgaenzungWindowIsolatedSmokeTests` (das Fenster zeichnet alle 14 Betonsorten,
nimmt einen eigenen Eintrag an, weist die Doppelschreibung ab und schreibt die Datei).

## Je Feld

Beschriftung, Bereich, Feldart, Pflicht, Nur-Lesen, Feldlänge, Einheit, die WebGIS-Kennung
und die Zugehörigkeit zu Tabelle und Zeilenart. Auswahlwerte mit gespeichertem Code und
Anzeigetext. Abhängige Listen (Art → Subart am Bauwerksteil, Versickerung am
Einzugsgebiet) werden als Vereinigung aller Elternwerte geführt und behalten den Verweis
auf das Elternfeld; verworfen wird kein Wert.

Der gespeicherte Code kommt aus der Quelle als **Zahl**, das Programm erwartet **Text**.
Der Erzeuger wandelt ihn um — ohne das bricht der Katalog beim Programmstart ab.

## Geprüft

Der Erzeuger führt dieselben Regeln wie `ObjektFeldKatalog.Pruefe()` in C# aus und
zusätzlich: Bestandsfelder und Bestandskataloge byteweise unverändert, keine zwei neuen
Kataloge gleichen Inhalts, kein Katalog ohne Feld, 24 Listenköpfe, jede Liste zeigt auf
eine bekannte Objektart, und jeder Wert hat den Datentyp, den das C#-Modell erwartet.

Die Typprüfung ist nachgerüstet, weil der erste Durchgang genau daran scheiterte: die
Spalten der Listen sind in der Quelle Objekte, nicht Texte — der Python-Lauf sah das
nicht, der echte Programmstart brach ab.

## Die Listen in der Oberfläche

`ObjektUnterliste` liest jetzt auch `zeigtAufObjektart`, `eigeneObjektart`, `nurLesen` und
`mehrerePositionen`. Aus dem Listenbereich ist eine Tabelle geworden: jede Zeile ist ein
Knopf, der den Eintrag in derselben Maske öffnet, in der Deckel und Sanierung schon
bearbeitet werden. Ein eigener Editor war dafür nicht nötig — die Maske entsteht aus der
Objektart.

**Was angelegt werden darf, sagt der Katalog.** `ObjektaktenBearbeitung.Neu` zählt keine
Arten mehr auf, sondern erlaubt genau das, wofür eine Liste des offenen Objekts ein Ziel
nennt, eine eigene Objektart führt und nicht nur lesend ist. Damit bleibt „Deckel gehören
zu einem Schacht" von selbst erhalten: an der Haltung zeigt keine Liste auf `deckel`.
15 Listeneinträge erlauben das Anlegen, 9 sind nur lesend.

`eigeneObjektart` heisst: die Zeile ist ein eigenes Objekt. Falsch ist das nur bei Ein- und
Ausläufen, die auf einen vorhandenen Projektdatensatz zeigen. Deckel und Sanierungen sind
eigene Akten und bleiben anlegbar.

Zeilen kommen aus zwei Quellen und bleiben unterscheidbar: eigene Akten sind bearbeitbar,
Zeilen aus dem Katasterabgleich tragen den Zusatz „aus dem Katasterabgleich" und bleiben
lesend. Spaltenwerte werden über die Beschriftung zugeordnet; `GlobalId` ist der technische
Schlüssel des WebGIS, hat kein Feld und erscheint nicht als Spalte.

**Kein neues Speicherformat.** Eine Unterhaltsmassnahme ist eine `ObjektAkte` mit
`Art = "unterhalt"` und dem Bezug auf ihre Haltung — genau wie ein Deckel. Projektformat 3
bleibt unverändert.

Wächter: `ObjektaktenListenTests` (6) und `ObjektaktenTests.Jede_Aufklappliste_nennt_ihr_Ziel_und_ihre_Rechte`.

## Kompakt: weniger scrollen (11.09.2026, abends)

Pascals erste Sichtprobe im Programm: «alles kompakter, ich scrolle viel zu viel». Gemessen
am Bildschirmfoto frass die Höhe viermal dasselbe — jede Feldzeile 42 bis 56 px (Eingabe
36 px nach Themevorgabe plus Abstände), höchstens zwei Spalten auch auf Full HD, ein fester
620-px-Kasten in der Aufklappliste (innen und aussen scrollen) und Listenzeilen als 32-px-Pillen.
«Daten I» der Haltung mit 29 Feldern brauchte allein über 800 px.

Umgesetzt in `ObjektakteView.xaml`, ohne Änderung an Inhalt, Reihenfolge oder Bedienung:

- **Dichte Zeilen:** Eingaben 28 px (`KompaktText`, `KompaktAuswahl`), Zeilenabstand 1 px —
  eine Zeile rund 30 px. Der Hinweis («Zuerst Materialgruppe wählen», «Originalcode: 133»)
  ist ein Info-Symbol neben dem Feld mit dem Text als Tooltip; als Text daneben hätte er
  das Eingabefeld auf 70 px zusammengedrückt.
- **Spalten nach Breite** (`ObjektakteView.SpaltenFuerBreite`, eine Regel für Liste und
  Fenster): unter 700 px eine, bis 1100 zwei, bis 1500 drei, darüber vier. Beschriftung 130 px.
  «Daten I» steht damit bei 1264 px in 10 Zeilen (~320 px), auf Full HD in 8.
- **Kopf einzeilig:** Objektwahl, Suche und Knöpfe in einer Zeile.
- **Kasten in der Zeile:** `FormularHoeheConverter` nimmt die Höhe der Aufklappliste minus
  150 px (Kopf, Zeile, Register), mindestens 360 px; ohne Mass gilt 620. Ein Scrollbalken.
- **Listen:** Zeilen 24 px (`ListenZeileKnopf`), Abschnitts-Polster 6/2 statt 8/5.

Wächter: `ObjektakteUiTests.Feldspalten_folgen_der_Breite` (8 Grenzwerte),
`Objektakte_in_der_Zeile_nimmt_die_Listenhoehe_minus_Kopf` (6), der Zeichentest bei 1800/1140/680 px
(4/3/1 Spalten, Bild `.tmp/objektakte-1800.png`) und `ObjektakteAufklappTests` (1280 → 3, 800 → 2,
640 → 1 Spalten, Kasten ≥ 360 px).

## Feld markieren und ruhiges Speichern (11.09.2026, spät)

**Feld markieren:** Rechtsklick auf Beschriftung oder Eingabe → «Feld markieren» → Gelb, Orange, Rot,
Grün, Blau oder «Markierung entfernen». Das Eingabefeld trägt die Farbe als Hintergrund (Theme-Tokens
`Markierung<Farbe>Brush`, hell und dunkel, Tinte bleibt normal). Gespeichert wird **programmweit je
Feld** in `AppSettings.ObjektakteFarben` (wie «Sichtbar» und «Meine Übersicht») — man markiert einmal,
was man ausfüllen will, und sieht es in jedem Projekt; das Projekt bleibt unberührt. Ein gemeinsames
Kontextmenü `FeldMenue` trägt auch «Liste bearbeiten…» (nur sichtbar, wo es geht).
Wächter: `ObjektakteUiTests.Feldmarkierung_liegt_programmweit_in_den_Einstellungen_und_nicht_im_Projekt`,
Zeichentest (Hintergrund = Theme-Farbe, Beschriftung und Eingabe teilen das Menü).

**Speichern verschiebt das Bild nicht mehr:** Die Zeile «Gespeichert …» stand im Kopf der Haltungsseite,
erschien beim Speichern und verschwand nach dem Timer — die ganze Liste rutschte rund 40 px hinunter
und zurück. Sie ist jetzt eine Einblendung rechts oben ÜBER der Liste (`Grid.Row=2`, `Panel.ZIndex`),
nimmt keinen Platz im Layout ein.

## Offen

**Zwei Bestandsfelder mit unlesbarer Beschriftung:** `haltung.extranumber1` («-nr.») und
`haltung.extranumber2` («/ null»), Gruppe «Zusatzangaben – Bedeutung noch offen» aus dem
Plan vom 10.09. Sie stehen in «Daten I» der Haltung; in keiner der 27 Maskendateien kommt
`extranumber` vor. Zu klären, ob sie im WebGIS existieren — sonst ausblenden.

**Vokabular — gemessen am 11.09.2026:** Von den 43 Haltungs-Materialeinträgen des
WebGIS findet `MaterialVokabular.NachNorm` **3** (Polyethylen, Polyvinylchlorid,
Polypropylen — die mit bekannter Kurzform); **40 bleiben ohne Normbegriff**. Am Schacht
**23 von 25**. Das gilt ausdrücklich auch für die 14 Betonsorten des Bestands:
`Beton, armiert (BA)` war auch vorher nie zugeordnet — gespeichert wird der Text
unverändert, in eine XTF käme er nicht. `ObjektaktenMaterialVokabularTests` hält beide
Zahlen fest; sie dürfen nur sinken und der Wächter nennt jeden unzugeordneten Namen.
Die fachliche Zuordnung WebGIS-Schreibweise → Normbegriff ist Entscheidung der Fachperson
und gehört zum XTF-Rückweg (Stufe 2).


Geerbte Felder zeigen noch keinen Schachtwert: bei Pumpen, Überläufen und Absperrorganen
sind die 17 bis 19 Felder des Schachts als nur lesend gekennzeichnet, aber leer. Dafür muss
der Erzeuger das Zielfeld eintragen (`erbtVonFeld`). Ebenso offen: Entfernen einer Zeile,
und `nurBeiArt` wird noch nicht ausgewertet.

Und die Zuordnung der neuen Felder zum Katastermodell — welches Feld in die XTF darf und
welches reine Anzeige bleibt. `exportziel` ist bei allen 428 neuen Feldern leer; es wird
nichts exportiert.
