# B-messung – Messung, Brennpunkte, Klone und leere catch-Blöcke (02.10.2026)

Prüfstand: release-master, HEAD fbff3dd4d. Der Analysator ist unverändert aus `docs/audits/2026-09-27-wartbarkeit/nachweise/analysator` kopiert. Nur `ImplicitUsings` und `Nullable` mussten in der csproj ergänzt werden, sonst baut er im Scratchpad nicht. Ausgabe liegt in `scratchpad/deepscan-1002/mess/`. Ergebnis: 6191 Dateien, 0 Parse-Fehler. Die Auswertung nutzt `auswerten.py` aus `messung-nachher`.

## 1) Kurzfazit
Die Kennzahlen stehen seit der Messung nach Welle 3 (30.09.) praktisch still. Es sind keine Methoden über 200 Zeilen und keine Dateien über 1000 Zeilen dazugekommen. Klongruppen (11) und leere catch-Blöcke (237) sind gleich geblieben. Die Brennpunkte der letzten 60 Tage sind meist Feature-Aufbauphasen (Dossiers 22.–31.08., Nova-Redesign 05.–09.09., Optik 28./29.09.). Nur ShellViewModel, AppSettings und SchaechtePage sammeln dauerhaft viele Themen. Seit 18.09. liegt die Änderungslast bei den WebGIS-Use-Cases. Von 11 Klongruppen sind etwa 9 echte Doppelpflege-Kandidaten. Alle 237 leeren catch-Blöcke tragen einen Kommentar.

## 2) Befundtabelle
| ID | Prio | Titel | Grösse | Nutzen |
|---|---|---|---|---|
| B1 | P3 | Kennzahlen seit Welle 3 unverändert, Sperrklinken halten | – | Zustand stabil; Messung dient als neue Basis |
| B2 | P2 | ShellViewModel, AppSettings, SchaechtePage sammeln viele Themen | M | Jede neue Funktion berührt dieselben 3 Dateien, 2 davon dicht an 1000 Zeilen |
| B3 | P3 | Dossier-Gruppe: Brennpunkt nur aus der Aufbauphase | – | Kein Handlungsbedarf, nur beobachten |
| B4 | P2 | Doppelpflege Nova-Controller (SetzeSichtbar, ApplyDrawerOpenState) | S | Zwei Methoden nur noch einmal pflegen |
| B5 | P2 | Doppelpflege Dossier-PDF-Dienste (3 Methodenpaare) | S | Kopf, Zustand, Metadaten einmal pflegen |
| B6 | P3 | Kleinere Klone (EnsureVisibleOnScreen 3x, Stores, Cadastre, VSA-Hook, Records) | S je | Je eine Stelle statt zwei bis drei |
| B7 | P2 | WebGIS-Use-Cases: aktuell die häufigsten Änderungen (seit 18.09.) | M | Zwei Dateien von 760 und 493 Zeilen mit Methoden bis 120 Zeilen |

## 3) Befunde im Detail

### B1 Messwerte (Produktcode `src/`)
Spalten 27.09. und 30.09. stammen aus `docs/audits/2026-09-30-wartbarkeit/nachweise/messwerte.json` und `messwerte-nach-welle3.json` (release-master). Die Spalte „heute" ist gemessen.

| Kennzahl | 27.09. | 30.09. (nach Welle 3) | heute |
|---|---|---|---|
| Dateien | 3218 | 3306 | 3310 |
| Zeilen | 375221 | 385705 | 386489 |
| Methoden | 14879 | 15333 | 15368 |
| Methoden > 100 Zeilen | 191 | 197 | 197 |
| Methoden > 200 Zeilen | 33 | 28 | 28 |
| Entscheidungen > 20 | 163 | 170 | 170 |
| Parameter > 8 | 155 | 156 | 156 |
| async void | 30 | 32 | 32 |
| leere catch | 232 | 237 | 237 |
| exakte Klongruppen | 12 | 11 | 11 |
| Dateien 900–1000 / 975–1000 | 23 / 8 | 21 / 9 | 21 / 9 |
| Dateien > 1000 | 2 | 0 | 0 |
| Teilklassen 1800–2000 / > 2000 | 7 / 2 | 7 / 2 | 7 / 2 |
| Testcode Dateien / Zeilen | 2620 / 363392 | 2713 / 383386 | 2722 / 384866 |
| UI/Ai Dateien / Zeilen | 599 / 34200 | 594 / 33970 | 594 / 33970 |

Gegenüber Welle 3: +4 Dateien, +784 Zeilen, +35 Methoden, alle Schwellenwerte gleich. Die 9 Dateien zwischen 975 und 1000 Zeilen stehen unverändert an der Sperrklinke, zum Beispiel ShellViewModel.cs mit 999 und SchaechtePage.xaml.cs mit 1000 Zeilen.

