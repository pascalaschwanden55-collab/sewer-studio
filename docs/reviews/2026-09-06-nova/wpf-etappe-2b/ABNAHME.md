# Nova WPF-Etappe 2b — Abnahme

Stand: 07.09.2026 · Branch `feature/nova-etappe-2b` · Worktree `C:\Sewer-Studio_KI_4.5-nova`
· Stand der Bilder: `8dc75fd37` (nach der finalen Fixwelle; die erste Fassung dieser
Abnahme zeigte `c6d271731`)

Grundlage: freigegebener Prototyp `docs/reviews/2026-09-06-nova/optimiert/v2/`,
Inventar `docs/reviews/2026-09-06-nova/wpf-etappe-2/PROTOTYP-INVENTAR.md` (4.3, 4.4, 5, 9),
Plan `docs/superpowers/plans/2026-09-07-nova-wpf-etappe-2b.md`,
Ledger `.superpowers/sdd/2026-09-07-nova-wpf-etappe-2b/progress.md`.

## 0 Anlass

Pascals Bild vom 07.09.2026 aus einem echten Projekt (272 Haltungen) zeigte die alte
Tabelle: Spaltenansicht „Alle Spalten 52", eine mehrzeilige Schadenzelle, dadurch nur
**sechs sichtbare Zeilen**, und bei „DN" und „Profil" stand statt eines Wertes der Text
`{DependencyProperty.UnsetValue}`. Diese Etappe bringt Haltungs- und Schachtliste auf den
Prototyp: Kompakt als Startansicht, Statusspalten, Zustandsklassen-Marke, einzeilige
Zeilen, Kopf in Grossbuchstaben, Suche als Pille und die vier Eingabefelder-Themen.

## 0a Stand der Fixwelle (07.09.2026)

Alle in dieser Abnahme gemeldeten Bildbefunde P1-P6 und alle fünf Punkte des
Schlussreviews F1-F5 sind behoben. Bericht:
`.superpowers/sdd/2026-09-07-nova-wpf-etappe-2b/final-fix-report.md`.

| Punkt | Kurz | Stand | Commit |
|---|---|---|---|
| F1 | Protokoll-/Videospalte gegen den Öffner | behoben — die Zelle sagt „hinterlegt", der Gedankenstrich nennt den zweiten Weg | `98ff204da` |
| F2 | KI-Ampel bei bestätigten Haltungen | behoben — neuer Zustand „bestätigt" | `8d0768100` |
| F3 | Falsche Klassennamen in CLAUDE.md | behoben — `SchachtZeilenStatus` → `SchachtProtokollQuelle` | `docs`-Commit dieser Welle |
| F4 | Kopf-Template überschreibt den Kopfstil | behoben — Grösse und Tinte werden vererbt | `615b1804c` |
| F5 | `PDF_Path` in „Dokumente und Medien" | behoben — Knopf UND bearbeitbarer Pfad | `dce78b56e` |
| P1 | Zahlen stehen links | behoben — jede Zahlenspalte rechts, beide Seiten | `4a5b8b230` |
| P2 | Nur zehn ganze Zeilen | behoben — **12 ganze Zeilen** gemessen | `1f6ad89d7` |
| P3 | Harte Abschnitte ohne Auslassungspunkte | behoben — Ellipsis, Volltext im Hinweis, Startbreiten | `4ca0a27b5` |
| P4 | Dunkles Theme: Kopfgriffe zu hell | behoben — eine dezente Trennlinie in `BorderBrush` | `615b1804c` |
| P5 | Umlaute in C#-Laufzeittexten | behoben — mit eigenem Wächter | `91addd66e` |
| P6 | xUnit2013-Warnung | behoben in Aufgabe 6, hier belegt: Release-Build **0 Warnungen** | `7856db81c` |

Zusätzlich aus der Restliste: Zustandsklassen-Marke der Übersicht über den gemeinsamen
Konverter, „Kompakt" nur im Nova-Layout einmalig, „Spalte leeren" auf virtuellen Spalten
wirkungslos (`70e81b727`).

### Runde 2 (Re-Review, 07.09.2026)

| Punkt | Kurz | Stand | Commit |
|---|---|---|---|
| R1 (Major) | Schacht-Rechtsklick schrieb `Nova_Protokoll` in jeden Datensatz | behoben — gemeinsamer Controller plus Sperre am Datensatz | `8d89f60df` |
| R2 | P1/P3 wirkten nur ohne gespeichertes Layout | behoben — einmalige Migration `ZahlenRechtsMigration` | `4f1f79ea1` |
| R3 | Zahlen klebten an der Nachbarspalte | behoben — rechtes Polster 6 px | `4f1f79ea1` |
| R4 | Regler „Zeilenhöhe" wirkte erst beim nächsten Seitenaufbau | behoben | `4f1f79ea1` |

