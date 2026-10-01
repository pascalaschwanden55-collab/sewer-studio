# Detect-Mehrklassen-Diagnose vom 03.08.2026

## Ergebnis

Der Kandidat `detect_gold_9eb020e30322` ist **nicht freigabefaehig**. Auf dem
erstmals ausgewerteten, menschlich reviewten Bestand erkennt er vor allem
`BCC_bogen` und eingeschraenkt `BCA_anschluss`. Elf weitere gemessene Klassen
haben keinen exakten Treffer; `BBD_boden` ist mangels Review-Beispiel nicht
messbar. Kein Modell wurde trainiert, aktiviert oder ersetzt.

## Gebundener Lauf

- Holdout: `detect_release_holdout_45b66da2c778`
- Review: 400/400 Bilder, SHA-256
  `995325b3865df2c4aea4eb1f585614a844d0ed7b219a21c4f6dc675585458a43`
- Kandidatengewicht: SHA-256
  `fdf30f77b6aa6271014d130248fde99089854bfc0e58b44d75d462b3b9172ebf`
- Auswertbar: 241 positive und 74 echte negative Bilder; 85 Bilder wurden
  ausgeschlossen
- Ground Truth: 350 bestaetigte Boxen
- Protokoll: `conf=0,25`, `imgsz=1280`, `IoU=0,5`, kein Schwellenlauf
- Technische Fehler: 0 auf 400 Bildern
- Labelblinder Vorhersagebeleg: SHA-256
  `a771cbd7fa1a959b49ecf41621df700259471494b7e110d73c7b96eb919adbf2`
- Vorhersage-Receipt: SHA-256
  `34d09bc80a458b719a6a881cbdde7c15ce810a6bb8dd76db34754a60bc99253a`
- Diagnosebericht: SHA-256
  `64bd6ae370bc1a0bc7320aca5a0921a89cfa467fc9b7ff1c5e926780dc00dcbc`

Der Ledger enthaelt weder Review-SHA noch Entscheidungen, Operateur-Referenzen,
Annotationen oder Ground Truth. Die Review wurde erst nach dem Schreiben und
erneuten Pruefen des Ledgers geladen.

Der unmittelbar vorherige Lauf mit denselben Metriken ist durch diesen finalen
Lauf ersetzt. Vor der Wiederholung wurden die Bindung zwischen Status- und
Review-Bytes sowie die Statussemantik fuer kuenftige vollstaendig abgedeckte
Holdouts gehaertet.

## Objektmetriken

Global: **TP 36, FP 59, FN 314**, Precision 37,9 %, Recall 10,3 % und
F1 16,2 %. Das Modell gab auf den 315 gewerteten Bildern 95 Vorhersagen aus.
Der Makro-F1 ueber die 14 messbaren Klassen betraegt 6,1 %.

| Klasse | Soll-Boxen | TP | FP | FN | Precision | Recall | F1 |
|---|---:|---:|---:|---:|---:|---:|---:|
| BCA_anschluss | 39 | 8 | 17 | 31 | 32,0 % | 20,5 % | 25,0 % |
| BAB_riss | 40 | 0 | 0 | 40 | 0,0 % | 0,0 % | 0,0 % |
| BAC_bruch | 15 | 0 | 0 | 15 | 0,0 % | 0,0 % | 0,0 % |
| BAA_verformung | 21 | 0 | 1 | 21 | 0,0 % | 0,0 % | 0,0 % |
| BAF_oberflaeche | 89 | 1 | 3 | 88 | 25,0 % | 1,1 % | 2,2 % |
| BAH_schadanschluss | 8 | 0 | 0 | 8 | 0,0 % | 0,0 % | 0,0 % |
| BAI_dichtung | 26 | 0 | 0 | 26 | 0,0 % | 0,0 % | 0,0 % |
| BAJ_verbindung | 18 | 0 | 3 | 18 | 0,0 % | 0,0 % | 0,0 % |
| BBA_wurzeln | 10 | 0 | 0 | 10 | 0,0 % | 0,0 % | 0,0 % |
| BBB_anhaftung | 8 | 0 | 2 | 8 | 0,0 % | 0,0 % | 0,0 % |
| BBC_ablagerung | 19 | 0 | 4 | 19 | 0,0 % | 0,0 % | 0,0 % |
| BBD_boden | 0 | 0 | 0 | 0 | nicht messbar | nicht messbar | nicht messbar |
| BBF_infiltration | 16 | 0 | 0 | 16 | 0,0 % | 0,0 % | 0,0 % |
| SONST_schaden | 4 | 0 | 0 | 4 | 0,0 % | 0,0 % | 0,0 % |
| BCC_bogen | 37 | 27 | 29 | 10 | 48,2 % | 73,0 % | 58,1 % |

