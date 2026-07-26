# Abschlussaudit SewerStudio – 25.07.2026

## 1. Kurzurteil und Freigabeentscheidung

**Freigabeentscheidung: NICHT FREIGEBEN.**

Von zwölf Funden sind:

- **3 behoben:** F8, F9, F11
- **7 teilweise behoben:** F1–F5, F7, F12
- **2 nicht behoben:** F6, F10
- **0 nachgewiesene Regressionen**

Der Build und alle normalen Tests sind grün. Trotzdem bleiben sicherheits- und fachlich wichtige Lücken: mögliche fremde Löschpfade beim Import-Rollback, unvollständiger Junction-Schutz, fehlendes Klassifikatorgewicht, ein zu breiter und nicht ausreichend gesicherter Wissen-Import sowie mehrere nicht getestete Fehlerpfade.

Kims Urheberschaft einzelner Änderungen ist nicht belegbar: Der Arbeitsbaum war bereits stark verändert.

## Ist-Schnappschuss

| Merkmal | Stand |
|---|---|
| Auditzeit | 25.07.2026, 04:41–05:20 Uhr MESZ |
| Branch | `feature/gis-karte` |
| HEAD | `2e92a23bb52aeac8e5c6daa3fd07f19b34700539` |
| Upstream | 17 Commits voraus |
| Git-Status | 201 Einträge: 129 verfolgte Änderungen, 72 neue Dateien |
| Diff gegen HEAD | 128 Dateien, +4’325 / −584 Zeilen, 0 Binärdateien |
| Neueste Auditdatei | `docs/audits/2026-07-25-kimi-ergebnis-audit-status.json`, 04:39:02 |
| Neueste Testdatei | `XtfMediaPathSecurityTests.cs`, 01:12:31 |
| Neueste Quelldatei | `ProjectPortabilityService.cs`, 01:09:05 |

Branch, HEAD, Status und Diff-Statistik blieben während des Audits unverändert. Nach Auditbeginn wurden keine Quell-, Test-, Konfigurations- oder `C:\KI_BRAIN`-Dateien geschrieben. Nur normale Testausgaben unter `bin/obj` entstanden.

## 2. Bewertung F1–F12

