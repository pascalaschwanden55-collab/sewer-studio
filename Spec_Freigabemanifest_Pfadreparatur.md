# Spec — Freigabemanifest Pfadreparatur (AP 0.1 → Reparaturlauf)

**Version:** 1.0 · **Datum:** 2026-07-16 · **Bezug:** `TrainingDataInventoryService`, Trainingsplan v1.2 (AP 0.1/0.3)
**Zweck:** Sichere Übernahme der **94 eindeutigen Pfadvorschläge** aus dem Inventarbericht — nur nach menschlicher Freigabe, ohne jemals `teacher_annotations.json` still zu verändern.

---

## 1. Grundprinzip: drei getrennte Phasen

```
[1] GENERIEREN (rein lesend)      → Manifest aus EINEM Report-RunId ableiten
        ↓  (Mensch prüft & entscheidet je Zeile)
[2] VERSIEGELN (Mensch)           → reviewedBy + reviewSealed=true + .sha256
        ↓
[3] REPARATURLAUF (schreibend)    → übernimmt NUR freigegebene, re-verifizierte Pfade
```

Kein Schritt darf den nächsten überspringen. Der Reparaturlauf vertraut **ausschließlich** einem versiegelten Manifest — nicht dem Report, nicht Dateinamen, nicht dem Dateisystem allein.

**Kernregel (aus der Inventar-README):** Ein gleicher Dateiname ist **kein** Beweis. Ohne einen *bestätigten Soll-Hash* wird nichts übernommen. Die menschliche Freigabe einer Zeile = Bestätigung genau dieses `suggestedSha256`.

---

## 2. Scope & Non-Goals

**In Scope:** genau die Records mit `FullFrame.State == SuggestedForManualReview` (die 94). Feld, das ggf. repariert wird: **nur** `FullFramePath` (Vollbild).

**Ausdrücklich NICHT (jeweils eigenes, späteres Manifest):**
- die **288 Herkunfts-Quarantäne** (`QuarantineOrigin`) — das ist eine Haltungs-/Herkunftsentscheidung, keine Pfadreparatur.
- die **30 Geometrie-Quarantäne** (`QuarantineGeometry`).
- **Ambiguous / ProtectedCandidate**-Referenzen (kein eindeutiger Vorschlag → gehören nie ins Freigabemanifest).
- alles mit `Disposition == EvaluationLocked` oder `EvalState != Clean` — Eval/Abnahme bleibt unberührt, auch als Vorschlag ausgeschlossen.

Ein Vorschlag, dessen `SuggestedPath` in einen `ProtectedRoots`/Eval-Ordner zeigt, wird bei der Generierung **hart verworfen** (mit Issue), nicht ins Manifest geschrieben.

---

## 3. Datenherkunft (Report → Manifest-Zeile)

Pro Manifest-Zeile aus `TeacherInventoryRecord`:

| Manifest-Feld | Quelle im Inventar-Modell |
|---|---|
| `recordKey` | `TeacherInventoryRecord.RecordKey` (stabiler Schlüssel) |
| `vsaCode` | `TeacherInventoryRecord.VsaCode` |
| `field` | fix `"FullFramePath"` |
| `storedPath` | `FullFrame.StoredPath` (aktuell gespeicherter, oft fehlender Pfad) |
| `suggestedPath` | `FullFrame.SuggestedPath` |
| `suggestedSha256` | `FullFrame.Sha256` (Hash der Vorschlagsdatei; **Pflicht**, sonst Zeile ungültig) |
| `pathState` | `FullFrame.State` (muss `SuggestedForManualReview` sein) |
| `disposition` | `TeacherInventoryRecord.Disposition` (muss ≠ `EvaluationLocked`) |
| `evalState` | `TeacherInventoryRecord.EvalState` (muss `Clean`) |
| `reasonCodes` | `TeacherInventoryRecord.ReasonCodes` (Kontext für den Prüfer) |

Fehlt `suggestedSha256` (z. B. Lauf mit `--no-hashes`), wird die Zeile **nicht** aufgenommen — ohne Soll-Hash keine Freigabe möglich.

---

## 4. Manifest-Schema

