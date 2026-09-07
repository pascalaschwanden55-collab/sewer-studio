# Inventar: SewerStudio-Nova-Optimiert-v2.html

Quelle: `c:\Sewer-Studio_KI_4.5\docs\reviews\2026-09-06-nova\optimiert\v2\SewerStudio-Nova-Optimiert-v2.html`
(1935 Zeilen, 224 486 Bytes, SHA-256 `e6ee31ac8a300f9b5881bdc1f22181ce5cd08862cdbadce96c4044f21edde936` laut PRUEFTABELLE.md Z. 187).
Aufbau der Datei: CSS Z. 7–358, HTML Z. 360–1056, JavaScript Z. 1057–1933.
Alle Zeilenangaben („Z.") beziehen sich auf diese Datei. Beschriftungen sind wörtlich übernommen.

---

## 1. Design-Tokens

### 1.1 Farbtokens je Stimmung

Drei Stimmungen (Vorschau-Leiste Z. 369–371, Einstellungen Z. 852):
- **„Hell · Glas"**: `:root` ohne Attribut bzw. `data-theme="light"` → Werte aus Z. 9–25.
- **„Dunkel · Cockpit"**: `:root[data-theme="dark"]` → Z. 41–52.
- **„System"**: kein `data-theme`-Attribut (Z. 1082); `@media (prefers-color-scheme: dark)` überschreibt `:root:not([data-theme="light"])` mit exakt denselben Werten wie Dunkel (Z. 27–40). System = Hell oder Dunkel je Windows-Einstellung, keine eigene dritte Palette.

| Token | Hell · Glas (Z. 9–25) | Dunkel · Cockpit (Z. 41–52; identisch Z. 27–40) |
|---|---|---|
| `color-scheme` | light | dark |
| `--bg` | `#EEF2F7` | `#0C1524` |
| `--bg2` | `#E4EAF2` | `#101B2E` |
| `--surface` | `#FFFFFF` | `#16223A` |
| `--surface2` | `#F6F8FB` | `#1B2940` |
| `--glass` | `rgba(255,255,255,.72)` | `rgba(16,27,48,.72)` |
| `--glass-border` | `rgba(20,40,80,.14)` | `rgba(140,190,255,.16)` |
| `--text` | `#14213A` | `#EAF0FA` |
| `--muted` | `#44546E` | `#B4C1D6` |
| `--faint` | `#4F5F78` | `#A5B3C8` |
| `--line` | `#D3DAE5` | `#2B3A55` |
| `--line2` | `#E4E9F0` | `#22304A` |
| `--accent` | `#1B5FD1` | `#7CC4FF` |
| `--accent-ink` | `#FFFFFF` | `#04101F` |
| `--accent-soft` | `rgba(27,95,209,.11)` | `rgba(124,196,255,.14)` |
| `--accent-text` | `#154FB0` | `#9AD3FF` |
| `--ki` | `#0A7F8E` | `#3FD6C6` |
| `--ki-soft` | `rgba(10,127,142,.12)` | `rgba(63,214,198,.14)` |
| `--ki-text` | `#0A6E7C` | `#7FE6DA` |
| `--z0` | `#D7263D` | `#FF5A62` |
| `--z1` | `#E06A0B` | `#FF9440` |
| `--z2` | `#E5B800` | `#F5D03A` |
| `--z3` | `#8E9A2B` | `#C2CB55` |
| `--z4` | `#2E9C4A` | `#5FD877` |
| `--z-ink` (Text auf Z1–Z4) | `#0B1220` | `#0B1220` |
| `--z0-ink` (Text auf Z0) | `#FFFFFF` | `#0B1220` |
| `--ok` | `#2E9C4A` | `#5FD877` |
| `--warn` | `#B8641A` | `#FFB066` |
| `--bad` | `#C2202F` | `#FF7A82` |
| `--ok-text` | `#1E7A36` | `#7FE39A` |
| `--warn-text` | `#8A4A0F` | `#FFB066` |
| `--bad-text` | `#B31B2A` | `#FF8A93` |
| `--ok-ink` (Text auf `--ok`/`--ki`-Flächen) | `#0B1220` | `#0B1220` |
| `--shadow` | `0 6px 20px rgba(16,28,51,.08)` | `0 10px 30px rgba(0,0,0,.45)` |
| `--engine-a` (Hintergrund-Verlauf oben links) | `rgba(27,95,209,.07)` | `rgba(124,196,255,.13)` |
| `--engine-b` (Verlauf unten rechts) | `rgba(10,127,142,.08)` | `rgba(63,214,198,.11)` |
| `--engine-line` (Netzlinien/Punkte) | `rgba(27,95,209,.09)` | `rgba(124,196,255,.14)` |

Nur im Hell-Block definiert, gelten in beiden Stimmungen (Z. 19–24):
- `--r: 10px` (Karten), `--rs: 7px` (kleine Rundung)
- `--font: "Segoe UI Variable Text","Segoe UI",system-ui,-apple-system,Arial,sans-serif`
- `--font-display: "Segoe UI Variable Display","Segoe UI",system-ui,Arial,sans-serif`
- `--font-data: "Cascadia Mono","Consolas","Courier New",monospace`
- Schriftskala: `--fs-xs: 12px`, `--fs-s: 13px`, `--fs-m: 14px`, `--fs-l: 15px`, `--fs-xl: 18px`, `--fs-title: 22px`, `--fs-display: 28px`

### 1.2 Feste Farbwerte ausserhalb der Tokens (in beiden Stimmungen gleich)
- Video-Fläche `.video` Hintergrund `#0b1220` (Z. 310); Rohrring `.ringv` Rand 12px `#1a2740`, Innenschatten `inset 0 0 60px #000`, Aussenring `0 0 0 26px #0f1a2e`, Füllung `radial-gradient(circle,#2b3a55 0%,#101a2c 65%,#0b1220 100%)` (Z. 314)
- OSD oben links: Text `#e8f0ff` auf `rgba(0,0,0,.5)` (Z. 312); Meterstand unten rechts: `#ffe58a` auf `rgba(0,0,0,.55)` (Z. 313)
- Bildkachel `.thumb`: Hintergrund `#16223A`, Text `#D5DEEC` (Z. 336)
- Dialog-Abdunkelung `.win`: `rgba(4,10,24,.5)` (Z. 299); Fensterschatten `0 30px 80px rgba(0,0,0,.5)` (Z. 302)
- Zustandschip-Rahmen `.zk`: `1px solid rgba(0,0,0,.18)` (Z. 161)
- Schalterknopf-Schatten `0 1px 3px rgba(0,0,0,.35)` (Z. 220)
- Puls-Keyframes mit festem `rgba(63,214,198,.55)` (Z. 354, entspricht dem dunklen `--ki`, auch im Hell-Thema)

### 1.3 Schriftgrössen (px), die nicht über die Skala laufen
- 11px: `.search kbd` (Z. 113), `.btn[data-demo]::after` ◌ (Z. 141), `.vchip small` (Z. 156), `.video .box span` (Z. 316), `.clock button` (Z. 334), `.kbd` (Z. 349), `.sec summary small` (Z. 286), SVG-Uhrzahlen im Rohrring (Z. 1527), Ring-Untertitel „mit Klasse" (Z. 1420), Schachtmass-Text (Z. 1534)
- 12px: `.avatar` (Z. 119), `.zk` (Z. 161), `.bar .k` (Z. 240), `.find .code` (Z. 268), `.find .m` (Z. 271), `.video .osd` (Z. 312)
- 13px: `.icon-btn` (Z. 142); 14px: `.nav .ico` (Z. 84), `.video .osdm` (Z. 313); 18px: Ring-Zentralzahl (Z. 1420)
- Grundschrift `body`: `13px/1.45 var(--font)` (Z. 57)
- Kleinste sichtbare Schrift laut Prüftabelle I1: 11 px (kbd „F1")

### 1.4 Rundungen
- `--r` 10px: `.card`, `.tablewrap`, `.drop`, `.video` (10px fest)
- `--rs` 7px: `.nav`, `.search .results`, `.more .menu`, `.row`, `.log`, `.note` (rechts), `.tabs`, `.sec`, `.find`, `.thumb`, `.toast`, `.kihint`, `.codes button`, `.sysinfo`, `.settingsnav button`
- 999px (Pille): `.seg`, `.btn`, `.search`, `.task`, `.vchip`, `.badge`, `.mtrack`, `.timeline`, `.switch`, `.progress`, `.bar .track`, `.gold .track`
- 6px: `.menubar button`, `.icon-btn`, `.more .menu button`, `.zk`, Formularfelder, `.legend button`, `.pick button`, `.clock button`, `.find .code`, `.dhd .tgl`, Schachtgrundriss-Rechteck (rx 6)
- 14px: `.winbox` (Z. 302); 8px: Markenzeichen `.brand .mark` (Z. 80); 5px: `kbd`, OSD-Felder; 4px: Video-Box und Label; 3px: Legenden-Swatch; 2px: Splitter-Linie; 50 %: Avatar, Punkte, Marker, Ring-Innenkreis

### 1.5 Rahmen, Schatten, Glas
- Standardrahmen `1px solid var(--line)` (Knöpfe, Eingaben, Fenster, Toasts) bzw. `var(--line2)` (Karten, Zeilen, Sektionen, Trennlinien)
- `.drop`: `1.5px dashed var(--line)` (Z. 206); `input[readonly]`: gestrichelt, Hintergrund `--surface2`, Text `--muted` (Z. 196); `.zk.zu` (nicht berechnet): gestrichelt, transparent (Z. 163)
- Geänderte Felder: `border-color: var(--warn)` plus `box-shadow: inset 3px 0 0 var(--warn)` (Z. 198); ausgewählte Zeilen/Navigation: `inset 3px 0 0 var(--accent)` (Z. 87, 174, 190, 218)
- Fokus: `outline: 2px solid var(--accent); outline-offset: 2px` (Z. 60); Tabellenzeile `-2px` (Z. 175); Eingaben `offset 0` (Z. 195)
- Glas: `.preview` `background: var(--glass); backdrop-filter: blur(14px)` (Z. 67); `.rail` `blur(18px)` (Z. 78); beide mit `border … var(--glass-border)`. Kein weiteres Element verwendet Glas. Backdrop-Filter ist NICHT stimmungsabhängig.
- Karten: `--shadow` (Z. 127). Suchliste, Menü, Toast ebenfalls `--shadow`.

### 1.6 Abstände (Auswahl der massgebenden Werte)
- Shell: `grid-template-columns: 220px 1fr` (Z. 77); unter 1100px Breite 64px Leiste ohne Texte (Z. 357)
- Leiste `.rail`: `padding: 12px 10px; gap: 3px` (Z. 78); `.brand` `padding: 4px 8px 12px`, Schrift `--fs-l` 700; Gruppe `padding: 8px 10px 2px`, `letter-spacing: .8px`, Grossbuchstaben, `--fs-xs` 700 (Z. 82); `.nav` `padding: 7px 10px; gap: 10px`, Symbol 18px breit (Z. 83–84)
- Menüleiste `padding: 3px 12px`, Knöpfe `3px 9px` (Z. 103–104); Kopfzeile `.topbar` `padding: 8px 16px; gap: 12px` (Z. 106); Suche `min-width: 300px; padding: 5px 12px` (Z. 109)
- Inhalt `.content` `padding: 14px 16px` (Z. 121); Karte `.hd` `padding: 12px 16px 0` (Z. 128), `.bd` `12px 16px 14px` (Z. 130); Seitenkopf `margin-bottom: 12px` (Z. 133); Raster `gap: 12px` (Z. 182–186, 226–237)
- Knöpfe `.btn` `padding: 6px 12px`, `--fs-s` 600 (Z. 134); `.btn.small` `4px 10px`, `--fs-xs` (Z. 139); `.badge` `2px 8px` (Z. 158); `.vchip` `4px 10px` (Z. 155); `.zk` `min-width 34px; height 22px` (Z. 161)
- Werkzeugleiste `gap: 6px; margin-bottom: 8px`, Trenner 1×22px (Z. 144–145)
- Tabelle: `th` `padding: 8px 10px`, `--fs-xs`, Grossbuchstaben, `letter-spacing .5px`, sticky (Z. 168); `td` `padding: 6px 10px; height: 32px; white-space: nowrap` (Z. 169); Zahlen rechtsbündig in `--font-data` mit `tabular-nums` (Z. 170)
- Formulare `.form` 2 Spalten `gap: 9px 12px` (Z. 191); Eingaben `padding: 5px 8px` (Z. 194); Beschriftung `--fs-xs` 600 `--muted`, `gap: 3px` (Z. 192)
- Arbeitsfläche Haltungen: Seitenpanel `--sidew` Standard 320px (Z. 253), Schublade `--drawerh` Standard 220px (Z. 273), Splitter 6px (Z. 252–253)
- Dialoge: `.winbox` `min(1360px,96vw) × min(860px,94vh)` (Z. 302); `.medium` `min(1180px,94vw) × min(760px,92vh)` (Z. 304); `.small` `min(560px,92vw)`, Höhe auto (Z. 303); Titel `padding: 8px 14px` (Z. 305); Körper `padding: 12px` (Z. 307)
- Toasts unten rechts `right/bottom 16px; max-width 420px; gap 8px` (Z. 344), 6000 ms (Z. 1292)

### 1.7 Was Glas konkret von Cockpit unterscheidet
Es gibt KEINE strukturellen Unterschiede; alle Regeln lesen dieselben Tokens. Unterschiede entstehen nur aus den Tokenwerten:
- **Hintergrund**: `#EEF2F7` mit sehr schwachen Verlaufsflecken (Blau .07 / Türkis .08) und Netzlinien .09 gegenüber `#0C1524` mit stärkeren Flecken (.13 / .11) und Linien .14.
- **Karten**: Weiss auf hellem Grau mit weichem Schatten (`0 6px 20px rgba(16,28,51,.08)`) gegenüber `#16223A` auf `#0C1524` mit dunklem Schatten (`0 10px 30px rgba(0,0,0,.45)`).
- **Leiste**: 72 % Weiss mit Blur 18px und Rand `rgba(20,40,80,.14)` gegenüber 72 % `rgb(16,27,48)` mit Rand `rgba(140,190,255,.16)`. Aktiver Eintrag beide: `--accent-soft` plus 3px Akzentstreifen links.
- **Tabelle**: Kopf `#F6F8FB` / Linien `#D3DAE5` gegenüber Kopf `#1B2940` / Linien `#2B3A55`; Auswahl `--accent-soft` (Blau 11 % bzw. Hellblau 14 %).
- **Akzent**: Kräftiges Blau `#1B5FD1` mit weissem Text gegenüber Hellblau `#7CC4FF` mit dunklem Text `#04101F`.
- **Zustandsklassen**: hell gesättigt (Z0 `#D7263D` mit weissem Text) gegenüber dunkel aufgehellt (Z0 `#FF5A62` mit dunklem Text `#0B1220`).

---

## 2. Bewegung („bewegt" / „ruhig")

- Umschalter: Vorschau-Leiste Z. 375–376 (`data-motion-set="on|off"`), Einstellungen Schalter „Bewegung reduzieren" `#swMotion` (Z. 853). Gespeichert als `nova.opt.motion` (Z. 1092). `setMotion` (Z. 1087–1094) setzt `data-motion="off"` am `<html>`.
- **„ruhig"** (Z. 355): `:root[data-motion="off"] *` und Pseudoelemente → `animation: none !important; transition: none !important`. Dasselbe bei Windows-Einstellung `prefers-reduced-motion: reduce` (Z. 356), auch wenn „bewegt" gewählt ist (`motionAllowed` Z. 1079 = Speicher `on` UND nicht reduziert).
- **Animationen und Übergänge** (nur bei „bewegt"):
  - `.pulse`: 9×9px Kreis in `--ki`, `animation: pulse 2.4s infinite` (Z. 353); Keyframes 0 % `box-shadow 0 0 0 0 rgba(63,214,198,.55)`, 70 % `0 0 0 8px rgba(63,214,198,0)`, 100 % `0 0 0 0 rgba(63,214,198,0)` (Z. 354). Vorkommen: Badge „Analyse bereit" im Training-Studio-Titel (Z. 984). Der Punkt in der Leiste (`#sysDot`, Z. 409) ist ein statischer `.dot` ohne Puls.
  - Navigationssymbol: `transition: transform .2s`, bei Hover `translateY(-1px) scale(1.12)` (Z. 84, 86)
  - Schalter `.switch::after`: `transition: left .15s` (Z. 220)
  - Fortschrittsbalken `.progress i`: `transition: width .3s` (Z. 223)
  - Schubladen-Pfeil `.dhd .chev`: `transform .2s`, zu = `rotate(-90deg)` (Z. 278–279); Sektionspfeil `.sec .chev`: `transform .2s`, offen = `rotate(90deg)` (Z. 287–288)
  - Primärknopf Hover: `filter: brightness(1.06)` (Z. 137), keine Dauer definiert
  - Player-Wiedergabe: `setInterval` 1000 ms, Position += Geschwindigkeit (Z. 1681) — kein CSS, läuft auch bei „ruhig"
  - Schattenlauf-Simulation: 5 Schritte à 250 ms (Z. 1864)
- **Hintergrund-Engine** (Canvas `#engine`, Z. 361, JS Z. 1900–1922): 60 Knoten, deterministisch gesät (LCG Startwert 7, Multiplikator 16807 mod 2147483647), Geschwindigkeit ±0,125 px je Bild, Radius 1,2–2,8 px; zwei Radialverläufe (Mittelpunkt 15 %/20 % Radius 45 % Breite mit `--engine-a`; Mittelpunkt 85 %/85 % Radius 50 % Breite mit `--engine-b`); Linien zwischen Knoten näher als 170 px × DPR mit Alpha `1 − d/max` in `--engine-line`; Knoten als gefüllte Kreise in `--engine-line`. Läuft nur, wenn `motionAllowed()` UND Schalter „Hintergrund-Engine" (`nova.opt.engine`, Standard ein) UND Tab sichtbar UND kein Dialog offen (Z. 1915). Sonst wird ein Standbild gezeichnet; bei ausgeschalteter Engine bleibt der Canvas leer (Z. 1906). Thema-Wechsel zeichnet neu (Z. 1085). Resize sät neu (Z. 1919).
- Einstellungs-Schalter „Hintergrund-Engine" (`#swEngine`, Z. 854) ist getrennt vom Bewegungsschalter.

---

## 3. Rahmen (Shell)

### 3.1 Vorschau-Leiste (nur Prototyp, Z. 363–384)
Glas-Leiste oben: „SewerStudio Nova · optimierte Vorschau", Hinweis „Beispieldaten. Speichern wirkt nur in dieser Vorschau (Browserspeicher), nie auf eine Projektdatei." (ausgeblendet unter 1500px Breite, Z. 70), Segmente „Stimmung" (Hell · Glas / Dunkel · Cockpit / System), „Bewegung" (bewegt / ruhig), „Fenster" (Player / Training Studio), Knopf „Tastenkürzel F1".

### 3.2 Linke Leiste `.rail` (Z. 387–419), 220px, Glas
- Marke: Quadrat 26×26px, Rundung 8px, `conic-gradient(from 210deg, --accent, --ki, --accent)` mit innerem Kreis (inset 6px, Farbe `--bg`), daneben „SewerStudio" fett 15px (Z. 79–81, 388)
- Gruppen (Grossbuchstaben, `--faint`) und Einträge mit Textsymbol (Z. 389–407):
  - **Projekt**: ● Übersicht (aktiv beim Start), ▣ Projekt, ≡ Haltungen, ◯ Schächte
  - **Daten**: ↓ Import, ↑ Export, ⚠ Medienkonflikte, ⎙ Druckcenter, ☰ Dossiers
  - **Bewertung**: ▦ Sanierungs-Matrix, ▦ Schacht-Matrix, ◑ Schattenauswertung, ✓ VSA
  - **System**: ⚙ Diagnose, ⚙ Einstellungen
- Aktiver Eintrag: `aria-current="page"`, Hintergrund `--accent-soft`, Rahmen `--line2`, Streifen `inset 3px 0 0 var(--accent)`, Symbol in `--accent-text` (Z. 87–88)
- Unten (`margin-top: auto`, Z. 90) Aufklapper `details.sysinfo`: Kopf mit grünem Punkt `#sysDot` (`.dot`, 9px, `--ok`) + „Analyse bereit" + ▾ (Z. 409). Inhalt (Z. 411–415): „Sidecar erreichbar · DINO · SAM 2.1 · Bildmodell qwen3-vl 8B. Detektor YOLO gesperrt (nicht qualifiziert)."; Messzeilen Raster `36px 1fr auto`: GPU 62 % (Balken 62 %), VRAM „21.3 / 29 GB" (Balken 73 %, rote Grenzmarke `.mlimit` 2×12px in `--z0` bei 90.6 %, Titel „Grenze 29 GB"), CPU 34 %; „Beispielwerte. Im Programm: nvidia-smi und Sidecar-Health."
- Fuss: „Göschenen 2026 · gespeichert 14:32" (`--faint`, 12px, Z. 418)

### 3.3 Menüleiste (Z. 422–426)
`role="menubar"`, Hintergrund `--surface2`, 12px: „Datei" (Demo: Neues Projekt, Projekt öffnen, Speichern, Speichern unter, Beenden), „Werkzeuge" (Code-Katalog, Preiskatalog, Messvorlagen, KI starten, Training Center, Training Studio), „Ansicht" (Fokusmodus, System-Monitor). Alle drei nur `data-demo` (Toast).

### 3.4 Kopfzeile `.topbar` (Z. 427–438)
- Brotkrume: „Göschenen 2026 /" in `--faint` + Seitentitel `#crumbPage` (fett 14px Display-Schrift), Titel aus `TITLES` Z. 1373
- Globale Suche `#gsearch`: Pille min 300px, Symbol ⌕, Platzhalter „Haltung, Schacht oder Strasse suchen", Tastenhinweis `kbd` „Ctrl K", Ergebnisliste `#gresults` (`role="listbox"`, max. Höhe 320px, Schatten, Z. 114)
- Chip `#taskChip` (`.task`: Pille, `--ki-soft`, 12px 600, Titel „Nächste fachliche Aufgabe"): Starttext „Nächste Aufgabe wird geladen", danach „Nächste Aufgabe: <Haltungsname> prüfen" oder „Keine offene Prüfung" (Z. 1650). Klick = Klick auf „Nächste Haltung prüfen" (Z. 1444).
- Avatar: Kreis 28px in `--accent`, Text „PA" 12px 700, Titel „Pascal Aschwanden", aria „Angemeldet: Pascal Aschwanden"

### 3.5 Tastenkürzel (Handler Z. 1339–1357, Hilfsfenster Z. 1043–1052)
Liste im Fenster „Tastenkürzel":
| Taste | Text im Fenster |
|---|---|
| Ctrl K | Globale Suche |
| F3 | Suche Haltung (auf der Seite Haltungen) |
| Esc | Oberstes Fenster schliessen, Suchliste schliessen |
| Tab / Enter | Navigation und alle Schaltflächen |
| ↑ ↓ Enter | In der Haltungsliste Zeile wählen |
| Leertaste | Player: Play / Pause |
| ← → | Player: 5 Sekunden springen |
| + − | Player: Geschwindigkeit |
| S · D · M | Player: Stop · Live-KI · Markieren |
| F1 | Diese Übersicht |

Verhaltensregeln:
- **Esc** (Z. 1340–1345): Reihenfolge 1. offene Suchliste schliessen, 2. offene „Weitere ▾"-Menüs schliessen, 3. oberstes Fenster über `requestClose` (beim Player mit Entwurf: Rückfrage).
- **Tab** in Dialogen (Z. 1346–1352): Fokus bleibt im obersten Dialog (Umlauf zwischen erstem und letztem sichtbaren Element).
- **F1** (Z. 1353): öffnet immer `winHelp` (auch über Player/Codierfenster, `stack2` z-index 60).
- **Ctrl K / Cmd K** (Z. 1354): bei offenem Dialog Toast „Suche nicht verfügbar" / „Zuerst das offene Fenster schliessen. Der Fokus bleibt im Fenster." (warn), sonst Fokus + Auswahl im Suchfeld.
- **F3** (Z. 1355): nur auf Seite Haltungen und ohne Dialog → Fokus `#searchH`.
- Player-Tasten (Z. 1356, 1696–1703) nur, wenn oberster Dialog der Player ist und der Fokus nicht in Eingabefeld/Select/Textarea liegt.
- Pfeiltasten ↑/↓ und Enter in Tabellenzeilen (Z. 1513); Enter in Suchliste wählt markierten oder ersten Treffer (Z. 1621).

### 3.6 Fenster (Dialoge)
- Alle Fenster: `.win` fixiert, z-index 50, Abdunkelung; `.stack2` (Codierung, Rückfrage, Tastenkürzel) z-index 60 (Z. 299–301). `role="dialog"`/`"alertdialog"`, `aria-modal="true"`.
- `openDialog` (Z. 1297–1304): merkt Auslöser, fokussiert erstes bedienbare Element im `.winbody`, pausiert Engine. `closeDialog` (Z. 1305–1318): Fokus zurück zum Auslöser; ist dieser nicht mehr sichtbar → gewählte Tabellenzeile der sichtbaren Seite, sonst aktiver Navigationseintrag. Player-Schliessen pausiert die Wiedergabe; ein sauberer Entwurf wird verworfen.
- **Player öffnen** (`openWinByName` Z. 1365–1370, `openPlayer` Z. 1631–1637): Vorschau-Leiste „Player" (Z. 380), Werkzeugleiste „▶ Video prüfen" `#btnPlayerH` (Z. 531) → Player der gewählten Haltung; ▶-Knopf in Tabellenspalte „Video" (Z. 1498, 1515); „Im Player prüfen" im Übersichtspanel (Z. 1530); „Prüfen" in KI-Vorabdurchlauf (Z. 1433–1435); „▶ Nächste Haltung prüfen"/Aufgaben-Chip (Z. 1440–1444); ▶ in Dossier-Leitungstabelle (Z. 1822, 1825). Alle Wege laufen über `select('h', id, false, then)` — bei ungespeicherten Änderungen erst Rückfrage. Ohne Video: Toast „Kein Video" / „Für <Name> ist kein Video verknüpft. Medienkonflikte prüfen." (warn) und kein Fenster.
- **Training Studio öffnen**: Vorschau-Leiste „Training Studio" (Z. 381); Menü Werkzeuge nur Demo. `renderStudio()` dann `openDialog('winStudio')`.
- **Codierfenster** `winVsa`: aus Player („Neues Ereignis erfassen", Eingabemarker-Chips, „Bearbeiten" eines offenen Befunds) oder Studio („Codieren… (Katalog)", `#stCatalog`).
- **Rückfrage** `winConfirm` (Z. 1030–1037): `role="alertdialog"`, Titel `#cfTitle`, Text `#cfText`, Knöpfe `#cfSave` (primary) / `#cfDiscard` / `#cfCancel` (ghost) mit variablen Texten, Hinweisfeld `#cfNote` (rot bei Speicherfehler).
- **Tastenkürzel** `winHelp` (Z. 1040–1054): Raster `auto 1fr`, Knopf „Schliessen".

---

## 4. Seiten

Allgemein: `.page` ist unsichtbar, `.page.show` sichtbar, `.page.fill` füllt die Höhe (Z. 122–124). `showPage` (Z. 1374–1390) setzt Brotkrume, scrollt nach oben und rendert seitenabhängig. Alle Knöpfe mit `data-demo` zeigen nur den Toast „Nur Vorschau" / „Im Programm: <Beschreibung>. Hier passiert nichts." (warn, Z. 1361) und tragen das Suffix ◌ (Z. 141). Prüftabelle J3: 181 solche Aktionen.

### 4.1 page-overview „Übersicht" (Z. 443–487, JS Z. 1407–1438)
Von oben nach unten:
1. **Hero** (Raster 1.4fr 1fr):
   - Karte links: `<h1>` „Göschenen 2026" (22px), Text `#ovText` (Z. 1409): „`<gepr>` von `<n>` Haltungen fachlich geprüft (`<x,x %>`). `<anal>` von der KI analysiert und noch nicht geprüft, `<off>` ohne Analyse. `<dring>` Haltungen dringend (Z0 oder Z1). Beispielbestand mit `<n>` Haltungen und `<ns>` Schächten." Mit Beispieldaten: „8 von 14 Haltungen fachlich geprüft (57,1 %). 4 von der KI analysiert und noch nicht geprüft, 2 ohne Analyse. 3 Haltungen dringend (Z0 oder Z1). Beispielbestand mit 14 Haltungen und 8 Schächten." Knöpfe: „▶ Nächste Haltung prüfen" (primary, `#btnNext`), „Haltungen öffnen" (`data-nav-go="haltungen"`), „Projekt öffnen" (ghost, Demo).
   - Karte rechts: Kopf „KI-Vorabdurchlauf" Untertitel „heute"; Liste `#kiRuns`: je Haltung mit `ki`-Einträgen eine Zeile mit Badge `.ki` (Texte vor dem Komma, „ · "-getrennt, z. B. „Bogen · Rohrende"), Meta „<Name> · <Orte>" (z. B. „78998-79002 · Meter 9,42, Sekunde 214"), Knopf „Prüfen" (klein, ghost). Leerzustand „Keine Vorabdurchläufe heute".
2. **KPIs** (4 Karten, Raster `repeat(4,1fr)`, Z. 459–464; Wert 28px 600, Label 12px Grossbuchstaben):
   - „Haltungen": `14` + klein „geprüft 8"; Trend „Gesamtlänge 601 m · KI analysiert 4 · offen 2"
   - „Schächte": `8` + „mit Protokoll 7"; Trend „1 ohne Originalprotokoll · Zustandsklasse immer von Hand"
   - „Dringend (Z0/Z1)": `3` in Farbe `--z0` + „Haltungen"; Trend „Z0 1 + Z1 2 · dazu 2 Schächte"
   - „Sanierungskosten": `243'900` + „CHF"; Trend „Haltungen 214'400 · Schächte 29'500 · ohne MWST"
3. **grid2** (1.2fr 1fr):
   - „Zustand Haltungen" Untertitel „13 mit Klasse · 1 nicht berechnet". **Donut** (Z. 1418–1421): SVG 140×140, viewBox 120; Grundkreis r 46, Strich `--line2` Breite 14; Segmente in Reihenfolge Z4, Z3, Z2, Z1, Z0, ab 12 Uhr im Uhrzeigersinn (`rotate(-90 60 60)`), Strich `var(--z<n>)` Breite 14, Länge = Umfang(2π·46) × Anzahl/Gesamt, ohne Lücke; Mitte: Zahl „13" (18px 700, `--text`) und „mit Klasse" (11px, `--muted`). **Legende** (Z. 1422): Knöpfe mit Swatch 11×11 (Rundung 3) in `--z<n>`, Text `ZK_LABEL` (Z. 1219: „Z0 · sofort", „Z1 · kurzfristig", „Z2 · mittelfristig", „Z3 · langfristig", „Z4 · kein Handlungsbedarf") und Anzahl rechts (Mono); zusätzlich „nicht berechnet" mit gestricheltem Swatch. Klick → Filter `zkFilter`, Seite Haltungen, Toast „Filter gesetzt" / „Liste zeigt nur <Klasse>. Suche leeren hebt den Filter auf."
   - „Häufigste Schäden" Untertitel „VSA-KEK Hauptcode, aus den Befunden gezählt": **Balken** `.bar` (Raster 64px 1fr 84px, Z. 239): links Hauptcode (Mono 12 600), Balken 10px in `--accent` relativ zum Maximum, rechts „<n> <Name>" mit Namen Z. 1425 (BAB Riss, BBC Ablagerung, BAF Oberfläche, BAJ Versatz, BBA Wurzeln, BCA Anschluss, BAC Bruch, BBB Inkrustation, BCC Bogen, BAA Verformung), absteigend sortiert. Leer: „Keine Befunde".
4. **grid3**:
   - „Projekte" Untertitel „zuletzt geöffnet": Zeilen „Göschenen 2026" (ausgewählt, Meta „14 Haltungen · 8 Schächte", rechts „heute"), „Seilergasse Altdorf" („15 Haltungen · 12 Schächte", „4. Sept."), „Jagdmatt" („48 Haltungen · 39 Schächte", „21. Aug.")
   - „Sanierungsverfahren" Untertitel „Kosten je Verfahren": Balken in `--ki`, rechts CHF (Verfahren mit Kosten: Inliner 133'610, Erneuerung 71'440, Robot 9'350). Leer: „Keine Kosten erfasst".
   - „Stammdaten" Untertitel „Vollständigkeit der Beispieldaten": Balken „Material", „DN", „Baujahr", „GEONIS" mit „k/n"; Farbe `--z4` ab 95 %, `--z3` ab 70 %, sonst `--z2` (Z. 1430).

### 4.2 page-projekt „Projekt" (Z. 490–523)
- Seitenkopf: „Projekt" klein „Stammdaten des offenen Projekts"; rechts „Speichern unter", „Projekt speichern" (primary), „Programm schliessen" (ghost) — alle Demo.
- Raster 2fr 1fr. Links Karte „Projektdaten", 2-spaltiges Formular: Name „Göschenen 2026", Auftrag Nr. „2026-0412", Beschreibung (volle Breite, Textarea) „Zustandsaufnahme Gemeindenetz Göschenen, Etappe 2026", Auftraggeber „Gemeinde Göschenen", Gemeinde „Göschenen", Zone „Dorf / Gotthardstrasse", Strasse „diverse", Bearbeiter „Pascal Aschwanden", Firma „Aschwanden Kanalinspektion", Inspektionsdatum „06.10.2025 – 21.11.2025", Datenherr „Gemeinde Göschenen", Datenlieferant „Aschwanden Kanalinspektion".
- Rechts Karte „Projekt-Infos" (`.setrow` Zeilen): Projektdatei „D:\Projekte\Goeschenen_2026\projekt.json" + „Ordner"; Projektformat „Version 2 · zuletzt gespeichert 14:32" + Badge ok „aktuell"; Letzter Import „WinCan-GEP · 04.09.2026 · 239 Haltungen" + „Bericht" (springt zu Import); Wiederherstellungspunkte „3 vorhanden · neuester heute 13:50" + Badge acc „an".
- Karte „Neues Projekt anlegen" Untertitel „nur ohne offenes Projekt": Hinweis „Stammdaten links eintragen, danach anlegen. Bis dahin wird nichts geschrieben."; „Projekt anlegen" (primary, deaktiviert, Titel „Nur ohne offenes Projekt"), „Abbrechen" (ghost, deaktiviert).

### 4.3 page-haltungen „Haltungen" (Z. 526–578, `.fill`)
Von oben nach unten:
1. **Werkzeugleiste** (Z. 527–554): „Speichern" (primary, `#btnSaveH`, nur bei Änderungen aktiv), „Verwerfen" (`#btnDiscardH`), Trenner, „▶ Video prüfen" (`#btnPlayerH`), „Neu" (Demo: „legt einen neuen leeren Haltungsdatensatz an"), „Löschen" (Demo), Trenner, **„Weitere Aktionen ▾"** (Menü, Z. 535–551, min 260px, Gruppenköpfe in Grossbuchstaben):
   - DATEN: Medien suchen, Leere Felder aus QGIS, Katasterkennungen ergänzen, Strassen
   - FACHLICH: Hydraulik, Sanierungsmassnahme, Vorschlag für diese Haltung, Dossier, KI-Videoanalyse
   - ANSICHT: Ansicht anpassen, Abdocken, Fokusmodus (Demo „Fokusmodus F11")
   - rechts Suche `#searchH` (min 220px, Platzhalter „Suche Haltung", `kbd` F3)
2. **Spaltenansichten** `#viewsH` (Z. 555, JS Z. 1517–1521): Hinweis „Ansicht", Chips `.vchip` mit Spaltenzahl klein (Mono 11px): „Kompakt 10", „Stammdaten 14", „Bewertung 11", „Sanierung 11", „Kosten 8", „Alle Spalten 36", dazu Knopf „Spalten anordnen" (ghost small, Demo). Gewählte Ansicht in `nova.opt.viewH`.
3. **Arbeitsfläche** `#workH` (Höhe `calc(100% - 84px)`, Raster Zeilen `minmax(0,1fr) 6px auto`, Z. 252, 556):
   - **Oben** `#upperH` (Spalten `minmax(0,1fr) 6px var(--sidew,320px)`): links Karte mit Tabelle `#tblH` (`.tablewrap` scrollt, sticky Kopf); **vertikaler Splitter** `#splitVH` (`role="separator"`, aria „Breite der Übersicht", Titel „Ziehen oder Pfeiltasten"); rechts Panel `#sideH` Kopf „Übersicht" + Haltungsname (siehe Abschnitt 5).
   - **Horizontaler Splitter** `#splitHH` (aria „Höhe der Eingabefelder").
   - **Eingabefelder-Schublade** `#drawerH` (Z. 563–576): Kopfzeile `.dhd`: Knopf „▾ Eingabefelder" (`#tglH`, klappt zu/auf; Pfeil dreht −90°), Name der Haltung (Mono, `--muted`), Badge „● Ungespeichert" (`#dirtyH`, Rahmen `--warn`, versteckt bis Änderung), Feldsuche `#fsH` Platzhalter „Feld suchen, z. B. Baujahr", Hinweis `#fnoteH` („<n> Felder passen", `aria-live`), „Alle auf", „Alle zu" (ghost small), Symbolknopf ⤢ `#tallH` „Eingabefelder gross anzeigen". Inhalt `#secsH`: Raster 4 Spalten (Z. 282), in „gross" 2 Spalten mit je 2-spaltigen Feldern (Z. 292–293). Vier `details.sec`-Themen mit Kopf „▸ <Titel>" + Feldanzahl klein: **Stammdaten 14, Bewertung 9, Sanierung 10, Kosten und Bemerkungen 3** (Feldliste Abschnitt 9). Offen-Zustand je Thema in `nova.opt.secsh`; Schublade offen/zu in `nova.opt.drawerOpenh`. Zugeklappt: nur Kopfzeile, Höhe auto (Z. 274–275).
- **Tabelle** (`renderTable` Z. 1505–1516): Spalten aus `VIEWS_H` (Z. 1242–1249), Kopfbeschriftungen aus Feldlabels bzw. `COL_LABEL` (Z. 1257: ampel → „KI", pruefung → „Prüfung", video → „Video", pdf → „Protokoll"). Zahlenspalten (`NUM_COLS` Z. 1504: dn, breite, laenge, baujahr, noteD, noteS, noteB, inliner, verpressen, manschette, lem, kurzliner, neubau, kosten, mass1, mass2, tiefe) rechtsbündig Mono. Erste Spalte fett + Änderungsmarke „●" in `--warn` (Z. 177, 1509). Zeilen `tabindex=0`, `aria-selected`, Auswahl mit Klick/Enter, Pfeile ↑/↓ wechseln Fokus (Z. 1511–1514). Leerzustand: „Keine Haltungen für diese Suche" / „Suchtext oder Filter anpassen." (Z. 1510).
- **Zellregeln** (`cell` Z. 1494–1503):
  - `zk`: Chip `.zk.z<n>` „Z<n>" (34×22px, Mono 12 700, Hintergrund `--z<n>`, Text `--z-ink`, bei Z0 `--z0-ink`); `null` → „–" gestrichelt mit Titel „nicht berechnet".
  - `ampel` (Spalte „KI"): Punkt 9px + Text: `pruefung=offen` → grauer Punkt „keine Analyse"; offene Befunde → gelb (`--warn`) „<n> offen"; sonst rot (`--bad`) wenn zk ≤ 1, grün (`--ok`) „geprüft".
  - `pruefung`: Badge ok „fachlich geprüft" / Badge ki „KI analysiert, Prüfung offen" / neutral „nicht analysiert" (`PRUEF_LABEL` Z. 1220).
  - `video`: Symbolknopf „▶" (aria „Video <Name> abspielen") oder „–" (Titel „kein Video").
  - `pdf` (Spalte „Protokoll"): Knopf „PDF" (bei Haltungen immer; Demo) oder „–".
  - `laenge`, `tiefe`: 2 Nachkommastellen; `kosten`: Tausender mit Apostroph; leer → „–".
- **Kompakt-Spalten** (10): Haltungsname (ID), Strasse, Rohrmaterial, Lichte Höhe / DN mm, Haltungslänge m, Zustandsklasse, KI, Prüfung, Video, Protokoll.
- **Stammdaten** (14): name, strasse, material, dn, breite, profil, nutzung, laenge, richtung, datum, baujahr, eigentuemer, geonis, lisag.
- **Bewertung** (11): name, zk, noteD, noteS, noteB, geschaetzt, resultat, referenz, gewaesser, gw, pruefung.
- **Sanierung** (11): name, sanieren, massnahme, inliner, verpressen, manschette, lem, kurzliner, neubau, ausgef, status.
- **Kosten** (8): name, strasse, zk, massnahme, kosten, ausgef, status, eigentuemer.
- **Alle Spalten** (36): alle Feldschlüssel der vier Themen in Reihenfolge.
- Prüftabelle: sichtbare Zeilen 7 (1366×768), 9 (1440×900), 12 (1920×1080); Panelbreite im Player 338,24 px.

### 4.4 page-schaechte „Schächte" (Z. 581–634, `.fill`)
Gleicher Aufbau wie Haltungen mit eigenem Zustand (`ENT.s`, unabhängig, Prüftabelle C1–C5):
- Werkzeugleiste: „Speichern" (`#btnSaveS`), „Verwerfen" (`#btnDiscardS`), Trenner, „Protokoll importieren", „Neu", „Löschen", Trenner, „Weitere Aktionen ▾": DATEN: PDF-Daten, Aktualisieren, Leere Felder aus QGIS, Katasterkennungen ergänzen, Feldnamen aufräumen, Strassen; REIHENFOLGE: Hoch, Runter; FACHLICH: Sanierungsmassnahmen, Protokoll (PDF), Gehe zu Ordner; ANSICHT: Ansicht anpassen. Suche `#searchS` „Suche Schacht" (ohne F3).
- Spaltenansichten (Z. 1250–1256, 1271): „Kompakt 9" (name, strasse, funktion, material, mass1, mass2, form, zk, pdf), „Zustand und Inspektion 9" (name, zk, resultat, dichtheit, referenz, gewaesser, gw, belastung, datum), „Sanierung und Kosten 7" (name, sanieren, massnahme, ausgef, status, kosten, eigentuemer), „Dokumente und Medien 5" (name, pdf, pdfEigen, link, fotos), „Alle Spalten 30".
- Panel rechts `#sideS` Kopf „Schachtansicht" + Nummer (Inhalt Abschnitt 5.2). Schublade `#drawerS` „Eingabefelder", Feldsuche Platzhalter „Feld suchen", Themen **Stammdaten 10, Zustand und Inspektion 9, Sanierung und Kosten 7, Dokumente und Medien 4** (Abschnitt 9).
- Leerzustand Tabelle: „Keine Schächte für diese Suche".

### 4.5 page-import „Import" (Z. 637–685)
- Kopf: „Import" klein „Kanalfernseh-Projekte, Protokolle, Medien"; Checkbox „Erst Vorschau anzeigen" (an); „Katalog neu laden" (ghost, Demo).
- Raster 2fr 1fr. Links:
  - Karte „Import Kanalfernseh-Projekt" Untertitel „ein Knopf für Archiv, Plan-PDF, Medien, Protokolle, Kanal und Dichtheit": Ablagefläche `.drop` (gestrichelt) „**Projektordner wählen**" + „XTF/M150/MDB, WinCan, IBAK, KINS und SchachtPro werden erkannt. Kundenoriginale werden nur gelesen, Kopien landen unter Imports." + Knopf „Ordner wählen und importieren" (primary, Demo).
  - Karte „Manuell" Untertitel „weitere Quellen": Knöpfe „Import PDF", „Protokolle verteilen", „Protokoll neu generieren", „XTF / M150 / MDB", „WinCan-Projekte", „IBAK-Projekte", „KINS-Projekte", „SchachtPro-Archiv (.spro)", „Projekt portabel machen", „Fotos zuordnen" (alle Demo).
  - Karte „Letzter Lauf" Untertitel „Beispielbericht · 04.09.2026 · WinCan-GEP Göschenen": Reiter „Zusammenfassung" / „Details". Zusammenfassung: 4 KPIs „Haltungen 239" (Trend „gefunden 239 · neu 239"), „Protokolle verteilt 239" („Sammel-PDF 1003 Seiten"), „Videos 241" („59 gegen Fliessrichtung"), „Fehler 0" (in `--ok-text`, „Unsicher 3"). Details: Log (Mono, `.ok` ✓ / `.wn` ! / `.er` ✕): „✓ 261 Schachtprotokolle erkannt und vom Haltungs-Split ausgeschlossen", „✓ Staging unter .import-staging/8f2c… veröffentlicht, Marker erneuert", „! 3 Untersuchungen mit WinCan-Platzhalterdatum 2007-12-31 übersprungen, siehe Bericht", „✕ Video 80399-80397.mp4 im Quellordner nicht gefunden, Haltung ohne Video übernommen", „✓ Projekt atomar gespeichert · Commit-TxId 8f2c…".
- Rechts: Karte „Import-Report": Zeilen „Letzter Bericht" „04.09.2026 14:02" + „Öffnen"; „Berichte" „__IMPORT_REPORTS · 7 Dateien" + „Ordner". Karte „Erkannte Quellen im Ordner" als Baum (Mono): `D:\Kunden\Goeschenen_2026\` ├─ Misc\Docu\Goeschenen.pdf **WinCan Sammel-PDF** ├─ DB\WinCan.mdb **WinCan-Datenbank** ├─ Video\ (241 Dateien) **Haltungsvideos** ├─ Schaechte\ (261 PDF) **Schachtprotokolle** ├─ Plan\Uebersicht.pdf **Werkleitungsplan** └─ Export\Goeschenen.xtf **SIA405 2020**. Hinweis (ok): „Originale werden nie verändert. Verknüpfungen und Netzlaufwerke werden abgewiesen. Ein Abbruch nimmt nur die eigenen neuen Dateien zurück."

### 4.6 page-export „Export" (Z. 688–719, JS Z. 1880–1886)
- Kopf „Export" klein „Excel, Verteilung, Kataster".
- Drei Karten (`.cols3`):
  1. „Excel-Export": Zeile „Zielordner" „D:\Projekte\Goeschenen_2026\Export · gilt für beide Dateien" + „Wählen"; Hinweis „Feste Dateien: Haltungen.xlsx · Schächte.xlsx. Die Vorlage bleibt bytegleich."; Knöpfe „Export Haltungen.xlsx", „Export Schächte.xlsx" (primary, `data-export`).
  2. „XTF an den Kataster": Zeile (ausgewählt) „Bestehende Katasterdaten aktualisieren" + Badge acc „empfohlen" + „Änderungen prüfen und schreiben" (small primary); Hinweis „Original: Goeschenen.xtf, Importkopie vom 04.09.2026. Ergebnis ist eine **revidierte XTF-Datei** für den Import in GEONIS. Ein automatischer Rückabgleich mit GEONIS existiert nicht; Konflikte mit neueren GEONIS-Ständen zeigt SewerStudio nicht."; Zeile „Neue eigenständige XTF erstellen" + „Neue XTF erstellen".
  3. „Haltungen und Schächte in Ordner verteilen": Checkboxen „Haltungen – Normal" (an), „Haltungen – Sanierung", „Schächte – Normal" (an), „Schächte – Sanierung"; Knöpfe „Verteilen" (primary), „Dichtheitsprüfung verteilen", „Abgleichen" (ghost).
- Karte „Ergebnis" `#exportResult`: Leerzustand „Noch kein Export in dieser Sitzung" / „Ziel und Ergebnis erscheinen hier nach jedem Lauf." Nach Klick (Z. 1883–1885): Hinweis warn „**Nur Vorschau: es wurde keine Datei geschrieben.**", Fakten „Vorgang" / „Ziel im Programm", Erklärtext; Toast „Export nur als Vorschau". Texte je Vorgang:
  - xlsx-h: „Haltungen.xlsx", `D:\Projekte\Goeschenen_2026\Export\Haltungen.xlsx`, „14 Datenzeilen ab Zeile 27, Vorlage Export_Vorlage\Haltungen.xlsx bleibt unverändert"
  - xlsx-s: „Schächte.xlsx", `…\Export\Schächte.xlsx`, „8 Datenzeilen"
  - xtf-rev: „Revidierte XTF", `…\Export\XTF_20260906-1432\Goeschenen_revidiert.xtf`, „Aktualisiert Kanal, Haltung und Normschacht an den Original-TIDs. Vorher Prüfbericht mit Bestätigung. Das ist eine Datei für den GEONIS-Import, kein Rückabgleich: Änderungen, die GEONIS seit dem Export gemacht hat, werden nicht erkannt."
  - xtf-neu: „Neue XTF", `…\Goeschenen_neu.xtf`, „Eigene stabile chSST-Kennungen. Im gefüllten Kataster können Duplikate entstehen; Warnung im Bericht."
  - verteilen: „Verteilung", `E:\Verteilung\Göschenen\2026\<Haltung>\`, „14 Haltungsordner und 8 Schachtordner geplant. Nur Projektkopien, nie Kundenoriginale."
- Karte „Zielordner & Verzeichnisbaum" Untertitel „Verzeichnisbaum für die Verteilung", Raster 1fr 2fr: Zeile „Ziel-Wurzel" „Basis-Ordner. Leer = beim Verteilen per Dialog fragen." + „Wählen"; Reiter „Normal" / „Sanierung" / „Erweitert (Ordner-Bausteine)"; Formular „1. Ordner (optional)" `{Gemeinde}`, „2. Unterordner (optional)" `{Jahr}`, „Objektordner (fest)" `{Haltung}` (nur lesbar), „Dateiname" `{Datum}_{Haltung}` (nur lesbar). Rechts Hinweis „Vorschau aus den Feldern links" + Baum (Z. 1880): `E:\Verteilung\` └─ Göschenen\ └─ 2026\ └─ 78998-79002\ ├─ 20251006_78998-79002.pdf ├─ 20251006_78998-79002.mp4 └─ 20251006_78998-79002.txt; live bei Eingabe.

### 4.7 page-medien „Medienkonflikte" (Z. 722–732, JS Z. 1783–1798)
- Kopf „Medienkonflikte" klein „Videos, die keiner Haltung sicher zugeordnet sind"; Filter-Chips `#mkFilter` mit Zähler: „Offen 5", „Fehlend 2", „Mehrdeutig 3", „Gelernt 1", „Alle 6" (Standard „Offen"); „Aktualisieren"; „Weitere Aktionen ▾": Auto-Resolve (gelernt), Mappings löschen.
- Raster 2fr 1fr. Links Karte „Fälle" Untertitel „<n> von 6", Tabelle `#tblMK` Spalten: Konflikt (Badge: Fehlend = bad, Gelernt = ok, sonst warn), Datum, Haltung (fett), Film aus PDF, Kandidaten (Zahl), Status. Leerzustand `#mkEmpty` „Keine Fälle in diesem Filter" / „Anderen Filter wählen oder neu scannen."
- Rechts Karte „Konfliktdetails" + Haltung: Fakten Konflikttyp, PDF-Protokoll (Filmname mit .pdf), Gelernte Quelle, Haltungsordner „Haltungen_Verteilt\<Haltung>"; Abschnitt „KANDIDATEN" mit Zeilen (erste ausgewählt): Name, Meta (Grösse/Datum), Knöpfe „Abspielen", „Übernehmen" (erste primary); leer „Kein Kandidat gefunden" / „Video manuell wählen."; darunter „Video manuell wählen" + „Weitere ▾" (PDF öffnen, Kandidat im Explorer, Gelernte Quelle im Explorer, Info-Datei, Haltungsordner, Gelernte Quelle übernehmen).
- Daten Z. 1203–1210: k1 Mehrdeutig 77457-77453 (3 Kandidaten), k2 Fehlend 80399-80397, k3 Mehrdeutig 75390-75388 (2), k4 Gelernt 81118-81115 (Status „Vorschlag"), k5 Mehrdeutig 82007-82005 (2), k6 Fehlend 82005-82001.

### 4.8 page-druck „Druckcenter" (Z. 735–769, JS Z. 1801–1811)
- Kopf „Druckcenter" klein „Listen, Statistik und NPK-Leistungsverzeichnis"; Segment „Haltungen" / „Schächte"; Select „Ansicht: Standard" / „Eigentümer-Liste" / „Nur mit Kosten"; „Weitere Aktionen ▾": Ansicht speichern, Ansicht löschen, Aktualisieren, Spalten….
- Links: Filterkarte: „Filter", Suche `#druckSearch`, Select „Eigentümer: alle" / „Gemeinde Göschenen" / „Privat", „Ausgeführt durch: alle", „Sanieren: alle", Checkboxen „Nur mit Kosten" (an) und „Nur mit Massnahmen", „Filter zurücksetzen". Tabelle `#tblDruck`: Im Ausdruck (Checkbox an), Haltung, Strasse, Zustand (Z-Chip), Massnahmen (Vorschau), Kostenquelle („Matrix" oder „–"), Netto CHF; Fuss „Netto total · <n> Bauteile · ohne Kosten: <m>" + Summe. Leer: „Keine Haltungen für diese Filter".
- Rechts: Karte „Was soll ins PDF?" Checkboxen: Datenübersicht (eine Zeile je Bauteil) (an), Detailliste je Bauteil (an), Eigentümer-Zusammenfassung, Kosten je Massnahme (an), Positions-Zusammenfassung, Spezialstatistik (Inliner GFK, Nadelfilz, Manschetten, LEM), Volles Dossier (diese Haltung); Knöpfe „PDF öffnen" (primary), „PDF ausdrucken", „PDF exportieren". Karte „Spezialstatistik" (Z. 1808): Balken „Inliner" (m, Breite Summe/2 %), „LEM" (Stk., ×10, `--ki`), „Mansch." (Stk., ×10, `--ki`); Hinweis „Sanierungsquote: <q> von 14 Haltungen (<x %>)". Karte „NPK-Leistungsverzeichnis": „NPK-Offerte (PDF)", „Leistungsverzeichnis (CSV)", „Leistungsverzeichnis (Excel)".

### 4.9 page-dossiers „Dossiers" (Z. 772–780, JS Z. 1814–1828)
- Kopf „Eigentümerdossiers" klein „eine Liegenschaft, ihre Leitungen und Schächte"; „Speichern" (primary, Demo „schreibt dossiers.json"); „Weitere Aktionen ▾": Gebietsangaben, Word-Vorlage, Aktualisieren, Aus Projekt erzeugen.
- Raster 1fr 2fr. Links Karte „Liegenschaften" Untertitel „Reihenfolge wie in dossiers.json": Knöpfe „+ Neue Liegenschaft" (primary small), „↑ Nach oben", „↓ Nach unten"; Zeilen als Knöpfe: Name, Meta „Parz. <n> · <k> Leitungen · <m> Schächte" (+ Badge bad „Bauteil fehlt", falls Haltung fehlt), rechts Badge „Stand: <stand>" (ok, ausser „offen"). Daten Z. 1211–1217: Seilergasse 12 (Parz. 412, h01+h12, s01, offen), Gotthardstrasse 8 (388, h02, Word erzeugt), Kirchgasse 3 (201, h06+h07, s05, versendet), Bahnhofstrasse 21 (97, h03, s03, offen), Rohrbachweg 5 (530, h08+h09, s06, zurück / unterschrieben).
- Rechts `#dossierDetail`: 4 KPIs „Leitungen" (Trend „Gesamtlänge <m> m"), „Sanierungskosten CHF" („ohne MWST"), „Dringend (Z0/Z1)" (Wert in `--warn-text`, Trend „Zustand: Z1, Z4"), „Häufigste Schäden" (Wert 18px, zwei häufigste Hauptcodes „ · ", Trend „aus den Befunden gezählt"). Karte „Leitungen" + „Leitungen wählen…": Tabelle Leitung, Länge, Zustand, Empfohlene Massnahme, Kosten CHF, Aktionen (▶ Video, „PDF", ⇥ zur Haltung); leer „Keine Leitungen gewählt". Karte „Schächte" + „Schächte wählen…": Schacht, Strasse, Funktion, Zustand, Aktionen („PDF", ⇥); leer „Keine Schächte gewählt". Aktionskarte: „Vorschau" (primary), „Word erzeugen", „Alles zu einem PDF", „Weitere Aktionen ▾": Nachführen, Stammdaten…, Haltungsliste erstellen, Schachtliste erstellen, Beilagen sammeln, Ordner öffnen, Entfernen.

### 4.10 page-matrix „Sanierungs-Matrix" (Z. 783–790, JS Z. 1831–1846)
- Kopf „Sanierungs-Matrix" klein „Massnahmen und Kosten je Haltung"; „Preise / Katalog" (ghost), „Neu laden", „Speichern" (primary).
- Tabelle `#tblMatrix` (nur Haltungen mit `sanieren='Ja'`): Haltung, DN, Länge m, Massnahmen, Menge, VD, Wasser, Fräsen, Dicht., Doku, Total CHF, Hinweis (Badge warn „Länge prüfen" bei Länge ≤ 0, Badge bad „Z0"); Fuss „Total (ohne MWST) · <n> Haltungen" + Summe. Zeilenklick wählt Haltung (`state.ent.h.sel`).
- Karte „Positionen" Untertitel „<Haltung> · Katalog NPK 135/171"; Knöpfe „Als Vorlage speichern", „Verwerfen", „Übernehmen" (primary; Demo „überträgt die Massnahmen in das Feld Empfohlene Sanierungsmassnahme"). Tabelle `#tblPos`: Empf. (Checkbox), Position, EH, Menge, EP, Total. Positionen aus Beispielpreisen (Z. 1837–1842): „171.412 Inliner GFK DN <dn>" m 640; „171.520 Anschluss verpressen" Stk. 1450; „171.430 Kurzliner" Stk. 2900; „171.440 Manschette" Stk. 1450; „411.210 Erneuerung Neubau DN <dn>" m 2250; wenn Positionen: „135.210 Reinigung vor Sanierung" h 4 × 312 und „135.310 TV-Abnahme" h 3 × 260. Fuss „Total (ohne MWST), Beispielpreise". Leer: „Keine Positionen" / „Haltung ohne Sanierungsmassnahme."

