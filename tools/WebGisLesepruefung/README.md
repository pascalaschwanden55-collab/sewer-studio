# WebGIS-Leseprüfung (nur lesend)

Liest am echten WebGIS (GEONIS-Attributeditor), wie Haltung, Schacht, ihre Sanierungsmassnahmen und der
Sanierungskatalog aussehen — **ohne irgendetwas zu ändern**.

- Anmeldung wie im Programm: Ein sichtbares Browserfenster öffnet sich, du meldest dich selbst an. Das
  Passwort sieht das Werkzeug nie; der Benutzerkontext kommt aus den SewerStudio-Einstellungen.
- **Nur-Lesen-Schutz** (`NurLesenHandler`): Durchgelassen werden nur `getLayoutDataCombined`,
  `getEmptyData`, `getControlValues` (GET) und die Suchaktionen `GET_QUERY_FULL_TEXT` / `GET_RESULTS` am
  synserver. Alles andere, insbesondere `saveData`, wird vor dem Netz abgewiesen und gezählt. Vor jeder
  Anmeldung prüft das Werkzeug diesen Filter selbst und bricht ab, wenn er nicht hält.
- Das Werkzeug ruft `SchreibeAsync` und `ErstelleSanierungAsync` nirgends auf.

```
dotnet run --project tools/WebGisLesepruefung -- --ausgabe <Ordner> [--haltung 80480-80478] [--schacht 80478]
dotnet run --project tools/WebGisLesepruefung -- --nur-selbstpruefung
```

Ausgabe: je Antwort eine JSON-Datei (ohne Anmeldedaten in den Dateinamen) und `BERICHT.txt`. Die Antworten
enthalten Katasterdaten — **nicht ins Repository legen**.

Befunde der ersten Lesung (28.09.2026): `docs/audits/2026-09-28-webgis-robustheit/LESEPRUEFUNG.md`.