Längste Methoden heute: PlayerWindow-Konstruktor 539, HaltungsgrafikSvgBuilder.BuildHaltungsgrafikSvg 502, ServiceProvider-Konstruktor 406, ProjectImportOrchestrator.Import 401, ParsedHoldingDistributionController.Distribute 362 (alle Codex-Bereich). Danach WinCanDbImportService.ImportEinzelnesProjekt 354, SchaechtePageViewModel.ImportProtocolFolderAsync 325, SelfTrainingOrchestrator.RunAsync 313. Grösste Teilklasse: PlayerWindow, 4246 Zeilen in 73 Dateien (Codex-Bereich); danach HoldingFolderDistributor 2981 in 6 Dateien.

### Brennpunkte
Zeitraum 03.08.–02.10. (848 Nicht-Merge-Commits, nur `src/*.cs`). Rang ist Commits mal Dateizeilen, ohne die Codex-Bereiche. Rohdaten: `mess/git60.txt`, Skript `mess/hot.py`.

| # | Datei (unter src/AuswertungPro.Next.) | Zeilen | Längste Methode | Commits |
|---|---|---|---|---|
| 1 | UI/ViewModels/ShellViewModel.cs | 999 | 161 (Konstruktor) | 31 |
| 2 | UI/Views/Pages/SchaechtePage.xaml.cs | 1000 | 122 (RebuildColumns) | 28 |
| 3 | Infrastructure/Dossiers/DossierWordTemplateExportService.cs | 948 | 167 (ExportAsync) | 29 |
| 4 | UI/AppSettings.cs | 851 | 45 (NormalizeAfterLoad) | 26 |
| 5 | UI/ViewModels/Pages/DossiersPageViewModel.Actions.cs | 910 | 72 (AssemblePdfAsync) | 22 |
| 6 | UI/Views/Pages/DataPage.xaml.cs | 876 | 90 | 21 |
| 7 | UI/ViewModels/Pages/DataPageViewModel.cs | 973 | 300 (Konstruktor) | 17 |
| 8 | UI/Views/Rendering/DossierPreviewPageRenderer.cs | 833 | 202 (ZeichneAbsatz, Entscheidungen 46) | 19 |
| 9 | UI/Views/Windows/DossierPreviewWindow.xaml.cs | 544 | 62 | 29 |
| 10 | UI/ViewModels/Pages/ExportPageViewModel.cs | 866 | 131 (DistributeHoldingsAsync) | 17 |
| 11 | UI/ViewModels/Pages/SchaechtePageViewModel.cs | 562 | 147 | 24 |
| 12 | UI/ViewModels/Pages/DossiersPageViewModel.cs | 732 | 89 | 18 |
| 13 | Infrastructure/Import/WinCan/WinCanDbImportService.cs | 958 | 354 (ImportEinzelnesProjekt, Entscheidungen 41) | 12 |
| 14 | Application/WebGis/WebGisExportUseCase.cs | 760 | 80 | 15 |
| 15 | Application/Xtf/XtfStammdatenPlanBuilder.cs | 813 | 93 | 14 |
| 16 | Infrastructure/Import/Xtf/LegacyXtfImportService.cs | 471 | 132 | 23 |
| 17 | UI/ServiceProviderRegistrationMap.cs | 262 | 184 (Create) | 36 |
| 18 | UI/Views/Windows/DossierPreviewFieldPanel.Fields.cs | 701 | 86 | 13 |
| 19 | Application/Xtf/XtfNeuPlanBuilder.cs | 700 | 108 | 13 |
| 20 | UI/Views/Windows/TrainingStudioWindow.xaml.cs | 816 | 68 | 11 |

Die Zählung geht je Datei. ShellViewModel besteht aus 5 Teildateien, der ganze Typ wird also öfter berührt.