### 4.11 page-smatrix „Schacht-Matrix" (Z. 793–796, JS Z. 1847–1851)
- Kopf klein „Massnahmen und Kosten je Schacht"; „Neu laden", „Speichern" (primary).
- Tabelle: Schacht, Funktion, Resultat, Massnahme, Menge, Reinig., VD, Wasser, Doku, Total CHF, Hinweis (Badge bad „Z0"; Badge warn „Zustand offen" bei zk null). Fuss „Total (ohne MWST)".

### 4.12 page-schatten „Schattenauswertung" (Z. 799–807, JS Z. 1854–1868)
- Kopf klein „dein Urteil neben dem der KI, ändert keine Projektdaten"; „Neu laden", „Abbrechen" (ghost, deaktiviert), „Schattenlauf starten (Vorschau-Simulation)" (primary). Fortschrittsbalken `#shadowProg` 8px.
- Tabelle: Haltung, ZK (Ich), ZK (Schatten), Massnahme (Ich), Massnahme (Schatten), Kosten (Ich), Kosten (Schatten), Vergleich (Badge ok „gleich" / warn „abweichend" / bad bei Differenz > 1), Status („aktuell"). Leerzustand: „Noch kein Schattenlauf in dieser Sitzung" / „Start erzeugt eine Vorschau-Simulation aus den Beispieldaten. Es läuft kein KI-Modell."
- Rechts Karte „Begründung der Schatten-KI" + Haltung: Text „Die Schatten-KI kommt auf dieselbe Klasse." oder „Die Schatten-KI weicht ab: weniger dringend/dringender. Grundlage sind <n> Befunde, höchste Stufe <s>." + „Vorgeschlagene Massnahme: <sm>. Kostenannahme <sk> CHF nach Beispielkatalog." + KI-Hinweis „**Vorschau-Simulation** Regel: Klasse aus der höchsten Befundstufe abgeleitet. Kein Modell, keine Projektdatenänderung."
- Regel (Z. 1865): höchste Stufe 5→Z0, 4→Z1, 3→Z2, 2→Z3, sonst Z4; Massnahme Z0/Z1 „Inliner GFK" (Kosten Länge×700), Z2 „Kurzliner" (2900×Befunde), sonst „keine" (0). Nur Haltungen mit Befunden. Toast „Schattenlauf fertig (Simulation)".

### 4.13 page-vsa „VSA" (Z. 810–821, JS Z. 1871–1877)
- Kopf „VSA-Bewertung" klein „Zustandsklasse und Noten nach VSA-KEK 2020"; „VSA-Bewertung starten (Vorschau-Simulation)" (primary).
- Links Karte „Ergebnis" Untertitel „noch kein Lauf in dieser Sitzung" → nach Lauf „Vorschau-Simulation · 14 Haltungen · <Uhrzeit de-CH>"; KPIs „Bewertet" (Trend „<n> ohne bewertbaren Befund"), „Geändert" („Klasse gegenüber vorher"), „Schächte –" („nie berechnet, immer von Hand"); Log: „Noch kein Lauf. Der Beispielbestand wird beim Start nach denselben Regeln wie die Übersicht ausgewertet." → je Haltung „✓ <Name> höchste Stufe <s> → Z<k> (vorher Z<x>)" oder „! <Name> ohne bewertbaren Befund · Klasse nicht berechnet / Z<x> belassen".
- Rechts Karte „Regeln dieses Laufs": „Aktiver Katalog: VSA-KEK 2020 Manifest.", „Ein Strich ist der Status „nicht berechnet", nie Z4.", „Zustandsklasse und Dringlichkeit bleiben getrennte Skalen.", „Sanierungsmassnahmen werden durch die Bewertung nicht verändert.", „Am Schacht wird die Zustandsklasse nie berechnet."
- Der Lauf schreibt `zk` in die Beispieldaten und den Browserspeicher (Z. 1873–1874); Toast „VSA-Bewertung (Simulation)" / „<n> Klassen geändert. Vereinfachte Regel, nicht der VSA-Regelsatz des Programms."

### 4.14 page-diagnose „Diagnose" (Z. 824–841)
- Kopf klein „Protokoll des laufenden Programms"; „Log aktualisieren", „Log-Ordner öffnen" (ghost), „Diagnosepaket erstellen" (primary).
- Links Log (Mono, Zeit + Stufe farbig `INFO` `--ok-text`, `WARN` `--warn-text`, `ERROR` `--bad-text`): 14:32:05 INFO Projekt gespeichert: Goeschenen_2026\projekt.json (atomar, .bak erneuert); 14:31:48 INFO KnowledgeRealtimeMirror: 3 Dateien nach Elements\Brain gespiegelt; 14:30:12 INFO Sidecar /health: ok · YOLO gesperrt (qualified=false) · DINO Swin-B · SAM 2.1; 14:29:55 WARN Detektor nicht qualifiziert: Batch-Video läuft ohne YOLO-Gate, Ergebnis Degraded; 14:28:40 INFO Codiermodus 78998-79002: Bogen-Durchlauf 3 Vorschläge, Rohrende Sekunde 214; 14:26:03 INFO Ollama qwen3-vl:8b-q8 geladen, num_ctx 12288; 14:25:30 ERROR OSD-Meterstand nicht lesbar: 77457-77453 Sekunde 88 (negativ vor Rohranfang); 14:20:11 INFO VSA-Bewertung: 239 Haltungen, 14 Klassen geändert.
- Rechts Karte „Zustand": Badges „Sidecar erreichbar" (ok), „Ollama erreichbar" (ok), „YOLO gesperrt" (warn), „Elements angeschlossen" (ok); Hinweis „Das Diagnosepaket enthält Log, Einstellungen ohne Token, Sidecar-Health und Modellstände. Dateiname: SewerStudio-Diagnose-JJJJMMTT-HHMM.zip".

### 4.15 page-einstellungen „Einstellungen" (Z. 844–896, JS Z. 1889–1893)
- Kopf „Einstellungen"; Suche `#setSearch` (min 300px, Platzhalter „Einstellung suchen, z. B. Fotos"); „Speichern" (primary, Demo).
- Raster `200px 1fr`: links Navigation `#setNav` (Allgemein (aktiv), Dateien und Ordner, Import und Referenzdaten, Video und KI, Datensicherung, Hilfe); rechts Karten `data-set`, nur die der aktiven Gruppe sichtbar:
  - **allg** „Darstellung und Diagnose": Erscheinungsbild „Hell · Glas, Dunkel · Cockpit oder System" mit Segment Hell/Dunkel/System; „Bewegung reduzieren" („Stellt Symbole, Übergänge und die Hintergrund-Engine still. Folgt der Windows-Einstellung.") Schalter `#swMotion`; „Hintergrund-Engine" („Leitungsnetz im Hintergrund, nur Optik") Schalter `#swEngine` an; „Log-Stufe" („Information, Warnung oder Debug") Select Information/Debug.
  - **allg** „Speichern": „Automatisch speichern" („bei jeder Änderung") an; „Wiederherstellungspunkte" („höchstens die drei neuesten Stände") an.
  - **allg** „Haltungsprotokoll (PDF)": „Fotos je Seite" („erlaubt sind 1, 2, 4 oder 6") Segment 1/2 (aktiv)/4/6.
  - **dateien** „Projektdateien, Datenordner und Logs": Projektwurzel „D:\Projekte" + Wählen; Datenordner und Logs „%AppData%\SewerStudio" + Öffnen; Programmbereinigung („Arbeitskopien und Testordner entfernen") + Ausführen; Wiederherstellung („Projekt aus Sicherung zurückholen") + Starten.
  - **import** „Importquellen, Werkzeuge, Referenzdaten": QGIS Haltungen (GeoPackage) „D:\QGIS_V4.2\Layer\haltungen.gpkg" + Wählen; QGIS Schächte (GeoPackage) „…\schaechte.gpkg"; Katasterkennungen GEONIS „Kataster_Kennungen_GEONIS_2024-12.gpkg"; Telefonsuche (search.ch) („für Eigentümer im Dossier") Schalter an; Werkzeuge „pdftotext, ffmpeg, LibreOffice" Badge ok „gefunden".
  - **video** „Video-Player, KI-Laufzeit, Schwellwerte, KI-Wissen": Standardgeschwindigkeit Select 1x/1.5x/2x; KI-Vorschläge im Codiermodus („Bogen, Rohranfang, Rohrende beim Eintritt") Schalter an; Sidecar „http://127.0.0.1:8100 · Token nur an Loopback" Badge „erreichbar"; Bildmodell („GPU-Auto: qwen3-vl 8B ab 24 GB VRAM") Select Auto/qwen3-vl:2b; VRAM-Budget („nie über 29 GB, nie alle Modelle gleichzeitig") „29 GB"; YOLO-Konfidenz („nur bei qualified=true wirksam") „0.25"; Wissenswurzel „C:\KI_BRAIN · Spiegel Elements\Brain" + Öffnen.
  - **sicher** „PC-Ausfall-Schutz": Vollsicherung („Programm, Projekte, Wissen · _Versionen 3 Stände") + „Jetzt sichern" (primary); Programm-Momentaufnahme („ZIP mit CRC- und SHA-256-Prüfung") + Erstellen.
  - **hilfe** „Hilfe": Tastenkürzel („Übersicht aller Tasten") + Anzeigen (öffnet winHelp); Version „SewerStudio 4.5 · Vorschau Nova" Badge „Beispiel".
