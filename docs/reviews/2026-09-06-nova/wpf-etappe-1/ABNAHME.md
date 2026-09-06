# Abnahme Nova-Etappe 1 (WPF): Leiste und Haltungsseite

Stand: 2026-09-06, Worktree `C:\Sewer-Studio_KI_4.5-nova`, Branch `feature/nova-etappe-1`
(abgezweigt von `feature/eval-pruefsatz-review`, Commit `69fd0a671`).
Plan: `docs/superpowers/plans/2026-09-06-nova-wpf-etappe-1.md`.
Quelle: Prototyp `docs/reviews/2026-09-06-nova/optimiert/v2/SewerStudio-Nova-Optimiert-v2.html`.

## Commits der Etappe

| Task | Commit | Inhalt |
|---|---|---|
| 1 | `24c0132b5` | Zustandsklassen-Marken: Textfarbe je Klasse mit mindestens 4,5:1 Kontrast |
| 2 | `5742d02ba` | Theme: KI-Farbtoken in Hell und Dunkel mit Kontrastwaechter |
| 3 | `dc1305b0d` | Leiste: Navigation in die Gruppen Projekt, Daten, Bewertung, System |
| 4 | `41b770207` | Leiste: Systemmonitor als Aufklapper (seit W02: „Systemleistung"); Schriftskala gilt auch fuer Stil-Setter |
| 5 | `09e268528` | Haltungen: Werkzeugleiste mit einer Hauptaktion, Video pruefen und Weitere Aktionen |
| 6 | `fbcfb4552` | Haltungen: Spaltenansichten Kompakt, Stammdaten, Bewertung, Sanierung, Kosten, Alle |
| 7 | `88b772dfe` | Haltungen: Liste, Uebersicht rechts und Eingabefelder unten mit gespeicherten Trennlinien |
| 8 | (dieser Commit) | Doku: CLAUDE.md-Abschnitt „Nova-Etappe 1" und dieses Protokoll |

## Pruefpunkte

Ergebnis: **bestanden** / **fehlgeschlagen** / **offen** (manuell, noch nicht geprueft).

| Nr. | Pruefpunkt | Ergebnis | Beleg |
|---|---|---|---|
| A1 | `dotnet build AuswertungPro.sln` ohne Fehler | bestanden | 0 Fehler, 1 Warnung (xUnit2013 in `DesignAuditCommandReachabilityTests`, in Task 8 behoben) |
| A2 | Alle Tests in `tests/AuswertungPro.Next.UI.Tests` gruen | bestanden | 6383 bestanden, 3 uebersprungen, 0 Fehler (Abschnitt „Testlauf") |
| A3 | Zustandsklassen-Tinte: jede Klasse 0..4 erreicht 4,5:1 auf ihrer Hintergrundfarbe | bestanden | `ZustandsklasseInkPolicyTests` |
| A4 | KI-Token in beiden Themes, Kontrast auf Karte und KI-Flaeche | bestanden | `DesignAuditContrastTests.Ki_text_reaches_normal_text_contrast_on_card_and_ki_subtle` |
| A5 | Leiste in vier Gruppen, Reihenfolge stabil, jeder Eintrag einer Gruppe zugeordnet | bestanden | `ShellNavigationGroupsTests` |
| A6 | Schriftskala auch in `Setter Property="FontSize"` (keine festen Zahlen unter 11) | bestanden | `DesignAuditSchriftskalaTests` (erweitert) |
| A7 | Systemmonitor als Aufklapper mit Pulspunkt, zugeklappt beim Start; Kopf „Systemleistung", bei gesperrten Sensoren „Sensoren gesperrt" und ruhender Punkt | bestanden | `MainWindow.xaml` (Expander `IsExpanded="False"`, Style-Trigger auf `Monitor.IsSensorBlocked`), `DesignAuditThemeResourceTests`; Nachpruefung W02 |
| A8 | Haltungen-Werkzeugleiste: genau eine Hauptaktion, `Video pruefen` mit `PlayVideoCommand`, alle bisherigen Befehle ueber `Weitere Aktionen` erreichbar | bestanden | `DesignAuditCommandReachabilityTests` (+2), `DataPageToolbarLayoutTests`, `XamlActionWiringGuardTests` |
| A9 | Spaltenansichten: sechs Ansichten, Haltungsname immer vorn, jedes Feld im `FieldCatalog` | bestanden | `DataPageColumnViewCatalogTests` |
| A10 | Spaltenansicht nach Neustart erhalten (`DataPageLayout.ActiveColumnView`) | bestanden | Sichtprobe: Chip „Kompakt" in Phase 1 gewaehlt, nach Neustart `chip_kompakt_aktiv = On` (`nachweise/sichtprobe/bericht-phase2.json`, `p2-01-haltungen-1366x768.png`) |
| A11 | Eingabefelder unten: mindestens sieben Zeilen sichtbar bei 1366 x 768 (Rechenregel) | bestanden | `DataPageWorkspaceLayoutPolicyTests` (556 px Flaeche, 32 px Zeile, 32 px Kopf) |
| A12 | Sieben Zeilen sichtbar bei 1366 x 768 am laufenden Programm, Windows-Skalierung 100 % | bestanden (mit Aenderung) | Sichtprobe: `sichtbare_zeilen = 7` (`p1-01-haltungen-1366x768.png`). Nur weil die Eingabefelder bei Platzmangel automatisch zugeklappt werden; aufgeklappt bleiben 5 Zeilen (Abschnitt „Sichtprobe") |
| A13 | Dasselbe bei Skalierung 125 % und 150 % | offen | Die Windows-Skalierung wurde nicht autonom umgestellt (Systemeinstellung der Arbeitsstation). Bleibt bei Pascal |
| A14 | Uebersicht rechts zeigt Name, Zustandsklasse, Material, DN, Laenge, Inspektion und Primaere Schaeden der gewaehlten Haltung | bestanden | Sichtprobe: `uebersicht_texte = Z2, 10003-10004, Steinzeug, 350, 35.9, 2026, BCD Rohranfang …` (`p1-02-zeile3-uebersicht.png`); ohne Auswahl Hinweis „Keine Haltung gewaehlt" |
| A15 | Eingabefelder in den Themen des Detail-Builders, Feldsuche „Baujahr" laesst nur dieses Feld stehen | bestanden | `HaltungFelderDrawerFilterTests`; Sichtprobe: fuenf Themen (`p1-03b-eingabefelder-auf.png`), Suche „Baujahr" laesst ein Eingabefeld im Thema „Weitere Angaben" (`p1-04-feldsuche-baujahr.png`, `feldsuche_baujahr_editfelder = 1`) |
| A16 | Trennlinien (Uebersicht-Breite, Eingabefelder-Hoehe) merken sich ihre Lage nach Neustart | bestanden (Breite) / bedingt (Hoehe) | Sichtprobe: Uebersicht 312 -> 372 px gezogen, nach Neustart 372 px. Eingabefelder 116 -> 186 px gezogen und gespeichert; nach Neustart bei 1366 x 768 gilt die Hoehenregel (Platzmangel: zugeklappt, geoeffnet 116 px). Die gespeicherte Hoehe wirkt erst, wenn sieben Zeilen daneben Platz haben |
| A17 | Toggle „Haltungsansicht" zeigt weiter die alte Ansicht; Uebersicht, Eingabefelder und Trennlinien verschwinden dabei ohne Luecke | bestanden | Sichtprobe `p1-08-haltungsansicht.png`: alte Liste plus Detail ueber die ganze Breite, `FelderDrawer` nicht mehr im Automationsbaum; zurueck 5 Zeilen |
| A18 | Jede Aktion aus „Weitere Aktionen" einmal ausgeloest | teilweise | Sichtprobe: Menue geoeffnet, 16 Punkte plus Untermenues sichtbar (`p1-05-weitere-aktionen.png`, `weitere_aktionen_punkte`). Die Punkte selbst wurden im kuenstlichen Projekt nicht ausgeloest (QGIS, Kataster, Hydraulik, Dossier brauchen echte Daten); ihre Befehle sind unveraendert (`DesignAuditCommandReachabilityTests`) |
| A19 | `DataPage.xaml.cs` bleibt unter 1000 Zeilen; neue Logik in `DataPage.ColumnViews.cs` und `DataPage.NovaWorkspace.cs` | bestanden | 875 Zeilen; `MaintainabilityFitnessTests` |
| A20 | Kundenoriginale unberuehrt: seit `69fd0a671` nur Pfade unter `src`, `tests`, `docs` und `CLAUDE.md` geaendert | bestanden | `git diff --name-only 69fd0a671..HEAD` liefert keinen anderen Pfad |
| A21 | Die App wurde nur isoliert gestartet | bestanden | Eigener Einstellungsordner (`SEWERSTUDIO_APPDATA_DIR`), kuenstliches Projekt mit 14 Haltungen, Wissensordner ueber `SEWERSTUDIO_KNOWLEDGE_ROOT` auf den echten `C:\KI_BRAIN` (der KI-Spiegel haette sonst eine leere Quelle gesehen; Log: „0 kopiert, 116739 unveraendert, 0 entfernt"). Pascals Einstellungen und Projekte wurden nicht beruehrt |

## Testlauf

Gezielter Lauf nach Task 7 (Waechter, DataPage, Xaml, Maintainability, Toolbar, neue Tests):
583 Tests, 0 Fehler.

Vollstaendiger Lauf `tests/AuswertungPro.Next.UI.Tests` nach dem Solution-Build (`--no-build`):
**6383 bestanden, 3 uebersprungen, 0 Fehler** (6386 gesamt, 1 min 42 s). Die drei Skips sind die
im `UebersprungeneTestsWaechterTests` namentlich erlaubten Stellen.

Gesamte Solution (`dotnet test AuswertungPro.sln --no-build`, alle vier Testprojekte):

| Projekt | bestanden | uebersprungen | Fehler |
|---|---|---|---|
| ProjectModernizer.Tests | 62 | 0 | 0 |
| Infrastructure.Tests | 6008 | 6 | 0 |
| UI.Tests | 6383 | 3 | 0 |
| Pipeline.Tests (erster Lauf) | 2528 | 3 | 17 |
| Pipeline.Tests (nach Helferkorrektur) | 2545 | 3 | 0 |

Die 17 Fehler waren alle `SidecarContractTests` mit `Could not locate repository root`:
Der Helfer `FindRepoRoot` verlangte einen ORDNER `.git`; in einem git-Worktree ist `.git`
eine Datei. Kein Bezug zur Etappe. Der Helfer akzeptiert jetzt beides (eine Zeile im
Testprojekt, kein Produktcode); danach 2545 gruen. Der Nachschlag-Kindprozess
(`NachschlagKontextmenueTests`) ist in diesem Lauf nicht umgefallen.

## Nachpruefung W01 bis W03 (Codex, 6. September 2026)

Quelle: `nachpruefung/NACHPRUEFUNG.md` mit Gegenprobe `nachpruefung/nachweise/gegenprobe.json`.
Alle drei Befunde wurden am Code bestaetigt und behoben.

| Nr. | Befund | Korrektur | Beleg |
|---|---|---|---|
| W01 | Formular zeigte eine Momentaufnahme; eine Formulareingabe konnte eine neuere Tabellenkorrektur ueberschreiben | `DataPageDetailLiveSync` haelt die Felder ueber `HaltungRecord.PropertyChanged` gleich; `RecordDetailItem` kennt `Ausgangswert`, `IsEditing`, `UebernehmeAusDatensatz`, `BeendeBearbeitung`; `RecordDetailsView` setzt den Bearbeitungszustand ueber den Tastaturfokus; der Rueckschreibweg der Fabrik (`IstKonflikt`) behaelt die neuere Korrektur und zeigt die verworfene Eingabe als Hinweis in der Kopfzeile der Eingabefelder | `DataPageFormularTabelleAbgleichTests` (6 Tests, darunter exakt der Ablauf der Gegenprobe) |
| W02 | „Analyse bereit" hing nur an Hardwaresensoren | Kopf „Systemleistung", bei `IsSensorBlocked` „Sensoren gesperrt" mit ruhendem Pulspunkt; Tooltip und zugaenglicher Name angepasst; eine echte KI-Bereitschaft bleibt Etappe 2 | `MainWindow.xaml`, `DesignAuditThemeResourceTests` |
| W03 | Zuklappen liess die Zeile bei 220 px stehen | `HaltungFelderDrawer.IsOpen` mit Ereignis; die Seite setzt die Zeile auf Auto (nur Kopfzeile), blendet die Trennlinie aus und stellt beim Oeffnen Mindesthoehe, Trennlinie und Hoehe wieder her | `DataPageNovaLayoutIsolatedSmokeTests` (Kindprozess mit echten App-Ressourcen: offen >= 120 px, zu < 80 px, wieder offen >= 120 px) |

Die Nachpruefung nannte ausserdem fehlende Laufprotokolle und den fehlenden Release-Build. Beides
liegt jetzt unter `nachweise/` (siehe Nachtrag „Release-Lauf" am Ende).

## Bekannte Grenzen

- Die Sichtpruefungen (A10, A12 bis A18) sind nicht ersetzt, sondern offen. Die Rechenregel fuer
  sieben Zeilen ist geprueft; ob die reale Zeilenhoehe (`GridMinRowHeight`, Standard 38 px) bei
  125 % und 150 % dieselbe Zahl ergibt, zeigt erst das Foto.
- Der Bearbeitungszustand (`IsEditing`) wird ueber den Tastaturfokus gesetzt. Ein Editor, der ohne
  Fokusverlust schreibt (Auswahlliste per Maus), meldet einen Konflikt sofort und zeigt den
  Datensatzwert; das ist gewollt, aber am Programm noch nicht gesehen.
- `RecordDetailsView` zeigt in den Eingabefeldern seinen Kopfbereich mit leerem `Header`; falls das
  am Programm als Leerraum stoert, ist ein Sichtbarkeits-Trigger im Control die richtige Stelle,
  nicht ein zweiter Detail-Renderer.
- Nicht Teil der Etappe: Uebersichtsseite, Schaechte, Player, Training Studio, Chip „Naechste
  Aufgabe", Palettenwechsel Glas/Cockpit.

## Sichtprobe am laufenden Programm (6. September 2026, 1366 x 768, 100 %)

Werkzeug: `nachweise/sichtprobe/werkzeug/` (PowerShell mit UI-Automation, `profil.py` fuer den
isolierten Einstellungsordner, `ProjektBauer` erzeugt das Projekt ueber das echte
`JsonProjectRepository`). Ablauf und Messwerte: `bericht-phase1.json` (hell, frisches Profil) und
`bericht-phase2.json` (dunkel, nach Neustart). Bildschirmfotos `p1-*.png`, `p2-*.png`.

| Messung | Phase 1 | Phase 2 (Neustart, dunkel) |
|---|---|---|
| Fenster | 1366 x 768 | 1366 x 768 |
| Sichtbare Zeilen beim Oeffnen | 7 | 7 |
| Eingabefelder beim Oeffnen | zugeklappt, 50 px | zugeklappt, 50 px |
| Eingabefelder vom Benutzer geoeffnet | 116 px, 5 Zeilen | 116 px, 5 Zeilen |
| Uebersicht-Breite | 312 px, gezogen auf 372 px | 372 px (gespeichert) |
| Chip „Kompakt" | gewaehlt | aktiv (gespeichert) |
| Feldsuche „Baujahr" | 1 Eingabefeld | – |
| „Weitere Aktionen" | 16 Punkte + Untermenues | – |
| Haltungsansicht-Toggle | alte Ansicht, keine Luecke | – |

Drei Erkenntnisse daraus, alle im Code nachgezogen:

1. **Sieben Zeilen gehen bei 1366 x 768 nur mit zugeklappten Eingabefeldern.** Die vier
   Werkzeugzeilen ueber der Liste (KI-Status, Werkzeugleiste, Suche, Spaltenansichten) plus
   Filterzeile lassen rund 400 px fuer Liste und Eingabefelder; sieben Zeilen zu 38 px plus
   Tabellenkopf brauchen 320 px. `DataPageWorkspaceLayoutPolicy.Berechne` liefert deshalb
   `Zugeklappt`, wenn Mindesthoehe plus sieben Zeilen nicht passen; `DataPageNovaWorkspaceController`
   klappt dann automatisch zu, und ein spaeteres Oeffnen durch den Benutzer bleibt bis zum
   naechsten Seitenaufbau bestehen. Auf groesseren Bildschirmen bleiben die Eingabefelder offen.
2. **Die gespeicherte Trennlinienhoehe darf das Zuklappen nicht aufheben.** `SplitterPersistenceBehavior`
   schreibt beim Laden der Trennlinie die gespeicherte Hoehe in die Zeile, auch wenn sie
   ausgeblendet ist; der Controller wendet nach `Loaded` den Zustand erneut an (erster Lauf: 219 px
   Zeile bei zugeklapptem Kopf, 3 sichtbare Zeilen).
3. **Der Detail-Renderer zeigte in jedem Thema seinen eigenen Kopf „Details".** `RecordDetailsView`
   hat jetzt `IsHeaderVisible`; die Eingabefelder blenden ihn aus, die Ueberschrift traegt der Expander.

Ausserdem: Die Uebersicht zeigt ohne Auswahl „Keine Haltung gewaehlt", und die Anbindung der
Arbeitsflaeche liegt in `DataPageNovaWorkspaceController` (Waechter `MaintainabilityFitnessTests`
hatte die Partial-Klasse `DataPage` bei 2011 Zeilen gestoppt).

Offen nach der Sichtprobe:

- Skalierung 125 % und 150 % (A13) — Systemeinstellung, nicht autonom umgestellt.
- Im dunklen Thema ist der aktive `CompactToggleButton` (Chip „Kompakt", Toggle „Eingabefelder")
  kontrastarm, und die gewaehlte Tabellenzeile erscheint hell mit hellem Text (`p2-02-nach-neustart-geoeffnet.png`).
  Beides sind bestehende Theme-Stile, nicht Teil dieser Etappe; gehoert in den Palettenwechsel
  Glas/Cockpit der Etappe 2.
- Die gespeicherte Hoehe der Eingabefelder wird in einem Fenster ohne Platz fuer sieben Zeilen
  nicht angewendet (A16); dort gilt die Mindesthoehe.
- Der Konflikthinweis (W01) wurde nur per Test, nicht am Programm ausgeloest.

## Nachtrag: Release-Lauf (6. September 2026, nach W01 bis W03)

Befehle im Worktree `C:\Sewer-Studio_KI_4.5-nova`, Stand `da273697e`:

```bash
dotnet build AuswertungPro.sln -c Release
dotnet test AuswertungPro.sln -c Release --no-build --logger trx --results-directory <nachweise>/testlauf-release
```

Build: 0 Fehler, 0 Warnungen (`nachweise/release-build.log`).

| Projekt | bestanden | uebersprungen | Fehler |
|---|---|---|---|
| ProjectModernizer.Tests | 62 | 0 | 0 |
| Pipeline.Tests | 2545 | 3 | 0 |
| Infrastructure.Tests | 6008 | 6 | 0 |
| UI.Tests | 6393 | 4 | 0 |

Protokolle: `nachweise/release-test.log` (Konsole) und `nachweise/testlauf-release.zip` (vier TRX-Dateien
mit `SHA256SUMS.txt`). Der vierte Skip im UI-Projekt ist der Elternprozess-Eintrag des neuen
`DataPageNovaLayoutIsolatedSmokeTests` (gleiches Muster wie die bestehenden isolierten WPF-Tests; die
Skip-Stelle liegt im gemeinsamen `IsolatedWpfFactAttribute`, der Skip-Waechter bleibt gruen).

Ein erster Release-Lauf davor hatte genau einen Fehler: `UiArchitectureGuardTests` meldete den
Zugriff auf `App.Services` im Konflikthinweis von W01. Die Meldung wurde in die Kopfzeile der
Eingabefelder verlegt (`da273697e`); der hier dokumentierte Lauf ist die Wiederholung danach.
