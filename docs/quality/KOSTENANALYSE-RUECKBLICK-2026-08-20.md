# Kostenanalyse — erste Rückblick-Messung

**Datum:** 2026-08-20
**Bestand:** Zone 1.15, 55 Fälle
**Bericht:** `C:\KI_BRAIN\kostenanalyse\berichte\kostenanalyse_rueckblick_20260820_140829.json`
**SHA-256:** `285703341d0abaae9df6d82f60eaacc00c2b757da8367668bd70e0165ff5d588`

**Dies ist eine Standortbestimmung, keine Freigabe.** Gemessen wurde ein Gebiet, ein
Bearbeiter, ein Preisstand.

---

## 1. Der Fallbestand

| | |
|---|---|
| Haltungen im Projekt | 96 |
| Kostenzeilen | 65 |
| **Fälle aufgebaut** | **55** |

Übersprungen mit Grund:

| Anzah| Grund |
|---|---|
| 32 | keine Kostenzusammenstellung |
| 6 | kein einziger Schaden im Protokoll (Bauteile zählen nicht) |
| 3 | keine ausgewählte Massnahme in der Kostenzusammenstellung |

**Nebenbefund:** 65 Kostenzeilen, aber nur 64 lassen sich einer Haltung zuordnen. Eine
Kostenzusammenstellung hängt an einem Haltungsnamen, den es so nicht mehr gibt —
vermutlich nach einer Umbenennung. Sie geht in jeder Auswertung still verloren.

---

## 2. Die Aufgabe ist leichter als sie aussieht

| | |
|---|---|
| Positionen je Fall | 12.2 (6–15) |
| Verschiedene Positionen insgesamt | 20 |
| Verschiedene Massnahmenpakete | 14 (bei 55 Fällen) |

**Neun der zwanzig Positionen kommen in mindestens 90 % aller Fälle vor:**

```
QK_TV_ABNAHME                100 %      QK_DICHTHEITSPRUEFUNG   98 %
VORARBEIT_REINIGUNG          100 %      VORARBEIT_EINMESSUNG    98 %
VORARBEIT_TV_VORKONTROLLE    100 %      VORARBEIT_FRAESEN       98 %
QK_DOKUMENTATION              98 %      INSTALL_UV_ANLAGE       95 %
LINERENDMANSCHETTE_LEM        98 %
```

Diese zu „treffen" ist keine Leistung. Eine Gesamtzahl über alle Positionen misst
deshalb überwiegend, wie einheitlich gearbeitet wird — nicht, ob das Verfahren etwas
versteht.

**Die elf entscheidenden Positionen:**

```
SCHLAUCHLINER_GFK                89 %      VORARBEIT_WASSERHALTUNG          9 %
ANSCHLUSS_AUFFRAESEN             55 %      HAUPTARBEIT_HINDERNISSE_ROBOTER  6 %
VORARBEIT_ANSCHLUSS_EINMESSEN    55 %      SCHLAUCHLINER_NADELFILZ_OPENEND  6 %
ANSCHLUSS_EINBINDEN              55 %      INSTALL_HL_ANLAGE                4 %
VORARBEIT_VD                     51 %      SCHLAUCHLINER_NADELFILZ          4 %
                                           INSTALL_ROBOTER                  2 %
```

---

## 3. Das Ergebnis

**Abdeckung: 94.5 %** (52 von 55 Fällen bekamen einen Vorschlag, 3 Enthaltungen)

### Über alle Positionen — die geschenkte Zahl

```
591 richtig / 44 zuviel / 44 fehlend        Genauigkeit 93.1 %
```

### Nur die entscheidenden Positionen — die ehrliche Zahl

| | richtig | zuviel | fehlend | Genauigkeit | Vollständigkeit | F1 |
|---|---|---|---|---|---|---|
| Gegenprobe (immer dasselbe Paket) | 162 | 98 | 12 | 62.3 % | 93.1 % | 74.7 % |
| **Modell (ähnliche Fälle)** | 130 | 37 | 44 | **77.8 %** | 74.7 % | **76.2 %** |

**Der Zugewinn beträgt 1.5 F1-Punkte.** Die Ähnlichkeitssuche bringt also etwas — aber
wenig. Der Charakter unterscheidet sich deutlicher als die Gesamtnote:

- Das Modell **schlägt weniger Unnötiges vor** (37 statt 98 überflüssige Positionen)
- Es **vergisst dafür mehr** (44 statt 12)

Für einen Vorschlag, den ein Mensch prüft, ist der erste Punkt mehr wert: 98 falsche
Positionen durchzustreichen ist mühsamer, als 44 fehlende zu ergänzen. Aber beides
heisst, dass jede Zeile weiterhin geprüft werden muss.

---

## 4. Warum der Zugewinn klein ist

Bei 55 Fällen aus einem Gebiet gibt es nur **14 verschiedene Massnahmenpakete**. Die
Arbeit ist so einheitlich, dass es wenig zu unterscheiden gibt. Die wirklich
interessanten Entscheidungen — Nadelfilz statt GFK (2 Fälle), Roboter (3), Wasserhaltung
(5), HL-Anlage (2) — kommen so selten vor, dass das Verfahren sie nicht lernen kann. Bei
drei Beispielen ist jede Regel Zufall.

---

## 5. Entscheidung

Die Regel aus dem Plan (Abdeckung ≥ 50 %, mehr richtig als fehlend) ist erfüllt:
94.5 % Abdeckung, 130 richtig gegen 44 fehlend.

**Das Verfahren trägt — aber die Datenlage begrenzt es, nicht die Methode.**

Der grösste Hebel ist deshalb **nicht** eine Verbesserung der Ähnlichkeitssuche, sondern:

1. **Ein zweites vollständig ausgewertetes Projekt.** Mehr Vielfalt in Durchmesser,
   Schadensbildern und Vorgehen. Erst dann lässt sich messen, ob das Verfahren
   überträgt oder nur Zone 1.15 auswendig kann.
2. **Bögen.** 3 von 64 Fällen. Solange das so bleibt, schweigt das System bei jeder
   Haltung mit Bogen — richtigerweise.

**Was ausdrücklich nicht getan wird:** Die Schwellen senken, damit die Zahlen besser
aussehen. Die Enthaltung ist der Grund, warum man dem Vorschlag überhaupt trauen kann.

---

## 6. Wiederholen

```bash
dotnet run --project tools/KostenfallAufbau -- \
  "D:\Projekte\Zone 1.15\Altdorf_Zone_1.15.json" \
  "D:\Projekte\Zone 1.15\costs\costs.json" "C:\KI_BRAIN" --execute

dotnet run --project tools/KostenfallAufbau -- --messen "C:\KI_BRAIN"
```

Ein bestehender Bericht wird nie überschrieben; jede Messung erzeugt eine eigene Datei
mit Prüfsumme.
