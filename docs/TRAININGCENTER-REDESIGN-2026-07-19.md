# Training Center Redesign — Design-Dokument (2026-07-19)

> **Status:** Design abgestimmt mit Pascal (Brainstorming 19.07.). Umsetzung folgt als eigener
> Implementierungsplan (Opus), Etappe fuer Etappe, additiv zum bestehenden Fenster.
> **Quellen:** Werte → `docs/SYSTEM-FAKTEN.md` · Architektur → `docs/CODEBASE-KARTE.md` · Regeln → `CLAUDE.md`.

## 1. Warum neu

Das heutige Training Center buendelt sechs Reiter und neun Querschnitts-Buttons in einem
Fenster (~1100 Z. XAML, ~700 Z. Code-behind, ViewModel mit 45 Properties / 25 Commands /
17 Konstruktor-Parametern, 154 Hilfsdateien). Befund der Bestandsaufnahme 19.07.:
Verantwortungen vermischt, Nutzen der Pruefarbeit unsichtbar, Bedienung zaeh
(7 Stationen pro Review-Klick, ein geteilter Abbruch-Schalter fuer vier Laeufe,
Yellow-Faelle gehen beim Schliessen verloren, Lehrer-Reiter ohne ViewModel).
**Der Maschinenraum ist gut** (AP-0.3-Exportkette, TrainingReviewSamWorkflow,
Eval-Schutznetz, sichere Selbsttraining-Defaults) — **die Verpackung ist das Problem.**

**Entschieden: Ansatz A — neues Fenster, alter Motor.** Neue schlanke Oberflaeche;
bewaehrte Services werden angeschlossen, nicht neu gebaut; das alte Fenster bleibt
parallel lauffaehig, bis alles umgezogen ist.

## 2. Leitidee

> Ein Pruef-Arbeitsplatz, der beim Oeffnen sagt, was sich gerade am meisten lohnt,
> bei dem Bestaetigen/Korrigieren + Box/Maske EIN Handgriff sind — und der schwarz
> auf weiss zeigt, dass die KI durch die Arbeit besser wird.

Der gemeinsame Kern-Handgriff (ueberall gleich, Center und Player):
**Box ziehen → SAM segmentiert → KI schlaegt Code vor → Mensch codiert → KB.**
Akzeptierte UND korrigierte Faelle gehen als geprueftes Wissen in die KB;
Korrekturen ueberschreiben nie das Original (eigene Korrektur-Fassung).

## 3. Die drei Arbeitsmodi

### Modus 1 — Pruefplatz (Fotos & Einzelframes)

Layout: Bild gross links; rechts KI-Vorschlag (Code, Sicherheit, 3 aehnlichste
gepruefte KB-Faelle als Mini-Bilder) und Codierung (Code-Auswahl, Uhrlage per
Ring-Klick, Stufe per Zifferntaste); unten Warteschlange mit Fortschritt
("Noch 6 — danach BBA-Ziel erreicht").

Ablauf:
1. Bild aus Warteschlange oder Foto/Ordner laden. Bei KI-Befunden sind Box, Maske
   und Vorschlag schon da — nur pruefen.
2. Uebersehenes selbst markieren: Box ziehen → Maske < 1 s (SAM box-getrieben,
   Bild vorgeladen) → Vorschlag automatisch (cls-Klassifikator + KB-Retrieval).
3. Tastatur-getrieben: `A` akzeptieren, `K` korrigieren, `V` verwerfen, `→` naechstes.
4. **Pflicht ist nur der VSA-Code.** Uhrlage/Stufe optional; fehlende Angaben senken
   die Trainings-Prioritaet des Samples, machen es aber nicht wertlos.

Beim Speichern (unsichtbar): geprueftes Sample → KB-Index; Box+Maske → Teacher-Pool-
Kandidat; **Eval-Schutz prueft jedes Bild hart** (eingefrorene Mess-Haltungen werden
abgewiesen — bestehender EvalContaminationGuard an allen Stellen).

### Modus 2 — Haltungs-Training (Video-Loop, "bis bestanden")

1. Haltung/Video waehlen → KI-Volllauf → Befundliste; Video springt je Befund zum Meterstand.
2. Korrigieren mit dem Box-Handgriff → korrigierte Fassung wird **Soll-Stand der Haltung**
   (persistiert, versionierbar).
3. Erneuter KI-Lauf → automatischer Vergleich gegen Soll ueber die **vorhandene
   ereignisbasierte Logik** (Haltung+EventId; ein Schaden ueber mehrere Frames zaehlt einmal):
   richtig / falsch codiert / uebersehen / Phantom. Toleranzen: Code exakt, Meter ±0,5 m,
   Uhrlage ±1.
4. Wiederholen bis **"Bestanden"**: alle Soll-Ereignisse gefunden, Codes korrekt,
   keine Phantom-Befunde ab Stufe 3. Gruener Haken an der Haltung.
5. Ehrlichkeit: wiederholt Uebersehenes wird angezeigt als "lernt die KI erst nach dem
   naechsten Modelltraining" (zwischen zwei Laeufen aendert sich nur der KB-Kontext,
   nicht die Modell-Gewichte).

### Modus 3 — Startseite mit Fortschritt

- **"Das lohnt sich jetzt":** priorisierte Vorschlaege (unsichere Befunde,
  unterrepraesentierte Codes, angefangene Haltungen) aus einem echten C#-Service.