**R1 war ein echter Datenschaden, nicht nur ein Schönheitsfehler.** Die Schachtseite hatte
einen zweiten, eigenen Rechtsklickpfad und kannte deshalb den Schutz aus `70e81b727` nicht:
„Spalte leeren" auf dem Kopf der Protokollspalte schrieb `Nova_Protokoll` mit
Handmarkierung in JEDEN Schachtdatensatz und damit in die gespeicherte Projektdatei. Beide
Seiten laufen jetzt durch `DataPageRightClickController`; zusätzlich weisen `HaltungRecord`
und `SchachtRecord` einen Schlüssel mit dem Präfix `Nova_` auf allen Schreibwegen mit
`ArgumentException` ab (`VirtuelleSpalte`, Domäne). Bewusst kein stilles Ignorieren — ein
verschluckter Schreibversuch sieht für den Aufrufer wie ein Erfolg aus.

**Zur Migration (R2):** Das Bild `haltungen-hell.png` entsteht seit Runde 2 mit einem
vorbelegten ALTEN Spaltenlayout (alles linksbündig, alles 72 px breit) — so, wie es in einer
bestehenden Installation liegt. Deshalb sind DN, Länge und Zustandsklasse dort nur 72 px
breit und ihre Köpfe gekürzt: Eine gespeicherte Breite wird nie verkleinert, und eine
Startbreite gibt es nur für Name, Strasse und Material. Genau das ist der Nachweis — die
Zellprobe zeigt trotz gespeichertem `Left` jetzt `Right`.

**„Bestätigt" neben „nicht analysiert" ist kein Widerspruch.** In der Zeile 10004-10005
steht in der KI-Spalte „bestätigt" und in der Prüfungsspalte „nicht analysiert". Das ist eine
bewusste Paarung: Die KI-Spalte sagt, was die KI gemacht hat (gerechnet, alles bestätigt);
die Prüfungsspalte liest `HaltungPruefstatus` und das ist „offen", solange weder das Feld
offen/abgeschlossen gesetzt ist noch ein offener KI-Befund vorliegt. Der Fall heisst im
Prüfhost genau so: KI gerechnet, alles bestätigt, Arbeitsablauf-Feld leer.

**Die eigentliche Ursache von P2 war eine andere als in der Meldung vermutet.** Nicht die
Höhe der Schublade, sondern die frei einstellbare Mindest-Zeilenhöhe
(`AppSettings.GridMinRowHeight`, Werkseinstellung 38) hat das Token `RowHeightCompact`
vollständig ausgehebelt: Gemessen blieb die Zeile bei 38 px, egal welcher Wert im Token
stand — auch bei 24. In einzeiligen Nova-Ansichten gilt jetzt die kleinere der beiden
Zahlen. Die Schublade steht unverändert bei 220 px.

---

## 1 Wie die Bilder entstanden sind

Alle Bilder stammen aus dem isolierten Prüfhost
`docs/reviews/2026-09-06-nova/wpf-etappe-2/werkzeug/` (eigenes `SEWERSTUDIO_APPDATA_DIR`,
eigener Wissensordner, `Application.OnStartup` unterdrückt — kein Echtzeitspiegel, keine
QGIS-Brücke, kein KI-Start). Full HD 1920 × 1080, DPI 96, Palette Hell und Dunkel.

Das Testprojekt ist rein künstlich und wurde für diese Etappe erweitert:

- **40 Haltungen** mit vier abwechselnden Prüfständen (fachlich geprüft mit bestätigten
  KI-Befunden · KI analysiert mit zwei offenen Befunden · abgeschlossen mit einem noch
  offenen Befund · nicht analysiert),
- **Zustandsklassen 0–4 und leer** (jede sechste Haltung ohne Klasse),
- **mehrzeilige „Primäre Schäden"** mit drei bis vier Zeilen wie aus einem echten Import,
- **Videopfad bei jeder zweiten**, **PDF-Pfad bei jeder dritten** Haltung,
- **acht Schächte** mit Form, beiden Innenmassen (rund 600/600, oval 1100/900), einer ohne
  Zustandsklasse, zwei Dritteln mit PDF.

