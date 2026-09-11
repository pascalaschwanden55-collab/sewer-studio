# Objektakten – Umsetzung und Prüfung

Stand: 11.09.2026. Auftrag: WebGIS-Plan umsetzen, Nova-Stil beibehalten.

Alle bestätigten Felder und Auswahlwerte sind in der WPF-Anwendung eingebaut.
Haltungen und Schächte öffnen die Akte über **Objektakte**. Deckel und Sanierungen
haben eigene Datensätze. Nova-Stil und bisherige Exportspalten bleiben erhalten.

## Nachweise

- Unabhängiger Vergleich mit dem Planpaket und 17 Quellkopien: 200 Definitionen,
  204 Anzeigen, 91 Dropdownstellen, 78 Kataloge, 1’158 unveränderte Einträge.
  Wiederholbar mit `python tools/PruefeObjektaktenKatalog.py <Planordner>`.
- Echter Schacht 79969 aus der 456’586’997 Byte grossen XTF: erster Messlauf 7,29 Sekunden,
  Prozessspitze etwa 130 MiB. Wiederholung der Übernahme: null neue Positionen.
  Weiterer Lauf: 6,64 Sekunden; Haltung 79929–79969 zusätzlich 6,35 Sekunden.
- Deckel 450.940, Schachtsohle 448.340, Einlauf 448.360 und Auslauf 448.340 bleiben getrennt.
  Hauptdeckelwahl wird nicht aus der Anzahl Deckel erfunden. Nach ausdrücklicher Wahl
  beträgt die berechnete Tiefe 2.60 m. Linerbestand und tatsächliches Ereignis sind getrennt.
- SHA-256 der unveränderten Original-XTF:
  `ed6bfdc290f66e8bdefd5fcc6cff59013337adcd763df754d1eadec41da34830`.
- WPF im eigenen Prozess geprüft: breite/schmale Darstellung, verborgene Werte,
  Suche über Unterobjekte, Elternwechsel, Fremdcode und letzte Eingabe beim Schliessen.
  Testeinstellungen liegen separat. Bilder: [breit](nachweise/objektakte-breit.png),
  [schmal](nachweise/objektakte-schmal.png).

## Builds und Tests

Dev-Release und vollständiger Release sind erfolgreich, ohne Warnungen oder Fehler.
Der vollständige Build verwendet einen getrennten Ausgabeordner, weil der laufende
MCP-Hilfsdienst seine normale DLL sperrt. Er wurde nicht beendet.

```powershell
dotnet build AuswertungPro.Dev.slnf -c Release --no-restore
dotnet build AuswertungPro.sln -c Release --no-restore -m:1 -p:OutDir=C:/Sewer-Studio_KI_4.5/.tmp/objektakten-release/
```

| Sammlung | Ergebnis |
|---|---|
| Infrastruktur | 6’455 bestanden, 6 übersprungen; die 3 unter der Sandbox fehlgeschlagenen Fälle bestanden anschliessend ausserhalb der Sandbox |
| Pipeline | 2’658 bestanden, 3 übersprungen |
| Oberfläche | 6’953 bestanden, 22 übersprungen |
| ProjectModernizer | 62 bestanden |
| Abschliessende UI-/Architekturprüfung nach Suchergänzung | 25 bestanden, 2 direkte Kindprozess-Einstiege übersprungen; deren Szenarien liefen erfolgreich über die Elterntests |
| Abschliessende Import-/Speicherprüfung einschließlich Firmenbeziehungen und SIA405-Basismodellen | 35 bestanden |

Die drei nachgeprüften Infrastruktur-Fälle betreffen bestehende OCR-Importtests und
einen vom Test selbst gestarteten Prozess. Zusammen bestanden sie in 3 Minuten 25 Sekunden.
Kundenquellen wurden nur gelesen. Ein vorläufiger Testlauf im gemeinsamen Ausgabeordner
zählte zusätzlich das mitkopierte UI-Logo in PDF-Tests. Diese Fototests bestanden in den
regulären getrennten Testordnern; dafür wurde kein Programmcode geändert.

Live-/GPU-Prüfungen mit zusätzlichen Voraussetzungen wurden durch die vorhandenen Tests
übersprungen. CLAUDE.md und der Architektur-Skill sind aktualisiert;
`quick_validate.py` meldet `Skill is valid!`. Die Protokolle stehen unter `nachweise/`.

## Abnahme A01–A26