Die klassenunabhaengige geometrische Zuordnung bildete 54 Paare. Davon hatten
35 dieselbe Klasse. Die separat berechnete klassenbewusste Zuordnung ergab 36
korrekte Treffer (TP). Die Abweichung entsteht bei einer BCA-Vorhersage, die
geometrisch einer staerker ueberlappenden BAH-Sollbox zugeordnet wird. Die
groesste Verwechslung ist `BAJ_verbindung` zu `BCC_bogen` mit acht Faellen.

## Echte Negativbilder

Auf 9 von 74 Negativbildern erschien mindestens eine Vorhersage. Das entspricht
einer Bild-Fehlalarmrate von 12,2 % und einer Spezifitaet von 87,8 %. Insgesamt
gab es 11 Fehlalarm-Boxen: sechs `BCA_anschluss`, zwei `BBC_ablagerung` und drei
`BCC_bogen`.

## Fachliche Grenze und naechster Schritt

Der Reviewbestand ist zwar vollstaendig, aber mit 74 statt 75 Negativbildern und
mehreren Klassen unter 20 Instanzen weiterhin `coverage_incomplete`. Unabhaengig
davon ist der Recall von 10,3 % bereits zu niedrig fuer einen produktiven Einsatz.

Die 400 Holdout-Bilder bleiben strikt von Training, Gold, Few-Shot und KB
getrennt. Fuer die naechste Trainingsrunde muessen neue Bilder aus anderen
Haltungen verwendet werden. Vor blindem Nachsammeln werden besonders die
Trainingslabels fuer `BAB`, `BAF`, `BAI`, `BAJ`, `BBC` und `BBF` geprueft: Diese
Klassen besitzen bereits Trainingsbeispiele, generalisieren aber im frischen
Bestand kaum oder gar nicht. Zusaetzlich sind getrennte Beispiele fuer
`BAJ_verbindung` gegen `BCC_bogen` und echte Hard-Negatives sinnvoll.

Sobald dieses Ergebnis die weitere Modellentwicklung beeinflusst, ist dieser
Bestand nur noch ein Diagnose-/Entwicklungsbestand. Vor einer spaeteren
Aktivierung ist deshalb ein neuer, zuvor unberuehrter Release-Holdout notwendig.

## Nachtrag 2026-08-03 (spaeter): Holdout-Kontamination aufgedeckt

Der schreibfreie Prueflauf `training/scripts/repair_pdf_gold_holding_ids.py`
(Bericht: `docs/quality/PRUEFBERICHT-PDF-GOLD-HALTUNGS-IDS-2026-08-03.json`,
SHA-256 `239ebd9b50f1ddec089a4fa74dc592288b854d5cf6191ec2a0b2dac31dfd08a7`)
hat 239 PDF-Goldsamples mit falscher Haltungs-ID gefunden. Dreizehn davon
zeigen byte- bzw. ordnerbelegt auf zwei Haltungen DIESES Holdouts:
`07.148371-10300` (4 Samples) und `60604-60603` (9 Samples).

Acht dieser Samples stehen im Trainingsregister DETECT_ALL, mit dem der
Kandidat `detect_gold_9eb020e30322` trainiert wurde:

- aus `07.148371-10300` (Gruppe 1, Bildbeleg): `wb_4eb82c1a51f7`,
  `wb_6bbc15171015`, `wb_e343ca2a7f4e`
- aus `60604-60603` (Gruppe 4, farbnormalisiert): `wb_33e0e2b3d56f`,
  `wb_5f7cbd92367e`, `wb_070730d4a8eb`, `wb_647eefeb9840`, `wb_6ab38a8e51a4`

Konsequenz: Der Holdout ist fuer diesen Kandidaten nicht unabhaengig; die
gemessenen Werte (Recall 10,3 %, F1 16,2 %) sind nach OBEN verzerrt. Am Urteil
`not_release_qualified` aendert das nichts — es verschaerft es hoechstens.
Der oben verlinkte Diagnosebericht bleibt als Messartefact dieses Stands
bytegenau bestehen; diese Grenze der Interpretation ist mit diesem Nachtrag
markiert. Vor dem naechsten Training sind die acht Samples aus dem Register
zu entfernen, jede Haltungs-Reparatur braucht einen Nachlauf der
Eval-Schutzpruefung, und ein frischer Holdout bleibt ohnehin Pflicht.