Die Migration auf „Kompakt" ist belegt, nicht angenommen: Das Prüfprofil wurde vor dem
ersten Start gelöscht, es startete also mit `DataPageLayout.ActiveColumnView = "alle"` und
`NovaKompaktEinmalGesetzt = false` (die Werkseinstellung von `DataPageLayoutSettings`).
Nach dem ersten Start steht in `settings.json` `"ActiveColumnView": "kompakt"` und
`"NovaKompaktEinmalGesetzt": true`; im Bild ist der Chip „Kompakt" aktiv.

Neben jedem Bild liegt eine Messdatei
(`.tmp/nova-etappe2/bedienung/messung-<Seite>[-<Variante>]-<Theme>.json`) mit sichtbaren
Spalten, Zeilenhöhe, Datensatzzahl, ganz sichtbaren Zeilen und einer Zellprobe der ersten
Zeile (Zellbreite, Texthöhe, Textausrichtung, Schriftfamilie).

---

## 2 Bildnachweise

| Datei | Was sie zeigt | Messdatei |
|---|---|---|
| `bilder/haltungen-hell.png` | Haltungen, Kompakt, erste Zeile gewählt, Eingabefelder offen | `messung-Haltungen-Light.json` |
| `bilder/haltungen-dunkel.png` | dasselbe im dunklen Theme | `messung-Haltungen-Dark.json` |
| `bilder/schaechte-hell.png` | Schächte, Kompakt, erste Zeile gewählt | `messung-Schaechte-Light.json` |
| `bilder/schaechte-dunkel.png` | dasselbe im dunklen Theme | `messung-Schaechte-Dark.json` |
| `bilder/haltungen-alle-spalten-hell.png` | Ansicht „Alle Spalten" mit der Drei-Zeilen-Grenze | `messung-Haltungen-alle-Light.json` |
| `bilder/haltungen-ohne-auswahl-hell.png` | keine Zeile gewählt: Übersicht nur im Leerzustand | `messung-Haltungen-ohneauswahl-Light.json` |

---

## 3 Prüfpunkte je Aufgabe

| Aufgabe | Prüfpunkt | Nachweis | Ergebnis |
|---|---|---|---|
| 1 Kopf gross, Zahlen rechts, NR nur in „Alle Spalten" | Kopfzeile in Grossbuchstaben | `haltungen-hell`, `schaechte-hell`: `HALTUNGSNAME (ID)`, `SCHACHTNUMMER` … | bestanden |
| 1 | NR nur in „Alle Spalten" | `haltungen-hell` (kein NR) gegen `haltungen-alle-spalten-hell` (Spalte `NR.` links) | bestanden |
| 1 | Zahlen rechtsbündig in der Datenschrift | Zellprobe nach der Fixwelle: DN und Länge `Cascadia Mono`, `ausrichtung: Right`, `waagrecht: Right` | bestanden (war P1) |
| 2 Zeilenstatus-Regel | alle fünf Ampelzustände sichtbar | `haltungen-hell`: „geprüft" (rot bei Z0), „2 offen", „1 offen", „bestätigt" (grün), „keine Analyse" | bestanden (F2) |
| 3 Statusspalten und Chip | Kompakt hat genau 10 Spalten | Messung `spaltenSichtbar: 10`, Reihenfolge Haltungsname, Strasse, Rohrmaterial, DN, Länge, Zustandsklasse, KI, Prüfung, Video, Protokoll | bestanden |
| 3 | Zustandsklassen-Marke statt gefärbter Zelle, „–" gestrichelt | `haltungen-hell` Zeile 6 (leere Klasse) und alle Z0–Z4 | bestanden |
| 3 | Video- und Protokoll-Knopf, sonst „–" | `haltungen-hell`: ▷ bei jeder zweiten, „PDF" bei jeder dritten Zeile | bestanden |
| 3 | einzeilige Zeilen in Kompakt | Messung `zeilenhoehe: 34`, tatsächliche Zeilenhöhe 34 px (gewählte Zeile 36) | bestanden |
| 3 | „Primäre Schäden" höchstens drei Zeilen | `haltungen-alle-spalten-hell`: Zeile 1 hat vier Schadenzeilen, sichtbar sind drei; Zeilenhöhe 39 px, 10 ganze Zeilen (vorher 7) | bestanden |
| 4 Kompakt einmalig, Suchpille, Reihenfolge im Menü | Migration „alle" → „kompakt" | `settings.json` nach dem ersten Start; Chip „Kompakt" aktiv | bestanden |
| 4 | Suche als Pille rechts mit Tastenmarke F3 | `haltungen-hell`: in der Werkzeugleiste rechts, neben „Weitere Aktionen" (Fixwelle P2) | bestanden |
| 4 | Schacht-Suchpille ohne F3 | `schaechte-hell` oben rechts | bestanden |
| 4 | keine Zeile „Verschieben auf Pos." mehr | `haltungen-hell` (Werkzeugleiste: Speichern, Neu, Löschen, Video prüfen, Weitere Aktionen) | bestanden |
| 4 | Filterzeile bleibt, direkt unter den Spaltenchips | `haltungen-hell`: „Filter: ZK 0 1 2 3 4 · mit Video · mit Schäden · 40 Haltungen" | bestanden |
| 5 Eingabefelder in vier Themen | Stammdaten 17, Bewertung 9, Sanierung 11, Kosten und Bemerkungen 3 | `haltungen-hell`, `haltungen-dunkel` (Zähler an den Themenköpfen) | bestanden |
| 5 | „Weitere Angaben" zugeklappt | `haltungen-hell`: „› Weitere Angaben 15" ohne Karte | bestanden |
| 6 Übersicht ohne Fehltext | mit Auswahl echte Werte, kein `{…}` | `haltungen-hell`: „DN / Profil 200 · Kreisprofil", „Länge 24.6 m", „Prüfung fachlich geprüft" | bestanden |
| 6 | ohne Auswahl nur Leerzustand | `haltungen-ohne-auswahl-hell`: „Keine Haltung gewählt. Links eine Zeile wählen.", kein Rohrring, keine Fakten | bestanden |
| 6 | Schacht-Kompakt mit 9 Spalten, Chip und PDF-Knopf | Messung `spaltenSichtbar: 9`; `schaechte-hell` Spalte `PROTOKOLL` mit „PDF" bzw. „–" | bestanden |
| 6 | Schachtansicht rechts | `schaechte-hell`: 1100 × 900, „Protokoll (PDF)", Hinweis „Am Schacht wird die Zustandsklasse nie berechnet." | bestanden |
| 7 Abnahme | Prüfhost mit realistischen Daten, sechs Bilder, Release-Gesamtlauf, CLAUDE.md | diese Datei, `bilder/`, Abschnitt 6 | bestanden |

