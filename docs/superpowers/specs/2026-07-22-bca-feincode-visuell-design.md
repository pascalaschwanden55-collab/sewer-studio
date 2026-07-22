# Design: Feiner Anschluss-Code (BCA) über den visuellen Weg

**Datum:** 2026-07-22
**Kontext:** Weg B aus der KI-Erkennungs-Diagnose (docs/KI-ERKENNUNG-DIAGNOSE-2026-07-21.md).
Pilot des Ansatzes „Mischung": das Modell erkennt die grobe Familie, der feine VSA-Code
entsteht teils per Regel aus der Messung, teils visuell. Dieser Pilot deckt den **visuellen**
Teilweg an der Schadensfamilie **Anschluss (BCA)** ab.

---

## 1. Ausgangslage (die das Design prägt)

- **Datenlage:** 103 feine VSA-Codes in den Trainingsdaten, aber 70 % (72 Codes) haben unter
  5 Beispiele. Ein Modell, das alle feinen Codes direkt aus Bildern lernt, ist damit nicht
  trainierbar. Reines Modelltraining scheidet als Einstieg aus.
- **BCA-Struktur (aus dem VSA-Katalog verifiziert):** Der feine Anschluss-Code kodiert die
  **Bauart** (Formstück BCAAA, Sattel gebohrt BCABA, Sattel eingespitzt BCACA, gebohrt BCADA,
  eingespitzt BCAEA, Spezial BCAFA, unbekannt BCAGA, andersartig BCAZA) plus **offen (…A) /
  verschlossen (…B)**. Die Uhrlage ist **nicht** Teil des BCA-Codes (separates Feld). Die
  Bauart ist ein **visuelles** Merkmal — nicht aus Geometrie ableitbar.
- **Vorhandener Stand:** `VsaCodeResolver` (Infrastructure/Ai) leitet heute aus der Messung nur
  **grobe** Hauptcodes ab (`InferCodeFromLabel`, `ResolveFromClassifier`). Eine feine
  Char1/Char2-Ableitung existiert nicht — genau diese Stufe fehlt.
- **Andockpunkte:** Qwen liefert bereits strukturierte Ausgaben mit festen Auswahllisten
  (strict JSON-Schema, `EnhancedVisionAnalysisService`). Der Prüfplatz (TrainingStudio, Etappe 1)
  hat einen Vorschlags-Mechanismus (`WorkbenchSuggestion` mit `WorkbenchCodeCandidate`:
  VsaCode / Confidence / Quelle).

## 2. Gewählter Ansatz

Aus drei Ansätzen (A = Qwen mit fester Auswahlliste jetzt, B = eigenes Bild-Modell später,
C = Hybrid) wurde **A als Start, eingebettet in C** gewählt:

- **A (jetzt):** Erkennt die Pipeline einen Anschluss (grobe Familie BCA), fragt ein neuer,
  fokussierter Schritt gezielt nach der Bauart. Qwen wählt aus den 17 festen BCA-Feincodes
  (strict JSON-Enum) und liefert einen Vorschlag mit Konfidenz.
- **C (Rahmen):** Am Prüfplatz bestätigt oder korrigiert der Nutzer den Vorschlag. Jede
  Bestätigung wird sauberes Trainingsmaterial — die Grundlage für ein späteres eigenes
  Bild-Modell (B), sobald genug Daten pro Bauart vorliegen. B ist ausdrücklich **kein** Teil
  dieses Piloten.

## 3. Architektur & Komponenten

Neuer, fokussierter Dienst nach der „injizierbar"-Konvention (Vertrag in Application, Impl in
Infrastructure, Registrierung im ServiceProvider):

- **`IBcaFineCodeClassifier` (Application/Ai):**
  - Eingabe: Bild des Anschlusses (Base64 oder Crop) + die erkannte grobe Familie (BCA).
  - Ausgabe: `BcaFineCodeSuggestion` — eine kurze, absteigend sortierte Kandidatenliste
    (feiner VSA-Code + Konfidenz + Quelle) oder „unsicher" (keine Kandidaten).
- **`BcaFineCodeClassifier` (Infrastructure/Ai):** Umsetzung über einen eigenen, engen
  Qwen-Aufruf mit strict JSON-Schema. Der Dienst kennt nur diese eine Aufgabe und keine
  UI-/Pipeline-Details.