| Fund | Status | Beleg und Bewertung |
|---|---|---|
| F1 Importmarker | **TEILWEISE BEHOBEN** | `StagingRoot` wird kanonisiert und begrenzt ([ImportTransactionRecoveryService.cs:85](C:/Sewer-Studio_KI_4.5/src/AuswertungPro.Next.Infrastructure/Import/ImportTransactionRecoveryService.cs:85)). `PublishedTargets` wird dagegen ungeprüft mit dem Projektpfad kombiniert; danach kann bei passendem, ebenfalls manipulierbarem Hash gelöscht werden ([Zeile 43](C:/Sewer-Studio_KI_4.5/src/AuswertungPro.Next.Infrastructure/Import/ImportTransactionRecoveryService.cs:43), [Zeile 65](C:/Sewer-Studio_KI_4.5/src/AuswertungPro.Next.Infrastructure/Import/ImportTransactionRecoveryService.cs:65)). Absolute Pfade, `..` und Junction-Eltern fehlen. Die drei Staging-Tests sind grün, prüfen diesen Angriff aber nicht. |
| F2 Backup/Junctions | **TEILWEISE BEHOBEN** | Die normale Spiegel-Aufzählung ist geschützt. Der Guard behandelt IO-/Zugriffsfehler jedoch als „kein Reparse Point“ ([ReparsePointGuard.cs:15](C:/Sewer-Studio_KI_4.5/src/AuswertungPro.Next.Infrastructure/Backup/ReparsePointGuard.cs:15)). Weitere ungeschützte Aufzählungen bestehen in [DirectoryMirror.cs:130](C:/Sewer-Studio_KI_4.5/src/AuswertungPro.Next.Infrastructure/Backup/DirectoryMirror.cs:130), [BackupManifestIntegrity.cs:236](C:/Sewer-Studio_KI_4.5/src/AuswertungPro.Next.Infrastructure/Backup/BackupManifestIntegrity.cs:236) und vor rekursiver Löschung in [FullBackupService.cs:491](C:/Sewer-Studio_KI_4.5/src/AuswertungPro.Next.Infrastructure/Backup/FullBackupService.cs:491). Junction-Fokustests sind grün, decken diese Wege nicht vollständig ab. |
| F3 Goldspeichern | **TEILWEISE BEHOBEN** | Das Panel schließt nach einem gemeldeten Persistenzfehler nicht und bietet Retry an ([Workflow:41](C:/Sewer-Studio_KI_4.5/src/AuswertungPro.Next.UI/Ai/CodingConfirmationDecisionCommandWorkflow.cs:41)). Ein fehlgeschlagenes Goldbild wird aber nur protokolliert; anschließend wird das unvollständige Sample trotzdem gespeichert ([Coordinator:171](C:/Sewer-Studio_KI_4.5/src/AuswertungPro.Next.UI/Ai/CodingTrainingSamplePersistenceCoordinator.cs:171), [Coordinator:184](C:/Sewer-Studio_KI_4.5/src/AuswertungPro.Next.UI/Ai/CodingTrainingSamplePersistenceCoordinator.cs:184)). Die sieben Retry-Tests prüfen nur Fehler des Sample-Persisters, nicht das Scheitern des Bildspeichers. |
| F4 Videoausfälle | **TEILWEISE BEHOBEN** | `NoFinding` bleibt Erfolg. Timeout und `ModelUnavailable` werden als fehlgeschlagene Frames gezählt ([VideoFullAnalysisService.cs:228](C:/Sewer-Studio_KI_4.5/src/AuswertungPro.Next.Infrastructure/Ai/VideoFullAnalysisService.cs:228)). Auch ein Totalausfall wird jedoch nur `Degraded`, nicht `Failed` ([Zeile 300](C:/Sewer-Studio_KI_4.5/src/AuswertungPro.Next.Infrastructure/Ai/VideoFullAnalysisService.cs:300)). Im übergeordneten Ergebnis fehlen diese Warnungen; dort werden nur Generator-Warnungen übernommen und `Error` bleibt null ([VideoAnalysisPipelineService.cs:279](C:/Sewer-Studio_KI_4.5/src/AuswertungPro.Next.Infrastructure/Ai/VideoAnalysisPipelineService.cs:279)). Vier Fokustests sind grün, ein Totalausfalltest fehlt. |
| F5 FFmpeg | **TEILWEISE BEHOBEN** | Exit-Code, stderr-Ende, Framezahl und Ein-Frame-Toleranz sind vorhanden. Ein unbekannter Exit-Code wird bei ausreichender Framezahl trotzdem als vollständig akzeptiert ([VideoFrameStream.cs:168](C:/Sewer-Studio_KI_4.5/src/AuswertungPro.Next.Infrastructure/Ai/VideoFrameStream.cs:168)). Ein Null-/Leerframe zählt nur als übersprungen, nicht als Fehler ([VideoFullAnalysisService.cs:169](C:/Sewer-Studio_KI_4.5/src/AuswertungPro.Next.Infrastructure/Ai/VideoFullAnalysisService.cs:169)). Die acht Abschlussprüfungen sind überwiegend prozessfreie Funktionstests; echte FFmpeg-Exit-, Rest-PNG- und Nullframe-Ketten fehlen. |
| F6 Klassifikator-Health | **NICHT BEHOBEN** | `/health` prüft im Lazy-Zustand nicht, ob Gewicht und SHA tatsächlich gültig sind. `get_classifier_status()` meldet dann nur `active_json_present` beziehungsweise `override_configured` ([yolo_wrapper.py:676](C:/Sewer-Studio_KI_4.5/sidecar/sidecar/models/yolo_wrapper.py:676)). Das konfigurierte Gewicht fehlt physisch. Die C#-Warnung reagiert nur auf ausdrücklich `loaded=false` ([VisionPipelineDtos.cs:38](C:/Sewer-Studio_KI_4.5/src/AuswertungPro.Next.Application/Ai/VisionPipelineDtos.cs:38)). Die Tests arbeiten mit künstlich vorgegebenen Statusobjekten. |
| F7 Echter YOLO-Test | **TEILWEISE BEHOBEN** | `test_yolo_empty_frame` ruft die echte `/detect/yolo`-API auf und bestand einzeln. Der Hilfscode fängt nur `ImportError`, kein breites `catch` ([test_yolo.py:11](C:/Sewer-Studio_KI_4.5/sidecar/tests/test_yolo.py:11)). Der Test trägt aber weder `gpu` noch `e2e` ([test_yolo.py:59](C:/Sewer-Studio_KI_4.5/sidecar/tests/test_yolo.py:59)) und läuft deshalb im angeblich CPU-hermetischen Standardlauf. CPU- und GPU-Integration sind nicht sauber getrennt. |
| F8 Klassenkarte V3 | **BEHOBEN** | V2 bleibt Version 2 mit 14 Klassen und ohne BCC ([YoloDetectClassMapV2.cs:11](C:/Sewer-Studio_KI_4.5/src/AuswertungPro.Next.Application/Ai/Training/ClassMaps/YoloDetectClassMapV2.cs:11)). V3 besitzt BCC als ID 14 ([YoloDetectClassMapV3.cs:11](C:/Sewer-Studio_KI_4.5/src/AuswertungPro.Next.Application/Ai/Training/ClassMaps/YoloDetectClassMapV3.cs:11)). UI und StageA verwenden V3 ([ServiceProvider.cs:551](C:/Sewer-Studio_KI_4.5/src/AuswertungPro.Next.UI/ServiceProvider.cs:551)). Beide früher roten Klassenkarten-/Golden-Tests sind grün. Der alte externe V2/15-Pilot existiert noch, beide Kandidaten stehen aber auf `not_deployed`; keine produktive Aktivierung gefunden. |
| F9 Exportregister | **BEHOBEN** | Auswahl und Meldung außerhalb des Registers sind getrennt ([TrainingYoloExportCoordinator.cs:204](C:/Sewer-Studio_KI_4.5/src/AuswertungPro.Next.Infrastructure/Ai/Training/ExportPlans/TrainingYoloExportCoordinator.cs:204)). Die UI nennt Anzahl und IDs der nicht exportierten Goldsamples ([TrainingYoloExportWorkflow.cs:106](C:/Sewer-Studio_KI_4.5/src/AuswertungPro.Next.UI/Ai/Training/TrainingYoloExportWorkflow.cs:106)). Zwei Registertests sind grün. Realdaten: 48 freigegeben, 121 weitere vollständige Samples außerhalb. |
| F10 Wissen-Export | **NICHT BEHOBEN** | SQLite-Snapshot und temporäres Exportarchiv sind gut umgesetzt. Das Manifest enthält jedoch keine Dateihashes. Der Export umfasst weiterhin Teacher-Daten, komplettes `training`, Modelle und Legacy-Pfade ([KnowledgeBackupFileCatalog.cs:60](C:/Sewer-Studio_KI_4.5/src/AuswertungPro.Next.UI/Services/KnowledgeBackupFileCatalog.cs:60), [Zeile 85](C:/Sewer-Studio_KI_4.5/src/AuswertungPro.Next.UI/Services/KnowledgeBackupFileCatalog.cs:85)). Beim Import fehlen Größen-, Verhältnis-, Anzahl- und Doppeltzielgrenzen; alle erkannten Einträge werden direkt gesammelt ([KnowledgeBackupService.cs:333](C:/Sewer-Studio_KI_4.5/src/AuswertungPro.Next.UI/Services/KnowledgeBackupService.cs:333)). Kein eigenständiges neues Goldprofil. |
| F11 Drag-and-drop | **BEHOBEN** | Drag-Start liegt pro Liste statt global ([CodingEventDragDropBehavior.cs:70](C:/Sewer-Studio_KI_4.5/src/AuswertungPro.Next.UI/Behaviors/CodingEventDragDropBehavior.cs:70)). Payload enthält Quellliste und Session-Key; `DragOver` und `Drop` prüfen erneut ([Zeile 131](C:/Sewer-Studio_KI_4.5/src/AuswertungPro.Next.UI/Behaviors/CodingEventDragDropBehavior.cs:131), [Zeile 145](C:/Sewer-Studio_KI_4.5/src/AuswertungPro.Next.UI/Behaviors/CodingEventDragDropBehavior.cs:145)). Beide Listen erhalten denselben fensterbezogenen Schlüssel. Neun Sessiontests sind grün. |
| F12 Räumlicher Dedup | **TEILWEISE BEHOBEN** | Räumlich getrennte Treffer im selben Frame erhalten getrennte Schlüssel; fehlende BBox behält das alte Verhalten ([TemporalFindingDeduplicator.cs:58](C:/Sewer-Studio_KI_4.5/src/AuswertungPro.Next.Infrastructure/Ai/Pipeline/TemporalFindingDeduplicator.cs:58)). Der aktive Zustand speichert aber keine BBox ([Zeile 295](C:/Sewer-Studio_KI_4.5/src/AuswertungPro.Next.Infrastructure/Ai/Pipeline/TemporalFindingDeduplicator.cs:295)). `#1/#2` hängt deshalb in Folgeframes von der Eingangsreihenfolge ab. Vertauscht sich diese, können Identitäten wechseln. Die fünf neuen Tests prüfen keine zwei aufeinanderfolgenden Erkennungsframes mit vertauschter Reihenfolge. |

