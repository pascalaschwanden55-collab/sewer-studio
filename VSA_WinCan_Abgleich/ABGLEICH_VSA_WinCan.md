# Abgleich VSA-Codes: SewerStudio-Manifest ↔ WinCan VX

**Datum:** 2026-07-10
**Manifest:** `vsa_kek_2020_catalog_manifest.json` (680 Codes, Version „VSA-KEK 2020")
**WinCan-Referenz:** Katalog `EN13508_VSA-2019_CH_DEU_{SEC,NOD}` — **v1.0.0.66, Stand 2018-04-09**
**Reale Referenzdaten:** Projekt `GEP_Altdorf_2025_Zone_1.09_27738_925` (SECOBS/NODOBS)

---

## Kernergebnis

Das Manifest ist gegenüber der WinCan-Praxis **vollständig und korrekt**. Es gibt **keine Abdeckungslücke** für real vorkommende Codes:

- **55 von 55** tatsächlich im WinCan-Projekt verwendeten OpCodes sind im Manifest vorhanden (0 Fehltreffer).
- 600 Codes sind in beiden Katalogen identisch vorhanden.

Der einzige nennenswerte Punkt ist ein **Versionsversatz**: Das Manifest ist „VSA-KEK 2020", der installierte WinCan-Schweiz-Katalog ist „VSA-2019" (Datenstand 2018). Ein VSA-2020/2023-Katalog für die Schweiz ist in dieser WinCan-Installation **nicht** vorhanden (nur NL- und AUS-Kataloge von 2020/2023).

---

## Diff-Übersicht (758 Codes gesamt)

| Kategorie | Anzahl | Bedeutung |
|---|---:|---|
| OK (in beiden) | 600 | Deckungsgleich |
| nur WinCan – Zwischencode | 76 | WinCan führt Ebene Hauptcode+Char1 (z. B. `BAFA`); Manifest hat die Blattcodes (`BAFAE`) → reine Modellierungsdifferenz, kein Verlust |
| nur Manifest – Basiscode | 41 | 3-stellige Hauptcodes (`BAF`, `BCA` …). Fehlen nur in meiner WinCan-Liste, weil diese aus `CE_CloseCode` (Blattcodes) stammt → **Methodenartefakt, keine echte Lücke** |
| nur Manifest – 2020-Erweiterung | 37 | Codes, die das Manifest über den WinCan-2019-Katalog hinaus hat (s. u.) |
| nur Manifest – Gruppe/Header | 2 | `BAG`, `BCCYY` (nicht selektierbar, korrekt) |
| **nur WinCan – KEIN Pendant** | **2** | **`BDGZ`, `DDGZ`** — einzige echte Katalog-Codes ohne Manifest-Eintrag |

---

## Konkrete Punkte für SewerStudio

### 1. Zwei Codes fehlen im Manifest (geringfügig)
`BDGZ` und `DDGZ` = „Keine Sicht – sonstige" (Kanal/Schacht). Das Manifest hat `BDGA/BDGB/BDGC` (keine Sicht: unter Wasser / Verschlammung / Dampf), aber keinen Sammel-/Sonstige-Code `…GZ`. → Falls WinCan-Importe diese liefern können, ergänzen; sonst vernachlässigbar.

### 2. Versionsversatz ist der eigentliche Interop-Punkt
37 Manifest-Codes existieren im WinCan-2019-Katalog nicht, u. a.:
`AEF` (Neue Baulänge), `BCDXP`/`BCEXP` (Distanzmessung Anfang/Ende), `BDBA…BDBM` (TV-Untersuchungs-Vorgaben), `DCGXA…DCGXCC` (Spezialprofil-Familie), `DCHAA/DCHAB`, `DCIAZ/DCIB`, `BDEBA…BDEBC`, `DDEBA…DDEBC`.
→ Beim Interlis-/XTF-Austausch mit **diesem** (2019er) WinCan werden solche Codes vom älteren Katalog evtl. nicht erkannt. Empfehlung: WinCan-Schweiz-Katalog auf VSA-KEK-2020 aktualisieren, damit beide auf gleichem Stand sind — oder beim Export ein Mapping/Whitelist für 2019-Kompatibilität vorsehen.

### 3. Selbst als ungültig markierter Code — ok
`DCHAA` trägt im Manifest bereits den Titel „…(ungültig: Ersatz DCHC)". Interne Hygiene stimmt; ggf. `isSelectable=false` setzen, damit er nicht neu vergeben wird.

### 4. Schacht-Codes (D-Gruppe) nur gegen Katalog geprüft
Im Referenzprojekt gab es **keine** Schachtbeobachtungen (NODOBS leer). Die D-Codes sind daher nur gegen den WinCan-Katalog, nicht gegen reale Nutzung validiert. Für eine vollständige Schacht-Validierung ein Projekt mit Schachtaufnahmen nachziehen.

---

## Methodik / Einschränkung
Die WinCan-Code-Liste wurde aus den lesbaren `CE_CloseCode`-Feldern der Katalog-XML gewonnen (`CE_ObsText` ist obfuskiert). Dadurch erscheinen reine Hauptcodes und einige Zwischenebenen scheinbar „nur im Manifest" — das sind Artefakte der Extraktion, keine echten Lücken. Die belastbare Aussage stützt sich auf die **realen OpCodes** aus der Projekt-Datenbank (55/55 abgedeckt).

Details je Code: siehe `vsa_wincan_codediff.csv`.
