# OSD-Kettenmessung vom 17.08.2026

## Ergebnis

Der trainierte Zeichenleser lohnt sich als Rueckfall. Er ersetzt den bisherigen
Leser nicht. Die Reihenfolge lautet:

```text
Vorlagenleser -> Tesseract-Vierziffern -> Tesseract-Zwei-Dezimal -> OSD-Modell
```

Das Modell wird nur aufgerufen, wenn alle bisherigen Wege keinen Meterstand
liefern.

| Satz | Leser allein | Kette | Beitrag des Modells |
|---|---:|---:|---:|
| `osd_sd_v1` | 88 richtig / 0 falsch | 90 / 0 | +2 / 0 |
| `osd_hd_v1` | 15 richtig / 0 falsch | 23 / 0 | +8 / 0 |
| `osd_hd2_v1` | 43 richtig / 0 falsch | 58 / 0 | +15 / 0 |
| `osd_mix_v1` | 48 richtig / 1 falsch | 53 / 1 | +5 / 0 |
| **gesamt** | **194 / 1** | **224 / 1** | **+30 / 0** |

Der Kandidat liest damit genau einen Teil der Luecken des Vorlagenlesers. Auf
HD und HD2 ist sein Zusatznutzen am groessten.

## Laufzeit und Grafikspeicher

Gemessen wurden 317 Bilder. Vor der Kettenmessung wurde der echte
Bogen-Copilot-Kandidat `bcc_nc15_seed46_20260808` geladen. Beide Modelle lagen
danach gleichzeitig im Speicher.

| Messwert | Ergebnis |
|---|---:|
| Modellaufrufe | 122 von 317 Bildern |
| erster Modellaufruf inklusive Laden | 295 ms |
| warme Modelllesung, Mittel | 61 ms |
| warme Modelllesung, Median | 35 ms |
| warme Modelllesung, p95 | 115 ms |
| mittlere Leserzeit vorher | 282 ms je Bild |
| mittlere Kettenzeit | 306 ms je Bild |
| mittlerer Zusatz ueber alle Bilder | rund 24 ms je Bild |
| zusaetzlich belegter VRAM bei geladenem BCC-Modell | rund 9 MB |

Die VRAM-Frage ist damit unkritisch. Der Zeitaufschlag entsteht nur bei den
Luecken und liegt im gemessenen Gesamtlauf bei etwa 8,5 Prozent.

## Sicherheitsgrenzen

- Kandidaten-ID: `osd_zeichen_c668e35d59cb`
- Gewicht-SHA-256: `c668e35d59cb4feba82b60b857663a11ac6f493104d03bf1b0414103a4a75845`
- Schwelle: `0,25`
- erlaubter Status: `diagnostic_not_deployed`
- produktiver Schalter: standardmaessig aus
- eigener GPU-Platz: `YOLO_OSD`

Der Sidecar prueft ID, Status, Dateiname, Gewichtshash, Schwelle und Zeichenkarte.
Das Modell wird aus einer privaten, erneut geprueften Momentaufnahme geladen.

## Bewusste Grenze

Diese vier Saetze wurden fuer die Entscheidung ueber die Kette verwendet. Sie
sind deshalb keine unberuehrte Produktabnahme mehr. Vor dem Einschalten wird ein
frischer, zuvor nicht verwendeter Bestand benoetigt.

Die Kette loest ausserdem keine fehlende Zeichenfindung. Bei 22 der 70 zuvor
nicht gelesenen Sollbilder findet der erste Schritt gar keine Zeichen. Das ist
eine eigene naechste Baustelle.

Der vollstaendige JSON-Beleg liegt unter
`C:\KI_BRAIN\training\reports\osd_kettenmessung_osd_zeichen_c668e35d59cb_20260817_143348.json`.
Sein SHA-256 lautet
`ef25b19df5ae1a169ea91da5b3e14e931b5c196084c596aa05732c810dcd1093`.
