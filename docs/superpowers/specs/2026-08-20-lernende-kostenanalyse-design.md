# Lernende Kostenanalyse aus Schadensbildern — Konzept

**Stand:** 2026-08-20
**Ziel:** Das Programm soll aus Pascals beurteilten Haltungen lernen und irgendwann
selbständig vorschlagen, welche Sanierungsmassnahmen eine Haltung braucht und was
das kostet.

---

## 1. Was entschieden ist

| Frage | Entscheidung |
|---|---|
| Merkmale | Schadenscodierung, Durchmesser, Bögen ja/nein |
| Lerneinheit | die **Haltung**, nicht der Einzelschaden |
| Ergebnis | Massnahmen **mit Mengen**; Preise rechnet der Kostenkatalog |
| Verfahren | fallbasiert — ähnliche beurteilte Haltungen als Vorbild |
| Messung | rückblickend über den Bestand **und** verdeckt bei neuen Haltungen |
| Wahrheit | nur persönlich beurteilte Haltungen (Gold-Prinzip) |

Ausdrücklich verworfen: ein trainiertes ML-Modell (Entscheidungsbaum, Gradient
Boosting). Bei knapp 60 Fällen würde es auswendig lernen statt zu verstehen, und es
könnte nie erklären, warum es etwas vorschlägt.

---

## 2. Ausgangslage — ehrlich gemessen

### 2.1 Es gibt bereits eine Lernfunktion, und sie lernt das Falsche

`MeasureRecommendationService` besitzt `Learn()`, `TrainModel()` und Kostenaggregate.
Sie wird bei jedem Speichern der Datenseite gefüttert. Stand in
`C:\KI_BRAIN\measures_learning.json`: 210 gelernte Haltungen, 404 Codes,
63 Code-Signaturen.

Verwertbar ist davon nichts. Drei belegte Gründe:

**a) Kreuzprodukt statt Zuordnung.** In `Learn()`:

```csharp
foreach (var code in codes)
    foreach (var measure in measures)
        perMeasure[measure]++;
```

Jeder Code der Haltung wird mit jeder Massnahme der Haltung gepaart. Hat eine Haltung
„Riss + Wurzeln" und bekommt „Liner + Fräsen", lernt das Modell alle vier Kombinationen
als gleich wahr. Ergebnis: Alle Codes haben praktisch dieselbe Verteilung.

**b) Nicht-Schäden dominieren.** Die häufigsten „Schadenscodes" sind:

```
BCE    675x    (Rohrende)
BCD    674x    (Rohranfang)
000M   664x
BDA    460x
```

Diese kommen in jeder Haltung vor. Es wird nicht gefiltert.

**c) Durchmesser und Bögen fehlen.** Die Signatur ist `BuildCodeSignature(codes)` —
nur die Code-Menge. Eine DN 150 mit Riss und eine DN 600 mit Riss landen im selben
Topf, obwohl sie fachlich völlig verschieden zu sanieren sind.

**Folge:** Der Lernbestand wird verworfen und neu aufgebaut. Der alte Dienst bleibt
unangetastet, bis der neue gemessen besser ist.

### 2.2 Die reale Lerngrundlage ist klein

Nur Zone 1.15 ist vollständig ausgewertet:

| | |
|---|---|
| Haltungen im Projekt | 96 |
| davon mit Kostenzusammenstellung | 65 |
| **davon mit DN und Schäden** | **58** |
| Längen | 2.4 – 94.1 m |

Verteilung über die 64 Haltungen, die sich im Projekt auflösen liessen (eine der 65
Kostenzeilen hat keinen passenden Haltungsdatensatz — das ist selbst schon ein
Prüfhinweis):

```
DN 300   36          BAF  38 Haltungen      ohne Bogen   61
DN 250   19          BAH  21                mit Bogen     3
DN 185    4          BAI  18
DN 150    3          BDD  17
DN 200    1          BAJ  17
DN 400    1          BBC  14, BAB 13, BAC 7
```

Von diesen 64 haben 58 sowohl DN als auch Schäden — das ist die Lernbasis.

**Drei Einschränkungen, die das Konzept prägen:**

1. **Bögen sind nicht vertreten** (3 von 64). Obwohl sie ein genanntes Merkmal sind,
   kann das System dazu vorerst nichts lernen.
2. **Der Durchmesser ist eng.** 55 von 64 sind DN 250/300. Für DN 400 gibt es einen
   Fall, für DN 500/600 keinen.
