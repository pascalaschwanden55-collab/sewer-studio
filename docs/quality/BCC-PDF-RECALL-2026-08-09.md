# Bogen-Copilot gegen die PDF-Protokolle — 2026-08-09

Erste Messung des Bogen-Copiloten auf breitem, unberuehrtem Bestand. Grundlage
sind die vom Operateur protokollierten Boegen aus den Kunden-PDFs.

## 1. Ergebnis

Kandidat `bcc_nc15_seed46_20260808`, `conf 0,40`, stark ab 0,70, 1 Bild/Sekunde,
Zuordnung ueber den Videozaehlerstand mit Toleranz ±15 s.

| | Boegen | getroffen | Recall | 95-%-Bereich | Vorschlaege je Haltung |
|---|---:|---:|---:|---:|---:|
| gesamt | 85 | 66 | **77,6 %** | 68–85 % | 4,0 |
| SD | 69 | 52 | 75,4 % | 64–84 % | 3,5 |
| HD | 16 | 14 | 87,5 % | 64–97 % | 5,6 |

Der HD-Wert steht auf 16 Boegen. Sein Fehlerbereich reicht von 64 bis 97 % — er
zeigt keinen belegten Vorsprung gegenueber SD, sondern nur, dass zu wenige
HD-Boegen im Bestand sind.

Das PDF allein liefert weiterhin keine Precision. Danach wurden jedoch alle 154
Vorschlaege der Messhaelfte blind als kurze Clips geprueft: 91 zeigen einen Bogen,
60 keinen Bogen und 3 sind unsicher. Ohne die unsicheren Faelle ergibt das
**60,3 % Precision**. Werden alle drei unsicheren Faelle einmal als falsch und
einmal als richtig gerechnet, liegt die harte Grenze bei **59,1 bis 61,0 %**.
Der Wilson-Bereich fuer die Uebertragung auf aehnliches Archivmaterial betraegt
52,3 bis 67,7 %.

Diese Zahl bewertet Vorschlaege, nicht eindeutige Bogenereignisse: Zwei getrennte
Vorschlaege am selben realen Bogen koennen beide als sichtbar richtig gelten.

## 2. Wie der Bestand entstanden ist

Aus 1476 gescannten Haltungen tragen 470 einen protokollierten Bogen (1158
Boegen). Davon wurden 92 gesperrt, weil sie als Trainings-, Negativ- oder
Eval-Material bekannt sind — beide Fahrtrichtungen jeder Haltung.

Aus dem Rest wurden 61 SD- und 25 HD-Haltungen geschichtet nach Bogenzahl
gezogen, deterministisch ueber einen Streuwert des Haltungsnamens. Bewusst
**nicht** nach Untercode ausgeglichen: Der Detektor kennt genau eine Klasse, der
Untercode beschreibt die Bogenform. Ein Ausgleich haette die Zahl auf einen Mix
gerechnet, den es in den Kanaelen nicht gibt. Die Abdeckung liegt gleichmaessig
bei 13–22 % je Untercode, alle acht sind vertreten.

Der Bestand wurde vor dem Lauf halbiert: 44 Haltungen zum Suchen der Schwelle,
42 eingefroren zum Messen. Die Auswahlregel stand vor der Kurve fest — die
niedrigste Schwelle mit hoechstens 5 Vorschlaegen je Haltung, bei Gleichstand die
hoehere. Das ergab `conf 0,40`.

Der Kalibrierteil ergab dort 70 %, der eingefrorene 77,6 %.

Das ist **kein Beweis gegen Ueberanpassung**. Bei 42 gegen 39 Haltungen liegt
der Unterschied gut innerhalb der Streuung; eine besser abschneidende
Pruefmenge kann auch schlicht die leichtere sein. Belegt ist nur das Fehlen
eines sichtbaren Anzeichens: Waere die Schwelle stark auf den Kalibrierteil
gezogen, waere der Abfall zur Pruefmenge zu erwarten gewesen, und er ist
ausgeblieben.

## 3. Der Arbeitspunkt gehoert auch zum Material