- Suche (Z. 1890): bei Text werden alle Gruppen nach Textinhalt gefiltert (Karte und einzelne `.setrow`); leer → zurück zur aktiven Gruppe.

---

## 5. Übersicht-Panel Haltung (`renderSide('h')`, Z. 1522–1531)

Panel `#sideH` (Karte, Kopf „Übersicht" + Haltungsname, Inhalt scrollt, Z. 560; Breite `--sidew` 240–560px, Standard 320px).

### 5.1 Rohrring mit Uhrlage (Z. 1526–1527)
- SVG `viewBox 0 0 200 120`, 200×120px, `aria-label="Rohrquerschnitt mit Uhrlagen"`, zentriert (`.pipe`, Z. 272).
- Rohr: Kreis Mittelpunkt (100,60), r 50, Füllung `--bg2`, Strich `--line` Breite 6; zweiter Kreis gleicher Grösse, Strich `--accent` Breite 1.5, Deckkraft .6.
- Uhrzahlen (Consolas 11px, `--muted`): „12" bei (100,9), „3" bei (160,63), „6" bei (100,118), „9" bei (40,63), zentriert.
- Schadensbögen: höchstens die **ersten drei** Befunde (`findings.slice(0,3)`). Bogen i beginnt beim Winkel **−90° + i·70°** (i=0: 12 Uhr, i=1: 70° weiter, i=2: 140° weiter; mathematisch positive Richtung = im Uhrzeigersinn auf dem Bildschirm), Sweep **30°** auf Radius 50, Strich Breite 6, `stroke-linecap: round`. **Die Lage folgt NICHT der erfassten Uhrlage (`uhr`), sondern nur dem Index.** Farbe: `var(--z<4 − stufe>)`, begrenzt auf 0–4; fehlende Stufe zählt als 1 → Stufe 5 = `--z0`, 4 = `--z1`, 3 = `--z2`, 2 = `--z3`, 1/leer = `--z4`. Tooltip `<title>` „<code> <text>".

### 5.2 Fakten (`.facts`, Raster 2 Spalten, Z. 1528)
Beschriftung 12px 600 `--muted`, Wert Mono 13px 600: „Schacht oben" (Name vor „-"), „Schacht unten" (Name nach „-", sonst „–"), „Material", „DN / Profil" („<dn> · <profil ohne 'profil'>", z. B. „300 · Kreis"), „Länge" („42.30 m"), „Inspektion" (Datum), „Prüfung" (`PRUEF_LABEL`), „Video" (Dateiname oder „kein Video").

### 5.3 Schadenliste (`.findings`, Z. 1529)
Je Befund `.find` (Raster `auto 1fr auto`, Hintergrund `--surface2`, Rahmen `--line2`):
- Chip `.code`: Code (Mono 12 700, Hintergrund `--accent-soft`, Text `--text`), z. B. „BAB B"
- Text 13px + Kleinzeile 12px `--muted`: „Stufe <n> · " (wenn Stufe) + „KI-Vorschlag, Konfidenz 0.91 (Modellsicherheit)" bzw. „fachlich erfasst" + „ · <status>" (offen / bestätigt / bearbeitet)
- rechts Meter (Mono 12 `--muted`): „9.42 m" oder Strecke „18.20–24.60 m"
- Leerzustand: „**Keine Befunde erfasst**" / „Video prüfen oder Ereignis im Player erfassen."
Beispiel h01: BAB B „Riss quer, 12 bis 2 Uhr" Stufe 3 KI 0.91 offen 9.42 m; BCA „Seitlicher Anschluss, 8 Uhr" Stufe 1 KI 0.78 bestätigt 14.80 m; BBC A „Ablagerung Sand, 6 Uhr" Stufe 2 KI 0.64 bearbeitet 18.20–24.60 m.

### 5.4 KI-Hinweis (Z. 1530), nur wenn offene Befunde
`.kihint` (Hintergrund `--ki-soft`): „**KI-Vorschläge warten auf fachliche Bestätigung**" / „<n> offen. Die Ampel wird erst grün, wenn zwei unabhängige Belegquellen vorliegen und du bestätigt hast." + Knopf „Im Player prüfen" (small primary).

### 5.5 Aktualisierung
Änderungen in den Feldern name, material, dn, laenge, profil, datum, zk rendern das Panel sofort neu (Z. 1559); Auswahlwechsel ebenfalls (Z. 1462).

### 5.6 Schachtansicht (`renderSide('s')`, Z. 1533–1537)
SVG gleiche Grösse, `aria-label="Schachtgrundriss"`: Ellipse rx 62 ry 50 bei Form Oval/Rechteckig, Rechteck 110×100 (rx 6) bei Quadratisch, sonst Kreis r 50; Füllung `--bg2`, Strich `--line` 6; Masstext „<mass1> × <mass2>" (Consolas 11). Fakten: Funktion, Material, Tiefe („2.35 m"), Baujahr, Belastungsklasse, Inspektion. Schäden: Code, Text, „Stufe <n>", rechts Ort (Sohle/Konus/Schachtwand/Anschluss); leer „Keine Schäden erfasst". Hinweis: „Am Schacht wird die Zustandsklasse nie berechnet. Die Fachperson setzt sie von Hand."

---

## 6. Fenster „Player" (`#winPlayer`, Z. 903–948; JS Z. 1627–1703)

### 6.1 Titelzeile (Z. 904–905)
„▶ Video · <Haltungsname> · <Videodatei>" (z. B. „78998-79002 · 20251006_78998-79002.mp4"), Badge ki „Codier-Modus", Badge dirty `#plDraft` „● Entwurf: <n> Änderungen" (versteckt ohne Änderungen), rechts „Tastenkürzel F1" (ghost small), „Schliessen" (small, `data-close` → `requestClose`).

