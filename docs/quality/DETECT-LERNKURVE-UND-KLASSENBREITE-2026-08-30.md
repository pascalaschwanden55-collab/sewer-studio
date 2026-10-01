# Detect-Kandidat: Lernkurve und Klassenbreite

Stand: 2026-08-30. Zwei getrennte Fragen an denselben Datensatz.

Beide Untersuchungen sind **reine Diagnose**. Sie entstehen ausserhalb der
Freigabekette, tragen kein Kandidatenmanifest und duerfen nie aktiviert werden.
Der Mehrklassen-Kandidat bleibt `not_deployed`.

Sicherheitsstand 2026-09-02: Das vom Referenztraining im Plan-Datensatz
abgelegte `yolo26n.pt` wurde bytegleich nach
`C:\KI_BRAIN\training\diagnostics\quarantine\ea8e715f-yolo26n-9B09CC8B.pt`
verschoben. Danach akzeptierte der Gold-Validator den Datensatz wieder mit 852
Bildern und 894 Instanzen. Diese Bereinigung veraendert keine Diagnosezahl.
Die einheitliche FP32-Nachmessung der drei `best.pt` mit Stapel 4 wurde am
2026-09-02 abgeschlossen. Sie bestaetigt die qualitative Aussage unten.

---

## Teil 1 — Lohnt weitere Handarbeit? (Lernkurve)

### Die Frage

`training/scripts/detect_lernkurve.py` nennt sie selbst „die teuerste offene
Frage im Projekt": Ist der schwache Recall des Mehrklassen-Kandidaten
Materialmangel — dann zahlt sich jede weitere Handbox aus — oder ist die Kurve
flach, dann hilft Handarbeit nicht mehr und es braucht einen anderen Ansatz.

### Was gemessen wurde

Drei Trainings am 2026-08-13 (16:19, 16:41, 17:09). Derselbe Datensatz
`ea8e715f3c4cee8a5e43adae35c734e4c8890be389ab0bba91148126d785bfc2`,
dieselbe **unveraendert vollstaendige** Validierung und dieselben
Trainingsparameter (Seed 42, 40 Epochen, `imgsz=1280`, `batch=4`, Basisgewicht
`yolo26m.pt`, `deterministic=true`). Der kontrolliert veraenderte Anteil war die
Zahl der Trainingsbilder.

Die Diagnose verwendete Ultralytics-Standard-Augmentierung mit `flipud=0.0`,
`fliplr=0.5`, `hsv_h=0.015`, `hsv_s=0.7`, `hsv_v=0.4` und `mosaic=1.0`.
Der produktive Detect-Gold-Trainer verwendet dagegen `flipud=0.0`,
`fliplr=0.0`, `hsv_h=0.01`, `hsv_s=0.3` und `hsv_v=0.3`. Ergebnisse der
Diagnose sind deshalb nicht ohne Weiteres auf die produktive Kandidatenlinie
uebertragbar.

Die Ergebnisse lagen bis zum 2026-08-30 unausgewertet auf der Platte.

### Ergebnis

| Trainingsbilder | mAP50 | mAP50-95 | Recall | Precision | Laufzeit |
|---|---:|---:|---:|---:|---:|
| 343 (50 %) | 0,140 | 0,063 | 0,153 | 0,422 | 896 s |
| 514 (75 %) | 0,181 | 0,099 | 0,183 | 0,418 | 1304 s |
| 686 (100 %) | **0,297** | **0,159** | **0,344** | 0,332 | 1688 s |

**In diesen drei Laeufen ist die Kurve nicht flach.** Zwischen 75 % und 100 %
steigt mAP50 um 0,12 Punkte, waehrend sie zwischen 50 % und 75 % um 0,04
steigt. Der Recall verdoppelt sich ueber die drei Stufen. Mit nur einem Seed je
Stufe ist die genaue Kurvenform noch kein stabiler Schaetzer fuer neue Laeufe.

Die Precision faellt gleichzeitig. Der schwellenfreie mAP steigt in diesen
Laeufen trotzdem. Fuer einen Assistenten muss die Fehlalarmquote spaeter am
produktiven Arbeitspunkt getrennt geprueft werden.

### Epochenverlauf (mAP50)

| Epoche | 50 % | 75 % | 100 % |
|---:|---:|---:|---:|
| 10 | 0,081 | 0,129 | 0,153 |
| 20 | 0,128 | 0,159 | 0,237 |
| 30 | 0,119 | 0,213 | 0,296 |
| 35 | 0,135 | 0,167 | 0,307 |
| 40 | 0,140 | 0,181 | 0,297 |