Der gepinnte Arbeitspunkt `conf 0,50` stammt aus Messungen auf
`D:\Videoprojekte`. Auf dem Archivbestand `D:\Haltungen` kostet er Treffer:

| Schwelle | Recall (Kalibrierteil) | Vorschlaege je Haltung |
|---:|---:|---:|
| 0,25 | 71 % | 5,8 |
| 0,40 | 70 % | 4,5 |
| 0,50 (gepinnt) | 64 % | 3,9 |

Bekannt war: Der Arbeitspunkt gehoert zum Gewicht. Neu ist: Er gehoert auch zum
Materialbestand. Ein Wert, der auf einem Videokorpus gemessen wurde, traegt auf
einem anderen nicht unveraendert.

## 4. Der OSD-Meterleser traegt auf dem Archiv nicht

Der geplante Vergleich ueber den Meterstand war nicht durchfuehrbar. Gemessen an
je 20 verteilten Bildern:

| | Haltungen | Abdeckung | ≥ 70 % | liest gar nichts |
|---|---:|---:|---:|---:|
| SD | 60 | 11 % | 0 | 18 |
| HD | 23 | 5 % | 0 | 13 |

Die bisher belegten 76–95 % stammen saemtlich aus `D:\Videoprojekte` — sieben
Videos, vier davon aus einem einzigen Projekt. Dieselbe Methode reproduziert
diese Werte dort (75–100 %); auf dem Archiv liest der Leser praktisch nichts.

Er liest dabei nicht falsch, sondern gar nicht. Auf Haltung `88218-88316` steht
bei Sekunde 64 sichtbar `1,54 m` im Bild — der Leser gibt `m.94.` zurueck. Bei
Sekunde 632 steht `22,20 m` — er gibt `ZZ.Z0.` zurueck und verwechselt `2` mit
`Z`. Ein Vorlagenproblem an gut lesbaren Bildern.

**Folge fuer das Programm:** Der Copilot kann auf Archivvideos keinen Meterstand
zu seinen Vorschlaegen anzeigen. Die Vorschlaege selbst bleiben ueber die Zeit
brauchbar.

## 5. Die Zeitachse ist geprueft

Dieselben zwei Bilder belegen den Gegenbeweis: Das Protokoll nennt fuer Sekunde
64 den Meterstand 1.54 und fuer Sekunde 632 den Wert 22.20 — genau das, was im
Bild steht. Der Nullpunkt des Protokollzaehlers stimmt mit der Videodatei
ueberein. Die Zuordnung ueber die Zeit ruht damit auf einer geprueften Grundlage.

Daraus folgt ein Wahrheitsbestand fuer den Leser, den niemand ablesen muss: Zu
jedem der 1158 protokollierten Boegen stehen Meterstand und Videozaehlerstand im
PDF. Das Bild an diesem Zaehlerstand traegt den Sollwert im Bild.
`training/scripts/osd_wahrheit_aus_protokoll.py` erzeugt diesen Bestand; eine
Abweichung ist erst nach menschlicher Sicht ein Leserfehler und nicht vorher.

Die blinde Sichtprobe ist inzwischen abgeschlossen: 25/30 Werte stimmen auf
1 cm genau, 29/30 innerhalb 10 cm. Die vier kleinen Differenzen passen zur
Kamerabewegung zwischen Protokollmoment und Bild; der grobe Fall ist ein falsches
PDF-Label. Die Sichtprobe misst damit die PDF-/Video-Zuordnung und nicht den
Leser. Die 897 Werte bleiben schwache Labels mit Zeit- und Zuordnungsrauschen.

## 6. Was nicht gemessen wurde

- **Eindeutige Bogenereignisse je Vorschlag.** Die Sichtpruefung beantwortet, ob
  im Clip ein Bogen sichtbar ist. Sie prueft nicht, ob zwei Vorschlaege denselben
  realen Bogen doppelt melden.
- **Lokalisation.** Die Toleranz ±15 s sagt nichts ueber die Genauigkeit der
  Stelle.
- **Fuenf Haltungen** blieben unausgewertet: zwei mit unsicherer Videozuordnung
  im PDF-Scan, drei mit mehreren Videopfaden im selben Protokoll. Sie zaehlen
  nicht als Fehlschlag.

