# Nova WPF-Etappe 2b — Abnahme

Stand: 07.09.2026 · Branch `feature/nova-etappe-2b` · Worktree `C:\Sewer-Studio_KI_4.5-nova`
· Stand der Bilder: `c6d271731`

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
| 1 | Zahlen rechtsbündig in der Datenschrift | Zellprobe: DN und Länge tragen `Cascadia Mono`, aber `TextAlignment = Left` | **abweichend, siehe P1** |
| 2 Zeilenstatus-Regel | vier Ampelzustände sichtbar | `haltungen-hell`: „geprüft" (rot bei Z0), „2 offen", „1 offen", „keine Analyse" | bestanden |
| 3 Statusspalten und Chip | Kompakt hat genau 10 Spalten | Messung `spaltenSichtbar: 10`, Reihenfolge Haltungsname, Strasse, Rohrmaterial, DN, Länge, Zustandsklasse, KI, Prüfung, Video, Protokoll | bestanden |
| 3 | Zustandsklassen-Marke statt gefärbter Zelle, „–" gestrichelt | `haltungen-hell` Zeile 6 (leere Klasse) und alle Z0–Z4 | bestanden |
| 3 | Video- und Protokoll-Knopf, sonst „–" | `haltungen-hell`: ▷ bei jeder zweiten, „PDF" bei jeder dritten Zeile | bestanden |
| 3 | einzeilige Zeilen in Kompakt | Messung `zeilenhoehe: 36`, Zellhöhe 38 px in allen zehn Spalten | bestanden |
| 3 | „Primäre Schäden" höchstens drei Zeilen | `haltungen-alle-spalten-hell`: Zeile 1 hat vier Schadenzeilen, sichtbar sind drei; Zellhöhe 57 px | bestanden |
| 4 Kompakt einmalig, Suchpille, Reihenfolge im Menü | Migration „alle" → „kompakt" | `settings.json` nach dem ersten Start; Chip „Kompakt" aktiv | bestanden |
| 4 | Suche als Pille rechts mit Tastenmarke F3 | `haltungen-hell` oben rechts | bestanden |
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

Zusätzlich geprüft und in Ordnung: mindestens zwölf sichtbare Zeilen — **nicht erreicht**,
siehe P2.

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

## 5 Befunde aus den Bildern

Diese Punkte sind an den Bildern und Messdateien dieser Abnahme belegt. Sie wurden hier
**nicht** korrigiert: Task 6 stand während dieser Aufgabe noch im Review, deshalb wurde
keine Datei unter `src/` oder `tests/` angefasst. Über eine Fixwelle entscheidet der
Controller.

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

Vorschlag: `DataPageColumnSetup.Apply` soll `DataPageColumnStyleRules.IstZahlenspalte`
verwenden statt nur `Kosten` zu prüfen. Der Wächter dafür müsste die tatsächliche
Ausrichtung nach dem Spaltenaufbau messen, nicht nur den Style der Fabrik.

### P2 — Nur zehn ganz sichtbare Zeilen statt der geforderten zwölf (mittel)

Bei 1920 × 1080 mit offenen Eingabefeldern zählt der Prüfhost `fullRows: 10` (hell wie
dunkel). Die elfte Zeile ist angeschnitten. Rechnung: Die Tabelle bekommt rund 408 px, eine
Zeile ist 38 px hoch (`RowHeightCompact` 36 plus Rahmen) — für zwölf ganze Zeilen bräuchte
es rund 456 px. Die Schublade der Eingabefelder steht mit 220 px plus Trennlinie und
Kopfzeile. Gegenüber Pascals Ausgangsbild (sechs Zeilen) ist das eine Verbesserung um zwei
Drittel, die Planvorgabe „mindestens 12" ist aber nicht erreicht.

Vorschlag: Starthöhe der Schublade auf rund 170 px, oder die Höhe aus
`DataPageWorkspaceLayoutPolicy` an der gewählten Spaltenansicht ausrichten.

### P3 — Werte werden ohne Auslassungspunkte hart abgeschnitten (mittel)

Die Spalten sind `SizeToHeader` breit; ein längerer Wert wird an der Zellkante gekappt,
ohne „…". Belegt in der Zellprobe: Spalte `STRASSE` ist 72 px breit, der Text
„Gotthardstrasse" 98 px. Im Bild steht „Gotthardstr", „Bahnhofwe", bei den Schächten
„KontrollschachDorfstrasse" — der Leser kann nicht erkennen, ob der Wert zu Ende ist. Der
Prototyp gibt den Spalten Anteile an der Breite. Betroffen sind beide Seiten und beide
Themes.

