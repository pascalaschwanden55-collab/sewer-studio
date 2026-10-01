# WinCan-Quellenwahl und Stopptor

Datum: 2026-08-21
Status: freigegeben zur Umsetzung

## Anlass

Der Kundenordner `G:\Sanierung_Andermatt_GKS` (5 WinCan-VX-Projekte) importierte null
Haltungen und meldete dabei **„erfolgreich"**. Ursache war nicht fehlendes Verstaendnis,
sondern eine falsch gewaehlte Datei: WinCan legt neben `<Projekt>.db3` (1,2 MB, mit
`SECTION`/`SECINSP`/`SECOBS`) eine `<Projekt>_Meta.db3` (6,8 MB, **ohne** diese Tabellen).
Der Importer waehlte „die groesste" und traf immer die falsche.

Entscheidend ist das Muster dahinter: **Dieselbe Suchregel lag zweimal im Code.**
`KanalExportDetector.FindWinCanDb3` schloss `_Meta` korrekt aus,
`WinCanDbImportService.FindDb3` nicht. Die Erkennung meldete die richtige Datei, der
Importer oeffnete danach eine andere.

Die Sofortreparatur vom 2026-08-21 ist bereits umgesetzt (Meta-Filter, alle Projekte je
Sammelordner, Medientyp aus der Dateiendung, Trennung gleichnamiger Haltungen ueber das
Schachtpaar). Dieser Entwurf beseitigt die **strukturelle** Ursache.

## Ziel

1. Erkennung und Import waehlen nachweislich **dieselbe** Datei — dauerhaft.
2. Gewaehlt wird nicht nach Bauchgefuehl, sondern nach **nachgeschautem Inhalt**.
3. Ein unstimmiges Ergebnis meldet sich **vor** der Uebernahme, nicht als „erfolgreich".

## Nicht-Ziele