## 3. Build- und Testergebnisse

| Prüfung | Bestanden | Fehlgeschlagen | Übersprungen/abgewählt | Ergebnis |
|---|---:|---:|---:|---|
| `dotnet build AuswertungPro.sln -c Release --no-restore` | – | 0 Fehler | 0 Warnungen | **Bestanden**, 2,02 s |
| Infrastructure | 3’071 | 0 | 1 | **Bestanden** |
| Pipeline | 1’962 | 0 | 1 | **Bestanden** |
| UI | 5’313 | 0 | 1 | **Bestanden** |
| ProjectModernizer | 62 | 0 | 0 | **Bestanden** |
| **.NET gesamt** | **10’408** | **0** | **3** | **Bestanden** |
| Sidecar CPU `-m "not gpu"` | 142 | 0 | 2 übersprungen, 2 GPU abgewählt | **Bestanden**, 3,37 s |
| Echter YOLO-Endpunkt einzeln | 1 | 0 | 0 | **Bestanden**, 2,49 s |
| Sidecar GPU DINO/SAM | 0 abgeschlossen | 0 nachgewiesen | – | **Nicht prüfbar: Timeout nach 600 s** |
| QGIS unittest | 5 | 0 | 0 | **Bestanden**, 0,555 s |
| Fokussierte .NET-Risikotests | 132 | 0 | 0 | **Bestanden** |
| Fokussierte Sidecar-Risikotests | 50 | 0 | 2 | **Bestanden** |

