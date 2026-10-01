# BCC-Videoweg: Arbeitspunkt aus menschlichen Urteilen — 2026-08-08

Nachfolger von `BCC-VIDEO-MESSUNG-PAKET4-2026-08-07.md`, dessen zwei
Kernaussagen (39 echte Bögen, Arbeitspunkt 0,10) durch die menschliche
Blindprüfung widerlegt sind. Dieser Bericht ersetzt sie.

## Datengrundlage

- **Blindprüfung:** alle 64 Meldungen des ersten Videolaufs, einzeln beurteilt
  (`C:\KI_BRAIN\eval_review\bcc_video_fehlalarm_review.json`; Review-Werkzeug:
  `tools/EvalVisibilityReview/bcc_video_fehlalarm_review_server.py`).
- **Kurve:** `C:\KI_BRAIN\training\diagnostics\bcc_video_messung_20260807\
  schwellenkurve.json`. Drei Schachtanfänge sind ausgenommen — sie gelten als
  durch Trimmung entfernbar, nicht als Fehlalarm.

## Was die Blindprüfung zeigte

- Von den 64 gemeldeten Gruppen wurden **15** als echte Bögen bestätigt
  (14 ausserhalb der Schachtanfänge; 43 „kein Bogen", 4 „unsicher").
- Die KI-Sichtprüfung hatte 39 als Bogen eingestuft; bestätigt wurden 13 —
  **Treffgenauigkeit 33 %**. KI-Sichtprüfungen sind als Beleg ungültig; sie
  kommen in dieser Pipeline nicht mehr als Wahrheitsersatz vor.

## Die Kurve (Recall gegen die 10 protokollierten Bögen)

| conf | Recall | richtig | falsch | unsicher | Precision |
|---:|:---:|---:|---:|---:|---:|
| 0,10 | 10/10 | 25 | 43 | 4 | 36,8 % |
| 0,15 | 9/10 | 24 | 36 | 2 | 40,0 % |
| 0,25 | 8/10 | 22 | 31 | 2 | 41,5 % |
| 0,35 | 7/10 | 19 | 22 | 2 | 46,3 % |
| **0,50** | **7/10** | **17** | **12** | **1** | **58,6 %** |
| 0,60 | 6/10 | 15 | 7 | 1 | 68,2 % |
| 0,70 | 3/10 | 9 | 0 | 1 | 100 % |

## Arbeitspunkt: conf 0,50

Begründung: Bei 0,10 kostet jeder Vorschlag den Operateur mehr Prüfzeit als
Nutzen (grob jeder dritte Vorschlag echt). Bei 0,50 ist jeder zweite Vorschlag
echt bei noch 7 von 10 protokollierten Bögen. 0,70 ist makellos, aber nutzlos
(3/10). Der Punkt 0,50 gilt für den **Videoweg mit menschlicher Bestätigung**;
für die Standbild-Messlatte bleibt das Protokoll 0,25.

## Das nc:15-Artefakt (Einbauweg ohne Vertragsänderung)

Training: `C:\KI_BRAIN\training\diagnostics\bcc_nc15_20260807` (Seed 44,
volle 15er-Klassenkarte, nur BCC-Boxen auf ID 14, 1101 Trainingsbilder davon
944 Hintergrund). Messung gegen `detect_benchmark_v1`:

| conf | TP (von 37) | FP | FA Negative (von 75) | FA Fremdschaden (von 220) |
|---:|---:|---:|---:|---:|
| 0,05 | 31 | 3 | 2 | 21 |
| 0,10 | 30 | 1 | 2 | 18 |
| 0,25 | 28 | 0 | 2 | 14 |
| 0,50 | 22 | 0 | 0 | 8 |

Einordnung: Der alte Ein-Klassen-Bestand lag bei 0,25 in der Dreier-Spanne
21–26. Ein Lauf mit 28 liegt darüber, bleibt aber **ein Lauf gegen drei
Seeds** — die stabile Aussage ist „nicht schlechter, keine
Fehlalarm-Verschlechterung, Vertrag erfüllt", nicht „besser". Der Video-
Nachlauf (`bcc_video_messung_nc15_20260808`, `--class-id 14`) fand 8/10
protokollierte Bögen bei derselben rohen Gruppenzahl.

Auffällig bleibt die Fremdschaden-Spalte: 14 von 220 Bildern mit anderem
Schaden feuern bei 0,25. Die hauptsächliche Verwechslung ist Bogen ↔
verschobene Rohrverbindung mit Knick — dieselbe runde dunkle Form voraus,
die auch in der Blindprüfung für Fehlurteile sorgte.

## Offene Entscheidung (bei Pascal)

**Zwei weitere Seeds** (rund 8 h GPU, keine Handarbeit; macht aus 1-gegen-3
ein sauberes 3-gegen-3 und schützt vor einem Glücks-Seed als gebundenem
Artefakt) **oder Einbau auf diesem einen Lauf**. Bis zur Entscheidung wird
nichts registriert; der Kandidat bleibt unter `training/diagnostics/`, nicht
unter `training/models/candidates/`.

Empfehlung dieses Berichts: die zwei Seeds. GPU-Zeit ist gratis, und der
gepinnte Kandidat stünde sonst auf n=1 gegen eine Dreier-Referenz — genau der
Fehler, gegen den die Dreier-Regel gebaut wurde.
