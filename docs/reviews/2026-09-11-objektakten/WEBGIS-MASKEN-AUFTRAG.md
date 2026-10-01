# Auftrag an Claude Desktop: WebGIS-Detailmasken erfassen

Stand: 11.09.2026. Dieser Text ist zum Kopieren in Claude Desktop gedacht.
Die Sitzung dort kennt dieses Projekt nicht — der Auftrag ist deshalb selbsttragend.

Hintergrund: SewerStudio hat die WebGIS-**Hauptmaske** von Haltung und Schacht bereits
nachgebaut (96 + 65 Felder, 78 Auswahlkataloge mit 1158 Einträgen). Was fehlt, sind die
**Detailmasken hinter den Aufklapplisten** (Bauwerksteile, Einläufe, Unterhaltsmassnahmen,
Dichtheitsprüfungen, Inspektionen, GEP Massnahmen, Pumpen, Überläufe, Einzugsgebiete …).
Ohne deren echte Feldliste und Auswahlwerte können sie nicht gebaut werden.

---

## Der Auftrag (ab hier kopieren)

Du hilfst mir, Eingabemasken aus einem WebGIS auszulesen. Das WebGIS ist das
Abwasserkataster meines Kantons; ich habe dafür einen eigenen Zugang und lese nur
meine eigenen beruflichen Daten.

**Adresse:** <HIER DIE ADRESSE DES WEBGIS EINSETZEN>
**Beispiel-Haltung:** <z. B. 525145-505377>
**Beispiel-Schacht:** <Schachtnummer>

### Was du tun sollst

Öffne das Beispielobjekt. In der Objektmaske gibt es unten mehrere aufklappbare Listen
(Tabellen). Hinter jeder Zeile einer solchen Liste steckt eine **Detailmaske** mit
Eingabefeldern. Diese Detailmasken sollst du einzeln öffnen und vollständig beschreiben.

Für **jede** Maske aus der Liste unten erzeugst du **eine JSON-Datei**.

### Was du erfassen musst

Je Detailmaske:

- Den Titel der Liste, so wie er im WebGIS steht.
- Die Spaltenüberschriften der Übersichtstabelle, in der angezeigten Reihenfolge.
- **Jedes** Eingabefeld der geöffneten Detailmaske, in der angezeigten Reihenfolge:
  - die sichtbare Beschriftung, zeichengenau samt Umlauten
  - die technische Kennung des Eingabefelds, falls im Seitenquelltext vorhanden
    (z. B. `id` oder `name` des Elements)
  - die Feldart: `text`, `mehrzeilig`, `zahl`, `datum`, `auswahl`, `kontrollkaestchen`
  - ob es ein Pflichtfeld ist
  - ob es nur lesbar ist (ausgegraut / nicht editierbar)
  - falls vorhanden: Einheit (mm, m, CHF, %), Maximallänge, Zahlenbereich
  - bei `auswahl`: **alle** Einträge der Aufklappliste, nicht nur die sichtbaren —
    je Eintrag der gespeicherte Wert (`value` im Quelltext) und der angezeigte Text
- Falls die Maske in Abschnitte oder Reiter unterteilt ist: den jeweiligen Abschnittsnamen.

Die Auswahlwerte sind der wichtigste Teil. Am verlässlichsten liest du sie aus dem
Seitenquelltext der geöffneten Maske (die `<option>`-Elemente), nicht durch Abtippen
des sichtbar Aufgeklappten.

### Ausgabeformat

Eine Datei je Maske, benannt `<objektart>-<liste>.json`, zum Beispiel
`haltung-inspektionen.json`. Aufbau genau so:

```json
{
  "maske": "Inspektionen",
  "objektart": "haltung",
  "quelle": {
    "adresse": "https://…",
    "beispielobjekt": "525145-505377",
    "gelesenAm": "2026-09-11"
  },
  "spalten": ["Datum", "Verfahren", "Richtung", "Nummer"],
  "felder": [
    {
      "reihenfolge": 1,
      "abschnitt": "Ereignis",
      "label": "Bezeichnung",
      "kennung": "inputField_attributeForm_bezeichnung",
      "feldart": "text",
      "pflicht": true,
      "nurLesen": false,
      "maxLaenge": 41,
      "einheit": null,
      "auswahl": null
    },
    {
      "reihenfolge": 2,
      "abschnitt": "Ereignis",
      "label": "Art",
      "kennung": "inputField_attributeForm_art",
      "feldart": "auswahl",
      "pflicht": false,
      "nurLesen": false,
      "maxLaenge": null,
      "einheit": null,
      "auswahl": [
        { "index": 0, "originalCode": "0", "label": "Unbekannt" },
        { "index": 1, "originalCode": "1", "label": "Kanalfernsehen" }
      ]
    }
  ]
}
```

