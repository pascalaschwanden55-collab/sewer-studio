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
| A10 | Spaltenansicht nach Neustart erhalten (`DataPageLayout.ActiveColumnView`) | offen | Sichtpruefung am Programm: Ansicht „Kompakt" waehlen, Programm neu starten, Chip bleibt aktiv |
| A11 | Eingabefelder unten: mindestens sieben Zeilen sichtbar bei 1366 x 768 (Rechenregel) | bestanden | `DataPageWorkspaceLayoutPolicyTests` (556 px Flaeche, 32 px Zeile, 32 px Kopf) |
| A12 | Sieben Zeilen sichtbar bei 1366 x 768 am laufenden Programm, Windows-Skalierung 100 % | offen | Bildschirmfoto `nachweise/haltungen-1366x768-100.png` anlegen |
| A13 | Dasselbe bei Skalierung 125 % und 150 % | offen | Bildschirmfotos `nachweise/haltungen-125.png`, `nachweise/haltungen-150.png` |
| A14 | Uebersicht rechts zeigt Name, Zustandsklasse, Material, DN, Laenge, Inspektion und Primaere Schaeden der gewaehlten Haltung | offen (XAML belegt) | `DesignAuditNovaHaltungenTests.Uebersicht_verwendet_die_Zustandsklassen_Tinte`; Sichtpruefung am Programm |
| A15 | Eingabefelder in den Themen des Detail-Builders, Feldsuche „Baujahr" laesst nur dieses Feld stehen | bestanden (Regel) / offen (Sicht) | `HaltungFelderDrawerFilterTests`; Sichtpruefung am Programm |
| A16 | Trennlinien (Uebersicht-Breite, Eingabefelder-Hoehe) merken sich ihre Lage nach Neustart | offen | `SplitterPersistenceBehavior` mit `ViewKey="DataPage"` verdrahtet (`DesignAuditNovaHaltungenTests`); Sichtpruefung: ziehen, neu starten, Lage vergleichen |
| A17 | Toggle „Haltungsansicht" zeigt weiter die alte Ansicht; Uebersicht, Eingabefelder und Trennlinien verschwinden dabei ohne Luecke | offen | `DataPage.NovaWorkspace.cs` `SetNovaWorkspaceVisible`; Sichtpruefung am Programm |
| A18 | Jede Aktion aus „Weitere Aktionen" einmal ausgeloest (Medien suchen, Leere Felder aus QGIS, Katasterkennungen, Strassen, drei Sanierungs-, zwei Hydraulik-Punkte, Dossier, Abdocken, Ansicht-Punkte) | offen | Am Programm mit geoeffnetem Projekt; jeder Punkt traegt weiter denselben Command wie vor der Etappe (`DesignAuditCommandReachabilityTests`) |
| A19 | `DataPage.xaml.cs` bleibt unter 1000 Zeilen; neue Logik in `DataPage.ColumnViews.cs` und `DataPage.NovaWorkspace.cs` | bestanden | 875 Zeilen; `MaintainabilityFitnessTests` |
| A20 | Kundenoriginale unberuehrt: seit `69fd0a671` nur Pfade unter `src`, `tests`, `docs` und `CLAUDE.md` geaendert | bestanden | `git diff --name-only 69fd0a671..HEAD` liefert keinen anderen Pfad |
| A21 | Die App wurde in dieser Etappe nicht autonom gestartet | bestanden (bewusst) | Autosave „bei jeder Aenderung" koennte das geoeffnete Projekt schreiben; Sichtpruefungen A10, A12-A18 bleiben deshalb bei Pascal |

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
| W01 | Formular zeigte eine Momentaufnahme; eine Formulareingabe konnte eine neuere Tabellenkorrektur ueberschreiben | `DataPageDetailLiveSync` haelt die Felder ueber `HaltungRecord.PropertyChanged` gleich; `RecordDetailItem` kennt `Ausgangswert`, `IsEditing`, `UebernehmeAusDatensatz`, `BeendeBearbeitung`; `RecordDetailsView` setzt den Bearbeitungszustand ueber den Tastaturfokus; der Rueckschreibweg der Fabrik (`IstKonflikt`) behaelt die neuere Korrektur und meldet die verworfene Eingabe als Toast | `DataPageFormularTabelleAbgleichTests` (6 Tests, darunter exakt der Ablauf der Gegenprobe) |
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