Die drei .NET-Skips sind:

- lokale VSA-KEK-Fixture unter `D:\Videoprojekte` fehlt;
- echter Video-/GPU-Vertrag verlangt `SEWERSTUDIO_RUN_MACHINE_INTEGRATION=1` und ein Testvideo;
- ein WPF-Kindtest ist absichtlich nur im isolierten Prozess ausführbar; sein übergeordneter Test bestand.

Die zwei früher roten Tests sind jetzt ausdrücklich grün:

1. `TrainingYoloClassMapArtifactsTests.Versionierte_Vorlagen_sind_vollstaendig_und_nur_BCC_ist_fuer_den_Pilot_freigegeben`
2. `TrainingExportGoldenFixtureTests.LocalExecutor_schreibt_exakt_die_gemeinsame_Golden_Fixture`

Die GPU-Prüfung war trotz RTX 5090 und vorhandener DINO-/SAM-Gewichte nicht abschließbar. Pytest lieferte innerhalb von zehn Minuten keinen abgeschlossenen DINO- oder SAM-Test.

## 4. Cybersecurity

### Kritisch

Keine bestätigte kritische Lücke.

### Hoch

| Fund | Einordnung |
|---|---|
| Manipulierter Importmarker kann fremde Datei löschen | `PublishedTargets` akzeptiert absolute/übergeordnete Pfade. Der Marker liefert zugleich den erwarteten Hash. Damit ist die Löschkette im Code belegt. Ein destruktiver Angriffstest wurde aus Sicherheitsgründen nicht ausgeführt. |

