# TrainingDataInventory (AP 0.1)

Dieses Werkzeug prueft den lokalen Teacher- und Trainingsbestand. Es liest die
JSON-Quellen direkt, damit bestehende Store-Migrationen keine Dateien veraendern
oder Sicherungen anlegen koennen.

Der Lauf meldet:

- vorhandene und fehlende Teacher-Bilder,
- positive und ausserhalb des Bildes liegende Boxen,
- Train/Val-Kandidaten mit belastbarer Haltung,
- technisch nutzbare Faelle mit unklarer Herkunft als Quarantaene,
- ungueltige oder ueber den Bildrand ragende Boxen als Geometrie-Quarantaene,
- eindeutige Dateinamen-Treffer nur als manuellen Reparaturvorschlag,
- mehrdeutige oder geschuetzte Treffer ohne Reparaturvorschlag,
- Treffer im Eval-/Abnahmebestand,
- aktuelle Quellen und Sicherungen getrennt mit SHA-256-Pruefsumme.

Standardlauf:

```powershell
dotnet run --project tools\TrainingDataInventory -c Release --no-build --
```

Der Bericht und seine Pruefsumme landen standardmaessig unter
`C:\KI_BRAIN\training\reports\`. Quelldateien, Bilder und gespeicherte Pfade
werden nicht veraendert. Auch ein eigenes `--out`-Ziel muss in diesem
Berichtsordner liegen.

Fuer einen schnellen Lauf nur mit den aktuellen JSON-Dateien:

```powershell
dotnet run --project tools\TrainingDataInventory -c Release --no-build -- --current-only
```

Ein Lauf endet mit Code `0`, wenn beide aktuellen JSON-Quellen typisiert gelesen
wurden und der Bericht keinen Fehler enthaelt. Code `1` bedeutet eine fehlerhafte
oder unvollstaendige Pruefung. Ein kontrollierter Abbruch mit `Strg+C` endet mit
Code `130`.

Die Zielangabe wird vor dem Scan geprueft. Dadurch faellt ein falsches `--out`
sofort auf. Der Bericht wird mit dem gemeinsamen JSON-Vertrag geschrieben. Die
SHA-256-Pruefsumme wird ueber exakt dieselben UTF-8-Bytes berechnet. Vor dem
Schreiben werden Ziel und Quellen nochmals geprueft. Verknuepfungen und Junctions
werden weder bei Quelldaten noch beim Ausgabeziel verfolgt.

Eine echte Pfadreparatur gehoert bewusst in einen spaeteren, getrennt
freizugebenden Schritt. Ohne gespeicherten Soll-Hash darf ein gleicher Dateiname
nicht automatisch uebernommen werden.

Fehlt der Eval-Bestand oder wird `--no-hashes` verwendet, meldet der Bericht die
betroffenen Faelle als `evaluationNotChecked`. Sie gelten dann nicht als freie
Train/Val-Kandidaten.

Der Bericht nutzt Schema `2.2`. Pflichtfelder, unbekannte Felder, manipulierte
Triage-Ergebnisse und widerspruechliche Pfadzustaende werden beim Lesen abgelehnt.
Mehrere Eval-Sets werden einzeln geprueft; ein defektes Set sperrt die Freigabe.
Ein Eval-Set gilt nur mit `frozen=true`, passenden Bild-Hashes und einem passenden
Manifest-Hash fuer `_candidates.json` als vollstaendig. Jedes Eval-Bild muss dabei
genau einem Kandidaten ueber dessen `frame_path` zugeordnet sein.
Aktuelle Quellen muessen exakt `teacher_annotations.json` und
`training_samples.json` unter der Wissenswurzel sein. Abgeleitete Werte werden im
Bericht nicht doppelt gespeichert.