### 6.2 Aufbau (Z. 308–309)
Raster `minmax(0,1fr) 340px`, Lücke 12px, volle Höhe. Linke Spalte Zeilen `1fr auto auto auto`, Lücke 8px.

### 6.3 Video (Z. 908–913)
Fläche `#0b1220`, Rundung 10, Rahmen `--line`; Frame 4:3 zentriert. OSD oben links `#plOsd` „LZ1: <Name> · <Datum> · mm:ss" (12px Mono `#e8f0ff`); Rohrring `.ringv` (58 % Breite, siehe 1.2); KI-Boxen `#plBoxes` (Z. 1661–1662): nur bei aktivem Live-KI und KI-Befund innerhalb 1,5 m der Position → Rechteck links 44 % oben 18 % Breite 22 % Höhe 16 %, Rahmen 2px `--ki`, Label oben links „<code> · <text> · <conf>" (Hintergrund `--ki`, Text `--ok-ink`, 11px Mono 700); Meterstand unten rechts `#plMeter` „<m> m" (14px Mono 600 `#ffe58a`). Meter = Länge × Position / Dauer (linear, Z. 1652).

### 6.4 Regler/Zeitleiste (Z. 915, 1659–1660)
`#plTimeline` `role="slider"` „Videoposition" 0–100, 10px Pille `--line2`. Marker `.mk` 8px Kreis bei `100·m/laenge %`: `--ki` für KI-Befunde, `.man` `--z1` für manuell erfasste; Tooltip „<code> <m> m". Positionszeiger `.pos` 2×18px `--accent`. Klasse `.heat` (`--z1`, Deckkraft .45, Z. 321) ist definiert, wird aber vom Skript nicht gesetzt („Schnell-Scan" ist Demo). Klick setzt Position; Pfeiltasten ±5 s.

### 6.5 Bedienleiste (Z. 916–932)
„⏮ 5 s" (Titel „5 Sekunden zurück — Pfeil links"), „◂" („Zurück (Step)", 0,04 s), „▶ Play" primary / „❚❚ Pause" („Abspielen / Pause — Leertaste"), „▸" („Weiter (Step)"), „5 s ⏭" („5 Sekunden vor — Pfeil rechts"), „■ Stop" („Stopp — Taste S", setzt Position 0), Trenner, Select Geschwindigkeit 0.5x / 1x (Standard) / 1.5x / 2x / 4x / 8x, Badge `#plTime` „mm:ss / mm:ss · <speed>x" (z. B. „03:12 / 14:05 · 1x"), Trenner, „Live-KI" (Toggle, „Erkennung ein/aus — Taste D"), „Schnell-Scan" (Demo „erzeugt die KI-Heatmap auf der Zeitleiste"), „Markieren" (Toggle, „Bereich markieren — Taste M"; Toast „Markieren aktiv" / „Im Programm: Rechteck im Video ziehen, danach SAM-Maske und Codierfenster."), „Weitere ▾": Werkzeuge, Rohr kalibrieren (DN), Screen, Overlays aus, Lautstärke, Markierform (Demo).
Wiedergabe: alle 1000 ms Position += Geschwindigkeit; Ende pausiert (Z. 1681). Startposition beim ersten Öffnen einer Haltung: min(192 s, Dauer) (Z. 1634); die Position bleibt beim erneuten Öffnen derselben Haltung erhalten.

### 6.6 Eingabemarker (Z. 934–937)
Karte „Eingabemarker" mit 12 Chips (`.vchip`, `data-marker`): Rohranfang (BCD), Rohrende (BCE), Anschluss (BCA), Bogen (BCC), Riss (BAB), Bruch (BAC), Verformung (BAA), Versatz (BAJ), Wurzeln (BBA), Ablagerung (BBC), Inkrustation (BBB), Wasserstand (BDD). Klick setzt Voreinstellung und öffnet das Codierfenster (Z. 1693).

### 6.7 KI-Zeile und Abschluss (Z. 938–944)
„KI-Pipeline ▾" (Aufklapptext: „Sidecar erreichbar · Token ok · YOLO gesperrt (nicht qualifiziert) · DINO Swin-B · SAM 2.1 · Modus Multi-Model. Automatische Analyse alle 5 s."), Checkbox „Automatische KI-Analyse (alle 5 s)" (an), „Aktuellen Frame analysieren" (Demo), rechts „Codierung übernehmen" (`#plApply`, small primary), „Beenden ohne Übernahme" (`#plDiscard`, ghost small).

### 6.8 Seitenpanel `#plSide` (340px, Z. 946, 1665–1669)
Abschnittsköpfe `.sh` (12px 700 Grossbuchstaben `--muted`):
1. **„KI-Vorschläge (Vorabdurchlauf)"**: je `ki`-Eintrag `.find`: Code, Text, Kleinzeile „<ort>" + „ · Konfidenz 0.83 (Modellsicherheit)" (wenn conf) + „ · Abnahme 89 % (Trefferquote der Lernstufe auf Prüfclips)" (wenn abnahme); Knopf „Bestätigen" → Befund manuell/bestätigt ohne Stufe angelegt (Meter aus „Meter x,xx" oder aus „Sekunde n" via meterAt), Vorschlag entfernt (Z. 1675). Leer: Hinweis „Keine Vorschläge für dieses Video."
2. **„KI-Befunde (<n> · <m> offen)"**: je Befund `.find` Code, Text, Kleinzeile „<m> m · Konfidenz x.xx" bzw. „ · fachlich erfasst" + „ · <status>", Symbolknopf „⇥" „Zur Stelle springen" (setzt Position = m/Länge·Dauer). Bei Status offen darunter: „Fachlich bestätigen" (small primary), „Bearbeiten" (öffnet Codierfenster mit Befund), „Ablehnen" (ghost; entfernt Befund). Leer: „Noch keine Befunde. Ereignis erfassen oder Live-KI einschalten."
3. **„Codieren"**: „Neues Ereignis erfassen" (primary, `#plNewEvent`, `aria-haspopup="dialog"`).
4. **„Freigabe für Training"**: Hinweis „Getrennter Schritt: nur fachlich bestätigte Befunde mit Box und Maske. Im Programm über „Grüne als Training"." + Knopf „Grüne als Training" (Demo).
5. **„Session"**: Fakten „bestätigt" (Anzahl), „offen", „DN", „Position" („<m> m").

### 6.9 Entwurf / Übernehmen / Beenden (Z. 1627–1649, 1677)
- Beim Öffnen wird eine Kopie der Haltung (`findings`, `ki`, `pruefung`) als Entwurf angelegt (Z. 1628, 1635). Alle Aktionen im Panel und im Codierfenster wirken nur auf den Entwurf; Toast-Zusatz „<Name> · steht im Player-Entwurf. Erst „Codierung übernehmen" schreibt in den Beispielbestand."
- Änderungszähler (Z. 1629): +1 bei geändertem Prüfstatus, +|Differenz der Vorschlagsanzahl|, bei unterschiedlichen Befunden +max(1, |Differenz der Befundanzahl|).
- Automatik (Z. 1672, 1677): sind alle Befunde nicht mehr offen und Status war „analysiert", wird der Entwurf-Prüfstatus „geprueft".
- **„Codierung übernehmen"** (`playerApply` Z. 1638–1648): schreibt Befunde und Vorschläge in den Bestand; Prüfstatus: Entwurf „geprueft" → „geprueft"; sonst wenn Befunde vorhanden und keiner offen → „geprueft"; sonst Entwurfswert. Speichert in Browserspeicher, rendert Tabelle/Panel/Übersicht, aktualisiert Aufgaben-Chip, schliesst Player. Toast „Codierung übernommen" / „<Name>: <n> Änderungen, <k> Befunde in Primäre Schäden (Vorschau)." (bei Speicherfehler Zusatz „Achtung: Browserspeicher nicht beschreibbar, gilt nur bis zum Neuladen.", warn).
- **„Beenden ohne Übernahme"** (`playerDiscard` Z. 1649): verwirft Entwurf, schliesst, Toast „Beendet ohne Übernahme" / „<Name>: <n> Entwurfsänderungen verworfen. Der Beispielbestand ist unverändert." (warn).
- **Esc/„Schliessen"** mit Entwurf (`requestClose` Z. 1321–1328): Rückfrage Titel „Codierung nicht übernommen", Text „Der Player hat <n> Änderungen im Entwurf. Übernehmen schreibt sie in den Beispielbestand, Verwerfen setzt sie zurück.", Knöpfe „Übernehmen und schliessen" / „Beenden ohne Übernahme" / „Zurück zum Player".

### 6.10 Tastenkürzel im Player (Z. 1696–1703)
Leertaste Play/Pause; ← / → ±5 s; s/S Stop; + oder = nächste Geschwindigkeitsstufe (0.5, 1, 1.5, 2, 4, 8); − vorherige; d/D Live-KI; m/M Markieren. Nur wenn der Player oberster Dialog ist und der Fokus nicht in einem Eingabefeld liegt.

### 6.11 Codierfenster „VSA-Codierung" (`#winVsa`, Z. 951–980; JS Z. 1706–1761)
- Titel „✓ VSA-Codierung (VSA-KEK 2020) · EN 13508-2", Badge `#vsaCtx` „aus Player · <Name> · Rückkehr zum Player" bzw. „aus Training Studio · Rückkehr dorthin"; rechts „Reset" (ghost small), „Abbrechen" (small). Fenster `medium` 1180×760.
- Körper Raster 1.2fr 1fr. **Links**: Karte „Häufig" (3-spaltiges Raster, Knopf mit Code fett + Text): BAB Riss, BCA Seitlicher Anschluss, BBC Ablagerung, BAF Oberflächenschaden, BAJ Verschobene Verbindung, BBA Wurzeln. Karte „Gruppe · Hauptcode · Charakterisierung" (4 Spalten, Köpfe GRUPPE / HAUPTCODE / CHARAKT. 1 / CHARAKT. 2): Gruppen BA Struktur, BB Betrieb, BC Bestand, BD Allgemein; Hauptcodes BA: BAA Verformung, BAB Riss, BAC Bruch, BAF Oberflächenschaden, BAH Schadhafter Anschluss, BAI Einragendes Dichtungsmaterial, BAJ Verschobene Verbindung; BB: BBA Wurzeln, BBB Inkrustation, BBC Ablagerung, BBD Eindringender Boden; BC: BCA Seitlicher Anschluss, BCC Bogen, BCD Rohranfang, BCE Rohrende; BD: BDD Wasserstand. Charakt. 1: BAB A längs / B quer / C diagonal / D ringförmig / E verzweigt; BAA A vertikal / B horizontal; BAC A partiell / B total; BBC A Sand / B Kies / C verfestigt; BAJ A breit / B versetzt / C Knick; BBD Z allgemein; sonst Hinweis „keine". Charakt. 2: nur BAB A Oberfläche / B durchgehend / Z andere; sonst „keine". Karte „Quantifizierung" Untertitel „Einheit und Bereich aus dem Katalog": „Q1 Rissbreite (mm, 0 bis 100)" (Standard 2), „Q2 Stufe (1 bis 5)" (Standard 3).
- **Rechts**: Karte „Schnellwahl Uhrlage" (5 Spalten, Mono 11): „12 00 Scheitel", „06 00 Sohle", „09 03 Rechts", „12 12 Gesamt", „00 00 Keine" (Wert = erste 5 Zeichen). Karte „Position": „Meter Start (m)", „Meter Ende (m)" (Platzhalter „nur Streckenschaden"), Checkbox „Streckenschaden (Anfang / Ende)", Checkbox „An einer Rohrverbindung", „Videozeit (mm:ss)", Fehlerhinweis `#vsaErr` (rot). Karte „Fotos": Kacheln „Foto 1 · aus Video mm:ss" (gewählt), „Foto 2 · kein Foto"; Knöpfe „Aus Video", „Vermessen" (Demo). Karte „Bemerkungen" (Textarea). Abschlusskarte: „Code:" + Ausgabe (Mono 15px), z. B. „BAB B A · Q1 2 · Stufe 3 · 12:00 Uhr · 9.42 m"; „Übernehmen" (primary), „Abbrechen" (ghost).
- Vorbelegung (Z. 1714–1725): Code aus Bearbeitungsbefund, Marker-Voreinstellung oder „BAB"; erste Charakterisierungen; Meter Start = aktueller Playermeter bzw. Befundmeter; Videozeit = Playerposition; Stufe 3 bzw. Befundstufe.
- Prüfungen beim Übernehmen (Z. 1746–1750): „Meter Start muss zwischen 0 und 400 m liegen.", „Ein Streckenschaden braucht Meter Ende.", „Meter Ende liegt vor Meter Start.", „Die Stufe muss zwischen 1 und 5 liegen.", „Q1 muss zwischen 0 und 100 mm liegen." (Komma als Dezimaltrenner erlaubt).
- Übernehmen aus Player (Z. 1753–1760): Befund mit Code „<main> <c1> <c2>", Text = Bemerkung oder Hauptcode-Klartext, Stufe, Meter, ggf. m2, Uhrlage, Quelle manuell, Status bestätigt → in den Player-**Entwurf**; Liste nach Meter sortiert; Player bleibt an derselben Position; Toast „Ereignis erfasst (Entwurf)" bzw. „Befund bearbeitet (Entwurf)" / „<code> bei <m> m für <Name>. Der Player bleibt an mm:ss. Erst „Codierung übernehmen" schreibt in den Beispielbestand."
- Übernehmen aus Studio (Z. 1752): `#stCode` = Code ohne Leerzeichen, `#stStufe`, `#stClock` = Uhrlage mit „–"; Toast „Code übernommen" / „<code> steht im Training Studio im Feld VSA-Code."
- Reset (Z. 1742): BAB / B / A, Uhrlage „12 00", Q1 2, Q2 3, Meter Ende leer, Streckenschaden aus, Bemerkung leer.

---

## 7. Fenster „Training Studio" (`#winStudio`, Z. 983–1027; JS Z. 1764–1780)

### 7.1 Titel und Aufbau
Titel „◉ Training Studio (Prüfplatz)", Badge ki mit `.pulse` „Analyse bereit", „Schliessen". Raster `210px minmax(0,1fr) 330px`, Lücke 12 (Z. 339); jede Spalte scrollt eigenständig.

### 7.2 Linke Spalte (Z. 986–999)
Knöpfe: „Fotos laden…", „PDF laden…", „PDF-Ordner laden…", „Eingang laden", „Segmentierung abarbeiten", „Goldprüfung (90)", „Weitere ▾" (Gold-Eingang öffnen, Warteschlange laden, Alle Gold-Reparaturfälle, Goldalbum, KI starten) — alle Demo. Feld „Rohr-DN (leer = 300)" Wert 300. Hinweis „Geprüft: 14 von 32 Bildern". Karte „Vorschläge aus dem Video-Durchlauf": „Video wählen…", „Durchgang starten" (small primary); Zeilen „Bogen" / „Meter 9,42 · stark · Konfidenz 0,83 · 4 Bilder", „Rohranfang" / „Sekunde 3 · Abnahme 85 %", „Rohrende" / „Sekunde 214 · Abnahme 89 %"; KI-Hinweis „„Abnahme" ist die gemessene Trefferquote der freigegebenen Lernstufe auf Prüfclips, keine Sicherheit für dieses Video."

### 7.3 Prüfplatz, Mitte (Z. 1000–1008)
- Videofläche 16:10 mit Rohrring; OSD „PDF-Operateurvorgabe, noch nicht bestätigt · BAB B · 9.42 m".
- **Hand-Box**: Rechteck links 40 % oben 22 % Breite 24 % Höhe 18 %, Rahmen 2px `--z0`, Label „Hand-Box" (Hintergrund `--z0`, Text `--z0-ink`, oben links).
- **SAM-Maske**: Rechteck links 42 % oben 26 % Breite 19 % Höhe 12 %, gestrichelt `--ok`, Label unten „SAM-Maske 91 % in der Box" (Hintergrund `--ok`, Text `--ok-ink`).
- Unten rechts „Box mit der Maus um den Schaden ziehen".
- Bildauswahl `#stThumbs` (`role="listbox"`, 4 Spalten): foto_0412.jpg (gewählt), foto_0413.jpg, foto_0414.jpg, foto_0415.jpg; gewählt = Outline 2px `--accent`.
- Karte „Modelltest am Foto": Select „Aktives Standardmodell (gesperrt, nicht qualifiziert)" / „bcc_nc15_seed46_20260808 · 8933…"; Knöpfe „Foto mit gewähltem Modell prüfen", „Foto allgemein mit KI prüfen" (Demo, „in dieser Vorschau läuft kein Modell").

### 7.4 Rechte Spalte, drei Schritte (Z. 1010–1024)
1. **„1 · KI-Vorschlag"**: `.find` „BAB B" / „Riss quer" / Kleinzeile „Klassifikator · Konfidenz 0,77 = Modellsicherheit, keine Trefferwahrscheinlichkeit"; Knopf „In Code übernehmen" (`#stTakeSuggestion`) → `#stCode` = „BABB", Hinweis „KI-Vorschlag in das Codefeld übernommen. Das ist noch keine Bestätigung." (Z. 1773).
2. **„2 · Fachliche Codierung"** (einspaltig): „VSA-Code (Pflicht)" `#stCode` „BABBA"; Knopf „Codieren… (Katalog)" (`#stCatalog`, öffnet Codierfenster mit Rückkehr); „Uhrlage (optional)" „12–02"; „Schadensstufe (optional)" „3"; „Beschreibung (mind. 10 Zeichen)" Textarea „Querriss im Scheitel, 2 mm breit, ohne Versatz."; Knöpfe „Akzeptieren (A)" (`#stAccept`, primary small), „Korrektur speichern (K)", „Verwerfen (V)" (ghost), „Nächstes (→)" (ghost) — die drei letzten Demo; Hinweis `#stNote` „Gold entsteht erst mit Hand-Box, gültiger SAM-Maske und Akzeptieren." Die Buchstaben A/K/V/→ sind nur Beschriftung; im Skript existiert kein Tastenhandler dafür.
3. **„3 · Freigabe für Training"**: Hinweis „Getrennter Schritt. Nur persönlich bestätigte Goldsamples mit Box und Maske gelangen in den Export."; Knopf „Für Training freigeben" (`#stRelease`, anfangs deaktiviert).
4. **„Goldstandard je Hauptcode"** Untertitel „Ziel 30 bis 50" (Raster 150px 1fr 34px, Z. 342): BAB - Riss 52, BCA - Anschluss 42, BBC - Ablagerung 35, BAF - Oberfläche 29, BCC - Bogen 57, BBA - Wurzeln 18, BBD - Eindringender Boden 6. Balken 6px, Breite min(100, 100·v/50) %, Farbe `--ok` ab 30, `--warn` ab 15, sonst `--bad` (Z. 1766).

### 7.5 Sperrregel Akzeptieren → Freigabe (Z. 1768–1780)
- Stand = JSON aus Code, Uhrlage, Stufe, Beschreibung (je getrimmt) und Text der gewählten Bildkachel (`studioStand`).
- **Akzeptieren**: Prüfung „Beschreibung braucht mindestens 10 Zeichen." (Fokus Beschreibung) bzw. „VSA-Code ist Pflicht." (Fokus Code); bei Fehler Hinweis, Bestätigung gelöscht, Freigabe gesperrt. Sonst: Stand gemerkt, Hinweis „Fachlich akzeptiert (Vorschau): <code>. Mit Hand-Box und gültiger SAM-Maske wäre das ein Goldsample. Freigabe für Training ist ein eigener Schritt.", Freigabe aktiv, Toast „Akzeptiert (Vorschau)" / „Kein Goldsample wurde geschrieben."
- **Jede Änderung** (Eingabe in Code, Uhrlage, Stufe, Beschreibung; Klick auf andere Bildkachel; „In Code übernehmen") ruft `studioInvalidate`: weicht der Stand vom bestätigten ab → Bestätigung weg, Freigabe gesperrt, Hinweis „Angaben nach der Bestätigung geändert. Zum Freigeben erneut akzeptieren."
- **Freigeben**: prüft erneut Pflichtfelder UND Stand-Gleichheit; bei Abweichung (auch erzwungenem Klick): Hinweis „Freigabe abgelehnt: <Grund oder „Die Angaben entsprechen nicht mehr dem bestätigten Stand."> Erneut akzeptieren.", Toast „Nicht freigegeben" (bad). Bei Erfolg: Toast „Für Training freigegeben (Vorschau)" / „Im Programm: nur persönlich bestätigte Goldsamples mit Box und Maske gelangen in den Export. Hier wurde nichts geschrieben.", Freigabe wieder gesperrt, Bestätigung gelöscht, Hinweis „Freigegeben (Vorschau). Für eine weitere Freigabe erneut akzeptieren."

---

## 8. JS-Regeln, die Verhalten beschreiben

### 8.1 „Nächste Haltung prüfen" (Z. 1439–1444, 1650)
- `nextTask()`: erste Haltung in Datenreihenfolge mit `pruefung === 'analysiert'`; sonst erste mit `pruefung === 'offen'` UND vorhandenem Video; sonst `null`. Mit Beispieldaten: h01 „78998-79002".
- Knopf: ohne Treffer Toast „Nichts offen" / „Alle Haltungen mit Video sind fachlich geprüft." (ok). Sonst `select('h', id, false, then)` → Seite Haltungen, Player öffnen. Der Chip in der Kopfzeile ruft denselben Knopf.
- Chip-Text: „Nächste Aufgabe: <Name> prüfen" oder „Keine offene Prüfung"; aktualisiert nach Übersicht-Render und nach Player-Übernahme.

### 8.2 Auswahlwechsel mit Rückfrage (Z. 1454–1466, 1331–1338)
- `select(k, id, force, then)`: ist eine andere Haltung/ein anderer Schacht gewählt und gibt es einen Entwurf (`draft` nicht leer), öffnet `askConfirm` mit Titel „Ungespeicherte Änderungen", Text „<Haltung|Schacht> <Name> hat ungespeicherte Änderungen. Speichern gilt nur in dieser Vorschau (Browserspeicher).", Knöpfe „Speichern und wechseln" / „Verwerfen und wechseln" / „Abbrechen, hier bleiben". Die Fortsetzung `then` läuft nur nach Speichern (bei Erfolg) oder Verwerfen; „Abbrechen" beendet die ganze Aktion (kein Player, keine Seitenänderung).
- Nach dem Wechsel: Tabelle, Panel und Schublade neu; gewählte Zeile wird in Sicht gescrollt.
- Gleicher Weg für: Play-Knopf in der Liste (Z. 1515), Panel-Knopf (Z. 1531), Übersicht-KI-Liste (Z. 1435), globale Suche (Z. 1610–1611), Dossier-Sprünge (Z. 1825–1827).

### 8.3 Speichern / Verwerfen / Speicherfehler (Z. 1467–1483, 1281–1286)
- Entwurf `state.ent[k].draft[id][feld] = string`; ein Feld zurück auf den Ursprungswert entfernt es aus dem Entwurf (Z. 1553–1556). Sichtbar: Feldrahmen `--warn`, Badge „● Ungespeichert", Knöpfe Speichern/Verwerfen aktiv, „●" in der ersten Tabellenspalte, Live-Aktualisierung der Zelle (Z. 1558).
- `saveEnt`: `zk` leer → `null`, sonst Zahl; andere Felder als Text. Bestand wird zuerst geändert, dann `persistData()` (nur `haltungen`/`schaechte` ohne `findings`/`ki` unter `nova.opt.data`). **Scheitert der Speicher** (`localStorage` wirft, z. B. QuotaExceededError): Bestand auf vorherige Werte zurück, Entwurf und Marke bleiben, Toast „Nicht gespeichert" / SAVE_FAIL („Speichern fehlgeschlagen: Der Browserspeicher hat die Daten nicht angenommen (zum Beispiel voll oder gesperrt). Die Eingabe bleibt erhalten, du kannst es erneut versuchen.") (bad), Rückgabe `false`. Im Rückfrage-Dialog bleibt dieser offen und zeigt SAVE_FAIL in `#cfNote` (rot) (Z. 1336).
- Erfolg: Entwurf leer, alles neu gerendert, Toast „Gespeichert (Vorschau)" / „<n> Feldwerte im Browserspeicher abgelegt. Keine Projektdatei wurde geschrieben." (ok).
- Verwerfen: Toast „Verworfen" / „Die Änderungen wurden zurückgesetzt."
- Beim Laden werden gespeicherte Werte über die Beispieldaten gelegt (Z. 1277–1280).

### 8.4 Spaltenansichten und Feldlisten
Siehe 4.3/4.4; Definition `VIEWS_H` Z. 1242–1249, `VIEWS_S` Z. 1250–1256, Beschriftungen `viewLabels` Z. 1270–1271. `alle = null` → alle Feldschlüssel in Themenreihenfolge (`allKeys`, Z. 1259). Auswahl je Seite getrennt in `nova.opt.viewH` / `viewS`.

### 8.5 Filter und Suche
- **Listensuche** (Z. 1485–1493): Text (kleingeschrieben, getrimmt) muss in name, strasse, material, eigentuemer oder funktion enthalten sein. Zusätzlich `zkFilter` (Zahl 0–4 oder `'u'` für „nicht berechnet"), gesetzt aus der Übersichts-Legende; Leeren der Haltungssuche hebt `zkFilter` auf (Z. 1573).
- **Sortierung**: keine Spaltensortierung; Datenreihenfolge. Befunde im Player-Entwurf werden nach Meter sortiert (Z. 1757). Übersichts-Balken absteigend nach Anzahl bzw. Kosten.
- **Feldsuche** in der Schublade (Z. 1565–1571): vergleicht nur den Beschriftungstext (`label > span`); nicht passende Felder und leere Themen `.hide`; passende Themen werden geöffnet; Hinweis „<n> Felder passen". Getrennt je Seite (Prüftabelle C1, C5).
- **Globale Suche** (Z. 1604–1623): Treffer „Haltung <Name>" (Name+Strasse enthält Text), „Schacht <Nummer>", „Strasse <Name>" (setzt Haltungssuche auf die Strasse); maximal 12; Enter wählt markierten oder ersten; Esc/Klick ausserhalb schliesst; leer → „Kein Treffer" / „Name, Nummer oder Strasse eingeben."
- **Medienkonflikte** (Z. 1785–1788): „Offen" = Status offen; sonst Typ; „Alle".
- **Druckcenter** (Z. 1802–1803): Suche (Name+Strasse), Eigentümer (Select), „Nur mit Kosten" (Kosten > 0), „Nur mit Massnahmen" (Massnahme nicht leer); „Filter zurücksetzen" leert alles (Kosten-Haken dann aus).
- **Einstellungen** (Z. 1890): Volltext über Karteninhalt und Zeilen.

### 8.6 Schublade und Splitter (Z. 1575–1601)
- Schublade offen/zu gespeichert (`drawerOpenh/s`); „gross" (`tall`) öffnet die Schublade mit; nicht gespeichert.
- Vertikaler Splitter: Breite 240–560px, Standard 320, Ziehen mit Pointer Capture, Pfeil links +20 / rechts −20 px; gespeichert `sidewh/s`.
- Horizontaler Splitter: Höhe min 120, beim Ziehen max Arbeitsflächenhöhe − 160; Pfeil hoch +20 / runter −20; gespeichert `drawerhh/s`.
- `layoutWork`: Mindesttabellenhöhe 32·7+20 = 244px; Schubladen-Maximum = max(120, Gesamt − 244 − 8); ohne gespeicherte Höhe oder bei Überschreitung: Höhe = min(Maximum, max(160, 36 % der Gesamthöhe)). Bei zugeklappter Schublade keine Anpassung. Wird bei Resize und Seitenwechsel ausgeführt.

### 8.7 Tabellenzustandsregeln
- Prüfstatus-Werte: `geprueft` („fachlich geprüft", Badge ok), `analysiert` („KI analysiert, Prüfung offen", Badge ki), `offen` („nicht analysiert").
- KI-Ampel (Z. 1496): siehe 4.3.
- Änderungsmarke nur in der ersten Spalte.

### 8.8 Dialog-Stapel und Fokus
Siehe 3.5/3.6. Zusätzlich: `openWinByName` (Z. 1365); `requestClose` nur beim Player mit Entwurf abweichend; Vorschau-Fenster-Knöpfe (`aria-pressed`) sind Segmente ohne eigene Logik.

### 8.9 Browserspeicher-Schlüssel (`nova.opt.` + …)
theme, motion, engine, data, viewH, viewS, secsh, secss, drawerOpenh, drawerOpens, sidewh, sidews, drawerhh, drawerhs.

### 8.10 Simulationen
- VSA-Lauf (Z. 1871–1877) und Schattenlauf (Z. 1862–1868): Klasse aus höchster Befundstufe (5→Z0, 4→Z1, 3→Z2, 2→Z3, sonst Z4); ohne Befund bleibt die Klasse. Der VSA-Lauf schreibt in Bestand und Browserspeicher; der Schattenlauf ändert nichts.
- Demo-Toast (Z. 1361) für alle `data-demo`-Elemente.

---

## 9. Beispieldaten-Felder je Thema der Eingabefelder

Typen: `sel` = Auswahlliste (Optionen aus `SEL`, Z. 1223–1229), `ro` = nur lesbar (gestrichelt, Beschriftungszusatz „ (nur lesbar)" per CSS Z. 197), `ta` = mehrzeilig (volle Breite), sonst einzeiliges Textfeld (Zahlenfelder mit `inputmode="decimal"`).

### 9.1 Haltung (`FIELDS_H`, Z. 1230–1235) — 36 Felder

**Stammdaten (14)**
| Schlüssel | Beschriftung | Typ / Optionen |
|---|---|---|
| name | Haltungsname (ID) | Text |
| strasse | Strasse | Text |
| material | Rohrmaterial | sel: Beton, PVC, PE, Steinzeug, Eternit, GFK, Guss, unbekannt |
| dn | Lichte Höhe / DN mm | Zahl |
| profil | Profilform | sel: Unbekannt, Kreisprofil, Eiprofil, Maulprofil, Offenes Profil, Rechteckprofil, Spezialprofil |
| breite | Lichte Breite mm | Zahl |
| nutzung | Nutzungsart | sel: Mischwasser, Schmutzwasser, Regenwasser |
| laenge | Haltungslänge m | Zahl |
| richtung | Inspektionsrichtung | sel: In Fliessrichtung, Gegen Fliessrichtung |
| datum | Datum/Jahr | Text |
| baujahr | Baujahr | Zahl |
| eigentuemer | Eigentümer | Text |
| geonis | GEONIS-Kennung | ro |
| lisag | Objekt-ID (Lisag) | Text |

**Bewertung (9)**
| Schlüssel | Beschriftung | Typ / Optionen |
|---|---|---|
| zk | Zustandsklasse | sel: – (leer), 0, 1, 2, 3, 4 |
| noteD | VSA-Zustandsnote D | Zahl |
| noteS | VSA-Zustandsnote S | Zahl |
| noteB | VSA-Zustandsnote B | Zahl |
| geschaetzt | Note geschätzt | sel: Ja, Nein |
| resultat | Prüfungsresultat | Text |
| referenz | Referenzprüfung | sel: Ja, Nein |
| gewaesser | Gewässerschutz | sel: Au, Zu, Ao |
| gw | Grundwasserspiegel | sel: unterhalb, oberhalb, unbekannt |

**Sanierung (10)**
| Schlüssel | Beschriftung | Typ / Optionen |
|---|---|---|
| sanieren | Sanieren Ja/Nein | sel: Ja, Nein |
| massnahme | Empfohlene Massnahmen | Text |
| inliner | Renovierung Inliner m | Zahl |
| verpressen | Anschlüsse verpressen | Zahl |
| manschette | Reparatur Manschette | Zahl |
| lem | Linerendmanschette LEM | Zahl |
| kurzliner | Reparatur Kurzliner | Zahl |
| neubau | Erneuerung Neubau m | Zahl |
| ausgef | Ausgeführt durch | sel: – (leer), Kanalsanierer, Baumeister, Gartenbauer |
| status | offen/abgeschlossen | sel: offen, abgeschlossen |

**Kosten und Bemerkungen (3)**
| Schlüssel | Beschriftung | Typ |
|---|---|---|
| kosten | Kosten CHF | Zahl |
| link | Link Video | Text |
| bemerk | Bemerkungen | ta |

Weitere Datensatzfelder ohne Eingabefeld (nur Tabelle/Panel/Player, Z. 1100–1107): `pruefung`, `dauer` (Sekunden), `video`, `verfahren`, `findings[]` (code, text, stufe, m, m2, uhr, quelle, conf, status), `ki[]` (code, text, ort, conf, bilder, abnahme).

### 9.2 Schacht (`FIELDS_S`, Z. 1236–1241) — 30 Felder

**Stammdaten (10)**: name „Schachtnummer"; strasse „Strasse"; funktion „Funktion" (sel: Kontrollschacht, Absturzschacht, Einlaufschacht, Spülschacht); material „Material" (sel: Beton, Kunststoff, andere, unbekannt); form „Schachtform" (sel: Unbekannt, Rund, Oval, Quadratisch, Rechteckig, Vieleckig); mass1 „Grösstes Innenmass mm"; mass2 „Kleinstes Innenmass mm"; tiefe „Tiefe m"; baujahr „Baujahr"; geonis „GEONIS-Kennung" (ro).

**Zustand und Inspektion (9)**: zk „Zustandsklasse" (sel –/0–4); resultat „Prüfungsresultat"; dichtheit „Dichtheit" (sel: nicht geprüft, dicht, undicht); referenz „Referenzprüfung" (Ja/Nein); gewaesser „Gewässerschutz" (Au/Zu/Ao); gw „Grundwasserspiegel" (unterhalb/oberhalb/unbekannt); belastung „Belastungsklasse" (sel: A15, B125, C250, D400, E600, F900); datum „Inspektionsdatum"; schaeden „Primäre Schäden" (ta).

**Sanierung und Kosten (7)**: sanieren „Sanieren Ja/Nein"; massnahme „Empfohlene Massnahme"; ausgef „Ausgeführt durch"; status „offen/abgeschlossen"; kosten „Kosten CHF"; eigentuemer „Eigentümer"; bemerk „Bemerkungen" (ta).

**Dokumente und Medien (4)**: pdf „PDF Protokoll"; pdfEigen „PDF eigen"; link „Link Video"; fotos „Fotos" (ro).

### 9.3 Beispielhaltungen (Z. 1100–1170), Kurzliste
| ID | Name | Strasse | Material | DN | Länge | ZK | Prüfung | Video | Kosten |
|---|---|---|---|---|---|---|---|---|---|
| h01 | 78998-79002 | Seilergasse | Beton | 300 | 42.30 | 1 | analysiert | 20251006_78998-79002.mp4 | 38070 |
| h02 | 07.6588-6587 | Gotthardstrasse | PVC | 250 | 28.10 | 4 | geprueft | 20251006_07.6588-6587.mp4 | 0 |
| h03 | 77457-77453 | Bahnhofstrasse | Steinzeug | 200 | 31.75 | 0 | geprueft | 77457_77453.mpg | 71440 |
| h04 | 75394-75390 | Schöllenenstrasse | Beton | 400 | 55.20 | 3 | geprueft | 20251007_75394-75390.mp4 | 0 |
| h05 | 75390-75388 | Schöllenenstrasse | Beton | 400 | 48.60 | 2 | analysiert | 75390.mpg | 9350 |
| h06 | 80401-80399 | Kirchgasse | Steinzeug | 250 | 19.85 | 4 | geprueft | 20251008_80401-80399.mp4 | 0 |
| h07 | 80399-80397 | Kirchgasse | Beton | 300 | 36.40 | 2 | offen | (kein Video) | leer |
| h08 | 81120-81118 | Rohrbachweg | PE | 200 | 24.00 | 3 | analysiert | 20251008_81120-81118.mp4 | 0 |
| h09 | 81118-81115 | Rohrbachweg | PE | 200 | 61.30 | 4 | geprueft | 20251008_81118-81115.mp4 | 0 |
| h10 | 82007-82005 | Sustenstrasse | Beton | 500 | 73.90 | 1 | geprueft | 82007.mp4 | 61340 |
| h11 | 82005-82001 | Sustenstrasse | Beton | 500 | 68.20 | null | offen | 20251009_82005-82001.mp4 | leer |
| h12 | 79002-79006 | Seilergasse | Beton | 300 | 28.10 | 4 | geprueft | 20251006_79002-79006.mp4 | 0 |
| h13 | 83310-83308 | Wassenerstrasse | Eternit | 300 (Breite 450, Eiprofil) | 39.50 | 2 | analysiert | 20251010_83310-83308.mp4 | 34200 |
| h14 | 83308-83305 | Wassenerstrasse | Beton | 300 | 44.00 | 3 | geprueft | 20251010_83308-83305.mp4 | 0 |

Schächte (Z. 1173–1201): s01 78998 Oval 1100×900 Z2; s02 79002 Rund 600 Z4; s03 77457 Absturzschacht Rund 800 Z0 undicht; s04 75394 Einlaufschacht Kunststoff Rund 600 Z3 dicht; s05 80401 Quadratisch 1000 Z4; s06 81120 Spülschacht Rund 600 Z1 undicht; s07 82007 Rechteckig 1200×900 ZK null (ohne PDF); s08 83310 Rund 800 Z2.

---

## 10. Begleitdateien (Kurzfassung)

**AENDERUNGEN.md** (32 Zeilen): v2 setzt sechs Restpunkte um — R01 Wechsel vor Player mit Rückfrage und Fortsetzung; R02 Trainingsfreigabe an den bestätigten Stand gebunden; R03 Speicherfehler sichtbar, Eingabe bleibt; R04 Strg+K/F3/Tab/Esc in Dialogen; R05 Kontrast mit eigenen Texttokens (`--ok-text`, `--warn-text`, `--bad-text`, `--ok-ink`, `--z0-ink`), Kacheln deckend; R06 Player arbeitet auf Entwurf mit „● Entwurf: n Änderungen". Bildschirmbilder: 14 (nicht 16). Unverändert: zentraler Beispielbestand, getrennte Suchen, gerechnete Kennzahlen, Rückkehr aus dem Codierfenster, ruhiger Modus, Systemschriften, Offline-Aufruf, 7/9/12 sichtbare Zeilen.

**PRUEFTABELLE.md** (188 Zeilen): 80 bestanden, 0 fehlgeschlagen, 2 nicht geprüft (Texte auf Verläufen, es gibt keine). Belegte Zahlen: 15 Seiten, 3 Fenster; Kontrast 2112 Textelemente ≥ 4,5:1 in beiden Themen; kleinste Schrift 11 px; sichtbare Zeilen 7 / 9 / 12; Player-Seitenspalte 338,24 px; Studio-Spalte 330 px; Dringend 3 = 1 + 2; Kosten 243'900 CHF; 181 Demo-Aktionen mit ◌. Nicht geprüft: Windows-Skalierung 125/150 % in WPF, Screenreader, echte Importe/Exporte/Modelle, vollständiger WCAG-Test, optische Bewertung durch den Fachanwender, andere Browser.