Bei 50 % verbessert sich der Endwert nach Epoche 15 kaum. Bei 100 % liegt der
beste gezeigte Wert in Epoche 35 und faellt bis Epoche 40 leicht. Dieser
Einzellauf liefert keinen Hinweis, dass ein blosses Verlaengern desselben Laufs
ueber 40 Epochen geholfen haette. Er beweist aber nicht, dass Rechenzeit oder
andere Trainingsparameter allgemein keine Rolle spielen.

### Was diese Zahlen nicht sind

- **Ein Seed je Stufe.** Das verletzt die projekteigene Dreier-Regel. Schwankungen
  zwischen Epochen ersetzen keine Wiederholung mit weiteren Seeds.
- **Interne Validierung, kein Holdout.** Sie war im Projekt schon um Faktor 2
  freundlicher als die Wirklichkeit. Aussagekraeftig ist nur die *Form* der
  Kurve, nicht die absolute Hoehe.
- **Die Verkleinerung zieht pro Bild, nicht pro Haltung** (im Skript
  ausdruecklich vermerkt). Dadurch bleiben fast alle Haltungen vertreten,
  waehrend sich die Zahl der Bilder je Haltung aendert. Das ist keine saubere
  Aussage darueber, wie ein nach Haltungen neu gesammelter Bestand reagieren
  wuerde.
- **Andere Augmentierung als im produktiven Trainer.** Insbesondere die
  horizontale Spiegelung veraendert die Uhrlage. Der Versuch beantwortet daher
  nur die Frage fuer sein eigenes Trainingsregime.

### Schlussfolgerung

Der groessere Trainingsanteil war in dieser Diagnose klar besser. Das spricht
dafuer, weitere unterschiedliche Goldboxen zu sammeln. Es beweist weder
Materialmangel als alleinigen Engpass noch, dass jede zusaetzliche Handbox gleich
viel bringt. Fuer den damaligen Stand wurde der Goldbestand mit 1801 Eintraegen
ausgewertet.

---

## Teil 2 — Verwaessern die schwachen Klassen die guten? (Klassenbreite)

### Die Frage

Der Kandidat traegt 15 Klassen. Zwei davon haben **keine einzige**
Trainingsbox, eine hat fuenf. Kostet diese Breite die gut belegten Klassen
Leistung — dann lohnt ein enger Assistent — oder ist sie gratis, dann darf
weiter fuer alle 15 gesammelt werden.

Die Antwort entscheidet, wofuer die naechste Handarbeit eingesetzt wird.

### Klassenbestand im Datensatz `ea8e715f3c4cee8a5e43adae35c734e4c8890be389ab0bba91148126d785bfc2`

| Klasse | Train | Val | | Klasse | Train | Val |
|---|---:|---:|---|---|---:|---:|
| BCA_anschluss | 163 | 36 | | BAI_dichtung | 26 | 9 |
| BCC_bogen | 159 | 41 | | BBC_ablagerung | 23 | 20 |
| BAB_riss | 86 | 13 | | BBB_anhaftung | 15 | 8 |
| BAF_oberflaeche | 58 | 15 | | BAH_schadanschluss | **5** | 3 |
| BAC_bruch | 39 | 4 | | BBD_boden | **0** | **0** |
| BBF_infiltration | 39 | 9 | | SONST_schaden | **0** | **0** |
| BAA_verformung | 37 | 14 | | BBA_wurzeln | 27 | **0** |
| BAJ_verbindung | 33 | 12 | | | | |

Gesamt 710 Trainings- und 184 Validierungsboxen auf 686 bzw. 166 Bildern.

Drei Auffaelligkeiten: `BBD_boden` und `SONST_schaden` belegen einen
Ausgabeplatz ohne jedes Beispiel. `BAH_schadanschluss` hat fuenf
Trainingsboxen. `BBA_wurzeln` hat keine Validierungsbox und ist damit
ueberhaupt nicht messbar.

### Versuchsaufbau

`training/scripts/detect_klassenbreite.py` erzeugt aus demselben Datensatz zwei
engere Fassungen. Technisch fallen Labelzeilen der nicht gewaehlten Klassen weg;
die verbleibenden Klassen werden neu durchnummeriert.