### Mittel

| Fund | Einordnung |
|---|---|
| Unvollständiger Reparse-/Junction-Schutz | Mehrere Backup-, Manifest-, Mirror- und Löschpfade prüfen Reparse Points zu spät oder gar nicht. Fehler werden teilweise fail-open behandelt. |
| ZIP-Bomb und Doppeltziele | Knowledge-Import hat keine Grenzen für Gesamtgröße, Eintragszahl oder Kompressionsverhältnis und erkennt doppelte Zielpfade nicht. Lexikalisches ZIP-Slip wird dagegen blockiert. |
| Unauthentisierte `.pt/.pth`-Modelle | YOLO-Classifier prüft beim `active.json`-Weg einen SHA, dieser ist aber lokal veränderbar. Legacy-, DINO- und SAM-Fallbacks wählen `.pt/.pth` ohne authentisierte Herkunft. PyTorch-Modelle dürfen deshalb nur aus kontrollierter Quelle übernommen werden. |
| Breite ACL von `active.json` | `Authentifizierte Benutzer` und mehrere SIDs besitzen Änderungsrechte. Hash und Modellpfad können gemeinsam geändert werden. |
| HTTP-/GPU-Warteschlange | Bildgröße und Pixelzahl werden nach dem JSON-Empfang begrenzt. Für die bereits gepufferte HTTP-Nutzlast sowie wartende GPU-Aufträge besteht keine klare globale Mengen- oder Parallelitätsgrenze. Ein Token-Inhaber kann lokalen Speicher und Wartezeit belasten. |
| Direkter Wissen-Import | Dateien werden zwar einzeln atomar und mit Rollback-Backup geschrieben, aber nicht als vollständig validiertes Gesamtpaket in einem getrennten Staging geprüft. |

### Niedrig

| Fund | Einordnung |
|---|---|
| Token-Erstellung verlässt sich auf geerbte ACL | Die aktuelle Token-Datei ist angemessen geschützt, aber der Erzeugungscode härtet ACL und Symlink-Ziel nicht selbst. |
| Register und Klassenkarten sind nicht kryptografisch signiert | Hashes erkennen unbeabsichtigte Änderungen, authentisieren aber keinen Autor. Für eine lokale Einzelbenutzer-App ist dies primär ein Integritäts- und kein Fernangriffsproblem. |
| Architekturdokumentation widersprüchlich | `CLAUDE.md` und Code verwenden V3; die geladene Architektur-Skill-Dokumentation beschreibt an einer Stelle noch V2/15. |

Nicht bestätigt wurden SQL-Injection, gefährliche polymorphe Deserialisierung, Kommandoinjektion oder ein Secret-Leak. SQL-Werte werden überwiegend parametrisiert; dynamische IBAK-Bezeichner werden maskiert. Der Sidecar akzeptiert nur Loopback, prüft den Host und verlangt einen Token mit konstantzeitlichem Vergleich.