| Kriterium | Stand |
|---|---|
| A01 Vollständigkeit | 200 Definitionen/204 Anzeigen unabhängig verglichen |
| A02 Dropdowns | 91 Stellen, alle 78 Listen unverändert; gleiche Texte behalten getrennte Einträge |
| A03 Weitere Kataloge | Extern offen: nicht gelesene Material-/Hersteller-Unterlisten |
| A04 Feldordnung | Bestehende Fields/FieldMeta bleiben Speicherort; Zusatzangaben getrennt; Umlautalias geprüft |
| A05 Sichtbarkeit | Persönliche Ansicht verändert keine Fachdaten oder Exportbewertung |
| A06 Suche | Versteckte Werte und Unterobjekte gefunden; Treffer öffnet Objekt und Gruppe |
| A07 Bedienung | Zwei/eine Spalte und letzte Eingabe beim Schliessen geprüft |
| A08 Altprojekte | Feldnamen/freie Spalten erhalten; Format 2 bleibt ohne Akten bestehen |
| A09 Fremdcodes | Anzeige und Dateirunde erhalten unbekannten Originalcode |
| A10 Abhängigkeiten | Elternwechsel löscht keinen Detailwert und trifft keine Ersatzwahl |
| A11 Ereignisse | Zwei Ereignisse getrennt; Hersteller/Firma/Operateur/Gewerk verschieden; externe Produktfilter offen |
| A12 Historie | Bemerkung allein und gewöhnliche Reinigung erzeugen keine Sanierung |
| A13 Planung | Sanieren Nein entfernt weder Liner noch Historie |
| A14 Deckel/Anschlüsse | Eigene Deckel-GUIDs; Punkte mit Originalkennung und echten Ein-/Auslaufbeziehungen; offene Detailmasken gekennzeichnet |
| A15 Höhen | 450.940/448.340/448.360 am Original geprüft; Tiefe separat abgeleitet |
| A16 Quellschutz | Handwerte einschliesslich Leerwert geschützt; Rohwerte/Referenzen erhalten |
| A17 Wiederholung | Keine Duplikate; geänderter Projektstand sperrt eine alte Vorschau |
| A18 Speichern | Dateirunde mit Unterobjekten, Fremdcodes und Herkunft bestanden; Akten Bestandteil der normalen Projektkopie/Sicherung |
| A19 XTF | Erweitert: vollständiger DSS-Neuexport mit Normprüfung, siehe [DSS-Prüfbericht](DSS-EXPORT.md); JSON erhält verbleibende Zusatzangaben |
| A20 Zusatzdatei | Export/Wiedereinlesen in dasselbe Projekt geprüft; defekte Beziehungen vor Schreibzugriff abgewiesen |
| A21 Bericht | Vorschau nennt Zusatzbedarf; JSON enthält Bericht; fehlgeschlagene Zusatzdatei sichtbar |
| A22 Gemeinsame Referenz | Bearbeitung bleibt am Projektdatensatz; Originalprofile werden nicht geändert |
| A23 Fehler | Abbruch/DTD/defekte und doppelte Kennungen, falsches Projekt, kaputtes Paket und veraltete Vorschau geprüft |
| A24 Leistung | 456-MB-Datei ohne vollständigen XML-DOM gelesen und gemessen; Formulare nur für gewähltes Objekt. Kein Lastnachweis für jede Projektgrösse |
| A25 Release | Builds/Tests wie oben; Architektur aktualisiert. Kein Commit erstellt |
| A26 WebGIS | Extern offen: Testsystem und GEONIS-Vertrag erforderlich; WebGIS unverändert |

## Extern noch offen

Extern offene Punkte des Planpakets gelten weiter, soweit sie im ergänzenden
[DSS-Prüfbericht](DSS-EXPORT.md) nicht bereits geklärt sind. Die lokale Erfassung bestätigter Felder ist umgesetzt;
ungeklärte Regeln sperren die betreffende automatische Zuordnung. Es fehlen insbesondere
vollständige abhängige Kataloge, bestimmte Unterobjektmasken, ein echtes Schachtsanierungsbeispiel,
andere Bauwerksmasken und der GEONIS-Rückimportvertrag. Die JSON-Zusatzdatei ist deshalb
noch kein bestätigtes WebGIS-Importformat.

Anleitung: [Objektakten](../../OBJEKTAKTEN.md).

## Nachtrag zur direkten Listenansicht

Die vollständige Objektakte ist jetzt direkt in der aufgeklappten Zeile erreichbar.
Bedienung, Bilder und aktuelle Prüfungen: [Inline-Ansicht](INLINE-ANSICHT.md).

## Nachtrag zur WebGIS-Anordnung

Die direkte Ansicht folgt jetzt dem WebGIS-Aufbau im Nova-Stil.
[Begriffsabgleich, Bilder und Prüfungen](WEBGIS-ANORDNUNG.md).