## 7. Einordnung

77,6 % der protokollierten Boegen werden gefunden, bei 4 Vorschlaegen je
Haltung. Von diesen Vorschlaegen zeigen rund 60 % wirklich einen Bogen. Fuer ein
Hilfetool mit Pflichtbestaetigung ist das ein brauchbarer Anfang: Im gemessenen
Bestand bestaetigt der Mensch etwa drei von fuenf Vorschlaegen und verwirft zwei.

Es ist keine Modellfreigabe. Der Kandidat bleibt `not_deployed`. Der eingefrorene
Messteil ist mit dieser Auswertung verbraucht — eine zweite Schwelle darf nicht
daran geprueft werden.

## 8. Der gemessene Arbeitspunkt

Gewicht, Schwelle und Materialbestand bilden zusammen den Arbeitspunkt. Keines
der drei gilt ohne die anderen beiden:

| Teil | Wert |
|---|---|
| Gewicht | `bcc_nc15_seed46_20260808`, SHA-256 `8ad82c1b0186ec…` |
| Schwelle | `conf 0,40`, stark ab 0,70 |
| Bestand | Archiv `D:\Haltungen`, gebunden ueber `messbestand_v1.json` SHA-256 `45f83df5bb8a…` |

Festgehalten in `workpoint_archiv.json` neben dem Kandidaten. Der bestehende
`workpoint.json` mit `conf 0,50` bleibt unveraendert — er gilt fuer
`D:\Videoprojekte`. Zwei Dateien nebeneinander sind Absicht: Ein einziger Wert
wuerde verschweigen, dass die Zahl vom Bestand abhaengt.

## 9. Naechste Schritte

1. **Die 85 Boegen nur noch als festen Vergleichsbestand fuehren.** Ein spaeterer
   Kandidat darf dort gegen 66/85 antreten, um Fortschritt zu zeigen — nicht als
   unabhaengige Abnahme.
2. **Erledigt:** Alle 154 Vorschlaege wurden blind geprueft. Ergebnis: 91 Bogen,
   60 kein Bogen, 3 unsicher; Precision 60,3 % ohne unsichere Faelle.
3. **OSD-Bestand nach Haltung trennen und bytegleiche Bilder entfernen**, bevor
   er zum Trainieren dient. Mehrere Boegen derselben Haltung liefern sonst
   abhaengige Beispiele.
4. **Neuen trainierbaren Meterleser planen.** Der vorhandene Leser ist eine feste
   Zeichenvorlage und besitzt keinen Trainingsweg. Bis dahin dienen die 897 Bilder
   als schwach zugeordneten Bestand und die 30 Handablesungen als exaktes Gold.
5. **Fuer den naechsten Kandidaten eine vollstaendig neue Messhaelfte
   reservieren.**

Zu Punkt 5 eine Einschraenkung, die aus einer Entscheidung dieses Laufs stammt:
Es gibt **keine unberuehrte HD-Reserve mehr.** Alle 25 freien HD-Haltungen
wurden in diesen Bestand genommen, weil es so wenige waren. Frei sind noch 291
SD-Haltungen mit 737 protokollierten Boegen. Fuer HD braucht der naechste
Kandidat entweder neue Haltungen oder er wird dort bewusst gegen einen bekannten
Bestand gefahren.

Am 2026-08-09 wurden davon 50 SD-Haltungen mit 130 Boegen als neue, noch nicht
ausgewertete Reserve V2 festgeschrieben. Sie ist fuer Training, Kalibrierung und
Kandidatenauswahl gesperrt. Der Beleg liegt unter
`<KnowledgeRoot>\training\diagnostics\bcc_pdf_auswahl\messreserve_sd_v2.json`.

## 10. Warum der Meterleser scheitert — vier belegte Anordnungen

Die Sichtprobe von 30 Bildern (`qa_bericht.json`) hat 25 exakte, 29 auf
Zehntelmeter passende und einen grob falschen Sollwert ergeben; kein Bild war
unleserlich. Die vier kleinen Abweichungen liegen bei +0,07, −0,02, +0,04 und
+0,10 m — das ist die Strecke, die die Kamera zwischen dem festgehaltenen Moment
und dem extrahierten Bild faehrt, kein Lesefehler.

