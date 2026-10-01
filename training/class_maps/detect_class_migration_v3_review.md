# Fachliche Freigabe der Detect-Migration v3

Stand: 2026-08-03

Die maschinenlesbare Tabelle enthaelt 143 Zeilen: 93 Teacher-Codes, 35 alte
Map-Schluessel, 10 produktive Modellnamen und 5 Einzelannotation. 74 Zeilen sind
`approved`, 69 bleiben `pending`. Die persoenliche Freigabe umfasst 73 im aktuellen
Gold-Audit beobachtete Quellcodes.

Durch `Besitzer` freigegeben sind 61 `map`- und 12 `discard`-Entscheidungen fuer
Teacher-Codes sowie eine Legacy-Zeile. Ein neuer oder anderer Code stoppt weiterhin
fail-closed.

## Aktueller Stand 2026-08-03

Der Besitzer hat die feste Goldpruefung mit je 15 Ausgangsfaellen fuer BAB, BAF,
BAI, BAJ, BBC und BBF vollstaendig abgeschlossen. Alle 90 Bilder besitzen einen
personenbezogenen Abschlussbeleg; zehn Ausgangscodes wurden dabei korrigiert.
Der anschliessende Audit `gold_stock_audit_20260803_191255_470.json` liefert
weiterhin 1353 verwendbare Goldinstanzen und bindet `training_samples.json` mit
SHA-256 `fd5340ce35d5b317273e9d34e340d70e319448c78c23d640ec682b94fb9c6a1b`.

Neu ist der fachlich bestaetigte VSA-Code `BBBZ` (Andersartige anhaftende Stoffe).
Er wird auf die feste Detect-Klasse `BBB_anhaftung` abgebildet. Die persoenliche
Freigabe bindet nun den neuen Audit, den aktuellen Sample-Hash und alle 73 darin
vorkommenden Codes. Das erneuerte Register enthaelt 894 Detect-Goldinstanzen und
9 strikt gepruefte Negativbilder; der Exportplan `ea8e715f3c4c...` enthaelt 852
Bilder mit 894 Boxen. Der produktive Modellzeiger bleibt davon unberuehrt.

Der neue Beleg `personal_gold_approval` bindet die Freigabe an:

- Gold-Audit SHA-256
  `5d036fd74dbdc6e80dae1ca2600b648fc99073f9b8a0157bee5da1a6027a0987`
- `training_samples.json` SHA-256
  `fd5340ce35d5b317273e9d34e340d70e319448c78c23d640ec682b94fb9c6a1b`
- die vollstaendige, sortierte Liste der 73 darin vorkommenden Codes
- Person und UTC-Zeitpunkt der Entscheidung

Beim ersten Mehrklassen-Beleg vom 2026-07-30 wurden von 521 audit-sauberen
Goldzeilen 392 Schaden-/Objektzeilen auf zwoelf Detect-Klassen abgebildet.
129 Zustandszeilen (`AED`, `BCD`, `BCE`, `BDA`, `BDD`) wurden vollstaendig aus
dem Detect-Export ausgeschlossen. Sie werden niemals als
leere Negativbilder umgedeutet. Nur getrennt mit `all_classes_clear` gepruefte
Negativbilder duerfen leere Labels erhalten.

Die v3-Tabelle richtet dieselbe Kandidatenmenge auf die aktive Karte v3 aus
(`BCC_bogen` mit fester ID 14). Die eingefrorene v2-Kandidatentabelle behaelt den
Stand vom 2026-07-16 (alle Zeilen `pending`) und wird nicht mehr veraendert.

## BCC-Pilot

- Detect-Klasse: `BCC_bogen`, feste ID 14.
- Umfang: nur persoenlich bestaetigte BCC-Goldbilder mit BBox und SAM-Maske.
- Das Exportregister nennt jede erlaubte Sample-ID einzeln.
- Das bestehende produktive Modell wird nicht ersetzt.
- Ohne Negativbilder ist das Ergebnis nur ein Lern- und Erkennungstest.

## Mehrklassen-Goldkandidat

Der Mehrklassenlauf ist ausschliesslich ein nicht aktivierter Trainingskandidat.
Aktuell sind 13 der 15 festen Klassen belegt; `BBD_boden` und `SONST_schaden`
besitzen keine Box. `BAH_schadanschluss` (8), `BBB_anhaftung` (22) und
`BBA_wurzeln` (27) liegen weiterhin unter 30 Beispielen. Der Lauf ist damit ein
echter Lernversuch, aber kein Release-Beweis und keine Modellfreigabe.

Die Goldbilder sind persoenlich mit Code, Box und SAM-Maske bestaetigt. Die
Hauptcode-Zuordnung ist eindeutig; optisch nahe Klassen wie Anschluss/schadhafter
Anschluss, Anhaftung/Ablagerung/Infiltration oder Verformung/Perspektive muessen
nach dem Training trotzdem getrennt auf einem menschlich geprueften
Mehrklassen-Holdout bewertet werden.

## Nachtrag 2026-08-01: Freigabeerneuerung auf gewachsenen Goldbestand

Der Goldbestand ist nach den PDF-Pruefsessions vom 2026-07-30/31 auf 1095 Samples
gewachsen (gebunden war 540). Der Besitzer hat am 2026-08-01 die Bindung von
`personal_gold_approval` auf den frischen Audit
`gold_stock_audit_20260801_134457_139.json` und die aktuelle
`training_samples.json` (SHA-256 `02aacb8f…`) erneuert. Dabei wurden 14 neue
Codes fachlich entschieden und freigegeben:

- map: BABAB/BABAE/BABBD → `BAB_riss`, BACA → `BAC_bruch`,
  BAFBZ/BAFDE/BAFEE/BAFKZ → `BAF_oberflaeche`, BAJ → `BAJ_verbindung`,
  BBAB/BBAC → `BBA_wurzeln` (erste Goldbeispiele dieser Klasse),
  BCAAB → `BCA_anschluss`
- discard: AEDXQ (Zustand Materialwechsel), BDDA (Wasserspiegel) —
  konsistent zu den bisherigen Zustandsausschluessen

Die fuenf zuvor eintragslosen Codes (BABAB, BABAE, BABBD, BACA, BAFBZ) wurden
als neue `teacher_vsa_code`-Zeilen ergaenzt (total 138 Zeilen, 88 davon
teacher_vsa_code). `BBA_wurzeln` besitzt nun 8 Goldinstanzen, bleibt aber wie
andere Klassen unter 30 Beispielen ein Lernkandidat ohne Release-Aussage.

Ergebnis des Wechsels: Exportregister ersetzt (alter Stand archiviert unter
`training/pilots/DETECT_ALL/registry_history/`), Datensatz-Plan `aed354bb…`
mit 650 Bildern (521 train / 129 val, 666 Instanzen, 13 Klassen mit Instanzen).
Trainierter Kandidat `detect_gold_aed354bb11fe` mit Status `not_deployed`;
das produktive Modell wurde nicht veraendert.

## Nachtrag 2026-08-01 (abends): Zweiter Lauf nach Pruefsession und Altbestand-Import

Der Goldbestand wuchs am selben Tag weiter auf 1360 Samples: 134 persoenliche
Samples aus dem Altbestand `gold_labels` (handgezeichnete Box+SAM-Maske,
VideoLabelTool, Import als `ManualCoding`/`ReviewApproved` nach fail-closed
Pruefung) und 131 neue PDF-Goldfotos aus der Pruefsession des Besitzers.
Der Besitzer erneuerte die Freigabe erneut: BABCA → `BAB_riss`,
BAFJE → `BAF_oberflaeche`, BBBC → `BBB_anhaftung`, BCADB → `BCA_anschluss`
sowie AEDXU → discard (gleiches Muster wie zuvor). Audit
`gold_stock_audit_20260801_164539_060.json`: 1279 verwendbar, 71 Codes.

Ergebnis: Register mit 854 Goldbildern (683/171), Datensatz-Plan `ffbb8612…`
(814 Bilder, 660/154, 854 Instanzen), Kandidat `detect_gold_ffbb8612fe50`
mit Status `not_deployed`. Validierung mAP50 = 0.242 (Lauf 1: 0.262);
BCC 0.82 und BAI 0.53 stabil, BAC erstmals messbar (0.246), BCA ruecklaeufig
(0.347). Weiterhin Lernkandidat, kein Release-Beweis; BAH und BBA bleiben
datenkritisch.

## Nachtrag 2026-08-02: reparierte Haltungen und neuer Trainingslauf

`repair_gold_holding_ids.py` hat fuer die betroffenen persoenlich bestaetigten
`foto_*`-Samples nur eindeutige, bytegleiche Quellen verwendet. Der Standardlauf
ist schreibfrei; der Ausfuehrungsweg sichert Gold-JSON, Teacher-JSON und SQLite und
veraendert keine Kundenbilder. Der danach erstellte Audit
`gold_stock_audit_20260802_205630_348.json` hat SHA-256
`bb7f01f6b3582029ad4393c7217e5c2bbbb4ed5770ab15c807a574972b4905ba`.
Er prueft 1391 Eintraege, ueberspringt 14 Drafts, verwirft 24 Kandidaten und
liefert 1353 verwendbare Instanzen im Split 961/264/128.

Die Freigabe wurde auf 72 Quellcodes erneuert. Neu hinzugekommen ist
`BAFCZ -> BAF_oberflaeche`. Damit besitzt die Migration 73 freigegebene und
69 offene Zeilen. Der gebundene `training_samples.json`-Snapshot hat SHA-256
`bfcb3362762dc552861feb0680f1267e086e8d7d3fb71d70e5806841b82daa83`.

`derive_negative_set_for_gold_audit.py` hat aus dem bisherigen Review einen neuen
unveraenderlichen Satz abgeleitet, ohne die Quelle zu aendern. Audit-Testhaltungen
und Splitkonflikte werden entfernt; bytegleiche Gold-/Negativbilder bleiben ein
harter Fehler. `bcc_hn_c25fd2f9d33f` enthaelt 9 Negative (7/2), Manifest-SHA-256
`518a341419b285da88ce674accfe7b0b41330f8cae736ef87a95ea9a48221772`.

Das neue Register nennt 898 Goldinstanzen (713 Train, 185 Validation) und die
9 Negative. Der Plan
`9eb020e303225109849cc3a4036cd33288ff0120efd1557a910484f4bd2a61f8`
enthaelt nach Bildzusammenfuehrung 856 Bilder (689/167), 898 Boxen und 13 belegte
von 15 festen Klassen. `detect_gold_9eb020e30322` hat 40/40 Epochen beendet und
bleibt `not_deployed`. Die interne Validation ergibt P 0,3917, R 0,3129,
mAP50 0,3026 und mAP50-95 0,1726. Gewicht-SHA-256 ist
`fdf30f77b6aa6271014d130248fde99089854bfc0e58b44d75d462b3b9172ebf`;
ohne unabhängigen Holdout ist dies keine Release-Freigabe.
