# Design: Trainingsmodule-Redesign (Review & Freigabe + Trainingsdaten & Modell)

**Datum:** 2026-06-02
**Status:** Entwurf zur Freigabe
**Branch-Kontext:** feature/gis-karte
**Grundlage:** Trainingscenter-Audit und Code-Gegenprüfung vom 2026-06-02

## 1. Problem / Ausgangslage

Das heutige Trainingscenter (`TrainingCenterWindow` + `TrainingCenterViewModel`, ~2200 Zeilen, 6 Tabs) ist **kein Fehlbau**, bringt aber im Ist-Zustand kaum Nutzen:

- Die **Knowledge Base ist real leer** (0 Samples / 0 Embeddings / 0 Versionen). Die Lern-Schleife ist verdrahtet, hat aber keinen Inhalt.
- Die Schleife **schließt sich technisch bis in die Produktion**: KB-Few-Shot fließt in echte Codier-Vorschläge ([FullProtocolGenerationService.cs:312](../../../src/AuswertungPro.Next.Infrastructure/Ai/FullProtocolGenerationService.cs), [OllamaProtocolAiService.cs:278](../../../src/AuswertungPro.Next.Infrastructure/Ai/OllamaProtocolAiService.cs)). → Wenn die KB gefüllt ist, wirkt sie wirklich.
- **NoFindings** (KI übersieht einen dokumentierten Schaden — der häufigste Anfangsfall) landet **weder in der Review-Queue noch in der KB** ([TrainingCenterViewModel.cs:2088](../../../src/AuswertungPro.Next.UI/ViewModels/Windows/TrainingCenterViewModel.cs)) → wird nicht gelernt.
- **Reject entfernt nichts aus der KB**: `DeindexSample` existiert ([KnowledgeBaseManager.cs:131](../../../src/AuswertungPro.Next.Infrastructure/Ai/KnowledgeBase/KnowledgeBaseManager.cs)), wird aber nie aufgerufen ([TrainingCenterViewModel.cs:783](../../../src/AuswertungPro.Next.UI/ViewModels/Windows/TrainingCenterViewModel.cs)).
- **Eval-Kontaminationsschutz lückenhaft**: zwei KB-Schreibpfade ohne Guard — [CodingSessionService.cs:218](../../../src/AuswertungPro.Next.Infrastructure/Ai/CodingSessionService.cs) und FeedbackIngestion via [TrainingCenterWindow.xaml.cs:225](../../../src/AuswertungPro.Next.UI/Views/Windows/TrainingCenterWindow.xaml.cs).
- **KB-Index prüft `TrainingEligible` nicht** ([KnowledgeBaseManager.cs:365](../../../src/AuswertungPro.Next.Infrastructure/Ai/KnowledgeBase/KnowledgeBaseManager.cs)) → untaugliche Samples (z.B. „missing-inspection-date") können trotzdem indexiert werden, obwohl der Export sie sperrt.
- **„Batch-Import + KB"** ist der prominente Standard-Knopf, lernt aber ungeprüft (Auto-Approve nur bei vorhandenem Frame, sofortiger Index, umgeht die Review-Politik).
- **YOLO-Export** erzeugt Dummy-Boxen (0.5/0.5/0.8/0.8) für Samples ohne echte Box → wertlos/schädlich fürs Detektor-Training.
- **Kein echter Fortschritts-Beweis** in der App: das eingefrorene Eval-Set + Benchmark-Scorer existieren, laufen aber nur als CLI; die UI-„Match-Rate" ist eine lauf-interne Vanity-Metrik.
- Tote/halbfertige Pfade: ungebundener `DistributeHaltungCommand`, ungebundene Live-Anzeigen, Tab „Fälle"-Buttons ohne Persistenz, Lehrer-Tab-Logik komplett im Code-Behind.

## 2. Ziel

Ein **einfaches, nicht überladenes** Trainings-UI mit **maximalem Lernwert** und **sicherer Human-in-the-loop-Kontrolle**, ohne Verschlechterung durch falsche Daten. **Die Engine bleibt** (KB, Retrieval, Embeddings, Self-Training-Gates, Stores, Eval-Schutz, Eval-Benchmark) — nur die Oberfläche wird auf **zwei klare Module** reduziert und die undichten Ränder werden geschlossen.

**Leitsatz (gilt überall):** *Erst prüfen → dann freigeben → dann lernt die KB.* Nichts geht ungeprüft in die KB.

## 3. Kernidee (vom User bestätigt)

Zwei Lernquellen füllen **dieselbe** KB:

1. **Geprüfte Protokolle** (tausende Haltungen, PDF/XTF/WinCan) liefern **Text + Code + Meter** → das ist die beste vorhandene Wahrheit und reicht für **Few-Shot-Code-Vorschläge** (kein Bild/keine KI nötig).
2. **Videos/Frames** liefern Bilder am Meterstand → über Review (+ optional Box) für **Bild-Erkennung/YOLO**.

Beide laufen über **Review/Freigabe** in die KB. Lernziel: **A** (beide Quellen — Protokolle als Bootstrap, Review für Bilder/Korrekturen). YOLO-Detektor-Training ist ein **Ziel** (Box-Werkzeug von Anfang an, aber **optional** pro Bild). Kandidaten-Reihenfolge: **KI-Fehler zuerst** (NoFindings + Mismatch).

## 4. Datenfluss

```
Quellen                         Module                                  Senken (Wirkung)
─────────────────────────────   ─────────────────────────────────────   ────────────────────────────
Geprüfte Protokolle (Text/Code) ─┐
                                 ├─▶ ① Review & Freigabe ──(freigegeben)─▶ KB (Few-Shot) ─▶ echte Codier-Vorschläge
Videos/Frames (Bild am Meter) ───┘     (ansehen, optional Box, ✓/✎/✕)                          (FullProtocol + Editor)
                                                          │
                                                          └──(mit echter Box)──▶ YOLO-Datensatz ─▶ besserer Bild-Detektor

② Trainingsdaten & Modell: Samples verwalten · Dubletten/Konflikte · YOLO-Export (nur echte Box) · Benchmark vorher/nachher · (Experten) Batch-Import über Review
```

## 5. Modul ① — „Review & Freigabe" (tägliches Werkzeug)

**Zielgruppe:** der Inspekteur, täglich. **Zweck:** Lern-Kandidaten schnell und sicher prüfen und freigeben.

**Layout:** links kompakte **Kandidaten-Liste** (priorisiert), rechts eine **große Karte**.

**Karte zeigt:** großes Frame-Bild · Protokoll-Code · Meter · KI-Aussage (z.B. „nichts erkannt") · Status (Kandidat). Darunter unauffällig: *„Optional: Box ziehen (nur für YOLO)"* — **Standard ist ohne Box**.

**Aktionen (Tastatur-Fluss):**
- **✓ Bestätigen (↵):** Protokoll-Code gilt → fließt als geprüftes Few-Shot in die KB; **+ YOLO-Label nur wenn eine echte Box gezogen wurde**.
- **✎ Korrigieren (K):** richtigen Code wählen, dann freigeben.
- **✕ Ablehnen (Entf):** wird nicht gelernt — **und entfernt einen evtl. vorhandenen alten KB-Eintrag** (siehe Modul ②/„Entfernen"-Semantik).
- **Pfeiltasten:** nächster/voriger Kandidat. Alles per Tastatur, kein Maus-Zwang.

**Kandidaten-Quellen (in die Queue):**
- **KI-Fehler** aus Self-Training: **NoFindings** (KI übersah dokumentierten Schaden) + **Mismatch/Partial**.
- **Protokoll-Startdaten** (Brücke ①↔②, siehe 6.): geprüfte Protokoll-Befunde als Kandidaten — *„Da sie aus geprüften Protokollen stammen, sind sie gute Kandidaten, werden aber trotzdem sichtbar freigegeben."* Für diese ist eine **sichtbare Sammel-Freigabe** möglich (kein Tausend-Klick-Zwang), aber explizit, nicht still.
- (Optional später) Codier-Modus-Captures aus dem Player.

**Kandidaten-Reihenfolge (Priorität):** **A — KI-Fehler zuerst** (NoFindings + Mismatch), danach Protokoll-Startdaten. Begründung: schnellster spürbarer Effekt, da genau die heute falschen Fälle zuerst korrigiert werden.

## 6. Brücke ①↔② — „Geprüfte Protokolle als Startdaten vorschlagen"

Eine Aktion, die vorhandene Protokoll-Befunde (Text + Code + Meter) als **Kandidaten in die Review-Queue (Modul ①)** legt — **nicht** in die KB. Konzeptionell **Review-Arbeit**, daher an Modul ① angedockt (auslösbar auch aus Modul ② als Pflege). Sammel-Freigabe ist möglich, aber sichtbar und bewusst.

## 7. Modul ② — „Trainingsdaten & Modell" (Pflege/Experten, selten)

**Zielgruppe:** Wartung/Kontrolle, nicht täglich. **Leitsatz prominent:** *Kein direktes Lernen ohne Review.*

1. **Samples verwalten:** Tabelle aller **geprüften** Samples; Filter nach Code/Quelle/Qualität; **Dubletten & Konflikte sichtbar** (gleiche Stelle, widersprüchlicher Code → markiert). **„Entfernen" setzt den Status auf *entfernt* (`Removed`) und löscht den KB-Eintrag** (nachvollziehbar — Soft-Status + Deindex, nicht nur hart weg). *Hinweis: `TrainingSampleStatus` kennt heute nur `New/Approved/Rejected` ([TrainingSampleModels.cs:10](../../../src/AuswertungPro.Next.Application/Ai/Training/TrainingSampleModels.cs)); der Wert `Removed` muss neu eingeführt werden — inkl. Persistenz/Migration der bestehenden JSON-Samples und Anpassung der UI-Filter.*
2. **YOLO-Export:** nur Samples mit **echter, gezeichneter Box**; Samples ohne Box werden **blockiert** (keine Dummy-Boxen). Vorab-Anzeige „X von Y Samples haben eine Box".
3. **Benchmark vorher/nachher:** Lauf gegen das **eingefrorene Eval-Set**, zeigt echten **Delta pro Code** (Recall/Treffer). **Der einzige echte Fortschritts-Beweis** — ersetzt die bunte Match-Rate.
4. **(Experten) Batch-Import:** **Kein Auto-Approve, kein Auto-Index — immer über die Review-Queue.** Versteckt, hinter Warnung. (Heute setzt er Auto-Approve allein bei vorhandenem Frame und indexiert sofort in die KB — genau dieses Verhalten wird entfernt.)

## 8. Sicherheits-Regeln (modulübergreifend)

- Erst prüfen → dann freigeben → dann lernt die KB.
- **Eval-Frames werden auf ALLEN KB-Schreibpfaden geblockt** (auch CodingSessionService + FeedbackIngestion).
- Samples ohne Inspektionsdatum / mit ungültigem Code kommen **weder in den Export noch in die KB** (TrainingEligible auch am KB-Index prüfen).
- **Ablehnen/Entfernen** räumt alte KB-Einträge mit weg (Deindex).
- **Fortschritt nur per Benchmark**, nicht per Prozentbalken.

## 9. Nötige Engine-Fixes (hinter der neuen UI)

Diese Fixes machen die Module erst sinnvoll (Reihenfolge ist Vorschlag für den Umsetzungsplan):

1. **Eval-Guard auf alle KB-Schreibpfade** — `evalImageHashes` an [CodingSessionService.cs:218](../../../src/AuswertungPro.Next.Infrastructure/Ai/CodingSessionService.cs) und [TrainingCenterWindow.xaml.cs:225](../../../src/AuswertungPro.Next.UI/Views/Windows/TrainingCenterWindow.xaml.cs) durchreichen. *(Sicherheit)*
2. **Reject/Entfernen → Deindex + neuer Status** — `RejectSampleAsync`/Review-Reject und „Entfernen" rufen `DeindexSample`. **Neuer Enum-Wert `TrainingSampleStatus.Removed`** (heute nur `New/Approved/Rejected`, [TrainingSampleModels.cs:10](../../../src/AuswertungPro.Next.Application/Ai/Training/TrainingSampleModels.cs)) inkl. Persistenz/Migration und UI-Filter. *(Korrektur wirksam + nachvollziehbar)*
3. **NoFindings in die Review-Queue** — Enqueue-Filter [TrainingCenterViewModel.cs:2088](../../../src/AuswertungPro.Next.UI/ViewModels/Windows/TrainingCenterViewModel.cs) um NoFindings erweitern (der policy-seitige `RouteToReview:true` muss konsumiert werden). *(häufigster Fall wird lernbar)*
4. **TrainingEligible am KB-Index prüfen** — `IsIndexWorthy` ([KnowledgeBaseManager.cs:365](../../../src/AuswertungPro.Next.Infrastructure/Ai/KnowledgeBase/KnowledgeBaseManager.cs)) prüft Eligibility; Export- und KB-Gate konsistent. *(keine untauglichen Samples in der KB)*
5. **Batch-Import + KB** aus der Standard-Top-Bar nehmen → Experten/über Review.
6. **YOLO-Export ohne echte Box blockieren** (Dummy-Box-Pfad entfernen, [TrainingCenterViewModel.cs:1080](../../../src/AuswertungPro.Next.UI/ViewModels/Windows/TrainingCenterViewModel.cs)).
7. **Benchmark vorher/nachher in der App** — feste Baseline-Datei + kleiner Delta-Vergleich gegen das Eval-Set (Scorer existiert bereits in tools/EvalSetBenchmark, in die App holen).
8. **„missing-inspection-date" klären** — warum fehlt das Datum (PDF/Import)? Sonst bleibt alles export-gesperrt.
9. **Box-Erfassung** im Review (`Bbox*`-Felder existieren im Model, werden aber nie gesetzt).

**Reihenfolge der Umsetzung (verbindlich für den Plan):**
1. **Engine-Sicherheit zuerst** — Eval-Guard auf alle KB-Schreibpfade (Fix 1), `TrainingEligible` am KB-Index (Fix 4), Deindex bei Reject/Remove + neuer `Removed`-Status (Fix 2), keine Dummy-Boxen / YOLO-Gate (Fix 6), Batch-Import entschärfen (Fix 5). *Macht die KB-Befüllung erst sicher — vor allem anderen.*
2. **Dann Modul ① „Review & Freigabe"** — inkl. NoFindings-Routing (Fix 3), Box-Erfassung (Fix 9), Kandidaten-Priorität „KI-Fehler zuerst", Startdaten-Brücke.
3. **Dann Modul ② „Trainingsdaten & Modell"** — Samples-Verwaltung/Dubletten, YOLO-Export, **Benchmark vorher/nachher** (Fix 7), Experten-Batch-Import.

Begründung der Reihenfolge: Bevor überhaupt etwas in die KB gelernt wird (Modul ①/②), müssen die Sicherheits- und Korrektur-Mechanismen stehen — sonst füllt man eine ungeschützte KB. „missing-inspection-date" (Fix 8) wird in Phase 1 als Untersuchung mitgeführt, da es Export/Eligibility blockiert.

## 10. Was bleibt / was wegfällt

**Bleibt (Engine, nicht neu bauen):** KB/Retrieval/Embeddings, Self-Training-Gates + KbDisagreement-Veto, Stores (atomar/Backup/Dedup), EvalContaminationGuard, Eval-Set-Benchmark (jetzt sichtbar).

**Fällt weg / wandert:** 6-Tab-Center → 2 Module; „Batch-Import + KB" als Standard-Knopf → Experten/Review; toter `DistributeHaltungCommand`; ungebundene Live-Anzeigen (`CurrentTechniqueDetails`, `LiveCaseInfo/CodeInfo/MeterInfo`); irreführende grüne „ExactMatch"-Färbung beim Batch-Import; Vanity-Match-Rate als „Fortschritt"; Legacy `RetrievalService.LoadAllEmbeddings`.

## 11. Architektur / Altitude

- **VM verschlanken:** KB-Index-Orchestrierung, Scan, Sample-Generierung gehören in einen Application/Infrastructure-Service, nicht ins 2200-Zeilen-ViewModel (Thin-UI/Layer-Disziplin).
- **Lehrer-Tab-Logik** aus dem Code-Behind in ein ViewModel (sofern der Lehrer-Tab erhalten bleibt — Scope im Plan klären).
- **Lebenszyklus sichtbar machen:** `Status`, `KbIndexState`, `TrainingEligible`, `MatchLevel` als sichtbare Spalten/Badges (Kandidat / geprüft / in KB indexiert / exportiert).

## 12. Nicht-Ziele (Scope-Grenzen)

- **Kein** Training/Fade von Modellgewichten zur Laufzeit (ADR-008; Self-Training füttert KB/Datensätze, kein In-App-Gewichtstraining).
- **Kein** kompletter Neubau der Engine.
- **Kein** ANN-Index und **kein** Cosine-Dedup beim Schreiben in dieser Iteration (später, wenn die KB wächst).
- **Keine** automatische Modell-Promotion/Rollback in dieser Iteration.

## 13. Tests / Messbarkeit

Neu/erweitert abzusichern (alle gut testbare Logik):
- NoFindings-Routing in die Review-Queue.
- Reject/Entfernen → Deindex (KB-Eintrag weg, Status „entfernt").
- Eval-Guard auf den zwei bisher offenen Schreibpfaden.
- TrainingEligible am KB-Index.
- YOLO-Export-Gate (ohne Box → blockiert).
- Benchmark-Vergleich (Baseline vs. neuer Lauf, Delta pro Code).
- ReviewQueue-Persistenz-Roundtrip (bisher ungetestet).

**Beweisbarkeit:** Echter Lernfortschritt wird ausschließlich über den **Eval-Set-Benchmark vorher/nachher** gezeigt; die lauf-interne Match-Rate ist kein Beweis und wird in der UX entsprechend entschärft.

## 14. Offene Fragen

1. **missing-inspection-date:** Wird das Inspektionsdatum aus PDF/Import nicht gelesen, oder fehlt es in den Daten? (Blockiert aktuell jeden Export.)
2. **Sammel-Freigabe Protokoll-Startdaten:** Wie groß sind die Mengen real, und reicht eine Sammel-Freigabe mit Stichproben-Sicht, oder braucht es Filter (pro Projekt/Code)?
3. **Lehrer-Tab:** erhalten (ins ViewModel ziehen) oder in Modul ②/Review aufgehen lassen?
4. **Migration:** ein-Schritt-Umbau des Fensters oder schrittweise (erst Engine-Fixes, dann UI)?

## 15. Verifikations-Hinweis

Alle Befunde in §1 sind im Trainingscenter-Audit (2026-06-02) am echten Code mit Datei:Zeile belegt und vom User stichprobenartig gegengeprüft. Die KB-Leere (0/0/0) wurde lokal bestätigt.