**Stichprobe der 5 grössten (belegt aus `git log` Betreffzeilen je Datei, keine Diffs gelesen):**
1. **ShellViewModel.cs, mischt Themen.** Nova-Leiste und Brotkrume (06./07.09.), Hilfe/F1/Handbuch (28.09.), Programmidentität (28.09.), Toast statt Dialog (28.09.), Umlaute und UserError (29.09.), Rückgängig/Wiederholen (29.09.), Objektakten (11.09.), Projektwechsel und Einstellungen (19.09.), Dossier-Navigation (22.–25.08.), Karte entfernt (30.08.). Mindestens 8 Themen. Echte Sammelstelle, obwohl es schon 5 Teildateien gibt.
2. **SchaechtePage.xaml.cs, mischt Themen.** 11 Nova-Commits am 07.09., Aufklappliste (08.09.), WebGIS-Begriffe (23.09.), Nachschlagen/Kataster (30.08.), XTF-Schachtfunktion (29.08.), Excel-Spalten (22.08.), Kosten/MWST (20.08.), Import-Absicherung (21.08.), Rückgängig (29.09.). Seit 29.09. ruhig. Die Seite spiegelt DataPage (21 Commits).
3. **DossierWordTemplateExportService.cs, ein Thema (Dossier).** 25 der 29 Commits liegen zwischen 22.08. und 29.08.; seit 29.08. kein Commit mehr. Aufbauphase. ExportAsync mit 167 Zeilen bleibt gross.
4. **AppSettings.cs, mischt Themen.** Nova, Spaltenansichten, Karte (entfernt), Dossier, KI-Schalter, WebGIS, Design wie Windows/Hochkontrast/DPI, Berichtsmarke. Typische Sammelklasse: Jede neue Funktion trägt ihre Felder ein. Letzte Änderung 29.09.
5. **DossiersPageViewModel.Actions.cs, ein Thema (Dossier).** 20 von 22 Commits zwischen 22.08. und 31.08., danach nur der Umlaut-Sammelcommit vom 29.09. Aufbauphase.

Ergebnis: Echte Mehrthemen-Stellen sind ShellViewModel, SchaechtePage und AppSettings. Die zwei Dossier-Dateien zeigen einen abgeschlossenen Feature-Aufbau.

**B7 Aktuelle Änderungslast (seit 18.09., 260 Nicht-Merge-Commits, ohne Codex-Bereiche):** WebGisImportUseCase.cs 16 Commits (493 Zeilen, Methode `Uebernimm` 120 Zeilen, Entscheidungen 54), WebGisExportUseCase.cs 15 (760 Zeilen), MultiModelAnalysisService.cs 14 (551 Zeilen, `ProcessFrameAsync` 162, Entscheidungen 32), LegacyXtfImportService.cs 13, AnnotationWorkbenchService.cs 12, WebGisImportPlanBuilder.cs 12, WebGisExportPlanBuilder.cs 11, SchaechtePageViewModel.cs 9. Die WebGIS-Gruppe (4 Dateien, 54 Berührungen) ist der aktuelle Brennpunkt. `WebGisImportUseCase.Uebernimm` hat mit 54 den höchsten Entscheidungswert unter den Brennpunkten, und die Regeln zum WebGIS-Schutz sind fachlich heikel (CLAUDE.md: nie Eigentum, Betreiber, Länge, Baujahr überschreiben). Die Themen der Commits wurden nicht gelesen; ob die Änderungen zusammenhängen, ist Vermutung.

### Exakte Methoden-Klone (11 Gruppen, mind. 100 Token, nur `src/`)
Rohdaten: `mess/exact-method-clones.json`. Klongruppen 12 am 27.09., 11 am 30.09., 11 heute.

| Gruppe | Orte | Zeilen | Echte Doppelpflege? |
|---|---|---|---|
| SetzeSichtbar | DataPageNovaWorkspaceController.cs:221 und SchaechteNovaWorkspaceController.cs:277 | 35 | Ja. Gleiche Aufgabe auf zwei Seiten; beide Seiten wurden in den Nova-Etappen gemeinsam angepasst (Betreffe 07.09.). |
| ApplyDrawerOpenState | dieselben zwei Dateien, :191 und :246 | 23 | Ja, dieselben Dateien. |
| ComposeHeader | DossierConditionClassPdfService.cs:184 und DossierHoldingListPdfService.cs:457 | 24 | Ja. Seitenkopf, soll überall gleich aussehen. |
| ComposeCondition | DossierHoldingListPdfService.cs:320 und DossierShaftListPdfService.cs:332 | 26 | Ja, Haltungs-/Schachtliste. |
| ComposeMetadata | DossierHoldingListPdfService.cs:166 und DossierShaftListPdfService.cs:181 | 26 | Ja. |
| ResetUserOverrides | CostCatalogStore.cs:194 und MeasureTemplateStore.cs:111 | 31 | Wahrscheinlich. Zwei Stores gleicher Bauart; Vermutung. |
| IsTableFresh | HaltungCadastreTableFileStore.cs:158 und SchachtCadastreTableFileStore.cs:222 | 22 | Ja, Haltung/Schacht-Paar. |
| HookVsaValidationEvents | ProtocolEntryEditorDialog.xaml.cs:187 und ObservationCatalogWindow.xaml.cs:109 | 28 | Ja. Zwei VSA-Fenster mit demselben Validierungsablauf. |
| StelleFeldzustandWiederHer | HaltungRecord.cs:246 und SchachtRecord.cs:244 | 12 | Ja, aber klein: Kern von Rückgängig, beide Records spiegeln sich. |
| EnsureVisibleOnScreen (3 Kopien) | MediaSearchWindow.xaml.cs:187, RecordDetailsWindow.xaml.cs:36, SanierungsmassnahmenWindow.xaml.cs:371 | 10 | Hilfscode, selten geändert; Doppelpflege-Risiko klein. |
| BuildSourceSuggestion | UI/Services/WorkbenchQueueService.cs:412 und Application/UseCases/GoldQualityReview/GoldQualityReviewQueueUseCase.cs:523 | 25 / 22 | Vermutung: Schichtübergreifend, UI kopiert Application-Logik; Methoden nicht inhaltlich verglichen. |

