# ADR-009: D6 Dedup-Logik als gemeinsame Temporal-Komponente extrahieren

**Status:** Vorgeschlagen - **Datum:** 2026-05-31 - **Kontext:** D6 aus dem Pipeline-Root-Cause-Audit

## Kontext

Der Audit hat gezeigt, dass die zeitliche Dedup-/Merge-Logik in zwei Analysepfaden fast doppelt vorhanden ist:

- `MultiModelAnalysisService`
- `VideoFullAnalysisService`

Beide Pfade verwalten aktive Befunde ueber mehrere Frames, verwenden ein Frame-Fenster gegen kurze Aussetzer, bauen Dedup-Keys, normalisieren Uhrlagen und erzeugen am Ende `RawVideoDetection`-Eintraege.

Die Duplizierung ist aktuell kein einzelner Crash-Bug, aber sie ist ein Qualitaetsrisiko: kleine Korrekturen landen leicht nur in einem Pfad. Genau das ist bereits passiert. D4 musste die Punkt-/Strecken-Aufloesung im zweiten Pfad nachziehen; dabei ruft `VideoFullAnalysisService` nun eine Methode aus `MultiModelAnalysisService` auf. Das ist ein klarer Hinweis, dass der gemeinsame Kern an eine neutrale Stelle gehoert.

## Entscheidung

Wir fuehren die beiden Analyse-Services nicht komplett zusammen.

Stattdessen soll nur der gemeinsame zeitliche Kern extrahiert werden:

`TemporalFindingDeduplicator`

Diese Komponente uebernimmt:

- aktive Befunde ueber Frames halten
- `DedupWindowFrames` anwenden
- Dedup-Key bauen
- Uhrlage normalisieren
- Start-/Endmeter zusammenfuehren
- Punkt- vs. Streckenbefund aufloesen
- fertige `RawVideoDetection`-Eintraege ausgeben

Die Analyse-Services behalten ihre eigentliche Verantwortung:

- `MultiModelAnalysisService`: Modell-/Evidence-/QualityGate-spezifische Verarbeitung
- `VideoFullAnalysisService`: VideoFull-/Fallback-spezifische Verarbeitung

## Empfohlene Zielstruktur

Neue Datei:

- `src/AuswertungPro.Next.Infrastructure/Ai/Pipeline/TemporalFindingDeduplicator.cs`

Neue Tests:

- `tests/AuswertungPro.Next.Infrastructure.Tests/TemporalFindingDeduplicatorTests.cs`

Moegliche API:

```csharp
public sealed class TemporalFindingDeduplicator
{
    public TemporalFindingDeduplicator(TemporalDedupOptions options);

    public IReadOnlyList<RawVideoDetection> Update(
        IReadOnlyList<EnhancedFinding> findings,
        double frameMeter,
        int frameIndex,
        EvidenceVector? evidence = null);

    public IReadOnlyList<RawVideoDetection> Flush();
}
```

Die genaue API darf bei der Umsetzung angepasst werden. Wichtig ist die Grenze: Die Komponente soll rein bleiben und keine UI-, Datei-, Sidecar- oder Netzwerklogik enthalten.

## Nicht empfohlen

### Beide Services komplett verschmelzen

Das waere zu gross fuer D6. Die Pfade unterscheiden sich weiterhin bei Modellaufruf, Evidence, Fallbacks und Pipeline-Kontext. Eine Voll-Zusammenfuehrung wuerde viele Nebeneffekte riskieren.

### Duplizierung lassen

Kurzfristig ist das am risikoaermsten, aber fachlich schwach. Jede spaetere Korrektur an Meterlogik, Uhrlage, Frame-Fenster oder Dedup-Key muesste wieder doppelt erfolgen.

### D5 gleichzeitig einbauen

D5, also ein Meter-Distanz-Guard beim Mergen, ist eine fachliche Verhaltensaenderung. Das gehoert nicht in D6. D6 soll zuerst nur die vorhandene Logik zentralisieren. Der Meter-Guard braucht separat eine Schwelle und eigene Tests.

## Umsetzungsschritte

1. `ResolveMeterEnd` und Uhrlagen-Normalisierung in die neue Komponente oder einen kleinen Helper verschieben.
2. Bestehende Tests fuer Punkt-/Strecken-Aufloesung auf die neue Komponente umhaengen.
3. Tests fuer das aktuelle Dedup-Verhalten schreiben:
   - gleicher Code innerhalb `DedupWindowFrames` wird zusammengefuehrt
   - gleicher Code nach Fensterablauf wird getrennt
   - verschiedene VSA-Codes bleiben getrennt
   - verschiedene Uhrlagen bleiben getrennt
   - Punktbefunde kollabieren auf Startmeter
   - Streckenbefunde behalten Start-/Endmeter
4. `VideoFullAnalysisService` zuerst auf die neue Komponente umstellen.
5. Danach `MultiModelAnalysisService` umstellen und Evidence-/QualityGate-Felder unveraendert erhalten.
6. Alte private Active-State-Klassen erst entfernen, wenn beide Pfade gruene Tests haben.
7. Manuelle Regression mit der bekannten 6.39-m-Haltung und mindestens einem echten Streckenbefund.

## Akzeptanzkriterien

- Beide Pfade liefern fuer gleiche fachliche Eingaben gleiche Dedup-/Meter-Entscheidungen.
- Bestehende Trefferanzahl darf sich ohne bewusste fachliche Entscheidung nicht aendern.
- D4 bleibt erhalten: Punkt-/Strecken-Aufloesung ist in beiden Pfaden identisch.
- D5 ist nicht enthalten.
- Build und Tests bleiben gruen.

## Risiken

- Kleine Aenderungen am Dedup-Verhalten koennen die Anzahl der Befunde sichtbar veraendern.
- Der MultiModel-Pfad traegt mehr Evidence-Daten als der VideoFull-Pfad. Diese Informationen duerfen beim Extrahieren nicht verloren gehen.
- Eine zu grosse API wuerde die neue Komponente wieder schwer testbar machen.

## Nicht Teil dieser Entscheidung

- ByteTrack, OC-SORT oder anderes echtes Tracking
- neuer Meter-Distanz-Guard
- neue Confidence-Kalibrierung
- QualityGate-Redesign
- Sidecar-Schema-Aenderungen
- UI-/DataGrid-Aenderungen
