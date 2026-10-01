# Programmaudit SewerStudio — 2026-08-18 (Vertiefungsaudit)

**Datum:** 18.08.2026 (Nachmittag)
**Branch:** `feature/eval-pruefsatz-review`
**Geprüfter Stand:** HEAD `53ad07c96` plus uncommitted Druckcenter-/Kosten-PDF-Arbeitsstand
**Verhältnis zum Gesamtaudit vom selben Tag:** Ergänzend, nicht ersetzend. Das
[Tages-Gesamtaudit](2026-08-18-gesamtaudit.md) prüfte Commit `50d5aa7e` mit Schwerpunkt
Druckcenter-Freigabe, Sidecar-Fehlerverträge (R-01/R-02, inzwischen behoben) und Optik.
Dieses Audit hier ist eine unabhängige Tiefenprüfung aller sechs Codebereiche auf
Fehler, Robustheit und Sicherheit. **Kein Befund überschneidet sich mit dem Tages-Audit.**

## Methode

- Vollständiger Release-Build (`AuswertungPro.sln`, inkl. aller 43 Tools-Projekte)
- Alle vier .NET-Testprojekte, Sidecar-Pytest (`not gpu`), QGIS-Unittests
- Sechs parallele Code-Auditbereiche (Application, Domain, Infrastructure, UI,
  Python-Sidecar, QGIS/scripts/tools), jeder Befund durch Lesen des Codes verifiziert
- Stichproben-Gegenprüfung der schwersten Befunde durch den Hauptauditor
- Bewusst ausgenommen: bereits in CLAUDE.md dokumentierte Härtungen vom 2026-08-14 und
  die Befunde des Tages-Gesamtaudits

## Messergebnisse

| Prüfung | Ergebnis |
|---|---|
| Release-Build | **0 Fehler, 0 Warnungen** (26 s) |
| Infrastructure.Tests | 3.902 bestanden, **1 Fehler (Flake, s. unten)**, 1 übersprungen |
| Pipeline.Tests | 2.329 bestanden, 2 übersprungen |
| UI.Tests | 5.734 bestanden, 2 übersprungen |
| ProjectModernizer.Tests | 62 bestanden |
| Sidecar-Pytest | 564 bestanden, 2 GPU-Tests deselektiert |
| QGIS-Brücke | 10 bestanden |
| **Summe** | **~12.590 bestanden**, 5 übersprungen, 1 nicht reproduzierbarer Einzelfehler |

Hinweis: Die beiden roten Architekturtests aus dem Tages-Audit (Druckcenter) sind
inzwischen grün — der UI-Lauf zeigt 0 Fehler.

## Fehleranalyse: der eine rote Test

`SidecarRestartServiceTests.Lifetime_stop_tracked_beendet_nur_den_eigenen_prozess`
schlug im Gesamtlauf fehl (Prozess nach 5 s noch lebendig, Startzeit und Modulpfad
korrekt). Der Einzel-Nachlauf war in 41 ms grün; der Testkommentar dokumentiert das
bereits als seltene, nicht reproduzierbare Flake.

Ursachenanalyse (`AiStartedProcessLifetimeService.cs:159-175`): `StopTrackedProcess`
entfernt den Eintrag **vor** dem Kill aus der Liste und ruft `KillIfSameProcess` über
`BestEffort.Try` auf. Ein transienter Fehler (z. B. `Win32Exception` beim
`GetProcessById`/`Kill` eines frisch gestarteten Prozesses) wird damit zur stillen
Warnung ohne Wiederholung — im Test schlägt dann das `WaitForExit` fehl. Produktiv ist
das fail-closed abgefedert (kein Kill → kein Neustart), daher kein Sicherheitsproblem.

**Kleiner Fix (NIEDRIG):** In `KillIfSameProcess` einen fehlgeschlagenen Kill einmal
wiederholen oder das Kill-Ergebnis explizit in die Warnung aufnehmen, damit die
nächste Flake die Ursache direkt mitliefert.

## Befunde

### HOCH