Zwei Dateien, analog zum Inventar: `…manifest.json` + `…manifest.json.sha256`. Ablage: `C:\KI_BRAIN\training\manifests\`.

```jsonc
{
  "schemaVersion": "1.0",
  "manifestKind": "fullframe-path-repair",
  "generatedUtc": "2026-07-16T19:55:00Z",
  "generatorVersion": "ap0.1-repair-v1",

  // Provenienz-Bindung: an GENAU einen Inventarlauf gekoppelt
  "sourceReport": {
    "path": "C:\\KI_BRAIN\\training\\reports\\training_inventory_20260716_195304_098.json",
    "runId": "….",                       // TrainingDataInventoryReport.RunId
    "reportSha256": "….",                // Hash des Report-JSON
    "scannerVersion": "ap0.1-v1"
  },

  // Zustands-Bindung: gegen stille Änderungen am Store seit Generierung
  "teacherStore": {
    "path": "C:\\KI_BRAIN\\…\\teacher_annotations.json",
    "sha256AtGeneration": "…."
  },

  // Freigabe-Status (von Phase 2 gesetzt)
  "review": {
    "reviewSealed": false,               // wird true bei Versiegelung
    "reviewedBy": null,
    "reviewedUtc": null
  },

  "entries": [
    {
      "recordKey": "…",
      "vsaCode": "BAB",
      "field": "FullFramePath",
      "storedPath": "…\\alt\\fehlt.jpg",
      "suggestedPath": "…\\gefunden\\frame_00123.jpg",
      "suggestedSha256": "…",
      "pathState": "SuggestedForManualReview",
      "disposition": "TrainValCandidate",
      "evalState": "Clean",
      "reasonCodes": ["unique-filename-match"],

      // ── vom Menschen in Phase 2 gesetzt ──
      "decision": "pending",             // pending | approved | rejected | deferred
      "reviewerNote": null
    }
    // … 94 Einträge
  ]
}
```

`decision` ist bei Generierung immer `pending`. Der Reparaturlauf fasst **nur** `approved` an.

---

## 5. Workflow & Kommandos (Stil wie `TrainingDataInventory`)

**Phase 1 — generieren (rein lesend):**
```powershell
dotnet run --project tools\TrainingDataPathRepair -c Release -- generate `
    --report C:\KI_BRAIN\training\reports\training_inventory_20260716_195304_098.json
```
Erzeugt `…manifest.json` + `.sha256`. Verändert nichts am Bestand.

**Phase 2 — Mensch prüft:** `decision` je Zeile auf `approved`/`rejected`/`deferred` setzen, optional `reviewerNote`. Danach versiegeln:
```powershell
dotnet run --project tools\TrainingDataPathRepair -c Release -- seal `
    --manifest …manifest.json --reviewer "Pascal"
