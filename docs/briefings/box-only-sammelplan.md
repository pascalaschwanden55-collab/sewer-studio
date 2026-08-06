# Sammelplan Box-only — Boxen ziehen ohne Segmentierung

> Erstellt 2026-08-05. Ergänzt [trainingsdaten-offensive-kimi.md](trainingsdaten-offensive-kimi.md).
> Betrifft nur die Gewinnung neuer Detect-Trainingsdaten. Der bestehende
> Goldweg mit SAM-Maske bleibt unverändert bestehen.

## Warum das fachlich tragfähig ist

Der exportierte YOLO-Datensatz enthält ausschliesslich Boxen:

```
14 0.529778 0.492598 0.869112 0.893674
```

Klasse, Mittelpunkt X, Mittelpunkt Y, Breite, Höhe. Kein Polygon, keine Maske.
Das trainierte Modell ist ein Detektor (`yolo26m.pt`, Detect-Task) und liest
niemals eine SAM-Maske. Die Maske ist heute ausschliesslich eine selbst gesetzte
Qualitätsprüfung, keine technische Voraussetzung.

**Entscheidender Punkt: Masken sind nachholbar, Boxen nicht.**
SAM 2.1 arbeitet box-getrieben — es bekommt Bild plus Box und erzeugt die Maske
ohne menschliches Zutun. Ein späterer Stapellauf über alle gespeicherten Boxen
kann die Masken jederzeit nachliefern. Eine nie gezogene Box ist dagegen
endgültig verloren. Box-only zu sammeln gibt also nichts auf, sondern verschiebt
nur.

## Was sich ändert

| | bisher | Box-only |
|---|---|---|
| Hand-Box | Pflicht | **Pflicht** |
| SAM-Maske | Pflicht | entfällt |
| 80-Prozent-Regel | Pflicht | ersetzt (siehe unten) |
| Persönliches Akzeptieren | Pflicht | **Pflicht** |
| Eval-Schutz | Pflicht | **Pflicht** |
| Inhaltsadressierte Goldkopie | Pflicht | **Pflicht** |
| Dublettenschutz über Signatur | Pflicht | **Pflicht** |
| KB-Index / Qwen-Retrieval | ja | **nein** (bewusst) |
| Teacher-Eintrag | ja | nein |

## Ersatz für die 80-Prozent-Regel

Die Maskenprüfung hat nie beurteilt, ob die Box *fachlich richtig* ist — nur ob
sie zu dem passt, was SAM segmentiert. Dieser Wächter entfällt. Zwei Ersatzteile,
beide automatisch und beide besser als das, was wegfällt:

**1. Uhrlagen-Plausibilität.** 93,5 Prozent der Protokollbefunde tragen eine
Uhrlage. „7 bis 5 Uhr" ist ein Winkelbereich im Rohrquerschnitt, und die
Ringgeometrie rechnet das Programm bereits (`LiveDetectionGeometryMapper`).
Liegt der Boxmittelpunkt ausserhalb des vom Operateur genannten Sektors, ist das
ein sichtbarer Hinweis — entweder ist die Box falsch gesetzt oder der
Operateurbefund passt nicht zum Bild. Beides will man wissen. Der Hinweis warnt,
er sperrt nicht.

**2. Grössen-Plausibilität.** Boxen unter 1 Prozent und über 90 Prozent der
Bildfläche werden markiert. Bildfüllende Boxen sind die Vorstufe des
BBox-Kollapses, der beim alten Modell bereits belegt ist.

Beides sind Warnungen im Prüfplatz, keine Sperren. Der Mensch entscheidet.

## Was bewusst verloren geht

- **Qwen-Retrieval.** Box-only-Samples werden nicht in die Wissensdatenbank
  indexiert. Sie helfen dem Detektor, nicht dem Textmodell. Das ist richtig so:
  Ohne geprüfte Maske und fertige Beschreibung gehören sie nicht in den
  Few-Shot-Kontext.
- **Segmentierungsmodell.** Aus reinen Boxen lässt sich kein Seg-Modell
  trainieren. Falls das je gewünscht ist, vorher der SAM-Stapellauf.
- **Copy-Paste-Augmentierung.** Braucht Masken. Ebenfalls über den Stapellauf
  nachholbar.

## Der Arbeitsablauf

1. **Kandidatenliste je Klasse** über `training/scripts/collect_class_candidates.py`
   (vorhanden). Gefiltert gegen Gold, Eval-Schutz und Byte-Dubletten, höchstens
   1–2 Bilder je physischer Haltung.