F4, F5 und der wahrscheinliche F12-Identitätswechsel sind hauptsächlich Robustheits- beziehungsweise Fachprobleme, keine Cybersecurity-Lücken.

## 5. Daten- und Goldstandard-Integrität

Quelle und Zeitpunkt: `C:\KI_BRAIN`, rein lesend geprüft am 25.07.2026 zwischen 05:17 und 05:19 Uhr MESZ.

| Kennzahl | Ergebnis |
|---|---:|
| Samples in `training_samples.json` | 182 |
| Persönlich bestätigt durch `Besitzer` | 182 |
| Samples mit vorhandenem Bild | 182 |
| Eindeutige physische Bilder | 177 |
| Samples mit BBox | 182 |
| Samples mit SAM | 169 |
| Fehlende SAM-Abdeckung | 13 |
| Vollständig: Bild + BBox + SAM | 169 |
| Freigaberegister | 48 |
| Vollständige Samples außerhalb des Registers | 121 |
| Eindeutige Bilder außerhalb des Registers | 120 |

Alle 177 physischen Bilder besitzen unterschiedliche SHA-256-Werte. Fünf Bildpfade werden jeweils von zwei Samples referenziert; dies sind logische Mehrfachreferenzen, keine doppelten Dateien.

Das abgeleitete Inventar `main_code_inventory_v1.json` ist veraltet: Es nennt nur 111 persönliche und 97 vollständige Samples. Der aktuelle Stand ist 182 beziehungsweise 169.

SQLite:

- `integrity_check`: `ok`
- `quick_check`: `ok`
- Fremdschlüsselverletzungen: 0
- Samples: 183
- Embeddings: 183
- verwaiste Embeddings: 0
- Versionsstände: 142

Es besteht eine logische Abweichung: SQLite enthält das zusätzliche Sample `wb_c70a75f727a9`. Es besitzt denselben Fall und Bildpfad wie `wb_45d478f79f41`. JSON hat 182 IDs, SQLite 183.

Aktive Klassenkarte:

- Anwendungs- und Exportcode: **V3, 15 Klassen, BCC ID 14**
- V2: **14 Klassen, unverändert**
- `C:\KI_BRAIN\yolo_class_map.json`: älteres, unversioniertes 35-Code-Verzeichnis; nicht die aktive V3-Detektor-Exportkarte.
- Alter V2/15-Datensatz existiert weiter, beide BCC-Kandidaten sind aber `not_deployed`.

Klassifikator:

- `active.json`: `vsa_cls_v5_nocrop`
- Soll-Pfad: `C:\KI_BRAIN\yolo_cls_runs\vsa_cls_v5_nocrop\weights\best.pt`
- Deklarierter SHA-256: `121134583eeb7b175047ee98b0cf9493cb93cc231971ad1c44a518def676bd80`
- Gewicht existiert nicht; tatsächlicher SHA-256 ist daher **nicht berechenbar**.

Token-Datei:

- vorhanden, 32 Bytes;
- Besitzer: `PCW11X01\Besitzer`;
- Zugriff nur Besitzer, Administratoren, SYSTEM und lesender Sandbox-Gruppe;
- kein Tokeninhalt in geprüften Logs gefunden.

## 6. Regressionen und neue Risiken

Eine sichere Zuordnung zu Kimi ist wegen des vorbelasteten Arbeitsbaums nicht möglich.

Nachgewiesene neue Regressionen wurden nicht gefunden. Im Endstand bestehen aber folgende Risiken:

