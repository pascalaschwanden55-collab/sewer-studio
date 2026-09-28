# AP07 – Arbeitsprotokoll: Import und Medienverteilung

Stand: 28.09.2026. Plan: [UMSETZUNGSPLAN.md](UMSETZUNGSPLAN.md), Paket AP07 (Befund W04).
Auswahl: grösste Änderungshäufigkeit der letzten 90 Tage (Import/Medien vor Player, XTF).

## Schritt 1 – Fehlergrenzen und Abbruchstellen des Ein-Knopf-Imports (Ist-Stand)

`ProjectImportOrchestrator.Import`. „Abbruch“ = Benutzerabbruch über das `CancellationToken`.

| Schritt | Fehlergrenze | Bei Ausnahme | Abbruch |
|---|---|---|---|
| Projektstruktur | eigene | Fehler „Projektstruktur“, Lauf geht weiter | vorher geprüft |
| Wiederherstellungspunkt | eigene | nur Meldung, kein Fehler | – |
| Formaterkennung | eigene | Fehler, **Lauf endet** mit Ergebnis; unbekannt/mehrdeutig endet ohne Fehler | – |
| Archivierung + Pläne | eigene | Fehler „Archivierung“, weiter | weitergereicht |
| Quelle einlesen | eigene | Fehler „Quelle einlesen“, weiter | weitergereicht |
| Ergänzende XTF (nur IBAK) | eigene | Fehlerbilanz + Meldung, weiter | weitergereicht |
| KINS-Anreicherung | eigene | Fehler „KINS-Anreicherung“, weiter | **wird als Fehler gezählt** (siehe Restbefund) |
| Katasterabgleich | eigene | nur Meldung „übersprungen“ | weitergereicht |
| SIA405-Anreicherung (nur IKAS) | eigene | nur Meldung | **wird verschluckt** (siehe Restbefund) |
| **Medienphase** (Fotos, Namensprotokolle, Sammelprotokolle/Videos, Dichtheit, Schächte) | **eine gemeinsame** | Fehler „Medienverteilung“, **Rest der Phase entfällt**; gezählte PDF-Rückfall-Haltungen bleiben | weitergereicht |
| – darin Schachtprotokolle | eigene innere | Fehler „Schachtprotokolle“, Phase läuft weiter (Abschlusszeile) | weitergereicht |
| Abschluss / Dateiprüfung | keine | Ausnahme beendet den Import | vorher geprüft |

## Schritt 2 – Medienphase als eigener Baustein

**Vorher geschützt (am unveränderten Code grün):**
- vorhanden: Reihenfolge Fotos → Namen → Kanal, Kopierfehler der Namensprotokolle,
  Dichtheit nach erfolgreicher Kanalverteilung, PDF-Rückfall-Zählung, Fotophase
  (Anfrage, Fehler, Abbruch);
- neu `Import_MediaPhase_DistributesShaftProtocolsLast_AndReportsOnlyRealFailures`
  (Schacht zuletzt, „Parse failed“ ist kein Fehler, Abschlusszeile nach den Schachtmeldungen);
- neu `Import_MediaPhase_ShaftFailureIsReported_WithoutStoppingTheSummary`;
- neu `Import_MediaPhase_KeepsPdfFallbackCount_WhenALaterStepThrows` – hält eine feine Regel
  fest: Die im PDF-Rückfall angelegte Haltung zählt sofort, auch wenn danach die
  Dichtheitsverteilung wirft.

**Umsetzung:**
- `Infrastructure/Import/ImportMediaPhase.cs`: Fotos, Namensprotokolle, Sammelprotokolle,
  Dichtheit, Schachtprotokolle in unveränderter Reihenfolge, mit den bisherigen Meldungen und
  Fortschrittstexten. Braucht nur die sieben Dienste der Phase.
- Meldungen, Fehlerbilanz und die Rückfall-Zählung gehen **sofort** in die Sammelstellen des
  Laufs (`ImportMediaPhaseSinks`). Ein Rückgabewert erst am Ende hätte bei einer Ausnahme mitten
  in der Phase die schon angelegten Haltungen verloren.