### P4 — Dunkles Theme: Trennstriche im Tabellenkopf sind zu hell (klein)

In `haltungen-dunkel.png` und `schaechte-dunkel.png` stehen zwischen den Kopfzellen breite,
nahezu weisse senkrechte Balken (der Ziehgriff zum Spaltenverbreitern). Sie sind
kontrastreicher als die Kopftexte selbst und ziehen den Blick. Im hellen Theme sind
dieselben Striche unauffällig grau.

### P5 — Sichtbare Texte ohne echte Umlaute (klein, Altbestand)

- „Lernbasis: 0 Faelle" im Band über der Werkzeugleiste — Quelle
  `Application/DataPage/LearningReadinessPresenter.cs:48,63` („Fälle").
- „Bewertung, Schaeden und Pruefresultate." als Beschreibung der Schacht-Gruppe
  „Zustand und Inspektion" — Quelle
  `UI/DataPage/SchaechteRecordDetailsBuilder.cs:74` („Schäden", „Prüfresultate").

Beides ist kein Regress dieser Etappe: Der Umlaut-Wächter prüft XAML, nicht die
C#-Laufzeittexte (Ledger, Task 5). In den Bildern ist es jetzt belegt.

### P6 — Analysewarnung im Release-Build (klein)

`tests/AuswertungPro.Next.UI.Tests/DesignAuditNovaSchaechteTests.cs(126,9): warning
xUnit2013` — `Assert.Equal()` für eine Sammlungsgrösse; xUnit empfiehlt `Assert.Single`.
Der Build meldet dadurch „1 Warnung", während Etappe 2 mit 0 Warnungen abgeschlossen hat.
Die Datei gehört zu Aufgabe 6 und wird dort gerade geprüft.

---

## 6 Gesamtlauf

```
dotnet build AuswertungPro.sln -c Release            → 0 Fehler, 1 Warnung (siehe P6)
dotnet test  AuswertungPro.sln -c Release --no-build
```

| Testprojekt | bestanden | übersprungen | Fehler |
|---|---|---|---|
| ProjectModernizer.Tests | 62 | 0 | 0 |
| AuswertungPro.Next.Pipeline.Tests | 2562 | 3 | 0 |
| AuswertungPro.Next.Infrastructure.Tests | 6261 | 6 | 0 |
| AuswertungPro.Next.UI.Tests | 6679 | 11 | 0 |

Summe **15 564 bestanden, 20 übersprungen, 0 Fehler** — in einem Durchgang, ohne
Wiederholung. Der Lauf brauchte rund vier Minuten.

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

- Das neue Kopf-Template gilt **programmweit** für alle `DataGrid` (Schriftgrösse `TextXS`
  statt fester 12, Trimming), nicht nur für Haltungen und Schächte.
- Doppelter `GetDisplayHeader`-Aufruf in `SchaechtePage` (~Zeile 217).
- Die Grossschreibung der Schachtköpfe hat keinen eigenen Test (in Aufgabe 6 mitgenommen).
- Die Ressource `Grossbuchstaben` in `App.xaml` hat nach dem Umbau keinen direkten
  Verwender mehr im XAML.
- Sechs `MultiBinding` je Tabellenzeile für die Statusspalten (Leistung unkritisch
  gemessen, aber vorhanden).
- Ampelpunkt 9 px und PDF-Knopfbreite 36 stehen als Zahlen im Code statt als Token.
- Die Ansicht „Bewertung" bleibt auf `Auto`-Zeilenhöhe, obwohl sie keine lange Textspalte
  mehr führt.
- Die Standard-Textspalte trägt keinen eigenen Hinweis (Tooltip) mit dem Volltext.
- `ProtocolDocument.Entries.Add` meldet keine Änderung — nur dokumentiert, nicht geändert.
- Toter Parameter `wertBeimOeffnen` in `ApplySchachtChange`.
- Der Test zu F1 prüft die Zwischenstufe `SelectedItem == null` nicht ausdrücklich.
- `DataPageColumnViewControllerTests` prüft inhaltlich die `KompaktStartRegel`;
  `KompaktStartRegel` ist `public`, obwohl nur die Seiten sie rufen.
- Die `DataPage`-Teildateien liegen bei 1996 von 2000 erlaubten Zeilen.
- `ResolveGroup` im `DataPageRecordDetailsBuilder` hat keinen Produktionsaufrufer.
- Der Umlaut-Wächter erfasst keine C#-Laufzeittexte (siehe P5).

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