Nach der Fixwelle zusätzlich geprüft:

| Prüfpunkt | Nachweis | Ergebnis |
|---|---|---|
| mindestens zwölf ganz sichtbare Zeilen | `messung-Haltungen-Light.json` / `-Dark.json`: `fullRows: 12` | bestanden |
| lange Werte mit Auslassungspunkten | `haltungen-hell` „KI analysiert, Prüfung of…", `schaechte-hell` „Kontrollscha…" | bestanden |
| Name/Strasse/Material breit genug für den Bestand | `haltungen-hell`: „Gotthardstrasse" und „Polyvinylchlorid" stehen ganz da | bestanden |
| dunkle Kopfgriffe dezent | `haltungen-dunkel`, `schaechte-dunkel`: keine hellen Balken mehr im Kopf | bestanden |
| Umlaute in C#-Texten | `haltungen-hell` „Lernbasis: 0 Fälle"; `schaechte-hell` „Bewertung, Schäden und Prüfresultate." | bestanden |

---

## 4 Rulings aus dem Ledger (gelten als abgenommen, nicht als Fehler)

- **Die Filterzeile bleibt.** Entscheid Pascal vom 07.09.: Der Prototyp kennt sie nicht,
  die Fachfunktion (ZK 0–4, mit Video, mit Schäden) bleibt trotzdem — sie steht jetzt
  direkt unter den Spaltenchips.
- **Verschieben auf Position und Gehe zu Zeile** wandern aus der eigenen Zeile in ein
  Popup unter „Weitere Aktionen → Reihenfolge" — für **beide** Layouts gleich. Der
  Prototyp hat beides nicht; die Fachfunktion durfte nicht verschwinden.
- **Die Prototyp-Feldliste ist eine Mindestliste, keine Ausschlussliste.** Deshalb
  17/9/11/3 statt 14/9/10/3: `Schacht_oben` und `Schacht_unten` bleiben in den Stammdaten
  (der Haltungsname hängt an den beiden Schächten), das Gefälle ebenso (CLAUDE.md führt es
  ausdrücklich als Stammdaten-Eingabe), `Renovierung_Inliner_Stk` bleibt in der Sanierung.
- **Die Drei-Zeilen-Grenze gilt nur im Nova-Layout.** Die alte Haltungsansicht bleibt
  unverändert; der Volltext steht dort wie bisher vollständig in der Zelle. Im Nova-Layout
  zeigt der Hinweis den Volltext samt Herkunftszeile.
