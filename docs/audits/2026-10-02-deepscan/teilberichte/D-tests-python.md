# Deepscan 02.10.2026 – D-tests-python (Tests, Testbarkeit, Python)

Stand: release-master, HEAD fbff3dd4d. Nur gelesen, nichts gebaut oder geändert.

## 1) Kurzfazit

Die Umgebungsabhängigkeiten vom 01.10. sind im Wesentlichen sauber abgefangen. Ich fand keine weitere feste Pfad-, Zeitzonen- oder Netzabhängigkeit, die sicher rot wird. Das Netz um die Querschnittsregeln (XTF, WebGIS, Eval-Schutz, Virtuelle Spalten) ist dicht. Die grössten Hebel liegen woanders: 16 Python-Testdateien unter `tools/` laufen in keiner CI, drei Skripte, die Trainingsdaten verändern, haben gar keinen Test, und die CI hat kein Zeitlimit. In den Testprojekten gibt es 87 private Kopien von `TempDirectory`. Beim Python-Sammelthema Z11 hat sich nichts verschlechtert, die Haltungsidentität ist inzwischen zusammengeführt.

## 2) Befundtabelle

| ID | Prio | Titel | Grösse | Nutzen |
| --- | --- | --- | --- | --- |
| T1 | P1 | 16 Python-Testdateien unter `tools/` laufen in keiner CI (13 Review-Server, davon `test_review_server_security.py`) | S | Sicherheitstests der Prüfserver schützen wirklich |
| T2 | P1 | Drei Skripte, die Trainingsdaten verändern, ohne jeden Test | M | Gold-/Eval-Regeln auch an diesen Schreibstellen festgehalten |
| T3 | P2 | CI ohne `timeout-minutes` und ohne `--blame-hang` | S | Ein hängender WPF-Test blockiert nicht 6 Stunden |
| T4 | P2 | 87 private `TempDirectory`/`TempDir`-Klassen, 3 verschiedene `TestRepoPaths` | M | Eine Hilfe pro Testprojekt, einheitliche Aufräumregel |
| T5 | P2 | Gold-Rolle: Pfadwächter nur für einen Teil der Schreibstellen mit Verknüpfungstest | M | Je Schreibstelle ein Verknüpfungs-Test |
| T6 | P2 | Handwert-Regel: 20 von 44 Schreibstellen ohne Test, der den Klassennamen nennt | M | Dünnstes Netz bei einer Kernregel |
| T7 | P3 | `DisableTestParallelization` der UI.Tests versteckt in `AiPlatformConfigTests.cs` | S | Auffindbar, nicht aus Versehen verloren |
| T8 | P3 | `ApplicationIdle`-Invoke trotz Regel noch in 7 WPF-Tests | S | Regel aus CLAUDE.md überall gleich |
| T9 | P3 | Grösste Python-Funktionen 200–414 Zeilen (39 über 150) | L | Nur bei Berührung angehen |

## 3) Befunde im Detail

