# Entwicklungsvergleich: detect_gold_3f45c1e945fe gegen den verbrauchten Holdout (2026-08-05)

**Ausdrücklich ein Entwicklungsvergleich, keine Abnahme.** Der Holdout
`detect_release_holdout_45b66da2c778` ist als Abnahmebestand verbraucht
(Kontamination belegt, Fehlfall-Review eingeflossen). Der Vergleich ist
trotzdem gültig, weil der Vorlauf auf demselben Bestand gemessen wurde.
Protokoll unverändert: `conf=0,25`, `imgsz=1280`, `IoU=0,5`, labelblind.
Der Messweg verlangte dafür den neuen Flag-Modus `--development-comparison`
(nur `not_deployed`-Kandidaten, fremd gebundener Holdout nur ausdrücklich).

Belege:
- Kandidat: `detect_gold_3f45c1e945fe` (99/150 Epochen, Early Stopping,
  `not_deployed`, Datensatz 1220 Bilder: 998 Gold + 286 Negative)
- Vorhersagebeleg SHA-256 `dd4c36b6429a0f3380445884e23ab8b5c1f24dcec9d889f506aa874f214d364f`
- Diagnosebericht: `training/reports/detect_release_diagnostic_..._20260805_225035_127690.json`
- Vorlauf-Beleg (Referenz): `..._20260803_154706_636933.json` (inkl. Nachtrag
  zur Kontamination in `DETECT-RELEASE-DIAGNOSTIC-2026-08-03.md`)

## Die fünf Vergleichszahlen

| Messgrösse | Vorlauf (9eb020e30322) | Neu (3f45c1e945fe) | Richtung |
|---|---:|---:|---|
| Recall global | 10,3 % | **15,7 %** | ↑ |
| Precision global | 37,9 % | 34,2 % | ↓ leicht |
| **BCA Recall** | 20,5 % | **23,1 %** | ↑ leicht |
| BCC Recall | 73,0 % | **78,4 %** | ↑ |
| Fehlalarm-Bildrate (74 Negative) | 12,2 % | **24,3 %** | ↓↓ schlechter |

F1 global: 16,2 % → 21,5 %. TP/FP/FN: 36/59/314 → 55/106/295.

## Klassentabelle (Recall, Soll-Boxen 350)

| Klasse | Soll | Vorlauf | Neu |
|---|---:|---:|---:|
| BAF_oberflaeche | 89 | 1,1 % | 4,5 % |
| BAB_riss | 40 | 0 % | 0 % |
| BCA_anschluss | 39 | 20,5 % | **23,1 %** |
| BCC_bogen | 37 | 73,0 % | **78,4 %** |
| BAI_dichtung | 26 | 0 % | **23,1 %** |
| BAA_verformung | 21 | 0 % | 9,5 % |
| BBC_ablagerung | 19 | 0 % | 0 % |
| BAJ_verbindung | 18 | 0 % | 11,1 % |
| BBF_infiltration | 16 | 0 % | 0 % |
| BAC_bruch | 15 | 0 % | 0 % |
| BBA_wurzeln | 10 | 0 % | 0 % |
| BAH_schadanschluss | 8 | 0 % | **37,5 %** |
| BBB_anhaftung | 8 | 0 % | 0 % |
| SONST_schaden | 4 | — | 0 % |

## Einordnung

**Hat sich BCA bewegt?** Ja, aber wie vorhergesagt nur leicht (20,5 → 23,1 %).
Bei +18 % Haltungen im Register bleibt es ein Schritt, kein Sprung — die
100-Haltungen-These wird weder bestätigt noch widerlegt.

**Sind die Fehlalarme gesunken?** Nein — sie haben sich verdoppelt (12,2 →
24,3 %), und das ist der ehrliche Pferdefuss des Laufs. Im Detail aber lesbar:
Die gezielt antrainierte Verwechslung BCD/BCE→BCA wirkt (BCA-Fehlalarme auf
Negativbildern 6 → 2). Der Anstieg kommt von neuen Klassen, die jetzt feuern
(BAF 4, BAJ 3, BBC 3, BCC 3, BAA/BAH je 2) — das Modell versucht mehr, und die
286 Negative haben (noch) nicht die spezifischen Verwechslungen dieser Klassen
abgedeckt.

**Der wichtigste Wert ist BAH.** Aus null wird 37,5 % Recall (3 von 8,
Precision 12,5 %) — bei nur 37 Register-Haltungen. Acht Instanzen sind dünn,
aber es ist das erste Lebenszeichen der Klasse überhaupt. Stützt das Szenario
„Schwelle deutlich unter 100 Haltungen" (eher Richtung 40 für unterscheidbare
Klassen) und damit den Sammelplan: BAH gezielt auf ~50–70 Haltungen bringen
und erneut messen, statt blind 100 anzustreben.

Interne Validierung vs. Holdout: R 32,2 % → 15,7 % — der Generalisierungsabstand
bleibt bei etwa Faktor 2 und ist damit das eigentliche Restproblem.