**Bilder werden nie entfernt.** Ein Bild, dessen einzige ausgewaehlte Box
wegfaellt, wird fuer den engen Detektor zum Negativbild. Dadurch aendern sich
zugleich die Klassenmenge und der Hintergrunddruck. Der Versuch isoliert die
Klassenbreite also nicht als einzige Variable.

| Stufe | Klassen | Train-Boxen | davon neue Negativbilder |
|---|---|---:|---:|
| Referenz | alle 15 | 710 | — |
| `klassen_5` | BCA, BAH, BAI, BAJ, BCC | 386 | 305 von 686 |
| `klassen_2` | BCA, BCC | 322 | 368 von 686 |

`training/scripts/detect_klassenbreite_messung.py` trainierte die engen Stufen
mit denselben Trainingsparametern wie den Referenzlauf und wertete **je Klasse**
aus. Ein mAP ueber 2 Klassen und eines ueber 15 sind verschiedene Massstaebe
und werden nicht gegeneinander gestellt.

Die zuerst gespeicherten Werte waren kein ganz gleicher Vergleich: Die Referenz
war mit FP32 und Stapel 4 gemessen worden, die trainierten engen Stufen in der
eingebauten Pruefung mit FP16 und Stapel 8. Am 2026-09-02 wurden deshalb alle
drei vorhandenen `best.pt` einheitlich mit `half=False, batch=4` nachgemessen.
Die folgende Vergleichstabelle verwendet nur diese neue einheitliche Messung.

### Referenzwerte je Klasse (15 Klassen, `lernkurve_100/weights/best.pt`)

| Klasse | AP50 | AP50-95 | Recall | Precision | Val-Boxen |
|---|---:|---:|---:|---:|---:|
| BCC_bogen | **0,827** | 0,519 | **0,876** | 0,545 | 41 |
| BAH_schadanschluss | 0,705 | 0,540 | 0,667 | 0,370 | **3** |
| BCA_anschluss | 0,412 | 0,163 | 0,459 | 0,327 | 36 |
| BAI_dichtung | 0,405 | 0,204 | 0,556 | 0,667 | 9 |
| BAC_bruch | 0,388 | 0,136 | 0,000 | 1,000 | 4 |
| BAA_verformung | 0,374 | 0,218 | 0,500 | 0,432 | 14 |
| BAJ_verbindung | 0,187 | 0,072 | 0,496 | 0,229 | 12 |
| BAF_oberflaeche | 0,141 | 0,043 | 0,133 | 0,277 | 15 |
| BBC_ablagerung | 0,116 | 0,054 | 0,100 | 0,234 | 20 |
| BAB_riss | 0,079 | 0,061 | 0,077 | 0,333 | 13 |
| BBF_infiltration | 0,036 | 0,013 | 0,111 | 0,077 | 9 |
| BBB_anhaftung | 0,003 | 0,001 | 0,000 | 0,000 | 8 |

Gesamt: mAP50 0,306, mAP50-95 0,169 (reproduziert `results.csv` mit 0,297 —
Unterschied ist bestes gegen letztes Gewicht).

`BCC_bogen` steht auch im Mehrklassen-Modell klar an der Spitze. Der Wert von
`BAH_schadanschluss` beruht auf nur **drei** Validierungsboxen und ist sehr
unsicher. Auch `BAC_bruch` mit vier Validierungsboxen ist keine belastbare
Einzelklassen-Aussage.

### Ergebnis

Beide Trainings liefen sauber ueber 40/40 Epochen (1670 s bzw. 1708 s).

| Klasse | 15 Klassen | 5 Klassen | 2 Klassen |
|---|---|---|---|
| | AP50 / R / P | AP50 / R / P | AP50 / R / P |
| BCC_bogen | 0,827 / 0,876 / 0,545 | 0,815 / 0,829 / 0,550 | **0,845 / 0,878 / 0,607** |
| BCA_anschluss | 0,412 / 0,459 / 0,327 | 0,400 / 0,500 / 0,426 | 0,290 / 0,222 / 0,688 |
| BAJ_verbindung | 0,187 / 0,496 / 0,229 | **0,380 / 0,417 / 0,632** | — |
| BAI_dichtung | 0,405 / 0,556 / 0,667 | 0,231 / 0,000 / 1,000 | — |
| BAH_schadanschluss | 0,705 / 0,667 / 0,370 | 0,665 / 0,000 / 1,000 | — |

