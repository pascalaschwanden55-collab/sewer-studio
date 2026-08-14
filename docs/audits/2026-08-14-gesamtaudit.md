# Gesamtaudit SewerStudio — 2026-08-14

**Stand:** Auditbasis `21f7b0194`; Sicherheits- und Grossumbauten bis `b6e640f2f`,
Branch `feature/eval-pruefsatz-review`.
**Methode:** Vollstaendiger Release-Build, alle Testprojekte, Sidecar- und
QGIS-Bruecken-Tests, Abhaengigkeitspruefung .NET und Python, Quellcode-Durchsicht der
sicherheits- und datenkritischen Wege, Pruefung der Programmsicherungs-ZIP.
**Nicht praktisch getestet:** echte GPU-Modelle, reale Kundenimporte, Ollama/Qwen,
QGIS als laufendes Programm, vollstaendige Wiederherstellung auf einem frischen Computer.

## Gesamturteil

SewerStudio ist architektonisch solide aufgebaut und sehr gut automatisiert getestet.
Die normalen Projekt-, Import-, Video-, Codierungs- und Exportfunktionen wirken
grundsaetzlich stabil. Die Freigabe ist trotzdem nur eingeschraenkt:

| Bereich | Urteil |
|---|---|
| Manuelle Nutzung mit menschlicher Kontrolle | Geeignet |
| Automatische KI-Schadenerkennung | Noch nicht produktionsreif |
| Release-Build | Bestanden |
| .NET-Sicherheit | Gut |
| Python-Abhaengigkeiten | Dringender Handlungsbedarf |
| ZIP als zusaetzliche Programmsicherung | Geeignet |
| ZIP als einzige vollstaendige Sicherung | Nicht geeignet |

## Messergebnisse

- Vollstaendiger Release-Build: 0 Fehler, 0 Warnungen
- .NET-Tests: 11.899 bestanden, 4 uebersprungen
- Python-Sidecar: 280 bestanden, GPU-Tests bewusst ausgeschlossen
- QGIS-Bruecke: 10 bestanden
- Insgesamt: 12.189 bestandene Tests
- Verwundbare NuGet-Pakete: keine gefunden
- Offensichtliche Zugangsdaten im aktuellen Quellcode: keine gefunden
- Keine konkrete SQL-Injection, unsichere Zertifikatspruefung oder gefaehrliche
  .NET-Deserialisierung gefunden

## Prioritaet 1

### P1-1 Python-Abhaengigkeiten enthalten bekannte Sicherheitsluecken

Betroffen ist die produktive Sperrdatei `sidecar/requirements-lock.txt`. Die CI
installiert dagegen lose Versionen aus `pyproject.toml` und prueft damit nicht exakt
die produktive Umgebung (`.github/workflows/ci.yml`).

Empfehlung: Pakete kontrolliert aktualisieren, RTX-5090-/CUDA-128-Kompatibilitaet von
Torch erhalten, `pip-audit` und `dotnet list package --vulnerable` als CI-Sperre
ergaenzen, Produktions-Lock zusaetzlich absichern.

### P1-2 Programmsicherung kann unvollstaendig sein und trotzdem „erfolgreich" melden

Wenn ein Ordner nicht gelesen werden kann, wird die Ausnahme nur protokolliert und der
gesamte Ordner still uebersprungen (`ProgramSnapshotService`). Anschliessend wird
trotzdem `Success=true` zurueckgegeben und „Momentaufnahme erstellt" angezeigt.

Empfehlung: unlesbare Ordner im Ergebnis und Manifest auffuehren; bei wichtigen Ordnern
wie `src`, `.git`, `sidecar` und Modellgewichten die Sicherung fehlschlagen lassen; ZIP
nach Erstellung erneut oeffnen und pruefen; SHA-256-Pruefsumme im Manifest speichern.

### P1-3 QGIS-Bruecke laeuft standardmaessig ohne Anmeldung

Die Bruecke lauscht nur lokal, wird aber standardmaessig gestartet und verwendet bewusst
kein Token (`QgisBridgeServer`). Damit kann jedes lokale Programm Projekt- und Geodaten
abrufen. Zusaetzlich werden interne Fehlermeldungen direkt zurueckgegeben
(`QgisBridgeRequestProcessor`).

Empfehlung: gemeinsames Zugriffstoken wie beim Sidecar; alternativ Bruecke
standardmaessig deaktivieren; nach aussen nur neutrale Fehlermeldungen liefern.

### P1-4 Gruene KI-Ampel basiert teilweise auf denselben Informationen