- Die gemeinsame Fehlergrenze und der Abbruch bleiben im Orchestrator (ein `try` um `Run`).
- `SynchronerFortschritt<T>`: der bisher private Fortschritts-Helfer, jetzt gemeinsam genutzt.
- Öffentlicher Konstruktor und `Import` unverändert. Orchestrator 961 → rund 750 Zeilen.
- Drei UI-Architekturtests lasen die Verteileraufrufe und das KINS-Suchfeld im Orchestrator
  (`ImportArchitectureGuardTests` ×2, `KinsGesamtprotokollLocatorDependencyTests`). Die Regel
  gilt unverändert, nur der Ort ist jetzt `ImportMediaPhase` (gleich benannte Felder). Der
  Commit `6e81660cb` war ohne den UI-Testlauf entstanden und hatte diese drei rot; behoben im
  Folge-Commit.

**Gegenproben (danach zurückgesetzt, Hash geprüft):** Dichtheit vor Kanal → 3 Tests rot;
Rückfall-Zählung erst am Phasenende → 1 rot; Dichtheitsfehler in der Phase still abgefangen
(Fehlergrenze verteilt) → 1 rot.

## Restbefunde (nicht geändert)

- **KINS-Anreicherung und SIA405-Anreicherung fangen auch den Benutzerabbruch.** Beide
  `catch (Exception)` ohne `OperationCanceledException`-Ausnahme: Ein Abbruch dort wird als
  Fehler gezählt bzw. still verschluckt, statt den Lauf zu beenden. Das widerspricht der Regel
  im Code („ein Abbruch ist kein Fehler und wird weitergeworfen“). Eine Korrektur ändert
  Verhalten und gehört in einen eigenen, getesteten Schritt.

## Schritt 3 und 4 – Video-Suche als eigene Entscheidung, 17 Parameter gruppiert

`ParsedHoldingDistributionController.Distribute` (457 Zeilen, 17 Parameter) verband drei Dinge:
Suchentscheidung (welches Video, welche Haltung), Ablageplan (alle Ziele prüfen) und
Ausführung (kopieren, Links setzen). Isoliert wurde die Suchentscheidung.

**Vorher geschützt (am unveränderten Verteiler grün):** Die Kaskade war auf dieser Ebene
ungetestet. Neu in `ParsedHoldingDistributionControllerTests`: Treffer über Seitenwagen M150/MDB,
über den Link eines importierten Datensatzes, über einen CD-Index-Fotohinweis im Protokoll;
direkter Treffer hat Vorrang vor dem Seitenwagen; Haltungskorrektur über eine eindeutige
Seitenwagen-Zuordnung und über den Videonamen einer bekannten Haltung.

**Umsetzung:**
- `HoldingDistribution/HoldingVideoSearch.cs`: die Kaskade unverändert (Name/Datum → unkorrigierte
  Protokoll-Haltung → Datensatz-Link → Seitenwagen → CD-Index; „mehrdeutig“ ersetzt nur „nicht
  gefunden“; danach Haltungskorrektur) plus die Gegeninspektions-Suche. Schreibt nichts.
- Parametergruppen statt 17 Einzelwerten: `HoldingPdfSource` (Original, abzulegende Datei,
  Seitenbereich), `HoldingVideoSearchContext` (Videoordner, rekursiv, Dateiliste, drei
  Zuordnungskarten), `HoldingDistributionTarget` (Gemeindeordner, verschieben, überschreiben,
  Ordner für Unklares, Verzeichnisschema, Variante). Das Projekt bleibt eigener Parameter: Es
  wird in Suche, Ablage und Verknüpfung gebraucht und gehört keiner Gruppe.
- `HoldingFolderDistributor` baut Suchkontext und Ziel einmal vor der Schleife; der Teil eines
  Sammelberichts erhält wie bisher `MoveInsteadOfCopy = false` (`target with { … }`).
- Verteiler 498 → 387 Zeilen; der Ablageplan und die Ausführung sind unverändert.

