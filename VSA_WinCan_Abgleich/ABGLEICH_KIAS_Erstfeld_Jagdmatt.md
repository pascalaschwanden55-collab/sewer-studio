# Abgleich 2: KIAS/IBAK-Export „Erstfeld_Jagdmatt_38454_0426" ↔ SewerStudio-Manifest

**Datum:** 2026-07-10
**Quelle:** `D:\Videoprojekte\Erstfeld_Jagdmatt_38454_0426_Export` — KIAS-Viewer-Export (v25.2.2.186), Firebird-DB `Arizona.fdb`, IBAK-Befundliste `Film\Daten.txt`
**Umfang:** 74 Haltungen, 1046 Beobachtungen, 76 Videos (MPG je Haltung), 866 Fotos

## Ergebnis Code-Abgleich (Hauptcode-Ebene, aus Daten.txt)

27 real verwendete Hauptcodes; 23 direkt im Manifest ok. 4 Befunde:

| Code | Anzahl | Befund | Empfehlung |
|---|---:|---|---|
| `BDC` | 29 | IBAK exportiert Hauptcode; Manifest kennt nur `BDCA…BDCZZ` | Import-Mapping Hauptcode→Detailcode via Klartext („Abbruch der Inspektion, Hindernis" → BDC*) |
| `AED` | 13 | dito, Manifest nur `AEDXA…AEDXX` | Klartext-Mapping („Rohrmaterialwechsel: Normalbeton" → AEDX*) |
| `AEC` | 10 | dito, Manifest nur `AECXA…AECXH` | Klartext-Mapping („Rohrprofilwechsel: Kreisprofil" → AECX*) |
| `BDG` | 1 | dito, Manifest nur `BDGA/B/C` | „Keine Sicht, Kamera unter Wasser" → `BDGA` |
| `BAG` | 1 | im Manifest vorhanden, aber `isSelectable=false` | Beim Import auf `BAGA` mappen oder selektierbar machen |

Kernaussage: **Keine inhaltliche Lücke im Manifest.** IBAK/KIAS exportiert Klartext nur auf Hauptcode-Ebene; die Untertypen stehen im Beschreibungstext („Riss längs", „Rohrverbindung versetzt" → BABA*, BAJB). Ein Importer braucht daher ein Klartext→Char1/Char2-Mapping.

## Strategischer Fund: fertig gelabelter Trainingsdatensatz

`Film\Daten.txt` liefert je Beobachtung: **Video-Timestamp + Meterstand + Code + Klartext + Haltungsname** — direkt verknüpfbar mit den 76 Haltungsvideos und 866 Fotos (`Film\Foto\H_<Haltung>_<n>.jpg`).

Das ist Ground Truth für die SewerStudio-Pipeline:
- Frame-Extraktion am Timestamp → gelabelte Frames für YOLO-Feintuning / DINO-Prompt-Validierung
- Meterstand-Angaben → OSD-Parser-Validierung
- 1046 Befunde → QualityGate-/Dedup-Benchmark (erwartete Befunde je Haltung bekannt)

Empfehlung: kleiner `IbakDatenTxtParser` als eigener Service mit Interface + Test (Format ist trivial: `HH:MM:SS  <m> m  CODE  Text@!$ibak$!<Haltung>$L`). Erschließt alle Exporte dieses Typs als Trainings-/Testdaten.

## Einschränkungen
- `Arizona.fdb` (Firebird) enthält die vollständigen Detailcodes, ist aber ohne Firebird-Client nicht sauber lesbar (Strings-Extraktion liefert nur Rauschen). Nicht nötig, solange Daten.txt vorliegt.
- `Bin\Bin.7z` (456 MB) = KIAS-Viewer-Programmdateien; im Sandbox kein 7z-Entpacker verfügbar. Für den Code-Abgleich nicht erforderlich. Falls der KIAS-eigene VSA-Katalog daraus geprüft werden soll: mit 7-Zip nach z. B. `D:\Videoprojekte\...\Bin\Bin_entpackt\` entpacken und Pfad angeben.
- Schacht-Codes (D-Gruppe) kommen auch in diesem Export nicht vor (Schachtprotokolle-Ordner leer) — Schacht-Validierung weiterhin offen.
