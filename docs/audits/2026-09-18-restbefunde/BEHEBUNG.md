# Restbefunde des Audits: 11 bis 18

Stand 19.09.2026. Viertes und letztes Reparaturpaket nach `2026-09-18-dateischutz`,
`2026-09-18-videoauswertung` und `2026-09-18-portabilitaet`. Damit sind alle 18 Befunde
des Audits vom 18.09.2026 bearbeitet.

## Behobene Befunde

| Nr. | Befund | Kern der Korrektur |
|---|---|---|
| 11 | Zwei KB-Einstiege umgehen den Schutz reservierter Pruefdaten | `GuardedRetrievalFactory` ist die einzige Erzeugung; der Pruefdaten-Root wird einmal beim Start gesetzt |
| 12 | Verspaetetes Laden ersetzt einen begonnenen Entwurf | `ShellViewModel.ProjectGeneration` + Sperre der Projektbefehle waehrend des Ladens |
| 13 | Umbenennung laesst historische Medienverweise zurueck | `OriginalFotoPaths` und `ImportVideoPaths` wandern mit |
| 15 | Aeltere Einstellungsspeicherung ueberschreibt neuere | `SettingsWriteOrder` entscheidet und schreibt im selben Abschnitt |
| 16 | Abgelehnte Trainingsfaelle bleiben in der Wissensdatenbank | `TryDeindex` liefert ein Ergebnis; das Training Center meldet den Rest |
| 17 | Fremde Suchvektoren nach Modellwechsel verwendbar | Retrieval laedt nur Vektoren des aktuellen Modells; Cache ist modellgebunden |
| 18 | LiveControl wartet bei Umlauten auf fehlende Zeichen | gemeinsamer `BoundedHttpRequestReader.ReadBodyAsync` statt eigener Schleife |

## Bemerkenswertes

**11 — Der Schutz darf nicht am Aufrufer haengen.** Drei Stellen erzeugten eine Suche,
nur eine reichte die Sperrliste weiter. Ein Transportweg ueber die Einstellungen haette
dieselbe Falle gehabt: Vergisst ein Aufrufer ihn, fehlt der Schutz wieder still. Deshalb
setzt die Anwendung den Pruefdaten-Root EINMAL beim Start
(`GuardedRetrievalFactory.ConfigureDefaultEvalSetRoot`), und ein Waechter verbietet
`new RetrievalService(` ueberall sonst im Produktivcode. Beim Selbsttraining war die
Sperrliste sogar schon zwei Zeilen tiefer geladen — sie ging nur nicht an die Suche.

**13 — `OriginalFotoPaths` ist slotgebunden.** Die Liste gehoert Index fuer Index zu
`FotoPaths`. Sie wird deshalb ausdruecklich NICHT dedupliziert; sonst verschiebt sich die
Zuordnung zwischen Anzeigebild und unveraenderter Quelle.

**15 — Eine Vorabfrage haette nicht genuegt.** Die erste Fassung der Regel hiess
`TryBeginWrite(sequence)`. Der eigene Lasttest zeigte, dass sich zwischen Freigabe und
Schreiben dieselben zwei Auftraege erneut ueberholen koennen — also genau der Fehler, um
den es geht. `SettingsWriteOrder.Write(sequence, action)` entscheidet und schreibt jetzt
im selben kritischen Abschnitt.

**17 — Der Fehler war messbar dramatisch.** Im Rot-Nachweis kam der Vektor eines ANDEREN
Embedding-Modells mit Score 1,0 zurueck, also als perfekter Treffer. Vor der Korrektur
wurde die echte Wissensdatenbank geprueft: 1684 Vektoren, alle `nomic-embed-text`, kein
Mischbestand — der strenge Filter gefaehrdet den vorhandenen Bestand nicht.

**12 — Beide Haelften umgesetzt.** Die Uebernahme prueft den Projektstand vom Ladebeginn
(`ProjectGeneration`), UND `ProjektLadeGuard` sperrt waehrend des Ladens Neu, Oeffnen und
Projektwechsel ueber denselben `IShellOperationGuard`-Weg, den Import und Export schon
nutzen. Die Sperre verhindert den Fall; die Pruefung faengt ihn ab, falls er doch
entsteht. Die Freigabe laeuft ueber `Dispose`, damit auch ein Ladefehler sie loest —
das prueft ein eigener Test.

**16 — Ein Bestandstest wurde bewusst umbenannt.** Er hiess
`TryDeindex_schluckt_deindex_fehler` und hielt damit genau das Verhalten fest, das der
Audit als Fehler erkannt hat. Neu:
`TryDeindex_bricht_bei_einem_Fehler_nicht_ab_meldet_ihn_aber`. Die persoenliche
Entscheidung bleibt gespeichert — das war richtig; nur der Fehler verschwindet nicht mehr.

## Nachweis

- Alle Korrekturen testgetrieben: erst ein roter Nachweis, dann die kleinste Korrektur.
- **Sabotageproben** (Fix zurueckgedreht, zugehoerige Tests werden rot):
  - Sperrliste in der Fabrik durch `null` ersetzt -> `Fabrik_sperrt_reservierte_Pruefhaltungen…` rot.
  - (Die uebrigen Befunde sind durch ihren eigenen Rot-Lauf vor der Korrektur belegt.)
- Nebenwirkung der Korrektur zu Befund 11: `ServiceProvider.cs` erreichte 1003 Zeilen und
  loeste den Waechter `No_new_production_file_exceeds_1000_lines` aus. Der Aufbau der
  Wissensdatenbank liegt deshalb unveraendert in der neuen Teildatei
  `ServiceProvider.KnowledgeBase.cs`; die Hauptdatei ist bei 927 Zeilen. Der Waechter hat
  den Zuwachs korrekt gemeldet — ein Kuerzen der Kommentare haette nicht genuegt.

## Grenzen

- Keine Sichtprobe im laufenden Programm; alle Belege stammen aus Tests.
- Der Modellfilter im Retrieval ist fail-closed: Ohne Vektoren des aktuellen Modells gibt
  es keinen KB-Kontext mehr. Das ist gewollt, macht aber nach einem Modellwechsel einen
  KB-Neuaufbau noetig.
- Befund 16 meldet den Fehler sichtbar, sperrt den nicht entfernten Eintrag aber nicht
  zusaetzlich gegen die Verwendung als Vergleichsfall. Eine solche Sperre waere der
  naechste Ausbauschritt.
