# Bogen-Copilot: erster benutzbarer Stand — 2026-08-08

Der BCC-Weg ist von der Messung in ein benutzbares Werkzeug uebergegangen:
Vorabdurchlauf ueber ein Video, Vorschlaege mit Meterangabe, menschliches
Bestaetigen oder Korrigieren. Dieser Bericht haelt die Zahlen und die drei
Konstruktionsfehler fest, die erst beim Benutzen sichtbar wurden.

## 1. Der Kandidat

Drei Seeds desselben Rezepts (`nc:15`, nur Klasse 14 belegt, identische Daten):

| Seed | interne mAP50 | Benchmark (37 Boxen, conf 0,25) | Video (10 Boegen, conf 0,50) |
|---|---:|---:|---:|
| 44 | 0,7998 | 28 | 7/10, 5 Fehlalarme |
| 45 | 0,8201 | 20 | **2/10**, 0 Fehlalarme |
| 46 | **0,8263** | 25 | **7/10, 3 Fehlalarme** |

Gepinnt wurde nach interner Validation — ein Kriterium, das vor den Messungen
festgelegt war, damit die Auswahl nicht auf der Pruefmenge stattfindet. Das
trifft Seed 46.

**Zwei Aussagen sind dabei zerbrochen.** Erstens: `nc:15` ist nicht besser als
das Ein-Klassen-Modell — die Spannen (20–28 gegen 21–26) ueberlappen
vollstaendig. Die fruehere Aussage beruhte auf Seed 44 allein. Zweitens: Die
interne Validation sagt ueber die Verallgemeinerung nichts — der Seed mit der
schlechtesten internen Zahl war auf dem Benchmark der beste. Die Pinning-Regel
bleibt gueltig, weil sie Benchmark-Auswahl verhindert, nicht weil sie
nachweislich das bessere Modell findet.

## 2. Der Arbeitspunkt gehoert zum Gewicht

Bei `conf 0,50` fanden Seed 44 und 46 je sieben von zehn Boegen, Seed 45 nur
zwei. Seine Konfidenzen liegen systematisch tiefer; bei 0,25 verhaelt er sich
wie die anderen bei 0,50. Auch die Grenze fuer starke Vorschlaege ist
modellabhaengig — fehlalarmfrei ab 0,70 (Seed 44) beziehungsweise 0,80 (Seed 46).

Ein fest verdrahteter Wert waere beim naechsten Modellwechsel still auf ein
Drittel der Treffer gefallen. `MinConfidence` und `StrongConfidence` sind
deshalb `required` und liegen als `workpoint.json` neben dem Kandidaten,
gebunden an Kandidaten-ID, Gewicht-SHA und einen Herkunftsbeleg. Ohne
gemessenen Arbeitspunkt laeuft kein Durchgang — nicht einmal die
Bildextraktion.

Damit ist auch der Pin strukturell erzwungen. Das ist wichtiger als es klingt:
Ohne ID waehlt der Sidecar nach hoechster interner mAP50, und das waere
`bcc_bogen_af8020b688ac_v3_negatives` mit 0,9489 — genau der Kandidat, der auf
9 von 14 sauberen Negativbildern feuert. Ein vergessener Pin wuerde nicht
irgendein Modell waehlen, sondern das schlechteste.

## 3. Ergebnis der sieben Durchgaenge

Kandidat `bcc_nc15_seed46_20260808`, conf 0,50, stark ab 0,80, 1 Bild/Sekunde.

| Haltung | zeitbasiert (Messung) | Vorschlaege | Meter-Abdeckung |
|---|---:|---:|---:|
| 36053-36052 | 12 | **4** | 90 % |
| 10.1035659-61895 | 4 | 3 | 92 % |
| 07.1028055-10.1064892 | 3 | 5 | **18 %** |
| 10261-10262 | 1 | 2 | **17 %** |
| 36051-33461 | 1 | 1 | 87 % |
| 10.1031726-07.1031724 | 0 | 0 | 83 % |
| 10.1037082-59981 | 0 | 0 | 95 % |
| **Summe** | **21** | **15** | |

Laufzeit 6 bis 51 Sekunden je Video.

### Menschliche Beurteilung aller 15 Vorschlaege

| Stufe | Vorschlaege | davon echter Bogen |
|---|---:|---:|
| stark (≥ 0,80) | 6 | **6 — 100 %** |
| schwach (0,50–0,80) | 9 | 7 — 78 % |
| gesamt | 15 | **13 — 87 %** |

Die grosse Haltung lieferte vier **verschiedene** Stellen (0,20 / 6,60 / 9,00 /
13,60 m) statt viermal derselben — die Zusammenfassung ueber den Meterstand
haelt.

