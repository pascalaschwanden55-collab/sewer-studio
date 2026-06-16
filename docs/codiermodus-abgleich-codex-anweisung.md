# Anweisung für Codex: KI-vs-Original-Abgleich im Codiermodus sichtbar machen

**Revier:** UI (`PlayerWindow.Coding.cs`, `PlayerWindow.CodingSidePanelAccessors.cs`, `PlayerCodingSidePanel.xaml`) — dein Bereich.
**Die Logik ist fertig und getestet** (Claude, Application-Schicht). Du baust nur die sichtbare Anbindung.
**Wichtig:** additiv arbeiten, nichts Bestehendes umbauen. Kommentare auf Deutsch.

## Was schon da ist (NICHT neu bauen)

Geteilte, reine Logik in `src/AuswertungPro.Next.Application/Ai/Evaluation/`:

- `BefundMatcher` — gestufter Meter-Abgleich (grün ≤0.20 m / gelb ≤0.50 m), Eins-zu-eins, vier Töpfe.
- `CodingProtocolMatchService` — die Methode, die du aufrufst.
- `CodingMatchRouting` — das schon fertig geroutete Ergebnis.

```csharp
using AuswertungPro.Next.Application.Ai.Evaluation;

// original = importierte Referenz, ki = KI-erkannte Befunde
CodingMatchRouting r = CodingProtocolMatchService.Match(
    _codingImportEvents.Select(ev => ev.Entry).ToList(),   // Original/Referenz
    _codingVm.Events.Select(ev => ev.Entry).ToList());     // KI
```

`CodingMatchRouting` liefert:

| Feld | Bedeutung | UX-Vorschlag |
|------|-----------|--------------|
| `Trainingskandidaten` | grüne Treffer (KI == Original, ≤0.20 m) | grünes Badge, „als Training"-Vorschlag |
| `ReviewGelb` | gelbe Treffer (≤0.50 m) | gelbes Badge, „kurz prüfen" |
| `FalscherCodeReview` | richtige Stelle, falscher Code | oranges Badge, „korrigieren" (Trainings-Gold) |
| `Verpasst` | Original ohne KI-Partner | graues Badge auf dem **Import**-Eintrag |
| `Fehlalarm` | KI ohne Original-Partner | rotes Badge auf dem **KI**-Eintrag |
| `Match.Precision` / `Match.Recall` | Kennzahlen | in die Kopfzeile |

Die Paare (`BefundMatchPair`) haben `.Gt` (Original) und `.Ki` (KI), je mit `.Tier` ("gruen"/"gelb") und `.Gap` (Meter-Abstand).

## Wie du ein Topf-Element auf sein CodingEvent zurückführst

Jeder `BefundMatchFinding` trägt **`RefId` = `EntryId.ToString()`**. Damit findest du das passende Event:

```csharp
CodingEvent? FindKiEvent(string? refId) =>
    _codingVm.Events.FirstOrDefault(ev => ev.Entry.EntryId.ToString() == refId);
CodingEvent? FindImportEvent(string? refId) =>
    _codingImportEvents.FirstOrDefault(ev => ev.Entry.EntryId.ToString() == refId);

// Beispiel: grüne Treffer einfärben
foreach (var pair in r.Trainingskandidaten)
{
    var kiEv = FindKiEvent(pair.Ki.RefId);          // KI-Spalte: grün
    var impEv = FindImportEvent(pair.Gt.RefId);     // Import-Spalte: grün
    // ... Badge/Hintergrund setzen ...
}
// Fehlalarm (nur KI):   r.Fehlalarm -> FindKiEvent(f.RefId)   -> rot
// Verpasst (nur Import): r.Verpasst -> FindImportEvent(f.RefId) -> grau
```

## Andock-Punkte (bereits vorhanden)

- **KI-Befunde:** `_codingVm.Events` → `LstCodingEvents`, Zähler `RunCodingDefectCount`.
- **Import-Referenz:** `_codingImportEvents` → `LstImportEvents`, Zähler `RunImportDefectCount`.
- **Training-Übernahme existiert schon:** `ImportConfirm_Click` (≈Zeile 2379) bestätigt den **selektierten** Import-Eintrag als `TeacherAnnotation` (Snapshot + teacher_images). Für „alle grünen Treffer übernehmen" am besten den Kern von `ImportConfirm_Click` in eine Methode `ConfirmImportAsTraining(CodingEvent importEvent)` herausziehen und sie pro grünem Treffer aufrufen.

## Konkrete Schritte (Vorschlag, additiv)

1. **Auslöser:** kleiner Button „Abgleich" in der Kopfzeile des Coding-Panels (neben dem QualityGate-Status, `TxtQualityGateStatus`). Optional zusätzlich automatisch nach jedem Import-Laden (`_codingImportEvents` neu befüllt, ≈Zeile 201) und nach `RefreshCodingEventsList()`.
2. **Methode** `RunCodingProtocolMatch()` in `PlayerWindow.Coding.cs`: ruft `CodingProtocolMatchService.Match(...)` auf, speichert das `CodingMatchRouting` in ein Feld `_lastCodingMatch`.
3. **Kopfzeile** aktualisieren: z.B. „Abgleich: {Treffer} ✓ ({grün}/{gelb}) · {FalscherCode} ⚠ · {Verpasst} fehlen · {Fehlalarm} extra · Recall {Recall:P0}".
4. **Einfärben:** über `RefId` (siehe oben) je Event ein farbiges Badge / Hintergrund setzen. Am einfachsten über einen kleinen Statusspeicher (Dictionary EntryId→Bucket), den dein ItemTemplate/Converter ausliest, oder direkt am ListBoxItem-Container.
5. **Optional** Button „Alle grünen Treffer als Training übernehmen": Schleife über `r.Trainingskandidaten`, je `ConfirmImportAsTraining(FindImportEvent(pair.Gt.RefId))`.

## Grenzen (bitte einhalten)

- **Nichts automatisch übernehmen.** Hoher Score = Vorschlag/Badge, kein Auto-Training (Leakage-Schutz). Übernahme nur per Klick.
- QualityGate-Green/Yellow/Red unangetastet lassen.
- Rein additiv — bestehende Coding-/Import-Logik nicht umbauen.
- Die Logik ist über `dotnet run --project tools/BefundMatcher -- --demo` (8/8) abgesichert; du verifizierst nur die UI.

## Rückfrage an Claude

Wenn `CodingMatchRouting` noch ein Feld braucht (z.B. die `Tier`-Info bei Verpasst/Fehlalarm, oder ein direktes `ProtocolEntry` statt `RefId`), sag Bescheid — die Application-Klasse ist meins, ich erweitere sie gern, statt dass du in der UI workaroundest.