- **Qwen-Schema:** strenge Auswahl — die 17 BCA-Feincodes als `enum` plus ein
  `"unsicher"`-Wert; Freitext ist ausgeschlossen (wie die bestehenden Qwen-Aufrufe).

## 4. Datenfluss & Andockpunkte

- **Zuerst ausschliesslich am Prüfplatz (TrainingStudio):** Erkennt der Prüfplatz einen
  Anschluss, ruft er `IBcaFineCodeClassifier` und zeigt den Bauart-Vorschlag als zusätzlichen
  `WorkbenchCodeCandidate` an. Der Nutzer bestätigt oder korrigiert → das Ergebnis fliesst in
  die bestehende Bestätigungs-/Speicherlogik (Trainingsmaterial).
- **Die automatische Analyse (Batch/Video) bleibt vorerst beim groben Code „BCA".** Sie wird in
  diesem Piloten **nicht** angefasst. So kann kein ungeprüfter Qwen-Feincode in ein Protokoll
  gelangen. Die Freischaltung für die Auto-Analyse ist ein späterer, eigener Schritt — erst
  nachdem die Messung (Abschnitt 6) belegt, dass Qwen zuverlässig genug trifft.

## 5. Fehlerbehandlung

- Qwen nicht erreichbar, Zeitüberschreitung oder Ergebnis „unsicher" → es bleibt beim groben
  Code „BCA". Nichts stürzt ab, nichts wird verschlechtert.
- Der feine Code ist **immer ein Zusatz, nie ein Ersatz** des groben Codes.

## 6. Messung & Erfolgskriterium

- Gemessen am eingefrorenen 120er-Eval-Set, das die feinen Anschluss-Codes als Soll enthält:
  **BCA-Feincode-Trefferquote vorher/nachher.**
- Erfolgskriterium des Piloten: Der Anschluss-Feincode wird spürbar häufiger korrekt getroffen
  als der heutige Stand (grob „BCA"), ohne dass die grobe Trefferquote sinkt.

## 7. Tests

- Reines Mapping Qwen-Antwort → BCA-Feincode (alle 17 Codes, plus „unsicher").
- Schema-Validierung: Freitext / unbekannter Code wird abgewiesen.
- Fallback-Pfad: Qwen-Fehler/„unsicher" → grober BCA bleibt (kein Wurf, kein Ersatz).
- Prüfplatz-Andockung: Vorschlag erscheint als zusätzlicher Kandidat; Auto-Analyse-Code
  bleibt unverändert grob.

## 8. Bewusst NICHT Teil dieses Piloten (YAGNI)

- Kein eigenes Bild-Modell (B) — erst nach genug bestätigten Daten.
- Keine anderen Schadensfamilien (BAB, BAA, BAF …) — separate Pakete.
- Keine Änderung an der groben Erkennung.
- Kein Eingriff in die automatische Analyse (späterer Schritt nach Messung).
- Kein Umgehen der bewusst gesperrten Export-/Migrationswege (CLAUDE.md).

## 9. Grobe Etappen (für den Implementierungsplan)

1. **Dienst + Schema + Tests:** `IBcaFineCodeClassifier` / `BcaFineCodeClassifier` mit
   strict-JSON-Qwen-Aufruf, Mapping und Fallback; Unit-Tests ohne echten Qwen-Server.
2. **Prüfplatz-Andockung:** Vorschlag als zusätzlicher Kandidat anzeigen; Bestätigung fliesst
   in die vorhandene Speicherlogik (Trainingsmaterial).
3. **Messung:** BCA-Feincode-Trefferquote am 120er-Eval vorher/nachher auswerten.

## 10. Offene Entscheidung (vom Nutzer zu bestätigen)

Der Vorschlag erscheint im Piloten **nur am Prüfplatz** (so hier festgelegt, empfohlen). Die
Alternative — den Feincode sofort auch in der automatischen Analyse zu setzen — wurde bewusst
zurückgestellt, weil sie ungeprüfte Feincodes ins Protokoll bringen könnte. Diese Wahl ist beim
Review dieses Dokuments noch änderbar.
