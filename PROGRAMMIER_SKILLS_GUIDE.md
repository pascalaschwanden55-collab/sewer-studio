# PROGRAMMIER-SKILLS — Wegleitung fuer SewerStudio

Der beste Skill zum Programmieren ist **nicht eine Programmiersprache**, sondern eine Denkweise. Diese Wegleitung beschreibt die vier Kern-Skills und die konkrete Lern-Reihenfolge fuer dieses Projekt.

---

## 1. Logisches Denken / Problem zerlegen

Du musst lernen, ein grosses Problem in kleine Schritte aufzuteilen.

**Nicht denken:**
> "Ich baue eine KI-Pipeline."

**Sondern:**
1. Video einlesen
2. Frames extrahieren
3. Schaden erkennen
4. Ergebnis pruefen
5. Code zuordnen
6. Bericht speichern

Das ist der wichtigste Programmier-Skill.

---

## 2. Saubere Architektur verstehen

Fuer SewerStudio besonders wichtig: **Nicht alles direkt mit KI loesen.**

```
Regeln / Matrix / C# Logik
        v
KI nur fuer Analyse und Begruendung
        v
Validierung
        v
Ausgabe
```

Das ist staerker als einfach ein grosses Modell einzubauen. Passt direkt zum **Thin-AI-Prinzip** aus `CLAUDE.md`: C# fuer die Geschaeftslogik, LLM nur fuer Textgenerierung.

---

## 3. Debuggen lernen

Ein guter Programmierer ist nicht jemand, der sofort perfekten Code schreibt.

Ein guter Programmierer kann herausfinden:

- **Wo** passiert der Fehler?
- **Warum** passiert er?
- Welche **Eingabe** loest ihn aus?
- Wie verhindere ich ihn **sauber** (statt mit einem Workaround)?

---

## 4. Fuer dich konkret: C# + Architektur + KI-Integration

Beste Reihenfolge fuer dieses Projekt:

1. **C# Grundlagen** festigen
2. **MVVM** verstehen (WPF-Pattern fuer dieses Projekt)
3. **Dependency Injection** verstehen
4. **Services sauber trennen** (siehe `InferenceOrchestratorService`, `QualityGateService`, `MeasurementService` in `CLAUDE.md`)
5. **Tests / QualityGate** einbauen (Green/Yellow/Red muss immer durchlaufen)
6. **KI kontrolliert** ueber definierte Schnittstellen einsetzen (JSON-Schema strict, kein freier Text)

---

## Kern-Empfehlung

Der wichtigste Skill ist **Systemdenken beim Programmieren**.

Also nicht nur Code schreiben, sondern verstehen:

```
Daten rein -> Verarbeitung -> Pruefung -> Entscheidung -> Ausgabe
```

Bei deinem SewerStudio-Projekt ist das wichtiger als Python, Gemma, Qwen oder irgendein neues Modell.

---

## Anwendung im Projekt

Bei jedem neuen Feature im SewerStudio:

- **Zerlege** das Feature in 4-8 konkrete Schritte
- **Pruefe**, was deterministisches C# erledigen kann (Regel-Matrix, Schwellwerte, Messung)
- **Setze KI nur dort ein**, wo Sprache oder visuelle Interpretation gebraucht wird
- **Validiere** jede KI-Ausgabe gegen das JSON-Schema
- **Falle nie zurueck** auf "lass die KI das schon richten" — das verletzt das Thin-AI-Prinzip