Der eine grobe Fall ist ebenfalls kein Lesefehler: Bei `07.717339-690761` nennt
das Protokoll 0,50 m fuer Sekunde 38, im Bild stehen 2,98 m. Der Leser hatte
dort gar nichts gelesen. Das Rauschen sitzt in der Zuordnung zwischen Protokoll
und Videozaehlerstand, nicht im Leser.

Beim Nachsehen an den Bildern zeigt sich die eigentliche Ursache. Der Archiv-
bestand verwendet mindestens vier deutlich verschiedene Einblendungen:

| Haltung | Meterstand im Bild | Was den Leser bricht |
|---|---|---|
| `88218-88316` | weiss auf dunklem Balken, unten rechts, `1,54 m` | Zeichenvorlagen passen nicht (`2` wird `Z`) |
| `07.717339-690761` | weiss, **oben links**, gross, `2.98m` | anderer Ort, kein Leerzeichen vor `m` |
| `06.24379-06.24377` | **gelb**, unten rechts, `LZ1: + 0009.09 m` | Farbe, Praefix, fuehrende Nullen, vier Ziffern |
| `06.691078-691070` | **schwarz auf hellgrauem Kasten**, unten rechts, `0.30` | umgekehrte Polaritaet, keine Einheit |

Der Leser erwartet hellen Text auf dunklem Grund an einer festen Stelle. Die
vierte Anordnung bricht schon an der Polaritaet.

**Zurueckgezogen:** Ein maschineller Versuch, die Anordnungen ueber die
Kantendichte je Bildzeile zu zaehlen, ergab 66 % "oben". Das ist falsch — oben
steht bei diesen Videos der Kopftext mit Ortschaft, Strasse und Profil, nicht
der Meterstand. Gezaehlt wurde die Titelzeile. Das Warnsignal stand im eigenen
Ergebnis: 84 von 364 Haltungen mit angeblich zwei Anordnungen, obwohl eine
Haltung ein Video mit einer Einblendung hat.

Eine belastbare Anordnungs-Zaehlung braucht deshalb den Blick auf die Stelle des
Meterstands, nicht auf Bildstatistik — zum Beispiel ein Bild je Haltung, kurz
menschlich eingeordnet.

Dafuer ist jetzt eine blinde 40er-Sichtung festgeschrieben. Sie verwendet genau
ein Bild aus 40 neuen physischen Haltungen und schliesst die 30 Haltungen der
frueheren Sichtprobe aus. PDF-Wert und Ergebnis des alten Lesers sind verdeckt.
Der Mensch klickt direkt auf die Meteranzeige und ordnet Polaritaet, Farbe und
Schreibweise ein. Erst eine vollstaendige, an Queue- und Bild-SHA gebundene
Review darf ausgewertet werden. Damit kann Kopftext nicht mehr automatisch als
Meterstand gezaehlt werden.

## 11. Ergebnis der 40er-Sichtung — der Ort war nicht das Problem

Auf allen 40 Bildern war ein Meterstand sichtbar.

| Merkmal | Verteilung |
|---|---|
| Lage | unten rechts 38 (95 %, Wilson 83,5–98,6 %), unten links 2, oben 0 |
| Polaritaet | hell auf dunkel 18, **dunkel auf hell 18**, andere 4 |
| Farbe | weiss/grau 20, gelb 7, andere 13 |
| Schreibweise | mit Praefix/fuehrenden Nullen 19, Zahl mit Einheit 15, ohne Einheit 6 |

**Damit ist auch die zweite eigene Erklaerung widerlegt.** Nach den vier
Einzelbildern oben stand im Bericht, der Leser suche "am falschen Ort". Das
stimmt nicht: Die Lage ist in 95 % der Faelle genau dort, wo er sucht. Der
oben-links-Stil aus `07.717339-690761` existiert, war in dieser Stichprobe aber
nicht enthalten — die 30 Haltungen der frueheren Sichtprobe waren ausgeschlossen.
Er ist also selten, nicht typisch.

Gebrochen wird der Leser von Polaritaet und Schreibweise:

| Kombination | n | Anteil |
|---|---:|---:|
| unten rechts, hell auf dunkel, weiss/grau, **mit Praefix/Nullen** | 12 | 30 % |
| unten rechts, **dunkel auf hell**, andere Farbe, mit Einheit | 8 | 20 % |
| unten rechts, **dunkel auf hell**, andere Farbe, ohne Einheit | 4 | 10 % |
| uebrige acht Kombinationen | 16 | 40 % |

Die groesste Gruppe unterscheidet sich vom Erwarteten **nur durch Praefix und
fuehrende Nullen** — Lage, Polaritaet und Farbe stimmen dort bereits. Das ist
die guenstigste Verbesserung. Danach kommt die Polaritaet: Auf 18 von 40
Bildern ist der Text dunkler als sein Grund, der Leser sucht dort das Gegenteil
von dem, was da ist.

### Zwei unabhaengige Wege stimmen ueberein

Genau 2 der 40 Bilder erfuellen alle Annahmen des heutigen Lesers gleichzeitig
(hell auf dunkel, weiss/grau, ohne Praefix) — 5 %. Die unabhaengig gemessene
Leserabdeckung auf dem Archiv betrug 11 %. Zwei getrennte Messwege,
Einzelbildabdeckung ueber 83 Haltungen und menschliche Einordnung ueber 40
Haltungen, landen damit in derselben Groessenordnung. Dass der Leser etwas mehr
schafft als die strengste Lesart, passt: Er beherrscht das Vierziffern-Format
teilweise.

### Folgerung fuer einen neuen Leser

Er muss Textbereiche zuerst finden und danach verschiedene Darstellungen lesen
koennen. Eine feste helle Vorlage reicht nicht. Die Lage darf dabei als starker
Vorrang genutzt werden (unten rechts), aber nicht als Bedingung.

Die Stichprobe findet die Hauptstile; exakte Anteile fuer das ganze Archiv
liefert sie nicht. Belege: `osd_layout_review_v1\bericht.json`, Queue-SHA-256
`5f38d2bfb00d381fff19daed69168fd8f505e22ce4d625141294cff48d99c513`,
Review-SHA-256 `f498d8f1e4312348db3b181e9b88dd6df1bfb62d7e192573d5e264af30653729`.

Die Sichtung ist abgeschlossen: In allen 40 Bildern war ein Meterstand sichtbar.
38/40 lagen unten rechts (95,0 %, Wilson-95-%-Bereich 83,5-98,6 %), 2/40 unten
links; keiner oben. Die Polaritaet teilt sich in 18 hell auf dunkel, 18 dunkel
auf hell und 4 andere. Farben: 20 weiss/grau, 7 gelb, 13 andere. Schreibweisen:
19 mit Praefix oder fuehrenden Nullen, 15 Zahl mit Einheit, 6 Zahl ohne Einheit.
Damit ist die feste Ortsannahme weniger falsch als die alte automatische Zaehlung
behauptete, aber eine einzige Polaritaets- und Formatannahme ist klar unzureichend.
Die 40er-Stichprobe belegt Hauptstile, nicht deren exakten Anteil im Gesamtarchiv.

Der erste kleine Leserumbau ist gemessen, aber nicht freigegeben. Eine reine
Parseraenderung reichte nicht: Auf den 12 passenden Bildern las die alte
Masken-/Vorlagenkette 0/12. Ein enger, optionaler Tesseract-Rueckfall fuer die
vollstaendige Form `LZ... + 0000.00 m` liest nun 8/12; alle acht passen zum
schwachen PDF-Label. Nach der Erweiterung auf beide unteren Ecken und beide
Polaritaeten liefert nur dieser neue Weg 12 Werte, 12/12 passend. Der gesamte
Leser liefert in der 40er-Probe 13 Werte; 12 passen, einer ist falsch oder nicht
pruefbar. Die Goldbestaende veraendern sich ebenfalls: SD steigt auf 82/82
richtig, HD bleibt bei 0. Der zunaechst noch gelieferte falsche HD2-Wert wird
durch die anschliessende Trennzeichen-Sperre verworfen; HD2 liefert nun keinen
Wert und keinen bekannten Fehler.