Einschätzung: 24 Methoden, rund 300 Zeilen. Acht Gruppen sind Haltung/Schacht-, Liste/Liste- oder Fenster/Fenster-Paare. Das Risiko ist, dass eine Änderung nur an einer Seite gemacht wird. Am wahrscheinlichsten laufen die Nova-Controller auseinander (zwei Methoden, 58 Zeilen), weil sie als Paar gepflegt werden.

### Leere catch-Blöcke (237 in `src/`)
Verteilung: Infrastructure 159, UI 64, Application 14. Alle 237 tragen einen Kommentar im Block. 164 sind unqualifiziert `catch`, 17 `OperationCanceledException`, 12 `Exception ex`, 10 `Exception`, 7 `IOException`, 3 `InvalidOperationException`. Meiste je Datei: App.xaml.cs 7, VideoFrameStream.cs 5, je 4 in DirectoryMirror, DossierPdfAssemblyService, ProgramCleanupService, MediaConflictCenterService, SystemMonitorService, PdfTextExtractionService. Seit Welle 3 unverändert. Nur Messung; die Ursachenanalyse liegt beim anderen Prüfer.

## Empfehlungen (klein)
- **B4 (S):** Die zwei Methoden der Nova-Controller in eine gemeinsame Hilfsklasse oder Basisklasse legen. Fertig, wenn: beide Methoden stehen einmal im Code, bestehende Controller-Tests grün, Klongruppen sinken um 2.
- **B5 (S):** ComposeHeader, ComposeCondition und ComposeMetadata der drei Dossier-PDF-Dienste in eine geteilte interne Klasse. Fertig, wenn: Klongruppen sinken um 3, PDF-Tests unverändert grün.
- **B2 (M):** Neue Shell-Themen und Einstellungen nicht mehr in ShellViewModel.cs und AppSettings.cs, sondern in neue Teildateien oder Unterobjekte. Fertig, wenn: ShellViewModel.cs und SchaechtePage.xaml.cs unter 950 Zeilen (Sperrklinke steht bei 1000), kein Verhalten geändert.
- **B7 (M):** Vor der nächsten WebGIS-Änderung `WebGisImportUseCase.Uebernimm` (120 Zeilen, Entscheidungen 54) in benannte Schritte teilen. Fertig, wenn: längste Methode der Datei unter 60 Zeilen, WebGIS-Schutztests unverändert grün.
- **B6 (S je):** EnsureVisibleOnScreen als gemeinsamer Fensterhelfer; die übrigen Paare bei Gelegenheit.

## 4) Was gut ist und erhalten bleiben soll
- Sperrklinken halten: keine Datei über 1000 Zeilen, keine neue Methode über 200 Zeilen seit Welle 3.
- Alle leeren catch-Blöcke sind kommentiert.
- Die Dossier-Dateien zeigen nach dem Aufbau keine Dauerlast mehr.
- Der Parser meldet 0 Syntaxfehler im Gesamtstand.

## 5) Grenzen der Prüfung
- Der Analysator zählt nur Methoden und Konstruktoren, keine Eigenschaften, Lambdas oder lokalen Funktionen. Der Entscheidungswert ist ein Proxy.
- Der Brennpunktrang ist Commits mal Dateizeilen je Datei, nicht je Typ.
- Die Themen der Commits stammen aus Betreffzeilen, nicht aus gelesenen Diffs.
- Bei ResetUserOverrides und BuildSourceSuggestion ist die Doppelpflege nur vermutet.
- Die Codex-Bereiche (PlayerWindow, ServiceProvider, Import-Orchestrator, Verteilung, Backup, SVG) fehlen in der Rangliste, führen aber bei den längsten Methoden.
- Die Analysator-csproj wurde für den Bau minimal angepasst (ImplicitUsings, Nullable); Program.cs ist unverändert.