**H1 — VRAM-Schutz-Gate der cls-Trainings ist seit der Sidecar-Token-Pflicht wirkungslos**
`tools/PdfProtocolIngest/train_cls.py:17-22` — `sidecar_up()` ruft `/health` ohne
`X-Sidecar-Token` auf; der Sidecar antwortet seit der Härtung vom 2026-08-14 mit 401,
der pauschale `except Exception` liefert `False` → das Training startet **auch bei
laufendem Sidecar** und riskiert genau den GPU-/VRAM-Konflikt, den das Gate verhindern
soll (Verstoß gegen das VRAM-Budget-Prinzip). Vom Auditor verifiziert.
Dasselbe Muster steckt in `training/scripts/train_osd_zeichen.py:80` und
`training/agent/config.py:31`.
**Fix:** Die korrekte Variante steht bereits im Repo (`training/scripts/train_bcc_pilot.py:299-308`:
`HTTPError` 401 = „Sidecar läuft", plus Socket-Fallback) — auf alle drei Stellen
übertragen.

### MITTEL

**M1 — VideoLabelTool: GET-Endpunkte ohne Host-/Token-Prüfung**
`tools/VideoLabelTool/server.py:409-426` — Nur `do_POST` prüft Host/Token/Origin.
`do_GET`/`do_HEAD` liefern Kundendaten (Befunde, Clips) an jeden Aufrufer;
`/session.json` (Zeile 415-416) gibt sogar das POST-Token preis. Der fehlende
Host-Check öffnet Browser-DNS-Rebinding — exakt das Muster, das das Audit 2026-08-14
bei der QGIS-Bridge als P1-3 gehärtet hat. Vom Auditor verifiziert.
**Fix:** `is_allowed_local_host(...)` am Anfang von `do_GET`/`do_HEAD` verlangen;
perspektivisch Token auch für Daten-GETs.

**M2 — Sidecar: Größenlimit per `Transfer-Encoding: chunked` umgehbar**
`sidecar/sidecar/main.py:235-253` — Der R-01-Fix prüft nur den `Content-Length`-Header;
chunked Requests ohne Längenangabe umgehen das 4-GiB-Limit vollständig (Body wächst
unbegrenzt im Speicher). Lokaler DoS-Vektor mit gültigem Token; die Tests in
`test_request_haertung.py` decken nur Content-Length ab.
**Fix:** Body-Methoden ohne parsebares Content-Length mit 411 ablehnen (oder zählend
streamen) und einen Chunked-Test ergänzen.

**M3 — `VsaCodeValidator.IsKnownCode` akzeptiert 6–8-stellige Müll-Codes**
`src/AuswertungPro.Next.Domain/VsaCatalog/VsaCodeValidator.cs:58` — Das Regex
`^[A-Z]{3,8}$` widerspricht der eigenen Konstante `MaxKnownCodeLength = 5` (Zeile 30)
und dem Klassenzweck („strenger Eintrittsfilter für Trainingslabels").
`IsKnownCode("BCAFAFOO")` liefert `true` (Hauptcode BCA bekannt); verwendet u. a. in
`TrainingCenterImportService.cs:539` und `GroundTruthFieldParser.cs:144` → solche
Parsing-Artefakte landen im Trainingsbestand. Vom Auditor verifiziert.
**Fix:** Regex auf `^[A-Z]{3,5}$` ändern (kein Test verlangt 6–8).

**M4 — VideoFrameStream: geteilte PNG-Signatur = stiller Frameverlust mit Zeitstempel-Versatz**
`src/AuswertungPro.Next.Infrastructure/Ai/VideoFrameStream.cs:329-334` — Findet
`TryExtractPng` keine 8-Byte-Signatur (weil sie über eine Pipe-Lesegrenze geteilt
wurde), wird der gesamte Akkumulator verworfen — **ohne** `frameIndex` zu erhöhen
(anders als die Timeout-/50-MB-Pfade, Zeile 305-310). Ein Frame geht verloren und alle
folgenden Frames bekommen einen um einen Schritt zu frühen Zeitstempel → die
Meterpositionen aller nachfolgenden Befunde des Videos verschieben sich still.
Vom Auditor verifiziert.
**Fix:** Bei fehlender Signatur die letzten 7 Bytes im Akkumulator behalten.

**M5 — Haltungs-/Dichtheits-Verteilung: Fehler enden als unobserved Exception**
`src/AuswertungPro.Next.UI/ViewModels/Pages/ExportPageViewModel.cs:581` und `:713` —
`DistributeHoldingsAsync`/`DistributeDichtheitAsync` haben `try/finally` ohne `catch`;
ein Gesamtfehler erscheint nur im Tageslog, der Nutzer sieht nichts. Die
Schwester-Methode `DistributeShaftsAsync` zeigt das erwartete Muster (Dialog-Warnung).
**Fix:** `catch (Exception ex)` mit `UserError.DescribeAndReport` + `_dialogs.Warn`
analog der Schacht-Verteilung (≈6 Zeilen).

**M6 — EvalSetV2Builder: V1-Konsistenzprüfung läuft nach dem Veröffentlichen**
`src/AuswertungPro.Next.Application/Ai/Evaluation/EvalSetV2Builder.cs:172-176` —
`Directory.Move(stagingRoot, outputRoot)` steht vor dem Digest-Vergleich; wirft der,
bleibt ein inkonsistentes V2-Set veröffentlicht liegen und ein Neulauf scheitert an
„Ziel existiert bereits".
**Fix:** Digest vor dem Move prüfen oder im Fehlerfall `outputRoot` entfernen.

**M7 — Uhrlage „3:00" wird beim Goldsample-Speichern still zu null**
`src/AuswertungPro.Next.Application/UseCases/PhotoAnnotations/PhotoAnnotationUseCase.cs:370-382` —
`ResolveClockPosition` parst `vsa.uhr.von` nur als Zahl; reale Werte tragen das Format
„3:00". Der Schwester-Code (`GoldQualityReviewQueueUseCase.ReadExistingClockPosition`)
schneidet den Doppelpunkt-Teil korrekt ab.
**Fix:** Denselben Colon-Strip vor dem Parsen einbauen.

**M8 — PdfProtocolIngest: Drei Robustheitslücken im Werkzeugkasten**
- `tools/PdfProtocolIngest/pdf_ingest.py:174-175` — Findings mit `meter=None` werfen im
  `format(..., ".2f")` einen `TypeError`; der äußere catch verwirft daraufhin den
  **ganzen** Record statt nur das defekte Finding. Fix: `meter is None` → `continue`.
- `tools/PdfProtocolIngest/pdf_ingest.py:18-22` — `_sh()` verschluckt auch
  `FileNotFoundError`: fehlen `pdftotext`/`pdfinfo`, sieht ein Lauf mit leerem Katalog
  „erfolgreich" aus. Fix: beim Start per `shutil.which` prüfen und klar abbrechen.
- `pdf_ingest.py:157`, `build_cls_dataset.py:15`, `enrich_vsa_dataset.py:82,119` —
  JSONL-Kataloge per nacktem `json.loads` über alle Zeilen: eine einzige korrupte
  (abgebrochen geschriebene) Zeile bricht extract/build/enrich komplett ab.
  Fix: gemeinsamer Ladehelfer, der korrupte Zeilen mit Zeilennummer-Warnung überspringt.

### NIEDRIG (Auswahl, vollständig in den Bereichs-Audits)

| # | Ort | Kurzfassung |
|---|---|---|
| N1 | `Application/Ai/CodingFindingCoveragePolicy.cs:70-75` | Uhrlage-Vergleich als Rohstring („3" vs „3:00") → mögliche Doppelbefunde. Fix: `ClockPositionNormalizer` |
| N2 | `Application/Media/PhotoImportService.cs:16-45` | Ein defektes Foto bricht den Ordnerimport ab (unverdrahtet, latent). Fix: Pro-Datei-catch + Fehlerliste |
| N3 | `Application/Protocol/WinCanCatalogDiscoveryService.cs:124-169` | Doppeltes Öffnen der Katalogdatei; `catch { return null; }` verschluckt Lesefehler lautlos |
| N4 | `Application/Costs/PositionListEditor.cs:18-19` | `CanMoveUp` ohne Obergrenzprüfung → `ArgumentOutOfRangeException` bei stale Selektion |
| N5 | `Domain/Models/AiDecisionAudit.cs:54-68` | Cloner wirft NRE bei deserialisierten Altbeständen mit null-Unterobjekten (bricht Projekt-Laden) |
| N6 | `Domain/Models/HaltungRecordCloner.cs:56-73` | „Tiefkopie" lässt XTF-Anker `KanalschadenTid`/`UntersuchungTid` still fallen |
| N7 | `Domain/Models/SchachtRecord.cs:38,64` | Kompat-Setter stempelt auch automatische Schreibungen auf `FieldSource.Manual` (falscher Tooltip) |
| N8 | `Domain/VsaCatalog/VsaLabelBuilder.cs:36-46` | XPrefix der AEC/AED-Codes wird ignoriert → Label nur „Rohrmaterial" statt „Rohrmaterial, PVC" |
| N9 | `Domain/Models/vsa_rili_rules_kanaele.json` | Unvollständige Schein-Regeldatei ohne Referenz, frei erfundene EZ-Werte → löschen oder nach docs/ |
| N10 | `Infrastructure/Media/VideoFrameSequenceExtractor.cs:101-120` | ffmpeg-stdout wird nie gelesen, kein Timeout → Deadlock-/Hänger-Risiko |
| N11 | `Infrastructure/Costs/CostCalculationService.cs:111-118,157-164` | „Self-heal" überschreibt korrupte Benutzerdateien ohne `.bak`-Sicherung |
| N12 | `Infrastructure/HoldingFolderDistributor.SidecarXtf.cs:33-36` | Statische XTF-/Dateiindex-Caches ohne Invalidierung → neue Dateien in laufender Sitzung unsichtbar |
| N13 | `UI/ViewModels/Pages/BuilderPageViewModel.cs:385-389` | Reset-Zweig detacht Handler der alten Collection nicht (latenter Leak) |
| N14 | `UI/Views/Windows/PhotoMeasurementWindow.Sam.cs:120-128` | `cts.Cancel(); cts.Dispose();` während Segment-Task läuft → `ObjectDisposedException` im Hintergrund |
| N15 | `UI/Views/Windows/ObservationCatalogWindow.xaml.cs:82-97` + `ProtocolEntryEditorDialog.xaml.cs:480-497` | Fire-and-forget `SuggestWithKiAsync` ohne catch → Nutzer sieht Fehler nicht |
| N16 | `UI/Views/Windows/TrainingStudioWindow.xaml.cs:140-145` | `async void` Loaded-Handler ohne try/catch |
| N17 | `UI/ViewModels/Pages/BuilderPageViewModel.cs:1` | Uncommitted Diff fügt unbeabsichtigt eine BOM hinzu |
| N18 | `sidecar/routes/warmup.py:99-103` + `models/yolo_wrapper.py:670-693` | SAM-Warmup und cls-Laden ohne Busy-Lease → hängender Ladevorgang blockiert Threadpool unsichtbar |
| N19 | `sidecar/routes/health.py:25-37` | `/health` (async) macht synchrones Datei-I/O + Voll-Hash im Event-Loop → blockiert Liveness-Polls |
| N20 | `sidecar/models/yolo_wrapper.py:480-486` | Telemetrie (nvidia-smi-Subprozess) im Early-Return noch innerhalb des Predict-Locks |
| N21 | `sidecar/gpu_manager.py:408,559,673` | CUDA-Geräteindex hart `0` verdrahtet, obwohl `cuda:N` konfigurierbar ist (Multi-GPU misst falsch) |
| N22 | `sidecar/routes/training.py:85-88,533-547` | Überlebte `*.tmp`-Staging-Ordner nach Crash werden nie aufgeräumt (bis GiB pro Vorfall) |
| N23 | `sidecar/schemas/detection.py:141-147` | `BoundingBox`-Floats ohne `allow_inf_nan=False` → NaN/Infinity wandern bis an SAM |
| N24 | `tools/VideoLabelTool/server.py:561-562` | `float(...)`/String-Parsing in `handle_save` ohne catch → Handler-Thread-Ausnahme, keine Antwort |
| N25 | `integrations/qgis/sewerstudio_bridge/sewerstudio_bridge.py:528-531` | Leer-Layer-Erkennung per Bytesubstring `b'"features":[]'` bricht still bei jeder Serializer-Formatänderung |
| N26 | `integrations/qgis/.../sewerstudio_bridge.py:496-512` | 8 sequenzielle HTTP-Polls à 1,5 s im GUI-Thread → QGIS friert bis zu ~12 s ein |
| N27 | `tools/BasemapDownloader/Program.cs:24-33` | `args[++i]` ohne Bounds-Check → `IndexOutOfRangeException` statt Usage |
| N28 | `tools/PdfProtocolIngest/Sidecar_stoppen.bat` | `findstr ":8100"` matcht Substrings → fremde Prozesse (Port 81000+) würden mitgetötet |
| N29 | `Application/Protocol/VsaKekCatalogBuilder.cs:680-685` | `GetAwaiter().GetResult()` mit 30-s-Prozess-Timeout (ungenutzt, latent) |

Dazu zwei Test-/Tooling-Kleinigkeiten: Sidecar-Pytest meldet eine
Cache-Schreibwarnung (`.pytest_cache`, Permission denied), und der
Flake-Fix aus der Fehleranalyse oben.

## Priorisierte Verbesserungsliste

Alle Punkte sind klein, lokal und brauchen keine neuen Abhängigkeiten:

1. **H1** (drei Stellen, Copy-Paste aus `train_bcc_pilot.py`) — VRAM-Konflikt-Gate
   wieder scharf machen; einziger Befund mit akutem Schadenspotenzial.
2. **M2** — Chunked-Lücke des gerade eingecheckten R-01-Fixes schließen (plus Test).
3. **M1** — VideoLabelTool-Host-Check (eine Zeile pro Methode), Rebinding abdichten.
4. **M3** — Regex `{3,8}` → `{3,5}` (Ein-Zeichen-Fix, schützt den Trainingsbestand).
5. **M4** — 7 Bytes im Akkumulator behalten (schützt Meterpositionen aller Befunde).
6. **M5** — Zwei catch-Blöcke nach Schacht-Muster (Nutzer sieht Importfehler wieder).
7. **M6/M7** — Reihenfolge-Fix bzw. Colon-Strip (Datenintegrität Eval-Set / Goldsamples).
8. **M8** — PdfProtocolIngest-Robustheit (Werkzeug funktioniert sonst „erfolgreich" leer).
9. Danach die NIEDRIG-Tabelle in Batches (N5/N6/N7 Domain-Datenintegrität,
   N18–N23 Sidecar-Hygiene, N13–N16 UI-Fehlersichtbarkeit) abarbeiten.

## Geprüft und in Ordnung (Kurzfassung)

- **Sicherheit .NET:** Keine SQL-/Command-Injection (ArgumentList, QuoteId, Parameter),
  XML ausschließlich über `SafeXmlDocumentLoader` (DTD verboten), ZIP-Traversal fail-closed,
  CSV-Formel-Entschärfung zentral, keine Secrets im Code.
- **Sidecar-HTTP:** Loopback-Bindung erzwungen, Pflicht-Token mit Konstantzeit-Vergleich,
  Middleware-Reihenfolge korrekt, kein Endpunkt akzeptiert clientseitige Dateipfade.
- **GPU-Manager:** Sperrenordnung überall eingehalten, Eviction atomar, In-flight-
  Ladereservierungen sauber — kein Deadlock-/Lease-Umgehungspfad gefunden.
- **Datei-Integrität:** Import-Staging (SHA-256, Reparse-Point, Rollback), atomare
  Projekt-Saves mit `.bak`, Backup mit Nachprüfung — vorbildlich.
- **UI:** Globale Exception-Abdeckung vorhanden, statische Events symmetrisch abgemeldet,
  HTTP-Server nur Loopback + Token; der uncommitted Druckcenter-Diff ist defensiv und
  testgedeckt.
- **Domain:** Kein IO/Threading — keine Leaks/Races möglich; Parser fail-closed,
  Zahlenkultur invariant, `CalibrationMath` divisionssicher.

## Nicht geprüft

- Echte GPU-Läufe (YOLO/DINO/SAM/Qwen), reale Kundenimporte, laufendes QGIS
- Die 43 Tools-Projekte nur stichprobenartig (Schwerpunkt: netzwerk-/datenberührende)
- `_legacy/` (PowerShell-Altbestand) — bewusst ausgenommen
- Optik/Bedienung (Deckungsbereich des Tages-Gesamtaudits)