## 12. Der Praefix-Rueckfall, unabhaengig nachgemessen

Die Zahlen 8/12 und 12/12 stehen auf denselben 40 Bildern, aus denen die
Diagnose stammt. Damit sind diese 40 die Kalibriermenge des Lesers, nicht sein
Pruefstand. Als unabhaengige Zahl dient deshalb die Abdeckung auf den 83
Haltungen der Abdeckungsmessung; 74 davon kommen in der 40er-Sichtung nicht vor.

| | Haltungen | vorher | nachher |
|---|---:|---:|---:|
| **SD, unabhaengig** | 56 | 10 % | **28 %** |
| HD, unabhaengig | 18 | 6 % | 8 % |
| SD, in der Kalibriermenge | 4 | 18 % | 45 % |
| HD, in der Kalibriermenge | 5 | 1 % | 1 % |

Haltungen mit brauchbarer Abdeckung (≥ 70 %) stiegen auf SD von 0 auf 8;
Haltungen, die gar nichts liefern, sanken von 18 auf 10. **Rueckwaertspruefung:
27 Haltungen besser, keine einzige schlechter.**

Die Trennung war kein Formalismus: Auf den Kalibrierhaltungen misst man 45 %,
auf unabhaengigen 28 %. Nur auf den 40 Bildern gemessen, stuende hier eine um
zwei Drittel zu hohe Zahl.

### Zwei Eigenschaften des Wegs

**Laufzeit.** Der Rueckfall startet je Bild einen Tesseract-Prozess. Auf 60
Bildern gemessen: Praefix-Stil 17 → 68 ms je Bild (Faktor 4,0) bei 11 → 21
gelesenen Werten; weiss-auf-dunkel 17 → 19 ms (Faktor 1,2) ohne Gewinn. Das Tor
greift also nur, wo es zahlt. Ein 675-Bilder-Video im Praefix-Stil braucht
dadurch rund 46 s statt 11 s allein fuers Meterlesen — der ganze Vorabdurchlauf
lag vorher bei 29 s.

**Spaetere Erweiterung des Testkandidaten.** Der aktuelle Stand arbeitet nicht
mehr nur unten rechts und hell auf dunkel. Er prueft beide unteren Ecken und
beide Polaritaeten, verwirft aber zugleich unvollstaendige Vorlagenfragmente.
Die 83 Haltungen wurden mit diesem Stand neu gemessen:

| | Haltungen | gelesene Bilder | Abdeckung |
|---|---:|---:|---:|
| **SD, unabhaengig** | 56 | 235/1108 | **21,2 %** |
| HD, unabhaengig | 18 | 14/354 | 4,0 % |
| SD, in der Kalibriermenge | 4 | 27/79 | 34,2 % |
| HD, in der Kalibriermenge | 5 | 0/98 | 0,0 % |

Ueber alle 83 eindeutigen Videos sind es SD 22,1 % und HD 3,1 %. Drei weitere
Auswahleintraege bleiben wegen nicht eindeutiger Videozuordnung unangetastet.
Der neue Lauf bindet den Leser-SHA und den SHA der festen Auswahl. Er bestaetigt
den Rueckgang gegenueber dem engeren Kandidaten; mehr Polaritaeten allein
gleichen die strengere Fragment-Sperre nicht aus.

Fehlt Tesseract auf einem Rechner, liefert `shutil.which` `None` und der
bisherige Leser laeuft unveraendert weiter — der Weg ist optional und
fail-safe, aber das Ergebnis haengt damit von der lokalen Installation ab.

Auch der erweiterte Kandidat darf noch keinen Meterstand im Copiloten anzeigen:
21,2 % unabhaengige SD-Abdeckung und 4,0 % HD-Abdeckung reichen nicht. Die enge
Trennzeichen-Sperre beseitigt zwar den bekannten HD2-Fehler ohne Rueckgang in
der 83-Video-Messung, schafft aber keine ausreichende HD-Abdeckung. Der Stand
bleibt `diagnostic_not_deployed`.

## 13. Der Meterleser nach dem Zusatzweg fuer zwei Nachkommastellen