- **„Kompakt" der alten Ansicht behält den Videopfad `Link`.** Dort gibt es die vier
  virtuellen Statusspalten nicht; ohne diese Regel wäre die Videoangabe ersatzlos weg.
- **Virtuelle Spalten sind keine Felder.** `Nova_KI`, `Nova_Pruefung`, `Nova_Video`,
  `Nova_Protokoll` stehen in keinem Export und werden nie als Feld im gespeicherten
  Spaltenlayout abgelegt.

---

### Rulings der finalen Fixwelle (07.09.2026)

- **Keine Dateiprüfung je Zeile.** Die Spalten Video und Protokoll sagen, ob ein Pfad
  HINTERLEGT ist — nicht, ob die Datei noch da ist. Bei tausenden Zeilen wäre das je Bild
  ein Ordnerlauf. Der Gedankenstrich nennt deshalb ausdrücklich den zweiten Weg
  („das Kontextmenü sucht im Projekt", „Video prüfen sucht im Ordner").
- **`PDF_Path` bleibt in „Dokumente und Medien".** Dort geht es um genau diese Dateien;
  ein falscher Pfad muss ohne Wechsel nach „Alle Spalten" zu korrigieren sein. In
  „Kompakt" bleibt nur der Knopf.
- **Grossschreibung nur in den Nova-Tabellen.** Das Kopf-Template gilt programmweit, die
  Umwandlung in Grossbuchstaben passiert aber beim Erzeugen der Spalte — also nur bei
  Haltungen und Schächten.
- **Die Suchpille steht in der Werkzeugleiste**, rechts angedockt, wie im Prototyp. Ihre
  eigene Zeile entfällt.
- **Startbreite, nicht Mindestbreite.** Name 150, Strasse 120, Material 100 sind
  Startwerte; der Benutzer kann jede Spalte weiterhin beliebig schmal ziehen, und ein
  gespeichertes Layout gewinnt.

---

## 5 Befunde aus den Bildern

Diese Punkte sind an den Bildern und Messdateien der ERSTEN Fassung dieser Abnahme belegt
(Stand `c6d271731`). Sie sind inzwischen alle behoben — der Text unten bleibt als Beleg
der Ursache stehen, jeder Befund endet mit dem, was daraus geworden ist. Die Bilder in
`bilder/` zeigen den Stand NACH der Fixwelle.

### P1 — Zahlen stehen links statt rechts (mittel, Abweichung von Aufgabe 1)

Die Spaltenfabrik setzt für Zahlenspalten `TextAlignment.Right`
(`DataGridStandardTextColumnFactory.cs:34`), und die Monoschrift kommt an. Die Ausrichtung
wird danach aber wieder überschrieben: `DataPage.xaml.cs` ruft für jede neue Spalte
`_columnAlignmentToolbar.SetAlignment(..., spalte.Setup.DefaultHorizontalAlignment, …)`,
und `DataPageColumnSetup.Apply` liefert `HorizontalAlignment.Right` **nur** für das Feld
`Kosten` — für alle anderen `Left`. `DataGridColumnLayoutController.ApplyTextColumnAlignment`
baut daraus einen abgeleiteten Style mit `HorizontalAlignment=Left`, `TextAlignment=Left`
und schlägt damit die Regel der Fabrik.

Beleg (Zellprobe `messung-Haltungen-Light.json`, erste Zeile):
`LICHTE HÖHE / DN MM` — Zellbreite 139, Textbreite 24, `ausrichtung: Left`,
`waagrecht: Left`, `schrift: Cascadia Mono`. Im Bild stehen 200/250/300 … linksbündig.

**Behoben (`4a5b8b230`):** `DataPageColumnSetup.Apply` fragt jetzt
`DataPageColumnStyleRules.IstZahlenspalte`; die Schachtliste vergleicht dabei gefaltet
(`SchachtFeldnamen.Falte`), damit auch beide Innenmasse erfasst sind. Eine gespeicherte
Nutzerausrichtung behält Vorrang, weil sie erst danach aus dem Layout gelesen wird.
Zellprobe nachher: `LICHTE HÖHE / DN MM` — Zellbreite 158, `ausrichtung: Right`,
`waagrecht: Right`, Schrift weiterhin `Cascadia Mono`.

### P2 — Nur zehn ganz sichtbare Zeilen statt der geforderten zwölf (mittel)

