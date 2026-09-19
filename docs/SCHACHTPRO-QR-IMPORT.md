# SchachtPro-QR importieren

Stand: 19.09.2026.

## Bedienung

1. In SchachtPro die PDF-Seite «Datenimport für Sewer Studio» anzeigen.
2. Einen gut lesbaren Screenshot als PNG oder JPG speichern; der ganze QR-Code muss sichtbar sein.
3. In SewerStudio das Zielprojekt oeffnen und zur Seite **Import** wechseln.
4. Unter **Weitere Quellen** den Punkt **SchachtPro-QR aus Bild (PNG/JPG)** waehlen.
5. Ein oder mehrere Bilder auswaehlen. Bei aktivierter Vorschau erst pruefen, dann uebernehmen.

Jedes Bild soll genau einen SchachtPro-QR-Code enthalten. Die Zuordnung erfolgt nach
Schachtnummer im Zielprojekt. Der Projektname im Code erzeugt kein separates Projekt.
Bilder mit Fehlern werden gemeldet; weitere ausgewaehlte Bilder werden trotzdem gelesen.
Wenn kein einziges Bild lesbar ist, wird nichts uebernommen.

## Uebernommene Daten

Der Import nutzt dieselbe Feldzuordnung wie der `.spro`-Import: Stammdaten,
Schachtaufbau, LV95-Koordinaten, Anschluesse und Bauteilzustaende.
Handgeänderte Felder und manuell bearbeitete Protokolle bleiben geschuetzt.
Fast leere Anschlusszeilen mit bloss Nummer und Standardform werden ausgelassen.

Der vollstaendige QR-Datensatz bleibt als Quellbeleg in den Projektmetadaten erhalten,
auch Angaben ohne eigenes sichtbares Zielfeld (beispielsweise Herstellerdaten,
Grafikrotation oder Anschluss-Einragung). Ein neuer Import derselben Quellkennung
aktualisiert diesen Quellbeleg. Kundenbilder werden nur gelesen; die vorhandene
Importablage speichert eine Kopie im Projekt. Zusaetzlich wird das Originalbild wie
bei der Schachtverteilung abgelegt, beispielsweise:
`Schächte_Verteilt/1236654/20260919_1236654_QR.png`.
Die Ablage verwendet `ProjectStructure.SchachtVerteiltDir`, `ImportDateStampResolver`
und dieselbe geschuetzte Datei-Staging-Sitzung wie die anderen Importe.
Gleiche Bilder werden wiederverwendet, abweichende Inhalte erhalten einen freien Namen.
Ein vorhandenes Protokoll-PDF bleibt verknuepft; ein QR-Bild wird nicht als PDF ausgegeben.
Die relative Bildzuordnung liegt unter `SchachtPro.QR.Bild.<sourceKey>` in den Projektmetadaten.
Ohne gespeichertes Zielprojekt meldet der Import die noch fehlende Bildablage.

## Grenzen

- PNG/JPG-Dateien; noch kein direktes QR-Lesen aus PDF oder Kamera.
- Fotos und Logos sind im SchachtPro-QR grundsaetzlich nicht enthalten.
- CRC32 und Zlib-Pruefung erkennen Beschaedigungen, bestaetigen keine Urheberschaft.
- Hoechstens 32 MiB und 24 Millionen Bildpunkte je Bild, 64 KiB entpackte QR-Daten.
- Die bestehenden SchachtPro-Regeln fuer Anschluesse gelten; die Anschlussliste
  besitzt keinen eigenen Schutz einzelner Handkorrekturen wie die Stammdatenfelder.
- Quelle wird im bisherigen Datenmodell als SchachtPro (`Spro`) gefuehrt.

## Pruefung

Der unveraenderte Screenshot `Screenshot_20260919_195748.png` wurde mit dem neuen
.NET-Import in ein frisches Projekt im Arbeitsspeicher gelesen: Schacht 1236654,
Tiefe 2,68 m, zwei Anschluesse, zehn Zustandsbefunde. Die Dateipruefsumme blieb gleich.
Der Screenshot ist keine Testdatei im Projektordner; automatische Tests verwenden
kuenstliche Daten. Tests decken fehlerhafte Pruefsummen, Formate, doppelte Felder,
Groessenlimits, unvollstaendige Kompression, Abbruch, Wiederimport, Handwertschutz,
Bilddecodierung, Vorschauabbruch, Stapelfehler und Dienstregistrierung ab.

Decoder: [ZXing.Net 0.16.11](https://www.nuget.org/packages/ZXing.Net/0.16.11),
 vom Nutzer am 19.09.2026 freigegeben. Er arbeitet lokal.

### Abschliessende Ergebnisse

- Vollstaendiger Release-Build: erfolgreich, keine Fehler; eine bestehende Warnung
  in `VsaFotoAblageTests.cs:81`.
- Infrastructure: 6848 bestanden, 6 bestehende Ausnahmen uebersprungen.
- Pipeline: 2744 bestanden, 3 uebersprungen.
- ProjectModernizer: 62 bestanden.
- UI-Gesamtlauf: 7345 bestanden, 30 uebersprungen. Ein Design-Test beanstandete
  das fehlende Symbol am neuen Menuepunkt. Symbol ergaenzt, erneut gebaut;
  Nachpruefung mit Design-, QR-, Importablauf-, Registrierungs- und echten
  Oberflaechen-Bindungstests: 27 bestanden, 1 bestehender Kindtest-Einstieg uebersprungen.
  Der gesamte UI-Lauf wurde danach nicht wiederholt.
- Endgueltiger Screenshot-Import erneut erfolgreich; Kundenoriginal unveraendert.
- Architektur-Skill validiert und `git diff --check` ohne Befund.

Nachweise: `C:/Users/Besitzer/Documents/SewerStudio-SchachtPro-QR-2026-09-19/Pruefnachweise`.
Die TRX-Dateien erhalten auch den urspruenglichen Designfehler und die gruene Nachpruefung.

### Nachtrag: gemeinsame Schachtablage

Die Verteilanbindung wurde mit 263 Import-/Verteil-/Stagingtests und 88 Tests fuer
Importablauf, Vorschau und Speichern geprueft: alle bestanden. Der Release-Build
ueber `AuswertungPro.Dev.slnf` ist erfolgreich.

Der echte Screenshot wurde erneut in einem frischen Testprojekt verteilt:
`Schächte_Verteilt/1236654/20260919_1236654_QR.png`.
Die Kopie ist bytegleich zum Original; die Quelle blieb unveraendert.
Die neuen Ablagetests pruefen auch erneuten Import ohne Dateidublette,
Namenskollision ohne Ueberschreiben, Ruecknahme einer veroeffentlichten Kopie,
blockierten Zielordner ohne neuen Schachtdatensatz und den Erhalt vorhandener PDF-Links.

Abschliessender vollstaendiger Infrastructure-Testlauf nach der Verteilanbindung:
6853 bestanden, 0 Fehler, 6 bestehende Ausnahmen uebersprungen.
