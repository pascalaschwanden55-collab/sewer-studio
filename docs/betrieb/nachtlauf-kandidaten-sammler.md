# Nachtlauf als Kandidaten-Sammler

Stand: 2026-06-01. Betriebsregel fuer den unbeaufsichtigten Self-Training-/Nachtlauf.

## Grundsatz

Der Nachtlauf darf **Kandidaten sammeln, aber nicht selbststaendig lernen.**
Alles, was spaeter als Wahrheit, Trainingsdaten oder Benchmark-Grundlage dient, wird strenger
geprueft als eine normale KI-Antwort und braucht eine menschliche Bestaetigung.

## Was der Nachtlauf DARF

- Faelle analysieren (KI-Blindanalyse gegen das Protokoll vergleichen)
- Kandidaten erzeugen (saubere Treffer markieren)
- die ReviewQueue fuellen
- Gruende speichern (warum ein Fall geprueft werden muss)

## Was der Nachtlauf NICHT DARF

- automatisch auf Gold-Standard setzen
- automatisch in die KnowledgeBase indexieren
- automatisch Trainingsdaten freigeben
- Auto-Accept ohne menschliche Pruefung

## Die Sicherheitskette (eingebaut, getestet)

| Stufe | Regel | Commit |
|---|---|---|
| S1 | Eval-Set-Frames werden hart vom KB-Index blockiert (Benchmark bleibt ehrlich) | `923482d1` |
| S2 | ExactMatch nur bei sauberen 4 Achsen: Code + Meter (typabhaengige Toleranz) + Severity + Uhrlage | `3b9546e5` |
| S2b | RequireHumanReview = **Default true**: nichts wird automatisch Gold/indexiert | `acd8c1c9` |
| Weg 1 | KB-Widerspruch (KB-Mehrheit != KI-Code) = **Veto** -> zwingend Review | `7a34d70a` |

## Wie es zusammenwirkt

- **RequireHumanReview = true** (Default in `TrainingCenterSettings`): selbst ein sauberer
  4-Achsen-ExactMatch wird NICHT automatisch Gold. Er wird nur Kandidat und geht in die ReviewQueue
  (Grund: `HumanReviewRequired`).
- **4-Achsen-ExactMatch** ist damit nur noch ein *starkes Signal*, keine automatische Wahrheit.
- **KB-Disagreement** ist ein Veto: widerspricht die KB-Mehrheit dem KI-Code, geht der Fall zwingend
  in die ReviewQueue (Grund: `KbDisagreement`) — unabhaengig vom MatchLevel.
- **KB-Agreement** ist nur ein Kandidaten-Signal (kein Auto-Gold-Trigger); `RequireHumanReview`
  bleibt staerker.
- **Eval-Daten** koennen ueber S1 gar nicht erst in die KB gelangen.
- **Ergebnis**: alle unsicheren und alle zurueckgehaltenen Faelle landen mit Begruendung in der
  ReviewQueue. Der Mensch entscheidet, was Wahrheit wird.

## Freigabe-Status

- Nachtlauf als **reiner Kandidaten-Sammler**: vorsichtig erlaubt (RequireHumanReview=true).
- Echtes automatisches Self-Training: **gesperrt**, bis bewusst und getrennt entschieden.

## Spaeter / offen (nicht im aktuellen Stand)

- **Confidence / Margin** als zusaetzliches Gate: aktuell NICHT moeglich, da der LLM-Self-Training-Pfad
  keine echten Wahrscheinlichkeiten liefert. Wird bewusst NICHT erfunden. Echte Confidence gaebe es
  erst ueber den MultiModel/YOLO-DINO-Pfad (Weg 3).
- **KB-Abgleich-Verfeinerung**: die Abfrage per Protokolltext kann das gleiche/aehnliche bereits
  indizierte Sample treffen (Bias Richtung Agreement). Eine spaetere Version koennte same-CaseId-Treffer
  ausschliessen.
