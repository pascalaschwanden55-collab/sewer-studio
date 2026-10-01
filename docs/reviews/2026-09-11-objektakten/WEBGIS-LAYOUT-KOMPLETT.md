# WebGIS Abwasser Uri – Gesamtlayout der Attributmasken

Stand 11.09.2026. Quelle: `https://www.geohost.ch/divum/synserver?project=awu_abw_edit` (GEONIS Attribute Editor, Projekt `awu_abw`). Gelesen aus den Maskendefinitionen des Servers (`getLayout`/`getLayouts`, `getControlValues`), nicht abgetippt.

Beschriftungen mit *(abgeleitet)* stehen so nicht im WebGIS: dort ist das Kästchen unbeschriftet und hängt an einer kombinierten Nachbarbeschriftung (`A/B`) oder an Spaltenköpfen. Der Originaltext steht dann in Klammern.

Feldart: text · mehrzeilig · zahl · datum · auswahl · verweis (Verknüpfung auf ein anderes Objekt). P = Pflicht, R = nur lesen. Auswahllisten: `Code` Text.

## Inhalt

1. [Haltung](#1-haltung) – Tabelle `AWK_HALTUNG`
2. [Schacht (Normschacht)](#2-schacht-normschacht) – Tabelle `AWK_ABWASSERKNOTEN`, Subtyp `art_bauwerk=1`
3. [Bauwerksteil](#3-bauwerksteil) – Tabelle `AWK_BAUWERKSTEIL`
4. [Haltungspunkt](#4-haltungspunkt) – Tabelle `AWK_HALTUNGSPUNKT`
5. [Strang](#5-strang) – Tabelle `AWK_STRANG`
6. [Deckel](#6-deckel) – Tabelle `AWK_DECKEL`
7. [Unterhalt – Erhaltungsereignis: Unbekannt](#7-unterhalt-erhaltungsereignis-unbekannt) – Tabelle `AWZ_UNTERHALT`, Subtyp `art=0`
8. [Unterhalt – Sanierungsmassnahme: Erneuerung](#8-unterhalt-sanierungsmassnahme-erneuerung) – Tabelle `AWZ_UNTERHALT`, Subtyp `art=1`
9. [Unterhalt – Sanierungsmassnahme: Reparatur](#9-unterhalt-sanierungsmassnahme-reparatur) – Tabelle `AWZ_UNTERHALT`, Subtyp `art=2`
10. [Unterhalt – Sanierungsmassnahme: Renovierung](#10-unterhalt-sanierungsmassnahme-renovierung) – Tabelle `AWZ_UNTERHALT`, Subtyp `art=4`
11. [Unterhalt – Dichtheitsprüfung: Dichtheitsprüfung](#11-unterhalt-dichtheitspr-fung-dichtheitspr-fung) – Tabelle `AWZ_UNTERHALT`, Subtyp `art=10`
12. [Unterhalt – Erhaltungsereignis: Reinigung](#12-unterhalt-erhaltungsereignis-reinigung) – Tabelle `AWZ_UNTERHALT`, Subtyp `art=3`
13. [Unterhalt – Erhaltungsereignis: Sanierung](#13-unterhalt-erhaltungsereignis-sanierung) – Tabelle `AWZ_UNTERHALT`, Subtyp `art=6`
14. [Unterhalt – Erhaltungsereignis: Andere](#14-unterhalt-erhaltungsereignis-andere) – Tabelle `AWZ_UNTERHALT`, Subtyp `art=7`
15. [Unterhalt – Erhaltungsereignis: Reinigung](#15-unterhalt-erhaltungsereignis-reinigung) – Tabelle `AWZ_UNTERHALT`, Subtyp `art=3`
16. [Unterhalt – Erhaltungsereignis: Andere](#16-unterhalt-erhaltungsereignis-andere) – Tabelle `AWZ_UNTERHALT`, Subtyp `art=7`
17. [Unterhalt – Erhaltungsereignis: Kanalfernsehen](#17-unterhalt-erhaltungsereignis-kanalfernsehen) – Tabelle `AWZ_UNTERHALT`, Subtyp `art=12`
18. [Unterhalt – Untersuchung: Untersuchung](#18-unterhalt-untersuchung-untersuchung) – Tabelle `AWZ_UNTERHALT`, Subtyp `art=5`
19. [Unterhalt – Begehung: Begehung](#19-unterhalt-begehung-begehung) – Tabelle `AWZ_UNTERHALT`, Subtyp `art=8`
20. [Unterhalt – Untersuchung: Deformationsmessung](#20-unterhalt-untersuchung-deformationsmessung) – Tabelle `AWZ_UNTERHALT`, Subtyp `art=9`
21. [Unterhalt – Untersuchung: Georadar](#21-unterhalt-untersuchung-georadar) – Tabelle `AWZ_UNTERHALT`, Subtyp `art=11`
22. [Inspektion Haltung](#22-inspektion-haltung) – Tabelle `AWZ_INSPEKTION_HALTUNG`
23. [Inspektion Schacht](#23-inspektion-schacht) – Tabelle `AWZ_INSPEKTION_KNOTEN`
24. [GEP-Massnahme](#24-gep-massnahme) – Tabelle `AWM_MASSNAHME`
25. [Einzugsgebiet](#25-einzugsgebiet) – Tabelle `AWH_EINZUGSGEBIET`
26. [Hydraulische Geometrie](#26-hydraulische-geometrie) – Tabelle `AWH_HYDR_GEOMETRIE`
27. [Mechanische Vorreinigung](#27-mechanische-vorreinigung) – Tabelle `AWK_VSA_MECHVORREINIGUNG`
28. [Förderaggregat (Pumpe)](#28-f-rderaggregat-pumpe) – Tabelle `AWK_ABWASSERKNOTEN`, Subtyp `art_bauwerk=12`
29. [Überlauf](#29-berlauf) – Tabelle `AWK_ABWASSERKNOTEN`, Subtyp `art_bauwerk=9`
30. [Absperr-/Drosselorgan](#30-absperr-drosselorgan) – Tabelle `AWK_ABWASSERKNOTEN`, Subtyp `art_bauwerk=8`


---

## 1. Haltung

Tabelle `AWK_HALTUNG`, Maskentitel im WebGIS: „Haltung“

95 Felder, davon 38 Auswahlfelder. Bereiche (Menüs) in Reihenfolge: **Kopf** → **Daten I** → **Daten II** → **Bauwerksteile** → **Haltungspunkte** → **Administrativ** → **Unterhalt** → **Hydraulik** → **Metadaten**

### Kopf

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Bezeichnung | text | ● |  |  |  |
| 2 | OBJECTID | text |  | ● |  |  |
| 3 | Bezeichnung alter./hist. | text |  |  |  |  |
| 4 | Bezeichnung alter./hist. – hist. *(abgeleitet; im WebGIS „“)* | text |  |  |  |  |
| 5 | Knoten (von) | verweis |  | ● |  | → `AWK_ABWASSERKNOTEN` [art_bauwerk]: [bezeichnung] |
| 6 | Knoten (bis) | verweis |  | ● |  | → `AWK_ABWASSERKNOTEN` [art_bauwerk]: [bezeichnung] |

### Daten I

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Typ AA/Nutzungsart | auswahl | ● |  |  | `0` Unbekannt · `1` PAA · `2` SAA |
| 2 | Typ AA/Nutzungsart – Nutzungsart *(abgeleitet; im WebGIS „“)* | auswahl | ● |  |  | `0` Unbekannt · `4` Mischabwasser · `5` Regenabwasser · `106` Reinabwasser · `7` Schmutzabwasser · `1` Bachabwasser · `102` Entlastetes Mischabwasser · `103` Industrieabwasser · `111` Andere · `1100` Bergwasser · `1101` Strassenabwasser |
| 3 | Funktion hier./Funktion hydr. | auswahl | ● |  |  | `0` Unbekannt · `5` Liegenschaftsentwässerung · `106` Sammelkanal · `3` Hauptsammelkanal · `2` Gewässer · `108` Strassenentwässerung · `104` Hauptsammelkanal, regional · `107` Sanierungsleitung · `14` Rinne · `115` Andere |
| 4 | Funktion hier./Funktion hydr. – Funktion hydr. *(abgeleitet; im WebGIS „“)* | auswahl | ● |  |  | `0` Unbekannt · `4` Freispiegelleitung · `5` Pumpendruckleitung · `101` Drainagetransportleitung · `102` Drosselleitung · `103` Dükerleitung · `106` Sickerleitung · `107` Speicherleitung · `108` Spülleitung · `111` Andere · `116` Vakuumleitung · `1100` Belagsrinne Wasserschale · `1101` Entwaesserungsgraben befestigt · `1102` Entwaesserungsgraben unbefestigt · `1103` Schlitzrinne · `1104` Wasserrinne mit Rost |
| 5 | Status | auswahl | ● |  |  | `0` Unbekannt · `1` In Betrieb · `2` Ausser Betrieb · `5` Tot/Aufgehoben, verfüllt |
| 6 | Baujahr *(abgeleitet; im WebGIS „“)* | zahl | ● |  | Integer |  |
| 7 | Material | auswahl | ● |  |  | `0` Unbekannt · `1` Beton · `2` Stahl · `3` Kunststoff · `4` Guss · `5` Andere |
| 8 | Material – 2. Feld *(abgeleitet; im WebGIS „“)* | auswahl | ● |  |  | abhängig von **Material**:<br>  - Elternwert `0`: `0` Unbekannt (U)<br>  - Elternwert `1`: `101` Beton, unbekannt (BU) · `102` Beton, armiert (BA) · `103` Beton, vorgespannt (BV) · `104` Beton, Fertigteil (BF) · `105` Beton, unarmiert (B) · `106` Ortsbeton (OB) · `107` Schleuderbeton (SBR) · `108` Spezialzement, armiert (SBR) · `109` Spezialzement, unarmiert (SBR) · `144` Normalbeton (NB) · `146` Pressrohrbeton (PRB) · `147` Spezialbeton (SB) · `1000` Schleuderbeton Pressvortrieb (SBR_PV) · `1003` Polymerbeton (PMB)<br>  - Elternwert `2`: `142` Stahl, unbekannt (ST) · `127` Stahl, nicht rostbeständig (ST) · `128` Stahl, rostbeständig (STI) · `148` Stahl, rostfrei (STI)<br>  - Elternwert `3`: `118` Kunststoff, unbekannt (KUU) · `117` GUP/GFK, Fertigteil (GUP) · `120` Polyester (P) · `121` Polyvinylchlorid (PVC) · `122` Polyvinylchlorid, hart (PVCH) · `123` Epoxidharz (EP) · `124` Polypropylen (PP) · `133` Polyethylen (PE) · `143` Hartpolyethylen (HPE) · `145` Polyester, ungesättigt (UP) · `1001` GFK (GFK)<br>  - Elternwert `4`: `113` Guss, unbekannt (GU) · `114` Grauguss (GG) · `115` Guss, duktil (GD) · `116` Gussbeton (GB)<br>  - Elternwert `5`: `131` Verschiedene (V) · `110` Faserzement (FZ) · `111` Asbestzement (AZ) · `112` Steine, gebrannt (SG) · `129` Steinzeug (STZ) · `130` Ton (T) · `132` Zement (Z) · `149` Andere (A) · `1002` KEY (KEY) |
| 9 | Profiltyp/Breite/Höhe | auswahl | ● |  |  | `0` Unbekannt (U) · `2` Kreisprofil (K) · `101` Eiprofil (E) · `103` Maulprofil (E) · `104` Offenes Profil (OP) · `105` Rechteckprofil (R) · `106` Spezialprofil (S) · `107` Andere (A) |
| 10 | [mm] | auswahl | ● |  |  | `0` Unbekannt · `40` 40 · `45` 45 · `50` 50 · `60` 60 · `63` 63 · `65` 65 · `70` 70 · `75` 75 · `80` 80 · `85` 85 · `90` 90 · `95` 95 · `100` 100 · `102` 102 · `103` 103 · `104` 104 · `110` 110 · `115` 115 · `117` 117 · `118` 118 · `119` 119 · `120` 120 · `121` 121 · `125` 125 · `132` 132 · `140` 140 · `148` 148 · `149` 149 · `150` 150 · `153` 153 · `154` 154 · `155` 155 · `160` 160 · `166` 166 · `170` 170 · `175` 175 · `180` 180 · `185` 185 · `186` 186 · `188` 188 · `191` 191 · `192` 192 · `200` 200 · `210` 210 · `211` 211 · `220` 220 · `225` 225 · `229` 229 · `231` 231 · `233` 233 · `235` 235 · `239` 239 · `240` 240 · `242` 242 · `250` 250 · `263` 263 · `270` 270 · `280` 280 · `284` 284 · `291` 291 · `296` 296 · `298` 298 · `300` 300 · `301` 301 · `303` 303 · `315` 315 · `320` 320 · `328` 328 · `333` 333 · `341` 341 · `350` 350 · `352` 352 · `355` 355 · `369` 369 · `375` 375 · `380` 380 · `384` 384 · `400` 400 · `406` 406 · `435` 435 · `440` 440 · `450` 450 · `464` 464 · `466` 466 · `480` 480 · `500` 500 · `550` 550 · `560` 560 · `580` 580 · `591` 591 · `600` 600 · `605` 605 · `610` 610 · `620` 620 · `630` 630 · `650` 650 · `670` 670 · `680` 680 · `700` 700 · `720` 720 · `730` 730 · `750` 750 · `760` 760 · `780` 780 · `800` 800 · `820` 820 · `850` 850 · `880` 880 · `900` 900 · `950` 950 · `990` 990 · `1000` 1000 · `1030` 1030 · `1050` 1050 · `1070` 1070 · `1100` 1100 · `1150` 1150 · `1180` 1180 · `1200` 1200 · `1210` 1210 · `1220` 1220 · `1240` 1240 · `1250` 1250 · `1260` 1260 · `1280` 1280 · `1300` 1300 · `1320` 1320 · `1350` 1350 · `1380` 1380 · `1400` 1400 · `1420` 1420 · `1450` 1450 · `1460` 1460 · `1470` 1470 · `1480` 1480 · `1490` 1490 · `1500` 1500 · `1550` 1550 · `1600` 1600 · `1610` 1610 · `1650` 1650 · `1660` 1660 · `1670` 1670 · `1690` 1690 · `1700` 1700 · `1720` 1720 · `1750` 1750 · `1760` 1760 · `1780` 1780 · `1790` 1790 · `1800` 1800 · `1830` 1830 · `1850` 1850 · `1870` 1870 · `1900` 1900 · `1950` 1950 · `1980` 1980 · `2000` 2000 · `2010` 2010 · `2050` 2050 · `2100` 2100 · `2200` 2200 · `2240` 2240 · `2250` 2250 · `2280` 2280 · `2300` 2300 · `2310` 2310 · `2320` 2320 · `2350` 2350 · `2400` 2400 · `2500` 2500 · `2600` 2600 · `2700` 2700 · `2800` 2800 · `3000` 3000 · `3150` 3150 · `3200` 3200 · `3250` 3250 · `3500` 3500 · `3600` 3600 · `4000` 4000 · `4500` 4500 · `4600` 4600 · `5000` 5000 · `5250` 5250 · `5300` 5300 |
| 11 | [mm] – 2. Feld *(abgeleitet; im WebGIS „“)* | auswahl | ● |  |  | `0` Unbekannt · `40` 40 · `45` 45 · `50` 50 · `60` 60 · `63` 63 · `65` 65 · `70` 70 · `75` 75 · `80` 80 · `85` 85 · `90` 90 · `95` 95 · `100` 100 · `102` 102 · `103` 103 · `104` 104 · `110` 110 · `115` 115 · `117` 117 · `118` 118 · `119` 119 · `120` 120 · `121` 121 · `125` 125 · `132` 132 · `140` 140 · `148` 148 · `149` 149 · `150` 150 · `153` 153 · `154` 154 · `155` 155 · `160` 160 · `166` 166 · `170` 170 · `175` 175 · `180` 180 · `185` 185 · `186` 186 · `188` 188 · `191` 191 · `192` 192 · `200` 200 · `210` 210 · `211` 211 · `220` 220 · `225` 225 · `229` 229 · `231` 231 · `233` 233 · `235` 235 · `239` 239 · `240` 240 · `242` 242 · `250` 250 · `263` 263 · `270` 270 · `280` 280 · `284` 284 · `291` 291 · `296` 296 · `298` 298 · `300` 300 · `301` 301 · `303` 303 · `315` 315 · `320` 320 · `328` 328 · `333` 333 · `341` 341 · `350` 350 · `352` 352 · `355` 355 · `369` 369 · `375` 375 · `380` 380 · `384` 384 · `400` 400 · `406` 406 · `435` 435 · `440` 440 · `450` 450 · `464` 464 · `466` 466 · `480` 480 · `500` 500 · `550` 550 · `560` 560 · `580` 580 · `591` 591 · `600` 600 · `605` 605 · `610` 610 · `620` 620 · `630` 630 · `650` 650 · `670` 670 · `680` 680 · `700` 700 · `720` 720 · `730` 730 · `750` 750 · `760` 760 · `780` 780 · `800` 800 · `820` 820 · `850` 850 · `880` 880 · `900` 900 · `950` 950 · `990` 990 · `1000` 1000 · `1030` 1030 · `1050` 1050 · `1070` 1070 · `1100` 1100 · `1150` 1150 · `1180` 1180 · `1200` 1200 · `1210` 1210 · `1220` 1220 · `1240` 1240 · `1250` 1250 · `1260` 1260 · `1280` 1280 · `1300` 1300 · `1320` 1320 · `1350` 1350 · `1380` 1380 · `1400` 1400 · `1420` 1420 · `1450` 1450 · `1460` 1460 · `1470` 1470 · `1480` 1480 · `1490` 1490 · `1500` 1500 · `1550` 1550 · `1600` 1600 · `1610` 1610 · `1650` 1650 · `1660` 1660 · `1670` 1670 · `1690` 1690 · `1700` 1700 · `1720` 1720 · `1750` 1750 · `1760` 1760 · `1780` 1780 · `1790` 1790 · `1800` 1800 · `1830` 1830 · `1850` 1850 · `1870` 1870 · `1900` 1900 · `1950` 1950 · `1980` 1980 · `2000` 2000 · `2010` 2010 · `2050` 2050 · `2100` 2100 · `2200` 2200 · `2240` 2240 · `2250` 2250 · `2280` 2280 · `2300` 2300 · `2310` 2310 · `2320` 2320 · `2350` 2350 · `2400` 2400 · `2500` 2500 · `2600` 2600 · `2700` 2700 · `2800` 2800 · `3000` 3000 · `3150` 3150 · `3200` 3200 · `3250` 3250 · `3500` 3500 · `3600` 3600 · `4000` 4000 · `4500` 4500 · `4600` 4600 · `5000` 5000 · `5250` 5250 · `5300` 5300 |
| 12 | Rohrprofil | auswahl | ● |  |  | `20ad3936-b086-407e-92ce-9cf7123c7c2d` Unbekannt: unbekannt () · `cc671ca0-c9af-4248-bbc0-3c69d9072031` Kreisprofil: Kreisprofil 1.00 (1) · `4fa990aa-c176-446f-8411-494f74eb5327` Eiprofil: Eiprofil 1.50 (1.5) · `49963e95-cb67-40bc-a1a3-7a5b34362aee` Maulprofil: Maulprofil () · `25f2319b-d530-4125-9afa-bdbccebf9747` Offenes Profil: offenes_Profil 1.00 (1) · `a014950b-fece-495f-8658-a3ed0a333522` Offenes Profil: offenes_Profil 0.50 (0.5) · `9cd65501-3fcb-45c8-8d61-bf1e471ce42a` Offenes Profil: offenes_Profil 0.67 (0.67) · `f88d5599-b0be-4d98-a969-ded0b51e8d36` Offenes Profil: offenes_Profil 0.83 (0.83) · `dc7657c1-907c-46b1-a51e-d173d352d2a5` Offenes Profil: offenes_Profil () · `f1d42b1c-d4d2-445f-b208-972167441bcc` Offenes Profil: offenes_Profil 5.00 (5) · `f7f4e466-c509-4ce0-8557-bdc74eb76b26` Offenes Profil: offenes_Profil 6.25 (6.25) · `a7a90540-1df2-4061-a1bc-e3d22bc595d7` Offenes Profil: offenes_Profil 1.30 (1.3) · `bedce3e3-aaa5-4860-87b1-c054d78dc3c3` Offenes Profil: offenes_Profil 1.74 (1.74) · `70e79690-13d1-4042-95cd-ed0324599a3b` Offenes Profil: offenes_Profil 1.10 (1.1) · `c365df6d-41da-4dad-bb6f-6517fc4c4684` Offenes Profil: offenes_Profil 0.88 (0.88) · `b9323960-82db-46c8-9a11-2c6a64d3caa3` Rechteckprofil: Rechteckprofil 2.33 (2.33) · `ce91a78c-c4e5-4221-9cf6-8016dff81c5b` Rechteckprofil: Rechteckprofil 1.00 (1) · `6650e7ca-f55f-4d2d-8d72-395b0e3bdaa7` Rechteckprofil: Rechteckprofil 2.00 (2) · `3ff80a83-6f7b-4f2c-b5da-0fcc30e0cde7` Rechteckprofil: Rechteckprofil 0.10 (0.1) · `4985cd32-3877-48ae-ab1c-7a015da0d56d` Rechteckprofil: Rechteckprofil 0.70 (0.7) · `17bf48c5-3878-4e83-af40-d89e05a24024` Rechteckprofil: Rechteckprofil 0.83 (0.83) · `41a3bcaa-e0bd-4d1f-a43c-f4f41ff4a635` Rechteckprofil: Rechteckprofil 0.63 (0.63) · `6850ad53-afcb-42ff-aa7b-956578933d31` Rechteckprofil: Rechteckprofil 0.90 (0.9) · `d6157433-cdc4-4804-b0a8-883c4b77fa4d` Rechteckprofil: Rechteckprofil 0.67 (0.67) · `1b970dac-09ed-4046-9dc0-dca40bb0ba72` Rechteckprofil: Rechteckprofil 6.25 (6.25) · `c2cfb0ec-95db-44f9-9be5-e0ea460bd96f` Rechteckprofil: Rechteckprofil 5.00 (5) · `a30183bc-f4d2-4267-a281-14c675f4938a` Rechteckprofil: Rechteckprofil 1.60 (1.6) · `1295dd77-b745-4a4e-baf5-dba167c13506` Rechteckprofil: Rechteckprofil 1.33 (1.33) · `6289ccb5-4a39-4950-a371-b88624fc7434` Rechteckprofil: Rechteckprofil 0.75 (0.75) · `c86ce1d4-e411-4d43-9168-1547cb708f6a` Rechteckprofil: Rechteckprofil 1.25 (1.25) · `91dc6623-830b-4bf9-b739-f9eea457c5eb` Rechteckprofil: Rechteckprofil 1.20 (1.2) · `86a9ec31-4644-4eeb-a02f-289be120aee7` Rechteckprofil: Rechteckprofil 0.96 (0.96) · `0629916f-145c-4812-a231-3c95649e3e41` Rechteckprofil: Rechteckprofil 0.59 (0.59) · `bd2ed8b6-df92-48de-a373-c0d0dad53770` Rechteckprofil: Rechteckprofil 0.43 (0.43) · `d9385b97-9965-42a6-8907-71b6b9990410` Rechteckprofil: Rechteckprofil 2.50 (2.5) · `704b8285-0872-4df6-8c12-0154cb745e05` Rechteckprofil: Rechteckprofil 0.57 (0.57) · `66c4f31c-0fb0-4f0a-b6e5-0a3737f86adb` Rechteckprofil: Rechteckprofil 3.33 (3.33) · `8f50b003-3786-44fc-acde-e942f3691cc5` Rechteckprofil: Rechteckprofil 1.22 (1.22) · `63ecc68d-58cb-46eb-9b6d-004c2c9b2a15` Rechteckprofil: Rechteckprofil 0.20 (0.2) · `a8bd2e2e-0925-46d1-a4c8-1a0e15662986` Rechteckprofil: Rechteckprofil 3.50 (3.5) · `9cbd44de-7453-4431-b4df-0d46ff196748` Rechteckprofil: Rechteckprofil 0.50 (0.5) · `27a2bf94-6848-462b-a6c0-605c6350f123` Rechteckprofil: Rechteckprofil () · `baecfa54-fb8b-429a-b889-8b244199e16d` Spezialprofil: Spezialprofil 1.20 (1.2) · `23009dc1-bc72-49f6-b489-d81cec52bfb1` Spezialprofil: Spezialprofil 0.50 (0.5) · `cb196989-0f4d-4c2b-bd9a-5fa96d45d253` Spezialprofil: Spezialprofil 0.24 (0.24) · `9e8438f4-e3e6-43c3-ad67-d7da6fe84c6c` Spezialprofil: Spezialprofil 1.00 (1) · `e17e95db-5ee1-4736-868c-8db0b99438d5` Spezialprofil: Spezialprofil 0.13 (0.13) · `193847aa-3dcf-45c1-b7e4-f335decd8df5` Spezialprofil: Spezialprofil 0.23 (0.23) · `fa01b6ec-9dfb-440e-92c4-d84e1ea60cbd` Spezialprofil: Spezialprofil 0.14 (0.14) · `7f97379f-b410-4d3c-9528-53d3e780f2a2` Spezialprofil: Spezialprofil 0.33 (0.33) · `ea4ba290-728f-42b9-a475-4586824e29b4` Spezialprofil: Spezialprofil 0.12 (0.12) · `a72a6751-6bc2-4bc3-ad7e-91e11a5c90ae` Spezialprofil: Spezialprofil 0.27 (0.27) · `22263faf-1008-49cd-ad29-08409777efbe` Spezialprofil: Spezialprofil 0.86 (0.86) · `5c042f62-691d-48b8-bf61-f8e3d2ba7076` Spezialprofil: Spezialprofil 1.10 (1.1) · `eafa0fd9-e618-4270-adf4-69196c2119fa` Spezialprofil: Spezialprofil () · `ba651cbf-e647-47ca-ae04-9f7c11b549fa` Andere: andere 0.88 (0.88) |
| 13 | Ringsteifigkeit [kN/m²] | text |  |  |  |  |
| 14 | Anfangs-/Endhöhe | zahl |  |  | Float |  |
| 15 | Anfangs-/Endhöhe – Endhöhe *(abgeleitet; im WebGIS „“)* | zahl |  |  | Float |  |
| 16 | -nr. | text |  |  |  |  |
| 17 | -nr. – 2. Feld *(abgeleitet; im WebGIS „“)* | text |  |  |  |  |
| 18 | Länge geom./eff. [m] | zahl |  | ● | Float |  |
| 19 | Länge geom./eff. [m] – eff. [m] *(abgeleitet; im WebGIS „“)* | zahl |  | ● | Float |  |
| 20 | Gefälle [‰] *(abgeleitet; im WebGIS „“)* | zahl |  | ● | Float |  |
| 21 | Plangefälle [‰] *(abgeleitet; im WebGIS „“)* | zahl |  |  | Float |  |
| 22 | Auslaufform VP/NP | auswahl |  |  |  | `0` Unbekannt · `101` Abgerundet · `102` Blendenförmig · `103` Keine Querschnittsänderung · `104` Scharfkantig |
| 23 | Auslaufform VP/NP – NP *(abgeleitet; im WebGIS „“)* | auswahl |  |  |  | `0` Unbekannt · `101` Abgerundet · `102` Blendenförmig · `103` Keine Querschnittsänderung · `104` Scharfkantig |
| 24 | Höhengenauigkeit VP/NP | auswahl |  |  |  | `0` Unbekannt · `101` > 6 cm · `102` +/- 1 cm · `103` +/- 3 cm · `104` +/- 6 cm |
| 25 | Höhengenauigkeit VP/NP – NP *(abgeleitet; im WebGIS „“)* | auswahl |  |  |  | `0` Unbekannt · `101` > 6 cm · `102` +/- 1 cm · `103` +/- 3 cm · `104` +/- 6 cm |
| 26 | Lageanschluss (Zifferblatt) VP/NP | text |  |  |  |  |
| 27 | Lageanschluss (Zifferblatt) VP/NP – NP *(abgeleitet; im WebGIS „“)* | text |  |  |  |  |
| 28 | Ebene | auswahl |  |  |  | `0` Ebene 0 · `1` Ebene 1 · `2` Ebene -1 · `3` Ebene 2 · `4` Ebene -2 · `5` Ebene 3 · `6` Ebene -3 · `7` Ebene 4 · `8` Ebene -4 · `9` Ebene 5 · `10` Ebene -5 · `11` Ebene 6 · `12` Ebene -6 · `13` Ebene 7 · `14` Ebene -7 · `15` Ebene 8 · `16` Ebene -8 · `17` Ebene 9 · `18` Ebene -9 · `19` Ebene 10 · `20` Ebene -10 |
| 29 | Bemerkung | mehrzeilig |  |  |  |  |

### Daten II

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Lagebest./-genauigkeit | auswahl | ● |  |  | `0` Unbekannt · `101` Ungenau · `102` Genau |
| 2 | Lagebest./-genauigkeit – -genauigkeit *(abgeleitet; im WebGIS „“)* | auswahl | ● |  |  | `0` Unbekannt · `101` > 50 cm · `102` +/- 10 cm · `103` +/- 3 cm · `104` +/- 50 cm |
| 3 | Höhenbest./-genauigkeit | auswahl | ● |  |  | `0` Unbekannt · `101` Ungenau · `102` Genau |
| 4 | Höhenbest./-genauigkeit – -genauigkeit *(abgeleitet; im WebGIS „“)* | auswahl | ● |  |  | `0` Unbekannt · `101` > 6 cm · `102` +/- 1 cm · `103` +/- 3 cm · `104` +/- 6 cm |
| 5 | Kanal-Referenz | text |  |  |  |  |
| 6 | VP-REF | text |  |  |  |  |
| 7 | NP-REF | text |  |  |  |  |
| 8 | Reibungsbeiwert [m^(1/3)/s] | text |  |  |  |  |
| 9 | Wandrauhigkeit [mm] *(abgeleitet; im WebGIS „“)* | text |  |  |  |  |
| 10 | Innenschutz/Umhüllung | auswahl |  |  |  | `0` Unbekannt · `101` Anstrich/Beschichtung · `102` Kanalklinkerauskleidung · `103` Steinzeugauskleidung · `104` Zementmörtelauskleidung · `105` Andere |
| 11 | Innenschutz/Umhüllung – Umhüllung *(abgeleitet; im WebGIS „“)* | auswahl |  |  |  | `0` Unbekannt · `101` Erdverlegt · `102` In Kanal aufgehängt · `103` In Kanal einbetoniert · `104` In Vortriebsrohr Beton · `105` In Vortriebsrohr Stahl · `106` In Leitungsgang · `107` Sand · `108` SIA Typ1 · `109` SIA Typ2 · `110` SIA Typ3 · `111` SIA Typ4 · `112` Kies · `113` In Kulisse · `114` Pressvortrieb · `116` Sohlbrett · `115` Andere |
| 12 | Verbindungsart | auswahl |  |  |  | `0` Unbekannt · `101` Elektroschweissmuffen · `102` Flachmuffen · `103` Flansch · `104` Glockenmuffen · `105` Kupplung · `106` Schraubmuffen · `107` Spiegelgeschweisst · `108` Spitzmuffen · `109` Steckmuffen · `110` Überschiebmuffen · `112` Vortriebsrohrkupplung · `113` Andere · `1000` Stumpfschweissmuffe · `1001` Führungsbolzen · `1002` Manschette einbetoniert · `1003` Schweissmuffen · `1004` Stahlmuffen |
| 13 | Geplante Nutzungsart | auswahl |  |  |  | `0` Unbekannt · `4` Mischabwasser · `5` Regenabwasser · `106` Reinabwasser · `7` Schmutzabwasser · `1` Bachabwasser · `102` Entlastetes Mischabwasser · `103` Industrieabwasser · `111` Andere · `1100` Bergwasser · `1101` Strassenabwasser |
| 14 | Rohr-/Haltungslänge [m] | zahl |  |  | Float |  |
| 15 | Rohr-/Haltungslänge [m] – Haltungslänge [m] *(abgeleitet; im WebGIS „“)* | zahl |  | ● | Float |  |
| 16 | Ringsteifigkeit | auswahl |  |  |  | `1000` 0 · `1001` 1 · `1002` 2 · `1003` 3 · `1004` 4 · `1005` 5 · `1006` 6 · `1007` 7 · `1008` 8 · `1009` 9 · `1010` 10 · `1011` 11 · `1012` 12 · `1013` 13 · `1014` 14 · `1015` 15 · `1016` 16 |
| 17 | Rohrserie | auswahl |  |  |  | `1000` unbekannt · `1001` 5 · `1002` 8 · `1003` 12.5 · `1004` 16 |

### Bauwerksteile

**Aufklappliste „Bauwerksteile“** (Untermenü) – Tabelle `AWK_BAUWERKSTEIL`, bearbeitbar, mehrere Zeilen; Spalten: Bezeichnung · Art · Subart; Detailmaske: **Bauwerksteil**

**Aufklappliste „Einläufe“** (Untermenü) – Tabelle `AWK_ABWASSERKNOTEN`, nur lesend; Spalten: Bezeichnung · Art · Höhe · Distanz; Detailmaske: **Schacht (Normschacht) – die anschliessenden Knoten, nur lesend**

### Haltungspunkte

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Haltungspunkt (von) | verweis |  |  |  | → `AWK_HALTUNGSPUNKT` [bezeichnung]: [kote] |
| 2 | Haltungspunkt (bis) | verweis |  |  |  | → `AWK_HALTUNGSPUNKT` [bezeichnung]: [kote] |

### Administrativ

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Eigentümer | auswahl | ● |  |  | `7747a36a-6420-41e2-bbe8-64ff28a7849d` unbekannt (Unbekannt) · `7692de6b-dce2-485c-8f01-190419e4c32f` Privat (Privat) · `4c18a435-f416-4814-a9b4-b68d98189cc4` Bund Astra (Bund) · `58d1c876-3d16-47da-8b38-b509bbc0cbca` Kanton Uri (Kanton) · `df1f763b-7f01-4d4d-a22c-14476c7a3a9b` Bund (Bund) · `f8186a5d-399a-443c-921f-460dff3d0566` oeff_Rechtl_Koerperschaften (Genossenschaft/Kooperation) · `afd5affb-1ce7-49d5-af42-c7e52641dc58` AWU_von_oeffentlich (Abwasserverband) · `86dd0b68-c510-4926-90eb-c5832e4663c4` AWU_von_privat (Abwasserverband) · `54a8d062-7796-4eee-b4d5-60a9d1084f9d` RUAG (Privat) · `357bfc90-bf4b-4e5d-87d6-277a76eefbbd` Meliorationsgen. Reussebene Uri (Genossenschaft/Kooperation) · `cbb47e18-cbc0-48cc-98ea-c37cc56622f9` Meliorationsgen. Seedorf (Genossenschaft/Kooperation) · `16853e1b-1f90-49e6-b762-7ec9ebd7bb59` Altdorf (Gemeinde) · `7c74fab1-4121-4cb9-bcfd-00eea3b4c204` Bürglen (Gemeinde) · `9f6939a7-ba0a-47c6-9c63-9c44cddfddf3` Schattdorf (Gemeinde) · `a4efbb6e-f8a6-4c65-a9df-be9ff1356dea` Andermatt (Gemeinde) · `65f14d2d-8f4e-45de-9635-ae76774e61db` Attinghausen (Gemeinde) · `07e5db4a-ecd0-4c23-bd5d-e144ec68d28e` Erstfeld (Gemeinde) · `6108523b-a8f3-42f9-bf88-1492bdea77d1` Flüelen (Gemeinde) · `1035c742-cb3c-44d5-a7c4-ee3db1ce7a96` Göschenen (Gemeinde) · `c929ee69-5218-4366-9691-a8662f134fe4` Gurtnellen (Gemeinde) · `07e608b7-d1c9-4700-99c6-e0cb65b1b4fd` Hospental (Gemeinde) · `d1647b72-5b4b-4940-af16-1f5325b97ee8` Isenthal (Gemeinde) · `ff2b72c6-c8d8-4a52-b3a0-1ab7810c5c69` Realp (Gemeinde) · `9a2739a5-dd1d-401d-b060-0c37a7b75362` Seedorf (Gemeinde) · `52da3b7e-760d-4659-87fd-a23c977d0941` Seelisberg (Gemeinde) · `6b3fb54d-d3a4-41bd-91e0-542e9ecd4e8e` Silenen (Gemeinde) · `db0e9992-5a10-4c5a-853d-a1e9637042cb` Sisikon (Gemeinde) · `43c22364-adb7-40c2-8759-0dbebf879de0` Spiringen (Gemeinde) · `289c8fc2-ee43-4da2-bb81-6592880080b7` Unterschächen (Gemeinde) · `95844aa7-7306-4cea-88f2-4629a4792c4c` Wassen (Gemeinde) |
| 2 | Betreiber | auswahl | ● |  |  | `7747a36a-6420-41e2-bbe8-64ff28a7849d` unbekannt (Unbekannt) · `7692de6b-dce2-485c-8f01-190419e4c32f` Privat (Privat) · `4c18a435-f416-4814-a9b4-b68d98189cc4` Bund Astra (Bund) · `58d1c876-3d16-47da-8b38-b509bbc0cbca` Kanton Uri (Kanton) · `df1f763b-7f01-4d4d-a22c-14476c7a3a9b` Bund (Bund) · `f8186a5d-399a-443c-921f-460dff3d0566` oeff_Rechtl_Koerperschaften (Genossenschaft/Kooperation) · `afd5affb-1ce7-49d5-af42-c7e52641dc58` AWU_von_oeffentlich (Abwasserverband) · `86dd0b68-c510-4926-90eb-c5832e4663c4` AWU_von_privat (Abwasserverband) · `54a8d062-7796-4eee-b4d5-60a9d1084f9d` RUAG (Privat) · `357bfc90-bf4b-4e5d-87d6-277a76eefbbd` Meliorationsgen. Reussebene Uri (Genossenschaft/Kooperation) · `cbb47e18-cbc0-48cc-98ea-c37cc56622f9` Meliorationsgen. Seedorf (Genossenschaft/Kooperation) · `16853e1b-1f90-49e6-b762-7ec9ebd7bb59` Altdorf (Gemeinde) · `7c74fab1-4121-4cb9-bcfd-00eea3b4c204` Bürglen (Gemeinde) · `9f6939a7-ba0a-47c6-9c63-9c44cddfddf3` Schattdorf (Gemeinde) · `a4efbb6e-f8a6-4c65-a9df-be9ff1356dea` Andermatt (Gemeinde) · `65f14d2d-8f4e-45de-9635-ae76774e61db` Attinghausen (Gemeinde) · `07e5db4a-ecd0-4c23-bd5d-e144ec68d28e` Erstfeld (Gemeinde) · `6108523b-a8f3-42f9-bf88-1492bdea77d1` Flüelen (Gemeinde) · `1035c742-cb3c-44d5-a7c4-ee3db1ce7a96` Göschenen (Gemeinde) · `c929ee69-5218-4366-9691-a8662f134fe4` Gurtnellen (Gemeinde) · `07e608b7-d1c9-4700-99c6-e0cb65b1b4fd` Hospental (Gemeinde) · `d1647b72-5b4b-4940-af16-1f5325b97ee8` Isenthal (Gemeinde) · `ff2b72c6-c8d8-4a52-b3a0-1ab7810c5c69` Realp (Gemeinde) · `9a2739a5-dd1d-401d-b060-0c37a7b75362` Seedorf (Gemeinde) · `52da3b7e-760d-4659-87fd-a23c977d0941` Seelisberg (Gemeinde) · `6b3fb54d-d3a4-41bd-91e0-542e9ecd4e8e` Silenen (Gemeinde) · `db0e9992-5a10-4c5a-853d-a1e9637042cb` Sisikon (Gemeinde) · `43c22364-adb7-40c2-8759-0dbebf879de0` Spiringen (Gemeinde) · `289c8fc2-ee43-4da2-bb81-6592880080b7` Unterschächen (Gemeinde) · `95844aa7-7306-4cea-88f2-4629a4792c4c` Wassen (Gemeinde) |
| 3 | Strang-ID/Bezeichnung | verweis |  |  |  | → `AWK_STRANG` [bezeichnung] |
| 4 | Standort | mehrzeilig |  |  |  |  |
| 5 | Ort/Zugänglichkeit | auswahl |  |  |  |  |
| 6 | Ort/Zugänglichkeit – Zugänglichkeit *(abgeleitet; im WebGIS „“)* | auswahl |  |  |  | `0` Unbekannt · `101` Überdeckt · `102` Unzugänglich · `103` Zugänglich |
| 7 | Baulos | text |  |  |  |  |
| 8 | Bruttokosten/Subventionen | text |  |  |  |  |
| 9 | Bruttokosten/Subventionen – Subventionen *(abgeleitet; im WebGIS „“)* | text |  |  |  |  |
| 10 | Baujahr/Ersatzjahr | zahl |  | ● | Integer |  |
| 11 | Baujahr/Ersatzjahr – Ersatzjahr *(abgeleitet; im WebGIS „“)* | zahl |  |  | Integer |  |
| 12 | Finanzierung | auswahl |  |  |  | `0` Unbekannt · `1` Öffentlich · `2` Privat |
| 13 | Wiederbeschaffungswert | text |  |  |  |  |
| 14 | Wiederbeschaffungswert Basisjahr | zahl |  |  | Integer |  |
| 15 | Wiederbeschaffungswert Bauart | auswahl |  |  |  | `0` Unbekannt · `1` Feld · `2` Sanierungsleitung: Bagger · `3` Sanierungsleitung: Grabenfräse · `4` Strasse · `5` Andere |
| 16 | Akten | mehrzeilig |  |  |  |  |

### Unterhalt

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Zustand/Sanierungsbedarf | auswahl |  |  |  | `0` Unbekannt · `100` Nicht mehr funktionstüchtig (Z0) · `101` Starke Mängel (Z1) · `102` Mittlere Mängel (Z2) · `103` Leichte Mängel (Z3) · `104` Keine Mängel (Z4) · `1000` nicht beurteilt |
| 2 | Zustand/Sanierungsbedarf – Sanierungsbedarf *(abgeleitet; im WebGIS „“)* | auswahl |  |  |  | `0` Unbekannt · `101` Dringend · `102` Kurzfristig · `103` Mittelfristig · `104` Langfristig · `105` Keiner · `106` Saniert |
| 3 | Erhebungsjahr [Zustand] | zahl |  |  | Integer |  |
| 4 | Inspektionsintervall [Jahr] | zahl |  |  | Float |  |
| 5 | Spülintervall [Jahr] | zahl |  |  | Float |  |

**Aufklappliste „Unterhaltsmassnahmen“** (Untermenü) – Tabelle `AWZ_UNTERHALT`, bearbeitbar, mehrere Zeilen; Spalten: Zeitpunkt · Art · Status · Ausführender; Detailmaske: **Unterhalt – Erhaltungsereignis: ***

**Aufklappliste „Sanierungsmassnahmen“** (Untermenü) – Tabelle `AWZ_UNTERHALT`, bearbeitbar, mehrere Zeilen; Spalten: Beginn · Art · Status · Verfahren; Detailmaske: **Unterhalt – Sanierungsmassnahme: ***

**Aufklappliste „Dichtheitsprüfungen“** (Untermenü) – Tabelle `AWZ_UNTERHALT`, bearbeitbar, mehrere Zeilen; Spalten: Datum · Art · Status · Verfahren; Detailmaske: **Unterhalt – Dichtheitsprüfung: Dichtheitsprüfung**

**Aufklappliste „Inspektionen“** (Untermenü) – Tabelle `AWZ_INSPEKTION_HALTUNG`, bearbeitbar, mehrere Zeilen; Spalten: Datum · Verfahren · Richtung · Nummer; Detailmaske: **Inspektion Haltung**

**Aufklappliste „GEP Massnahmen“** (Untermenü) – Tabelle `AWM_MASSNAHME`, bearbeitbar, mehrere Zeilen; Spalten: Bezeichnung · Beschreibung; Detailmaske: **GEP-Massnahme**

### Hydraulik

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Rohrprofil | auswahl |  | ● |  | `20ad3936-b086-407e-92ce-9cf7123c7c2d` Unbekannt: unbekannt () · `cc671ca0-c9af-4248-bbc0-3c69d9072031` Kreisprofil: Kreisprofil 1.00 (1) · `4fa990aa-c176-446f-8411-494f74eb5327` Eiprofil: Eiprofil 1.50 (1.5) · `49963e95-cb67-40bc-a1a3-7a5b34362aee` Maulprofil: Maulprofil () · `25f2319b-d530-4125-9afa-bdbccebf9747` Offenes Profil: offenes_Profil 1.00 (1) · `a014950b-fece-495f-8658-a3ed0a333522` Offenes Profil: offenes_Profil 0.50 (0.5) · `9cd65501-3fcb-45c8-8d61-bf1e471ce42a` Offenes Profil: offenes_Profil 0.67 (0.67) · `f88d5599-b0be-4d98-a969-ded0b51e8d36` Offenes Profil: offenes_Profil 0.83 (0.83) · `dc7657c1-907c-46b1-a51e-d173d352d2a5` Offenes Profil: offenes_Profil () · `f1d42b1c-d4d2-445f-b208-972167441bcc` Offenes Profil: offenes_Profil 5.00 (5) · `f7f4e466-c509-4ce0-8557-bdc74eb76b26` Offenes Profil: offenes_Profil 6.25 (6.25) · `a7a90540-1df2-4061-a1bc-e3d22bc595d7` Offenes Profil: offenes_Profil 1.30 (1.3) · `bedce3e3-aaa5-4860-87b1-c054d78dc3c3` Offenes Profil: offenes_Profil 1.74 (1.74) · `70e79690-13d1-4042-95cd-ed0324599a3b` Offenes Profil: offenes_Profil 1.10 (1.1) · `c365df6d-41da-4dad-bb6f-6517fc4c4684` Offenes Profil: offenes_Profil 0.88 (0.88) · `b9323960-82db-46c8-9a11-2c6a64d3caa3` Rechteckprofil: Rechteckprofil 2.33 (2.33) · `ce91a78c-c4e5-4221-9cf6-8016dff81c5b` Rechteckprofil: Rechteckprofil 1.00 (1) · `6650e7ca-f55f-4d2d-8d72-395b0e3bdaa7` Rechteckprofil: Rechteckprofil 2.00 (2) · `3ff80a83-6f7b-4f2c-b5da-0fcc30e0cde7` Rechteckprofil: Rechteckprofil 0.10 (0.1) · `4985cd32-3877-48ae-ab1c-7a015da0d56d` Rechteckprofil: Rechteckprofil 0.70 (0.7) · `17bf48c5-3878-4e83-af40-d89e05a24024` Rechteckprofil: Rechteckprofil 0.83 (0.83) · `41a3bcaa-e0bd-4d1f-a43c-f4f41ff4a635` Rechteckprofil: Rechteckprofil 0.63 (0.63) · `6850ad53-afcb-42ff-aa7b-956578933d31` Rechteckprofil: Rechteckprofil 0.90 (0.9) · `d6157433-cdc4-4804-b0a8-883c4b77fa4d` Rechteckprofil: Rechteckprofil 0.67 (0.67) · `1b970dac-09ed-4046-9dc0-dca40bb0ba72` Rechteckprofil: Rechteckprofil 6.25 (6.25) · `c2cfb0ec-95db-44f9-9be5-e0ea460bd96f` Rechteckprofil: Rechteckprofil 5.00 (5) · `a30183bc-f4d2-4267-a281-14c675f4938a` Rechteckprofil: Rechteckprofil 1.60 (1.6) · `1295dd77-b745-4a4e-baf5-dba167c13506` Rechteckprofil: Rechteckprofil 1.33 (1.33) · `6289ccb5-4a39-4950-a371-b88624fc7434` Rechteckprofil: Rechteckprofil 0.75 (0.75) · `c86ce1d4-e411-4d43-9168-1547cb708f6a` Rechteckprofil: Rechteckprofil 1.25 (1.25) · `91dc6623-830b-4bf9-b739-f9eea457c5eb` Rechteckprofil: Rechteckprofil 1.20 (1.2) · `86a9ec31-4644-4eeb-a02f-289be120aee7` Rechteckprofil: Rechteckprofil 0.96 (0.96) · `0629916f-145c-4812-a231-3c95649e3e41` Rechteckprofil: Rechteckprofil 0.59 (0.59) · `bd2ed8b6-df92-48de-a373-c0d0dad53770` Rechteckprofil: Rechteckprofil 0.43 (0.43) · `d9385b97-9965-42a6-8907-71b6b9990410` Rechteckprofil: Rechteckprofil 2.50 (2.5) · `704b8285-0872-4df6-8c12-0154cb745e05` Rechteckprofil: Rechteckprofil 0.57 (0.57) · `66c4f31c-0fb0-4f0a-b6e5-0a3737f86adb` Rechteckprofil: Rechteckprofil 3.33 (3.33) · `8f50b003-3786-44fc-acde-e942f3691cc5` Rechteckprofil: Rechteckprofil 1.22 (1.22) · `63ecc68d-58cb-46eb-9b6d-004c2c9b2a15` Rechteckprofil: Rechteckprofil 0.20 (0.2) · `a8bd2e2e-0925-46d1-a4c8-1a0e15662986` Rechteckprofil: Rechteckprofil 3.50 (3.5) · `9cbd44de-7453-4431-b4df-0d46ff196748` Rechteckprofil: Rechteckprofil 0.50 (0.5) · `27a2bf94-6848-462b-a6c0-605c6350f123` Rechteckprofil: Rechteckprofil () · `baecfa54-fb8b-429a-b889-8b244199e16d` Spezialprofil: Spezialprofil 1.20 (1.2) · `23009dc1-bc72-49f6-b489-d81cec52bfb1` Spezialprofil: Spezialprofil 0.50 (0.5) · `cb196989-0f4d-4c2b-bd9a-5fa96d45d253` Spezialprofil: Spezialprofil 0.24 (0.24) · `9e8438f4-e3e6-43c3-ad67-d7da6fe84c6c` Spezialprofil: Spezialprofil 1.00 (1) · `e17e95db-5ee1-4736-868c-8db0b99438d5` Spezialprofil: Spezialprofil 0.13 (0.13) · `193847aa-3dcf-45c1-b7e4-f335decd8df5` Spezialprofil: Spezialprofil 0.23 (0.23) · `fa01b6ec-9dfb-440e-92c4-d84e1ea60cbd` Spezialprofil: Spezialprofil 0.14 (0.14) · `7f97379f-b410-4d3c-9528-53d3e780f2a2` Spezialprofil: Spezialprofil 0.33 (0.33) · `ea4ba290-728f-42b9-a475-4586824e29b4` Spezialprofil: Spezialprofil 0.12 (0.12) · `a72a6751-6bc2-4bc3-ad7e-91e11a5c90ae` Spezialprofil: Spezialprofil 0.27 (0.27) · `22263faf-1008-49cd-ad29-08409777efbe` Spezialprofil: Spezialprofil 0.86 (0.86) · `5c042f62-691d-48b8-bf61-f8e3d2ba7076` Spezialprofil: Spezialprofil 1.10 (1.1) · `eafa0fd9-e618-4270-adf4-69196c2119fa` Spezialprofil: Spezialprofil () · `ba651cbf-e647-47ca-ae04-9f7c11b549fa` Andere: andere 0.88 (0.88) |
| 2 | Strang-ID/Bezeichnung | verweis |  |  |  | → `AWK_STRANG` [bezeichnung] |
| 3 | Funktion Melioration | auswahl |  |  |  | `0` Unbekannt · `1` Hauptkanal · `2` Sammelkanal · `3` Sauger |
| 4 | Sickerung / Leckschutz | auswahl |  |  |  | `0` Unbekannt · `1` Holzschnitzel · `2` Sickerkies · `3` Andere |
| 5 | Sickerung / Leckschutz – Leckschutz *(abgeleitet; im WebGIS „“)* | auswahl |  |  |  | `0` Unbekannt · `1` Holzschnitzel · `2` Sickerkies · `3` Andere |
| 6 | Hydraulische Belastung Ist | zahl |  |  | Float |  |
| 7 | Fliesszeit Trockenwetter | text |  |  |  |  |
| 8 | Qmax. | text |  | ● | [m³/s] |  |
| 9 | Qvoll. | text |  | ● | [m³/s] |  |
| 10 | Vmax. | text |  | ● | [m/s] |  |
| 11 | Auslastungsgrad | text |  | ● |  |  |
| 12 | Hmax. oben | zahl |  | ● | Float |  |
| 13 | Hfrei oben | zahl |  | ● | [m] Float |  |
| 14 | Hmax. unten | zahl |  | ● | Float |  |
| 15 | Hfrei unten | zahl |  | ● | [m] Float |  |
| 16 | Länge effektiv | zahl |  | ● | Float |  |

### Metadaten

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Erstellt am (UTC) | datum |  | ● | Date |  |
| 2 | Erstellt von | text |  | ● |  |  |
| 3 | Geändert am (UTC) | datum |  | ● | Date |  |
| 4 | Geändert von | text |  | ● |  |  |


---

## 2. Schacht (Normschacht)

Tabelle `AWK_ABWASSERKNOTEN`, Subtyp `art_bauwerk=1`, Maskentitel im WebGIS: „Normschacht“

64 Felder, davon 31 Auswahlfelder. Bereiche (Menüs) in Reihenfolge: **Kopf** → **Daten I** → **Daten II** → **Bauwerksteile** → **Stammkarte** → **Administrativ** → **Einbauten** → **Unterhalt** → **Hydraulik** → **Metadaten**

### Kopf

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Bezeichnung | text | ● |  |  |  |
| 2 | OBJECTID | text |  | ● |  |  |
| 3 | Bezeichnung alter./hist. | text |  |  |  |  |
| 4 | Bezeichnung alter./hist. – hist. *(abgeleitet; im WebGIS „“)* | text |  |  |  |  |
| 5 | Rechtswert/Hochwert | zahl |  | ● | Float |  |
| 6 | Rechtswert/Hochwert – Hochwert *(abgeleitet; im WebGIS „“)* | zahl |  | ● | Float |  |

### Daten I

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Bauwerksart Normschacht *(abgeleitet; im WebGIS „“)* | auswahl | ● |  |  | `100` Unbekannt · `105` Absturzschacht · `108` Dachwasserschacht · `110` Einlaufschacht · `113` Entwässerungsrinne · `116` Geleiseschacht · `118` Regenüberlauf · `125` Kontrollschacht · `127` Ölabscheider · `137` Schlammsammler · `138` Schwimmstoffabscheider · `141` Spülschacht · `144` Trennschacht · `170` Andere · `173` Pumpenschacht · `185` Behandlungsanlage · `186` Bodenablauf · `187` Entwässerungsrinne mit Schlammsack · `188` Fettabscheider · `189` Kombischacht · `190` Vorbehandlungsanlage |
| 2 | Typ AA/Nutzungsart | auswahl | ● |  |  | `0` Unbekannt · `1` PAA · `2` SAA |
| 3 | Typ AA/Nutzungsart – Nutzungsart *(abgeleitet; im WebGIS „“)* | auswahl | ● |  |  | `0` Unbekannt · `4` Mischabwasser · `5` Regenabwasser · `106` Reinabwasser · `7` Schmutzabwasser · `1` Bachabwasser · `102` Entlastetes Mischabwasser · `103` Industrieabwasser · `111` Andere · `1100` Bergwasser · `1101` Strassenabwasser |
| 4 | Funktion hier./hydr. | auswahl | ● |  |  | `0` Unbekannt · `5` Liegenschaftsentwässerung · `106` Sammelkanal · `3` Hauptsammelkanal · `2` Gewässer · `108` Strassenentwässerung · `104` Hauptsammelkanal, regional · `107` Sanierungsleitung · `14` Rinne · `115` Andere |
| 5 | Funktion hier./hydr. – hydr. *(abgeleitet; im WebGIS „“)* | auswahl | ● |  |  | `0` Unbekannt · `4` Freispiegelleitung · `5` Pumpendruckleitung · `101` Drainagetransportleitung · `102` Drosselleitung · `103` Dükerleitung · `106` Sickerleitung · `107` Speicherleitung · `108` Spülleitung · `111` Andere · `116` Vakuumleitung · `1100` Belagsrinne Wasserschale · `1101` Entwaesserungsgraben befestigt · `1102` Entwaesserungsgraben unbefestigt · `1103` Schlitzrinne · `1104` Wasserrinne mit Rost |
| 6 | Status | auswahl | ● |  |  | `0` Unbekannt · `1` In Betrieb · `2` Ausser Betrieb · `5` Tot/Aufgehoben, verfüllt |
| 7 | Baujahr *(abgeleitet; im WebGIS „“)* | zahl | ● |  | Integer |  |
| 8 | Material | auswahl | ● |  |  | `0` Unbekannt · `1` Beton · `2` Stahl · `3` Kunststoff · `4` Guss · `5` Andere |
| 9 | Material – 2. Feld *(abgeleitet; im WebGIS „“)* | auswahl | ● |  |  | abhängig von **Material**:<br>  - Elternwert `0`: `0` Unbekannt<br>  - Elternwert `1`: `101` Beton, unbekannt · `102` Beton, armiert · `103` Beton, vorgespannt · `104` Beton, Fertigteil · `105` Beton, unarmiert · `106` Ortsbeton · `107` Schleuderbeton · `108` Spezialzement, armiert · `109` Spezialzement, unarmiert · `110` Polymerbeton<br>  - Elternwert `2`: `122` Stahl, unbekannt · `123` Stahl, nicht rostbeständig · `124` Stahl, rostbeständig<br>  - Elternwert `3`: `119` Kunststoff, unbekannt · `120` Kunststoff, HDPE · `121` GUP/GFK, Fertigteil<br>  - Elternwert `4`: `115` Guss, unbekannt · `116` Grauguss · `117` Guss, duktil · `118` Gussbeton<br>  - Elternwert `5`: `111` Verschiedene · `112` Zement · `113` Faserzement · `114` Asbestzement |
| 10 | Form | auswahl | ● |  |  | `0` Unbekannt · `1` Rund · `102` Oval · `103` Quadratisch · `104` Rechteckig · `105` Vieleckig |
| 11 | Breite/Länge [mm] | auswahl | ● |  |  | `100` 100 · `150` 150 · `200` 200 · `250` 250 · `300` 300 · `400` 400 · `450` 450 · `500` 500 · `600` 600 · `700` 700 · `800` 800 · `900` 900 · `1000` 1000 · `1050` 1050 · `1100` 1100 · `1200` 1200 · `1250` 1250 · `1400` 1400 · `1500` 1500 · `1600` 1600 · `1650` 1650 · `1750` 1750 · `1800` 1800 · `1900` 1900 · `2000` 2000 · `2300` 2300 · `2500` 2500 · `2740` 2740 · `2800` 2800 · `3000` 3000 · `3800` 3800 |
| 12 | Breite/Länge [mm] – Länge [mm] *(abgeleitet; im WebGIS „“)* | auswahl | ● |  |  | `100` 100 · `150` 150 · `200` 200 · `250` 250 · `300` 300 · `400` 400 · `450` 450 · `500` 500 · `600` 600 · `700` 700 · `800` 800 · `900` 900 · `1000` 1000 · `1050` 1050 · `1100` 1100 · `1200` 1200 · `1250` 1250 · `1400` 1400 · `1500` 1500 · `1600` 1600 · `1650` 1650 · `1750` 1750 · `1800` 1800 · `1900` 1900 · `2000` 2000 · `2300` 2300 · `2500` 2500 · `2740` 2740 · `2800` 2800 · `3000` 3000 · `3800` 3800 |
| 13 | Rotation | text |  |  |  |  |
| 14 | Deckelhöhe | text |  | ● |  |  |
| 15 | Geländehöhe | zahl |  |  | Float |  |
| 16 | Sohlenhöhe | text |  |  |  |  |
| 17 | Tiefe [m] | zahl |  |  | Float |  |
| 18 | Ebene | auswahl |  |  |  | `0` Ebene 0 · `1` Ebene 1 · `2` Ebene -1 · `3` Ebene 2 · `4` Ebene -2 · `5` Ebene 3 · `6` Ebene -3 · `7` Ebene 4 · `8` Ebene -4 · `9` Ebene 5 · `10` Ebene -5 · `11` Ebene 6 · `12` Ebene -6 · `13` Ebene 7 · `14` Ebene -7 · `15` Ebene 8 · `16` Ebene -8 · `17` Ebene 9 · `18` Ebene -9 · `19` Ebene 10 · `20` Ebene -10 |
| 19 | Bemerkung | mehrzeilig |  |  |  |  |

### Daten II

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Lagebest./-genauigkeit | auswahl | ● |  |  | `0` Unbekannt · `101` Ungenau · `102` Genau |
| 2 | Lagebest./-genauigkeit – -genauigkeit *(abgeleitet; im WebGIS „“)* | auswahl | ● |  |  | `0` Unbekannt · `101` > 50 cm · `102` +/- 10 cm · `103` +/- 3 cm · `104` +/- 50 cm |
| 3 | Höhenbest./-genauigkeit | auswahl | ● |  |  | `0` Unbekannt · `101` Ungenau · `102` Genau |
| 4 | Höhenbest./-genauigkeit – -genauigkeit *(abgeleitet; im WebGIS „“)* | auswahl | ● |  |  | `0` Unbekannt · `101` > 6 cm · `102` +/- 1 cm · `103` +/- 3 cm · `104` +/- 6 cm |
| 5 | Zugänglichkeit | auswahl | ● |  |  | `0` Unbekannt · `101` Überdeckt · `102` Unzugänglich · `103` Zugänglich |
| 6 | Rückstaukote | zahl |  |  | Float |  |
| 7 | Interventionsmöglichkeit | auswahl |  |  |  | `0` Unbekannt · `1` Ja · `2` Nein |
| 8 | Amphibienausstieg | auswahl |  |  |  | `0` Unbekannt · `1` Ja · `2` Nein |

### Bauwerksteile

**Aufklappliste „Hauptdeckel“** (Untermenü) – Tabelle `AWK_DECKEL`, bearbeitbar, mehrere Zeilen; Spalten: Nummer · Form · Material · Höhe; Detailmaske: **Deckel**

**Aufklappliste „Deckel“** (Untermenü) – Tabelle `AWK_DECKEL`, bearbeitbar, mehrere Zeilen; Spalten: Nummer · Form · Material · Höhe; Detailmaske: **Deckel**

**Aufklappliste „Bauwerksteile“** (Untermenü) – Tabelle `AWK_BAUWERKSTEIL`, bearbeitbar, mehrere Zeilen; Spalten: Bezeichnung · Art · Subart; Detailmaske: **Bauwerksteil**

**Aufklappliste „Einläufe“** (Untermenü) – Tabelle `AWK_HALTUNG`, nur lesend; Spalten: Nr. · Bezeichnung · Breite · Lichte Höhe · Einlaufhöhe; Detailmaske: **Haltung – die anschliessenden Haltungen, nur lesend**

**Aufklappliste „Ausläufe“** (Untermenü) – Tabelle `AWK_HALTUNG`, nur lesend; Spalten: Nr. · Bezeichnung · Breite · Lichte Höhe · Auslaufhöhe; Detailmaske: **Haltung – die anschliessenden Haltungen, nur lesend**

### Stammkarte

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Stammkarte | kontrollkaestchen |  |  |  |  |
| 2 | Hauptbauwerk | verweis |  |  |  | → `AWK_ABWASSERKNOTEN` [art_bauwerk] |
| 3 | Nächstes Bauwerk | verweis |  |  |  | → `AWK_ABWASSERKNOTEN` [art_bauwerk] |
| 4 | Informationsquelle | auswahl |  |  |  | `0` Unbekannt · `1` GEP: ARA-Einzugsgebiet · `2` GEP: Trägerschaft · `3` Andere |
| 5 | Steuerung/Fernwirkung | auswahl |  |  |  | `0` Unbekannt · `1` Keine Steuerung · `2` Lokale Steuerung · `3` Übermittlung Alarm · `4` Übermittlung Messsignale · `5` Verbundsteuerung · `6` Andere |
| 6 | Sachbearbeiter | text |  |  |  |  |
| 7 | Büro | auswahl |  |  |  | `7747a36a-6420-41e2-bbe8-64ff28a7849d` unbekannt (Unbekannt) · `7692de6b-dce2-485c-8f01-190419e4c32f` Privat (Privat) · `4c18a435-f416-4814-a9b4-b68d98189cc4` Bund Astra (Bund) · `58d1c876-3d16-47da-8b38-b509bbc0cbca` Kanton Uri (Kanton) · `df1f763b-7f01-4d4d-a22c-14476c7a3a9b` Bund (Bund) · `f8186a5d-399a-443c-921f-460dff3d0566` oeff_Rechtl_Koerperschaften (Genossenschaft/Kooperation) · `afd5affb-1ce7-49d5-af42-c7e52641dc58` AWU_von_oeffentlich (Abwasserverband) · `86dd0b68-c510-4926-90eb-c5832e4663c4` AWU_von_privat (Abwasserverband) · `54a8d062-7796-4eee-b4d5-60a9d1084f9d` RUAG (Privat) · `357bfc90-bf4b-4e5d-87d6-277a76eefbbd` Meliorationsgen. Reussebene Uri (Genossenschaft/Kooperation) · `cbb47e18-cbc0-48cc-98ea-c37cc56622f9` Meliorationsgen. Seedorf (Genossenschaft/Kooperation) · `16853e1b-1f90-49e6-b762-7ec9ebd7bb59` Altdorf (Gemeinde) · `7c74fab1-4121-4cb9-bcfd-00eea3b4c204` Bürglen (Gemeinde) · `9f6939a7-ba0a-47c6-9c63-9c44cddfddf3` Schattdorf (Gemeinde) · `a4efbb6e-f8a6-4c65-a9df-be9ff1356dea` Andermatt (Gemeinde) · `65f14d2d-8f4e-45de-9635-ae76774e61db` Attinghausen (Gemeinde) · `07e5db4a-ecd0-4c23-bd5d-e144ec68d28e` Erstfeld (Gemeinde) · `6108523b-a8f3-42f9-bf88-1492bdea77d1` Flüelen (Gemeinde) · `1035c742-cb3c-44d5-a7c4-ee3db1ce7a96` Göschenen (Gemeinde) · `c929ee69-5218-4366-9691-a8662f134fe4` Gurtnellen (Gemeinde) · `07e608b7-d1c9-4700-99c6-e0cb65b1b4fd` Hospental (Gemeinde) · `d1647b72-5b4b-4940-af16-1f5325b97ee8` Isenthal (Gemeinde) · `ff2b72c6-c8d8-4a52-b3a0-1ab7810c5c69` Realp (Gemeinde) · `9a2739a5-dd1d-401d-b060-0c37a7b75362` Seedorf (Gemeinde) · `52da3b7e-760d-4659-87fd-a23c977d0941` Seelisberg (Gemeinde) · `6b3fb54d-d3a4-41bd-91e0-542e9ecd4e8e` Silenen (Gemeinde) · `db0e9992-5a10-4c5a-853d-a1e9637042cb` Sisikon (Gemeinde) · `43c22364-adb7-40c2-8759-0dbebf879de0` Spiringen (Gemeinde) · `289c8fc2-ee43-4da2-bb81-6592880080b7` Unterschächen (Gemeinde) · `95844aa7-7306-4cea-88f2-4629a4792c4c` Wassen (Gemeinde) |
| 8 | Standortgemeinde | auswahl |  |  |  | `7747a36a-6420-41e2-bbe8-64ff28a7849d` unbekannt (Unbekannt) · `7692de6b-dce2-485c-8f01-190419e4c32f` Privat (Privat) · `4c18a435-f416-4814-a9b4-b68d98189cc4` Bund Astra (Bund) · `58d1c876-3d16-47da-8b38-b509bbc0cbca` Kanton Uri (Kanton) · `df1f763b-7f01-4d4d-a22c-14476c7a3a9b` Bund (Bund) · `f8186a5d-399a-443c-921f-460dff3d0566` oeff_Rechtl_Koerperschaften (Genossenschaft/Kooperation) · `afd5affb-1ce7-49d5-af42-c7e52641dc58` AWU_von_oeffentlich (Abwasserverband) · `86dd0b68-c510-4926-90eb-c5832e4663c4` AWU_von_privat (Abwasserverband) · `54a8d062-7796-4eee-b4d5-60a9d1084f9d` RUAG (Privat) · `357bfc90-bf4b-4e5d-87d6-277a76eefbbd` Meliorationsgen. Reussebene Uri (Genossenschaft/Kooperation) · `cbb47e18-cbc0-48cc-98ea-c37cc56622f9` Meliorationsgen. Seedorf (Genossenschaft/Kooperation) · `16853e1b-1f90-49e6-b762-7ec9ebd7bb59` Altdorf (Gemeinde) · `7c74fab1-4121-4cb9-bcfd-00eea3b4c204` Bürglen (Gemeinde) · `9f6939a7-ba0a-47c6-9c63-9c44cddfddf3` Schattdorf (Gemeinde) · `a4efbb6e-f8a6-4c65-a9df-be9ff1356dea` Andermatt (Gemeinde) · `65f14d2d-8f4e-45de-9635-ae76774e61db` Attinghausen (Gemeinde) · `07e5db4a-ecd0-4c23-bd5d-e144ec68d28e` Erstfeld (Gemeinde) · `6108523b-a8f3-42f9-bf88-1492bdea77d1` Flüelen (Gemeinde) · `1035c742-cb3c-44d5-a7c4-ee3db1ce7a96` Göschenen (Gemeinde) · `c929ee69-5218-4366-9691-a8662f134fe4` Gurtnellen (Gemeinde) · `07e608b7-d1c9-4700-99c6-e0cb65b1b4fd` Hospental (Gemeinde) · `d1647b72-5b4b-4940-af16-1f5325b97ee8` Isenthal (Gemeinde) · `ff2b72c6-c8d8-4a52-b3a0-1ab7810c5c69` Realp (Gemeinde) · `9a2739a5-dd1d-401d-b060-0c37a7b75362` Seedorf (Gemeinde) · `52da3b7e-760d-4659-87fd-a23c977d0941` Seelisberg (Gemeinde) · `6b3fb54d-d3a4-41bd-91e0-542e9ecd4e8e` Silenen (Gemeinde) · `db0e9992-5a10-4c5a-853d-a1e9637042cb` Sisikon (Gemeinde) · `43c22364-adb7-40c2-8759-0dbebf879de0` Spiringen (Gemeinde) · `289c8fc2-ee43-4da2-bb81-6592880080b7` Unterschächen (Gemeinde) · `95844aa7-7306-4cea-88f2-4629a4792c4c` Wassen (Gemeinde) |

### Administrativ

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Eigentümer | auswahl | ● |  |  | `16853e1b-1f90-49e6-b762-7ec9ebd7bb59` Altdorf · `a4efbb6e-f8a6-4c65-a9df-be9ff1356dea` Andermatt · `65f14d2d-8f4e-45de-9635-ae76774e61db` Attinghausen · `afd5affb-1ce7-49d5-af42-c7e52641dc58` AWU_von_oeffentlich · `86dd0b68-c510-4926-90eb-c5832e4663c4` AWU_von_privat · `df1f763b-7f01-4d4d-a22c-14476c7a3a9b` Bund · `4c18a435-f416-4814-a9b4-b68d98189cc4` Bund Astra · `7c74fab1-4121-4cb9-bcfd-00eea3b4c204` Bürglen · `07e5db4a-ecd0-4c23-bd5d-e144ec68d28e` Erstfeld · `6108523b-a8f3-42f9-bf88-1492bdea77d1` Flüelen · `1035c742-cb3c-44d5-a7c4-ee3db1ce7a96` Göschenen · `c929ee69-5218-4366-9691-a8662f134fe4` Gurtnellen · `07e608b7-d1c9-4700-99c6-e0cb65b1b4fd` Hospental · `d1647b72-5b4b-4940-af16-1f5325b97ee8` Isenthal · `58d1c876-3d16-47da-8b38-b509bbc0cbca` Kanton Uri · `357bfc90-bf4b-4e5d-87d6-277a76eefbbd` Meliorationsgen. Reussebene Uri · `cbb47e18-cbc0-48cc-98ea-c37cc56622f9` Meliorationsgen. Seedorf · `f8186a5d-399a-443c-921f-460dff3d0566` oeff_Rechtl_Koerperschaften · `7692de6b-dce2-485c-8f01-190419e4c32f` Privat · `ff2b72c6-c8d8-4a52-b3a0-1ab7810c5c69` Realp · `54a8d062-7796-4eee-b4d5-60a9d1084f9d` RUAG · `9f6939a7-ba0a-47c6-9c63-9c44cddfddf3` Schattdorf · `9a2739a5-dd1d-401d-b060-0c37a7b75362` Seedorf · `52da3b7e-760d-4659-87fd-a23c977d0941` Seelisberg · `6b3fb54d-d3a4-41bd-91e0-542e9ecd4e8e` Silenen · `db0e9992-5a10-4c5a-853d-a1e9637042cb` Sisikon · `43c22364-adb7-40c2-8759-0dbebf879de0` Spiringen · `7747a36a-6420-41e2-bbe8-64ff28a7849d` unbekannt · `289c8fc2-ee43-4da2-bb81-6592880080b7` Unterschächen · `95844aa7-7306-4cea-88f2-4629a4792c4c` Wassen |
| 2 | Betreiber | auswahl | ● |  |  | `16853e1b-1f90-49e6-b762-7ec9ebd7bb59` Altdorf · `a4efbb6e-f8a6-4c65-a9df-be9ff1356dea` Andermatt · `65f14d2d-8f4e-45de-9635-ae76774e61db` Attinghausen · `afd5affb-1ce7-49d5-af42-c7e52641dc58` AWU_von_oeffentlich · `86dd0b68-c510-4926-90eb-c5832e4663c4` AWU_von_privat · `df1f763b-7f01-4d4d-a22c-14476c7a3a9b` Bund · `4c18a435-f416-4814-a9b4-b68d98189cc4` Bund Astra · `7c74fab1-4121-4cb9-bcfd-00eea3b4c204` Bürglen · `07e5db4a-ecd0-4c23-bd5d-e144ec68d28e` Erstfeld · `6108523b-a8f3-42f9-bf88-1492bdea77d1` Flüelen · `1035c742-cb3c-44d5-a7c4-ee3db1ce7a96` Göschenen · `c929ee69-5218-4366-9691-a8662f134fe4` Gurtnellen · `07e608b7-d1c9-4700-99c6-e0cb65b1b4fd` Hospental · `d1647b72-5b4b-4940-af16-1f5325b97ee8` Isenthal · `58d1c876-3d16-47da-8b38-b509bbc0cbca` Kanton Uri · `357bfc90-bf4b-4e5d-87d6-277a76eefbbd` Meliorationsgen. Reussebene Uri · `cbb47e18-cbc0-48cc-98ea-c37cc56622f9` Meliorationsgen. Seedorf · `f8186a5d-399a-443c-921f-460dff3d0566` oeff_Rechtl_Koerperschaften · `7692de6b-dce2-485c-8f01-190419e4c32f` Privat · `ff2b72c6-c8d8-4a52-b3a0-1ab7810c5c69` Realp · `54a8d062-7796-4eee-b4d5-60a9d1084f9d` RUAG · `9f6939a7-ba0a-47c6-9c63-9c44cddfddf3` Schattdorf · `9a2739a5-dd1d-401d-b060-0c37a7b75362` Seedorf · `52da3b7e-760d-4659-87fd-a23c977d0941` Seelisberg · `6b3fb54d-d3a4-41bd-91e0-542e9ecd4e8e` Silenen · `db0e9992-5a10-4c5a-853d-a1e9637042cb` Sisikon · `43c22364-adb7-40c2-8759-0dbebf879de0` Spiringen · `7747a36a-6420-41e2-bbe8-64ff28a7849d` unbekannt · `289c8fc2-ee43-4da2-bb81-6592880080b7` Unterschächen · `95844aa7-7306-4cea-88f2-4629a4792c4c` Wassen |
| 3 | Finanzierung | auswahl |  |  |  | `0` Unbekannt · `1` Öffentlich · `2` Privat |
| 4 | Wiederbeschaffungswert | text |  |  |  |  |
| 5 | Wiederbeschaffungswert Basisjahr | zahl |  |  | Integer |  |
| 6 | Wiederbeschaffungswert Bauart | auswahl |  |  |  | `0` Unbekannt · `1` Feld · `2` Sanierungsleitung: Bagger · `3` Sanierungsleitung: Grabenfräse · `4` Strasse · `5` Andere |
| 7 | Akten | mehrzeilig |  |  |  |  |

### Einbauten

**Aufklappliste „Absperr-/Drosselorgane“** (Untermenü) – Tabelle `AWK_ABWASSERKNOTEN`, nur lesend; Spalten: Bezeichnung · Art; Detailmaske: **Absperr-/Drosselorgan**

**Aufklappliste „Pumpen“** (Untermenü) – Tabelle `AWK_ABWASSERKNOTEN`, nur lesend; Spalten: Bezeichnung · Art; Detailmaske: **Förderaggregat (Pumpe)**

**Aufklappliste „Überläufe“** (Untermenü) – Tabelle `AWK_ABWASSERKNOTEN`, nur lesend; Spalten: Bezeichnung · Art; Detailmaske: **Überlauf**

**Aufklappliste „Mechanische Vorreinigung“** (Untermenü) – Tabelle `AWK_VSA_MECHVORREINIGUNG`, bearbeitbar, mehrere Zeilen; Spalten: Bezeichnung · Art; Detailmaske: **Mechanische Vorreinigung**

### Unterhalt

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Zustand/Sanierungsbedarf | auswahl |  |  |  | `0` Unbekannt · `100` Nicht mehr funktionstüchtig (Z0) · `101` Starke Mängel (Z1) · `102` Mittlere Mängel (Z2) · `103` Leichte Mängel (Z3) · `104` Keine Mängel (Z4) · `1000` nicht beurteilt |
| 2 | Zustand/Sanierungsbedarf – Sanierungsbedarf *(abgeleitet; im WebGIS „“)* | auswahl |  |  |  | `0` Unbekannt · `101` Dringend · `102` Kurzfristig · `103` Mittelfristig · `104` Langfristig · `105` Keiner · `106` Saniert |
| 3 | Erhebungsjahr [Zustand] | zahl |  |  | Integer |  |
| 4 | Inspektionsintervall [Jahr] | zahl |  |  | Float |  |
| 5 | Spülintervall [Jahr] | zahl |  |  | Float |  |
| 6 | Zustandsklasse automatisch | auswahl |  | ● |  | `0` Keiner · `1` Mittelbar · `2` Langfristig · `3` Mittelfristig · `4` Kurzfristig · `5` Umgehend |
| 7 | Systemgrenze | auswahl |  |  |  | `0` unbekannt · `1` keine · `2` Uebergabepunkt · `3` Uebernahmepunkt |

**Aufklappliste „Unterhaltsmassnahmen“** (Untermenü) – Tabelle `AWZ_UNTERHALT`, bearbeitbar, mehrere Zeilen; Spalten: Zeitpunkt · Art · Status · Ausführender; Detailmaske: **Unterhalt – Erhaltungsereignis: ***

**Aufklappliste „Sanierungsmassnahmen“** (Untermenü) – Tabelle `AWZ_UNTERHALT`, bearbeitbar, mehrere Zeilen; Spalten: Beginn · Art · Status · Verfahren; Detailmaske: **Unterhalt – Sanierungsmassnahme: ***

**Aufklappliste „Dichtheitsprüfungen“** (Untermenü) – Tabelle `AWZ_UNTERHALT`, bearbeitbar, mehrere Zeilen; Spalten: Datum · Art · Status · Verfahren; Detailmaske: **Unterhalt – Dichtheitsprüfung: Dichtheitsprüfung**

**Aufklappliste „Inspektionen“** (Untermenü) – Tabelle `AWZ_INSPEKTION_KNOTEN`, bearbeitbar, mehrere Zeilen; Spalten: Datum · Verfahren · Nummer; Detailmaske: **Inspektion Schacht**

**Aufklappliste „GEP Massnahmen“** (Untermenü) – Tabelle `AWM_MASSNAHME`, bearbeitbar, mehrere Zeilen; Spalten: Bezeichnung · Beschreibung; Detailmaske: **GEP-Massnahme**

### Hydraulik

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Hmax. | zahl |  | ● | Float |  |
| 2 | Hfrei [m] | zahl |  |  | Float |  |
| 3 | Wassertiefe [m] | zahl |  | ● | Float |  |
| 4 | Anzahl Einzugsgebiete | text |  | ● |  |  |
| 5 | Hydraulische Geometrie | verweis |  |  |  | → `AWH_HYDR_GEOMETRIE` [bezeichnung] |

**Aufklappliste „Einzugsgebiete SW“** (Untermenü) – Tabelle `AWH_EINZUGSGEBIET`, nur lesend; Spalten: Fläche · Psi SW · Entwässerungssystem · E/ha · Zufluss; Detailmaske: **Einzugsgebiet**

**Aufklappliste „Einzugsgebiete RW“** (Untermenü) – Tabelle `AWH_EINZUGSGEBIET`, nur lesend; Spalten: Fläche · Psi RW · Entwässerungssystem · E/ha · Zufluss; Detailmaske: **Einzugsgebiet**

**Aufklappliste „Einzugsgebiete MW“** (Untermenü) – Tabelle `AWH_EINZUGSGEBIET`, nur lesend; Spalten: Fläche · Psi MW · Entwässerungssystem · E/ha · Zufluss; Detailmaske: **Einzugsgebiet**

### Metadaten

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Erstellt am (UTC) | datum |  | ● | Date |  |
| 2 | Erstellt von | text |  | ● |  |  |
| 3 | Geändert am (UTC) | datum |  | ● | Date |  |
| 4 | Geändert von | text |  | ● |  |  |


---

## 3. Bauwerksteil

Tabelle `AWK_BAUWERKSTEIL`, Maskentitel im WebGIS: „Bauwerksteil“

19 Felder, davon 4 Auswahlfelder. Bereiche (Menüs) in Reihenfolge: **Kopf** → **Daten** → **Daten II** → **Administrativ** → **Metadaten**

### Kopf

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Bezeichnung | text | ● |  |  |  |
| 2 | OBJECTID | text |  | ● |  |  |
| 3 | Knoten | verweis |  | ● |  | → `AWK_ABWASSERKNOTEN` [art_bauwerk]: [bezeichnung] |
| 4 | Haltung | verweis |  | ● |  | → `AWK_HALTUNG` [nutzungsart]: [bezeichnung] |

### Daten

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Art/Subart | auswahl | ● |  |  | `0` Unbekannt · `101` Bankett · `102` Elektromechanische Ausrüstung · `103` Trockenwetterrinne · `105` Elektrische Einrichtung · `106` Einstiegshilfe · `107` Trockenwetterfallrohr · `108` Feststoffrückhalt · `109` Beckenreinigung · `110` Beckenentleerung · `111` Rückstausicherung |
| 2 | Art/Subart – Subart *(abgeleitet; im WebGIS „“)* | auswahl | ● |  |  | abhängig von **Art/Subart**:<br>  - Elternwert `0`: `0` Unbekannt<br>  - Elternwert `101`: `124` Unbekannt · `120` Beidseitig · `121` Einseitig · `125` Andere · `1000` kein<br>  - Elternwert `102`: `126` Unbekannt · `102` Räumeinrichtung · `116` Leckwasserpumpe · `117` Luftentfeuchter · `127` Andere<br>  - Elternwert `103`: _(leer)_<br>  - Elternwert `105`: `128` Unbekannt · `110` Beleuchtung · `111` Fernwirkanlage · `112` Funk · `113` Telefon · `129` Andere<br>  - Elternwert `106`: `130` Unbekannt · `104` Drucktüre · `105` Leiter · `106` Steigeisen · `107` Treppe · `108` Trittnischen · `109` Türe · `131` Andere · `150` Keine<br>  - Elternwert `107`: _(leer)_<br>  - Elternwert `108`: `114` Feinrechen · `115` Grobrechen · `118` Sieb · `119` Tauchwand · `122` Keine · `160` Bürstenrechen · `161` Stauschild · `147` Andere · `148` Unbekannt<br>  - Elternwert `109`: `101` Air Jet · `103` Spülkippe · `132` Andere · `133` Keine · `134` Schwallspülung · `170` Räumereinrichtung · `171` Rührwerk · `172` Schlängelrinne · `173` Unbekannt<br>  - Elternwert `110`: `138` Pumpe · `139` Schieber · `140` Keine · `180` Gravitation · `181` Unbekannt · `141` Andere<br>  - Elternwert `111`: `143` Pumpe · `144` Rückstauklappe · `145` Stauschild · `146` Andere |
| 3 | Material | auswahl |  |  |  | `0` Unbekannt · `101` Kombiniert · `102` Kunststoff · `103` Steinzeug · `104` Zementmörtel · `105` Andere · `1000` Aluminium · `1001` Stahl |
| 4 | Durchmesser | text |  |  |  |  |
| 5 | Bemerkung | mehrzeilig |  |  |  |  |

### Daten II

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Instandstellung | auswahl |  |  |  | `0` Unbekannt · `101` Nicht notwendig · `102` Notwendig |
| 2 | Dimensionierungswert [l/s] | zahl |  |  | Float |  |
| 3 | Leistung [l/s] | zahl |  |  | Float |  |
| 4 | Anspringkote | zahl |  |  | Float |  |

### Administrativ

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Bruttokosten | text |  |  |  |  |
| 2 | Ersatzjahr | zahl |  |  | Integer |  |

### Metadaten

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Erstellt am (UTC) | datum |  | ● | Date |  |
| 2 | Erstellt von | text |  | ● |  |  |
| 3 | Geändert am (UTC) | datum |  | ● | Date |  |
| 4 | Geändert von | text |  | ● |  |  |


---

## 4. Haltungspunkt

Tabelle `AWK_HALTUNGSPUNKT`, Maskentitel im WebGIS: „Haltungspunkt“

15 Felder, davon 5 Auswahlfelder. Bereiche (Menüs) in Reihenfolge: **Kopf** → **Daten** → **Dokumente** → **Metadaten**

### Kopf

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Bezeichnung | text |  |  |  |  |
| 2 | Rechtswert/Hochwert | zahl |  | ● | Float |  |
| 3 | Rechtswert/Hochwert – Hochwert *(abgeleitet; im WebGIS „“)* | zahl |  | ● | Float |  |

### Daten

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Höhe/Lage/Auslaufform | zahl |  |  | Float |  |
| 2 | Höhe/Lage/Auslaufform – Lage *(abgeleitet; im WebGIS „“)* | text |  |  |  |  |
| 3 | Höhe/Lage/Auslaufform – Auslaufform *(abgeleitet; im WebGIS „“)* | auswahl |  |  |  | `0` Unbekannt · `101` Abgerundet · `102` Blendenförmig · `103` Keine Querschnittsänderung · `104` Scharfkantig |
| 4 | Lagebest./-genauigkeit | auswahl |  |  |  | `0` Unbekannt · `101` Ungenau · `102` Genau |
| 5 | Lagebest./-genauigkeit – -genauigkeit *(abgeleitet; im WebGIS „“)* | auswahl |  |  |  | `0` Unbekannt · `101` > 50 cm · `102` +/- 10 cm · `103` +/- 3 cm · `104` +/- 50 cm |
| 6 | Höhenbest./-genauigkeit | auswahl |  |  |  | `0` Unbekannt · `101` Ungenau · `102` Genau |
| 7 | Höhenbest./-genauigkeit – -genauigkeit *(abgeleitet; im WebGIS „“)* | auswahl |  |  |  | `0` Unbekannt · `101` > 6 cm · `102` +/- 1 cm · `103` +/- 3 cm · `104` +/- 6 cm |
| 8 | Bemerkung | mehrzeilig |  |  |  |  |

### Dokumente

**Aufklappliste „Dokumente“** (Untermenü) – Tabelle `AWK_DOKUMENT`, bearbeitbar, mehrere Zeilen; Spalten: Beschreibung · Pfad · Datum · dokument

### Metadaten

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Erstellt am (UTC) | datum |  | ● | Date |  |
| 2 | Erstellt von | text |  | ● |  |  |
| 3 | Geändert am (UTC) | datum |  | ● | Date |  |
| 4 | Geändert von | text |  | ● |  |  |


---

## 5. Strang

Tabelle `AWK_STRANG`, Maskentitel im WebGIS: „Strang“

11 Felder, davon 0 Auswahlfelder. Bereiche (Menüs) in Reihenfolge: **Kopf** → **Daten** → **Haltungen** → **Hydraulik** → **Metadaten**

### Kopf

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Bezeichnung | text |  |  |  |  |
| 2 | Knoten (von) | verweis |  |  |  | → `AWK_ABWASSERKNOTEN` [art_bauwerk]: [bezeichnung] |
| 3 | Knoten (bis) | verweis |  |  |  | → `AWK_ABWASSERKNOTEN` [art_bauwerk]: [bezeichnung] |

### Daten

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Länge [m] | zahl |  | ● | Float |  |
| 2 | Bemerkung | mehrzeilig |  |  |  |  |

### Haltungen

**Aufklappliste „Haltungen“** (Untermenü) – Tabelle `AWK_HALTUNG`, nur lesend; Spalten: Bezeichnung · Länge [m]

### Hydraulik

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Reibungsbeiwert [m^(1/3)/s] | text |  |  |  |  |
| 2 | Wandrauhigkeit [mm] *(abgeleitet; im WebGIS „“)* | text |  |  |  |  |

### Metadaten

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Erstellt am (UTC) | datum |  | ● | Date |  |
| 2 | Erstellt von | text |  | ● |  |  |
| 3 | Geändert am (UTC) | datum |  | ● | Date |  |
| 4 | Geändert von | text |  | ● |  |  |


---

## 6. Deckel

Tabelle `AWK_DECKEL`, Maskentitel im WebGIS: „Deckel“

28 Felder, davon 14 Auswahlfelder. Bereiche (Menüs) in Reihenfolge: **Kopf** → **Daten I** → **Daten II** → **Metadaten**

### Kopf

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Hauptdeckel | kontrollkaestchen |  |  |  |  |
| 2 | Nummer | text |  |  |  |  |
| 3 | Bezeichnung | text | ● |  |  |  |
| 4 | OBJECTID | text |  | ● |  |  |
| 5 | Rechtswert/Hochwert | zahl |  | ● | Float |  |
| 6 | Rechtswert/Hochwert – Hochwert *(abgeleitet; im WebGIS „“)* | zahl |  | ● | Float |  |

### Daten I

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Material | auswahl | ● |  |  | `0` Unbekannt · `109` Andere · `110` Beton · `111` Guss · `112` Guss mit Belagsfüllung · `113` Guss mit Betonfüllung |
| 2 | Knoten | verweis |  |  |  | → `AWK_ABWASSERKNOTEN` [art_bauwerk]: [bezeichnung] |
| 3 | Form | auswahl | ● |  |  | `0` Unbekannt · `2` Rund · `101` Eckig · `103` Andere |
| 4 | Breite/Länge [mm] | auswahl | ● |  |  | `100` 100 · `270` 270 · `300` 300 · `360` 360 · `400` 400 · `450` 450 · `470` 470 · `500` 500 · `550` 550 · `600` 600 · `700` 700 · `800` 800 · `840` 840 · `1000` 1000 · `1150` 1150 · `1250` 1250 · `1500` 1500 · `2000` 2000 · `2500` 2500 |
| 5 | Breite/Länge [mm] – Länge [mm] *(abgeleitet; im WebGIS „“)* | auswahl | ● |  |  | `100` 100 · `270` 270 · `300` 300 · `360` 360 · `400` 400 · `450` 450 · `470` 470 · `500` 500 · `550` 550 · `600` 600 · `700` 700 · `800` 800 · `840` 840 · `1000` 1000 · `1150` 1150 · `1250` 1250 · `1500` 1500 · `2000` 2000 · `2500` 2500 |
| 6 | Rotation | zahl |  |  | Float |  |
| 7 | Verschluss/Entlüftung | auswahl | ● |  |  | `0` Unbekannt · `101` Schlüssel · `102` Vierkant · `103` Verschraubt · `104` Keiner · `105` Nicht verschraubt |
| 8 | Verschluss/Entlüftung – Entlüftung *(abgeleitet; im WebGIS „“)* | auswahl | ● |  |  | `0` Unbekannt · `101` Entlüftet · `102` Nicht entlüftet · `103` Druckdicht |
| 9 | Höhe | zahl |  |  | Float |  |
| 10 | Bemerkung | mehrzeilig |  |  |  |  |

### Daten II

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Lagebest./-genauigkeit | auswahl | ● |  |  | `0` Unbekannt · `101` Ungenau · `102` Genau |
| 2 | Lagebest./-genauigkeit – -genauigkeit *(abgeleitet; im WebGIS „“)* | auswahl | ● |  |  | `0` Unbekannt · `101` > 50 cm · `102` +/- 10 cm · `103` +/- 3 cm · `104` +/- 50 cm |
| 3 | Höhenbest./-genauigkeit | auswahl | ● |  |  | `0` Unbekannt · `101` Ungenau · `102` Genau |
| 4 | Höhenbest./-genauigkeit – -genauigkeit *(abgeleitet; im WebGIS „“)* | auswahl | ● |  |  | `0` Unbekannt · `101` > 6 cm · `102` +/- 1 cm · `103` +/- 3 cm · `104` +/- 6 cm |
| 5 | Last/Schlammeimer | auswahl |  |  |  | `0` Unbekannt · `101` bis 5 t · `102` von 5 t bis 10 t · `103` grösser als 10 t · `104` Klasse A (15kN) · `105` Klasse B (125kN) · `106` Klasse C (250kN) · `107` Klasse D (400kN) · `108` Klasse E (600kN) · `109` Klasse F (900kN) |
| 6 | Last/Schlammeimer – Schlammeimer *(abgeleitet; im WebGIS „“)* | auswahl |  |  |  | `0` Unbekannt · `1` Nicht vorhanden · `2` Vorhanden |
| 7 | Instandstellung/Fabrikat | auswahl |  |  |  | `0` Unbekannt · `101` Nicht notwendig · `102` Notwendig |
| 8 | Instandstellung/Fabrikat – Fabrikat *(abgeleitet; im WebGIS „“)* | auswahl |  |  |  | `0` Unbekannt · `1000` BEGU Beton · `1001` BGS 115-50 · `1002` BGS 180 · `1003` BGS 640 · `1004` BGS N180 · `1005` BGS N190 · `1006` BGS N640 · `1007` Erz 6205 · `1008` Erz 6216 · `1009` Erz 6501 · `1010` Erz 6502 · `1011` Erz 6503 · `1012` Erz 6503b · `1013` Erz 6504 · `1014` Erz 6504b · `1015` Erz 6511 · `1016` Erz 6512 · `1017` Erz 6513 · `1018` Erz 7190/RI · `1019` Erz 7196/3 · `1020` Erz 7196/4 · `1021` Erz 7196/6 · `1022` Erz 7196/7 · `1023` Erz 7496/8 · `1024` Erz 7421 · `1025` GUBE / Erz 6584 · `1026` Spezial · `1027` vonRoll 2973 · `1028` GE 6511 · `1029` GE 6512 · `1030` BGS 690 · `1031` BGS N696S60 · `1032` BGS 186-80VS · `1033` BGS N186 · `1034` BGS 140-60 · `1035` BGS 150-60 |

### Metadaten

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Erstellt am (UTC) | datum |  | ● | Date |  |
| 2 | Erstellt von | text |  | ● |  |  |
| 3 | Geändert am (UTC) | datum |  | ● | Date |  |
| 4 | Geändert von | text |  | ● |  |  |


---

## 7. Unterhalt – Erhaltungsereignis: Unbekannt

Tabelle `AWZ_UNTERHALT`, Subtyp `art=0`, Maskentitel im WebGIS: „Erhaltungsereignis: Unbekannt“

8 Felder, davon 2 Auswahlfelder. Bereiche (Menüs) in Reihenfolge: **Kopf** → **Knoten** → **Haltung** → **Dokumente** → **Metadaten**

### Kopf

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Bezeichnung | text |  |  |  |  |
| 2 | Art | auswahl |  |  |  | `0` Unbekannt · `8` Begehung · `3` Reinigung · `10` Dichtheitsprüfung · `2` Reparatur · `4` Renovierung · `5` Untersuchung · `1` Erneuerung · `9` Deformationsmessung · `11` Georadar · `7` Andere · `12` Kanalfernsehen |
| 3 | Status | auswahl |  |  |  | `0` Unbekannt · `1` Ausgeführt · `2` Geplant · `3` Nicht möglich · `4` Beauftragt · `5` Nicht beauftragt |
| 4 | Auftrag Nr./Bez. | verweis |  |  |  | → `AWZ_AUFTRAG` [nummer] / [bezeichnung] |

### Knoten

**Aufklappliste „“** (Untermenü) – Tabelle `AWK_ABWASSERKNOTEN`, bearbeitbar, mehrere Zeilen; Spalten: Art · Subart · Bezeichnung

### Haltung

**Aufklappliste „“** (Untermenü) – Tabelle `AWK_HALTUNG`, bearbeitbar, mehrere Zeilen; Spalten: Bezeichnung · Nutzungsart · Funktionhierarchisch

### Dokumente

**Aufklappliste „Dokumente“** (Untermenü) – Tabelle `AWK_DOKUMENT`, bearbeitbar, mehrere Zeilen; Spalten: Beschreibung · Pfad · Datum · dokument

### Metadaten

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Erstellt am (UTC) | datum |  | ● | Date |  |
| 2 | Erstellt von | text |  | ● |  |  |
| 3 | Geändert am (UTC) | datum |  | ● | Date |  |
| 4 | Geändert von | text |  | ● |  |  |


---

## 8. Unterhalt – Sanierungsmassnahme: Erneuerung

Tabelle `AWZ_UNTERHALT`, Subtyp `art=1`, Maskentitel im WebGIS: „Sanierungsmassnahme: Erneuerung“

17 Felder, davon 8 Auswahlfelder. Bereiche (Menüs) in Reihenfolge: **Kopf** → **Daten I** → **Metadaten**

### Kopf

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Bezeichnung | text |  |  |  |  |
| 2 | Art | auswahl |  |  |  | `0` Unbekannt · `8` Begehung · `3` Reinigung · `10` Dichtheitsprüfung · `2` Reparatur · `4` Renovierung · `5` Untersuchung · `1` Erneuerung · `9` Deformationsmessung · `11` Georadar · `7` Andere · `12` Kanalfernsehen |
| 3 | Status | auswahl |  |  |  | `0` Unbekannt · `1` Ausgeführt · `2` Geplant · `3` Nicht möglich · `4` Beauftragt · `5` Nicht beauftragt |
| 4 | Auftrag Nr./Bez. | verweis |  |  |  | → `AWZ_AUFTRAG` [nummer] / [bezeichnung] |

### Daten I

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Verfahren | auswahl |  |  |  | abhängig von **Art**:<br>  - Elternwert `0`: `0` Unbekannt · `35` Neues Verfahren<br>  - Elternwert `1`: `3` Austausch von Bauteilen · `7` Berstverfahren · `17` Vollständige Erneuerung · `21` Rohraustausch · `26` Rohrziehverfahren · `29` Teilerneuerung · `30` Überfahren<br>  - Elternwert `2`: `5` Aussenmanschetten · `8` Einbau von Abdichtungsstoffen · `9` Flutungsverfahren · `10` Injektionen der Leitungszone · `11` Innenmanschetten · `12` Injektion von Undichtigkeiten · `15` Lageregulierung Schachtabdeckung · `19` Oberflächenbehandlung · `20` Ortslaminate · `23` Roboterverfahren · `28` Schrumpfschläuche · `31` Verdrängungsverfahren · `32` Verfugung · `33` Vermörtelung · `1000` Partieller Liner<br>  - Elternwert `3`: _(leer)_<br>  - Elternwert `4`: `1` Anschleuderverfahren · `2` Auspressverfahren · `4` Aufspritzverfahren · `6` Auskleidung mit vorgefertigten Teilen · `13` Kurzrohrverfahren · `14` Langrohrverfahren · `16` Montageverfahren · `18` Noppenbahnverfahren · `22` Reduktionsverfahren · `24` Rohrstrangverfahren · `25` Rückverformung · `27` Schlauchverfahren · `34` Wickelrohrverfahren<br>  - Elternwert `5`: _(leer)_<br>  - Elternwert `7`: _(leer)_<br>  - Elternwert `8`: _(leer)_<br>  - Elternwert `9`: _(leer)_<br>  - Elternwert `10`: _(leer)_<br>  - Elternwert `11`: _(leer)_<br>  - Elternwert `12`: _(leer)_ |
| 2 | Umfang | auswahl |  |  |  | `0` Unbekannt · `1` Gesamt · `2` Partiell · `3` Punktuell · `4` Punktuell, Muffe · `5` Punktuell, Abzweig/Stutzen |
| 3 | Sanierungsjahr | datum |  |  | Date |  |
| 4 | Profiltyp | auswahl |  |  |  | `1` Kreisprofil · `2` Eiprofil (H/B=3/2) · `3` Maulprofil (H/B = 1,66/2) · `4` Rechteckprofil (geschlossen) · `5` Kreisprofil (doppelwandig) · `6` Rechteckprofil (offen) · `7` Eiprofil (H/B ungleich 3/2) · `8` Maulprofil (H/B ungleich 1,66/2) · `9` Trapezprofil · `10` Doppeltrapezprofil · `11` U-förmig · `12` Bogenförmig · `13` Oval · `14` Rund · `15` Andere · `16` Eckig |
| 5 | Lichte Weite 1/2[mm] | text |  |  |  |  |
| 6 | Lichte Weite 1/2[mm] – 2[mm] *(abgeleitet; im WebGIS „“)* | text |  |  |  |  |
| 7 | Material | auswahl |  |  |  | `1` Asbestzement · `2` Beton · `3` Betonsegmente · `4` Edelstahl · `5` Eisen und Stahl, nicht identifiziert · `6` Faserzement · `7` Kunststoff, glasfaserverstärkt · `8` Grauguss · `9` Guss, duktil · `10` Kunststoff, nicht identifiziert · `11` Mauerwerk · `12` Ortsbeton · `13` Porosit · `14` Polymerbeton · `15` Zementbeton, polymermodifiziert · `16` Polyethylen · `17` Polyethylen hoher Dichte · `18` Polyesterharz · `19` Polyesterharzbeton · `20` Polypropylen · `21` Polyvinylchlorid · `22` Polyvinylchlorid, hart · `23` Stahlfaserbeton · `24` Spannbeton · `25` Stahlbeton · `26` Stahl · `27` Steinzeug · `28` Spritzbeton · `29` Werkstoff, nicht identifiziert · `30` Ziegelwerk · `31` Verschiedene · `32` Unbefestigt · `33` Rasen · `34` Pflaster |
| 8 | Fabrikat | auswahl |  |  |  | `0` unbekannt · `1` Alphaliner 500 · `2` Alphaliner 1500 · `3` Alphaliner 1500 HP · `4` BKP Beroliner liner · `5` Brandenburger Liner BB 2.5 · `6` Kansani warmhärtend · `7` iMPREG-Liner · `8` Insituform-Relining · `9` Saertex-Liner Typ S · `10` KFS-Liner · `11` Flexliner II Plus · `12` Flexliner II Plus/VFG · `13` Brawoliner XT · `14` RS Maxliner Flex mit MaxPox 15/40 · `15` RS Maxliner Flex mit MaxPox 15/40 · `16` Roboliner 10 · `17` KFS-Hausliner Epoxy · `18` UP Hausanschlussliner · `19` PL Flexliner EP 60 · `20` Brawoliner EP 60 · `21` Flexliner H · `22` Brawoliner · `23` Hächler EL 300/600 · `24` EL 300/600 · `25` KA-TE Roboter · `26` KA-TE / PMO Roboter · `27` Flexliner · `28` Flexoren · `29` PE Kurzrohr-Reliningsystem · `30` City-Liner · `31` IN-TEC Flex-Liner FAP 600 · `32` Kibag Inlinersystem · `33` EP Epoxydharz · `34` Brawoliner 3D · `35` IMPREG-Liner Härtung mit UV-Licht · `36` LinTEC ProFlex · `41` Alphaliner 1800H · `42` Biresin lineTEC ProFlex S EP 70 · `43` Bluelight PAA-F Liner · `44` Brawoliner EP 50 · `45` Brawoliner EP 60 · `46` Brawoliner XT EP 60 · `47` Epoxidharz · `48` Flexi SF Tube mit EP 50 · `49` Flexi tt 38s mit EP 50 · `50` Flexiliner H mit EP 70 · `51` IMPREG Liner GL16 · `52` Insituform-Liner 2.0 · `53` IPANEX Stopfmörtel · `54` Kerakoll Geolite 10 · `55` Manschette · `56` MC Fastpack Injekt  · `57` PCI Polyfix plus · `58` PL Flex 3D mit EP 60 · `59` PL Flexliner mit Combi-Tec EP 60 · `60` Quick-Lock |
| 9 | Hersteller | auswahl |  |  |  | `0` unbekannt · `1` arpe AG · `2` AITV SA · `3` ISS Kanal Services AG · `4` Jenni Kanalsanierungens AG · `5` Kanaltec AG · `6` Kansani AG · `7` KA-TE Insituform AG · `8` KFS Kanal-Service AG · `9` KIBAG Kanaltechnik AG · `10` AB Marti AG · `11` ABT Elseneri GmbH · `12` F& H Ablauf und Kanalsanierungen AG · `13` G.Landolt AG · `14` Hans Hächler Holding AG · `15` InnoService AG · `16` MÖKAH Kanalsanierungen AG · `17` RS-Technik · `18` KRT Kanal-Service AG · `19` AKS Kanalsanierung AG · `20` Schneider Kanalreinigung AG · `21` ITS Kanal Services AG · `22` Fretz Kanal-Service AG · `23` A. Arnold Tiefbau GmbH · `24` Akasan AG · `25` Arnold AG Heizung-Sanitär · `26` Arnold Bau-Allround · `27` Baugruppe Bristen GmbH · `28` Baumann Epp Bau AG · `29` Caviezel Canalizzazioni SA · `30` Cellere Bau AG · `31` Dubacher Schnellservice GmbH · `32` Elias Arnold Baugeschäft GmbH · `33` Epp / Jauch GmbH · `34` GAMMA AG Bau · `35` Gamma Gartenraum GmbH · `36` GEBR. BRUN AG · `37` Gislerbau GmbH · `38` GKS Cahenzli AG · `39` GLB Uri · `40` Hess Galabau AG · `41` Implenia Schweiz AG · `42` J. Tresch Sanitär-Heizung GmbH · `43` Josef Lussmann AG · `44` Kalbermatter AG · `45` Kanal total · `46` Käppeli, Strassen- und Tiefbau AG · `47` Koni Wyrsch Sanitär & Heizung · `48` M hoch3 AG · `49` Markus Enz AG · `50` Marti AG Bürglen · `51` Marty AG · `52` Otto Rohrunterhalt GmbH · `53` PK Bau AG Erstfeld · `54` PORR Suisse AG  · `55` Püntener Heizung - Sanitär · `56` Schelbert AG · `57` SPAG Schnyder, Plüss AG · `58` STRABAG AG · `59` Swissinliner GmbH · `60` Truttmann Transporte & Bauunternehmung AG · `61` Walo Bertschinger AG · `62` Walter Kempf, Heizungen und San. Anlagen · `63` Walter Marty AG · `64` Zurfluh Tiefbau GmbH |

### Metadaten

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Erstellt am (UTC) | datum |  | ● | Date |  |
| 2 | Erstellt von | text |  | ● |  |  |
| 3 | Geändert am (UTC) | datum |  | ● | Date |  |
| 4 | Geändert von | text |  | ● |  |  |


---

## 9. Unterhalt – Sanierungsmassnahme: Reparatur

Tabelle `AWZ_UNTERHALT`, Subtyp `art=2`, Maskentitel im WebGIS: „Sanierungsmassnahme: Reparatur“

17 Felder, davon 8 Auswahlfelder. Bereiche (Menüs) in Reihenfolge: **Kopf** → **Daten I** → **Metadaten**

### Kopf

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Bezeichnung | text |  |  |  |  |
| 2 | Art | auswahl |  |  |  | `0` Unbekannt · `8` Begehung · `3` Reinigung · `10` Dichtheitsprüfung · `2` Reparatur · `4` Renovierung · `5` Untersuchung · `1` Erneuerung · `9` Deformationsmessung · `11` Georadar · `7` Andere · `12` Kanalfernsehen |
| 3 | Status | auswahl |  |  |  | `0` Unbekannt · `1` Ausgeführt · `2` Geplant · `3` Nicht möglich · `4` Beauftragt · `5` Nicht beauftragt |
| 4 | Auftrag Nr./Bez. | verweis |  |  |  | → `AWZ_AUFTRAG` [nummer] / [bezeichnung] |

### Daten I

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Verfahren | auswahl |  |  |  | abhängig von **Art**:<br>  - Elternwert `0`: `0` Unbekannt · `35` Neues Verfahren<br>  - Elternwert `1`: `3` Austausch von Bauteilen · `7` Berstverfahren · `17` Vollständige Erneuerung · `21` Rohraustausch · `26` Rohrziehverfahren · `29` Teilerneuerung · `30` Überfahren<br>  - Elternwert `2`: `5` Aussenmanschetten · `8` Einbau von Abdichtungsstoffen · `9` Flutungsverfahren · `10` Injektionen der Leitungszone · `11` Innenmanschetten · `12` Injektion von Undichtigkeiten · `15` Lageregulierung Schachtabdeckung · `19` Oberflächenbehandlung · `20` Ortslaminate · `23` Roboterverfahren · `28` Schrumpfschläuche · `31` Verdrängungsverfahren · `32` Verfugung · `33` Vermörtelung · `1000` Partieller Liner<br>  - Elternwert `3`: _(leer)_<br>  - Elternwert `4`: `1` Anschleuderverfahren · `2` Auspressverfahren · `4` Aufspritzverfahren · `6` Auskleidung mit vorgefertigten Teilen · `13` Kurzrohrverfahren · `14` Langrohrverfahren · `16` Montageverfahren · `18` Noppenbahnverfahren · `22` Reduktionsverfahren · `24` Rohrstrangverfahren · `25` Rückverformung · `27` Schlauchverfahren · `34` Wickelrohrverfahren<br>  - Elternwert `5`: _(leer)_<br>  - Elternwert `7`: _(leer)_<br>  - Elternwert `8`: _(leer)_<br>  - Elternwert `9`: _(leer)_<br>  - Elternwert `10`: _(leer)_<br>  - Elternwert `11`: _(leer)_<br>  - Elternwert `12`: _(leer)_ |
| 2 | Umfang | auswahl |  |  |  | `0` Unbekannt · `1` Gesamt · `2` Partiell · `3` Punktuell · `4` Punktuell, Muffe · `5` Punktuell, Abzweig/Stutzen |
| 3 | Sanierungsjahr | datum |  |  | Date |  |
| 4 | Profiltyp | auswahl |  |  |  | `1` Kreisprofil · `2` Eiprofil (H/B=3/2) · `3` Maulprofil (H/B = 1,66/2) · `4` Rechteckprofil (geschlossen) · `5` Kreisprofil (doppelwandig) · `6` Rechteckprofil (offen) · `7` Eiprofil (H/B ungleich 3/2) · `8` Maulprofil (H/B ungleich 1,66/2) · `9` Trapezprofil · `10` Doppeltrapezprofil · `11` U-förmig · `12` Bogenförmig · `13` Oval · `14` Rund · `15` Andere · `16` Eckig |
| 5 | Lichte Weite 1/2[mm] | text |  |  |  |  |
| 6 | Lichte Weite 1/2[mm] – 2[mm] *(abgeleitet; im WebGIS „“)* | text |  |  |  |  |
| 7 | Material | auswahl |  |  |  | `1` Asbestzement · `2` Beton · `3` Betonsegmente · `4` Edelstahl · `5` Eisen und Stahl, nicht identifiziert · `6` Faserzement · `7` Kunststoff, glasfaserverstärkt · `8` Grauguss · `9` Guss, duktil · `10` Kunststoff, nicht identifiziert · `11` Mauerwerk · `12` Ortsbeton · `13` Porosit · `14` Polymerbeton · `15` Zementbeton, polymermodifiziert · `16` Polyethylen · `17` Polyethylen hoher Dichte · `18` Polyesterharz · `19` Polyesterharzbeton · `20` Polypropylen · `21` Polyvinylchlorid · `22` Polyvinylchlorid, hart · `23` Stahlfaserbeton · `24` Spannbeton · `25` Stahlbeton · `26` Stahl · `27` Steinzeug · `28` Spritzbeton · `29` Werkstoff, nicht identifiziert · `30` Ziegelwerk · `31` Verschiedene · `32` Unbefestigt · `33` Rasen · `34` Pflaster |
| 8 | Fabrikat | auswahl |  |  |  | `0` unbekannt · `1` Alphaliner 500 · `2` Alphaliner 1500 · `3` Alphaliner 1500 HP · `4` BKP Beroliner liner · `5` Brandenburger Liner BB 2.5 · `6` Kansani warmhärtend · `7` iMPREG-Liner · `8` Insituform-Relining · `9` Saertex-Liner Typ S · `10` KFS-Liner · `11` Flexliner II Plus · `12` Flexliner II Plus/VFG · `13` Brawoliner XT · `14` RS Maxliner Flex mit MaxPox 15/40 · `15` RS Maxliner Flex mit MaxPox 15/40 · `16` Roboliner 10 · `17` KFS-Hausliner Epoxy · `18` UP Hausanschlussliner · `19` PL Flexliner EP 60 · `20` Brawoliner EP 60 · `21` Flexliner H · `22` Brawoliner · `23` Hächler EL 300/600 · `24` EL 300/600 · `25` KA-TE Roboter · `26` KA-TE / PMO Roboter · `27` Flexliner · `28` Flexoren · `29` PE Kurzrohr-Reliningsystem · `30` City-Liner · `31` IN-TEC Flex-Liner FAP 600 · `32` Kibag Inlinersystem · `33` EP Epoxydharz · `34` Brawoliner 3D · `35` IMPREG-Liner Härtung mit UV-Licht · `36` LinTEC ProFlex · `41` Alphaliner 1800H · `42` Biresin lineTEC ProFlex S EP 70 · `43` Bluelight PAA-F Liner · `44` Brawoliner EP 50 · `45` Brawoliner EP 60 · `46` Brawoliner XT EP 60 · `47` Epoxidharz · `48` Flexi SF Tube mit EP 50 · `49` Flexi tt 38s mit EP 50 · `50` Flexiliner H mit EP 70 · `51` IMPREG Liner GL16 · `52` Insituform-Liner 2.0 · `53` IPANEX Stopfmörtel · `54` Kerakoll Geolite 10 · `55` Manschette · `56` MC Fastpack Injekt  · `57` PCI Polyfix plus · `58` PL Flex 3D mit EP 60 · `59` PL Flexliner mit Combi-Tec EP 60 · `60` Quick-Lock |
| 9 | Hersteller | auswahl |  |  |  | `0` unbekannt · `1` arpe AG · `2` AITV SA · `3` ISS Kanal Services AG · `4` Jenni Kanalsanierungens AG · `5` Kanaltec AG · `6` Kansani AG · `7` KA-TE Insituform AG · `8` KFS Kanal-Service AG · `9` KIBAG Kanaltechnik AG · `10` AB Marti AG · `11` ABT Elseneri GmbH · `12` F& H Ablauf und Kanalsanierungen AG · `13` G.Landolt AG · `14` Hans Hächler Holding AG · `15` InnoService AG · `16` MÖKAH Kanalsanierungen AG · `17` RS-Technik · `18` KRT Kanal-Service AG · `19` AKS Kanalsanierung AG · `20` Schneider Kanalreinigung AG · `21` ITS Kanal Services AG · `22` Fretz Kanal-Service AG · `23` A. Arnold Tiefbau GmbH · `24` Akasan AG · `25` Arnold AG Heizung-Sanitär · `26` Arnold Bau-Allround · `27` Baugruppe Bristen GmbH · `28` Baumann Epp Bau AG · `29` Caviezel Canalizzazioni SA · `30` Cellere Bau AG · `31` Dubacher Schnellservice GmbH · `32` Elias Arnold Baugeschäft GmbH · `33` Epp / Jauch GmbH · `34` GAMMA AG Bau · `35` Gamma Gartenraum GmbH · `36` GEBR. BRUN AG · `37` Gislerbau GmbH · `38` GKS Cahenzli AG · `39` GLB Uri · `40` Hess Galabau AG · `41` Implenia Schweiz AG · `42` J. Tresch Sanitär-Heizung GmbH · `43` Josef Lussmann AG · `44` Kalbermatter AG · `45` Kanal total · `46` Käppeli, Strassen- und Tiefbau AG · `47` Koni Wyrsch Sanitär & Heizung · `48` M hoch3 AG · `49` Markus Enz AG · `50` Marti AG Bürglen · `51` Marty AG · `52` Otto Rohrunterhalt GmbH · `53` PK Bau AG Erstfeld · `54` PORR Suisse AG  · `55` Püntener Heizung - Sanitär · `56` Schelbert AG · `57` SPAG Schnyder, Plüss AG · `58` STRABAG AG · `59` Swissinliner GmbH · `60` Truttmann Transporte & Bauunternehmung AG · `61` Walo Bertschinger AG · `62` Walter Kempf, Heizungen und San. Anlagen · `63` Walter Marty AG · `64` Zurfluh Tiefbau GmbH |

### Metadaten

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Erstellt am (UTC) | datum |  | ● | Date |  |
| 2 | Erstellt von | text |  | ● |  |  |
| 3 | Geändert am (UTC) | datum |  | ● | Date |  |
| 4 | Geändert von | text |  | ● |  |  |


---

## 10. Unterhalt – Sanierungsmassnahme: Renovierung

Tabelle `AWZ_UNTERHALT`, Subtyp `art=4`, Maskentitel im WebGIS: „Sanierungsmassnahme: Renovierung“

17 Felder, davon 8 Auswahlfelder. Bereiche (Menüs) in Reihenfolge: **Kopf** → **Daten I** → **Metadaten**

### Kopf

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Bezeichnung | text |  |  |  |  |
| 2 | Art | auswahl |  |  |  | `0` Unbekannt · `8` Begehung · `3` Reinigung · `10` Dichtheitsprüfung · `2` Reparatur · `4` Renovierung · `5` Untersuchung · `1` Erneuerung · `9` Deformationsmessung · `11` Georadar · `7` Andere · `12` Kanalfernsehen |
| 3 | Status | auswahl |  |  |  | `0` Unbekannt · `1` Ausgeführt · `2` Geplant · `3` Nicht möglich · `4` Beauftragt · `5` Nicht beauftragt |
| 4 | Auftrag Nr./Bez. | verweis |  |  |  | → `AWZ_AUFTRAG` [nummer] / [bezeichnung] |

### Daten I

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Verfahren | auswahl |  |  |  | abhängig von **Art**:<br>  - Elternwert `0`: `0` Unbekannt · `35` Neues Verfahren<br>  - Elternwert `1`: `3` Austausch von Bauteilen · `7` Berstverfahren · `17` Vollständige Erneuerung · `21` Rohraustausch · `26` Rohrziehverfahren · `29` Teilerneuerung · `30` Überfahren<br>  - Elternwert `2`: `5` Aussenmanschetten · `8` Einbau von Abdichtungsstoffen · `9` Flutungsverfahren · `10` Injektionen der Leitungszone · `11` Innenmanschetten · `12` Injektion von Undichtigkeiten · `15` Lageregulierung Schachtabdeckung · `19` Oberflächenbehandlung · `20` Ortslaminate · `23` Roboterverfahren · `28` Schrumpfschläuche · `31` Verdrängungsverfahren · `32` Verfugung · `33` Vermörtelung · `1000` Partieller Liner<br>  - Elternwert `3`: _(leer)_<br>  - Elternwert `4`: `1` Anschleuderverfahren · `2` Auspressverfahren · `4` Aufspritzverfahren · `6` Auskleidung mit vorgefertigten Teilen · `13` Kurzrohrverfahren · `14` Langrohrverfahren · `16` Montageverfahren · `18` Noppenbahnverfahren · `22` Reduktionsverfahren · `24` Rohrstrangverfahren · `25` Rückverformung · `27` Schlauchverfahren · `34` Wickelrohrverfahren<br>  - Elternwert `5`: _(leer)_<br>  - Elternwert `7`: _(leer)_<br>  - Elternwert `8`: _(leer)_<br>  - Elternwert `9`: _(leer)_<br>  - Elternwert `10`: _(leer)_<br>  - Elternwert `11`: _(leer)_<br>  - Elternwert `12`: _(leer)_ |
| 2 | Umfang | auswahl |  |  |  | `0` Unbekannt · `1` Gesamt · `2` Partiell · `3` Punktuell · `4` Punktuell, Muffe · `5` Punktuell, Abzweig/Stutzen |
| 3 | Sanierungsjahr | datum |  |  | Date |  |
| 4 | Profiltyp | auswahl |  |  |  | `1` Kreisprofil · `2` Eiprofil (H/B=3/2) · `3` Maulprofil (H/B = 1,66/2) · `4` Rechteckprofil (geschlossen) · `5` Kreisprofil (doppelwandig) · `6` Rechteckprofil (offen) · `7` Eiprofil (H/B ungleich 3/2) · `8` Maulprofil (H/B ungleich 1,66/2) · `9` Trapezprofil · `10` Doppeltrapezprofil · `11` U-förmig · `12` Bogenförmig · `13` Oval · `14` Rund · `15` Andere · `16` Eckig |
| 5 | Lichte Weite 1/2[mm] | text |  |  |  |  |
| 6 | Lichte Weite 1/2[mm] – 2[mm] *(abgeleitet; im WebGIS „“)* | text |  |  |  |  |
| 7 | Material | auswahl |  |  |  | `1` Asbestzement · `2` Beton · `3` Betonsegmente · `4` Edelstahl · `5` Eisen und Stahl, nicht identifiziert · `6` Faserzement · `7` Kunststoff, glasfaserverstärkt · `8` Grauguss · `9` Guss, duktil · `10` Kunststoff, nicht identifiziert · `11` Mauerwerk · `12` Ortsbeton · `13` Porosit · `14` Polymerbeton · `15` Zementbeton, polymermodifiziert · `16` Polyethylen · `17` Polyethylen hoher Dichte · `18` Polyesterharz · `19` Polyesterharzbeton · `20` Polypropylen · `21` Polyvinylchlorid · `22` Polyvinylchlorid, hart · `23` Stahlfaserbeton · `24` Spannbeton · `25` Stahlbeton · `26` Stahl · `27` Steinzeug · `28` Spritzbeton · `29` Werkstoff, nicht identifiziert · `30` Ziegelwerk · `31` Verschiedene · `32` Unbefestigt · `33` Rasen · `34` Pflaster |
| 8 | Fabrikat | auswahl |  |  |  | `0` unbekannt · `1` Alphaliner 500 · `2` Alphaliner 1500 · `3` Alphaliner 1500 HP · `4` BKP Beroliner liner · `5` Brandenburger Liner BB 2.5 · `6` Kansani warmhärtend · `7` iMPREG-Liner · `8` Insituform-Relining · `9` Saertex-Liner Typ S · `10` KFS-Liner · `11` Flexliner II Plus · `12` Flexliner II Plus/VFG · `13` Brawoliner XT · `14` RS Maxliner Flex mit MaxPox 15/40 · `15` RS Maxliner Flex mit MaxPox 15/40 · `16` Roboliner 10 · `17` KFS-Hausliner Epoxy · `18` UP Hausanschlussliner · `19` PL Flexliner EP 60 · `20` Brawoliner EP 60 · `21` Flexliner H · `22` Brawoliner · `23` Hächler EL 300/600 · `24` EL 300/600 · `25` KA-TE Roboter · `26` KA-TE / PMO Roboter · `27` Flexliner · `28` Flexoren · `29` PE Kurzrohr-Reliningsystem · `30` City-Liner · `31` IN-TEC Flex-Liner FAP 600 · `32` Kibag Inlinersystem · `33` EP Epoxydharz · `34` Brawoliner 3D · `35` IMPREG-Liner Härtung mit UV-Licht · `36` LinTEC ProFlex · `41` Alphaliner 1800H · `42` Biresin lineTEC ProFlex S EP 70 · `43` Bluelight PAA-F Liner · `44` Brawoliner EP 50 · `45` Brawoliner EP 60 · `46` Brawoliner XT EP 60 · `47` Epoxidharz · `48` Flexi SF Tube mit EP 50 · `49` Flexi tt 38s mit EP 50 · `50` Flexiliner H mit EP 70 · `51` IMPREG Liner GL16 · `52` Insituform-Liner 2.0 · `53` IPANEX Stopfmörtel · `54` Kerakoll Geolite 10 · `55` Manschette · `56` MC Fastpack Injekt  · `57` PCI Polyfix plus · `58` PL Flex 3D mit EP 60 · `59` PL Flexliner mit Combi-Tec EP 60 · `60` Quick-Lock |
| 9 | Hersteller | auswahl |  |  |  | `0` unbekannt · `1` arpe AG · `2` AITV SA · `3` ISS Kanal Services AG · `4` Jenni Kanalsanierungens AG · `5` Kanaltec AG · `6` Kansani AG · `7` KA-TE Insituform AG · `8` KFS Kanal-Service AG · `9` KIBAG Kanaltechnik AG · `10` AB Marti AG · `11` ABT Elseneri GmbH · `12` F& H Ablauf und Kanalsanierungen AG · `13` G.Landolt AG · `14` Hans Hächler Holding AG · `15` InnoService AG · `16` MÖKAH Kanalsanierungen AG · `17` RS-Technik · `18` KRT Kanal-Service AG · `19` AKS Kanalsanierung AG · `20` Schneider Kanalreinigung AG · `21` ITS Kanal Services AG · `22` Fretz Kanal-Service AG · `23` A. Arnold Tiefbau GmbH · `24` Akasan AG · `25` Arnold AG Heizung-Sanitär · `26` Arnold Bau-Allround · `27` Baugruppe Bristen GmbH · `28` Baumann Epp Bau AG · `29` Caviezel Canalizzazioni SA · `30` Cellere Bau AG · `31` Dubacher Schnellservice GmbH · `32` Elias Arnold Baugeschäft GmbH · `33` Epp / Jauch GmbH · `34` GAMMA AG Bau · `35` Gamma Gartenraum GmbH · `36` GEBR. BRUN AG · `37` Gislerbau GmbH · `38` GKS Cahenzli AG · `39` GLB Uri · `40` Hess Galabau AG · `41` Implenia Schweiz AG · `42` J. Tresch Sanitär-Heizung GmbH · `43` Josef Lussmann AG · `44` Kalbermatter AG · `45` Kanal total · `46` Käppeli, Strassen- und Tiefbau AG · `47` Koni Wyrsch Sanitär & Heizung · `48` M hoch3 AG · `49` Markus Enz AG · `50` Marti AG Bürglen · `51` Marty AG · `52` Otto Rohrunterhalt GmbH · `53` PK Bau AG Erstfeld · `54` PORR Suisse AG  · `55` Püntener Heizung - Sanitär · `56` Schelbert AG · `57` SPAG Schnyder, Plüss AG · `58` STRABAG AG · `59` Swissinliner GmbH · `60` Truttmann Transporte & Bauunternehmung AG · `61` Walo Bertschinger AG · `62` Walter Kempf, Heizungen und San. Anlagen · `63` Walter Marty AG · `64` Zurfluh Tiefbau GmbH |

### Metadaten

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Erstellt am (UTC) | datum |  | ● | Date |  |
| 2 | Erstellt von | text |  | ● |  |  |
| 3 | Geändert am (UTC) | datum |  | ● | Date |  |
| 4 | Geändert von | text |  | ● |  |  |


---

## 11. Unterhalt – Dichtheitsprüfung: Dichtheitsprüfung

Tabelle `AWZ_UNTERHALT`, Subtyp `art=10`, Maskentitel im WebGIS: „Dichtheitsprüfung: Dichtheitsprüfung“

28 Felder, davon 7 Auswahlfelder. Bereiche (Menüs) in Reihenfolge: **Kopf** → **Daten** → **Knoten** → **Haltung** → **Administrativ** → **Dokumente** → **Metadaten**

### Kopf

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Bezeichnung | text |  |  |  |  |
| 2 | Art | auswahl |  |  |  | `0` Unbekannt · `8` Begehung · `3` Reinigung · `10` Dichtheitsprüfung · `2` Reparatur · `4` Renovierung · `5` Untersuchung · `1` Erneuerung · `9` Deformationsmessung · `11` Georadar · `7` Andere · `12` Kanalfernsehen |
| 3 | Status | auswahl |  |  |  | `0` Unbekannt · `1` Ausgeführt · `2` Geplant · `3` Nicht möglich · `4` Beauftragt · `5` Nicht beauftragt |
| 4 | Auftrag Nr./Bez. | verweis |  |  |  | → `AWZ_AUFTRAG` [nummer] / [bezeichnung] |

### Daten

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Objektbezeichnung | text |  |  |  |  |
| 2 | Prüfgrund | auswahl |  |  |  | `0` Unbekannt · `1` Prüfung bestehender Anlagen · `2` Abnahme nach Neubau oder Sanierung |
| 3 | Prüfvorschrift | auswahl |  |  |  | `0` Unbekannt · `1` ATV-DVWK-M 143 Teil 6 · `2` DIN EN 1610 · `3` DIN EN 1610, ATV-DVWK-A 139 · `4` Merkblatt 4.3/6 Teil 2 · `5` DIN EN 12889 · `6` ATV-DVWK-A 142 |
| 4 | Verfahren/Umfang | auswahl |  |  |  | `0` Unbekannt · `1` Mit Überluftdruck · `2` Mit Unterluftdruck · `3` Mit Wasser |
| 5 | Verfahren/Umfang – Umfang *(abgeleitet; im WebGIS „“)* | auswahl |  |  |  | `0` Unbekannt · `1` Objekt · `2` Abschnittsweise · `3` Punktuell |
| 6 | Ausführende Firma | verweis |  |  |  | → `AWO_ORGANISATION` [bezeichnung] |
| 7 | Ausführender | text |  |  |  |  |
| 8 | Datum | datum |  |  | Date |  |
| 9 | Dauer [t] | text |  |  |  |  |
| 10 | Kosten | text |  |  |  |  |
| 11 | Bestanden – Kosten *(abgeleitet; im WebGIS „“)* | auswahl |  |  |  | `0` Unbekannt · `1` Ja · `2` Nein |
| 12 | Bemerkung | mehrzeilig |  |  |  |  |

### Knoten

**Aufklappliste „“** (Untermenü) – Tabelle `AWK_ABWASSERKNOTEN`, bearbeitbar, mehrere Zeilen; Spalten: Art · Subart · Bezeichnung

### Haltung

**Aufklappliste „“** (Untermenü) – Tabelle `AWK_HALTUNG`, bearbeitbar, mehrere Zeilen; Spalten: Bezeichnung · Nutzungsart · Funktionhierarchisch

### Administrativ

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Datengrundlage | text |  |  |  |  |
| 2 | Detaildaten | text |  |  |  |  |
| 3 | Strasse/Ortsteil | text |  |  |  |  |
| 4 | Strasse/Ortsteil – Ortsteil *(abgeleitet; im WebGIS „“)* | text |  |  |  |  |
| 5 | Geräteführer | text |  |  |  |  |
| 6 | Fahrzeug | text |  |  |  |  |
| 7 | Gerät | text |  |  |  |  |
| 8 | Inspizierte Länge [m] | text |  |  |  |  |

### Dokumente

**Aufklappliste „Dokumente“** (Untermenü) – Tabelle `AWK_DOKUMENT`, bearbeitbar, mehrere Zeilen; Spalten: Beschreibung · Pfad · Datum · dokument

### Metadaten

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Erstellt am (UTC) | datum |  | ● | Date |  |
| 2 | Erstellt von | text |  | ● |  |  |
| 3 | Geändert am (UTC) | datum |  | ● | Date |  |
| 4 | Geändert von | text |  | ● |  |  |


---

## 12. Unterhalt – Erhaltungsereignis: Reinigung

Tabelle `AWZ_UNTERHALT`, Subtyp `art=3`, Maskentitel im WebGIS: „Erhaltungsereignis: Reinigung“

18 Felder, davon 2 Auswahlfelder. Bereiche (Menüs) in Reihenfolge: **Kopf** → **Daten** → **Knoten** → **Haltung** → **Administrativ** → **Dokumente** → **Metadaten**

### Kopf

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Bezeichnung | text |  |  |  |  |
| 2 | Art | auswahl |  |  |  | `0` Unbekannt · `8` Begehung · `3` Reinigung · `10` Dichtheitsprüfung · `2` Reparatur · `4` Renovierung · `5` Untersuchung · `1` Erneuerung · `9` Deformationsmessung · `11` Georadar · `7` Andere · `12` Kanalfernsehen |
| 3 | Status | auswahl |  |  |  | `0` Unbekannt · `1` Ausgeführt · `2` Geplant · `3` Nicht möglich · `4` Beauftragt · `5` Nicht beauftragt |
| 4 | Auftrag Nr./Bez. | verweis |  |  |  | → `AWZ_AUFTRAG` [nummer] / [bezeichnung] |

### Daten

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Grund | text |  |  |  |  |
| 2 | Ergebnis | text |  |  |  |  |
| 3 | Zeitpunkt | datum |  |  | Date |  |
| 4 | Dauer [t] | text |  |  |  |  |
| 5 | Kosten | text |  |  |  |  |
| 6 | Bemerkung | mehrzeilig |  |  |  |  |

### Knoten

**Aufklappliste „“** (Untermenü) – Tabelle `AWK_ABWASSERKNOTEN`, bearbeitbar, mehrere Zeilen; Spalten: Art · Subart · Bezeichnung

### Haltung

**Aufklappliste „“** (Untermenü) – Tabelle `AWK_HALTUNG`, bearbeitbar, mehrere Zeilen; Spalten: Bezeichnung · Nutzungsart · Funktionhierarchisch

### Administrativ

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Datengrundlage | text |  |  |  |  |
| 2 | Detaildaten | text |  |  |  |  |
| 3 | Ausführender | verweis |  |  |  | → `AWO_ORGANISATION` [bezeichnung] |
| 4 | Ausführender | text |  |  |  |  |

### Dokumente

**Aufklappliste „Dokumente“** (Untermenü) – Tabelle `AWK_DOKUMENT`, bearbeitbar, mehrere Zeilen; Spalten: Beschreibung · Pfad · Datum · dokument

### Metadaten

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Erstellt am (UTC) | datum |  | ● | Date |  |
| 2 | Erstellt von | text |  | ● |  |  |
| 3 | Geändert am (UTC) | datum |  | ● | Date |  |
| 4 | Geändert von | text |  | ● |  |  |


---

## 13. Unterhalt – Erhaltungsereignis: Sanierung

Tabelle `AWZ_UNTERHALT`, Subtyp `art=6`, Maskentitel im WebGIS: „Erhaltungsereignis: Sanierung“

18 Felder, davon 2 Auswahlfelder. Bereiche (Menüs) in Reihenfolge: **Kopf** → **Daten** → **Knoten** → **Haltung** → **Administrativ** → **Dokumente** → **Metadaten**

### Kopf

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Bezeichnung | text |  |  |  |  |
| 2 | Art | auswahl |  |  |  | `0` Unbekannt · `8` Begehung · `3` Reinigung · `10` Dichtheitsprüfung · `2` Reparatur · `4` Renovierung · `5` Untersuchung · `1` Erneuerung · `9` Deformationsmessung · `11` Georadar · `7` Andere · `12` Kanalfernsehen |
| 3 | Status | auswahl |  |  |  | `0` Unbekannt · `1` Ausgeführt · `2` Geplant · `3` Nicht möglich · `4` Beauftragt · `5` Nicht beauftragt |
| 4 | Auftrag Nr./Bez. | verweis |  |  |  | → `AWZ_AUFTRAG` [nummer] / [bezeichnung] |

### Daten

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Grund | text |  |  |  |  |
| 2 | Ergebnis | text |  |  |  |  |
| 3 | Zeitpunkt | datum |  |  | Date |  |
| 4 | Dauer [t] | text |  |  |  |  |
| 5 | Kosten | text |  |  |  |  |
| 6 | Bemerkung | mehrzeilig |  |  |  |  |

### Knoten

**Aufklappliste „“** (Untermenü) – Tabelle `AWK_ABWASSERKNOTEN`, bearbeitbar, mehrere Zeilen; Spalten: Art · Subart · Bezeichnung

### Haltung

**Aufklappliste „“** (Untermenü) – Tabelle `AWK_HALTUNG`, bearbeitbar, mehrere Zeilen; Spalten: Bezeichnung · Nutzungsart · Funktionhierarchisch

### Administrativ

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Datengrundlage | text |  |  |  |  |
| 2 | Detaildaten | text |  |  |  |  |
| 3 | Ausführender | verweis |  |  |  | → `AWO_ORGANISATION` [bezeichnung] |
| 4 | Ausführender | text |  |  |  |  |

### Dokumente

**Aufklappliste „Dokumente“** (Untermenü) – Tabelle `AWK_DOKUMENT`, bearbeitbar, mehrere Zeilen; Spalten: Beschreibung · Pfad · Datum · dokument

### Metadaten

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Erstellt am (UTC) | datum |  | ● | Date |  |
| 2 | Erstellt von | text |  | ● |  |  |
| 3 | Geändert am (UTC) | datum |  | ● | Date |  |
| 4 | Geändert von | text |  | ● |  |  |


---

## 14. Unterhalt – Erhaltungsereignis: Andere

Tabelle `AWZ_UNTERHALT`, Subtyp `art=7`, Maskentitel im WebGIS: „Erhaltungsereignis: Andere“

18 Felder, davon 2 Auswahlfelder. Bereiche (Menüs) in Reihenfolge: **Kopf** → **Daten** → **Knoten** → **Haltung** → **Administrativ** → **Dokumente** → **Metadaten**

### Kopf

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Bezeichnung | text |  |  |  |  |
| 2 | Art | auswahl |  |  |  | `0` Unbekannt · `8` Begehung · `3` Reinigung · `10` Dichtheitsprüfung · `2` Reparatur · `4` Renovierung · `5` Untersuchung · `1` Erneuerung · `9` Deformationsmessung · `11` Georadar · `7` Andere · `12` Kanalfernsehen |
| 3 | Status | auswahl |  |  |  | `0` Unbekannt · `1` Ausgeführt · `2` Geplant · `3` Nicht möglich · `4` Beauftragt · `5` Nicht beauftragt |
| 4 | Auftrag Nr./Bez. | verweis |  |  |  | → `AWZ_AUFTRAG` [nummer] / [bezeichnung] |

### Daten

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Grund | text |  |  |  |  |
| 2 | Ergebnis | text |  |  |  |  |
| 3 | Zeitpunkt | datum |  |  | Date |  |
| 4 | Dauer [t] | text |  |  |  |  |
| 5 | Kosten | text |  |  |  |  |
| 6 | Bemerkung | mehrzeilig |  |  |  |  |

### Knoten

**Aufklappliste „“** (Untermenü) – Tabelle `AWK_ABWASSERKNOTEN`, bearbeitbar, mehrere Zeilen; Spalten: Art · Subart · Bezeichnung

### Haltung

**Aufklappliste „“** (Untermenü) – Tabelle `AWK_HALTUNG`, bearbeitbar, mehrere Zeilen; Spalten: Bezeichnung · Nutzungsart · Funktionhierarchisch

### Administrativ

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Datengrundlage | text |  |  |  |  |
| 2 | Detaildaten | text |  |  |  |  |
| 3 | Ausführender | verweis |  |  |  | → `AWO_ORGANISATION` [bezeichnung] |
| 4 | Ausführender | text |  |  |  |  |

### Dokumente

**Aufklappliste „Dokumente“** (Untermenü) – Tabelle `AWK_DOKUMENT`, bearbeitbar, mehrere Zeilen; Spalten: Beschreibung · Pfad · Datum · dokument

### Metadaten

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Erstellt am (UTC) | datum |  | ● | Date |  |
| 2 | Erstellt von | text |  | ● |  |  |
| 3 | Geändert am (UTC) | datum |  | ● | Date |  |
| 4 | Geändert von | text |  | ● |  |  |


---

## 15. Unterhalt – Erhaltungsereignis: Reinigung

Tabelle `AWZ_UNTERHALT`, Subtyp `art=3`, Maskentitel im WebGIS: „Erhaltungsereignis: Reinigung“

17 Felder, davon 2 Auswahlfelder. Bereiche (Menüs) in Reihenfolge: **Kopf** → **Daten** → **Administrativ** → **Dokumente** → **Metadaten**

### Kopf

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Bezeichnung | text |  |  |  |  |
| 2 | Art | auswahl |  |  |  | _(Werte nicht abrufbar)_ |
| 3 | Status | auswahl |  |  |  | _(Werte nicht abrufbar)_ |
| 4 | Auftrag | verweis |  |  |  | → `AWZ_AUFTRAG` [nummer] |

### Daten

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Grund | text |  |  |  |  |
| 2 | Ergebnis | text |  |  |  |  |
| 3 | Zeitpunkt | datum |  |  | Date |  |
| 4 | Dauer [t] | text |  |  |  |  |
| 5 | Kosten | text |  |  |  |  |
| 6 | Bemerkung | mehrzeilig |  |  |  |  |

### Administrativ

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Datengrundlage | text |  |  |  |  |
| 2 | Detaildaten | text |  |  |  |  |
| 3 | Ausführender | text |  |  |  |  |

### Dokumente

**Aufklappliste „Dokumente“** (Untermenü) – Tabelle `AWK_DOKUMENT`, bearbeitbar, mehrere Zeilen; Spalten: Beschreibung · Pfad · Datum · dokument

### Metadaten

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Erstellt am (UTC) | datum |  | ● | Date |  |
| 2 | Erstellt von | text |  | ● |  |  |
| 3 | Geändert am (UTC) | datum |  | ● | Date |  |
| 4 | Geändert von | text |  | ● |  |  |


---

## 16. Unterhalt – Erhaltungsereignis: Andere

Tabelle `AWZ_UNTERHALT`, Subtyp `art=7`, Maskentitel im WebGIS: „Erhaltungsereignis: Andere“

17 Felder, davon 2 Auswahlfelder. Bereiche (Menüs) in Reihenfolge: **Kopf** → **Daten** → **Administrativ** → **Dokumente** → **Metadaten**

### Kopf

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Bezeichnung | text |  |  |  |  |
| 2 | Art | auswahl |  |  |  | _(Werte nicht abrufbar)_ |
| 3 | Status | auswahl |  |  |  | _(Werte nicht abrufbar)_ |
| 4 | Auftrag | verweis |  |  |  | → `AWZ_AUFTRAG` [nummer] |

### Daten

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Grund | text |  |  |  |  |
| 2 | Ergebnis | text |  |  |  |  |
| 3 | Zeitpunkt | datum |  |  | Date |  |
| 4 | Dauer [t] | text |  |  |  |  |
| 5 | Kosten | text |  |  |  |  |
| 6 | Bemerkung | mehrzeilig |  |  |  |  |

### Administrativ

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Datengrundlage | text |  |  |  |  |
| 2 | Detaildaten | text |  |  |  |  |
| 3 | Ausführender | text |  |  |  |  |

### Dokumente

**Aufklappliste „Dokumente“** (Untermenü) – Tabelle `AWK_DOKUMENT`, bearbeitbar, mehrere Zeilen; Spalten: Beschreibung · Pfad · Datum · dokument

### Metadaten

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Erstellt am (UTC) | datum |  | ● | Date |  |
| 2 | Erstellt von | text |  | ● |  |  |
| 3 | Geändert am (UTC) | datum |  | ● | Date |  |
| 4 | Geändert von | text |  | ● |  |  |


---

## 17. Unterhalt – Erhaltungsereignis: Kanalfernsehen

Tabelle `AWZ_UNTERHALT`, Subtyp `art=12`, Maskentitel im WebGIS: „Erhaltungsereignis: Kanalfernsehen“

17 Felder, davon 2 Auswahlfelder. Bereiche (Menüs) in Reihenfolge: **Kopf** → **Daten** → **Administrativ** → **Dokumente** → **Metadaten**

### Kopf

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Bezeichnung | text |  |  |  |  |
| 2 | Art | auswahl |  |  |  | `0` Unbekannt · `8` Begehung · `3` Reinigung · `10` Dichtheitsprüfung · `2` Reparatur · `4` Renovierung · `5` Untersuchung · `1` Erneuerung · `9` Deformationsmessung · `11` Georadar · `7` Andere · `12` Kanalfernsehen |
| 3 | Status | auswahl |  |  |  | `0` Unbekannt · `1` Ausgeführt · `2` Geplant · `3` Nicht möglich · `4` Beauftragt · `5` Nicht beauftragt |
| 4 | Auftrag | verweis |  |  |  | → `AWZ_AUFTRAG` [nummer] |

### Daten

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Grund | text |  |  |  |  |
| 2 | Ergebnis | text |  |  |  |  |
| 3 | Zeitpunkt | datum |  |  | Date |  |
| 4 | Dauer [t] | text |  |  |  |  |
| 5 | Kosten | text |  |  |  |  |
| 6 | Bemerkung | mehrzeilig |  |  |  |  |

### Administrativ

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Datengrundlage | text |  |  |  |  |
| 2 | Detaildaten | text |  |  |  |  |
| 3 | Ausführender | text |  |  |  |  |

### Dokumente

**Aufklappliste „Dokumente“** (Untermenü) – Tabelle `AWK_DOKUMENT`, bearbeitbar, mehrere Zeilen; Spalten: Beschreibung · Pfad · Datum · dokument

### Metadaten

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Erstellt am (UTC) | datum |  | ● | Date |  |
| 2 | Erstellt von | text |  | ● |  |  |
| 3 | Geändert am (UTC) | datum |  | ● | Date |  |
| 4 | Geändert von | text |  | ● |  |  |


---

## 18. Unterhalt – Untersuchung: Untersuchung

Tabelle `AWZ_UNTERHALT`, Subtyp `art=5`, Maskentitel im WebGIS: „Untersuchung: Untersuchung“

22 Felder, davon 3 Auswahlfelder. Bereiche (Menüs) in Reihenfolge: **Kopf** → **Daten** → **Knoten** → **Haltung** → **Administrativ** → **Dokumente** → **Metadaten**

### Kopf

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Bezeichnung | text |  |  |  |  |
| 2 | Art | auswahl |  |  |  | `0` Unbekannt · `8` Begehung · `3` Reinigung · `10` Dichtheitsprüfung · `2` Reparatur · `4` Renovierung · `5` Untersuchung · `1` Erneuerung · `9` Deformationsmessung · `11` Georadar · `7` Andere · `12` Kanalfernsehen |
| 3 | Status | auswahl |  |  |  | `0` Unbekannt · `1` Ausgeführt · `2` Geplant · `3` Nicht möglich · `4` Beauftragt · `5` Nicht beauftragt |
| 4 | Auftrag Nr./Bez. | verweis |  |  |  | → `AWZ_AUFTRAG` [nummer] / [bezeichnung] |

### Daten

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Grund | text |  |  |  |  |
| 2 | Ergebnis | text |  |  |  |  |
| 3 | Zeitpunkt | datum |  |  | Date |  |
| 4 | Dauer [t] | text |  |  |  |  |
| 5 | Kosten | text |  |  |  |  |
| 6 | Bemerkung | mehrzeilig |  |  |  |  |

### Knoten

**Aufklappliste „“** (Untermenü) – Tabelle `AWK_ABWASSERKNOTEN`, bearbeitbar, mehrere Zeilen; Spalten: Art · Subart · Bezeichnung

### Haltung

**Aufklappliste „“** (Untermenü) – Tabelle `AWK_HALTUNG`, bearbeitbar, mehrere Zeilen; Spalten: Bezeichnung · Nutzungsart · Funktionhierarchisch

### Administrativ

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Datengrundlage | text |  |  |  |  |
| 2 | Detaildaten | text |  |  |  |  |
| 3 | Ausführende Firma | verweis |  |  |  | → `AWO_ORGANISATION` [bezeichnung] |
| 4 | Ausführender | text |  |  |  |  |
| 5 | Witterung | auswahl |  |  |  | `0` Unbekannt · `1` Kein Niederschlag · `2` Regen · `3` Schmelzwasser · `4` Bedeckt, regnerisch · `5` Nieselregen · `6` Schneefall · `7` Schön, trocken · `8` Frost |
| 6 | Fahrzeug | text |  |  |  |  |
| 7 | Gerät | text |  |  |  |  |
| 8 | Inspizierte Länge [m] | text |  |  |  |  |

### Dokumente

**Aufklappliste „Dokumente“** (Untermenü) – Tabelle `AWK_DOKUMENT`, bearbeitbar, mehrere Zeilen; Spalten: Beschreibung · Pfad · Datum · dokument

### Metadaten

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Erstellt am (UTC) | datum |  | ● | Date |  |
| 2 | Erstellt von | text |  | ● |  |  |
| 3 | Geändert am (UTC) | datum |  | ● | Date |  |
| 4 | Geändert von | text |  | ● |  |  |


---

## 19. Unterhalt – Begehung: Begehung

Tabelle `AWZ_UNTERHALT`, Subtyp `art=8`, Maskentitel im WebGIS: „Begehung: Begehung“

19 Felder, davon 3 Auswahlfelder. Bereiche (Menüs) in Reihenfolge: **Kopf** → **Daten** → **Knoten** → **Haltung** → **Administrativ** → **Dokumente** → **Metadaten**

### Kopf

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Bezeichnung | text |  |  |  |  |
| 2 | Art | auswahl |  |  |  | `0` Unbekannt · `8` Begehung · `3` Reinigung · `10` Dichtheitsprüfung · `2` Reparatur · `4` Renovierung · `5` Untersuchung · `1` Erneuerung · `9` Deformationsmessung · `11` Georadar · `7` Andere · `12` Kanalfernsehen |
| 3 | Status | auswahl |  |  |  | `0` Unbekannt · `1` Ausgeführt · `2` Geplant · `3` Nicht möglich · `4` Beauftragt · `5` Nicht beauftragt |
| 4 | Auftrag Nr./Bez. | verweis |  |  |  | → `AWZ_AUFTRAG` [nummer] / [bezeichnung] |

### Daten

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Grund | text |  |  |  |  |
| 2 | Ergebnis | text |  |  |  |  |
| 3 | Zeitpunkt | datum |  |  | Date |  |
| 4 | Dauer [t] | text |  |  |  |  |
| 5 | Kosten | text |  |  |  |  |
| 6 | Bemerkung | mehrzeilig |  |  |  |  |

### Knoten

**Aufklappliste „“** (Untermenü) – Tabelle `AWK_ABWASSERKNOTEN`, bearbeitbar, mehrere Zeilen; Spalten: Art · Subart · Bezeichnung

### Haltung

**Aufklappliste „“** (Untermenü) – Tabelle `AWK_HALTUNG`, bearbeitbar, mehrere Zeilen; Spalten: Bezeichnung · Nutzungsart · Funktionhierarchisch

### Administrativ

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Datengrundlage | text |  |  |  |  |
| 2 | Detaildaten | text |  |  |  |  |
| 3 | Ausführende Firma | verweis |  |  |  | → `AWO_ORGANISATION` [bezeichnung] |
| 4 | Ausführender | text |  |  |  |  |
| 5 | Witterung | auswahl |  |  |  | `0` Unbekannt · `1` Kein Niederschlag · `2` Regen · `3` Schmelzwasser · `4` Bedeckt, regnerisch · `5` Nieselregen · `6` Schneefall · `7` Schön, trocken · `8` Frost |

### Dokumente

**Aufklappliste „Dokumente“** (Untermenü) – Tabelle `AWK_DOKUMENT`, bearbeitbar, mehrere Zeilen; Spalten: Beschreibung · Pfad · Datum · dokument

### Metadaten

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Erstellt am (UTC) | datum |  | ● | Date |  |
| 2 | Erstellt von | text |  | ● |  |  |
| 3 | Geändert am (UTC) | datum |  | ● | Date |  |
| 4 | Geändert von | text |  | ● |  |  |


---

## 20. Unterhalt – Untersuchung: Deformationsmessung

Tabelle `AWZ_UNTERHALT`, Subtyp `art=9`, Maskentitel im WebGIS: „Untersuchung: Deformationsmessung“

22 Felder, davon 3 Auswahlfelder. Bereiche (Menüs) in Reihenfolge: **Kopf** → **Daten** → **Knoten** → **Haltung** → **Administrativ** → **Dokumente** → **Metadaten**

### Kopf

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Bezeichnung | text |  |  |  |  |
| 2 | Art | auswahl |  |  |  | `0` Unbekannt · `8` Begehung · `3` Reinigung · `10` Dichtheitsprüfung · `2` Reparatur · `4` Renovierung · `5` Untersuchung · `1` Erneuerung · `9` Deformationsmessung · `11` Georadar · `7` Andere · `12` Kanalfernsehen |
| 3 | Status | auswahl |  |  |  | `0` Unbekannt · `1` Ausgeführt · `2` Geplant · `3` Nicht möglich · `4` Beauftragt · `5` Nicht beauftragt |
| 4 | Auftrag Nr./Bez. | verweis |  |  |  | → `AWZ_AUFTRAG` [nummer] / [bezeichnung] |

### Daten

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Grund | text |  |  |  |  |
| 2 | Ergebnis | text |  |  |  |  |
| 3 | Zeitpunkt | datum |  |  | Date |  |
| 4 | Dauer [t] | text |  |  |  |  |
| 5 | Kosten | text |  |  |  |  |
| 6 | Bemerkung | mehrzeilig |  |  |  |  |

### Knoten

**Aufklappliste „“** (Untermenü) – Tabelle `AWK_ABWASSERKNOTEN`, bearbeitbar, mehrere Zeilen; Spalten: Art · Subart · Bezeichnung

### Haltung

**Aufklappliste „“** (Untermenü) – Tabelle `AWK_HALTUNG`, bearbeitbar, mehrere Zeilen; Spalten: Bezeichnung · Nutzungsart · Funktionhierarchisch

### Administrativ

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Datengrundlage | text |  |  |  |  |
| 2 | Detaildaten | text |  |  |  |  |
| 3 | Ausführende Firma | verweis |  |  |  | → `AWO_ORGANISATION` [bezeichnung] |
| 4 | Ausführender | text |  |  |  |  |
| 5 | Witterung | auswahl |  |  |  | `0` Unbekannt · `1` Kein Niederschlag · `2` Regen · `3` Schmelzwasser · `4` Bedeckt, regnerisch · `5` Nieselregen · `6` Schneefall · `7` Schön, trocken · `8` Frost |
| 6 | Fahrzeug | text |  |  |  |  |
| 7 | Gerät | text |  |  |  |  |
| 8 | Inspizierte Länge [m] | text |  |  |  |  |

### Dokumente

**Aufklappliste „Dokumente“** (Untermenü) – Tabelle `AWK_DOKUMENT`, bearbeitbar, mehrere Zeilen; Spalten: Beschreibung · Pfad · Datum · dokument

### Metadaten

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Erstellt am (UTC) | datum |  | ● | Date |  |
| 2 | Erstellt von | text |  | ● |  |  |
| 3 | Geändert am (UTC) | datum |  | ● | Date |  |
| 4 | Geändert von | text |  | ● |  |  |


---

## 21. Unterhalt – Untersuchung: Georadar

Tabelle `AWZ_UNTERHALT`, Subtyp `art=11`, Maskentitel im WebGIS: „Untersuchung: Georadar“

22 Felder, davon 3 Auswahlfelder. Bereiche (Menüs) in Reihenfolge: **Kopf** → **Daten** → **Knoten** → **Haltung** → **Administrativ** → **Dokumente** → **Metadaten**

### Kopf

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Bezeichnung | text |  |  |  |  |
| 2 | Art | auswahl |  |  |  | `0` Unbekannt · `8` Begehung · `3` Reinigung · `10` Dichtheitsprüfung · `2` Reparatur · `4` Renovierung · `5` Untersuchung · `1` Erneuerung · `9` Deformationsmessung · `11` Georadar · `7` Andere · `12` Kanalfernsehen |
| 3 | Status | auswahl |  |  |  | `0` Unbekannt · `1` Ausgeführt · `2` Geplant · `3` Nicht möglich · `4` Beauftragt · `5` Nicht beauftragt |
| 4 | Auftrag Nr./Bez. | verweis |  |  |  | → `AWZ_AUFTRAG` [nummer] / [bezeichnung] |

### Daten

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Grund | text |  |  |  |  |
| 2 | Ergebnis | text |  |  |  |  |
| 3 | Zeitpunkt | datum |  |  | Date |  |
| 4 | Dauer [t] | text |  |  |  |  |
| 5 | Kosten | text |  |  |  |  |
| 6 | Bemerkung | mehrzeilig |  |  |  |  |

### Knoten

**Aufklappliste „“** (Untermenü) – Tabelle `AWK_ABWASSERKNOTEN`, bearbeitbar, mehrere Zeilen; Spalten: Art · Subart · Bezeichnung

### Haltung

**Aufklappliste „“** (Untermenü) – Tabelle `AWK_HALTUNG`, bearbeitbar, mehrere Zeilen; Spalten: Bezeichnung · Nutzungsart · Funktionhierarchisch

### Administrativ

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Datengrundlage | text |  |  |  |  |
| 2 | Detaildaten | text |  |  |  |  |
| 3 | Ausführende Firma | verweis |  |  |  | → `AWO_ORGANISATION` [bezeichnung] |
| 4 | Ausführender | text |  |  |  |  |
| 5 | Witterung | auswahl |  |  |  | `0` Unbekannt · `1` Kein Niederschlag · `2` Regen · `3` Schmelzwasser · `4` Bedeckt, regnerisch · `5` Nieselregen · `6` Schneefall · `7` Schön, trocken · `8` Frost |
| 6 | Fahrzeug | text |  |  |  |  |
| 7 | Gerät | text |  |  |  |  |
| 8 | Inspizierte Länge [m] | text |  |  |  |  |

### Dokumente

**Aufklappliste „Dokumente“** (Untermenü) – Tabelle `AWK_DOKUMENT`, bearbeitbar, mehrere Zeilen; Spalten: Beschreibung · Pfad · Datum · dokument

### Metadaten

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Erstellt am (UTC) | datum |  | ● | Date |  |
| 2 | Erstellt von | text |  | ● |  |  |
| 3 | Geändert am (UTC) | datum |  | ● | Date |  |
| 4 | Geändert von | text |  | ● |  |  |


---

## 22. Inspektion Haltung

Tabelle `AWZ_INSPEKTION_HALTUNG`, Maskentitel im WebGIS: „Inspektion Haltung“

56 Felder, davon 23 Auswahlfelder. Bereiche (Menüs) in Reihenfolge: **Kopf** → **Daten I** → **Daten II** → **Bewertung** → **Administrativ** → **Dokumente** → **Anschluss** → **Video** → **Schäden** → **Metadaten**

### Kopf

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Nummer | text |  |  |  |  |
| 2 | Auftrag Nr./Bez. | verweis |  |  |  | → `AWZ_AUFTRAG` [nummer] / [bezeichnung] |
| 3 | Bez. Schacht (von/bis) | text | ● |  |  |  |
| 4 | Bez. Schacht (von/bis) – bis) *(abgeleitet; im WebGIS „“)* | text | ● |  |  |  |
| 5 | Haltung | text | ● |  |  |  |

**Aufklappliste „Haltungen“** (Untermenü) – Tabelle `AWK_HALTUNG`, bearbeitbar, mehrere Zeilen; Spalten: Bezeichnung · Funktionhierarchisch

### Daten I

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Verfahren/Richtung | auswahl |  |  |  | `0` Unbekannt · `1` TV-Untersuchung · `2` Begehung · `3` Vom Schacht · `4` Andere · `5` Deformationsmessung · `6` Dichtheitsprüfung · `7` Georadar |
| 2 | Verfahren/Richtung – Richtung *(abgeleitet; im WebGIS „“)* | auswahl | ● |  |  | `0` Unbekannt · `1` Von oben · `2` Von unten |
| 3 | Länge/Bezugspunkt | zahl |  |  | Float |  |
| 4 | Länge/Bezugspunkt – Bezugspunkt *(abgeleitet; im WebGIS „“)* | auswahl |  |  |  | `0` Unbekannt · `1` Mittelpunkt Startschacht · `2` Innenseite der Wand am Anfangsknoten · `3` Scheitel des Haltungsendes innerhalb des Anfangsknotens · `4` Mittelpunkt zwischen ankommender und abgehender Leitung · `5` Andere |
| 5 | Datum/Zeit | datum |  |  | Date |  |
| 6 | Wasserhaltung | auswahl |  |  |  | `0` Unbekannt · `1` Keine · `2` Von oberhalb abgesperrt · `3` Von oberhalb teilweise abgesperrt · `4` Seitenzuläufe abgesperrt · `5` Von unterhalb abgesperrt · `6` Andere |
| 7 | Vorreinigung *(abgeleitet; im WebGIS „“)* | auswahl |  |  |  | `0` Unbekannt · `1` Ja · `2` Nein |
| 8 | Bemerkung | mehrzeilig |  |  |  |  |

### Daten II

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Profiltyp/Breite/Höhe | auswahl |  |  |  | `1` Kreisprofil · `2` Eiprofil (H/B=3/2) · `3` Maulprofil (H/B = 1,66/2) · `4` Rechteckprofil (geschlossen) · `5` Kreisprofil (doppelwandig) · `6` Rechteckprofil (offen) · `7` Eiprofil (H/B ungleich 3/2) · `8` Maulprofil (H/B ungleich 1,66/2) · `9` Trapezprofil · `10` Doppeltrapezprofil · `11` U-förmig · `12` Bogenförmig · `13` Oval · `14` Rund · `15` Andere · `16` Eckig |
| 2 | [mm] | auswahl |  |  |  | `0` Unbekannt · `40` 40 · `45` 45 · `50` 50 · `60` 60 · `63` 63 · `65` 65 · `70` 70 · `75` 75 · `80` 80 · `85` 85 · `90` 90 · `95` 95 · `100` 100 · `102` 102 · `103` 103 · `104` 104 · `110` 110 · `115` 115 · `117` 117 · `118` 118 · `119` 119 · `120` 120 · `121` 121 · `125` 125 · `132` 132 · `140` 140 · `148` 148 · `149` 149 · `150` 150 · `153` 153 · `154` 154 · `155` 155 · `160` 160 · `166` 166 · `170` 170 · `175` 175 · `180` 180 · `185` 185 · `186` 186 · `188` 188 · `191` 191 · `192` 192 · `200` 200 · `210` 210 · `211` 211 · `220` 220 · `225` 225 · `229` 229 · `231` 231 · `233` 233 · `235` 235 · `239` 239 · `240` 240 · `242` 242 · `250` 250 · `263` 263 · `270` 270 · `280` 280 · `284` 284 · `291` 291 · `296` 296 · `298` 298 · `300` 300 · `301` 301 · `303` 303 · `315` 315 · `320` 320 · `328` 328 · `333` 333 · `341` 341 · `350` 350 · `352` 352 · `355` 355 · `369` 369 · `375` 375 · `380` 380 · `384` 384 · `400` 400 · `406` 406 · `435` 435 · `440` 440 · `450` 450 · `464` 464 · `466` 466 · `480` 480 · `500` 500 · `550` 550 · `560` 560 · `580` 580 · `591` 591 · `600` 600 · `605` 605 · `610` 610 · `620` 620 · `630` 630 · `650` 650 · `670` 670 · `680` 680 · `700` 700 · `720` 720 · `730` 730 · `750` 750 · `760` 760 · `780` 780 · `800` 800 · `820` 820 · `850` 850 · `880` 880 · `900` 900 · `950` 950 · `990` 990 · `1000` 1000 · `1030` 1030 · `1050` 1050 · `1070` 1070 · `1100` 1100 · `1150` 1150 · `1180` 1180 · `1200` 1200 · `1210` 1210 · `1220` 1220 · `1240` 1240 · `1250` 1250 · `1260` 1260 · `1280` 1280 · `1300` 1300 · `1320` 1320 · `1350` 1350 · `1380` 1380 · `1400` 1400 · `1420` 1420 · `1450` 1450 · `1460` 1460 · `1470` 1470 · `1480` 1480 · `1490` 1490 · `1500` 1500 · `1550` 1550 · `1600` 1600 · `1610` 1610 · `1650` 1650 · `1660` 1660 · `1670` 1670 · `1690` 1690 · `1700` 1700 · `1720` 1720 · `1750` 1750 · `1760` 1760 · `1780` 1780 · `1790` 1790 · `1800` 1800 · `1830` 1830 · `1850` 1850 · `1870` 1870 · `1900` 1900 · `1950` 1950 · `1980` 1980 · `2000` 2000 · `2010` 2010 · `2050` 2050 · `2100` 2100 · `2200` 2200 · `2240` 2240 · `2250` 2250 · `2280` 2280 · `2300` 2300 · `2310` 2310 · `2320` 2320 · `2350` 2350 · `2400` 2400 · `2500` 2500 · `2600` 2600 · `2700` 2700 · `2800` 2800 · `3000` 3000 · `3150` 3150 · `3200` 3200 · `3250` 3250 · `3500` 3500 · `3600` 3600 · `4000` 4000 · `4500` 4500 · `4600` 4600 · `5000` 5000 · `5250` 5250 · `5300` 5300 |
| 3 | [mm] – 2. Feld *(abgeleitet; im WebGIS „“)* | auswahl |  |  |  | `0` Unbekannt · `40` 40 · `45` 45 · `50` 50 · `60` 60 · `63` 63 · `65` 65 · `70` 70 · `75` 75 · `80` 80 · `85` 85 · `90` 90 · `95` 95 · `100` 100 · `102` 102 · `103` 103 · `104` 104 · `110` 110 · `115` 115 · `117` 117 · `118` 118 · `119` 119 · `120` 120 · `121` 121 · `125` 125 · `132` 132 · `140` 140 · `148` 148 · `149` 149 · `150` 150 · `153` 153 · `154` 154 · `155` 155 · `160` 160 · `166` 166 · `170` 170 · `175` 175 · `180` 180 · `185` 185 · `186` 186 · `188` 188 · `191` 191 · `192` 192 · `200` 200 · `210` 210 · `211` 211 · `220` 220 · `225` 225 · `229` 229 · `231` 231 · `233` 233 · `235` 235 · `239` 239 · `240` 240 · `242` 242 · `250` 250 · `263` 263 · `270` 270 · `280` 280 · `284` 284 · `291` 291 · `296` 296 · `298` 298 · `300` 300 · `301` 301 · `303` 303 · `315` 315 · `320` 320 · `328` 328 · `333` 333 · `341` 341 · `350` 350 · `352` 352 · `355` 355 · `369` 369 · `375` 375 · `380` 380 · `384` 384 · `400` 400 · `406` 406 · `435` 435 · `440` 440 · `450` 450 · `464` 464 · `466` 466 · `480` 480 · `500` 500 · `550` 550 · `560` 560 · `580` 580 · `591` 591 · `600` 600 · `605` 605 · `610` 610 · `620` 620 · `630` 630 · `650` 650 · `670` 670 · `680` 680 · `700` 700 · `720` 720 · `730` 730 · `750` 750 · `760` 760 · `780` 780 · `800` 800 · `820` 820 · `850` 850 · `880` 880 · `900` 900 · `950` 950 · `990` 990 · `1000` 1000 · `1030` 1030 · `1050` 1050 · `1070` 1070 · `1100` 1100 · `1150` 1150 · `1180` 1180 · `1200` 1200 · `1210` 1210 · `1220` 1220 · `1240` 1240 · `1250` 1250 · `1260` 1260 · `1280` 1280 · `1300` 1300 · `1320` 1320 · `1350` 1350 · `1380` 1380 · `1400` 1400 · `1420` 1420 · `1450` 1450 · `1460` 1460 · `1470` 1470 · `1480` 1480 · `1490` 1490 · `1500` 1500 · `1550` 1550 · `1600` 1600 · `1610` 1610 · `1650` 1650 · `1660` 1660 · `1670` 1670 · `1690` 1690 · `1700` 1700 · `1720` 1720 · `1750` 1750 · `1760` 1760 · `1780` 1780 · `1790` 1790 · `1800` 1800 · `1830` 1830 · `1850` 1850 · `1870` 1870 · `1900` 1900 · `1950` 1950 · `1980` 1980 · `2000` 2000 · `2010` 2010 · `2050` 2050 · `2100` 2100 · `2200` 2200 · `2240` 2240 · `2250` 2250 · `2280` 2280 · `2300` 2300 · `2310` 2310 · `2320` 2320 · `2350` 2350 · `2400` 2400 · `2500` 2500 · `2600` 2600 · `2700` 2700 · `2800` 2800 · `3000` 3000 · `3150` 3150 · `3200` 3200 · `3250` 3250 · `3500` 3500 · `3600` 3600 · `4000` 4000 · `4500` 4500 · `4600` 4600 · `5000` 5000 · `5250` 5250 · `5300` 5300 |
| 4 | Herkunft Profilmasse | auswahl |  |  |  | `0` Unbekannt · `1` Aus Plänen · `2` Aus Stammdaten · `3` Gemessen am Knoten · `4` Querschnittsvermessung |
| 5 | Funktion hydr. | auswahl |  |  |  | `0` Unbekannt · `4` Freispiegelleitung · `5` Pumpendruckleitung · `101` Drainagetransportleitung · `102` Drosselleitung · `103` Dükerleitung · `106` Sickerleitung · `107` Speicherleitung · `108` Spülleitung · `111` Andere · `116` Vakuumleitung · `1100` Belagsrinne Wasserschale · `1101` Entwaesserungsgraben befestigt · `1102` Entwaesserungsgraben unbefestigt · `1103` Schlitzrinne · `1104` Wasserrinne mit Rost |
| 6 | Nutzungsart | auswahl |  |  |  | `0` Unbekannt · `4` Mischabwasser · `5` Regenabwasser · `106` Reinabwasser · `7` Schmutzabwasser · `1` Bachabwasser · `102` Entlastetes Mischabwasser · `103` Industrieabwasser · `111` Andere · `1100` Bergwasser · `1101` Strassenabwasser |
| 7 | Material/Herkunft | auswahl |  |  |  | `1` Asbestzement · `2` Beton · `3` Betonsegmente · `4` Edelstahl · `5` Eisen und Stahl, nicht identifiziert · `6` Faserzement · `7` Kunststoff, glasfaserverstärkt · `8` Grauguss · `9` Guss, duktil · `10` Kunststoff, nicht identifiziert · `11` Mauerwerk · `12` Ortsbeton · `13` Porosit · `14` Polymerbeton · `15` Zementbeton, polymermodifiziert · `16` Polyethylen · `17` Polyethylen hoher Dichte · `18` Polyesterharz · `19` Polyesterharzbeton · `20` Polypropylen · `21` Polyvinylchlorid · `22` Polyvinylchlorid, hart · `23` Stahlfaserbeton · `24` Spannbeton · `25` Stahlbeton · `26` Stahl · `27` Steinzeug · `28` Spritzbeton · `29` Werkstoff, nicht identifiziert · `30` Ziegelwerk · `31` Verschiedene · `32` Unbefestigt · `33` Rasen · `34` Pflaster |
| 8 | Material/Herkunft – Herkunft *(abgeleitet; im WebGIS „“)* | auswahl |  |  |  | `0` Unbekannt · `1` Aus Plänen · `2` Aus Stammdaten · `3` Feststellung vor Ort |
| 9 | Innenschutz | auswahl |  |  |  | `1` Anstrich oder Beschichtung: Gesamter Innenraum · `2` Anstrich oder Beschichtung: Sohle · `3` Anstrich oder Beschichtung: Wandung · `4` Bitumenanstrich: Gesamter Innenraum · `5` Bitumenanstrich: Sohle · `6` Bitumenanstrich: Wandung · `7` Kunstharzbeschichtung: Gesamter Innenraum · `8` Kunstharzbeschichtung: Sohle · `9` Kunstharzbeschichtung: Wandung · `10` Kanalklinker: Gesamter Innenraum · `11` Kanalklinker: Sohle · `12` Kanalklinker: Wandung · `13` Zementmörtelauskleidung: Gesamter Innenraum · `14` Zementmörtelauskleidung: Sohle · `15` Zementmörtelauskleidung: Wandung |
| 10 | Auskleidung | auswahl |  |  |  | `0` Unbekannt · `1` Werksmässig · `2` Spritzauskleidung · `3` Vor-Ort-Auskleidung · `4` Abschnittsweise · `5` Einzelne Rohre · `6` Schlauch-Relining · `7` Endlosrohre · `8` Close-Fit-Lining · `9` Wickelrohr-Lining |
| 11 | Wasserspiegel *(abgeleitet; im WebGIS „“)* | zahl |  |  | Float |  |
| 12 | Rohrleitungstyp | auswahl |  |  |  | `0` Unbekannt · `1` Haltung · `2` Leitung |
| 13 | Rohrlänge [m] *(abgeleitet; im WebGIS „“)* | zahl |  |  | Float |  |

### Bewertung

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Verfahren | auswahl |  |  |  | `0` Unbekannt · `1` ISYBAU 2006 · `2` ISYBAU 2001 · `3` ISYBAU 1996 · `4` Andere |
| 2 | Datum *(abgeleitet; im WebGIS „“)* | datum |  |  | Date |  |
| 3 | Massgebender Schaden | text |  |  |  |  |
| 4 | Quantifizierung *(abgeleitet; im WebGIS „“)* | text |  |  |  |  |
| 5 | Zahl vorl./endg. | text |  |  |  |  |
| 6 | Zahl vorl./endg. – endg. *(abgeleitet; im WebGIS „“)* | text |  |  |  |  |
| 7 | Zusatzpunkte *(abgeleitet; im WebGIS „“)* | text |  |  |  |  |
| 8 | Klasse autom./manu. | text |  | ● |  |  |
| 9 | Klasse autom./manu. – manu. *(abgeleitet; im WebGIS „“)* | text |  |  |  |  |

### Administrativ

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Strasse/Ortsteil | text |  |  |  |  |
| 2 | Strasse/Ortsteil – Ortsteil *(abgeleitet; im WebGIS „“)* | text |  |  |  |  |
| 3 | Untersucher | text |  |  |  |  |
| 4 | Wetter | auswahl |  |  |  | `0` Unbekannt · `1` Kein Niederschlag · `2` Regen · `3` Schmelzwasser · `4` Bedeckt, regnerisch · `5` Nieselregen · `6` Schneefall · `7` Schön, trocken · `8` Frost |
| 5 | Temperatur [°C] *(abgeleitet; im WebGIS „“)* | text |  |  |  |  |

### Dokumente

**Aufklappliste „Dokumente“** (Untermenü) – Tabelle `AWK_DOKUMENT`, bearbeitbar, mehrere Zeilen; Spalten: Beschreibung · Pfad · Datum · dokument

### Anschluss

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Bezeichnung/Funktion hier. | text |  |  |  |  |
| 2 | Bezeichnung/Funktion hier. – Funktion hier. *(abgeleitet; im WebGIS „“)* | auswahl |  |  |  | `0` Unbekannt · `5` Liegenschaftsentwässerung · `106` Sammelkanal · `3` Hauptsammelkanal · `2` Gewässer · `108` Strassenentwässerung · `104` Hauptsammelkanal, regional · `107` Sanierungsleitung · `14` Rinne · `115` Andere |
| 3 | Entfernung [m]/Fixierung | zahl |  |  | Float |  |
| 4 | Entfernung [m]/Fixierung – Fixierung *(abgeleitet; im WebGIS „“)* | text |  |  |  |  |
| 5 | Anschlussart *(abgeleitet; im WebGIS „“)* | auswahl |  |  |  | `1` Abzweig · `2` Stutzen |
| 6 | Kommentar | mehrzeilig |  |  |  |  |

### Video

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Kameratechnik/Referenzart | auswahl |  |  |  | `0` Unbekannt · `1` Satellitenkamera · `2` Schiebekamera · `3` Selbstfahrende Kamera · `4` Andere |
| 2 | Kameratechnik/Referenzart – Referenzart *(abgeleitet; im WebGIS „“)* | auswahl |  |  |  | `0` Unbekannt · `1` Zeitcode · `2` Bildrate · `3` Videozählerstand · `4` Keine |
| 3 | Speichermedium | auswahl |  |  |  | `0` Unbekannt · `1` VHS · `2` SVHS · `3` CD · `4` DVD · `5` MOD · `6` Sonstige |
| 4 | Videoablagereferenz | text |  |  |  |  |
| 5 | Videodateiname | verweis |  | ● |  | → `AWZ_FILM` [filename] |
| 6 | Relativer Pfad | text |  | ● |  |  |

### Schäden

**Aufklappliste „Schäden“** (Untermenü) – Tabelle `AWZ_ZUSTAND_HALTUNG`, bearbeitbar, mehrere Zeilen; Spalten: Stationierung · Code · CH1 · CH2

### Metadaten

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Erstellt am (UTC) | datum |  | ● | Date |  |
| 2 | Erstellt von | text |  | ● |  |  |
| 3 | Geändert am (UTC) | datum |  | ● | Date |  |
| 4 | Geändert von | text |  | ● |  |  |


---

## 23. Inspektion Schacht

Tabelle `AWZ_INSPEKTION_KNOTEN`, Maskentitel im WebGIS: „Inspektion Knoten“

40 Felder, davon 14 Auswahlfelder. Bereiche (Menüs) in Reihenfolge: **Kopf** → **Daten I** → **Daten II** → **Bewertung** → **Administrativ** → **Dokumente** → **Video** → **Schäden** → **Metadaten**

### Kopf

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Nummer | text |  |  |  |  |
| 2 | Auftrag Nr./Bez. | verweis |  |  |  | → `AWZ_AUFTRAG` [nummer] / [bezeichnung] |
| 3 | Knoten | verweis |  |  |  | → `AWK_ABWASSERKNOTEN` [art_bauwerk]: [bezeichnung] |

### Daten I

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Verfahren/Richtung | auswahl |  |  |  | `0` Unbekannt · `1` TV-Untersuchung · `2` Begehung · `3` Vom Schacht · `4` Andere · `5` Deformationsmessung · `6` Dichtheitsprüfung · `7` Georadar |
| 2 | Bezugspunkt horizontal | auswahl |  |  |  | `0` Unbekannt · `1` Niedrigstes abgehendes Rohr bei 12 Uhr · `2` Niedrigstes abgehendes Rohr bei 6 Uhr · `3` Andere |
| 3 | Bezugspunkt vertikal | auswahl |  |  |  | `0` Unbekannt · `1` Sohllage (tiefste Rohrleitung) · `2` Oberkante Abdeckung · `3` Nationaler Bezugspunkt · `4` Lokaler Bezugspunkt · `5` Andere |
| 4 | Datum/Zeit | datum |  |  | Date |  |
| 5 | Wasserhaltung | auswahl |  |  |  | `0` Unbekannt · `1` Keine · `2` Von oberhalb abgesperrt · `3` Von oberhalb teilweise abgesperrt · `4` Seitenzuläufe abgesperrt · `5` Von unterhalb abgesperrt · `6` Andere |
| 6 | Vorreinigung *(abgeleitet; im WebGIS „“)* | auswahl |  |  |  | `0` Unbekannt · `1` Ja · `2` Nein |
| 7 | Bemerkung | mehrzeilig |  |  |  |  |

### Daten II

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Innenschutz | auswahl |  |  |  | `1` Anstrich oder Beschichtung: Gesamter Innenraum · `2` Anstrich oder Beschichtung: Sohle · `3` Anstrich oder Beschichtung: Wandung · `4` Bitumenanstrich: Gesamter Innenraum · `5` Bitumenanstrich: Sohle · `6` Bitumenanstrich: Wandung · `7` Kunstharzbeschichtung: Gesamter Innenraum · `8` Kunstharzbeschichtung: Sohle · `9` Kunstharzbeschichtung: Wandung · `10` Kanalklinker: Gesamter Innenraum · `11` Kanalklinker: Sohle · `12` Kanalklinker: Wandung · `13` Zementmörtelauskleidung: Gesamter Innenraum · `14` Zementmörtelauskleidung: Sohle · `15` Zementmörtelauskleidung: Wandung |
| 2 | Auskleidung | auswahl |  |  |  | `0` Unbekannt · `1` Werksmässig · `2` Spritzauskleidung · `3` Vor-Ort-Auskleidung · `4` Abschnittsweise · `5` Einzelne Rohre · `6` Schlauch-Relining · `7` Endlosrohre · `8` Close-Fit-Lining · `9` Wickelrohr-Lining |
| 3 | Rohrleitungstyp | auswahl |  |  |  | `0` Unbekannt · `1` Haltung · `2` Leitung |
| 4 | Wasserspiegel | zahl |  |  | Float |  |
| 5 | Konus richtig | auswahl |  |  |  | `0` Unbekannt · `1` Ja · `2` Nein |
| 6 | Betriebssicher | auswahl |  |  |  | `0` Unbekannt · `1` Ja · `2` Nein |

### Bewertung

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Verfahren | auswahl |  |  |  | `0` Unbekannt · `1` ISYBAU 2006 · `2` ISYBAU 2001 · `3` ISYBAU 1996 · `4` Andere |
| 2 | Datum *(abgeleitet; im WebGIS „“)* | datum |  |  | Date |  |
| 3 | Massgebender Schaden | text |  |  |  |  |
| 4 | Quantifizierung *(abgeleitet; im WebGIS „“)* | text |  |  |  |  |
| 5 | Zahl vorl./endg. | text |  |  |  |  |
| 6 | Zahl vorl./endg. – endg. *(abgeleitet; im WebGIS „“)* | text |  |  |  |  |
| 7 | Zusatzpunkte *(abgeleitet; im WebGIS „“)* | text |  |  |  |  |
| 8 | Klasse autom./manu. | text |  |  |  |  |
| 9 | Klasse autom./manu. – manu. *(abgeleitet; im WebGIS „“)* | text |  |  |  |  |

### Administrativ

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Strasse/Ortsteil | text |  |  |  |  |
| 2 | Strasse/Ortsteil – Ortsteil *(abgeleitet; im WebGIS „“)* | text |  |  |  |  |
| 3 | Untersucher | text |  |  |  |  |
| 4 | Wetter | auswahl |  |  |  | `0` Unbekannt · `1` Kein Niederschlag · `2` Regen · `3` Schmelzwasser · `4` Bedeckt, regnerisch · `5` Nieselregen · `6` Schneefall · `7` Schön, trocken · `8` Frost |
| 5 | Temperatur [°C] *(abgeleitet; im WebGIS „“)* | text |  |  |  |  |

### Dokumente

**Aufklappliste „Dokumente“** (Untermenü) – Tabelle `AWK_DOKUMENT`, bearbeitbar, mehrere Zeilen; Spalten: Beschreibung · Pfad · Datum · dokument

### Video

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Referenzart | auswahl |  |  |  | `0` Unbekannt · `1` Zeitcode · `2` Bildrate · `3` Videozählerstand · `4` Keine |
| 2 | Speichermedium | auswahl |  |  |  | `0` Unbekannt · `1` VHS · `2` SVHS · `3` CD · `4` DVD · `5` MOD · `6` Sonstige |
| 3 | Videoablagereferenz | text |  |  |  |  |
| 4 | Dateiname Foto | text |  |  |  |  |
| 5 | Videodateiname | verweis |  | ● |  | → `AWZ_FILM` [filename] |
| 6 | Relativer Pfad | text |  | ● |  |  |

### Schäden

**Aufklappliste „Schäden“** (Untermenü) – Tabelle `AWZ_ZUSTAND_KNOTEN`, bearbeitbar, mehrere Zeilen; Spalten: Code · CH1 · CH2

### Metadaten

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Erstellt am (UTC) | datum |  | ● | Date |  |
| 2 | Erstellt von | text |  | ● |  |  |
| 3 | Geändert am (UTC) | datum |  | ● | Date |  |
| 4 | Geändert von | text |  | ● |  |  |


---

## 24. GEP-Massnahme

Tabelle `AWM_MASSNAHME`, Maskentitel im WebGIS: „GEP-Massnahme“

30 Felder, davon 5 Auswahlfelder. Bereiche (Menüs) in Reihenfolge: **Kopf** → **Attribute** → **Objekte referenzieren** → **Referenzen** → **Darstellung** → **Dokumente** → **Metadaten**

### Kopf

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Nummer/Bezeichnung | text | ● |  |  |  |
| 2 | Nummer/Bezeichnung – Bezeichnung *(abgeleitet; im WebGIS „“)* | text |  |  |  |  |
| 3 | VSA-Bezeichnung | text | ● |  |  |  |
| 4 | Rechtswert/Hochwert | zahl |  | ● | Float |  |
| 5 | Rechtswert/Hochwert – Hochwert *(abgeleitet; im WebGIS „“)* | zahl |  | ● | Float |  |
| 6 | Beschreibung | mehrzeilig |  |  |  |  |

### Attribute

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Bemerkung | mehrzeilig |  |  |  |  |
| 2 | Eingang [Datum] | datum |  |  | Date |  |
| 3 | Gesamtkosten geplant/effektiv [CHF] | zahl |  |  | Integer |  |
| 4 | -> *(abgeleitet; im WebGIS „“)* | zahl |  |  | Integer |  |
| 5 | Handlungsbedarf | text |  |  |  |  |
| 6 | Umsetzung geplant/effektiv [Jahr] | zahl |  |  | Integer |  |
| 7 | -> *(abgeleitet; im WebGIS „“)* | zahl |  |  | Integer |  |
| 8 | Kategorie | auswahl |  |  |  | `0` Unbekannt · `1` Administrative Massnahme · `2` andere · `3` Aufhebung · `4` Bachrenaturierung · `5` Bachsanierung · `6` Datenmanagement · `7` Einstellung anpassen (hydraulisch) · `8` Fremdwasserreduktion · `9` GEP-Bearbeitung · `10` GEP-Vorbereitungsarbeiten · `11` Kontrolle und Überwachung · `12` Leitungsersatz (diverse Gründe) · `13` Leitungsersatz (hydraulisch) · `14` Leitungsersatz (Zustand) · `15` Netzerweiterung · `16` Sanierung Kanal/Sonderbauwerke · `17` Abflussvermeidung/Retention/Versickerung · `18` Abwasser ländlicher Raum (ALR) · `19` Erhaltung/Erneuerung · `20` Erhaltung/Reinigung · `21` Erhaltung/Renovierung/Reparatur · `22` Erhaltung, unbekannt · `23` Funktionsänderung · `24` Massnahme im Gewässer · `25` Netzerweiterung · `26` Sonderbauwerk (Anpassung) · `27` Sonderbauwerk (Neubau) · `28` Störfallvorsorge |
| 9 | Priorität | auswahl | ● |  |  | `0` Unbekannt · `1` M0 (sehr dringend) · `2` M1 (dringend) · `3` M2 (mittelfristig) · `4` M3 (längerfristig) · `5` M4 (bei nächster GEP-Überarbeitung) |
| 10 | Status | auswahl | ● |  |  | `0` Unbekannt · `1` Erledigt · `2` In Bearbeitung · `3` Pendent · `4` Sistiert |
| 11 | Trägerschaft | auswahl |  |  |  | `7747a36a-6420-41e2-bbe8-64ff28a7849d` unbekannt (Unbekannt) · `7692de6b-dce2-485c-8f01-190419e4c32f` Privat (Privat) · `4c18a435-f416-4814-a9b4-b68d98189cc4` Bund Astra (Bund) · `58d1c876-3d16-47da-8b38-b509bbc0cbca` Kanton Uri (Kanton) · `df1f763b-7f01-4d4d-a22c-14476c7a3a9b` Bund (Bund) · `f8186a5d-399a-443c-921f-460dff3d0566` oeff_Rechtl_Koerperschaften (Genossenschaft/Kooperation) · `afd5affb-1ce7-49d5-af42-c7e52641dc58` AWU_von_oeffentlich (Abwasserverband) · `86dd0b68-c510-4926-90eb-c5832e4663c4` AWU_von_privat (Abwasserverband) · `54a8d062-7796-4eee-b4d5-60a9d1084f9d` RUAG (Privat) · `357bfc90-bf4b-4e5d-87d6-277a76eefbbd` Meliorationsgen. Reussebene Uri (Genossenschaft/Kooperation) · `cbb47e18-cbc0-48cc-98ea-c37cc56622f9` Meliorationsgen. Seedorf (Genossenschaft/Kooperation) · `16853e1b-1f90-49e6-b762-7ec9ebd7bb59` Altdorf (Gemeinde) · `7c74fab1-4121-4cb9-bcfd-00eea3b4c204` Bürglen (Gemeinde) · `9f6939a7-ba0a-47c6-9c63-9c44cddfddf3` Schattdorf (Gemeinde) · `a4efbb6e-f8a6-4c65-a9df-be9ff1356dea` Andermatt (Gemeinde) · `65f14d2d-8f4e-45de-9635-ae76774e61db` Attinghausen (Gemeinde) · `07e5db4a-ecd0-4c23-bd5d-e144ec68d28e` Erstfeld (Gemeinde) · `6108523b-a8f3-42f9-bf88-1492bdea77d1` Flüelen (Gemeinde) · `1035c742-cb3c-44d5-a7c4-ee3db1ce7a96` Göschenen (Gemeinde) · `c929ee69-5218-4366-9691-a8662f134fe4` Gurtnellen (Gemeinde) · `07e608b7-d1c9-4700-99c6-e0cb65b1b4fd` Hospental (Gemeinde) · `d1647b72-5b4b-4940-af16-1f5325b97ee8` Isenthal (Gemeinde) · `ff2b72c6-c8d8-4a52-b3a0-1ab7810c5c69` Realp (Gemeinde) · `9a2739a5-dd1d-401d-b060-0c37a7b75362` Seedorf (Gemeinde) · `52da3b7e-760d-4659-87fd-a23c977d0941` Seelisberg (Gemeinde) · `6b3fb54d-d3a4-41bd-91e0-542e9ecd4e8e` Silenen (Gemeinde) · `db0e9992-5a10-4c5a-853d-a1e9637042cb` Sisikon (Gemeinde) · `43c22364-adb7-40c2-8759-0dbebf879de0` Spiringen (Gemeinde) · `289c8fc2-ee43-4da2-bb81-6592880080b7` Unterschächen (Gemeinde) · `95844aa7-7306-4cea-88f2-4629a4792c4c` Wassen (Gemeinde) |
| 12 | Verantwortlich für Auslösung | auswahl |  |  |  | `7747a36a-6420-41e2-bbe8-64ff28a7849d` unbekannt (Unbekannt) · `7692de6b-dce2-485c-8f01-190419e4c32f` Privat (Privat) · `4c18a435-f416-4814-a9b4-b68d98189cc4` Bund Astra (Bund) · `58d1c876-3d16-47da-8b38-b509bbc0cbca` Kanton Uri (Kanton) · `df1f763b-7f01-4d4d-a22c-14476c7a3a9b` Bund (Bund) · `f8186a5d-399a-443c-921f-460dff3d0566` oeff_Rechtl_Koerperschaften (Genossenschaft/Kooperation) · `afd5affb-1ce7-49d5-af42-c7e52641dc58` AWU_von_oeffentlich (Abwasserverband) · `86dd0b68-c510-4926-90eb-c5832e4663c4` AWU_von_privat (Abwasserverband) · `54a8d062-7796-4eee-b4d5-60a9d1084f9d` RUAG (Privat) · `357bfc90-bf4b-4e5d-87d6-277a76eefbbd` Meliorationsgen. Reussebene Uri (Genossenschaft/Kooperation) · `cbb47e18-cbc0-48cc-98ea-c37cc56622f9` Meliorationsgen. Seedorf (Genossenschaft/Kooperation) · `16853e1b-1f90-49e6-b762-7ec9ebd7bb59` Altdorf (Gemeinde) · `7c74fab1-4121-4cb9-bcfd-00eea3b4c204` Bürglen (Gemeinde) · `9f6939a7-ba0a-47c6-9c63-9c44cddfddf3` Schattdorf (Gemeinde) · `a4efbb6e-f8a6-4c65-a9df-be9ff1356dea` Andermatt (Gemeinde) · `65f14d2d-8f4e-45de-9635-ae76774e61db` Attinghausen (Gemeinde) · `07e5db4a-ecd0-4c23-bd5d-e144ec68d28e` Erstfeld (Gemeinde) · `6108523b-a8f3-42f9-bf88-1492bdea77d1` Flüelen (Gemeinde) · `1035c742-cb3c-44d5-a7c4-ee3db1ce7a96` Göschenen (Gemeinde) · `c929ee69-5218-4366-9691-a8662f134fe4` Gurtnellen (Gemeinde) · `07e608b7-d1c9-4700-99c6-e0cb65b1b4fd` Hospental (Gemeinde) · `d1647b72-5b4b-4940-af16-1f5325b97ee8` Isenthal (Gemeinde) · `ff2b72c6-c8d8-4a52-b3a0-1ab7810c5c69` Realp (Gemeinde) · `9a2739a5-dd1d-401d-b060-0c37a7b75362` Seedorf (Gemeinde) · `52da3b7e-760d-4659-87fd-a23c977d0941` Seelisberg (Gemeinde) · `6b3fb54d-d3a4-41bd-91e0-542e9ecd4e8e` Silenen (Gemeinde) · `db0e9992-5a10-4c5a-853d-a1e9637042cb` Sisikon (Gemeinde) · `43c22364-adb7-40c2-8759-0dbebf879de0` Spiringen (Gemeinde) · `289c8fc2-ee43-4da2-bb81-6592880080b7` Unterschächen (Gemeinde) · `95844aa7-7306-4cea-88f2-4629a4792c4c` Wassen (Gemeinde) |
| 13 | Verweis (Abhängig von Massnahme) | verweis |  |  |  | → `AWM_MASSNAHME` Massnahme [vsa_bezeichnung] (%SubstrPos([globalid]; 2; 9)) |
| 14 | Bemerkung zu Abhängigkeit | text |  |  |  |  |
| 15 | Verweis Interlis | mehrzeilig |  |  |  |  |

**Aufklappliste „Verweis (von dieser Massnahme abhängig)“** (Untermenü) – Tabelle `awm_massnahme`, nur lesend; Spalten: Globalid · Bezeichnung · Status

### Objekte referenzieren

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Anzahl Haltungen | text |  | ● |  |  |
| 2 | Anzahl Abwasserknoten | text |  | ● |  |  |
| 3 | Anzahl Einzugsgebiete | text |  | ● |  |  |

### Referenzen

**Aufklappliste „Haltungen“** (Untermenü) – Tabelle `AWK_HALTUNG`, bearbeitbar, mehrere Zeilen; Spalten: Bezeichnung · Zustand

**Aufklappliste „Abwasserknoten“** (Untermenü) – Tabelle `AWK_ABWASSERKNOTEN`, bearbeitbar, mehrere Zeilen; Spalten: Bezeichnung · Zustand

**Aufklappliste „Einzugsgebiete (Fläche)“** (Untermenü) – Tabelle `AWH_EINZUGSGEBIET`, bearbeitbar, mehrere Zeilen; Spalten: Globalid · Bezeichnung · Nummer

**Aufklappliste „Einzugsgebiete (Zentroid/Attribute)“** (Untermenü) – Tabelle `AWH_EINZUG_ZENTR`, bearbeitbar, mehrere Zeilen; Spalten: Bezeichnung · Nummer

### Darstellung

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Linie Versatz/Fläche Puffer | zahl |  |  | Integer |  |
| 2 | Linie Versatz/Fläche Puffer – Fläche Puffer *(abgeleitet; im WebGIS „“)* | zahl |  |  | Integer |  |

**Aufklappliste „Zusätzliche Symbole“** (Untermenü) – Tabelle `AWM_MASSNAHME_ZUSATZ`, bearbeitbar, mehrere Zeilen; Spalten: Geometrie

**Aufklappliste „Linie/Fläche“** (Untermenü) – Tabelle `AWM_MASSNAHME_LIN`, bearbeitbar, mehrere Zeilen; Spalten: Geometrie

**Aufklappliste „None“** (Untermenü) – Tabelle `AWM_MASSNAHME_FLA`, bearbeitbar, mehrere Zeilen; Spalten: Geometrie

### Dokumente

**Aufklappliste „Dokumente“** (Untermenü) – Tabelle `AWK_DOKUMENT`, bearbeitbar, mehrere Zeilen; Spalten: Beschreibung · Pfad · Datum · dokument

### Metadaten

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Erstellt am (UTC) | datum |  | ● | Date |  |
| 2 | Erstellt von | text |  | ● |  |  |
| 3 | Geändert am (UTC) | datum |  | ● | Date |  |
| 4 | Geändert von | text |  | ● |  |  |


---

## 25. Einzugsgebiet

Tabelle `AWH_EINZUGSGEBIET`, Maskentitel im WebGIS: „Einzugsgebiet“

61 Felder, davon 14 Auswahlfelder. Bereiche (Menüs) in Reihenfolge: **Kopf** → **Daten** → **Daten II** → **Administrativ** → **Hydraulik** → **GEP Massnahmen** → **Metadaten**

### Kopf

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Bezeichnung/Nummer | text | ● |  |  |  |
| 2 | Nummer *(abgeleitet; im WebGIS „“)* | text |  |  |  |  |
| 3 | Zentroid | verweis |  | ● |  | → `AWH_EINZUG_ZENTR` [bezeichnung] [nummer] |

### Daten

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Entwässerungssystem | auswahl | ● |  |  | `0` Unbekannt · `1` Mischsystem · `2` Trennsystem · `6` Regenabwassersystem · `3` Nicht angeschlossen · `4` Modifiziertes System · `5` Nicht entwässert |
| 2 | Geplant | auswahl |  |  |  | `0` Unbekannt · `1` Mischsystem · `2` Trennsystem · `6` Regenabwassersystem · `3` Nicht angeschlossen · `4` Modifiziertes System · `5` Nicht entwässert |
| 3 | Versickerung | auswahl |  |  |  | abhängig von **Entwässerungssystem**:<br>  - Elternwert `0`: _(leer)_<br>  - Elternwert `1`: `0` Unbekannt · `1` Vollständige Regenabwasserversickerung · `2` Teilweise Regenabwasserversickerung · `8` Gewässerschutzzone S · `9` Eingeschränkte Versickerungsmöglichkeiten · `10` Versickerung verboten · `11` Versickerung mit Oberbodenpassage · `12` Versickerung ohne Oberbodenpassage · `13` Regenabwasser darf nicht kanalisiert werden · `14` Retention des Regenabwassers obligatorisch · `3` Weitere<br>  - Elternwert `2`: `7` Unbekannt · `4` Vorfluter · `5` Regenabwasserretention · `15` Gewässerschutzzone S · `16` Eingeschränkte Versickerungsmöglichkeiten · `17` Versickerung verboten · `18` Versickerung mit Oberbodenpassage · `19` Versickerung ohne Oberbodenpassage · `20` Regenabwasser darf nicht kanalisiert werden · `21` Retention des Regenabwassers obligatorisch · `6` Weitere<br>  - Elternwert `3`: _(leer)_<br>  - Elternwert `4`: _(leer)_<br>  - Elternwert `5`: _(leer)_<br>  - Elternwert `6`: _(leer)_ |
| 4 | Status/Art | auswahl | ● |  |  | `0` Unbekannt · `1` Ist- und Planungszustand · `2` Ist-Zustand · `3` Planungszustand |
| 5 | Art *(abgeleitet; im WebGIS „“)* | auswahl | ● |  |  | `0` Unbekannt · `1` Regen- und Trockenwetter · `2` Regenwetter · `3` Trockenwetter |
| 6 | Einwohnerdichte [E/ha] | text |  |  |  |  |
| 7 | Geplant | text |  |  |  |  |
| 8 | Fläche [ha] | zahl |  | ● | Float |  |
| 9 | Entwässerungsart | auswahl |  |  |  | `0` unbekannt · `1` Versickerung über die Schulter · `2` Behandlungsanlage SABA · `3` Ölrückhaltebecken · `4` Abwasserreinigungsanlage · `5` Direkteinleitung |
| 10 | Bemerkung | mehrzeilig |  |  |  |  |

### Daten II

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Abwasserknoten SW/geplant | verweis |  |  |  | → `AWK_ABWASSERKNOTEN` [art_bauwerk]: [bezeichnung] [nutzungsart] |
| 2 | Abwasserknoten SW geplant *(abgeleitet; im WebGIS „“)* | verweis |  |  |  | → `AWK_ABWASSERKNOTEN` [art_bauwerk]: [bezeichnung] [nutzungsart] |
| 3 | Abwasserknoten RW/geplant | verweis |  |  |  | → `AWK_ABWASSERKNOTEN` [art_bauwerk]: [bezeichnung] [nutzungsart] |
| 4 | Abwasserknoten RW geplant *(abgeleitet; im WebGIS „“)* | verweis |  |  |  | → `AWK_ABWASSERKNOTEN` [art_bauwerk]: [bezeichnung] [nutzungsart] |
| 5 | Abwasserknoten MW/geplant | verweis |  |  |  | → `AWK_ABWASSERKNOTEN` [art_bauwerk]: [bezeichnung] [nutzungsart] |
| 6 | Abwasserknoten MW geplant *(abgeleitet; im WebGIS „“)* | verweis |  |  |  | → `AWK_ABWASSERKNOTEN` [art_bauwerk]: [bezeichnung] [nutzungsart] |
| 7 | Sonderbauwerk | text |  |  |  |  |
| 8 | Haltung SW/geplant | verweis |  |  |  | → `AWK_HALTUNG` [funktionhierarchisch]: [bezeichnung] [nutzungsart] |
| 9 | Haltung SW geplant *(abgeleitet; im WebGIS „“)* | verweis |  |  |  | → `AWK_HALTUNG` [funktionhierarchisch]: [bezeichnung] [nutzungsart] |
| 10 | Haltung RW/geplant | verweis |  |  |  | → `AWK_HALTUNG` [funktionhierarchisch]: [bezeichnung] [nutzungsart] |
| 11 | Haltung RW geplant *(abgeleitet; im WebGIS „“)* | verweis |  |  |  | → `AWK_HALTUNG` [funktionhierarchisch]: [bezeichnung] [nutzungsart] |
| 12 | Haltung MW/geplant | verweis |  |  |  | → `AWK_HALTUNG` [funktionhierarchisch]: [bezeichnung] [nutzungsart] |
| 13 | Haltung MW geplant *(abgeleitet; im WebGIS „“)* | verweis |  |  |  | → `AWK_HALTUNG` [funktionhierarchisch]: [bezeichnung] [nutzungsart] |

### Administrativ

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Bauzone | auswahl | ● |  |  | `0` Unbekannt · `1` Wald · `2` Wohn- und Gewerbezone, 3 Geschosse · `3` Wohn- und Gewerbezone, 2 Geschosse · `4` Wohn- und Gewerbezone · `5` Wohnzone, 3 Geschosse · `6` Wohnzone, 2 Geschosse · `7` Wohnzone, 1 Geschoss · `8` Wohnzone · `9` Zone für öffentliche Bauten · `10` Zone für öffentliche Anlagen · `11` Zone für öffentliche Bauten und Anlagen · `12` Dorfkern, Altstadtzone · `13` Industriezone · `14` Gewerbezone · `15` Andere · `1000` B_d · `1001` B_l · `1002` B_m · `1003` F_d · `1004` F_l · `1005` F_m · `1006` K_d · `1007` K_l · `1008` Oe_d · `1009` Oe_l · `1010` Oe_ld · `1011` Oe_ll · `1012` Oe_u · `1013` W1_d · `1014` W1_l · `1015` W1_ll · `1016` W2_d · `1017` W2_l · `1018` W3_d · `1019` W3_l · `1020` W3_ll · `1021` WG_d · `1022` WG_l |
| 2 | Direkteinleitung | auswahl |  |  |  | `0` Unbekannt · `1` Ja · `2` Nein |
| 3 | Geplant | auswahl |  |  |  | `0` Unbekannt · `1` Ja · `2` Nein |
| 4 | Retention | auswahl |  |  |  | `0` Unbekannt · `1` Ja · `2` Nein |
| 5 | Geplant | auswahl |  |  |  | `0` Unbekannt · `1` Ja · `2` Nein |
| 6 | Versickerung | auswahl |  |  |  | `0` Unbekannt · `1` Ja · `2` Nein |
| 7 | Geplant | auswahl |  |  |  | `0` Unbekannt · `1` Ja · `2` Nein |
| 8 | Abflussbegrenzung [(l/s)/ha] | text |  |  |  |  |
| 9 | Geplant | text |  |  |  |  |
| 10 | Fremdwasseranfall [m³/s] | text |  |  |  |  |
| 11 | Geplant | text |  |  |  |  |
| 12 | Schmutzwasseranfall [m³/s] | text |  |  |  |  |
| 13 | Geplant | text |  |  |  |  |

### Hydraulik

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Befestigungsgrad | auswahl |  |  |  | `0` Unbekannt · `1` Befestigt · `2` Bestockt · `3` Humusiert · `4` Vegetationslos · `5` Andere |
| 2 | Befestigungsgrad [%] | text |  |  |  |  |
| 3 | Befestigungsgrad [%] RW *(abgeleitet; im WebGIS „“)* | text |  |  |  |  |
| 4 | Befestigungsgrad [%] MW *(abgeleitet; im WebGIS „“)* | text |  |  |  |  |
| 5 | Befestigungsgrad [%] Geplant SW *(abgeleitet; im WebGIS „“)* | text |  |  |  |  |
| 6 | Befestigungsgrad [%] Geplant RW *(abgeleitet; im WebGIS „“)* | text |  |  |  |  |
| 7 | Befestigungsgrad [%] Geplant MW *(abgeleitet; im WebGIS „“)* | text |  |  |  |  |
| 8 | Abflussbeiwert [%] | text |  |  |  |  |
| 9 | Abflussbeiwert [%] RW *(abgeleitet; im WebGIS „“)* | text |  |  |  |  |
| 10 | Abflussbeiwert [%] MW *(abgeleitet; im WebGIS „“)* | text |  |  |  |  |
| 11 | Abflussbeiwert [%] Geplant SW *(abgeleitet; im WebGIS „“)* | text |  |  |  |  |
| 12 | Abflussbeiwert [%] Geplant RW *(abgeleitet; im WebGIS „“)* | text |  |  |  |  |
| 13 | Abflussbeiwert [%] Geplant MW *(abgeleitet; im WebGIS „“)* | text |  |  |  |  |
| 14 | Abflussbeiwert berechnet [%] | text |  | ● |  |  |
| 15 | Abflussbeiwert manuell [%] | text |  |  |  |  |
| 16 | Fliessweg [m] / Neigung [%] | text |  |  |  |  |
| 17 | Neigung [%] *(abgeleitet; im WebGIS „“)* | text |  |  |  |  |
| 18 | Sonderzufluss [m³/s] | text |  |  |  |  |

### GEP Massnahmen

**Aufklappliste „GEP Massnahmen“** (Untermenü) – Tabelle `AWM_MASSNAHME`, bearbeitbar, mehrere Zeilen; Spalten: Bezeichnung · Beschreibung

### Metadaten

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Erstellt am (UTC) | datum |  | ● | Date |  |
| 2 | Erstellt von | text |  | ● |  |  |
| 3 | Geändert am (UTC) | datum |  | ● | Date |  |
| 4 | Geändert von | text |  | ● |  |  |


---

## 26. Hydraulische Geometrie

Tabelle `AWH_HYDR_GEOMETRIE`, Maskentitel im WebGIS: „Hydraulische Geometrie“

12 Felder, davon 0 Auswahlfelder. Bereiche (Menüs) in Reihenfolge: **Kopf** → **Daten I** → **Metadaten**

### Kopf

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Bezeichnung | text |  |  |  |  |
| 2 | Hydr. Geom. - Relation | verweis |  |  |  | → `AWH_HYDR_GEOMRELATION` %SubstrPos([globalid];2;9) |

**Aufklappliste „Knoten“** (Untermenü) – Tabelle `AWK_ABWASSERKNOTEN`, bearbeitbar, mehrere Zeilen; Spalten: Art · Bezeichnung

### Daten I

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Nutzinhalt [m³] | zahl |  |  | Float |  |
| 2 | Nutzinhalt Fangteil [m³] | zahl |  |  | Float |  |
| 3 | Nutzinhalt Klärteil [m³] | zahl |  |  | Float |  |
| 4 | Stauraum [m³] | zahl |  |  | Float |  |
| 5 | Volumen Pumpensumpf [m³] | zahl |  |  | Float |  |
| 6 | Bemerkung | mehrzeilig |  |  |  |  |

### Metadaten

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Erstellt am (UTC) | datum |  | ● | Date |  |
| 2 | Erstellt von | text |  | ● |  |  |
| 3 | Geändert am (UTC) | datum |  | ● | Date |  |
| 4 | Geändert von | text |  | ● |  |  |


---

## 27. Mechanische Vorreinigung

Tabelle `AWK_VSA_MECHVORREINIGUNG`, Maskentitel im WebGIS: „Mechanische Vorreinigung“

8 Felder, davon 1 Auswahlfelder. Bereiche (Menüs) in Reihenfolge: **Kopf** → **Daten** → **Metadaten**

### Kopf

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Bezeichnung | text |  |  |  |  |

### Daten

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Art | auswahl |  |  |  | `0` Unbekannt · `101` Filtersack · `102` Künstlicher Adsorber · `103` Mulden-Rigolen-System · `104` Schlammsammler · `105` Schwimmstoffabscheider · `106` Retentions-/Filterbecken |
| 2 | Knoten | verweis |  |  |  | → `AWK_ABWASSERKNOTEN` [art_bauwerk]: [bezeichnung] |
| 3 | Bemerkung | mehrzeilig |  |  |  |  |

### Metadaten

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Erstellt am (UTC) | datum |  | ● | Date |  |
| 2 | Erstellt von | text |  | ● |  |  |
| 3 | Geändert am (UTC) | datum |  | ● | Date |  |
| 4 | Geändert von | text |  | ● |  |  |


---

## 28. Förderaggregat (Pumpe)

Tabelle `AWK_ABWASSERKNOTEN`, Subtyp `art_bauwerk=12`, Maskentitel im WebGIS: „Förderaggregat“

60 Felder, davon 23 Auswahlfelder. Bereiche (Menüs) in Reihenfolge: **Kopf** → **Daten I** → **Daten II** → **Bauwerksteile** → **Administrativ** → **Unterhalt** → **Hydraulik** → **Metadaten**

### Kopf

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Bezeichnung | text | ● |  |  |  |
| 2 | OBJECTID | text |  | ● |  |  |
| 3 | Bezeichnung alter./hist. | text |  |  |  |  |
| 4 | Bezeichnung alter./hist. – hist. *(abgeleitet; im WebGIS „“)* | text |  |  |  |  |
| 5 | Rechtswert/Hochwert | zahl |  | ● | Float |  |
| 6 | Rechtswert/Hochwert – Hochwert *(abgeleitet; im WebGIS „“)* | zahl |  | ● | Float |  |

### Daten I

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Bauwerksart Förderaggregat *(abgeleitet; im WebGIS „“)* | auswahl | ● |  |  | `700` Unbekannt · `701` Druckluftanlage · `702` Kolbenpumpe · `703` Kreiselpumpe · `704` Schneckenpumpe · `705` Vakuumanlage · `706` Andere |
| 2 | Funktion | auswahl | ● |  |  | `0` Unbekannt · `101` Trennüberlauf · `102` Intern · `103` Notentlastung · `104` Regenüberlauf · `105` Andere |
| 3 | Nutzungsart | auswahl | ● |  |  | `0` Unbekannt · `4` Mischabwasser · `5` Regenabwasser · `106` Reinabwasser · `7` Schmutzabwasser · `1` Bachabwasser · `102` Entlastetes Mischabwasser · `103` Industrieabwasser · `111` Andere · `1100` Bergwasser · `1101` Strassenabwasser |
| 4 | Knoten *(abgeleitet; im WebGIS „“)* | verweis |  |  |  | → `AWK_ABWASSERKNOTEN` [art_bauwerk]: [bezeichnung] |
| 5 | Knoten *(abgeleitet; im WebGIS „“)* | verweis |  |  |  | → `AWK_ABWASSERKNOTEN` [art_bauwerk]: [bezeichnung] |
| 6 | Typ AA | auswahl | ● |  |  | `0` Unbekannt · `1` PAA · `2` SAA |
| 7 | Funktion hier./hydr. | auswahl | ● |  |  | `0` Unbekannt · `5` Liegenschaftsentwässerung · `106` Sammelkanal · `3` Hauptsammelkanal · `2` Gewässer · `108` Strassenentwässerung · `104` Hauptsammelkanal, regional · `107` Sanierungsleitung · `14` Rinne · `115` Andere |
| 8 | Funktion hier./hydr. – hydr. *(abgeleitet; im WebGIS „“)* | auswahl | ● |  |  | `0` Unbekannt · `4` Freispiegelleitung · `5` Pumpendruckleitung · `101` Drainagetransportleitung · `102` Drosselleitung · `103` Dükerleitung · `106` Sickerleitung · `107` Speicherleitung · `108` Spülleitung · `111` Andere · `116` Vakuumleitung · `1100` Belagsrinne Wasserschale · `1101` Entwaesserungsgraben befestigt · `1102` Entwaesserungsgraben unbefestigt · `1103` Schlitzrinne · `1104` Wasserrinne mit Rost |
| 9 | Status | auswahl | ● |  |  | `0` Unbekannt · `1` In Betrieb · `2` Ausser Betrieb · `5` Tot/Aufgehoben, verfüllt |
| 10 | Baujahr *(abgeleitet; im WebGIS „“)* | zahl | ● |  | Integer |  |
| 11 | Sohlenhöhe | text |  |  |  |  |
| 12 | Geländehöhe *(abgeleitet; im WebGIS „“)* | zahl |  |  | Float |  |
| 13 | Ebene | auswahl |  |  |  | `0` Ebene 0 · `1` Ebene 1 · `2` Ebene -1 · `3` Ebene 2 · `4` Ebene -2 · `5` Ebene 3 · `6` Ebene -3 · `7` Ebene 4 · `8` Ebene -4 · `9` Ebene 5 · `10` Ebene -5 · `11` Ebene 6 · `12` Ebene -6 · `13` Ebene 7 · `14` Ebene -7 · `15` Ebene 8 · `16` Ebene -8 · `17` Ebene 9 · `18` Ebene -9 · `19` Ebene 10 · `20` Ebene -10 |
| 14 | Bemerkung | mehrzeilig |  |  |  |  |

### Daten II

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Lagebest./-genauigkeit | auswahl | ● |  |  | `0` Unbekannt · `101` Ungenau · `102` Genau |
| 2 | Lagebest./-genauigkeit – -genauigkeit *(abgeleitet; im WebGIS „“)* | auswahl | ● |  |  | `0` Unbekannt · `101` > 50 cm · `102` +/- 10 cm · `103` +/- 3 cm · `104` +/- 50 cm |
| 3 | Höhenbest./-genauigkeit | auswahl | ● |  |  | `0` Unbekannt · `101` Ungenau · `102` Genau |
| 4 | Höhenbest./-genauigkeit – -genauigkeit *(abgeleitet; im WebGIS „“)* | auswahl | ● |  |  | `0` Unbekannt · `101` > 6 cm · `102` +/- 1 cm · `103` +/- 3 cm · `104` +/- 6 cm |
| 5 | Rückstaukote | zahl |  |  | Float |  |
| 6 | Fabrikat | text |  |  |  |  |
| 7 | Steuerung/Verstellbarkeit | auswahl |  |  |  | `0` Unbekannt · `101` Geregelt · `102` Gesteuert · `103` Keine |
| 8 | Steuerung/Verstellbarkeit – Verstellbarkeit *(abgeleitet; im WebGIS „“)* | auswahl |  |  |  | `0` Unbekannt · `101` Fest · `102` Verstellbar |
| 9 | Signalübermittlung | auswahl |  |  |  | `0` Unbekannt · `101` Empfangen · `102` Senden · `103` Senden, empfangen |
| 10 | Antrieb/Aufstellung | auswahl |  |  |  | `0` Unbekannt · `101` Benzinmotor · `102` Dieselmotor · `103` Elektromotor · `104` Hydraulisch · `105` Keiner · `106` Manuell · `107` Pneumatisch · `108` Andere |
| 11 | Antrieb/Aufstellung – Aufstellung *(abgeleitet; im WebGIS „“)* | auswahl |  |  |  | `0` Unbekannt · `101` Nass · `102` Trocken |
| 12 | Aufstellung Förderaggregat | auswahl |  |  |  | `0` Unbekannt · `101` Horizontal · `102` Vertikal |
| 13 | Anzahl | text |  |  |  |  |
| 14 | Kote Start | zahl |  |  | Float |  |
| 15 | Kote Stop | zahl |  |  | Float |  |
| 16 | Steuerungszentrale | verweis |  |  |  | → `AWK_VSA_STEUERUNGSZENTRALE` [vsa_bezeichnung] |

### Bauwerksteile

**Aufklappliste „Rückstausicherung“** (Untermenü) – Tabelle `AWK_BAUWERKSTEIL`, bearbeitbar, mehrere Zeilen; Spalten: Bezeichnung · Art

**Aufklappliste „Beckenentleerung“** (Untermenü) – Tabelle `AWK_BAUWERKSTEIL`, bearbeitbar, mehrere Zeilen; Spalten: Bezeichnung · Art

### Administrativ

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Eigentümer | auswahl | ● |  |  | `16853e1b-1f90-49e6-b762-7ec9ebd7bb59` Altdorf · `a4efbb6e-f8a6-4c65-a9df-be9ff1356dea` Andermatt · `65f14d2d-8f4e-45de-9635-ae76774e61db` Attinghausen · `afd5affb-1ce7-49d5-af42-c7e52641dc58` AWU_von_oeffentlich · `86dd0b68-c510-4926-90eb-c5832e4663c4` AWU_von_privat · `df1f763b-7f01-4d4d-a22c-14476c7a3a9b` Bund · `4c18a435-f416-4814-a9b4-b68d98189cc4` Bund Astra · `7c74fab1-4121-4cb9-bcfd-00eea3b4c204` Bürglen · `07e5db4a-ecd0-4c23-bd5d-e144ec68d28e` Erstfeld · `6108523b-a8f3-42f9-bf88-1492bdea77d1` Flüelen · `1035c742-cb3c-44d5-a7c4-ee3db1ce7a96` Göschenen · `c929ee69-5218-4366-9691-a8662f134fe4` Gurtnellen · `07e608b7-d1c9-4700-99c6-e0cb65b1b4fd` Hospental · `d1647b72-5b4b-4940-af16-1f5325b97ee8` Isenthal · `58d1c876-3d16-47da-8b38-b509bbc0cbca` Kanton Uri · `357bfc90-bf4b-4e5d-87d6-277a76eefbbd` Meliorationsgen. Reussebene Uri · `cbb47e18-cbc0-48cc-98ea-c37cc56622f9` Meliorationsgen. Seedorf · `f8186a5d-399a-443c-921f-460dff3d0566` oeff_Rechtl_Koerperschaften · `7692de6b-dce2-485c-8f01-190419e4c32f` Privat · `ff2b72c6-c8d8-4a52-b3a0-1ab7810c5c69` Realp · `54a8d062-7796-4eee-b4d5-60a9d1084f9d` RUAG · `9f6939a7-ba0a-47c6-9c63-9c44cddfddf3` Schattdorf · `9a2739a5-dd1d-401d-b060-0c37a7b75362` Seedorf · `52da3b7e-760d-4659-87fd-a23c977d0941` Seelisberg · `6b3fb54d-d3a4-41bd-91e0-542e9ecd4e8e` Silenen · `db0e9992-5a10-4c5a-853d-a1e9637042cb` Sisikon · `43c22364-adb7-40c2-8759-0dbebf879de0` Spiringen · `7747a36a-6420-41e2-bbe8-64ff28a7849d` unbekannt · `289c8fc2-ee43-4da2-bb81-6592880080b7` Unterschächen · `95844aa7-7306-4cea-88f2-4629a4792c4c` Wassen |
| 2 | Betreiber | auswahl | ● |  |  | `16853e1b-1f90-49e6-b762-7ec9ebd7bb59` Altdorf · `a4efbb6e-f8a6-4c65-a9df-be9ff1356dea` Andermatt · `65f14d2d-8f4e-45de-9635-ae76774e61db` Attinghausen · `afd5affb-1ce7-49d5-af42-c7e52641dc58` AWU_von_oeffentlich · `86dd0b68-c510-4926-90eb-c5832e4663c4` AWU_von_privat · `df1f763b-7f01-4d4d-a22c-14476c7a3a9b` Bund · `4c18a435-f416-4814-a9b4-b68d98189cc4` Bund Astra · `7c74fab1-4121-4cb9-bcfd-00eea3b4c204` Bürglen · `07e5db4a-ecd0-4c23-bd5d-e144ec68d28e` Erstfeld · `6108523b-a8f3-42f9-bf88-1492bdea77d1` Flüelen · `1035c742-cb3c-44d5-a7c4-ee3db1ce7a96` Göschenen · `c929ee69-5218-4366-9691-a8662f134fe4` Gurtnellen · `07e608b7-d1c9-4700-99c6-e0cb65b1b4fd` Hospental · `d1647b72-5b4b-4940-af16-1f5325b97ee8` Isenthal · `58d1c876-3d16-47da-8b38-b509bbc0cbca` Kanton Uri · `357bfc90-bf4b-4e5d-87d6-277a76eefbbd` Meliorationsgen. Reussebene Uri · `cbb47e18-cbc0-48cc-98ea-c37cc56622f9` Meliorationsgen. Seedorf · `f8186a5d-399a-443c-921f-460dff3d0566` oeff_Rechtl_Koerperschaften · `7692de6b-dce2-485c-8f01-190419e4c32f` Privat · `ff2b72c6-c8d8-4a52-b3a0-1ab7810c5c69` Realp · `54a8d062-7796-4eee-b4d5-60a9d1084f9d` RUAG · `9f6939a7-ba0a-47c6-9c63-9c44cddfddf3` Schattdorf · `9a2739a5-dd1d-401d-b060-0c37a7b75362` Seedorf · `52da3b7e-760d-4659-87fd-a23c977d0941` Seelisberg · `6b3fb54d-d3a4-41bd-91e0-542e9ecd4e8e` Silenen · `db0e9992-5a10-4c5a-853d-a1e9637042cb` Sisikon · `43c22364-adb7-40c2-8759-0dbebf879de0` Spiringen · `7747a36a-6420-41e2-bbe8-64ff28a7849d` unbekannt · `289c8fc2-ee43-4da2-bb81-6592880080b7` Unterschächen · `95844aa7-7306-4cea-88f2-4629a4792c4c` Wassen |
| 3 | Einleitstelle | text |  |  | String |  |
| 4 | Akten | mehrzeilig |  |  |  |  |

### Unterhalt

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Zustand/Sanierungsbedarf | auswahl |  |  |  | `0` Unbekannt · `100` Nicht mehr funktionstüchtig (Z0) · `101` Starke Mängel (Z1) · `102` Mittlere Mängel (Z2) · `103` Leichte Mängel (Z3) · `104` Keine Mängel (Z4) · `1000` nicht beurteilt |
| 2 | Zustand/Sanierungsbedarf – Sanierungsbedarf *(abgeleitet; im WebGIS „“)* | auswahl |  |  |  | `0` Unbekannt · `101` Dringend · `102` Kurzfristig · `103` Mittelfristig · `104` Langfristig · `105` Keiner · `106` Saniert |
| 3 | Inspektionsintervall [Jahr] | zahl |  |  | Float |  |
| 4 | Spülintervall [Jahr] | zahl |  |  | Float |  |
| 5 | Systemgrenze | auswahl |  |  |  | `0` unbekannt · `1` keine · `2` Uebergabepunkt · `3` Uebernahmepunkt |

### Hydraulik

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Überlaufcharakteristik | verweis |  |  |  | → `AWH_UEBERLAUFCHARAKTERISTIK` [bezeichnung] |
| 2 | Hydraulische Geometrie | verweis |  |  |  | → `AWH_HYDR_GEOMETRIE` [bezeichnung] |
| 3 | Arbeitspunkt [m³] | zahl |  |  | Float |  |
| 4 | Förderstrom min. einzel [l/s] | zahl |  |  | Float |  |
| 5 | Förderstrom max. einzel [l/s] | zahl |  |  | Float |  |
| 6 | Qan ist [l/s] | zahl |  |  | Float |  |
| 7 | Qan Dim. [l/s] | zahl |  |  | Float |  |
| 8 | Überlaufdauer [h] | zahl |  |  | Float |  |
| 9 | Überlauffracht [kg/Jahr] | text |  |  |  |  |
| 10 | Überlaufhäufigkeit [Anzahl/Jahr] | text |  |  |  |  |
| 11 | Überlaufmenge [m³] | zahl |  |  | Float |  |

### Metadaten

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Erstellt am (UTC) | datum |  | ● | Date |  |
| 2 | Erstellt von | text |  | ● |  |  |
| 3 | Geändert am (UTC) | datum |  | ● | Date |  |
| 4 | Geändert von | text |  | ● |  |  |


---

## 29. Überlauf

Tabelle `AWK_ABWASSERKNOTEN`, Subtyp `art_bauwerk=9`, Maskentitel im WebGIS: „Überlauf“

62 Felder, davon 24 Auswahlfelder. Bereiche (Menüs) in Reihenfolge: **Kopf** → **Daten I** → **Daten II** → **Bauwerksteile** → **Administrativ** → **Unterhalt** → **Hydraulik** → **Metadaten**

### Kopf

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Bezeichnung | text | ● |  |  |  |
| 2 | OBJECTID | text |  | ● |  |  |
| 3 | Bezeichnung alter./hist. | text |  |  |  |  |
| 4 | Bezeichnung alter./hist. – hist. *(abgeleitet; im WebGIS „“)* | text |  |  |  |  |
| 5 | Rechtswert/Hochwert | zahl |  | ● | Float |  |
| 6 | Rechtswert/Hochwert – Hochwert *(abgeleitet; im WebGIS „“)* | zahl |  | ● | Float |  |
| 7 | Knoten (von)/Knoten (bis) | verweis |  |  |  | → `AWK_ABWASSERKNOTEN` [art_bauwerk]: [bezeichnung] |
| 8 | Knoten (von)/Knoten (bis) – Knoten (bis) *(abgeleitet; im WebGIS „“)* | verweis |  |  |  | → `AWK_ABWASSERKNOTEN` [art_bauwerk]: [bezeichnung] |

### Daten I

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Bauwerksart Überlauf *(abgeleitet; im WebGIS „“)* | auswahl | ● |  |  | `550` Unbekannt · `551` Streichwehr, hochgezogen · `553` Streichwehr, niedrig · `552` Leapingwehr |
| 2 | Funktion | auswahl | ● |  |  | `0` Unbekannt · `101` Trennüberlauf · `102` Intern · `103` Notentlastung · `104` Regenüberlauf · `105` Andere |
| 3 | Nutzungsart | auswahl | ● |  |  | `0` Unbekannt · `4` Mischabwasser · `5` Regenabwasser · `106` Reinabwasser · `7` Schmutzabwasser · `1` Bachabwasser · `102` Entlastetes Mischabwasser · `103` Industrieabwasser · `111` Andere · `1100` Bergwasser · `1101` Strassenabwasser |
| 4 | Knoten *(abgeleitet; im WebGIS „“)* | verweis |  |  |  | → `AWK_ABWASSERKNOTEN` [art_bauwerk]: [bezeichnung] |
| 5 | Knoten *(abgeleitet; im WebGIS „“)* | verweis |  |  |  | → `AWK_ABWASSERKNOTEN` [art_bauwerk]: [bezeichnung] |
| 6 | Typ AA | auswahl | ● |  |  | `0` Unbekannt · `1` PAA · `2` SAA |
| 7 | Funktion hier./hydr. | auswahl | ● |  |  | `0` Unbekannt · `5` Liegenschaftsentwässerung · `106` Sammelkanal · `3` Hauptsammelkanal · `2` Gewässer · `108` Strassenentwässerung · `104` Hauptsammelkanal, regional · `107` Sanierungsleitung · `14` Rinne · `115` Andere |
| 8 | Funktion hier./hydr. – hydr. *(abgeleitet; im WebGIS „“)* | auswahl | ● |  |  | `0` Unbekannt · `4` Freispiegelleitung · `5` Pumpendruckleitung · `101` Drainagetransportleitung · `102` Drosselleitung · `103` Dükerleitung · `106` Sickerleitung · `107` Speicherleitung · `108` Spülleitung · `111` Andere · `116` Vakuumleitung · `1100` Belagsrinne Wasserschale · `1101` Entwaesserungsgraben befestigt · `1102` Entwaesserungsgraben unbefestigt · `1103` Schlitzrinne · `1104` Wasserrinne mit Rost |
| 9 | Status | auswahl | ● |  |  | `0` Unbekannt · `1` In Betrieb · `2` Ausser Betrieb · `5` Tot/Aufgehoben, verfüllt |
| 10 | Baujahr *(abgeleitet; im WebGIS „“)* | zahl | ● |  | Integer |  |
| 11 | Öffnungsform | auswahl |  |  |  | `0` Unbekannt · `101` Kreis · `102` Parabel · `103` Rechteck · `104` Andere |
| 12 | Breite/Länge [m] | zahl |  |  | Float |  |
| 13 | Breite/Länge [m] – Länge [m] *(abgeleitet; im WebGIS „“)* | zahl |  |  | Float |  |
| 14 | Sohlenhöhe | text |  |  |  |  |
| 15 | Geländehöhe *(abgeleitet; im WebGIS „“)* | zahl |  |  | Float |  |
| 16 | Ebene | auswahl |  |  |  | `0` Ebene 0 · `1` Ebene 1 · `2` Ebene -1 · `3` Ebene 2 · `4` Ebene -2 · `5` Ebene 3 · `6` Ebene -3 · `7` Ebene 4 · `8` Ebene -4 · `9` Ebene 5 · `10` Ebene -5 · `11` Ebene 6 · `12` Ebene -6 · `13` Ebene 7 · `14` Ebene -7 · `15` Ebene 8 · `16` Ebene -8 · `17` Ebene 9 · `18` Ebene -9 · `19` Ebene 10 · `20` Ebene -10 |
| 17 | Bemerkung | mehrzeilig |  |  |  |  |

### Daten II

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Lagebest./-genauigkeit | auswahl | ● |  |  | `0` Unbekannt · `101` Ungenau · `102` Genau |
| 2 | Lagebest./-genauigkeit – -genauigkeit *(abgeleitet; im WebGIS „“)* | auswahl | ● |  |  | `0` Unbekannt · `101` > 50 cm · `102` +/- 10 cm · `103` +/- 3 cm · `104` +/- 50 cm |
| 3 | Höhenbest./-genauigkeit | auswahl | ● |  |  | `0` Unbekannt · `101` Ungenau · `102` Genau |
| 4 | Höhenbest./-genauigkeit – -genauigkeit *(abgeleitet; im WebGIS „“)* | auswahl | ● |  |  | `0` Unbekannt · `101` > 6 cm · `102` +/- 1 cm · `103` +/- 3 cm · `104` +/- 6 cm |
| 5 | Überfallkante | auswahl |  |  |  | `0` Unbekannt · `101` Rechteckig · `102` Rund · `103` Scharfkantig · `104` Andere |
| 6 | Kote min./max. | zahl |  |  | Float |  |
| 7 | Kote min./max. – max. *(abgeleitet; im WebGIS „“)* | zahl |  |  | Float |  |
| 8 | Rückstaukote | zahl |  |  | Float |  |
| 9 | Fabrikat | text |  |  |  |  |
| 10 | Steuerung/Verstellbarkeit | auswahl |  |  |  | `0` Unbekannt · `101` Geregelt · `102` Gesteuert · `103` Keine |
| 11 | Steuerung/Verstellbarkeit – Verstellbarkeit *(abgeleitet; im WebGIS „“)* | auswahl |  |  |  | `0` Unbekannt · `101` Fest · `102` Verstellbar |
| 12 | Signalübermittlung | auswahl |  |  |  | `0` Unbekannt · `101` Empfangen · `102` Senden · `103` Senden, empfangen |
| 13 | Antrieb | auswahl |  |  |  | `0` Unbekannt · `101` Benzinmotor · `102` Dieselmotor · `103` Elektromotor · `104` Hydraulisch · `105` Keiner · `106` Manuell · `107` Pneumatisch · `108` Andere |
| 14 | Wehrschwellenzahl | auswahl |  |  |  | `0` Unbekannt · `101` Beidseitig · `102` Einseitig |
| 15 | Steuerungszentrale | verweis |  |  |  | → `AWK_VSA_STEUERUNGSZENTRALE` [vsa_bezeichnung] |

### Bauwerksteile

**Aufklappliste „Beckenentleerung“** (Untermenü) – Tabelle `AWK_BAUWERKSTEIL`, bearbeitbar, mehrere Zeilen; Spalten: Bezeichnung · Art

### Administrativ

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Eigentümer | auswahl | ● |  |  | `16853e1b-1f90-49e6-b762-7ec9ebd7bb59` Altdorf · `a4efbb6e-f8a6-4c65-a9df-be9ff1356dea` Andermatt · `65f14d2d-8f4e-45de-9635-ae76774e61db` Attinghausen · `afd5affb-1ce7-49d5-af42-c7e52641dc58` AWU_von_oeffentlich · `86dd0b68-c510-4926-90eb-c5832e4663c4` AWU_von_privat · `df1f763b-7f01-4d4d-a22c-14476c7a3a9b` Bund · `4c18a435-f416-4814-a9b4-b68d98189cc4` Bund Astra · `7c74fab1-4121-4cb9-bcfd-00eea3b4c204` Bürglen · `07e5db4a-ecd0-4c23-bd5d-e144ec68d28e` Erstfeld · `6108523b-a8f3-42f9-bf88-1492bdea77d1` Flüelen · `1035c742-cb3c-44d5-a7c4-ee3db1ce7a96` Göschenen · `c929ee69-5218-4366-9691-a8662f134fe4` Gurtnellen · `07e608b7-d1c9-4700-99c6-e0cb65b1b4fd` Hospental · `d1647b72-5b4b-4940-af16-1f5325b97ee8` Isenthal · `58d1c876-3d16-47da-8b38-b509bbc0cbca` Kanton Uri · `357bfc90-bf4b-4e5d-87d6-277a76eefbbd` Meliorationsgen. Reussebene Uri · `cbb47e18-cbc0-48cc-98ea-c37cc56622f9` Meliorationsgen. Seedorf · `f8186a5d-399a-443c-921f-460dff3d0566` oeff_Rechtl_Koerperschaften · `7692de6b-dce2-485c-8f01-190419e4c32f` Privat · `ff2b72c6-c8d8-4a52-b3a0-1ab7810c5c69` Realp · `54a8d062-7796-4eee-b4d5-60a9d1084f9d` RUAG · `9f6939a7-ba0a-47c6-9c63-9c44cddfddf3` Schattdorf · `9a2739a5-dd1d-401d-b060-0c37a7b75362` Seedorf · `52da3b7e-760d-4659-87fd-a23c977d0941` Seelisberg · `6b3fb54d-d3a4-41bd-91e0-542e9ecd4e8e` Silenen · `db0e9992-5a10-4c5a-853d-a1e9637042cb` Sisikon · `43c22364-adb7-40c2-8759-0dbebf879de0` Spiringen · `7747a36a-6420-41e2-bbe8-64ff28a7849d` unbekannt · `289c8fc2-ee43-4da2-bb81-6592880080b7` Unterschächen · `95844aa7-7306-4cea-88f2-4629a4792c4c` Wassen |
| 2 | Betreiber | auswahl | ● |  |  | `16853e1b-1f90-49e6-b762-7ec9ebd7bb59` Altdorf · `a4efbb6e-f8a6-4c65-a9df-be9ff1356dea` Andermatt · `65f14d2d-8f4e-45de-9635-ae76774e61db` Attinghausen · `afd5affb-1ce7-49d5-af42-c7e52641dc58` AWU_von_oeffentlich · `86dd0b68-c510-4926-90eb-c5832e4663c4` AWU_von_privat · `df1f763b-7f01-4d4d-a22c-14476c7a3a9b` Bund · `4c18a435-f416-4814-a9b4-b68d98189cc4` Bund Astra · `7c74fab1-4121-4cb9-bcfd-00eea3b4c204` Bürglen · `07e5db4a-ecd0-4c23-bd5d-e144ec68d28e` Erstfeld · `6108523b-a8f3-42f9-bf88-1492bdea77d1` Flüelen · `1035c742-cb3c-44d5-a7c4-ee3db1ce7a96` Göschenen · `c929ee69-5218-4366-9691-a8662f134fe4` Gurtnellen · `07e608b7-d1c9-4700-99c6-e0cb65b1b4fd` Hospental · `d1647b72-5b4b-4940-af16-1f5325b97ee8` Isenthal · `58d1c876-3d16-47da-8b38-b509bbc0cbca` Kanton Uri · `357bfc90-bf4b-4e5d-87d6-277a76eefbbd` Meliorationsgen. Reussebene Uri · `cbb47e18-cbc0-48cc-98ea-c37cc56622f9` Meliorationsgen. Seedorf · `f8186a5d-399a-443c-921f-460dff3d0566` oeff_Rechtl_Koerperschaften · `7692de6b-dce2-485c-8f01-190419e4c32f` Privat · `ff2b72c6-c8d8-4a52-b3a0-1ab7810c5c69` Realp · `54a8d062-7796-4eee-b4d5-60a9d1084f9d` RUAG · `9f6939a7-ba0a-47c6-9c63-9c44cddfddf3` Schattdorf · `9a2739a5-dd1d-401d-b060-0c37a7b75362` Seedorf · `52da3b7e-760d-4659-87fd-a23c977d0941` Seelisberg · `6b3fb54d-d3a4-41bd-91e0-542e9ecd4e8e` Silenen · `db0e9992-5a10-4c5a-853d-a1e9637042cb` Sisikon · `43c22364-adb7-40c2-8759-0dbebf879de0` Spiringen · `7747a36a-6420-41e2-bbe8-64ff28a7849d` unbekannt · `289c8fc2-ee43-4da2-bb81-6592880080b7` Unterschächen · `95844aa7-7306-4cea-88f2-4629a4792c4c` Wassen |
| 3 | Einleitstelle | text |  |  | String |  |
| 4 | Akten | mehrzeilig |  |  |  |  |

### Unterhalt

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Zustand/Sanierungsbedarf | auswahl |  |  |  | `0` Unbekannt · `100` Nicht mehr funktionstüchtig (Z0) · `101` Starke Mängel (Z1) · `102` Mittlere Mängel (Z2) · `103` Leichte Mängel (Z3) · `104` Keine Mängel (Z4) · `1000` nicht beurteilt |
| 2 | Zustand/Sanierungsbedarf – Sanierungsbedarf *(abgeleitet; im WebGIS „“)* | auswahl |  |  |  | `0` Unbekannt · `101` Dringend · `102` Kurzfristig · `103` Mittelfristig · `104` Langfristig · `105` Keiner · `106` Saniert |
| 3 | Inspektionsintervall [Jahr] | zahl |  |  | Float |  |
| 4 | Spülintervall [Jahr] | zahl |  |  | Float |  |
| 5 | Systemgrenze | auswahl |  |  |  | `0` unbekannt · `1` keine · `2` Uebergabepunkt · `3` Uebernahmepunkt |

### Hydraulik

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Überlaufcharakteristik | verweis |  |  |  | → `AWH_UEBERLAUFCHARAKTERISTIK` [bezeichnung] |
| 2 | Hydraulische Geometrie | verweis |  |  |  | → `AWH_HYDR_GEOMETRIE` [bezeichnung] |
| 3 | Überfalllänge [m] | zahl |  |  | Float |  |
| 4 | Qan ist [l/s] | zahl |  |  | Float |  |
| 5 | Qan Dim. [l/s] | zahl |  |  | Float |  |
| 6 | Überlaufdauer [h] | zahl |  |  | Float |  |
| 7 | Überlauffracht [kg/Jahr] | text |  |  |  |  |
| 8 | Überlaufhäufigkeit [Anzahl/Jahr] | text |  |  |  |  |
| 9 | Überlaufmenge [m³] | zahl |  |  | Float |  |

### Metadaten

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Erstellt am (UTC) | datum |  | ● | Date |  |
| 2 | Erstellt von | text |  | ● |  |  |
| 3 | Geändert am (UTC) | datum |  | ● | Date |  |
| 4 | Geändert von | text |  | ● |  |  |


---

## 30. Absperr-/Drosselorgan

Tabelle `AWK_ABWASSERKNOTEN`, Subtyp `art_bauwerk=8`, Maskentitel im WebGIS: „Absperr-/Drosselorgan“

46 Felder, davon 21 Auswahlfelder. Bereiche (Menüs) in Reihenfolge: **Kopf** → **Daten** → **Daten II** → **Bauwerksteile** → **Administrativ** → **Unterhalt** → **Hydraulik** → **Metadaten**

### Kopf

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Bezeichnung | text | ● |  |  |  |
| 2 | OBJECTID | text |  | ● |  |  |
| 3 | Bezeichnung alter./hist. | text |  |  |  |  |
| 4 | Bezeichnung alter./hist. – hist. *(abgeleitet; im WebGIS „“)* | text |  |  |  |  |
| 5 | Rechtswert/Hochwert | zahl |  | ● | Float |  |
| 6 | Rechtswert/Hochwert – Hochwert *(abgeleitet; im WebGIS „“)* | zahl |  | ● | Float |  |

### Daten

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Bauwerksart Absperr-/Drosselorgan *(abgeleitet; im WebGIS „“)* | auswahl | ● |  |  | `500` Unbekannt · `501` Blende · `502` Dammbalken · `503` Drosselklappe · `504` Drosselschieber · `505` Rückstauklappe · `506` Schieber · `507` Schlauchdrossel · `508` Schütze · `509` Wirbeldrossel · `510` Andere · `511` Drosselstrecke · `512` Leapingwehr · `513` Pumpe · `514` Stauschild |
| 2 | Nutzungsart | auswahl | ● |  |  | `0` Unbekannt · `4` Mischabwasser · `5` Regenabwasser · `106` Reinabwasser · `7` Schmutzabwasser · `1` Bachabwasser · `102` Entlastetes Mischabwasser · `103` Industrieabwasser · `111` Andere · `1100` Bergwasser · `1101` Strassenabwasser |
| 3 | Knoten *(abgeleitet; im WebGIS „“)* | verweis |  |  |  | → `AWK_ABWASSERKNOTEN` [art_bauwerk]: [bezeichnung] |
| 4 | Typ AA | auswahl | ● |  |  | `0` Unbekannt · `1` PAA · `2` SAA |
| 5 | Funktion hier./hydr. | auswahl | ● |  |  | `0` Unbekannt · `5` Liegenschaftsentwässerung · `106` Sammelkanal · `3` Hauptsammelkanal · `2` Gewässer · `108` Strassenentwässerung · `104` Hauptsammelkanal, regional · `107` Sanierungsleitung · `14` Rinne · `115` Andere |
| 6 | Funktion hier./hydr. – hydr. *(abgeleitet; im WebGIS „“)* | auswahl | ● |  |  | `0` Unbekannt · `4` Freispiegelleitung · `5` Pumpendruckleitung · `101` Drainagetransportleitung · `102` Drosselleitung · `103` Dükerleitung · `106` Sickerleitung · `107` Speicherleitung · `108` Spülleitung · `111` Andere · `116` Vakuumleitung · `1100` Belagsrinne Wasserschale · `1101` Entwaesserungsgraben befestigt · `1102` Entwaesserungsgraben unbefestigt · `1103` Schlitzrinne · `1104` Wasserrinne mit Rost |
| 7 | Status | auswahl | ● |  |  | `0` Unbekannt · `1` In Betrieb · `2` Ausser Betrieb · `5` Tot/Aufgehoben, verfüllt |
| 8 | Baujahr *(abgeleitet; im WebGIS „“)* | zahl | ● |  | Integer |  |
| 9 | Ebene | auswahl |  |  |  | `0` Ebene 0 · `1` Ebene 1 · `2` Ebene -1 · `3` Ebene 2 · `4` Ebene -2 · `5` Ebene 3 · `6` Ebene -3 · `7` Ebene 4 · `8` Ebene -4 · `9` Ebene 5 · `10` Ebene -5 · `11` Ebene 6 · `12` Ebene -6 · `13` Ebene 7 · `14` Ebene -7 · `15` Ebene 8 · `16` Ebene -8 · `17` Ebene 9 · `18` Ebene -9 · `19` Ebene 10 · `20` Ebene -10 |
| 10 | Bemerkung | mehrzeilig |  |  |  |  |

### Daten II

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Lagebest./-genauigkeit | auswahl | ● |  |  | `0` Unbekannt · `101` Ungenau · `102` Genau |
| 2 | Lagebest./-genauigkeit – -genauigkeit *(abgeleitet; im WebGIS „“)* | auswahl | ● |  |  | `0` Unbekannt · `101` > 50 cm · `102` +/- 10 cm · `103` +/- 3 cm · `104` +/- 50 cm |
| 3 | Höhenbest./-genauigkeit | auswahl | ● |  |  | `0` Unbekannt · `101` Ungenau · `102` Genau |
| 4 | Höhenbest./-genauigkeit – -genauigkeit *(abgeleitet; im WebGIS „“)* | auswahl | ● |  |  | `0` Unbekannt · `101` > 6 cm · `102` +/- 1 cm · `103` +/- 3 cm · `104` +/- 6 cm |
| 5 | Öffnung ist [mm] | zahl |  |  | Integer |  |
| 6 | Querschnitt [m²] *(abgeleitet; im WebGIS „“)* | zahl |  |  | Float |  |
| 7 | Öffnung ist optimiert [mm] | zahl |  |  | Integer |  |
| 8 | Wirksamer Querschnitt [m²] *(abgeleitet; im WebGIS „“)* | zahl |  |  | Float |  |
| 9 | Rückstaukote | zahl |  |  | Float |  |
| 10 | Fabrikat | text |  |  |  |  |
| 11 | Steuerung/Verstellbarkeit | auswahl |  |  |  | `0` Unbekannt · `101` Geregelt · `102` Gesteuert · `103` Keine |
| 12 | Steuerung/Verstellbarkeit – Verstellbarkeit *(abgeleitet; im WebGIS „“)* | auswahl |  |  |  | `0` Unbekannt · `101` Fest · `102` Verstellbar |
| 13 | Signalübermittlung/Stellung | auswahl |  |  |  | `0` Unbekannt · `101` Empfangen · `102` Senden · `103` Senden, empfangen |
| 14 | Signalübermittlung/Stellung – Stellung *(abgeleitet; im WebGIS „“)* | auswahl |  |  |  | `0` Unbekannt · `101` Offen · `102` Geschlossen |
| 15 | Antrieb | auswahl |  |  |  | `0` Unbekannt · `101` Benzinmotor · `102` Dieselmotor · `103` Elektromotor · `104` Hydraulisch · `105` Keiner · `106` Manuell · `107` Pneumatisch · `108` Andere |
| 16 | Steuerungszentrale | verweis |  |  |  | → `AWK_VSA_STEUERUNGSZENTRALE` [vsa_bezeichnung] |

### Bauwerksteile

**Aufklappliste „Rückstausicherung“** (Untermenü) – Tabelle `AWK_BAUWERKSTEIL`, bearbeitbar, mehrere Zeilen; Spalten: Bezeichnung · Art

**Aufklappliste „Beckenentleerung“** (Untermenü) – Tabelle `AWK_BAUWERKSTEIL`, bearbeitbar, mehrere Zeilen; Spalten: Bezeichnung · Art

### Administrativ

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Eigentümer | auswahl | ● |  |  | `16853e1b-1f90-49e6-b762-7ec9ebd7bb59` Altdorf · `a4efbb6e-f8a6-4c65-a9df-be9ff1356dea` Andermatt · `65f14d2d-8f4e-45de-9635-ae76774e61db` Attinghausen · `afd5affb-1ce7-49d5-af42-c7e52641dc58` AWU_von_oeffentlich · `86dd0b68-c510-4926-90eb-c5832e4663c4` AWU_von_privat · `df1f763b-7f01-4d4d-a22c-14476c7a3a9b` Bund · `4c18a435-f416-4814-a9b4-b68d98189cc4` Bund Astra · `7c74fab1-4121-4cb9-bcfd-00eea3b4c204` Bürglen · `07e5db4a-ecd0-4c23-bd5d-e144ec68d28e` Erstfeld · `6108523b-a8f3-42f9-bf88-1492bdea77d1` Flüelen · `1035c742-cb3c-44d5-a7c4-ee3db1ce7a96` Göschenen · `c929ee69-5218-4366-9691-a8662f134fe4` Gurtnellen · `07e608b7-d1c9-4700-99c6-e0cb65b1b4fd` Hospental · `d1647b72-5b4b-4940-af16-1f5325b97ee8` Isenthal · `58d1c876-3d16-47da-8b38-b509bbc0cbca` Kanton Uri · `357bfc90-bf4b-4e5d-87d6-277a76eefbbd` Meliorationsgen. Reussebene Uri · `cbb47e18-cbc0-48cc-98ea-c37cc56622f9` Meliorationsgen. Seedorf · `f8186a5d-399a-443c-921f-460dff3d0566` oeff_Rechtl_Koerperschaften · `7692de6b-dce2-485c-8f01-190419e4c32f` Privat · `ff2b72c6-c8d8-4a52-b3a0-1ab7810c5c69` Realp · `54a8d062-7796-4eee-b4d5-60a9d1084f9d` RUAG · `9f6939a7-ba0a-47c6-9c63-9c44cddfddf3` Schattdorf · `9a2739a5-dd1d-401d-b060-0c37a7b75362` Seedorf · `52da3b7e-760d-4659-87fd-a23c977d0941` Seelisberg · `6b3fb54d-d3a4-41bd-91e0-542e9ecd4e8e` Silenen · `db0e9992-5a10-4c5a-853d-a1e9637042cb` Sisikon · `43c22364-adb7-40c2-8759-0dbebf879de0` Spiringen · `7747a36a-6420-41e2-bbe8-64ff28a7849d` unbekannt · `289c8fc2-ee43-4da2-bb81-6592880080b7` Unterschächen · `95844aa7-7306-4cea-88f2-4629a4792c4c` Wassen |
| 2 | Betreiber | auswahl | ● |  |  | `16853e1b-1f90-49e6-b762-7ec9ebd7bb59` Altdorf · `a4efbb6e-f8a6-4c65-a9df-be9ff1356dea` Andermatt · `65f14d2d-8f4e-45de-9635-ae76774e61db` Attinghausen · `afd5affb-1ce7-49d5-af42-c7e52641dc58` AWU_von_oeffentlich · `86dd0b68-c510-4926-90eb-c5832e4663c4` AWU_von_privat · `df1f763b-7f01-4d4d-a22c-14476c7a3a9b` Bund · `4c18a435-f416-4814-a9b4-b68d98189cc4` Bund Astra · `7c74fab1-4121-4cb9-bcfd-00eea3b4c204` Bürglen · `07e5db4a-ecd0-4c23-bd5d-e144ec68d28e` Erstfeld · `6108523b-a8f3-42f9-bf88-1492bdea77d1` Flüelen · `1035c742-cb3c-44d5-a7c4-ee3db1ce7a96` Göschenen · `c929ee69-5218-4366-9691-a8662f134fe4` Gurtnellen · `07e608b7-d1c9-4700-99c6-e0cb65b1b4fd` Hospental · `d1647b72-5b4b-4940-af16-1f5325b97ee8` Isenthal · `58d1c876-3d16-47da-8b38-b509bbc0cbca` Kanton Uri · `357bfc90-bf4b-4e5d-87d6-277a76eefbbd` Meliorationsgen. Reussebene Uri · `cbb47e18-cbc0-48cc-98ea-c37cc56622f9` Meliorationsgen. Seedorf · `f8186a5d-399a-443c-921f-460dff3d0566` oeff_Rechtl_Koerperschaften · `7692de6b-dce2-485c-8f01-190419e4c32f` Privat · `ff2b72c6-c8d8-4a52-b3a0-1ab7810c5c69` Realp · `54a8d062-7796-4eee-b4d5-60a9d1084f9d` RUAG · `9f6939a7-ba0a-47c6-9c63-9c44cddfddf3` Schattdorf · `9a2739a5-dd1d-401d-b060-0c37a7b75362` Seedorf · `52da3b7e-760d-4659-87fd-a23c977d0941` Seelisberg · `6b3fb54d-d3a4-41bd-91e0-542e9ecd4e8e` Silenen · `db0e9992-5a10-4c5a-853d-a1e9637042cb` Sisikon · `43c22364-adb7-40c2-8759-0dbebf879de0` Spiringen · `7747a36a-6420-41e2-bbe8-64ff28a7849d` unbekannt · `289c8fc2-ee43-4da2-bb81-6592880080b7` Unterschächen · `95844aa7-7306-4cea-88f2-4629a4792c4c` Wassen |
| 3 | Akten | mehrzeilig |  |  |  |  |

### Unterhalt

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Zustand/Sanierungsbedarf | auswahl |  |  |  | `0` Unbekannt · `100` Nicht mehr funktionstüchtig (Z0) · `101` Starke Mängel (Z1) · `102` Mittlere Mängel (Z2) · `103` Leichte Mängel (Z3) · `104` Keine Mängel (Z4) · `1000` nicht beurteilt |
| 2 | Zustand/Sanierungsbedarf – Sanierungsbedarf *(abgeleitet; im WebGIS „“)* | auswahl |  |  |  | `0` Unbekannt · `101` Dringend · `102` Kurzfristig · `103` Mittelfristig · `104` Langfristig · `105` Keiner · `106` Saniert |
| 3 | Inspektionsintervall [Jahr] | zahl |  |  | Float |  |
| 4 | Spülintervall [Jahr] | zahl |  |  | Float |  |
| 5 | Systemgrenze | auswahl |  |  |  | `0` unbekannt · `1` keine · `2` Uebergabepunkt · `3` Uebernahmepunkt |

### Hydraulik

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Hydraulische Geometrie | verweis |  |  |  | → `AWH_HYDR_GEOMETRIE` [bezeichnung] |
| 2 | Hydraulische Kennwerte | verweis |  |  |  | → `AWH_HYDR_KENNWERTE` [bezeichnung] |

### Metadaten

| # | Feld | Art | P | R | Länge/Einheit | Auswahlwerte |
|---:|---|---|:-:|:-:|---|---|
| 1 | Erstellt am (UTC) | datum |  | ● | Date |  |
| 2 | Erstellt von | text |  | ● |  |  |
| 3 | Geändert am (UTC) | datum |  | ● | Date |  |
| 4 | Geändert von | text |  | ● |  |  |