- Der breite Wissen-Export/-Import vergrößert die mögliche Schadensfläche auf Teacher-, Trainings-, Modell- und Legacy-Daten.
- `active.json` behauptet ein produktives Klassifikatorgewicht, das nicht existiert.
- FFmpeg akzeptiert einen unbekannten Exit-Code als Erfolg, wenn genügend Frames gelesen wurden.
- Goldbildfehler können als erfolgreich gespeichertes, aber unvollständiges Goldsample enden.
- Räumlich getrennte Schäden können über Folgeframes ihre Identität wechseln.
- Das Goldinventar und die SQLite-/JSON-Samplemengen sind nicht synchron.

## 7. Noten

Schweizer Skala: 6 = sehr gut, 4 = genügend.

| Bereich | Note | Begründung |
|---|---:|---|
| Codequalität | **4,0** | Viele kleine Controller und fokussierte Tests, aber mehrere Schutzketten enden zu früh oder behandeln Fehler nur als Logmeldung. |
| Sicherheit | **3,0** | Konkreter fremder Löschpfad, unvollständiger Junction-Schutz und unzureichend gehärteter ZIP-/Modellimport. |
| KI-Pipeline | **3,5** | NoFinding und Teilfehler verbessert; Klassifikator fehlt, Totalausfallstatus und GPU-Realtests bleiben offen. |
| Datenintegrität | **4,0** | SQLite technisch intakt und Register sauber, aber Inventar veraltet, ein DB-Duplikat und 13 Samples ohne SAM. |
| Testabdeckung | **4,0** | Sehr große grüne Suite und gute Fokustests; kritische Gegenbeispiele und GPU-Lauf fehlen. |
| Gesamtfreigabe | **3,5** | Für eine produktive Freigabe noch nicht genügend. |

## 8. Fünf wichtigste nächste Schritte

1. `PublishedTargets` kanonisch auf den Projektbereich begrenzen, absolute/`..`-Pfade und alle Reparse-Eltern ablehnen; destruktionsfreie Angriffstests ergänzen.
2. Für KI-Wissen ein eigenes `personal_gold_v1`-Profil bauen: Dateihashes, Gesamt-/Einzelgrößen, Verhältnis, Eintragszahl, Doppeltziele und vollständiges Staging vor Live-Änderungen.
3. Klassifikator-Health gegen echte Datei und SHA prüfen; fehlendes Gewicht entweder kontrolliert promoten oder `active.json` bereinigen und dessen ACL härten.
4. Reparse-Schutz in DirectoryMirror, Vollbackup, Manifest und Echtzeitspiegel fail-closed vor jeder Aufzählung, Bewegung und Löschung anwenden.
5. Fehlende Fachtests ergänzen: Goldbildfehler, Video-Totalausfall, unbekannter FFmpeg-Exit, Nullframe sowie zwei räumliche Schäden mit vertauschter Reihenfolge über Folgeframes.

## 9. Beweisstand

**Bewiesen:**

- Git-Schnappschuss und unveränderter Arbeitsstand.
- Alle genannten Build-/Testergebnisse.
- Die beschriebenen Codeketten.
- Aktuelle `C:\KI_BRAIN`-Zahlen, SQLite-Prüfung und fehlendes Klassifikatorgewicht.
- V2/V3-Dateien und `not_deployed`-Status der BCC-Kandidaten.

**Wahrscheinlich:**

- Identitätswechsel räumlich getrennter Schäden bei vertauschter Erkennungsreihenfolge.
- Ausnutzbarkeit der Junction-Lücken an weiteren, derzeit ungetesteten Backup-Pfaden.
- Lokale Codeausführung beim Laden eines manipulierten PyTorch-Modells, sofern ein nicht vertrauenswürdiges Modell einen aktiven Modellpfad erreicht.

**Nicht prüfbar:**

- DINO-/SAM-GPU-Vertrag: Timeout nach 600 Sekunden ohne abgeschlossenen Test.
- Maschinengebundener echter Video-Goldenvertrag mangels aktivierter Umgebungsparameter und Testvideo.
- Welche der bereits vorhandenen 201 Arbeitsbaumänderungen tatsächlich von Kimi stammen.