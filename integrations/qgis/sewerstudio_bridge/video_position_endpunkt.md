# Endpunkt fuer die Live-Videoposition

Das QGIS-Plugin (sewerstudio_bridge ab 0.9.0) fragt waehrend der Videowiedergabe
im Takt von rund 250 ms diesen Endpunkt ab. Fehlt er, bleibt alles still (404
wird nicht protokolliert) - die uebrige Bruecke laeuft unveraendert weiter.

## GET /qgis/video_position.json

Antwort (application/json), Authentifizierung wie bei den uebrigen Endpunkten
ueber den Header X-QGIS-Bridge-Token:

    {
      "haltung":  "80475-80462",        // Pflicht - muss dem Feld "haltung" in
                                        //   /qgis/current.geojson entsprechen
      "meter":    12.4,                 // Pflicht - Position ab Aufnahmestart
      "zeit":     "00:01:23.4",         // optional - nur fuer die Textanzeige
      "laenge":   31.5,                 // optional - Bezugslaenge des Videos;
                                        //   fehlt sie, gilt die Katasterlaenge
      "richtung": "in_fliessrichtung",  // optional - sonst aus der aktuellen
                                        //   Haltung uebernommen
      "playing":  true,                 // optional
      "x": 2692813.7, "y": 1192416.4    // optional - fertige LV95-Koordinate;
                                        //   wenn gesetzt, wird nicht gerechnet
    }

Steht kein Video, genuegt HTTP 404 oder eine Antwort ohne "meter".

### Wie aus der Videozeit ein Meterwert wird

Die Umrechnung gehoert nach SewerStudio, weil nur dort die Zuordnung von
Zeitmarken zu Stationierungen bekannt ist. Die Stuetzstellen liegen bereits vor:
jede Beobachtung im Protokoll traegt eine Videozeit (mpeg) und einen Meterwert
(meter_start). Zwischen zwei Stuetzstellen linear interpolieren, ausserhalb mit
der mittleren Vorschubgeschwindigkeit (Haltungslaenge geteilt durch Videodauer)
rechnen. Fehlen Stuetzstellen ganz, bleibt nur die konstante Geschwindigkeit -
fuer eine Grobortung reicht das.

Die Umrechnung Meter -> Koordinate macht QGIS selbst: die Geometrie der aktiven
Haltung liegt als Layer "SewerStudio - Aktuelle Haltung" bereits vor. Weichen
Video- und Katasterlaenge voneinander ab, wird anteilig skaliert.

## POST /qgis/seek  (Rueckweg)

Klickt der Benutzer in QGIS auf die Haltung, meldet das Plugin die Stelle:

    { "haltung": "80475-80462", "meter": 18.75 }

SewerStudio rechnet den Meterwert in eine Videozeit zurueck (dieselben
Stuetzstellen, umgekehrte Richtung) und springt dorthin.

Antworten:

    200   gesprungen
    400   Rumpf unverstaendlich, oder der Meterwert ist keine brauchbare Zahl
    404   es laeuft kein Video (oder der Endpunkt fehlt in dieser Version)
    409   im Video laeuft eine andere Haltung, oder zu dieser Stelle ist keine
          Videozeit bestimmbar

Jede Antwort ausser 200 traegt einen Klartext in "error"; das Plugin zeigt ihn
im Status an. Fehlt der Endpunkt ganz, aendert sich in QGIS nichts.

Der Sprung ist die einzige Wirkung: Er oeffnet kein Video, wechselt keine
Haltung, waehlt nichts aus und veraendert keine Projektdaten. Gesprungen wird
ausschliesslich in der Haltung, die gerade laeuft — die Gegenfahrt
("80462-80475") ist dabei eine andere Haltung und ergibt 409.

## Bedienung in QGIS

Im Bruecken-Dock unten, Bereich "Videoposition (live)":

  - aktiv                          schaltet die Abfrage ein
  - Takt (ms)                      Standard 250 ms
  - Karte folgt                    schiebt den Ausschnitt nach, sobald der
                                   Marker den Rand erreicht
  - In Karte klicken = Video springt   aktiviert das Klickwerkzeug
  - Test (Simulation)              faehrt die aktive Haltung einmal ab, ohne
                                   dass der Endpunkt existieren muss

Darstellung: blau der bereits abgefahrene Teil, grau der Rest, roter Punkt die
aktuelle Position. Es entsteht kein Layer im Projektbaum.