**Gegenproben (danach zurückgesetzt, Hash geprüft):** Seitenwagen vor dem direkten Treffer → rot;
Haltungskorrektur über den Videonamen weggelassen → rot.

## Schritt 5 – Robustheit (Befunde aus AP07-PRUEFUNG-VERTEILUNG.md)

Diese Punkte bestanden schon vor dem Umbau; sie ändern bewusst das Verhalten. Jeder Test war am
Code davor rot (`HoldingDistributionRobustnessTests`, 4 von 6 rot; die beiden Abschlusszeilen-Tests
hielten die alte Zählung fest und wurden auf die richtige Aussage umgestellt).

| Befund | Korrektur | Test |
|---|---|---|
| Gescheiterte Videokopie hinterlässt die PDF, Wiederholung legt `_01` an | Eine bytegleiche PDF im Haltungsordner wird wiederverwendet (gleiche Regel wie bisher für Videos, `FindExistingIdenticalFile`). Scheitert nach der abgelegten PDF ein Schritt, kommt ein Fehlerergebnis mit «PDF bereits abgelegt …» und dem Pfad zurück statt einer blossen Ausnahme. Eine wiederverwendete PDF lässt die Quelle unberührt, auch im Verschiebemodus. | `Gesperrtes_Video_meldet_…`, `Zweiter_unveraenderter_Lauf_…` |
| (gleiche Ursache) Begleit-PDFs und Quelldateien (XTF/M150/MDB/XML) bekamen bei jedem Lauf eine weitere Kopie | Dieselbe Wiederverwendung; Meldung «bereits vorhanden» | `Zweiter_unveraenderter_Lauf_…` (XTF) |
| Fehlende oder fremde ausgewählte Dateien verschwinden still | `DistributionPdfSelection`: je Datei ein Fehlerergebnis («nicht gefunden» / «keine PDF»), übrige werden verteilt; gilt für Haltungs-, Schacht- und Dichtheitsauswahl. Leere Auswahl meldet wie bisher «No valid PDF files selected.» | `Fehlende_und_fremde_…`, `Nur_fehlende_Auswahl_…`, `Leere_Auswahl_…` |
| Abschlusszeile «Verteilung: … n Fehler» zählte nur Foto und Kanal | Zahl = Zuwachs der gemeinsamen Fehlerbilanz während der Phase | `Import_MediaPhase_*` (2 und 1 Fehler) |
| Suche nach Quelldateien schrieb Probleme nur ins Warnprotokoll | Jedes nicht durchsuchbare Muster wird ein Fehlerergebnis; das bisher leere äussere `catch` meldet ebenfalls | `Fehler_bei_der_Quelldateisuche_…` |

**Gegenproben (danach zurückgesetzt):** PDF-Wiederverwendung aus → 2 rot; fehlende Datei still →
2 rot; Abschlusszahl wie früher → 2 rot; Suchproblem verschluckt → 1 rot.

Grenzen: Eine korrigierte PDF (Textebene umgeschrieben) ist nur wiederverwendbar, wenn die Korrektur
bytegleich ausfällt; sonst entsteht wie bisher eine zweite Datei. Die Infodateien
`…_VIDEO_MISSING.txt` / `…_VIDEO_AMBIGUOUS.txt` bekommen bei jedem Lauf weiterhin eine neue Datei.
Dass `DistributeCore` die Suchprobleme ins Ergebnis übernimmt, ist nur über den Quelltext geprüft
(ein echter Suchfehler lässt sich im Test nicht zuverlässig erzeugen).

## Offen in AP07

- Ungetestet bleibt der Suchweg „unkorrigierte Protokoll-Haltung“ (braucht eine
  PDF-Korrektur-Metadatenlage im Projekt) und „mehrdeutig ersetzt nicht gefunden“.
- Ablageplan und Ausführung im Verteiler könnten als nächster Schritt getrennt werden (Plan-Schritt 3
  nennt sie ausdrücklich); der Nutzen ist geringer, weil beide bereits linear und durch die
  Verteilungstests geschützt sind.
- Restbefund Abbruch in KINS-/SIA405-Anreicherung (oben).
