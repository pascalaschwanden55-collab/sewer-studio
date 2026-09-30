# Bericht Tests und Pruefweg (Audit 30.09.2026)

## 1) Kurzfazit
Die Testsuite ist gross (rund 17'900 Tests am 27.09., vier Projekte) und der Pruefweg ist sauber gespiegelt (Hook = CI). Die Wartungslast waechst aber sichtbar bei den Text-/XAML-Waechtern: 288 Testdateien lesen Quelltext oder XAML aus dem Repo (Stand 27.09.: W01), 23 der 77 seit 0c2d5c17b neu angelegten Testdateien gehoeren dazu. Dazu kommen von Hand gepflegte Wort- und Ausnahmelisten (14 gefundene Listen, eine mit ca. 165 Woertern) und ein eigener XAML-Zustandsparser mit 917 Zeilen. Ein Umbenennen oder Verschieben von Texten zieht mehrere Listen nach; der volle lokale Weg dauert ca. 10 Minuten reine Testzeit. Eine schnelle Teilmenge fuer den Alltag fehlt.

## 2) Befundtabelle
| ID | Prio | Titel | Nutzen |
|---|---|---|---|
| T01 | P1 | Text-/XAML-Waechter wachsen weiter (288 Dateien, +23 neu) | Aenderungen an Quelltext/XAML werden weniger oft von Testpflege begleitet |
| T02 | P2 | Handgepflegte Wort-/Ausnahmelisten in 14 Waechtern | Umbenennen/Verschieben zieht weniger Listen nach |
| T03 | P2 | XamlFarbpaarModell (917 Z.) und Doppelungen bei XAML-Einlesen | Eine Stelle fuer "alle XAML-Dateien"; Analysator ist testbar |
| T04 | P2 | Kein schneller Alltagslauf; Hook fuehrt immer alle vier Projekte | Kurze Rueckmeldung beim Arbeiten |
| T05 | P2 | Kindprozess-Tests: teuer, 60-s-Grenze, ApplicationIdle-Falle nur in Prosa | Stabilere, billigere WPF-Smoketests |
| T06 | P3 | Repo-Wurzel-Suche mehrfach kopiert | Weniger Streuung |
| T07 | P3 | Uebersprungene/maschinengebundene Tests (gut geregelt, nur Hinweis) | Bleibt so |

## 3) Befunde im Detail

### T01 (P1) Quelltext-/XAML-Waechter: Umfang und Zuwachs
Beleg (Zaehlung 30.09., grep/Python ueber tests/):
- 288 Testdateien lesen per File.ReadAllText/ReadAllLines/XDocument.Load und beziehen sich auf src/XAML/Repo-Wurzel (411 Dateien nutzen ReadAllText o. ae. insgesamt; 263 davon mit Pfad-/Repo-Bezug per gröberer Regex, 288 mit weiterer Regex — Grössenordnung 260–290).
- 188 Dateien in tests/AuswertungPro.Next.UI.Tests nennen ".xaml", 168 pfadartige "Views"/"Theme"-Bezuege.
- Seit 0c2d5c17b: 77 neue und 437 geaenderte Testdateien; von den 77 neuen lesen 23 Quelltext/XAML (Regex-Zaehlung, obere Naeherung).
- Neue Waechter aus dem Optik-Umbau: 30 Dateien `DesignAudit*Tests.cs` (zusammen 7'831 Zeilen inkl. ThemeHighContrast*), z. B. DesignAuditThemeResourceTests.cs (729 Z.), DesignAuditPlayerCodingSidePanelTests.cs (695 Z.), DesignAuditNovaHaltungenTests.cs (479 Z.).
Beispiele, was ein Entwickler nachziehen muss:
1. Ein Text in einer .cs-Datei der UI mit ae/oe/ue-Wort umbenennen: `DesignAuditLaufzeittexteTests.cs:230` (Aufgabe10c1BereinigteWoerter, ca. 165 Woerter) und `:268` (GeschuetzteGanzeZeichenketten), dazu `DesignAuditLaufzeittexteSchichtenTests.cs:99` (AusgenommeneDateien, 7 Dateinamen) und `:117`.
2. Neues Fenster: `DesignAuditKnopfleistenTests.cs:55` (Ausnahmen nach Regel), `DesignAuditLeerUndLadezustandTests.cs:179` (EmptyStateDateien) und `:31` (ProgressBarAusnahmen), `ThemeHighContrastFarbpaarTests.cs:43` (BekannteAusnahmen), `ExportImportAbschnittsKnopfTests.cs:41`.
3. Datei umbenennen, in der Ausnahmen stehen (z. B. EnhancedVisionPromptBuilder.cs): die Ausnahmen sind Dateinamen-Strings; UebersprungeneTestsWaechterTests.cs:24–48 haelt Pfade der Attribut-Dateien fest (Umbenennen von IsolatedWpfFactAttribute.cs macht ihn rot).
4. `UserErrorEigeneMeldungenSpracheTests.cs:168` MessageEinbettungErlaubt als Schluessel (Datei, Ausschnitt) — bricht bei Textaenderung im Ausschnitt.
Ein Teil der Waechter meldet gut, wenn eine Ausnahme verwaist ist (ThemeHighContrastFarbpaarTests.cs:64–70; UebersprungeneTestsWaechterTests.cs:69–79). Das ist gut, ist aber auch der Grund, warum jede Umbenennung sofort Testpflege erzeugt.
Warum es bremst: Aenderungen an Texten/Layout verlangen Pflege von 2–5 Listen; Fehlalarme (Wortliste trifft Datenschluessel) mussten nachweislich einzeln zurueckgesetzt werden (CLAUDE.md Aufgabe 10c1, "Selbstkorrektur, drei Fehlerklassen").
Empfehlung (klein): Neue Text-/XAML-Waechter nur noch, wenn die Regel nicht in einem typisierten Test pruefbar ist. Bestehende Sprachwaechter auf EINE Datei mit EINER Wort-/Ausnahmenliste (z. B. `Sprachregeln.cs`) zusammenziehen; beide Laufzeittexte-Klassen teilen sich dann Woerter und Geschuetzte.
Abnahme: Ein Umbenennungsfall ("Wort X in Text Y") bearbeitet hoechstens eine Liste; Anzahl Ausnahmelisten in tests/ ≤ 8.

### T02 (P2) Von Hand gepflegte Listen
Belegt (grep `Ausnahmen|AusgenommeneDateien|…`): 14 Listen in 13 Dateien (DropdownExportierbarkeitTests.cs:53, UserErrorEigeneMeldungenSpracheTests.cs:86/168, DesignAuditFeinschliffTests.cs:25, DesignAuditKeinStaticResourceThemeTokenTests.cs:63 (leer), DesignAuditKnopfleistenTests.cs:55, DesignAuditLaufzeittexteSchichtenTests.cs:99/117, DesignAuditLaufzeittexteTests.cs:99/230/268, DesignAuditLeerUndLadezustandTests.cs:31/179, ExportImportAbschnittsKnopfTests.cs:41, ThemeHighContrastFarbpaarTests.cs:43). Doppelt gefuehrt: GeschuetzteGanzeZeichenketten steht fast wortgleich in DesignAuditLaufzeittexteTests.cs:268 (3 Eintraege) und DesignAuditLaufzeittexteSchichtenTests.cs:117 (Liste 10+); Excel-Farbregelwerte "Pruefung bestanden" kommen in beiden vor.
Empfehlung: Doppelte Liste zusammenlegen, jede Liste mit Grund (bereits Standard). Abnahme: kein Wert steht in zwei Listen.

### T03 (P2) Eigene Analysewerkzeuge und Doppelungen
- XamlFarbpaarModell.cs: 917 Zeilen, ein eigenes Zustandsmodell (Stil/Vorlage/Trigger/BasedOn/Rangfolge), nur von ThemeHighContrastFarbpaarTests.cs (368 Z.) genutzt. Es wird durch "sieben dauerhafte Fundweg-Proben" im selben Test geprueft (laut CLAUDE.md), aber es gibt keine eigene XamlFarbpaarModellTests-Datei (grep: nur 2 Dateien verweisen darauf). Erwartete Wartungsfolge: WPF-Erweiterung von Vorlagen erfordert Modellanpassung.
- XAML-Dateiaufzaehlung mehrfach neu gebaut: `XamlDateien()`/`AlleXamlDateien()`/`XamlFiles()` in mindestens 8 Dateien (DesignAuditFeinschliffTests.cs:317, DesignAuditKeinStaticResourceThemeTokenTests.cs:65, DesignAuditLeerUndLadezustandTests.cs:44, ThemeHighContrastCoverageTests.cs:374, ThemeHighContrastFarbpaarTests.cs:364, ThemeRessourcenNamenTests.cs:96, XamlActionWiringGuardTests.cs:171, …). Die Ausschlusslisten (z. B. Video-Fenster, Theme-Ordner) sind pro Kopie eigen — Fehlerquelle bei Ausnahmen.
- XDocument in 35 Dateien; Regex + ReadAllText in 44 Dateien.
Empfehlung: `TestXaml.Alle(ausgenommen)` und `TestXaml.Laden(datei)` in TestRepoPaths.cs (oder Nachbar) bereitstellen; XamlFarbpaarModell bekommt 5–8 Modelltests mit kleinen XAML-Strings. Abnahme: hoechstens 1 Implementierung "alle XAML-Dateien"; eigene Modelltests vorhanden.

### T04 (P2) Ausfuehrungsweg, Dauer, keine schnelle Teilmenge
- Hook (`.githooks/pre-push`, aktiv via core.hooksPath=.githooks): baut und testet vier Projekte nacheinander im Debug (Zeilen 55–58). CI (`.github/workflows/ci.yml`:30–59): restore locked, Sicherheitscheck, Release-Build, dieselben vier Projekte, Abdeckungsmessung + Grenze, danach Python (Sidecar, QGIS, Training). Lokal = CI in den .NET-Tests: gut. Unterschiede: Hook Debug/ohne Coverage/ohne Vulnerabilitaetsscan; nur CI Python-Tests.
- Dauer aus nachweise/testergebnisse.json (27.09.): Infrastructure 22:47:11–22:53:03 (5 Min 52 s, 7'498 bestanden, 6 ausgelassen); Pipeline 14 s (2'853 / 3); UI 22:53:20–22:57:14 (3 Min 54 s, 7'458 bestanden / 33 nicht ausgefuehrt / 1 rot); ModernizerTests 1 s (62). Summe ca. 10 Min reine Testzeit, plus Build. Der eine rote UI-Test war der 1000-Zeilen-Waechter (MaintainabilityFitnessTests, AnnotationWorkbenchService 1034, MultiModelAnalysisService 1016 Zeilen).
- Es gibt fast keine Filterung: 5 `Trait(`-Vorkommen in tests/. Keine Kategorie "schnell/Waechter/Kindprozess".
Empfehlung: Trait `Kategorie=Waechter` und `Kategorie=Kindprozess` einfuehren (Attribute IsolatedWpfFact setzt Trait selbst), plus Skript/Doku `dotnet test --filter "Kategorie!=Kindprozess"` fuer den Alltag (Ziel unter 3 Min). Hook bleibt voll.
Abnahme: dokumentierter Schnelllauf; Hook und CI weiterhin voll.

### T05 (P2) Kindprozess-Tests
Belege: 45 `[IsolatedWpfFact]`-Attribute in 41 Dateien, 80 Dateien nennen WpfIsolatedTestProcess/StaTestRunner/IsolatedWpf, 25 `*IsolatedSmokeTests`-Dateien mit RunAsync. Jeder Test startet `dotnet vstest <Assembly> --TestCaseFilter` als eigenen Prozess (WpfIsolatedTestProcess.cs:35–52) mit 60-s-Grenze (z. B. AboutWindowIsolatedSmokeTests.cs:31, ComboBoxAnzeigeIsolatedSmokeTests.cs:32). Startkosten pro Test = Vollstart des Testhosts; Ihr Anteil an den 3 Min 54 s der UI-Suite ist nicht einzeln gemessen (Vermutung: ein grosser Teil). Bekannte Falle: `Dispatcher.Invoke(..., ApplicationIdle)` haengt im Kind (CLAUDE.md Aufgabe 5); 23 ApplicationIdle-Vorkommen in tests/ zeigen, dass die Regel nur als Prosa steht. Bekannt aus frueher: NachschlagKontextmenue-Kindtest am 60-s-Limit (Abschnitt "Nachpruefung 2026-09-06").
Gemeinsame Hilfe vorhanden (WpfIsolatedTestProcess + Attribut + Ergebnisobjekt): gut. Doppelter Aufbau eher bei den Baumsuchen: `Nachfahren(...)` in mindestens 5 Dateien kopiert (AufklappLayoutBedienTests.cs:158, DossierFieldSectionTests.cs:425, DossierRevisionRowFocusTests.cs:383, DossierTextUndoControllerTests.cs:82, FuellknopfFarbenIsolatedSmokeTests.cs:215).
Empfehlung: `WpfTestHilfe.Nachfahren` und `WpfTestHilfe.WarteAufLayout(window)` (UpdateLayout + IsLoaded, kein ApplicationIdle) in die gemeinsame Hilfe; ein Waechter, der `DispatcherPriority.ApplicationIdle` in *IsolatedSmoke*-Dateien meldet. Abnahme: kein ApplicationIdle in Kindtests; eine Nachfahren-Implementierung.

### T06 (P3) Repo-Wurzel-Suche
`FindRepositoryRoot`/`FindRepoRoot`/`ReadRepoFile`/`RepoFile` in 15+ Dateien, obwohl TestRepoPaths.cs (UI, Infrastructure, Pipeline je eine Kopie) existiert (z. B. ExplorerRevealArchitectureTests.cs:57, ImportQuellenwahlArchitectureTests.cs:124, OverviewPageLayoutTests.cs:82, SettingsMigrationArchitectureTests.cs:34, SettingsQuarantineArchitectureTests.cs:38, SettingsRestorePointArchitectureTests.cs:49). Drei Testprojekte haben je eine eigene Hilfsdatei. Empfehlung: auf TestRepoPaths umstellen (mechanisch). Abnahme: eine Definition je Testprojekt.

### T07 (P3) Uebersprungene und maschinengebundene Tests
Gut geregelt: UebersprungeneTestsWaechterTests.cs:24–48 listet 9 namentliche Skip-Quellen mit Grund (Junction, Kundenbestand, ffmpeg, GPU, Kindprozess, Meterfolgen, GeoUr); Ergebnis 27.09.: 6+3+33 nicht ausgefuehrte Tests (die 33 in UI sind ueberwiegend Kindprozess-Elternfaelle ohne Kind). 4 Dateien nutzen MachineIntegrationFact (XtfVideoCounterLive…, BendSuggestionLive…, PipeEndSuggestionLive…, SidecarE2eSmokeContractTests.cs:89), 8 `Skip =`-Stellen. Keine Aenderung noetig; nur sicherstellen, dass der Waechter bei Attributdatei-Umbenennung mitgezogen wird (siehe T01).

## 4) Was gut ist
- Hook versioniert (.githooks) und ident mit CI in den vier Projekten; MSB3021-Erkennung im Hook.
- UebersprungeneTestsWaechter mit Begruendung und Rueckrichtung (verwaiste Eintraege werden rot).
- Waechter melden verwaiste Ausnahmen (Farbpaar, Sprache).
- Coverage-Ratchet (.github/coverage-baseline.json, check-coverage.ps1), gepinnte Actions, locked restore.
- WpfIsolatedTestProcess mit Handshake/Quittung, Kindprozess kann nicht rekursiv starten.

## 5) Grenzen
- Nichts gebaut oder ausgefuehrt; Dauern stammen aus testergebnisse.json (27.09.), nicht aus einem neuen Lauf. Kindprozess-Anteil an der Laufzeit ist nicht gemessen.
- Zaehlungen per grep/Regex (Naeherungen: "liest src" 263–288 je nach Regex; Neuzugang 23 ist obere Naeherung, Dateien mit ReadAllText und src|xaml|Repo-Bezug).
- CI-Laufzeit auf GitHub nicht bekannt.
- Inhalte der Modelle (XamlFarbpaarModell) wurden nicht Zeile fuer Zeile geprueft, nur Struktur/Groesse/Nutzung.