Fuenf Archivstile wurden am 2026-08-09 einzeln diagnostiziert und jede Loesung
adversarisch geprueft. Vier der fuenf sind derselbe Fall: dunkle Ziffern auf
hellem OSD-Kasten unten rechts, Form `NN.NN m` oder `NN,NN m`. Sie scheiterten
alle an derselben Stelle — an der EINEN globalen Otsu-Schwelle.

Die Geraete schreiben eine Null mit einem eigenen Punkt in der Mitte. Liegt die
Schwelle zu hoch, verschmilzt der Punkt mit dem Ring und Tesseract liest eine
Acht. Daher die mit dem Auge bestaetigten Grobfehler `0,20 -> 8,26`,
`22,20 -> 22,28`, `0,30 -> 0,38` und `0,00 -> 8,00`.

### Der Zusatzweg

Vier Zutaten, alle gemessen:

1. **Schwellenfaecher statt einer Schwelle** — fuenf Anteile (0,30 bis 0,46) des
   95. Perzentils der Zone. Ueber elf Anteile auf drei Bildern gemessen: Der
   richtige Wert steht durchgaengig im tiefen Band.
2. **Zeilenisolierung** — nur Komponenten in einer Hoehenklasse, Median-Mitte
   ±8 px, plus Satzzeichen auf der Grundlinie. Ohne den Dezimalpunkt wird aus
   `0.20` die Zahl `020`.
3. **Quorum 3 von 5** — nur ein Wert, den mindestens drei Schwellen gleich
   lesen. Bei `22,20` bekommt die falsche 22,28 dadurch keine Mehrheit; der
   Leser schweigt, statt falsch zu antworten.
4. **Einheitspflicht** — `(\d{1,3})[.,](\d{2})\s*m` als Vollstring-Anker. Das
   ist die einzige Sperre gegen Datumsbruchstuecke: Ein angeschnittenes
   Datumsfeld liefert `.10.24` oder `16.24`, und ohne diese Pflicht waeren das
   die Meterstaende 10,24 m und 16,24 m.

Der Weg ist strikt **additiv**. Er laeuft nur, wenn Vorlagenweg und
Vierziffern-Rueckfall beide nichts liefern, und er feuert auf allen 94
menschlich abgelesenen Goldbildern null Mal. Ein Ersatz war ausgeschlossen: Er
kostete in jedem gepruefen Rezept 10 bis 11 Goldwerte, weil der Goldbestand
`LZ2: 0000.30 m` traegt und eine Form mit hoechstens drei Vorkommastellen eine
vierstellige Zahl nie treffen kann.

### Messung

| 897 beschriftete Archivbilder | geliefert | exakt | grob falsch |
|---|---:|---:|---:|
| Ausgangsstand 2026-08-09 frueh | 324 (36 %) | 215 | 81 (25 %) |
| nach der Vollstaendigkeitsregel | 292 (33 %) | 225 | 28 (10 %) |
| **mit Zusatzweg** | **384 (43 %)** | **314** | **29 (8 %)** |

Der Zusatzweg liefert 92 Werte. Genau einer davon zaehlt als grob falsch, und
er ist keiner: Auf `955509-4789` steht im Bild sichtbar `0.00 m`, der Leser las
0,00 — falsch ist das PDF-Label mit 0,2. Mit dem Auge geprueft.

| je Haltung, 83 Archivhaltungen | vorher | nachher |
|---|---:|---:|
| SD-Abdeckung | 23 % | **45 %** |
| Haltungen ≥ 70 % | 8 | **21** |
| Haltungen ohne jeden Wert | 32 | **17** |
| HD-Abdeckung | 2 % | 2 % |

**Rueckwaertspruefung: 17 Haltungen besser, keine einzige schlechter.**
SD-Gold unveraendert 82 richtig / 0 falsch. 259 Tests gruen.

### Bewusst nicht abgedeckt

- **Anzeigen ohne Einheit** (`0.30` im Graukasten). Die Einheitspflicht sperrt
  sie. Das ist der Preis fuer den Datumsschutz und bewusst so gewaehlt.