Regeln zum Format:

- `originalCode` ist der **gespeicherte** Wert aus dem Quelltext, `label` der **angezeigte**
  Text. Beide werden gebraucht — verwechsle sie nicht und lasse keinen davon weg.
- `index` ist die Position in der Liste, beginnend bei 0.
- Felder, die es nicht gibt, setzt du auf `null` — nicht weglassen, nicht erfinden.
- Umlaute ausschreiben, Datei als UTF-8 speichern.

### Regeln, die du einhalten musst

1. **Nur lesen.** Klicke im WebGIS niemals auf Speichern, Übernehmen, Anwenden, Neu
   anlegen mit Speichern, Löschen oder Kopieren. Wenn du versehentlich etwas geändert
   hast, brich ab und sag es mir sofort.
2. **Nichts erfinden.** Wenn du eine Beschriftung, einen Auswahlwert oder eine Feldart
   nicht sicher erkennst, schreib in die Datei `"unsicher": true` samt kurzer Begründung.
   Eine geratene Angabe ist schlimmer als eine fehlende.
3. **Leere Listen.** Hat ein Objekt in einer Liste keine Zeile, kannst du die Detailmaske
   oft über die Schaltfläche „Neu" oder „+" leer öffnen. Das reicht — die Felder und
   Auswahlwerte sind auch dort vollständig. Diese leere Maske danach **verwerfen, nicht
   speichern**. Geht auch das nicht, schreib eine Datei mit
   `"maskeNichtErreichbar": "kein Eintrag vorhanden und Neu nicht möglich"`.
4. **Keine Zugangsdaten** in die Dateien oder in deine Antwort übernehmen.
5. **Keine Personendaten** ausser dem, was zur Maske gehört. Konkrete Eigentümernamen aus
   dem Beispielobjekt brauchst du nicht zu übernehmen — es geht um die Maske, nicht um
   den Datensatz.

### Welche Masken

Am **Schacht** (14):

1. Bauwerksteile
2. Einläufe
3. Ausläufe
4. Absperr-/Drosselorgane
5. Pumpen
6. Überläufe
7. Mechanische Vorreinigung
8. Unterhaltsmassnahmen
9. Dichtheitsprüfungen
10. Inspektionen
11. GEP Massnahmen
12. Einzugsgebiete SW
13. Einzugsgebiete RW
14. Einzugsgebiete MW

An der **Haltung** (6):

15. Bauwerksteile
16. Einläufe
17. Unterhaltsmassnahmen
18. Dichtheitsprüfungen
19. Inspektionen
20. GEP Massnahmen

Sind zwei Masken augenscheinlich identisch (z. B. Unterhaltsmassnahmen an Haltung und
Schacht), erzeuge trotzdem **beide** Dateien. Ob sie wirklich gleich sind, prüfe ich.

Nicht nötig: **Deckel**, **Hauptdeckel** und **Sanierungsmassnahmen** — die sind bereits
erfasst. Wenn du sie mühelos mitnehmen kannst, nimm sie als Kontrolle mit; Vorrang haben
aber die zwanzig oben.

### Wohin

Speichere die Dateien in den Ordner:

```
D:\QGIS_V4.2\GeoShop\webgis-masken\
```

Sag mir am Schluss in einer kurzen Liste, welche Masken du erfasst hast, welche nicht,
und wo du unsicher warst.

## (Ende des Auftrags)

---

## Danach

Den Ordner `D:\QGIS_V4.2\GeoShop\webgis-masken\` in dieser Sitzung nennen. Aus den
Dateien entstehen dann die Katalogeinträge in `Objektakten.Katalog.json` — je Maske eine
Objektart mit Feldern, Auswahlkatalogen und dem Verweis auf das DSS-Ziel. Der Abgleich
mit dem Katastermodell (welches Feld in die XTF darf und welches nur Anzeige bleibt)
passiert hier im Projekt, nicht in der Desktop-Sitzung.
