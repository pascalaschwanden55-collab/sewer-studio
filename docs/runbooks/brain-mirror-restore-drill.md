# Runbook: Brain-Mirror Restore-Drill

**Roadmap-Eintrag:** P1.5 — eintägiger Restore-Test, Pflicht vor jedem produktiven Brain-Mirror-Wechsel.

## Zweck

Verifiziert, dass der Brain-Mirror (default `E:\Brain Sync`) eine **funktionierende Kopie** der KnowledgeBase + Frames enthält. Reine Existenz reicht nicht — Korruption durch Disk-Fehler oder Sync-Bugs würde nur beim echten Restore auffallen.

## Wann

- Einmal pro Quartal als Sanity-Check.
- Vor jedem Wechsel des Mirror-Laufwerks.
- Nach jedem grösseren Sidecar-/Modell-Update (KB-Schema-Changes).
- Sofort, wenn der Sync-Job einen Fehler gemeldet hat.

## Voraussetzungen

- Brain-Mirror erreichbar (Default `E:\Brain Sync`, override via `SEWERSTUDIO_BRAIN_MIRROR`-Env).
- `manifest.json` im Mirror (wird vom `KnowledgeMirrorService` geschrieben).
- Optional: `sqlite3.exe` im PATH für DB-Smoke.

## Ausführung

```powershell
pwsh ./scripts/brain-mirror-restore-drill.ps1
```

Oder mit explizitem Mirror-Pfad:

```powershell
pwsh ./scripts/brain-mirror-restore-drill.ps1 -MirrorPath "F:\altes-brain"
```

Mit erhaltenem Restore (für manuelle Inspektion):

```powershell
pwsh ./scripts/brain-mirror-restore-drill.ps1 -KeepRestore
```

## Was wird geprüft

1. **Mirror erreichbar** — Pfad existiert + `manifest.json` lesbar.
2. **Robocopy-Sync** in temp-Restore-Pfad.
3. **SHA256** der `knowledge_base.db` gegen `manifest.json`.
4. **SQLite-Smoke** — Tabellen + Zeilenzahl von `knowledge_entries` (best-effort, nur wenn `sqlite3.exe` im PATH).
5. **Frames-Sample** — 5 zufällige PNGs, Magic-Header geprüft.

## Soll-Ergebnis

Exit 0 + alle 5 Schritte mit `[PASS]`. Beispiel:

```
==> Schritt 1/5 - Mirror-Verfuegbarkeit
    [PASS] Mirror erreichbar; manifest.json gelesen
==> Schritt 2/5 - Restore-Kopie nach C:\Users\...\Temp\sewerstudio-restore-drill-...
    [PASS] Robocopy OK (Exit 1)
==> Schritt 3/5 - SHA256 KnowledgeBase.db
    [PASS] SHA256 OK (a1b2c3...)
==> Schritt 4/5 - SQLite-Smoke
    [PASS] knowledge_entries: 21437 Zeilen
==> Schritt 5/5 - Frames-Sample
    [PASS] PNG OK: sample-12345.png
    ...

=== Restore-Drill PASS (Dauer: 28s) ===
```

## Fehlerpfade

| Symptom | Mögliche Ursache | Aktion |
|---|---|---|
| `Mirror-Pfad nicht gefunden` | Externes Laufwerk nicht angesteckt | Laufwerk anstecken und erneut starten |
| `manifest.json fehlt` | Mirror nie vom `KnowledgeMirrorService` befüllt | `KnowledgeMirrorService.SyncAsync` einmal manuell triggern |
| `SHA256-Mismatch` | Disk-Korruption oder Schreibfehler | Mirror neu aufbauen, Quell-DB integritäts-prüfen (`PRAGMA integrity_check`) |
| `SQLite-Query fehlgeschlagen` | DB korrupt oder Schema-Mismatch | KB-Backup verwenden, Schema-Migration prüfen |
| `PNG-Header fehlt` | Frame-Datei korrupt | Source-Frame prüfen, Mirror neu aufbauen für betroffenes Sample |

## Dokumentationspflicht

Nach jedem Drill den Output (oder Pass/Fail + Datum) in einer kurzen Notiz festhalten — entweder im Issue-Tracker oder in `docs/runbooks/restore-drill-log.md`. Ein einzelner PASS allein reicht nicht — wichtig ist die zeitliche Abfolge, damit ein langsam degradierender Mirror früh auffällt.

## Verwandte Code-Stellen

- [`KnowledgeMirrorService.cs`](../../src/AuswertungPro.Next.UI/Services/KnowledgeMirrorService.cs) — schreibt `manifest.json` mit SHA256.
- [`KnowledgeBaseModule.StartBrainMirror`](../../src/AuswertungPro.Next.UI/Modules/KnowledgeBaseModule.cs) — startet den Background-Sync.