Bei 1920 × 1080 mit offenen Eingabefeldern zählt der Prüfhost `fullRows: 10` (hell wie
dunkel). Die elfte Zeile ist angeschnitten. Rechnung: Die Tabelle bekommt rund 408 px, eine
Zeile ist 38 px hoch (`RowHeightCompact` 36 plus Rahmen) — für zwölf ganze Zeilen bräuchte
es rund 456 px. Die Schublade der Eingabefelder steht mit 220 px plus Trennlinie und
Kopfzeile. Gegenüber Pascals Ausgangsbild (sechs Zeilen) ist das eine Verbesserung um zwei
Drittel, die Planvorgabe „mindestens 12" ist aber nicht erreicht.

**Behoben (`1f6ad89d7`), aber mit einer anderen Ursache als hier vermutet.** Die Rechnung
oben stimmt nicht: `RowHeightCompact` war wirkungslos. Die Tabelle trägt neben der
Zeilenhöhe eine frei einstellbare **Mindesthöhe** (`AppSettings.GridMinRowHeight`,
Werkseinstellung 38, gebunden an `MinRowHeight` des `DataGrid`). Sie ist grösser als die
kompakte Zeilenhöhe und hat sie vollständig geschlagen — eine Gegenprobe mit Token 24
ergab weiterhin 38 px je Zeile. In einzeiligen Nova-Ansichten gilt jetzt die kleinere der
beiden Zahlen (`DataPageZeilenhoehePolicy.Mindesthoehe`, angewendet von
`DataPageZeilenhoehenAnwender`); eine bewusst kleiner eingestellte Mindesthöhe bleibt
erhalten. Dazu kamen: Suchpille in die Werkzeugleiste (Ruling), Filterzeile kompakt
(Chips 24 statt 26 px), Token auf 34, Knöpfe in Statuszellen 24 statt 28 px.

Gemessen nachher bei 1920 × 1080 mit offener Schublade (220 px, unverändert):
**`fullRows: 12`** hell wie dunkel, Viewport 438 px, Zeilenhöhe 34 (gewählte Zeile 36).
In „Alle Spalten" 10 statt vorher 7 ganze Zeilen.

### P3 — Werte werden ohne Auslassungspunkte hart abgeschnitten (mittel)

Die Spalten sind `SizeToHeader` breit; ein längerer Wert wird an der Zellkante gekappt,
ohne „…". Belegt in der Zellprobe: Spalte `STRASSE` ist 72 px breit, der Text
„Gotthardstrasse" 98 px. Im Bild steht „Gotthardstr", „Bahnhofwe", bei den Schächten
„KontrollschachDorfstrasse" — der Leser kann nicht erkennen, ob der Wert zu Ende ist. Der
Prototyp gibt den Spalten Anteile an der Breite. Betroffen sind beide Seiten und beide
Themes.

**Behoben (`4ca0a27b5`):** Standard-Textspalten kürzen im Nova-Layout mit
`TextTrimming=CharacterEllipsis`; die Schachtliste bekommt denselben Setter über
`NovaTextZellenStil`. Jede Nova-Zelle trägt zusätzlich den Volltext oben im Hinweis, die
Herkunftszeile bleibt darunter. Startbreiten aus dem Prototyp (`NovaSpaltenbreiten`):
Name 150, Strasse 120, Material 100 — bewusst als START-, nicht als Mindestbreite, und ein
gespeichertes Spaltenlayout gewinnt.

### P4 — Dunkles Theme: Trennstriche im Tabellenkopf sind zu hell (klein)

In `haltungen-dunkel.png` und `schaechte-dunkel.png` stehen zwischen den Kopfzellen breite,
nahezu weisse senkrechte Balken (der Ziehgriff zum Spaltenverbreitern). Sie sind
kontrastreicher als die Kopftexte selbst und ziehen den Blick. Im hellen Theme sind
dieselben Striche unauffällig grau.

**Behoben (`615b1804c`):** Die Griffe hatten gar keine eigene Vorlage und trugen deshalb
die WPF-Standardoptik. Jetzt zeichnet nur der rechte Griff eine 1 px schmale Trennlinie in
`BorderBrush`; der linke bleibt unsichtbar, sonst stünde an jeder Grenze eine doppelte
Linie. Der Ziehbereich (6 px, Cursor `SizeWE`) bleibt unverändert.

Der neue Wächter ist bewusst als **Kontrastregel** formuliert und nicht als „nicht heller
als die Tinte": Im hellen Theme ist eine Linie heller als die Tinte gerade das
Unauffällige, im dunklen das Auffällige. Geprüft wird deshalb, dass die Linie sich nicht
stärker vom Kopfgrund abhebt als der Kopftext (`DesignAuditContrastTests`).

### P5 — Sichtbare Texte ohne echte Umlaute (klein, Altbestand)