3. **Ein Gebiet, ein Bearbeiter, ein Preisstand.** Alles stammt aus Altdorf Zone 1.15.

Das ist dieselbe Lage wie beim OSD-Goldsatz vor `osd_mix_v1`: Ein Bestand, der den
späteren Einsatz nicht vertritt. Deshalb ist das wichtigste Bauteil dieses Systems
nicht die Empfehlung, sondern die **Enthaltung**.

---

## 3. Der Fall — was gelernt wird

Ein Fall entsteht aus einer Haltung, die Pascal beurteilt und deren
Kostenzusammenstellung er bestätigt hat.

### 3.1 Merkmale (die Frage)

| Merkmal | Herkunft | Bemerkung |
|---|---|---|
| Schadensarten | Protokoll, Hauptcode (3 Zeichen) | BCD, BCE, BDA, BCA, 000M werden ausgeschlossen — das sind Bauteile, keine Schäden |
| Anzahl je Schadensart | Protokoll | 1 Riss ist nicht 8 Risse |
| Streckenschaden je Art | `IsStreckenschaden` | ein durchgehender Oberflächenschaden verlangt anderes als ein Punktschaden |
| Durchmesser | `DN_mm` | |
| Länge | `Haltungslaenge_m` | Grundlage der Mengenumrechnung |
| Bögen | Anzahl `BCC*` im Protokoll | vorerst nur ja/nein, weil zu dünn belegt |

Nicht aufgenommen (bewusst): Zustandsklasse (folgt aus den Schäden, wäre doppelt),
Material, Baujahr, Eigentümer. Erst wenn eine Messung zeigt, dass sie fehlen.

### 3.2 Ergebnis (die Antwort)

Das Massnahmenpaket aus den **ausgewählten** Kostenzeilen der Haltung:

```
ItemKey                 Menge   Einheit
SCHLAUCHLINER_GFK        42.5   m
LINERENDMANSCHETTE_LEM      2   Stk
ANSCHLUSS_EINBINDEN         3   Stk
```

**Ohne Preise.** Die kommen beim Vorschlagen aus dem aktuellen Katalog. Damit bleibt
ein Fall von 2026 auch 2028 gültig, wenn die Preise gestiegen sind.

### 3.3 Wahrheitsregel

Ein Fall wird nur aufgenommen, wenn:

- die Haltung eine Kostenzusammenstellung mit mindestens einer ausgewählten Zeile hat,
- Schadensarten und DN vorhanden sind,
- die Länge grösser 0 ist,
- die Zusammenstellung von Pascal stammt (kein automatisch erzeugter Vorschlag).

Der letzte Punkt ist der wichtigste und braucht die Herkunftskennzeichnung aus
Abschnitt 6.2 — sonst lernt das System von sich selbst.

---

## 4. Der Vorschlag — wie entschieden wird

### 4.1 Ähnliche Fälle finden

**Schritt 1 — harte Grenzen.** Ein Fall kommt nur in Frage, wenn

- der Durchmesser höchstens **eine Katalogstufe** entfernt liegt
  (150 · 185 · 200 · 250 · 300 · 400 · 500 · 600 …),
- der Bogen-Status übereinstimmt, **sobald** genügend Bogenfälle vorliegen
  (bis dahin wird das Merkmal nur als Hinweis angezeigt, nicht als Filter benutzt).

**Schritt 2 — Rangfolge nach Schadensähnlichkeit.** Über die Menge der Schadensarten:

```
Ähnlichkeit = gemeinsame Arten / alle vorkommenden Arten
```

Zwei Haltungen mit {BAF, BAJ} und {BAF, BAJ, BBC} ergeben 2/3 = 0.67.
Bei gleichem Wert entscheidet die nähere Anzahl der Schäden, danach der nähere DN.

**Schritt 3 — die besten Fälle** (Vorschlag: bis zu 7) bilden die Grundlage.

### 4.2 Mengen umrechnen

Je Position wird über die Nachbarn der **Median** genommen — nicht der Mittelwert,
damit ein einzelner Ausreisser den Vorschlag nicht kippt.

| Positionsart | Regel |
|---|---|
| längenbezogen (m) | Menge des Nachbarn ÷ dessen Länge × Länge der neuen Haltung |
| stückbezogen (Stk) | Median der Nachbarn, gerundet |
| pauschal (pl) | übernehmen, wenn die Mehrheit der Nachbarn sie hat |