Fuer Gruen werden zwei vorhandene Werte verlangt, aber nicht zwei unabhaengige Belege
(`QualityGateService`). In der Protokollerzeugung werden LLM-Sicherheit und
Plausibilitaetswert aus derselben Pruefung uebernommen
(`FullProtocolGenerationService`). Auch die Aehnlichkeit der Beispiele, die bereits das
LLM beeinflusst haben, wird mitgezaehlt.

Positiv: Die zentrale automatische Freigabe verlangt zusaetzlich einen unabhaengigen
Datenbankabgleich (`AiDecisionPolicy`). Die Pipeline uebernimmt weiterhin nichts ohne
Menschen.

Empfehlung: nach Belegquellen gruppieren statt Felder zaehlen; LLM-Sicherheit und
daraus abgeleitete Plausibilitaet nur einmal werten; Prompt-Beispiele nicht als
unabhaengigen Beleg zaehlen; „sicher" durch „KI-Kriterien erfuellt — pruefen" ersetzen;
Regressionstest gegen die Doppelzaehlung.

### P1-5 Ein-Knopf-Import ist nicht vollstaendig transaktional

Die Projektdaten werden zwar erst auf einer Kopie bearbeitet. Archivierung und
Medienverteilung schreiben jedoch schon vorher direkt in den Projektordner
(`ProjectImportOrchestrator`). Bei Fehler, Projektwechsel oder fehlgeschlagenem
Speichern koennen Dateien zurueckbleiben, obwohl das Projektergebnis verworfen wurde.

Empfehlung: den vorhandenen `ImportFileStagingSession` auch fuer Ein-Knopf-, Medien- und
Schachtimporte verwenden und erst am Ende veroeffentlichen.

## Prioritaet 2

- **CSV-Formeln:** CSV-Escaping behandelt Trennzeichen, aber nicht gefaehrliche Anfaenge
  wie `=`, `+`, `-` oder `@` (`CsvExcelExportService`). Importierte Werte koennten beim
  Oeffnen in Excel als Formel ausgefuehrt werden.
- **Medienpfade:** Der Protokolleditor akzeptiert vorhandene absolute Pfade und relative
  Pfade mit `..` (`ProtocolEntryEditorMediaPathResolver`). Standardmaessig auf den
  Projektordner begrenzen.
- **`async void`:** `ApplyAndClose()` kann unbehandelte Ausnahmen an die WPF-Oberflaeche
  weiterreichen (`VsaCodeExplorerWindow`).
- **UI-Schicht:** Aeltere Bereiche fuehren noch viel Datei-, Netzwerk-, Prozess- und
  Sicherungslogik direkt in der UI aus, besonders `KnowledgeBackupService`,
  `TrainingCenterStore`, QGIS und einzelne Exporte.
- **CI-Reproduzierbarkeit:** NuGet-Restore verwendet nicht `--locked-mode`; der
  Python-Test verwendet nicht die produktive Sperrdatei; GitHub-Actions sind nur auf
  Versions-Tags festgelegt.
- **Testabdeckung:** Die Testmenge ist hervorragend, es gibt aber keine gemessene
  Zeilen-/Zweigabdeckung und keine Mindestgrenze. Vier wichtige Integrationspruefungen
  sind standardmaessig uebersprungen.

## KI-Funktionsbewertung

Der normale Detektor ist richtigerweise gesperrt (`sidecar/models/model_qualification.json`).
Die gemessene allgemeine Erkennung liegt bei Precision 37,9 %, Recall 10,3 %, F1 16,2 %
(36 richtige Treffer, 59 Fehlalarme, 314 verpasste Schaeden) — Quelle
`docs/quality/DETECT-RELEASE-DIAGNOSTIC-2026-08-03.md`.

Der Bogen-Assistent erreicht etwa 77,6 % Recall und ungefaehr 60 % brauchbare
Vorschlaege (`docs/quality/BCC-PDF-RECALL-2026-08-09.md`). Als Hilfsmittel mit
Pflichtkontrolle brauchbar, aber keine Modellfreigabe.

Empfehlung: KI weiterhin nur als Vorschlags- und Kontrollwerkzeug verwenden.

## Pruefung der Programmsicherungs-ZIP

`E:\SewerStudio_Programm_2026-08-13.zip` ist technisch lesbar:

