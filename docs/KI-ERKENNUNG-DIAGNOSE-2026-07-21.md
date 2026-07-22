# KI-Erkennung: Diagnose & Weg nach vorn (2026-07-21)

> Erstellt, weil „es wird nicht viel oder das Falsche erkannt" — und die Überlegung
> aufkam, das „Gehirn" (KnowledgeBase) zu löschen und alles von Hand als Gold zu sichern.
> **Kernbotschaft: Beides wäre falsch gewesen.** Nichts wurde verändert; alle Zahlen sind
> aus einem rein lesenden Eval-Lauf gegen das 120er-Set.

## 1. Zuerst die Entwarnung

- **Deine manuelle Arbeit ist intakt:** `teacher_annotations.json` (707 Bilder / 673 Labels),
  88 annotierte Gold-Frames. Nichts verloren.
- **Der Sidecar läuft, Multi-Modell ist an** → deine trainierte Pipeline wird *tatsächlich*
  benutzt (nicht nur Qwen). Es ist nicht „aus".
- **Das „Gehirn" (KnowledgeBase.db) macht nur Few-Shot-Textbeispiele für Qwen** — es
  verbessert die *Erkennung* nicht. Löschen hätte an „wenig/falsch erkannt" **nichts** geändert.

## 2. Die eigentliche Ursache: Granularitäts-Fehlpassung

Das aktive Erkennungsmodell (`vsa_cls_v5_nocrop`, 10.06.) kennt nur **11 grobe Klassen**:
`BAB, BAF, BAI, BAJ, BBA, BBB, BCD, BCE, BDA, BDD, LEER`.
Das Eval-Set erwartet **feine Codes** (BDDC, BAIZ, BAHC, BAFCE, …).

| Messebene | Top-1 | Top-5 |
|---|---:|---:|
| Feincode (BDDC, BAIZ …) | 21,7 % | 31,7 % |
| **Hauptcode** (BAIZ→BAI, `kein_schaden`==`LEER`) | **48,3 %** | **69,2 %** |

Mehr als die Hälfte des scheinbaren Versagens war ein **Mess-/Namensartefakt**:
- **68 % der Eval-Frames** tragen Codes, die das Modell gar nicht ausgeben kann → automatisch „falsch".
- Auf Hauptcode-Ebene trifft das Modell oft die **richtige Familie** (BAIZ→BAI, BDDC→BDD).
- `kein_schaden` vs. `LEER` ist dasselbe unter zwei Namen.

**Wo Codes + Daten passen, ist das Modell gut:** BCD 100 %, BAI 92 %, BDA 58 %, BDD 50 %.

## 3. Kein billiger Gewinn durch neueres Modell

Deine neueren Modelle (`pdfplus_v1/v2`, 18./19.07.) haben **exakt dieselben 11 Klassen**
und sind auf dem Eval **nicht besser**:

| Modell | Feincode-Top1 | Hauptcode-Top1 | LEER korrekt |
|---|---:|---:|---:|
| **aktiv `v5_nocrop`** | 21,7 % | **48,3 %** | 12/30 |
| `pdfplus_v2` | 20,8 % | 47,5 % | 10/30 |

→ Das aktive Modell ist schon das beste. **Alle Modelle kleben an ~48 % Hauptcode.**
Das ist eine **strukturelle Grenze (Taxonomie + Daten)**, kein „falsches Modell eingesetzt".

## 4. Die drei echten Probleme (nach Hebel)

1. **Taxonomie-Ausrichtung — größter Hebel, KEIN Label-Problem.**
   Solange Fein-Codes nicht auf die 11 Modellklassen abgebildet sind (das in `CLAUDE.md`
   als *pending* markierte `detect_class_migration_v2`) und `LEER`≠`kein_schaden` gilt,
   wirkt alles ~2× schlechter, als das Modell ist. **Zudem hat das Modell Codes wie
   BAH, BCA, BAA, BCC gar nicht** — die brauchen eine bewusste Taxonomie-Erweiterung.

2. **Fehlalarme auf sauberen Rohren (LEER nur 40 %).**
   60 % der leeren Frames werden als Schaden gelesen (→ BDA/BCE/BAI). Echter Schwachpunkt.
   → Gezielt **mehr saubere/leere Frames** ins Training.

3. **BAI ist ein „Magnet" (Klassen-Unwucht).**
   Bei Unsicherheit rät das Modell BAI (BCE→BAI, BAH→BAI, BAB→BAI …).
   → Gezielt Daten für die unterrepräsentierten Codes.

## 5. Was NICHT zu tun ist

- **Das „Gehirn" (KnowledgeBase) löschen** — ändert an der Erkennung nichts.
- **Alles von Hand als Gold neu sichern** — falscher Aufwand; das Nadelöhr ist Taxonomie +
  zwei gezielte Datenlücken, nicht fehlende Masse.

## 6. Empfehlung (eine Entscheidung zuerst)

**Der eine wichtige Schritt ist eine Produkt-Entscheidung, kein Labeln:**

> **Auf welcher Code-Granularität soll das System arbeiten?**

- **Weg A — grob (empfohlen als Start):** Die 11 groben Klassen als Ziel akzeptieren,
  `detect_class_migration_v2` fachlich freigeben (Fein→Grob-Mapping), `kein_schaden`/`LEER`
  vereinheitlichen. Effekt: Das System wirkt sofort ~2× besser (48 % statt 22 %), Fehlalarme
  werden ehrlich messbar — **ohne ein einziges neues Label.** Danach gezielt saubere Frames
  gegen die LEER-Fehlalarme nachlegen.
- **Weg B — fein:** Feine Codes als Ziel. Das ist ein echtes Daten-/Trainingsprojekt für die
  fehlenden/feinen Klassen — über den **Prüfplatz** (nicht von null), und **nur**, wenn der
  fachliche Bedarf das rechtfertigt.

**Prozess-Hinweis (aus deinen eigenen Regeln):** Class-Migration **und** Trainings-Export sind
derzeit bewusst gesperrt. Ein Teil von „Verbesserungen landen nicht" ist *by design* — diese
Sperren müssen erst fachlich freigegeben werden.

## 7. Datenlage (nichts verloren)

- `training_samples.json` heute = ~41 echte Prüfplatz-Samples (`wb_…`) + 1 Platzhalter;
  große Alt-Stände liegen als Backups (April 24 MB, Juni 51 MB `pre-dedup`).
- `KnowledgeBase.db` 80 MB (Juni 195 MB) — passende Backups `pre-dedup`/`rejected-cleanup`
  vorhanden → frühere bewusste Bereinigung, kein Datenverlust.
- Detail-CSV des Eval-Laufs: alle 120 Frames mit wahr/vorhergesagt (im Scratchpad erzeugt).
