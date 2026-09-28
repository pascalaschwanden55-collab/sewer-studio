# Funktionsabgleich: WinCan VX ↔ SewerStudio

**Datum:** 2026-09-28
**WinCan-Referenz:** Installation `C:\Program Files (x86)\CDLAB` (16 Module, 5'284 Dateien, 4,5 GB; Programmdateien vom 2026-03-04) und `C:\ProgramData\CDLAB` (nur Dongle-Treiber `MultiUSB`, `sgkst`)
**SewerStudio-Referenz:** `docs/CODEBASE-KARTE.md`, `VSA_WinCan_Abgleich/ABGLEICH_VSA_WinCan.md` (Code-Abgleich 2026-07-10)

---

## Methodik / Einschränkung

- Ausgewertet wurden nur Dateinamen, Konfigurationsdateien und offene XML/JSON-Daten (Kataloge, Vorlagen, Datenmodell `MetaResources/*.csdl`). Nichts dekompiliert, nichts verändert.
- Klartexte und Bewertungsregeln in den WinCan-Katalogen sind verschlüsselt (`CE_ObsText`, `RAT_Grade`, `CLASS_Item`) und wurden nicht ausgewertet.
- Der SewerStudio-Quellcode wurde **nicht** durchsucht. Punkte mit Status **„prüfen"** sind daher offen: Die Funktion kann in SewerStudio schon vorhanden sein.
- Die Punkte unter „Mögliche Lücken" sind **neue Funktionen**. Sie gehören nicht in den Ferienlauf, der nur Wartbarkeit verbessert.

---

## Kernergebnis

1. **VSA-Codes:** erledigt (Abgleich 2026-07-10, Manifest 719 Codes inkl. `BDGZ`/`DDGZ`).
2. **KI:** Im ganzen CDLAB-Ordner liegt keine Modelldatei (`.onnx`, `.pt`, `.pth`, `.engine`, `.tflite`, `.pb`, `.bin`). Vorhanden sind nur Client-Bibliotheken (`CDLAB.WinCan.AI.SDK.dll`, `CDLAB.WinCan.AI.UI.dll`, `CDLAB.WinCan.AIScoring.dll`). Die WinCan-KI rechnet also nicht lokal auf diesem PC.
3. **Schweiz:** WinCan bringt VSA-2019 (DE/FR/IT), VSA-DSS (DE/FR/IT), ERZ (Zürich), Bern und weitere CH-Kataloge mit. In `WinCanMerger/App_Data/Config/CrossStandards.json` gibt es 107 Umwandlungen zwischen Normen, davon 82 mit Schweizer Normen, alle in beide Richtungen.
4. **Sanierungskataloge:** In `Catalogs/MethodCatalogs` gibt es nur NL, UK, USA und DE, keinen Schweizer Katalog. Mit Massnahmen- und NPK-Logik ist SewerStudio für die AWU weiter.

---

## Korrektur zum Code-Abgleich vom 2026-07-10

`ABGLEICH_VSA_WinCan.md` nennt für `EN13508_VSA-2019_CH_DEU_SEC` die Version 1.0.0.66 (2018-04-09). Das ist der Versionskopf (`CATVER`). Der Katalog selbst hat:

- `CAT_Version` 2.0.14.1
- `CAT_Created` 2019-09-03
- Bewertungsregeln (`RATING`) zuletzt geändert am 2023-01-11

Die Aussage „kein VSA-2020/2023-Katalog für die Schweiz installiert" bleibt richtig.

---

## Mögliche Lücken (nach Nutzen für die AWU)

| # | Thema | Beleg in WinCan | SewerStudio | Nutzen |
|---|---|---|---|---|
| 1 | Pflichtfeld- und Plausibilitätsprüfung vor Lieferung | `WinCanMerger/App_Data/Templates/VSA-2019.xml`: 548 Felder, 56 Pflichtfelder, 55 Wertebereiche, 315 Textlängen-Regeln, 83 Wertelisten | prüfen | Unvollständige Stammdaten vor dem XTF-Export oder der GEONIS-Übergabe erkennen |
| 2 | Offerte ↔ Ausmass ↔ Rechnung je Haltung | Datenmodell `WCUSER`: `JOB`, `JOBPOS`, `JOBPOSCOST`, `JOBPOSSTAT`, `JOBINVOICE`, `JOBINVPOS`, `JOBINVCOST`, `JOBREPORT` | prüfen (Kosten/NPK vorhanden) | Die Rechnungsprüfung (Andermatt, Bürglen, Silenen) direkt an der Haltung statt in separaten Excel-Dateien |
| 3 | Reinigung als eigene Inspektion | Vorlage VSA-2019: Ansichten `grid_s_inspection_cln`, `detail_s_inspection_cln`, `detail_n_inspection_cln` (Datum ist Pflichtfeld bei der Haltung) | prüfen | Spülen + TV (Silenen, Göschenen) nachvollziehbar dokumentieren |
| 4 | Änderungsverlauf und Papierkorb | `PROJHIST`, `SECHIST`, `NODHIST`, `JOBHIST`, `USRLOG`, `USRTRASH` | prüfen | Nachvollziehbar, wer einen Code, eine Klasse oder eine Massnahme geändert hat |
| 5 | Export für WinCan mit Katalog VSA-2019 | 37 Codes aus VSA-KEK 2020 fehlen im installierten Katalog (siehe Abgleich 2026-07-10) | Empfehlung vom 2026-07-10: Mapping/Whitelist, Umsetzung prüfen | Fehlerfreier XTF-Austausch mit Unternehmern, die dieses WinCan nutzen |
| 6 | Getrennte Teilnoten je Haltung | Katalog VSA-2019: `RATOBJ` → `COL_ID_SCORE_S`, `COL_ID_SCORE_O`, `COL_ID_SCORE_H`; je Code `LIST_GradeS/O/H` | prüfen | Teilnoten getrennt ausweisen. Was „H" bedeutet, steht in WinCan nicht im Klartext |

### Pflichtfelder der Vorlage VSA-2019 (Auszug)

- **Haltung** (`detail_s`), u. a.: `OBJ_Key`, `OBJ_Type`, `OBJ_FromNode_REF`, `OBJ_ToNode_REF`, `OBJ_City`, `OBJ_Street`, `OBJ_Situation`, `OBJ_Usage`, `OBJ_Shape`, `OBJ_Size1`
- **Inspektion Haltung** (`detail_s_inspection`): `INS_InspectionDir`, `V_INS_DateOfStart`, `INS_Operator_REF`, `INS_Equipment_REF`, `INS_Camera_REF`, `INS_State`, `INS_Method`, `INS_Weather`, `INS_Purpose`, `INS_Cleaned`
- **Schacht** (`detail_n`): `OBJ_Type`, `OBJ_Key`, `OBJ_City`, `OBJ_Street`, `OBJ_DepthToInvert`
- **Inspektion Schacht** (`detail_n_inspection`): `INS_InspectionDir`, `INS_Operator_REF`, `V_INS_DateOfStart`, `INS_Weather`, `INS_Purpose`, `INS_Method`, `INS_State`
- **Schacht-Einstieg** (`detail_n_entry`): `NOE_Type`, `NOE_ClockPosition`, `NOE_Shape`, `NOE_Size1`, `NOE_Drop`
- **Schachtteile:** `detail_n_part_sha` (`NOP_Shape`, `NOP_Size1`), `detail_n_part_cha` (`NOP_Shape`)

---

## Nur bei Bedarf

Diese Punkte sind nur relevant, wenn Unternehmer solche Daten liefern oder ein Auftraggeber das Format verlangt:

| Modul in WinCan | Datei/Ordner |
|---|---|
| Neigungsmessung (Gegengefälle) | `CDLAB.WinCan.Inclination.dll` |
| Laser-Profilmessung | `LaserScan/`, `Spirallo/` |
| Scanner-Abwicklung (Panoramo) | `ScanExplorer/`, `CDLAB.WinCan.Unfolding.dll` |
| Leckortung ElectroScan | `CDLAB.WinCan.ElectroScan.dll` |
| Sonar, Drohne, Ortung (Vivax) | `CDLAB.SonarScan.dll`, `CDLAB.WinCan.Drone.dll`, `CDLAB.WinCan.VivaxApiClient.dll` |
| ISYBAU 2001–2024, DWA-M149 | Kataloge `EN13508_ISYBAU-*`, `EN13508_DWA-M149*` |
| IFC 2x3/4/4x3, Esri FileGDB, DWG/DXF | `Xbim.Ifc*.dll`, `FileGDBAPI.dll`, `CDLAB.WinCan.ExportDWG.dll` |

## Bewusst weglassen

- Mehrbenutzerbetrieb mit Rechten, Gruppen und Mandanten (`USER_PERMISSION`, `UGROUP`, `MANDATOR`)
- Datenbanken MSSQL/Oracle/SQL CE (`MetaResources/MSSQL|ORACLE|SQLCE`)
- Cloud-Anbindung WinCan Web (`CDLAB.WinCan.WebIntegration.dll`, `WebDownloader/`)

## Bereits vorhanden (laut Doku)

- VSA-Code-Manifest, XTF/DSS-Export, Importe aus PDF, XTF, WinCan, IBAK und KINS, Dedup/Zusammenführung (ADR-009), Medienverteilung, Kosten/NPK, QGIS-Brücke, WebGIS-Übertragung