**Die vorliegenden Werte zeigen keinen systematischen Gewinn der Verengung.**
Es gibt keine einheitliche Richtung:
`BAJ_verbindung` verbessert sich deutlich (AP50 0,187 → 0,380), `BAI_dichtung`
verschlechtert sich deutlich (0,405 → 0,231). Die drei `BCC_bogen`-Werte liegen
nahe beieinander (0,827 / 0,815 / 0,845). Gegenueber den alten gemischten
Pruefeinstellungen aenderte die einheitliche Nachmessung AP50 je Klasse um
hoechstens 0,004. FP16/Stapel 8 gegen FP32/Stapel 4 erklaert die beobachteten
Stufenunterschiede damit nicht allein.

Damit ist kein Vorteil dieser konkreten Verengung belegt. Der Versuch widerlegt
aber nicht allgemein, dass Klassenbreite eine gut belegte Klasse beeinflussen
kann: Hintergrunddruck, Pruefeinstellungen und die sehr kleinen Stichproben sind
zusätzliche Einflussgroessen.

`BCA_anschluss` wird im 2-Klassen-Modell sogar klar schlechter: Der Recall
faellt von 0,459 auf 0,222, waehrend die Precision von 0,327 auf 0,688 steigt.
Das Modell wird vorsichtiger und findet weniger — siehe die Einschraenkung
unten.

Die Nullwerte beim Recall von `BAH` und `BAI` im 5-Klassen-Lauf beruhen auf nur
drei beziehungsweise neun Validierungsboxen. Solche Werte koennen stark
schwanken und duerfen nicht als allgemeine Klassenaussage gelesen werden.

### Der Vorbehalt, der dazugehoert

Der Aufbau haelt die Bildmenge konstant und aendert damit **zwei Dinge
gleichzeitig**: die Klassenzahl und den Hintergrunddruck. Im 2-Klassen-Satz
werden 368 von 686 Trainingsbildern zu Negativbildern — mehr als die Haelfte.
Das entspricht dem gewaehlten engen Einsatzziel, kann aber selbst den Recall
beeinflussen. Der Versuch kann diesen Effekt nicht von der Klassenzahl trennen.

Was er beantwortet: Fuer diesen Aufbau ist kein Vorteil des engen Detektors
belegt. Was er nicht beantwortet: ob die Klassenbreite selbst oder ein enger
Detektor mit eigens gesammelten Negativen anders wirken wuerde.

Wie in Teil 1 gilt: ein Seed je Stufe, interne Validierung, kein Holdout und ein
von der produktiven Linie abweichendes Augmentierungsregime.

### Schlussfolgerung

Der Versuch liefert keinen Grund, die bestehende 15er-Linie jetzt zu verengen.
Die naechste Datensammlung darf deshalb weiter auf bessere und vielfaeltigere
Goldboxen zielen. Das ist eine Arbeitsentscheidung, kein allgemeiner Beweis
gegen enge Modelle. Die Zielgroesse fuer einen Assistenten bleibt die
Fehlalarmquote; die hier ausgewiesene Precision steht am F1-Bestpunkt und nicht
am produktiven `conf=0,25`, taugt also nicht als Fehlalarm-Aussage.

---

## Belegdateien

| Was | Pfad |
|---|---|
| Lernkurve, drei Stufen | `C:\KI_BRAIN\training\cls_runs\lernkurve_{050,075,100}\results.csv` |
| Stufenbelege | `C:\KI_BRAIN\training\diagnostics\lernkurve_{050,075}\lernkurve.json` |
| Quelldatensatz | `C:\KI_BRAIN\training\datasets\ea8e715f3c4cee8a5e43adae35c734e4c8890be389ab0bba91148126d785bfc2` |
| Historische Referenz je Klasse | `C:\KI_BRAIN\training\diagnostics\referenz_15_klassenwerte.json` |
| Enge Datensaetze | `C:\KI_BRAIN\training\diagnostics\klassen_{2,5}\klassenbreite.json` |
| Enge Trainingslaeufe | `C:\KI_BRAIN\training\cls_runs\klassen_{2,5}` |
| Historische enge Klassenwerte | `C:\KI_BRAIN\training\diagnostics\klassen_{2,5}_klassenwerte.json` |
| Einheitliche FP32-/Stapel-4-Werte | `C:\KI_BRAIN\training\diagnostics\{referenz_15,klassen_5,klassen_2}_einheitlich_fp32_b4_klassenwerte.json` |