- **Abdeckungskarte:** Ampel je Schadenscode mit Ziel ("noch 8 BAB bis Trainingsreife").
- **Messlatte:** Eval-Lauf gegen eingefrorene, nie geuebte Haltungen. Anzeige strikt
  getrennt: "Bestanden (geuebt)" ≠ "KI generalisiert". Der offene AP-0.4-Punkt
  (120er-Set ohne Severity/EventId) wird hier als sichtbare Aufgabe gefuehrt.
- **Nebenbereiche** (zweite Reihe): Export mit sichtbarem Sperr-Status
  (class_map-Migration pending, Registry-Freigabe fehlt — anzeigen, nie umgehen),
  Bestand/Lehrer-Galerie, Selbsttraining.

## 4. Ehrlichkeits-Regeln (nicht verhandelbar)

1. **Geuebt ≠ besser.** Bestanden auf einer geuebten Haltung belegt keine
   Generalisierung — die zeigt nur das eingefrorene Eval-Set. Beide Zahlen getrennt.
2. **Eval-Schutz bleibt hart** an jeder Speicherstelle (Load, Save, KB-Index,
   Retrieval, Export-Plan). Kein Eval-Frame in KB/Training, nie.
3. **Export-Sperren sichtbar machen statt verstecken**; nie automatisch umgehen.
4. **Korrekturen ueberschreiben nie das Original** (Korrektur-Fassung, Nachvollziehbarkeit).
5. **Kein Auto-Gold:** Selbsttraining-Defaults (RequireHumanReview=true, AutoAccept=1.0)
   bleiben.

## 5. Architektur

Neues Fenster (Arbeitstitel `TrainingStudioWindow`), additiv; alter Motor bleibt.
Muster: duenne UI-Huelle → Workflow → Service (wie TrainingReviewSamWorkflow / AP-0.3-Kette).

**Neue Services (Interface + DI, ViewModels duenn):**

| Service | Verantwortung |
|---|---|
| `IAnnotationWorkbenchService` | Box → SAM → Vorschlag → Speichern (KB + Teacher-Pool + Eval-Schutz). EIN Service fuer Center und Player. |
| `IHaltungTrainingLoopService` | Soll-Stand je Haltung verwalten, KI-Laeufe ereignisbasiert vergleichen, Bestanden-Urteil. |
| `ITrainingPriorityService` | Startseiten-Priorisierung + Abdeckungsziele (Logik des bisherigen active-learning-curator-Konzepts als Code). |

**Wiederverwendet (nicht neu bauen):** VisionPipelineClient, TrainingReviewSamWorkflow-
Muster, TrainingSamplesStore, KnowledgeBaseManager, RetrievalService,
EvalContaminationGuard, ereignisbasierte Eval-Klassen (EvalSetEventScorer u. a.),
TrainingYoloExportCoordinator (Export-Nebenbereich), QualityGateService.

**Bereinigungen im Zuge des Umzugs:** getrennte Busy-/Abbruch-Verwaltung je Lauf
(kein geteilter CTS), Yellow-Faelle persistent (kein Verlust beim Schliessen),
ein einziger Ablehnen-Weg, Lehrer-Galerie bekommt beim Umzug ein ViewModel,
tote Teile (DistributeHaltungCommand-Leiche, unsichtbarer Hinweistext) entfallen.

## 6. Etappen (jede einzeln nutzbar, jede mit Tests)

1. **Pruefplatz** — Fenster + `IAnnotationWorkbenchService` + Foto-/Frame-Quellen + KB-Speichern.
2. **Player-Integration** — derselbe Service im Player (Box ziehen waehrend Video laeuft).
3. **Haltungs-Loop** — Soll-Stand, Vergleich, Bestanden; nutzt Etappe-1-Handgriff.
4. **Startseite + Messlatte** — Priorisierung, Abdeckungskarte, Eval-Anbindung.
5. **Umzug & Stilllegung** — Nebenbereiche (Export/Bestand/Selbsttraining) umziehen,
   altes Fenster stilllegen (Code bleibt bis zur naechsten Grossversion als Fallback).

## 7. Tests & Sicherheit

- Verhaltenstests je neuem Service (Workbench-Speichern inkl. Eval-Abweisung;
  Loop-Vergleich mit Toleranzen; Prioritaets-Ranking deterministisch).
- XAML-Binding-Pruefung fuer alle neuen Views.
- Golden-Pfad-Test: Box → Maske → Vorschlag → Speichern erzeugt exakt ein Sample,
  einen Teacher-Kandidaten, einen KB-Eintrag.
- Bestehende Guards (Eval, Export-Plan, Atomic Writes) bleiben unveraendert und
  werden von den neuen Services benutzt, nicht dupliziert.

## 8. Nicht-Ziele

- Kein neues Modelltraining-Verfahren (Training selbst bleibt AP-0.3-Weg).
- Keine Aufweichung der Export-Sperren oder Eval-Regeln.
- Kein Umbau des Sidecars (bestehende Routen genuegen: /segment/sam, /classify/yolo,
  /detect/yolo — siehe SYSTEM-FAKTEN.md).
- Keine automatische 8B→32B-Eskalation o. ae. — Modelle wie in SYSTEM-FAKTEN.md.