**Diese 87 % sind kein Messwert.** Der Pruefplatz zeigt Konfidenz und Stufe vor
der Entscheidung. Alle Entscheidungen tragen deshalb `vorschlag_sichtbar=true`:
Trainingsmaterial ja, Messmaterial nie. Zum Vergleich die blinde Pruefung vom
Vortag (andere Schwelle, andere Auswahl): 15 von 64.

## 4. Drei Konstruktionsfehler, gefunden durch Benutzen

**Der Arbeitspunkt galt fuer das einzelne Bild statt fuer die Stelle.** Eine
Stelle wird ueber mehrere Bilder gesehen, und die Konfidenz schwankt; ein
Einbruch (0,6 – 0,4 – 0,7) zerlegte sie in zwei Vorschlaege. Sichtbar wurde es
nur auf den beiden Haltungen mit schlechter Meterlesung — bei guter Lesung
verdeckte der 1-Meter-Abstand den Fehler. Jetzt wird ab einer niedrigen
Aufnahmegrenze gesammelt und erst die fertige Stelle am Arbeitspunkt gemessen.
Wirkung: 19 → 15 Vorschlaege.

**Meterluecken spalteten eine Stelle.** Bilder mit und ohne Meterstand liefen
auf getrennten Wegen zusammen; bei 75–90 % Abdeckung erzeugte jede Luecke eine
zweite Meldung. Ein Bild ohne belastbaren Meterstand darf jetzt jeder Gruppe
zugeordnet werden, deren Zeitbereich es beruehrt — sein eigener Meterstand
entscheidet nichts, er ordnet nur zu.

**Ein geschaetzter Meterstand wurde wie ein gelesener behandelt.**
`VideoFullAnalysisService.EstimateMeter` schaetzt linear aus der Zeit und waechst
immer monoton; eine zurueckgesetzte Kamera waere damit nie als dieselbe Stelle
erkannt worden. Geschaetzte Werte zaehlen jetzt nicht als Ort und werden nach
aussen als geschaetzt gekennzeichnet. Fehlt ein Meterstand ganz, ist die Angabe
`null` statt `0,0` — eine erfundene Null sieht aus wie eine Messung.

## 5. Offener Engpass: der OSD-Meterleser

Der Leser (`training/scripts/osd_meter_leser.py`) erreicht auf dem dominanten
Stil 90 % Abdeckung, auf dem Vierziffern-Layout nur 17–31 %. Genau die beiden
Haltungen mit 17–18 % sind die einzigen, auf denen der Durchgang **mehr**
Meldungen erzeugt als die zeitbasierte Messung.

Zusaetzlich eine bestaetigte Fehllesung: **133,08 m in einer Haltung von keinen
20 m.** Der Formvalidator liess sie durch, weil `133.08` eine gueltige
Zahlenform ist. Es fehlt eine Plausibilitaetspruefung gegen Haltungslaenge und
Sprungweite. Der Verbraucher faengt eine solche Zahl inzwischen ab
(`MaxPlausibleMeter`), aber das ersetzt die Behebung am Leser nicht.

Menschlich abgelesene Wahrheitswerte aus dem Prueflauf:

| Haltung | Leser | richtig |
|---|---|---|
| 07.1028055-10.1064892 | 133,08 m | Fehllesung |
| 07.1028055-10.1064892 | nicht lesbar | 3,00 m |
| 07.1028055-10.1064892 | nicht lesbar | 3,80 m |
| 10261-10262 | nicht lesbar | 0,70 m |

## 6. Was noch fehlt

- Sidecar-Anbindung in C# (der Durchgang laeuft bisher ueber das Prototypskript)
- Anzeige der Vorschlagsliste im Programm (Variante B: Vorabdurchlauf, nicht
  live im Player — bei jedem zweiten falschen Vorschlag zerstoert eine
  Live-Einblendung das Vertrauen, von dem der Assistent lebt)
- Nachtrainieren → messen → nur bei Verbesserung tauschen. Ohne diesen Teil ist
  das Sammeln folgenlos: Das Modell wird durch Bestaetigungen nicht besser, es
  hat feste Gewichte.

## 7. Einordnung

Die Zahlenbasis bleibt schmal: 7 Videos, 10 protokollierte Boegen, 15
Vorschlaege. Das zeigt Richtung, keine Abnahme. Fuer einen Assistenten mit
Pflichtbestaetigung reicht es zum Anfangen — nicht fuer eine Zahl in der
Oberflaeche.

Mit der Herkunftserfassung entsteht das fehlende Messmaterial ab jetzt von
selbst: Jede Codierung mit ausgeschaltetem Assistenten ist unabhaengig
entstanden und darf messen.
