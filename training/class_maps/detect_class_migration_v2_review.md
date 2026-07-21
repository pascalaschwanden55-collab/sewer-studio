# Fachliche Freigabe der Detect-Migration v2

Stand: 2026-07-16

Die maschinenlesbare Tabelle enthaelt 124 Zeilen: 68 vorgeschlagene Zuordnungen,
45 vorgeschlagene Ausschluesse und 11 Review-Zeilen. `BAG` kommt in zwei Quellen vor;
damit bleiben zehn fachliche Entscheidungen.

Alle Zeilen stehen absichtlich auf `pending`. Der lokale Export schreibt deshalb noch
keinen ungeprueften Datensatz.

## Zehn Entscheidungen

| Quelle | Beobachtung | Empfehlung |
|---|---|---|
| Annotation `29c9505302db`, gespeichert `BACB` | Freigabetext nennt `BABBC` | `BAB_riss` |
| Annotation `a01261a12b27`, gespeichert `BCAAB` | Freigabetext nennt `BCAAA`; Detect-Familie bleibt gleich | `BCA_anschluss` |
| Annotation `84dc8d637507`, gespeichert `BAFFE` | Beschreibung lautet „Schadhafter Anschluss“ | `BAH_schadanschluss` |
| Annotation `5bbd2b038007`, gespeichert `BAJC` | Freigabetext nennt Profilwechsel `AECXC` | `discard` |
| Annotation `7f5f4be0c15e`, gespeichert `BABBA` | Beschreibung lautet „Einragender Anschluss“; keine eigene BAG-Klasse in v2 | `SONST_schaden` |
| `BAG` in Alt-Map und Teacher-Daten | Einragender Anschluss; `BAG` selbst ist nicht auswaehlbar, `BAGA` waere gueltig | `SONST_schaden` |
| `BBE` | Sichtbares Hindernis oder Fremdobjekt | `SONST_schaden` |
| `BBG` | Sichtbarer Wasseraustritt | `SONST_schaden` |
| Modellname `deposit` | Bestehende produktive Bedeutung ist Ablagerung | `BBC_ablagerung` |
| Modellname `intrusion` | Bestehende produktive Bedeutung ist einragendes Dichtungsmaterial | `BAI_dichtung` |

## Bilder der fuenf Einzelpruefungen

- `29c9505302db`: `C:\Users\Besitzer\AppData\Local\AuswertungPro\review_frames\20250311_07.1027615-10523\frame_000120.png`
- `a01261a12b27`: `C:\Users\Besitzer\AppData\Local\AuswertungPro\review_frames\20251113_07.1066257-54818\frame_000081.png`
- `84dc8d637507`: `C:\KI_BRAIN\teacher_images\manual_84dc8d637507.png`
- `5bbd2b038007`: `C:\Users\Besitzer\AppData\Local\AuswertungPro\review_frames\20200609_3370-7400\frame_000085.png`
- `7f5f4be0c15e`: `C:\KI_BRAIN\teacher_images\manual_7f5f4be0c15e.png`

Freigabe bedeutet: Diese zehn Empfehlungen sind fachlich bestaetigt und auch die
restlichen 113 eindeutigen Tabellenzeilen wurden als korrekt geprueft.