Eine Position erscheint im Vorschlag nur, wenn **mehr als die Hälfte** der
herangezogenen Fälle sie enthält. Sonst entstünde aus sieben verschiedenen
Massnahmenpaketen ein Sammelsurium, das kein Mensch je so bestellt hätte.

### 4.3 Preise

Die Mengen gehen durch den vorhandenen Kostenkatalog (`CostCatalogItem`, inklusive
DN-abhängiger Preise). Das Ergebnis ist eine **Nettosumme ohne MWST** — dieselbe
Regel wie im Projekt-Cockpit.

---

## 5. Die Enthaltung — wann das System schweigt

Das System gibt **keinen** Vorschlag, wenn eine dieser Bedingungen zutrifft:

| Bedingung | Grund |
|---|---|
| weniger als 3 passende Fälle | zu dünn für einen Median |
| DN ausserhalb des gelernten Bereichs | reine Hochrechnung ins Blaue |
| Bogen vorhanden, aber weniger als 10 gelernte Bogenfälle | heute der Normalfall |
| die Nachbarn sind sich uneinig (keine Position erreicht die Mehrheit) | es gibt kein gemeinsames Vorbild |

Ausgegeben wird dann ein **Grund im Klartext**: „Zu wenig Erfahrung: nur 1 ähnlicher
Fall (DN 400)". Keine Zahl ist besser als eine erfundene Zahl in einer Offerte.

**Diese Enthaltungsquote wird immer mitgemessen.** Ein System, das immer schweigt,
hat null Fehler und null Nutzen — das ist die Lehre aus der OSD-Arbeit: Wer etwas
ändert, misst Treffer **und** Abdeckung.

---

## 6. Die Messung

### 6.1 Rückblickend — sofort verfügbar

Für jeden der 58 Fälle: Das Modell wird **ohne diesen Fall** aufgebaut, sagt ihn
vorher, und das Ergebnis wird mit Pascals echter Zusammenstellung verglichen
(Leave-one-out).

Berichtet wird, streng getrennt:

| Kennzahl | Bedeutung |
|---|---|
| Abdeckung | wie viele Haltungen überhaupt einen Vorschlag bekamen |
| Massnahmen-Treffer | stimmt die Hauptmassnahme (z. B. Liner ja/nein) |
| Positions-Genauigkeit | wie viele Positionen richtig / zu viel / vergessen |
| Kostenabweichung | Median und Streuung der prozentualen Abweichung |
| grobe Fehler | Abweichung über 50 % — die, die in einer Offerte weh tun |

Der Bericht wird als Datei mit SHA-256 abgelegt, wie die übrigen Messberichte des
Projekts. **Diese Messung ist eine Standortbestimmung, keine Freigabe** — sie misst
ein Gebiet, einen Bearbeiter, einen Preisstand.

### 6.2 Verdeckt — die ehrliche Abnahme

Sobald Pascal eine neue Haltung öffnet und bevor er sie beurteilt, rechnet das System
seinen Vorschlag und legt ihn **versiegelt** ab: Merkmals-Hash, Vorschlag, Zeitpunkt.
Der Vorschlag wird ihm dabei nicht gezeigt, solange die Haltung als Messfall gilt.

Erst wenn er seine Zusammenstellung bestätigt hat, wird verglichen.

Jeder Fall trägt eine Herkunft:

| Herkunft | Bedeutung |
|---|---|
| `Unbeeinflusst` | Vorschlag war verdeckt — zählt als Messfall **und** als Lernfall |
| `VorschlagGesehen` | Pascal hat den Vorschlag vorher gesehen — zählt **nur** als Lernfall |

Das ist dasselbe Prinzip wie `SuggestionProvenance` beim Bogen-Copiloten und
verhindert den einzigen Fehler, der dieses Vorhaben wertlos machen würde:
dass sich das System an seinen eigenen Vorschlägen misst.

---

## 7. Aufbau im Programm

Nach den Architekturregeln des Projekts (Geschäftslogik in C#, Schichten getrennt,
neue Orchestrierung unter `UseCases/`):