- Keine KI, kein Ollama, kein Sidecar in diesem Weg (siehe „Hardware-Regel").
- Keine Umstellung von IBAK, XTF, PDF, SchachtPro.
- KINS ist bewusst abgetrennt (eigener Entwurf, siehe unten).
- Keine Aenderung an der fachlichen Zuordnung von Befunden, Medien oder Haltungen.

## Warum KINS nicht dazugehoert

`KinsGesamtprotokollFileLocator` raet **nicht** blind: Er filtert bereits auf Dateinamen
mit „Protokoll" und ohne „Deckblatt". Er ist ausserdem nicht die allgemeine KINS-
Quellenwahl, sondern wird nur vom `ProjectImportOrchestrator` fuer den PDF-Seiten-Split
des Ein-Knopf-Imports benutzt.

Die Frage „ist das wirklich ein Gesamtprotokoll?" ist dort fachlich schwer: sicher lesbar,
TV-Protokoll statt Plan/Deckblatt/Dichtheitspruefung, mindestens eine erkennbare Haltung,
und passend zu einer bereits eingelesenen KINS-Haltung. Das braucht einen eigenen Entwurf
und eigene Messung. Bei WinCan ist die Pruefung dagegen eindeutig und billig.

## Architektur

### Schichten

| Teil | Ort | Aufgabe |
|---|---|---|
| `Quellenwahl` | `Application/UseCases/Import/Quellen/` | reiner Ablauf: sammeln → pruefen → besten waehlen → protokollieren |
| `ImportPlausibilitaetsTor` | `Application/UseCases/Import/Quellen/` | reines Urteil aus Protokoll und Zahlen |
| `WinCanDb3Pruefer` | `Infrastructure/Import/WinCan/` | oeffnet die SQLite-Datei und liefert einen Befund |
| Anschluss | `KanalExportDetector`, `WinCanDbImportService` | benutzen beide denselben Pruefer |
| Tor-Aufruf | `ImportRunWorkflowController`, `ImportOneClickProjectController` | vor `Publish` |

`Application` enthaelt keinen Datei-, SQLite- oder PDF-Zugriff. Die Pruefung wird als
Delegate hereingereicht — dieselbe Trennung wie bei `PdfKiSchiedsrichter`.

### Der Befund: drei Ausgaenge, nicht zwei

```
Tauglich    Tabelle SECTION vorhanden und mindestens eine Haltung
Leer        Tabelle SECTION vorhanden, aber ohne Haltungen
Untauglich  nicht lesbar ODER keine Haltungstabelle (z. B. *_Meta.db3)
```

Die Trennung von `Leer` und `Untauglich` ist wesentlich: Ein lesbarer, aber noch leerer
Projektstand ist ein gueltiger Zustand und darf nicht wie ein Defekt behandelt werden.

### Auswahlregel

Kandidaten werden **alle** geprueft, dann sortiert nach:

1. `Tauglich` vor `Leer` vor `Untauglich`
2. innerhalb `Tauglich`: hoehere Haltungszahl zuerst
3. bei Gleichstand: Pfad alphabetisch (damit dasselbe zweimal dasselbe ergibt)

**Nicht** nach Dateigroesse. Genau diese Regel hat den Fehler verursacht.

### Ein Gewinner je Projektordner

Ein Sammelordner enthaelt mehrere vollstaendige WinCan-Projekte. Die Quellenwahl laeuft
deshalb **je Projektordner** (Ordner ueber dem `DB`-Verzeichnis), nicht einmal fuer den
ganzen Baum. Andernfalls gewinnt eine einzige Datenbank und die uebrigen Projekte fallen
still weg — der zweite Fehler vom 2026-08-21.

### Erkennung und Import teilen sich den Weg

`KanalExportDetector` und `WinCanDbImportService` rufen **beide** `Quellenwahl` mit
**demselben** `WinCanDb3Pruefer`. Es gibt keine zweite Kopie der Regel mehr. Ein Test
haelt fest, dass beide dieselbe Datei waehlen.

## Zahlen: nicht `Found` vergleichen

`ImportStats.Found` zaehlt auch Schaechte (`WinCanDbImportService.Records.cs`, `found++`
in der Knotenschleife). Gemessen: `Found = 44` bei 15 Haltungen und 26 Schaechten. Als
Pruefgroesse unbrauchbar.

`ImportStats` erhaelt deshalb **additive** Felder mit Standardwert (kein Bruch bestehender
Aufrufer):

```csharp
public int ErwarteteHaltungen { get; init; }      // Summe der Haltungen aus tauglichen Quellen
public int BearbeiteteHaltungen { get; init; }    // wirklich verarbeitete Haltungen, ohne Schaechte
public IReadOnlyList<QuellenVersuch> Quellenprotokoll { get; init; } = [];
```

## Das Stopptor

### Urteil

| Lage | Stufe |
|---|---|
| kein Quellenprotokoll (anderer Importweg) | Gruen |
| mindestens eine Quelle, aber **keine** `Tauglich` und **keine** `Leer` | **HartAbbruch** |
| mindestens eine `Tauglich`, aber `BearbeiteteHaltungen == 0` | **Rueckfrage** |
| `BearbeiteteHaltungen < ErwarteteHaltungen` | **Rueckfrage** |
| sonst | Gruen |

Alle Quellen `Leer` ergibt `ErwarteteHaltungen == 0` und damit Gruen — der legitime leere
Projektstand laeuft durch.

### Verhalten

- **HartAbbruch**: kein Uebersteuern. Der Import bricht ab.
- **Rueckfrage**: darf uebersteuert werden. Vorbelegung ist **Abbrechen**. Die
  Entscheidung wird im Importbericht festgehalten.

### Ort: vor `Publish`

Das Tor laeuft unmittelbar **vor** `fileTransaction.Publish()` — in **beiden** Wegen:

- `ImportRunWorkflowController` (heute Publish auf Zeile 218)
- `ImportOneClickProjectController` (eigener Publish-Weg)

Die bestehende `ValidatePlausibility` laeuft **nach** `Publish` und bleibt unveraendert;
sie ist als Stopptor ungeeignet, weil die Dateien dann bereits veroeffentlicht sind.

Der Anschluss erfolgt ueber ein zusaetzliches, optionales Feld in
`ImportRunWorkflowActions` (`ConfirmImplausible`), damit bestehende Aufrufer und Tests
unveraendert bleiben.

### Nicht zweimal fragen

Vorschau und anschliessender Echtlauf sind zwei Laeufe. Das Urteil traegt deshalb einen
**Fingerabdruck** ueber Protokoll und Zahlen. Wurde in der Vorschau zugestimmt, gilt die
Zustimmung im Echtlauf nur, wenn der neu berechnete Fingerabdruck identisch ist.
Weicht er ab, wird erneut gefragt.

### Ehrliche Abbruchmeldung

Nicht „nichts veraendert". Vor dem Tor koennen Wiederherstellungspunkt, Arbeitsdateien im
Staging und der Importbericht bereits entstanden sein. Der Text lautet:

> Keine Projektdaten und keine Importdateien uebernommen.

## Hardware-Regel: laeuft ohne grosse Grafikkarte

Quellenwahl, Tor **und die konkreten Pruefer** duerfen keinen Bezug auf KI, Ollama oder
Sidecar haben. Ein Waechtertest haelt das fest — ausdruecklich auch fuer
`WinCanDb3Pruefer` in `Infrastructure`, denn dort sitzt das Risiko, nicht im
Application-Baustein.

Praktisch: Ollama aus, kleine Grafikkarte oder GPU durch die Videoanalyse belegt — der
Import findet trotzdem alles.

## Tests

Zuerst rot, dann gruen.

1. Metadatei groesser und ohne Datentabellen → die Datendatei gewinnt (echter Andermatt-Fall)
2. Erkennung und Importer waehlen dieselbe Datei
3. Sammelordner: je Projektordner ein Gewinner
4. Alle Kandidaten untauglich → HartAbbruch, kein „Trotzdem"
5. Alle Kandidaten lesbar und leer → Gruen, kein Alarm
6. Tauglich vorhanden, aber null Haltungen bearbeitet → Rueckfrage
7. Weniger bearbeitet als erwartet → Rueckfrage
8. Fingerabdruck: gleiche Lage → keine zweite Frage; veraenderte Lage → erneute Frage
9. Tor liegt vor `Publish` (beide Wege) — Waechter auf Quelltextebene
10. Waechter: kein KI-/Ollama-/Sidecar-Bezug in Quellenwahl, Tor und Pruefer
11. Sortierung ist deterministisch (zweimal dieselbe Reihenfolge)

## Offen fuer spaeter

Das `Quellenprotokoll` ist im Kern bereits die „Quellenkarte", die ein spaeterer
KI-Notnagel als Eingabe bekaeme (nur wenn die feste Suche nichts findet, nur als
Vorschlag, von C# nachgeprueft — Muster `PdfKiSchiedsrichter`). Dieser Entwurf baut ihn
nicht, verbaut ihn aber auch nicht.

## Beim Bauen dazugelernt (2026-08-21, nach der Umsetzung ergaenzt)

Zwei Dinge standen so nicht im Entwurf und haben ihn veraendert. Beide sind gemessen,
nicht vermutet.

### 1. Erkennung und Auswahl brauchen verschiedene Strenge

Der erste Bauversuch liess die Formaterkennung dieselbe strenge Auswahl benutzen wie den
Import. Damit fielen 10 bestehende Tests um — und das war kein Testproblem: Eine
WinCan-Datenbank, die gerade im LightViewer geoeffnet und dadurch gesperrt ist, waere
plotzlich kein WinCan-Export mehr gewesen. Der Ordner haette als "unbekanntes Format"
gegolten.

Die beiden Stellen beantworten verschiedene Fragen:

| | Frage | Regel |
|---|---|---|
| Erkennung | Was fuer ein Export ist das? | eine vorhandene, nicht-Meta ".db3" genuegt, auch gesperrt |
| Auswahl | Welche Datei oeffne ich? | nur eine nachweislich lesbare mit Haltungen |

Geloest ueber `QuellenBefund.ErkanntAlsQuelle`: `NichtLesbar` (gesperrt, defekt) heisst
weiterhin "richtige Quellenart", `Untauglich` (Metadatenbank, keine SECTION-Tabelle)
heisst "gehoert nicht dazu". Die Erkennung nimmt `BesterErkannter`, der Import
`Gewinner`.

Beide koennen dadurch nicht auseinanderlaufen: Sobald es einen Gewinner gibt, nehmen
beide denselben. Sie unterscheiden sich nur, wenn ueberhaupt nichts lesbar ist — und
dann hat der Import ohnehin nichts zu waehlen und meldet das ehrlich.

### 2. Eine Haltung ohne Befunde ist trotzdem importiert

Der erste Zaehler stand hinter dem Protokollaufbau. Eine Haltung ohne eine einzige
Beobachtung — ein sauberes Rohr — verlaesst die Schleife aber vorher per `continue`.
Sie wurde damit nicht als bearbeitet gezaehlt.

Am echten Ordner fiel das sofort auf: 16 Quellhaltungen, nur 15 gezaehlt, Tor meldete
"1 fehlt". Ein Fehlalarm bei einem vollstaendig korrekten Import — und genau das haette
das Tor unglaubwuerdig gemacht.

Der Zaehler steht jetzt direkt nach dem Zusammenfuehren der Stammdaten: Ab dort ist die
Haltung im Projekt angekommen; ob sie Befunde hat, ist eine fachliche Eigenschaft und
kein Importfehler. Ein Test haelt das fest.

### 3. Verglichen werden Quellzeilen, nicht Datensaetze

`BearbeiteteHaltungen` zaehlt verarbeitete Quellzeilen, nicht Datensaetze im Projekt.
Am Andermatt-Ordner sind das 16 gegen 15 Datensaetze — eine Haltung lag in zwei Zonen
mit identischem Schachtpaar und wurde bewusst zusammengefuehrt.

Wuerde das Tor gegen die Datensatzzahl pruefen, schluege es bei jeder legitimen
Zusammenfuehrung an. Es vergleicht deshalb Gleiches mit Gleichem.

## Ergebnis am echten Ordner

```
Erkennung             : WinCan -> 2_26_046 ... Bodenstrasse.db3
Erwartete Haltungen   : 16
Bearbeitete Haltungen : 16
Found (mit Schaechten): 44   <- als Pruefgroesse untauglich
Stopptor              : Gruen
```

Das Quellenprotokoll nennt alle 10 geprueften Dateien mit Begruendung — 5 Datendateien
mit ihrer Haltungszahl, 5 Metadatenbanken als verworfen.

---

# Nachtrag 2: Die Haltungsnummer (2026-08-21, spaeter am Tag)

Pascal: *„Eine Haltungsnummer ist von Schacht oben-Schacht unten. Es funktioniert nicht
der Import. Massgebend sind die Protokolle mit den entsprechenden Videos, anschliessend
Dichtheitspruefungsprotokolle wenn vorhanden."*

Damit faellt eine Annahme, auf der der obige Entwurf noch stand.

## Befund

Das Protokoll fuehrt `Haltung: H6` — daneben aber `Schacht oben 955509 / Schacht unten
4789`. `H6` ist eine Laufnummer des Operateurs, keine Haltungsnummer.

**Der PDF-Import macht es bereits richtig.** Ueber die fuenf Kundenprotokolle gemessen
liefert er 15 Haltungen mit den Namen `955509-4789`, `7370-7427`, `06.8360-2835` usw.
Der WinCan-Weg ueberschrieb das mit `H6`.

Damit entfaellt auch der Notbehelf `H6 (Zone 2.11)` aus Nachtrag 1: Zwei verschiedene
Haltungen heissen unter der richtigen Regel `955509-4789` und `2413-327015` und
kollidieren nie.

## Zweiter Fehler: Schacht oben/unten waren bei 3 von 16 vertauscht

`ShouldReverseWinCanDirection` drehte oben und unten um, sobald die Kamera gegen die
Fliessrichtung fuhr. Ein Test hielt das ausdruecklich fest
(*„Schacht_oben = Anfangsschacht der Befahrung"*).

Aus **drei unabhaengigen Quellen** widerlegt:

1. `OBJ_FromNode_REF`/`ToNode_REF` stimmen in **16 von 16** Faellen mit
   `Schacht oben`/`Schacht unten` im Protokoll ueberein — auch bei allen drei
   Gegenbefahrungen.
2. Die WinCan-XTF fuehrt die Fahrtrichtung getrennt:
   `vonPunktBezeichnung 4789`, `bisPunktBezeichnung 955509`,
   `Inspektionsrichtung Gegen` — waehrend das Protokoll `oben 955509` sagt.
3. Der amtliche Kataster bestaetigt die Reihenfolge.

Die Datenbank speichert also **hydraulisch**; die Umkehrung machte daraus einen Fehler —
und unter der neuen Namensregel eine falsche Haltungsnummer. Die Fahrtrichtung bleibt
getrennt im Feld `Inspektionsrichtung` erhalten.

Aufgefallen war es nie, weil 13 von 16 Haltungen in Fliessrichtung befahren wurden.

## Pruefung gegen den amtlichen Kataster

`Abwasserkataster_Uri.xtf` (575 MB, 94'109 Haltungen) auf Pascals Hinweis ausgewertet:

- **15 von 15** unserer Haltungen gefunden
- alle tragen amtlich das Format `Schacht-Schacht`
- **13 von 15** exakt gleich der aus dem Protokoll gebildeten Nummer
- 2 weichen ab: `7.4790-4789` statt `955509-4789`, `7370-06.8360` statt `7370-7427`

Ursache der zwei: Der obere Schacht heisst heute `955509`, die amtliche Haltung traegt
aber noch `7.4790`. `7.4790` existiert im Kataster gar nicht mehr als Schacht — eine
Umnummerierung, bei der der Haltungsname nicht mitgezogen wurde.

## Entschieden und umgesetzt

**Namensbildung.** Beide Importwege bilden dieselbe Nummer aus dem Schachtpaar. Sie
treffen sich dadurch auf demselben Datensatz und ergaenzen sich, statt zwei getrennte
Bestaende zu erzeugen. Die WinCan-Bezeichnung bleibt im Importbericht nachvollziehbar
(`Haltung 955509-4789 (WinCan-Bezeichnung H6)`); die Medien heissen weiterhin danach.
Fehlt ein Schacht, bleibt die WinCan-Bezeichnung als Rueckfall — samt der bisherigen
Trennung gleichnamiger Haltungen.

**Katasterabgleich** (`Application/UseCases/Import/Kataster/`, Leser in
`Infrastructure/Import/Kataster/`). Grundlage bleibt immer das Protokoll. Liegt eine
amtliche SIA405-Datei im Projekt, wird abgeglichen und bei Abweichung die amtliche
Nummer uebernommen — sichtbar gemeldet. **Haltungen, die der Kataster nicht kennt,
behalten ausdruecklich ihre Protokollnummer.** Ohne Katasterdatei aendert sich nichts.

Konservativ: kein Eingriff ohne beide Schaechte, kein Eingriff an von Hand bearbeiteten
Namen, keine Umbenennung auf einen bereits vergebenen Namen (statt dessen Meldung), und
ein im Kataster mehrdeutiges Schachtpaar wird verworfen statt geraten.

Der Abgleich laeuft als Schritt 5c **vor** der Verteilung — die Zielordner werden nach
dem Haltungsnamen benannt.

## Gemessenes Ergebnis

```
15 Haltungen, alle als Schacht-oben-Schacht-unten benannt
13 davon deckungsgleich mit dem Kataster
 2 durch den Abgleich korrigiert  ->  15 von 15 amtlich
Kataster gelesen: 83'049 Haltungen aus 575 MB in 3,0 Sekunden
```

## Testvorlage korrigiert

Die alte Mini-Datenbank nannte die Haltung `06-001`, gab ihr aber die Schaechte `S-865`
und `S-864` — in sich widerspruechlich, so kann echte Kundendaten nie aussehen. Die
Schaechte heissen jetzt `06` und `001`; damit ist die Vorlage realistisch und die
uebrigen Tests bleiben aussagekraeftig.