### T1 (P1, belegt) – Python-Tests unter `tools/` ohne CI
- Beleg: `.github/workflows/ci.yml` Zeilen 90, 93, 111 führen nur `sidecar` (pytest), `integrations/qgis/tests` (unittest) und `training/scripts/tests` aus.
- Nicht erfasst: `tools/EvalVisibilityReview/test_*.py` (13 Dateien, ca. 3'900 Zeilen Tests, darunter `test_review_server_security.py`, `test_bcc_release_holdout_review_server.py` 648 Zeilen, `test_detect_gold_error_review_server.py` 670 Zeilen), `tools/skill-linter/test_skill_lint.py`, `tools/VideoLabelTool/test_server_selection.py`, `tools/GroundTruthPipeScaleProbe/test_probe.py`.
- Warum es bremst: Die Server schreiben Prüfurteile (Eval-/Gold-Entscheide). Ihre Host-/Pfad-/Signaturprüfungen sind getestet, aber nur auf Pascals Rechner, wenn er daran denkt. Sie können still brechen.
- Empfehlung: Im Schritt «Trainingsskript-Tests» eine Zeile `pytest tools/EvalVisibilityReview tools/VideoLabelTool tools/GroundTruthPipeScaleProbe tools/skill-linter -q -p no:cacheprovider` (alle vier Ordner, also alle 16 Dateien; Nachtrag nach Review 02.10.) anfügen (gleiche venv). Fehlende Abhängigkeiten in `requirements-test.txt` ergänzen.
- Fertig, wenn: CI führt die Tests aus und ist grün; eine absichtlich entfernte Host-Prüfung in `review_server_security.py` macht CI rot.

### T2 (P1, belegt) – Trainingsdaten schreibende Skripte ohne Test
Kein Test in `training/scripts/tests`, `sidecar/tests`, `tools/**` nennt diese Dateien (Namenssuche über alle .py/.cs):
- `training/scripts/import_gold_labels.py` (375 Zeilen, schreibt Gold; eigener Eval-Haltungsschutz in Zeile 138, aber ungetestet).
- `training/scripts/remove_eval_contaminated_from_register.py` (225 Zeilen) verändert `training_samples.json` und `export_registry_v1.json`. Docstring sagt: Standardlauf schreibfrei, atomar mit Sicherung. Keiner dieser Zusagen hält ein Test fest.
- `training/scripts/repair_inbox_gold_holding_ids.py` (376 Zeilen) schreibt CaseIds um. Die Schwesterskripte `repair_gold_holding_ids` und `repair_pdf_gold_holding_ids` haben Tests.
- Zusätzlich ohne Test, aber kleiner Risiko: `publish_bcc_copilot_candidate.py` (220), `freeze_osd_validation_set.py` (170), `lernstufe_fehler_einbauen.py` (121).
- Warum: CLAUDE.md verlangt «Prüf- und Eval-Daten nie ins Training». Gerade Skripte, die Register oder Gold verändern, sind die Schreibstellen dieser Regel.
- Empfehlung: Je Skript ein kleiner Test im Stil der Schwestertests: (a) Standardlauf schreibt nichts, (b) Eval-Haltung wird nicht übernommen/wird entfernt, (c) Sicherung entsteht vor dem Schreiben. Reihenfolge: `remove_eval_contaminated…`, `import_gold_labels`, `repair_inbox…`.
- Fertig, wenn: je Skript mindestens die Tests (a)–(c) laufen in CI unter `training/scripts/tests`.

### T3 (P2, belegt) – CI ohne Zeitgrenze
- Beleg: `ci.yml` enthält weder `timeout-minutes` noch `--blame-hang-timeout`.
- Warum: Die WPF-Tests laufen teils in Kindprozessen (`WpfIsolatedTestProcess`), aber ein Hänger im Hauptprozess oder in `ApplicationIdle` (siehe T8) hält den Lauf bis zum GitHub-Limit. Am 01.10. stand die CI schon einmal wochenlang rot.
- Empfehlung: `timeout-minutes: 45` je Job (Wert vorher an der letzten grünen Laufzeit messen) und `--blame-hang-timeout 10m` bei `dotnet test`.
- Fertig, wenn: ein absichtlich hängender Test den Job nach dem Limit mit Hinweis auf den Test abbricht.

### T4 (P2, belegt) – Testhilfen nicht zusammengeführt
(Q4 vom 30.09. betraf Theme-/XAML-Hilfen und Sprachlisten; das hier ist neu.)
- 87 private Klassen `TempDirectory`/`TempDir` (45 in Infrastructure.Tests, 42 in UI.Tests), zum Beispiel `Dossiers/DossierComponentListExportServiceTests.cs:267`, `AtomicTextFileWriterTests.cs:88`, `FileContentComparerTests.cs:40`. Es gibt keine gemeinsame Klasse. Daneben 509 Dateien mit `Path.GetTempPath` und 922 `Guid.NewGuid`.
- 66 Dateien mit `GetTempPath` enthalten weder `Directory.Delete`, `Dispose` noch `Cleanup` (Heuristik per Suche, nicht einzeln geprüft). Die zwei `TestAppDataIsolation` (Infrastructure, UI) legen je Lauf einen Temp-Ordner an und löschen ihn nie.
- Drei `TestRepoPaths` (UI, Pipeline, Infrastructure) mit gleicher Aufgabe, aber verschiedenem Code (Infrastructure hat `RepoRoot()`, die anderen `FindRepositoryRoot()`); dazu ein eigener `RepoRoot()` in `Infrastructure.Tests/LegacyOfferCodeRemovedTests.cs:32`.
- Warum bremst es: Das 8.3-Kurzpfad-Problem vom 01.10. hätte bei einer einzigen Hilfe an einer Stelle gelöst werden können. Heute müsste man 87 Stellen prüfen, wenn der Temp-Pfad wieder zum Problem wird.
- Empfehlung: Je Testprojekt `TestTempDirectory : IDisposable` (langer Pfad via `Path.GetFullPath`, eindeutiger Name, Löschen in `Dispose`), danach die privaten Kopien bei Berührung ersetzen (nicht alle auf einmal). Die drei `TestRepoPaths` als verlinkte Quelldatei in einem `tests/Shared/` ablegen (kein neues Projekt nötig, `Compile Include` mit Link).
- Fertig, wenn: neue Tests nutzen nur die gemeinsame Klasse; eine Sperrklinke (Test, der neue `class TempDir…` in tests/ zählt) hält die Zahl 87 nicht mehr steigend.

### T5 (P2, belegt/teilweise Vermutung) – Pfadwächter je Schreibstelle
- 39 Dateien in `src` nutzen `ProjectWritePathGuard`/`DistributionWritePathGuard`. Direkt die Klasse nennen nur `ProjectWritePathGuardTests` und `DossierAttachmentCollectorTests`. Über Verknüpfungs-/Junction-Suche (`junction|ReparsePoint|Verknüpfung`) sind viele Schreibstellen abgedeckt.
- Ohne Test, der die Klasse nennt UND Verknüpfungen betrifft: `DossierAttachmentFilePublisher`, `DossierFileStore`, `DossierWordTemplateExportService`, `DossierComponentListExportService`, `DossierAttachmentOwnershipManifest`, `VerteilberichtAblage`, `SchachtProQrAblage`, `ImportProjektdateiPruefer`, `ParsedShaftDistributionController`, `DichtheitDistributionController`, `DistributionTargetReuse`.
- Vermutung: Einige davon sind über einen Dienst darüber indirekt getestet (z. B. `SchachtProQrAblage` über `SchachtProQrImportService`, Dossiers über `DossierPlanPublicationServiceTests`). Ich habe das nicht Zeile für Zeile geprüft.
- Empfehlung: Eine Parameter-Testklasse «jede Schreibstelle verweigert Verknüpfungsziel» (Theorie über eine Liste der 11 Stellen, wie `ReparsePointGuardTests` es für Backup macht). Dazu eine Sperrklinke: neue Datei in `src` mit `File.WriteAll*` ausserhalb der Wächterliste wird gemeldet.
- Fertig, wenn: für jede genannte Klasse ein Test existiert, der ein Verknüpfungsziel ablehnt; `JunctionFact` ist dabei in der CI nicht übersprungen.

### T6 (P2, belegt) – Handwert-Regel hat das dünnste Netz
- 44 Dateien in `src` kennen `UserEdited`, 53 Testdateien erwähnen es, aber nur 4 setzen `UserEdited = true` ausdrücklich.
- Schreibstellen, deren Klassenname in keinem Test vorkommt: `HoldingExcelExportSnapshotFactory`, `ObjektaktenSchachtVererbung`, `DssExportBearbeitung`, `DssProfilBearbeitung`, `DssProjektAngaben`, `FieldMetadataKopie`, `KatasterFeldschutz`, `KinsHoldingNameNormalizer`, `VsaKekAbbildung` (jeweils 0 Treffer in tests/).
- Mit nur 1 Testdatei: `Sia405WhitelistEnricher`, `HaltungsnummerKatasterAbgleich`, `ObjektaktenPaketImport`, `SchachtHoehenRechnung`, `XtfAenderungsPlanBuilder`, `HaltungRecordCloner`, `KinsDbfWhitelistEnrichmentService`, `KinsDvdTextEnrichmentService`, `ProjectVideoReferenceNormalizer`, `DataPageClearColumnController`, `ParsedHoldingDistributionController`.
- Einschränkung: Ein Test kann die Klasse über eine Fassade treffen, ohne den Namen zu nennen. Die Liste ist ein Suchhinweis, kein Beweis für fehlenden Schutz. `KatasterFeldschutz` und `FieldMetadataKopie` sind aber Kernstücke der Regel und sollten direkt getestet sein.
- Empfehlung: Eine Theorie «jede Schreibstelle lässt ein Feld mit `UserEdited` (auch leer) unangetastet» über eine kleine Registerliste der Stellen. Reihenfolge: `KatasterFeldschutz`, `FieldMetadataKopie`, `ObjektaktenSchachtVererbung`, `VsaKekAbbildung`, DSS-Bearbeitungen.
- Fertig, wenn: jede der 9 «0 Treffer»-Klassen mindestens einen Test hat, der ein Handfeld (auch leer) bewahrt.

### T7 (P3, belegt) – Parallelschalter versteckt
- `tests/AuswertungPro.Next.UI.Tests/AiPlatformConfigTests.cs:11` trägt `[assembly: CollectionBehavior(DisableTestParallelization = true)]`. Infrastructure.Tests hat dafür `AssemblyInfo.cs`, Pipeline.Tests läuft parallel (nur Sammlung «EnvironmentVars» seriell).
- Risiko: Wird die Datei umgebaut oder gelöscht, laufen die UI-Tests plötzlich parallel, und WPF-/Env-Tests stören sich. Kommentar in `VsaCodeToLabelConverterTests.cs:12` verlässt sich darauf.
- Empfehlung: Zeile in `UI.Tests/AssemblyInfo.cs` verschieben. Fertig, wenn: Datei existiert und `AiPlatformConfigTests.cs` enthält keine `assembly:`-Zeile mehr.

### T8 (P3, belegt) – `ApplicationIdle` trotz Regel
CLAUDE.md: «nach `Show()` `UpdateLayout()` statt `ApplicationIdle`-Invoke». Noch zu finden in `BearbeitungErledigtUiTests.cs:132`, `ListenErgaenzungWindowIsolatedSmokeTests.cs:51,63`, `NovaDialogHeaderIsolatedSmokeTests.cs:130`, `NovaDialogWindowIsolatedSmokeTests.cs:218,225`, `AufklappLayoutBedienTests.cs:69,105`. Bei `ListenErgaenzung` und `NovaDialog` ist `WindowFx.SetEntrance(…, false)` gesetzt (die Eintrittsanimation, die Idle aushungern lässt, ist dort aus), bei den anderen nicht geprüft. Heute grün, aber dieselbe Art Hänger, die `AboutWindowIsolatedSmokeTests.cs:52` dokumentiert. Empfehlung: bei Berührung ersetzen; bis dahin T3 als Netz.

### T9 (P3, belegt) – Grösste Python-Funktionen
Per `ast` gemessen (ohne Tests): `_validate_hard_negative_queue` 414 Zeilen (`tools/EvalVisibilityReview/bcc_release_holdout_review_server.py`), `_read_audit_samples` 339 (`prepare_bcc_pilot.py`), `main` 312 (`bcc_lernstufe_aus_protokoll.py`), `build_audit` 309 (`gold_stock_audit.py`), `build_preparation` 304 / `execute_preparation` 289 / `_read_active_migration` 270 (`prepare_detect_gold.py`), `evaluate_holdout_status` 290 (`bcc_release_holdout.py`). Insgesamt 39 Funktionen über 150 Zeilen. Kein Fehler für sich. `gold_stock_audit.py` ist von 2'917 auf 1'828 Zeilen gesunken (AP10), `prepare_detect_gold.py` hat 2'206 und die zugehörigen Tests 1'037 Zeilen, das passt. Nur anfassen, wenn der Test des jeweiligen Skripts die Funktion schon festhält (`test_gold_stock_audit.py`, `test_prepare_detect_gold.py` sind da).

## Zu Z11 (nur Neues)
- Haltungsidentität: bessert sich. `training/scripts/haltungsidentitaet.py` wird von 3 Skripten, 2 Review-Servern und 4 Testdateien genutzt; die gemeinsame Beispieldatei `tests/Fixtures/Haltungsidentitaet/beispiele.json` bindet C# und Python. Übrig sind Hüllfunktionen (`_physical_holding_key` je Datei, bewusst, siehe Docstring) und 2 Kopien `normalize_haltungs_key` in `tools/kb_audit/kb_context_*.py`. Unverändert, keine Verschlechterung.
- SHA-256: 65 Dateien in `training`/`tools` definieren eine eigene Funktion (57 ohne Tests); 30.09. standen 58. Die Zählweise ist nicht identisch (ich zählte jede `def …sha256…`), deshalb keine belegte Verschlechterung. `os.replace` selbst gebaut: 23 Dateien (30.09.: 25). Es gibt weiterhin kein gemeinsames Modul ausser `haltungsidentitaet.py`. Neu seit 30.09.: nichts Belegtes. Empfehlung unverändert.

## 4) Was gut ist und erhalten bleiben soll
- Das Netz für WebGIS-Schutz (12 Testdateien, u. a. `WebGisGeschuetzteFelderTests`, `WebGisHandwertTests`, `WebGisSchreibschutzTests`), XTF (`XtfDss*`, `XtfAenderungsExportTests`, Bemerkung nie kürzen in 13 Dateien) und Eval-Schutz (`KnowledgeBaseManagerEvalGuardTests`, `GuardedRetrievalFactoryTests`, `TrainingSamplesStoreEvalLeakageTests`, `RetrievalEvalFilterTests`, Python `test_negativsatz_vertrag.py`).
- `VirtuelleSpalteSchutzTests` hält die Nova_-Regel an Haltung und Schacht fest.
- Befundfoto-Regel: `VsaFotoAblagePolicyTests` benennt Temp-Rückfall bei fehlendem Video bewusst (Zeile 29); `CodingSnapshotTargetPolicy.cs:16` und `VsaFotoAblagePolicy.cs:27` fallen dann auf Temp zurück. Das ist als Ausnahme festgehalten, nicht als Fehler gemeldet.
- Umgebungsisolation: `TestAppDataIsolation` (Modulinitialisierer), Sammlung «EnvironmentVars» seriell, Env-Tests mit Wiederherstellung, `JunctionFactAttribute`, `FfmpegFactAttribute`, `GeoUrLiveFactAttribute` (Live nur auf Zuruf), Kultur-Tests setzen und stellen zurück (`FachzahlParserTests`, `NormalizedBoundingBoxTests`). CI läuft en-US, Pascal de-CH; beide Kulturen laufen also real.
- Feste `C:\…`-Strings in den Tests sind fast alle Platzhalter ohne Dateizugriff (geprüft an `HoldingDistributionFileServicesTests`, `ProjektVorschauPdfUseCaseTests`, `ActiveProjectGuardTests`, `KnowledgeRootGuardTests`); `ExternalProcessRunnerTests` hat nach dem 01.10. 60 s statt 5 s. Die erste Prüfung dort (`RunAsync_KillsProcess…`, Obergrenze 3 s bei 250 ms Timeout) misst nur den Abbruch, nicht den Start; Rest-Risiko klein.
- Python: Review-Server teilen `review_server_security.py` (loopback, JSON-Grösse); Testdichte in `training/scripts/tests` hoch (41 Dateien).

## 5) Grenzen der Prüfung
- Nichts ausgeführt (kein dotnet, kein pytest); «grün/rot» sind nicht gemessen. Die Aussagen zu Tests beruhen auf Textsuche (Klassennamen, Muster). Ein Test, der eine Klasse über eine Fassade trifft, taucht dort nicht auf; T5 und T6 sind deshalb Suchhinweise.
- Kulturabhängigkeit ohne ausdrückliches `CurrentCulture` (z. B. `ToString("F2")` in Erwartungswerten) habe ich nicht einzeln geprüft. Keine Testzeitmessungen.
- `bin`/`obj`-Ordner im Repo wurden ignoriert. Grössen der Python-Funktionen per `ast`, ohne Tests und ohne `venv`.
- Nicht geprüft (Codex arbeitet dort): PlayerWindow*, Player/*, ProjectImportOrchestrator, ParsedHoldingDistributionController, SettingsFullBackup*, HaltungsgrafikSvgBuilder, ServiceProvider, PDF-Import. Nur genannt, weil sie in der Liste von T5/T6 vorkommen (`ParsedHoldingDistributionController`).