```
Application/Kostenanalyse/
  IKostenfallStore.cs           Vertrag: Fälle lesen/schreiben
  KostenfallDtos.cs             Fall, Merkmale, Massnahmenpaket, Vorschlag, Enthaltungsgrund
  KostenfallExtraktor.cs        Haltung + Kostenzeilen -> Fall (rein, ohne Datei-Zugriff)
  KostenfallAehnlichkeit.cs     Ähnlichkeitsmass und Rangfolge (rein)
  KostenVorschlagRechner.cs     Nachbarn -> Massnahmenpaket mit Mengen (rein)
  KostenVorschlagPolicy.cs      Enthaltungsregeln an einer Stelle

Application/UseCases/
  KostenanalyseLernUseCase.cs        Fall aufnehmen (mit Herkunft)
  KostenanalyseVorschlagUseCase.cs   Vorschlag für eine Haltung
  KostenanalyseMessungUseCase.cs     Leave-one-out über den Bestand

Infrastructure/Kostenanalyse/
  KostenfallFileStore.cs        JSON unter <KnowledgeRoot>\kostenanalyse\
  KostenanalyseMessBericht.cs   Bericht mit SHA-256
```

Die Rechenteile sind rein und ohne Oberfläche testbar. Der Dateizugriff liegt allein
in Infrastructure. Der bestehende `MeasureRecommendationService` wird **nicht**
verändert und läuft weiter, bis der neue Weg gemessen besser ist.

Anzeige: Die Schattenauswertung bekommt eine zusätzliche Spalte mit dem
Massnahmenvorschlag und der Katalogsumme, neben dem, was du selbst eingetragen hast.
Ihre Zusicherung bleibt unberührt — es wird nichts in die Haltung geschrieben.

---

## 8. Was dieses Konzept ausdrücklich nicht macht

- **Keine automatische Übernahme** in den Kostenrechner. Der Vorschlag ist ein
  Vorschlag; das Übernehmen bleibt ein bewusster Klick.
- **Keine Schächte.** Erst Haltungen. Schächte haben andere Merkmale und eine eigene
  Datenlage.
- **Kein Preislernen.** Preise kommen aus dem Katalog, nicht aus der Statistik.
- **Kein LLM.** Der Weg ist nachvollziehbare Statistik über deine eigenen Fälle. Die
  vorhandene KI-Phase der Schattenauswertung bleibt daneben bestehen und unverändert.
- **Keine Freigabe ohne Messung.** Bis die verdeckte Messung eine belastbare Zahl
  liefert, ist der Vorschlag Diagnose und nichts anderes.

---

## 9. Offene Punkte

1. **Wie viele Fälle braucht es?** Die Mindestzahl 3 und die 7 Nachbarn sind
   begründete Startwerte, keine gemessenen. Die Rückblick-Messung soll sie prüfen —
   möglicherweise ist bei dieser Datenlage 5 besser als 7.
2. **Ausmass der Schäden.** Vorerst wird nur die Anzahl je Schadensart benutzt. Ob
   Länge und Umfang eines Schadens die Vorhersage verbessern, muss gemessen werden,
   bevor es eingebaut wird.
3. **Bögen.** Bleiben bis auf Weiteres nur ein Hinweis. Sobald der Bogen-Copilot
   Bögen zuverlässig meldet, kann das Merkmal aufgewertet werden — dann liefert die
   Bogenerkennung Futter für die Kostenanalyse.
4. **Zweites Gebiet.** Solange nur Zone 1.15 vollständig ist, misst jede Zahl nur
   dieses Gebiet. Ein zweites ausgewertetes Projekt ist der grösste Hebel für die
   Aussagekraft — grösser als jede Verbesserung am Verfahren.

---

## 10. Reihenfolge der Umsetzung

| Etappe | Inhalt | Ergebnis |
|---|---|---|
| 1 | Fall-Extraktion + Speicher + Aufbau aus Zone 1.15 | 58 saubere Fälle, prüfbar |
| 2 | Ähnlichkeit, Vorschlag, Enthaltung | erste Vorschläge, noch ungemessen |
| 3 | Rückblick-Messung (Leave-one-out) | die erste ehrliche Zahl |
| 4 | Anzeige in der Schattenauswertung | sichtbar, weiterhin ohne Wirkung auf Daten |
| 5 | Verdeckte Vorhersage + Herkunft | die Abnahme beginnt zu laufen |

Nach Etappe 3 wird entschieden, ob es weitergeht. Zeigt die Messung, dass die
Datenlage nicht trägt, ist das ein gültiges Ergebnis — dann ist die Antwort
„mehr ausgewertete Projekte", nicht „besseres Verfahren".