- „Lernbasis: 0 Faelle" im Band über der Werkzeugleiste — Quelle
  `Application/DataPage/LearningReadinessPresenter.cs:48,63` („Fälle").
- „Bewertung, Schaeden und Pruefresultate." als Beschreibung der Schacht-Gruppe
  „Zustand und Inspektion" — Quelle
  `UI/DataPage/SchaechteRecordDetailsBuilder.cs:74` („Schäden", „Prüfresultate").

Beides ist kein Regress dieser Etappe: Der Umlaut-Wächter prüft XAML, nicht die
C#-Laufzeittexte (Ledger, Task 5). In den Bildern ist es jetzt belegt.

**Behoben (`91addd66e`):** Dazu kamen der Ampeltext „Gruen" und „Verknuepfte Dateien,
PDFs und Links.". Neuer Wächter `DesignAuditLaufzeittexteTests` über die drei Quellen
sichtbarer Laufzeittexte. Er sieht nur Zeichenketten MIT Leerzeichen — Feldschlüssel wie
`Gefaelle_Promille` sind Datenschlüssel, keine Beschriftungen, und dürfen ihre Schreibweise
nie ändern. Gegengeprobt: mit „Faelle" wird der Wächter rot.

### P6 — Analysewarnung im Release-Build (klein)

`tests/AuswertungPro.Next.UI.Tests/DesignAuditNovaSchaechteTests.cs(126,9): warning
xUnit2013` — `Assert.Equal()` für eine Sammlungsgrösse; xUnit empfiehlt `Assert.Single`.
Der Build meldet dadurch „1 Warnung", während Etappe 2 mit 0 Warnungen abgeschlossen hat.
Die Datei gehört zu Aufgabe 6 und wird dort gerade geprüft.

**Behoben in `7856db81c` (Aufgabe 6), hier belegt:** `dotnet build AuswertungPro.sln
-c Release` meldet jetzt **0 Warnungen, 0 Fehler**.

---

## 6 Gesamtlauf

```
dotnet build AuswertungPro.sln -c Release            → 0 Fehler, 0 Warnungen
dotnet test  AuswertungPro.sln -c Release --no-build → Exit-Code 0
```

| Testprojekt | bestanden | übersprungen | Fehler |
|---|---|---|---|
| ProjectModernizer.Tests | 62 | 0 | 0 |
| AuswertungPro.Next.Pipeline.Tests | 2562 | 3 | 0 |
| AuswertungPro.Next.Infrastructure.Tests | 6291 | 6 | 0 |
| AuswertungPro.Next.UI.Tests | 6748 | 11 | 0 |

Summe **15 663 bestanden, 20 übersprungen, 0 Fehler** — in einem Durchgang, ohne
Wiederholung. Der Lauf brauchte rund vier Minuten. (Vor der Fixwelle: 15 564 bestanden und
eine Analysewarnung; nach Runde 1: 15 638.)

Die drei bekannten zeitabhängigen Wackler sind in diesem Lauf **nicht** umgefallen:
`SidecarRestartServiceTests.Lifetime_stop_tracked_beendet_nur_den_eigenen_prozess` und
`KatasterHaltungFeldNachschlagTests.Die_Suche_laeuft_nicht_auf_dem_aufrufenden_Thread`
sind grün, `NachschlagKontextmenueTests.Kindprozess_prueft_die_Kontextmenues` hat nicht
gehangen. Es wurde kein Test gezielt wiederholt und keiner weggelassen.

Die 20 übersprungenen Tests sind die bekannten maschinengebundenen Abnahmen (Sidecar,
echtes Video, Live-Dienste geo.ur.ch, VSA-KEK-Archiv) sowie die elf isolierten
WPF-Smoketests, die ihre Arbeit in einem Kindprozess leisten und im Elternprozess als
übersprungen erscheinen — darunter die drei neuen dieser Etappe
(`DataGridColumnHeaderGrossbuchstabenIsolatedSmokeTests`,
`DataPageNovaLayoutIsolatedSmokeTests`, `SchaechteNovaLayoutIsolatedSmokeTests`).

---

## 7 Aufgeschobene Kleinbefunde aus den Aufgaben 1–6

Aus dem Ledger übernommen, bewusst offen gelassen:

- ~~Das neue Kopf-Template gilt **programmweit** für alle `DataGrid`~~ — Ruling: Die
  Grossschreibung bleibt bewusst auf den Nova-Tabellen (Haltungen und Schächte), weil nur
  dort die Köpfe beim Erzeugen umgewandelt werden. Grösse und Tinte erbt die Vorlage seit
  der Fixwelle vom Kopfstil (F4), ein abgeleiteter `ColumnHeaderStyle` kann also wieder
  einfärben.
- Doppelter `GetDisplayHeader`-Aufruf in `SchaechtePage` (~Zeile 217).
- Die Grossschreibung der Schachtköpfe hat keinen eigenen Test (in Aufgabe 6 mitgenommen).
- Die Ressource `Grossbuchstaben` in `App.xaml` hat nach dem Umbau keinen direkten
  Verwender mehr im XAML.
- Sechs `MultiBinding` je Tabellenzeile für die Statusspalten (Leistung unkritisch
  gemessen, aber vorhanden).
- Ampelpunkt 9 px und PDF-Knopfbreite 36 stehen als Zahlen im Code statt als Token.
- Die Ansicht „Bewertung" bleibt auf `Auto`-Zeilenhöhe, obwohl sie keine lange Textspalte
  mehr führt.
- ~~Die Standard-Textspalte trägt keinen eigenen Hinweis (Tooltip) mit dem Volltext.~~
  Erledigt mit P3: Im Nova-Layout trägt jede Zelle den Volltext oben im Hinweis.
- `ProtocolDocument.Entries.Add` meldet keine Änderung — nur dokumentiert, nicht geändert.
- Toter Parameter `wertBeimOeffnen` in `ApplySchachtChange`.
- Der Test zu F1 prüft die Zwischenstufe `SelectedItem == null` nicht ausdrücklich.
- `DataPageColumnViewControllerTests` prüft inhaltlich die `KompaktStartRegel`;
  `KompaktStartRegel` ist `public`, obwohl nur die Seiten sie rufen.
- Die `DataPage`-Teildateien liegen weiterhin dicht an der Grenze (2000 Zeilen). Die
  Fixwelle hat ihre neue Logik deshalb in `DataPageZeilenhoehenAnwender` daneben gelegt —
  der Wächter war beim ersten Versuch mit 2007 Zeilen rot.
- `ResolveGroup` im `DataPageRecordDetailsBuilder` hat keinen Produktionsaufrufer.
- ~~Der Umlaut-Wächter erfasst keine C#-Laufzeittexte (siehe P5).~~ Erledigt mit P5:
  `DesignAuditLaufzeittexteTests`.

---

## 8 Grenzen dieser Abnahme

- **G1 — Kein produktiver Programmstart.** Der Prüfhost unterdrückt
  `Application.OnStartup`: kein Echtzeitspiegel, keine QGIS-Brücke, kein KI-Start, eigenes
  AppData- und Wissensverzeichnis, künstliches Projekt. Ein produktiver Start mit dem
  echten Profil würde bei „Autosave bei jeder Änderung" Kundendaten berühren. Diese
  Abnahme belegt daher **keine** vollständige Programmabnahme.
- **G2 — Keine Skalierungsmessung.** Alle Bilder sind Full HD bei 100 % (DPI 96).
  Windows-Skalierung 125 % und 150 % wurde in dieser Etappe **nicht** gemessen. Bei 150 %
  bleibt weniger Arbeitsfläche; zusammen mit P2 ist das die offene Sichtprüfung durch
  Pascal beim Merge.
- **G3 — Menüs und Popups sind nur durch Tests belegt.** „Weitere Aktionen →
  Reihenfolge → Verschieben auf Position / Gehe zu Zeile" öffnet ein Popup; Popups
  zeichnen in ein eigenes Fenster und erscheinen in keinem `RenderTargetBitmap`. Belegt
  sind sie über `DesignAuditNovaHaltungenTests`, `XamlActionWiringGuardTests` und
  `PopupFocusHelperTests`, nicht im Bild.
- **G4 — Kein echter Bedienweg.** Die Bilder entstehen über ViewModel-Aufrufe
  (`TryOpenProject`, `EnterWorkspaceOn`, Auswahl setzen) und einen ausgelösten
  Chip-Klick. Maus, Tastatur, F3 und Kontextmenüs sind nicht bildlich belegt.
- **G5 — Nur Haltungen und Schächte.** Übersicht, Player, Training Studio und die
  übrigen Seiten sind in dieser Etappe unverändert und nur über ihre Wächtertests
  abgedeckt.
- **G6 — Der Prüfhost malt die Mica-Fläche aus.** `Fluent.Backdrop="Mica"` setzt
  `Window.Background` auf Transparent; diese Fläche zeichnet der Windows-Compositor, den
  ein `RenderTargetBitmap` nicht erfasst. Der Prüfhost setzt vor dem Foto die
  Theme-Fläche ein, sonst wäre jede Kontrastbeurteilung an der Kopfzeile falsch.