2. **Bilder nach `gold_inbox\<Hauptcode - Klartext>`** kopieren. Kundenoriginale
   auf `D:\` bleiben unangetastet.
3. **Im Prüfplatz Box-only-Modus:**
   - Bild wird angezeigt, dazu der Operateurcode, Meter und Uhrlage als Referenz
   - Box ziehen, Klasse bestätigen oder korrigieren, weiter
   - kein SAM-Aufruf, keine Wartezeit, kein Maskenurteil
4. **Alle sichtbaren Objekte der 13 Klassen boxen**, nicht nur das protokollierte.
5. **Bild fertig** → nächstes.

### Regel 4 ist die wichtigste im ganzen Plan

Was YOLO nicht als Box bekommt, gilt als Hintergrund. Ein Bild mit einem
geboxten `BAH_schadanschluss`, auf dem ein sichtbarer `BCA_anschluss` ungeboxt
bleibt, bringt dem Modell bei: *hier ist kein BCA*. Bei wenigen hundert
Trainingsbildern richtet das echten Schaden an und ist ein plausibler Mitgrund
dafür, dass elf Klassen heute 0 Prozent Recall haben.

Der Operateurcode nennt **einen** Befund. Das Bild zeigt oft mehrere. Die
Mehrfach-Objekt-Funktion („Weiteres Ereignis auf diesem Bild") ist genau dafür
gebaut.

Lieber zehn Bilder vollständig als dreissig halb.

## Technische Umsetzung — additiv, nicht am Bestand schrauben

Nicht `ManualGoldTrainingPolicy` oder `SamMaskValidator` aufweichen. Der
bestehende, streng geprüfte Goldweg bleibt, wie er ist.

Stattdessen ein zweiter, klar gekennzeichneter Weg:

1. **Neuer Sample-Zustand** für „Box vorhanden, Maske offen" — abgrenzbar vom
   heutigen `Draft` (unvollständig, gehört in die Reparaturliste) und von
   `Approved` (vollständiges Gold mit Maske). Diese Samples sind für den
   Detect-Export zugelassen und für KB und Teacher gesperrt.
2. **Box-only-Modus im Training Studio** als eigener Warteschlangentyp, analog
   zur bestehenden Reparaturliste. Kein SAM-Aufruf im Speicherweg.
3. **Export**: `TrainingExportPlanInputBuilder` muss den neuen Zustand als
   exportfähig kennen. Der Plan selbst ändert sich nicht — er schreibt ohnehin
   nur Boxen.
4. **Stapellauf für Masken** als eigenes Skript, jederzeit nachträglich
   ausführbar: liest alle Box-only-Samples, ruft SAM mit Bild und Box, schreibt
   die Maske zurück, prüft die 80-Prozent-Regel und hebt bestandene Samples auf
   vollwertiges Gold. Läuft ohne Menschen.
5. **Tests**: Speicherweg ohne Maske, Exportfähigkeit des neuen Zustands,
   KB-Sperre, Uhrlagen- und Grössenwarnung.

Architekturregeln des Projekts beachten: neue Workflow-Klassen nach
`src/AuswertungPro.Next.Application/UseCases/`, eigener Dienst mit Interface,
DI-Eintrag in `ServiceProviderRegistrationMap`, mindestens ein fokussierter Test.

## Reihenfolge der Klassen

Nach verfügbarem Material, knappste zuerst. Stand nach dem BAH-Piloten:

| Klasse | Haltungen heute | verfügbar | Priorität |
|---|---:|---:|---|
| BAI Dichtung | 11 | 68 | 1 |
| BBA Wurzeln | 13 | 19 (+Video) | 2 — Material knapp |
| BBB Anhaftung | 15 | 99 | 3 |
| BBF Infiltration | 17 | 77 | 4 |
| BBC Ablagerung | 21 | 149 | 5 |
| BAC Bruch | 28 | 106 | 6 |
| BAA Verformung | 30 | 144 | 7 |
| BAJ Verbindung | 43 | 323 | 8 |
| BAF Oberfläche | 54 | 159 | 9 |
| BAB Riss | 46 | 150 | 10 |
| BAH Schadanschluss | 64 | 49 (+32 Video) | 11 |
| BCA Anschluss | 108 | 364 | erreicht |
| BCC Bogen | 118 | 374 | erreicht |
| BBD Boden | 0 | 1 | nicht belegbar |

## Aufwand

Gemessen im BAH-Piloten mit SAM: 0,78 Minuten pro Sample, 0 Prozent Ausschuss.
Ohne SAM entfällt Wartezeit und Maskenurteil — realistisch 0,4 bis 0,6 Minuten.

Rund 758 fehlende Klasse-Haltung-Plätze bis zum Ziel von 100 je Klasse.
Bei einem Label je Platz und 0,5 Minuten: **etwa 6 bis 10 Stunden** reine
Zeichenzeit, plus Nachladen und Pausen realistisch 10 bis 15 Stunden.

Wichtig: Regel 4 erhöht die Zahl der Boxen je Bild, senkt aber die Zahl der
benötigten Bilder. Netto bleibt der Aufwand ähnlich, die Datenqualität steigt.

## Was in Kraft bleibt

- Negativbilder: von 9 auf 300–500. Braucht gar keine Boxen, nur die
  Feststellung „nichts sichtbar". Unabhängig, jederzeit, billigster Gewinn.
- Die 100-Haltungen-Marke bleibt Arbeitshypothese mit Abbruchpunkt an den
  Meilensteinen A und B.
- BBD bleibt nicht belegbar. Es wird ein 12- bis 13-Klassen-Modell ohne BBD.
- Vor der Abnahme ein frischer, zuvor unberührter Holdout.
- Die OSD-Entscheidung ist weiterhin offen und gehört vor die Massensammlung.