- Groesse 5.219.003.102 Byte, 12.251 Eintraege
- Alle Dateien vollstaendig gelesen, CRC-Pruefsummen bestanden
- Keine doppelten Archivnamen, keine gefaehrlichen Pfade wie `..\`
- Manifest, Quellcode, Git-Verlauf und Modellgewichte vorhanden
- SHA-256 `05C9E40C1C093E9ACDF117A73B359121B8AE49E0D2D9F7469D44FFCA98CCB9E3`

Einschraenkungen: Es ist eine Programm-Momentaufnahme, keine fertige Installation
(Build-Ausgaben, Python-Umgebung und Kartenkacheln fehlen bewusst); sie ist sechs
Commits aelter als der Auditstand; das Manifest nennt Commit `54529e6180`, enthaelt aber
auch damals nicht eingecheckte Dateien, der Commit identifiziert den Inhalt daher nicht
eindeutig; rund 55 MB `.claude/worktrees` und lokale Claude/Codex-Einstellungen wurden
unnoetig mitgesichert; die ZIP ist nicht verschluesselt und enthaelt den vollstaendigen
Git-Verlauf sowie Modellgewichte; Kundenprojekte und externe QGIS-Profile sind nicht
enthalten.

## Empfohlene Reihenfolge

1. Python-Sicherheitsluecken und CI-Abhaengigkeitspruefung beheben.
2. Unvollstaendige Programmsicherungen zuverlaessig erkennen.
3. QGIS-Bruecke authentifizieren.
4. KI-Ampel auf unabhaengige Belegquellen umstellen.
5. Ein-Knopf-Import vollstaendig ueber Dateistaging absichern.
6. CSV- und Medienpfade haerten.
7. Danach UI-Dateilogik schrittweise auslagern und echte Wiederherstellungstests
   automatisieren.

## Umsetzungsstand 2026-08-14

Ergaenzende Messung zu P1-1 am selben Tag mit `pip-audit 2.9.0` gegen die reinen
PyPI-Pins der Sperrdatei: 40 bekannte Luecken in 10 Paketen (`certifi`, `idna`, `onnx`,
`pillow`, `pydantic-settings`, `requests`, `urllib3`, `setuptools`, `starlette`,
`transformers`). Die im Bericht genannte Zahl 58 stammt aus einer weiter gefassten
Zaehlung; die betroffene Paketliste ist identisch.

### Behoben

**P1-1 Python-Abhaengigkeiten.** Von 40 Luecken in 10 Paketen auf **5 in 2 Paketen**.
torch/torchvision/tensorrt blieben unangetastet. Nach dem Update geprueft: CUDA
verfuegbar und sm_120 erkannt, numpy-Bruecke mit numpy 2.5.2 in Ordnung, 273
Sidecar-Tests gruen, echter Grounding-DINO-Lauf auf drei Bildern mit **identischen**
Treffern. Rueckweg: `sidecar/requirements-lock.pre-security-2026-08-14-backup.txt`.

Zwei Ausnahmen bleiben belegt offen und stehen mit Grund in
`sidecar/security/lock_audit_exceptions.json`:

- `transformers` 4.57.6 — die Version 5.3.0 behebt zwei der vier Luecken, bricht aber
  Grounding DINO. Real getestet: `AttributeError: 'BertModel' object has no attribute
  'get_head_mask'` in `groundingdino/models/GroundingDINO/bertwarper.py:29`, 0 statt 1
  Treffer. Zwei der vier Luecken haben ueberhaupt keine Fix-Version.
- `setuptools` 81.0.0 — der Fix waere 83.0.0, aber torch verlangt `setuptools<82`.

Neu: `sidecar/security/audit_lock.py` prueft die produktive Sperrdatei in der CI und
wird auch bei einer **veralteten** Ausnahme rot. `.github/scripts/check-dotnet-vulnerable.ps1`
prueft die NuGet-Seite und wertet dafuer JSON aus — die Textmeldung ist uebersetzt, ein
englischer Textvergleich hatte nie etwas gefunden.

**P1-2 Programmsicherung.** Unlesbare Ordner erscheinen in Ergebnis, Manifest und
Dialog; bei einem unersetzlichen Ordner (`src`, `tests`, `tools`, `sidecar`, `.git`)
schlaegt die Sicherung fehl, statt erfolgreich zu melden. Die fertige ZIP wird vor der
Veroeffentlichung vollstaendig nachgeprueft — mit **selbst nachgerechneter CRC-Summe**,
weil `System.IO.Compression` beim Lesen keine Pruefsumme kontrolliert; reines Durchlesen
haette einen Bitfehler in einer unkomprimiert abgelegten Modellgewichtsdatei nicht
bemerkt. Die SHA-256 der Sicherung liegt als `<name>.zip.sha256` daneben. Das Manifest
sagt jetzt ausdruecklich, dass der Git-Commit den Inhalt nicht eindeutig identifiziert.

**P1-3 QGIS-Bruecke.** Token-Pflicht auf beiden Wegen (eigener Server und Live-Control
auf demselben Port). Das Plugin liest den Token selbst aus `.qgis_bridge_token` im
AppData-Ordner und sendet ihn als `X-QGIS-Bridge-Token`; es ist nichts einzurichten.
Fehlermeldungen nach aussen sind neutral, Einzelheiten nur im Protokoll.

**P1-4 KI-Ampel.** Gruen verlangt zwei unabhaengige **Belegquellen**. Sprachmodell, die
daraus abgeleitete Plausibilitaet, die Bildbeschreibung desselben Modells und die
Aehnlichkeit der Prompt-Beispiele zaehlen zusammen als eine Quelle
(`EvidenceSourceGrouping`). Der Anzeigetext heisst „KI-Kriterien erfüllt – prüfen"
statt „Sicher". Die Gewichtung im Zahlenwert ist bewusst unveraendert.

**P2 Weitere.** CSV-Formelzellen werden zentral entschaerft (`CsvCell`), Medienpfade des
Protokolleditors sind auf Mediendateien in erlaubten Wurzeln begrenzt, `ApplyAndClose`
ist ein `Task` mit Fehleranzeige, die CI restauriert gesperrt und mit gepinnten Actions,
und die Testabdeckung ist nach den Grossumbauten neu gemessen: **45,60 %**
(292.503 von 641.408 instrumentierten Zeilen). Der Ratchet verlangte deshalb die
Anhebung der Untergrenze von 42 % auf **45,60 %**. Die abweichende Gesamtzeilenzahl
gegenueber der Erstmessung zeigt, dass der SDK-Sammler nur als CI-Verlaufswert taugt.
Ein Waechter haelt die sechs zulaessigen
Skip-Stellen namentlich fest.

### Nachgelagerte Grossumbauten umgesetzt

**P1-5 Ein-Knopf-Import vollstaendig gestaged.** Der vorhandene Marker wurde nicht
dupliziert: `ImportFileTransaction` steuert fuer manuellen und Ein-Knopf-Import denselben
Ablauf aus Marker schreiben, `Publish`, Projekt-TxId speichern und aufraeumen. Archiv,
Plan-PDF, Medien, namensbasierte Protokolle, Kanal und Dichtheit verwenden dieselbe
`IImportFileStagingSession`. `ResolveReadPath` und `EnumerateReadableFiles` erlauben den
Folgeschritten, vorbereitete Dateien vor der Veroeffentlichung zu lesen;
`StageGeneratedFile` nimmt neu erzeugte PDF-Seiten auf. Das alte
`IImportedFileLedger` bleibt nur bis unmittelbar vor `Publish` ein Sicherheitsnetz und
kann danach keine veroeffentlichten Dateien loeschen. Absturzfaelle werden beim naechsten
Projektstart ueber `.import-transaction.json` und `LastCommittedImportTxId` entschieden.

**Manuelle Schachtverteilung.** `IShaftDistributionService` kapselt den alten Splitter.
Projektinterne Ziele laufen ueber dasselbe Staging und denselben Marker; die 1.000-Zeilen-
Grenze wurde durch `ExportPageViewModel.ShaftDistribution.cs` eingehalten, nicht
aufgeweicht. Bewusst externe Zielordner bleiben direkte Exporte, weil der Projektmarker
dort keine Dateien loeschen darf.

**Training-Center-Dateispeicher.** `TrainingCenterStore` bleibt am eingefrorenen UI-Ort
und behaelt Konstruktor, Zustand und Aufrufer, ist aber nur noch eine 61-zeilige
Mapping-Fassade. `ITrainingCenterDocumentStore` und
`TrainingCenterDocumentFileStore` liegen in Application/Infrastructure. Tests belegen
das bestehende JSON mit numerischem Status, alle Felder, atomaren Austausch, `.bak`-
Rueckfall und `.bad_<Zeit>`-Quarantaene.

**Wissenssicherung.** Die 565-zeilige `KnowledgeBackupEngine`, der Dateikatalog und die
Import-Nachbearbeitung liegen jetzt unter `Infrastructure/Ai/Backup` statt in der UI.
Die oeffentliche `KnowledgeBackupService`-Fassade samt verschachteltem `BackupResult`
und ihren Aufrufern blieb erhalten. Wissens- und Vollsicherung teilen dieselbe zentrale
SQLite-Snapshot-Instanz. Export, Import, Abbruch, Manifest, Pfadschutz, Rollback und
SQLite-Integritaet sind durch die bestehenden und ergaenzten Tests abgesichert.

Damit sind die beiden im Erstaudit bewusst zurueckgestellten Grossumbauten erledigt.
Der zentrale `ServiceProviderRegistrationMap` enthaelt nun 141 bewusst gepruefte
Vertragstypen.