```
Setzt `reviewSealed=true`, `reviewedBy`, `reviewedUtc`, schreibt frischen `.sha256` über den versiegelten Stand.

**Phase 3 — Reparatur (Standard = Dry-Run):**
```powershell
# zeigt nur, was passieren WÜRDE:
dotnet run --project tools\TrainingDataPathRepair -c Release -- apply --manifest …manifest.json
# echte Übernahme erst mit explizitem Flag:
dotnet run --project tools\TrainingDataPathRepair -c Release -- apply --manifest …manifest.json --commit
```

---

## 6. Reparaturlauf — Algorithmus & Verweigerungsgründe

Vor **jeder** Übernahme prüft der Lauf (bei Verstoß → Abbruch der Zeile bzw. des Laufs, protokolliert):

1. **Manifest-Integrität:** `.sha256` passt zum Manifest-Inhalt und `review.reviewSealed == true`. Sonst: kompletter Abbruch.
2. **Store unverändert:** aktueller SHA-256 von `teacher_annotations.json` == `teacherStore.sha256AtGeneration`. Bei Abweichung: Abbruch mit Hinweis „Store geändert → Inventar + Manifest neu erzeugen". (Verhindert Anwenden veralteter Entscheidungen.)
3. **Nur freigegeben:** `decision == approved`. `pending`/`deferred`/`rejected` werden übersprungen und gezählt.
4. **Datei existiert & Hash stimmt:** `suggestedPath` existiert und ihr **neu berechneter** SHA-256 == `suggestedSha256`. Bei Abweichung: Zeile verweigert (Datei hat sich seit Review geändert).
5. **Kein Eval/Protected:** `suggestedPath` liegt in keinem `ProtectedRoots`/Eval-Ordner; `evalState == Clean`, `disposition != EvaluationLocked`. (Doppelte Absicherung gegen manipuliertes Manifest.)
6. **Idempotenz:** ist `storedPath` des Records bereits == `suggestedPath`, No-Op (kein Schreiben, als „unchanged" gezählt).

**Schreiben** (nur wenn 1–5 bestanden und nicht idempotent):
- Vorher **Backup** von `teacher_annotations.json` (Zeitstempel), wie die vorhandene Store-Sicherung.
- Setzt **ausschließlich** `FullFramePath` des Records auf `suggestedPath`. Kein anderes Feld.
- **Report** schreiben: `…path_repair_report_<ts>.json` + `.sha256` unter `reports\` mit Zeilen `applied | skipped | refused | unchanged` inkl. Grund. Read-only-Prinzip der Auswertung bleibt.

**Rollback:** Backup + Reparatur-Report erlauben jederzeit die Rückkehr zum Vorzustand.

---

## 7. Architektur (nach CLAUDE.md-Checkliste)

- **Application (Interfaces + Modelle):** `IPathRepairManifestService` (generate/seal) und `IPathRepairService` (apply) im Namespace `…Application.Ai.Training.Inventory` (neben den vorhandenen Inventar-Modellen). Neues Modell `PathRepairManifest` + `PathRepairEntry` + `PathRepairResult`.
- **Infrastructure:** Implementierungen neben `TrainingDataInventoryService`; wiederverwenden: `TrainingInventoryPaths` (Pfad-Normalisierung/Out-Sandbox), `TrainingInventoryFileAccess` (SHA-256), `EvalContaminationGuard`, `TrainingInventoryPathResolver` (Protected-Roots).
- **DI:** beide Services im `ServiceProvider` registrieren (kein `new` verstreut).
- **Tool:** `tools\TrainingDataPathRepair` mit Subkommandos `generate|seal|apply` (Standard Dry-Run). `--out`/Manifest-/Report-Ziele müssen — wie beim Inventar — innerhalb `KI_BRAIN\training\` liegen.
- **Additiv:** kein Umbau am Inventardienst; das Manifest ist ein neuer, eigenständiger Baustein.

---

## 8. Definition of Done / Tests

Fokussierte Tests (Stil der 11 bestehenden Schutztests):

- [ ] **Generierung** nimmt nur `SuggestedForManualReview` + `EvalState==Clean` + `Disposition!=EvaluationLocked` auf; Ambiguous/Protected/fehlender Hash werden ausgeschlossen (mit Issue).
- [ ] **Kein-Hash-Lauf** erzeugt für betroffene Zeilen keinen Eintrag.
- [ ] **Seal** setzt Felder + `.sha256`; manipulierter Inhalt nach Seal → Integritätsfehler.
- [ ] **Apply verweigert**, wenn: nicht versiegelt / Store-Hash abweicht / Datei-Hash abweicht / Ziel in Protected-Root / `decision != approved`.
- [ ] **Apply idempotent:** zweiter Lauf ändert nichts (alles „unchanged").
- [ ] **Apply schreibt** genau `FullFramePath`, legt Backup an, erzeugt Report+`.sha256`.
- [ ] **Dry-Run** (ohne `--commit`) verändert nie eine Datei.
- [ ] Release-Build 0 Fehler/0 Warnungen; alle vier Testsammlungen grün.

---

## 9. Nächste Schritte

1. Application-Modelle + Interfaces anlegen (`PathRepairManifest`, Services).
2. `generate` + `seal` implementieren (rein lesend / nur Manifest schreiben) inkl. Tests.
3. `apply` mit Dry-Run-Default + allen Verweigerungsgründen inkl. Tests.
4. Erst danach: echten Lauf über die 94 Vorschläge (Review → Seal → Dry-Run → `--commit`).
5. Danach getrennt planen: Herkunfts-Quarantäne (288) als **eigenes** Manifest.