- **Meterstand oben links, grosse weisse Ziffern.** Ertrag 1 von 897 Bildern,
  aber im Quellvideo zwei stille Fehler und ein negativer Stand, der positiv
  wuerde. Nutzen zu klein, Fehlertyp zu gefaehrlich.
- **Zone unten links.** Bleibt gesperrt; dort steht das Aufnahmedatum.
- **HD/1080p.** Der Zusatzweg hilft dort nicht (2 % unveraendert).

### Laufzeit

202 ms je Bild gegen vorher 74 ms — bis zu fuenf Tesseract-Laeufe statt einem.
Auf einem 675-Bilder-Video sind das rund zwei Minuten allein fuers Meterlesen.

Der Status bleibt `diagnostic_not_deployed`. 45 % Abdeckung auf SD und 2 % auf
HD reichen nicht, um im Copiloten verlaesslich einen Meterstand anzuzeigen.

## Belege

| Was | Wo |
|---|---|
| Messbestand mit Aufteilung | `<KnowledgeRoot>\training\diagnostics\bcc_pdf_auswahl\messbestand_v1.json`, SHA-256 `45f83df5bb8a2815d191c1a97951e5481cf5a2ee83a2064ccdafaed63530a471` |
| Gesperrte Haltungen | `…\bcc_pdf_auswahl\gesperrte_haltungen.json` |
| Leserabdeckung | `…\bcc_pdf_auswahl\meter_abdeckung.json` |
| Einzelergebnisse je Haltung | `…\bcc_pdf_recall_20260809\haltungen\` |
| Gesamtauswertung | `…\bcc_pdf_recall_20260809\messung_conf040_gesamt.json` |
| SD-/HD-Auswertung | `…\bcc_pdf_recall_20260809\messung_conf040_sd.json` und `messung_conf040_hd.json` |
| PDF-Positionen | `PdfCodeScanner`-Lauf 2026-08-09, 470 Haltungen / 1158 Boegen |
| Blinde Precision-Pruefung | `<KnowledgeRoot>\training\diagnostics\bcc_pdf_precision_queue_v1`, 154/154 hashgepruefte Clips |
| Precision-Bericht | `<KnowledgeRoot>\training\diagnostics\bcc_pdf_recall_20260809\precision_conf040.json`, Queue und Review SHA-gebunden |
| OSD-Bestand | `<KnowledgeRoot>\training\diagnostics\osd_wahrheit_protokoll_v1`, 897 eindeutige Bilder aus 364 physischen Haltungen, Status `qa_offen` |
| OSD-Sichtprobe | `<KnowledgeRoot>\training\diagnostics\osd_wahrheit_protokoll_v1_qa`, 30 Bilder aus 30 Haltungen |
| OSD-Anordnungs-Sichtung | `<KnowledgeRoot>\training\diagnostics\osd_layout_review_v1`, 40/40 Bilder aus 40 neuen Haltungen, blind abgeschlossen, Queue- und Review-SHA gebunden |
| OSD-Praefix-Rueckfall | `...\osd_layout_review_v1\prefix_fallback_bericht.json`, Zielstil 8/12, neuer Weg insgesamt 12/12 schwach passend; Gold SD 82/82, HD 0, HD2 0 geliefert/0 falsch; Status `diagnostic_not_deployed` |
| OSD-Archivabdeckung aktuell | `...\bcc_pdf_auswahl\osd_archiv_abdeckung_current_d6d43cd5_20260809.json`, 83 eindeutige Videos, Leser-SHA und Auswahl-SHA gebunden, SD 22,1 %, HD 3,1 % |
| Neue SD-Messreserve | `<KnowledgeRoot>\training\diagnostics\bcc_pdf_auswahl\messreserve_sd_v2.json`, 50 Haltungen / 130 Boegen, Status `reserved_not_evaluated` |

Hinweis zur Ablage: Die fruehere gemeinsame Datei `messung_conf040.json` konnte
durch einen SD- oder HD-Einzellauf ueberschrieben werden. Die Rohdaten waren davon
nicht betroffen. Das Werkzeug schreibt die drei Bereiche jetzt getrennt und legt
jeweils einen SHA-gebundenen Vergleichsbeleg daneben.
