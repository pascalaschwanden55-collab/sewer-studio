# Schachtprotokolle aus PDF einlesen

Stand: 19.09.2026. Gemessen an allen 264 SchachtPro-Protokollen aus
`Gep_Aufnahmen_Göschenen_2026`, rein lesend; die Kundenoriginale bleiben unverändert.

## Warum der Leser geprüft wird

SewerStudio holt den Text eines PDFs mit dem Hilfsprogramm `pdftotext`. Welches auf einem
Rechner gefunden wird, entschied bisher die Reihenfolge `tools\` → daneben → PATH → WinGet,
ohne jede Prüfung. Das ist der Unterschied zwischen einem vollständigen und einem stillen
halben Import:

| Leser | Tabellen vollständig | Anschlüsse gelesen |
|---|---|---|
| Poppler 25.07 | 219 von 224 | 704 von 704 |
| Xpdf 4.00 | 164 von 224 | 627 von 704 |
| Eingebauter Leser (PdfPig) | 219 von 224 | 704 von 704 |

Schlimmer als die fehlenden Anschlüsse sind die falschen Werte: Xpdf verschiebt die
Tabellenspalten zeilenweise. In Schacht 10039 bekommt Einlauf 1 dadurch Tiefe und
Durchmesser von Einlauf 2. Im Programm sieht das völlig plausibel aus.

Deshalb prüft SewerStudio jetzt vor dem ersten Lesevorgang, welches Programm es gefunden
hat (`pdftotext -v`), und verwendet nur eine geprüfte Fassung: Poppler ab Version 21.
Xpdf und unbekannte Programme werden nicht benutzt. Ersatz ist der eingebaute Leser, der
in derselben Messung gleich gut liest und mit dem Programm mitgeliefert wird.

Greift der Ersatz, steht der Grund im Importhinweis des Protokolls, zum Beispiel:
*Gelesen mit dem eingebauten Leser. Das gefundene pdftotext stammt von Xpdf (Version 4).*

Die Prüfung läuft einmal je Programmpfad, nicht einmal je PDF.

## Was aus einem SchachtPro-Protokoll übernommen wird

- **Anschlüsse** mit Kennung, Uhrzeit, Tiefe, Durchmesser, Typ, Material und Zustand.
- **Koordinaten** in LV95 aus der Kopfzeile, wenn das Protokoll sie nennt: 212 von 212 im
  gemessenen Bestand. Übernommen wird nur ein vollständiges Paar innerhalb der Schweizer
  Ausdehnung. Ein halber, vertauschter oder unplausibler Wert ergibt nichts.
- **Stammdaten** wie bisher: Tiefe, Durchmesser, Form, Medium, Deckelmaterial,
  Deckeldurchmesser, Tauchbogen.

Vorhandene Handkorrekturen und Katasterwerte werden nie überschrieben. Das gilt auch beim
Neueinlesen eines Protokolls.

## Grenzen

- **Ein gekürzter Zustand bleibt gekürzt.** Passt die Zustandszelle nicht in die Spalte,
  schneidet SchachtPro den Text im PDF ab, entweder mit «…» oder mit «+3» für drei weitere
  Befunde. Die weggelassenen Befunde stehen nirgends im Dokument. SewerStudio merkt sich
  das und schreibt in der Grafik «… (im Protokoll gekürzt)». Im gemessenen Bestand trifft
  das 24 von 705 Anschlüssen. Vollständig kommen die Zustände nur über den QR-Code oder
  das `.spro`-Archiv.
- **Das Uri-Kästchenformular nennt weder Koordinaten noch Anschlusszustände.** Dort bleibt
  beides leer; nichts wird ergänzt.
- Ein Protokoll ohne Textebene (reiner Scan) läuft weiterhin über die Texterkennung. Im
  gemessenen Bestand war das eine Datei von 264.
- Der eingebaute Leser ist für Schachtprotokolle gemessen. Für Haltungsprotokolle und
  Dichtheitsberichte ist er nicht gegengemessen; dort greift er nur, wenn gar kein
  geeignetes `pdftotext` vorhanden wäre.

## Nachweise

- `PdfLeserEignungTests` — die Versionsregel, mit den echten Ausgaben beider Programme.
- `PdfLeserWahlTests` — ein vorgetäuschtes `pdftotext` hält jeden Aufruf in einer
  Markierungsdatei fest. Der Test zeigt, dass ein ungeeignetes Programm nicht einmal
  aufgerufen wird.
- `SchachtProtocolVollstaendigkeitTests` — Anschlusszahl, Tiefen, Durchmesser, Material,
  Zustände, gekürzte Zustände, Koordinaten und der Feldschutz, gegen den echten Seitentext
  der Protokolle 10039 und 10091.
