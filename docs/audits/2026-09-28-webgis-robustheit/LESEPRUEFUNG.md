# Leseprüfung am echten WebGIS (28.09.2026, nur lesend)

**Auftrag Pascal:** «Wichtig ist, dass nichts geändert wird in der Datenbank. Nur die Logik verstehen und prüfen.»

**Werkzeug:** `tools/WebGisLesepruefung` mit Nur-Lesen-Schutz (siehe dessen README). Anmeldung durch Pascal im
sichtbaren Browser. Gelesen: Haltung 80480-80478, Schacht 80478 (Bürglen), je ihre Massnahme und der
Sanierungskatalog. **34 Leseaufrufe, 0 abgewiesen, kein Schreibaufruf.** Die Rohantworten liegen nur lokal
(Katasterdaten, nicht im Repository).

## Befunde

1. **`newId` ist die OBJECTID, nicht die GlobalID.** Beim Anlegen am 21.09. meldete der Server `newId: "66921"`.
   Die Massnahmenmaske ist über die GlobalID adressiert (`objectKeyField: globalid`,
   `objectKeyValue: 3855a0d1-…`); die Zahl 66921 kommt in keiner Leseantwort vor. Eine neu angelegte
   Massnahme lässt sich also nicht direkt über `newId` zurücklesen. Sicherer Weg für WG05: Liste am
   Elternobjekt vorher/nachher vergleichen, die neu hinzugekommene GlobalID lesen und deren Werte prüfen.
2. **Die erste Spalte der Massnahmenliste ist «Zeitpunkt», nicht das Sanierungsjahr.** Spaltendefinition des
   Servers: Zeitpunkt (`zeitpunkt`, Datum), Art, Status, Ausführender (`ausfuehrender`), GlobalId. Bei beiden
   Massnahmen war «Zeitpunkt» leer; das Sanierungsjahr (01.01.2026) steht nur in der Massnahme selbst.
   Folge im Code bis heute: Der Jahresvergleich (Reparatur 2020 ≠ Reparatur 2026, Stand 24.09.2026) griff
   nie; jede zweite Massnahme gleicher Art galt als «bereits vorhanden». **Behoben:**
   `WebGisMassnahmenJahr` liest das Jahr aus der Massnahme nach — nur wo der Vergleich davon abhängt, nur
   lesend; ein Lesefehler lässt das Jahr offen (dann wie bisher «vorhanden», kein Doppel).
   Das nachgelesene Feld ist laut Servermaske **«Sanierungsjahr»** der Massnahme (refId `e1b9c707…`).
   Es ist **nicht das Baujahr** der Haltung oder des Schachts; die Massnahmenmaske hat gar kein Baujahr.
   Das Baujahr gehört zum Objekt (eigene Regeln: nie überschreiben, nur ein leeres Feld füllen) und wird
   hier weder gelesen noch verglichen.
3. **Offen (Frage an Trigonet):** Die vierte Spalte heisst laut Server «Ausführender», zeigt aber bei beiden
   Objekten den Verfahrenstext («Schlauchverfahren», «Vermörtelung»). SewerStudio liest sie als Verfahren,
   und die Werte passen. Warum der Server dort das Verfahren zeigt, geht aus der Antwort nicht hervor.

## Nicht geprüft

Kein Schreibweg (bewusst). Verhalten bei abgelaufener Sitzung, 401/403 und Timeout wurde nicht ausgelöst.
