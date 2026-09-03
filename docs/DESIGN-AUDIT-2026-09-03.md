# SewerStudio — Design-Audit 2026-09-03

**Erstellt:** 2026-09-03 von Fable (Claude)
**Stand:** Branch `feature/eval-pruefsatz-review`, Working Tree mit 61 offenen XTF-/Schacht-Aenderungen (nicht Teil dieses Audits)
**Methode:** Code-Lesung der 82 XAML-Dateien (16'560 Zeilen), der Theme-Ressourcen, der Effekt-Klassen und der Design-Waechtertests. Zahlen sind per Skript gezaehlt, nicht geschaetzt. **Es wurden keine Screenshots gemacht** — Aussagen zur Wirkung am Bildschirm sind aus dem Code abgeleitet und muessen am laufenden Programm bestaetigt werden.
**Scope:** Optik, Symbole, Animationen, Bedienbarkeit. Keine Logik, keine KI-Pipeline, keine NuGet-Pakete.

---

## 1. Kurzfazit

Das Fundament ist gut und besser als bei den meisten Fachprogrammen dieser Groesse: ein
zentrales Theme mit Hell/Dunkel, Bewegungs-, Radius- und Schatten-Tokens, eine Icon-Schrift,
ein Ruhe-Schalter, eigene Bausteine fuer Leerzustaende, Statusflaechen, Toasts und
Wartekreise — und **13 Waechtertests**, die das Fundament schuetzen. Die Design-Plaene vom
Juli 2026 (Symbole & Effekte, Neural Elegance, Optik-Paket) sind umgesetzt.

**Was fehlt, ist die Durchdringung.** Das Fundament wird an vielen Stellen nicht benutzt:
226 Stellen mit 10-Pixel-Schrift ohne Schriftgroessen-Token, 216 verschiedene
Abstandswerte, 8 Rundungsvarianten trotz vier Radius-Tokens, 95 fest eingebrannte Farben in
10 Dateien, 53 sichtbare Texte mit `ae/oe/ue` statt Umlaut neben 453 mit echten Umlauten,
31 Menuepunkte ohne Symbol, und die Eintrittsanimation laeuft nur in 19 von 45 Fenstern.

**Note: B.** Solide, professionell, aber uneinheitlich im Detail. Mit den Quick Wins aus
Abschnitt 5 (ca. 2-3 Arbeitstage, kein Risiko) ist **B+/A-** erreichbar. Der Sprung auf A
braucht die zwei mittleren Pakete (Schriftskala, Feedback-Schicht).

---

## 2. Was gut ist (Ist-Bausteine — darauf aufbauen, nichts doppelt bauen)

| Baustein | Fundort | Befund |
| --- | --- | --- |
| Theme Hell/Dunkel | `Theme/ThemeLight.xaml` (907 Z.), `Theme/Theme.xaml` (910 Z.), Schalter in Einstellungen | vollstaendig, beide Paletten gleich gross |
| Design-Tokens | `Theme/Controls.xaml`: `AnimDurationFast..XSlow`, `AnimEaseOut/In/InOut`, `RadiusS..XL`, `ShadowS..L`, `AccentGlow`, `FontMono`, `FontIcon` | vorhanden |
| Icon-Schrift | `FluentIcon.cs`, `IconFonts.cs`; 184 Fluent-Glyphen in 27 Dateien, 120x `{DynamicResource FontIcon}` | eine Sprache, nur 1 Rest `Segoe MDL2 Assets` direkt |
| Effekte | `WindowFx` (Fenster-Eintritt), `EntranceFx` (gestaffeltes Einblenden), `HoverFx` (Anheben), `ButtonFx`, `ClickSheenAdorner`, `AnimatedContentControl` (Seitenwechsel), `GridLengthAnimation` (Sidebar) | sauber, alle mit `CubicEase`, alle ueber `AnimationTokens` |
| Ruhe-Schalter | `Controls/MotionSettings.cs`, `AppSettings.ReduceMotion`, folgt Windows-Systemeinstellung | vorhanden und in Bausteinen beachtet |
| Feedback-Bausteine | `EmptyStateControl` (28 Einsaetze), `StatusHost` (12), `BusyOverlay` (12), `ToastHost`, `NeuralPulseDot`, `NeuralSphereControl` (6) | gute Auswahl |
| Dialoge | 269 Aufrufe ueber den Dialogdienst, nur noch 7 `MessageBox.Show` | Migration praktisch fertig |
| Tooltips | 367 Tooltips bei 470 Buttons (ca. 78 %) | ueberdurchschnittlich |
| Navigation | Sidebar als ListBox mit Icon + Titel, animierte Breite, ausblendbar; Hauptmenue mit Icons und Tastenkuerzeln | klar |
| Waechter | `DesignAudit*Tests` (8 Dateien), `Theme*Tests` (4), `XamlActionWiringGuardTests` | Fundament ist testgeschuetzt |
| Startanimation | `StartupSplashWindow`, hell, ganzflaechig, ueberspringbar | frisch (2026-09-03) |

---

## 3. Befunde

Prioritaet: **P1** = sichtbar fuer jeden Nutzer, billig zu beheben · **P2** = sichtbar, mittlerer Aufwand · **P3** = Feinschliff / langfristig.

### 3.1 Schrift — zu klein und ohne Skala (P1)

| Groesse | Stellen |
| --- | --- |
| 8 px | 1 |
| 10 px | **226** |
| 11 px | 210 |
| 12 px | 220 |
| 13 px | 94 |
| 14 px | 42 |
| 15-76 px | 104 |

- **656 Stellen mit 10-12 px.** Der Nutzer ist Kanalinspekteur, arbeitet am Laptop im Feld und am 4K-Bildschirm im Buero. 10 px ist auf beiden schwer lesbar; Windows selbst nutzt 12 px als Minimum fuer Beschriftungen.
- **Hotspots fuer 10 px:** `VsaCodeExplorerWindow` (50), `PlayerCodingSidePanel` (48), `VideoAnalysisPipelineWindow` (29), `TrainingCenterWindow` (25), `PhotoMeasurementWindow` (21). Das sind genau die Fenster, in denen codiert wird — also die Arbeitsfenster.
- **Es gibt keine Schriftgroessen-Tokens.** Radius, Schatten und Bewegung haben Tokens, die Schrift nicht. Jede Datei entscheidet selbst.

**Empfehlung:** Vier Tokens `TextXS=11`, `TextS=12`, `TextM=13`, `TextL=15` in `Controls.xaml` (als `sys:Double`), 8 und 10 px verschwinden. Ein Waechtertest verbietet `FontSize="10"` und `FontSize="8"` ausserhalb von Chart-/Achsenbeschriftungen. Aufwand M (viele Stellen, aber mechanisch).

### 3.2 Sichtbare Texte mit `ae/oe/ue` statt Umlaut (P1, Quick Win)

- **53 Stellen** in Beschriftungen, Menues und Tooltips schreiben `oeffnen`, `pruefen`, `fuer`, `Schaechte` — daneben stehen **453 Stellen mit echten Umlauten**. Beides nebeneinander wirkt unfertig, besonders im Hauptmenue (`MainWindow.xaml`: 10) und in den Einstellungen (`SettingsPage.xaml`: 12).
- Beispiele: `Log-Ordner oeffnen`, `System-Monitor oeffnen`, `Ausgewaehlten Videokandidaten im Player pruefen.`, `Klicken oder Taste druecken zum Ueberspringen` (Splash!).

**Empfehlung:** Alle 53 Stellen auf echte Umlaute umstellen. Ein Waechtertest sucht in `Content`, `Header`, `ToolTip`, `Text`, `Title` nach den 30 haeufigsten Ersatzschreibweisen. Aufwand S (1-2 Stunden). Der Quellcode (Kommentare, Bezeichner) bleibt bewusst bei `ae/oe` — die CLAUDE.md-Konvention gilt fuer Code, nicht fuer das, was der Nutzer liest.

### 3.3 Symbole (P1/P2)

- **31 Menuepunkte ohne Symbol** (119 `MenuItem`, 88 mit `MenuItem.Icon`). Im Hauptmenue sind alle bestueckt; die Luecken liegen in Kontextmenues der Seiten (Rechtsklick auf Haltung/Schacht/Dossier). Checkbare Punkte duerfen laut Icon-Leitbild leer bleiben — die Zahl enthaelt diese noch. Aufwand S.
- **15 Text-/Emoji-Zeichen in 8 XAML-Dateien** (`PlayerWindow` 5, `DossiersPage` 3, `DossierAreaWindow` 2) und **17 in 8 Code-Dateien** (`DossierPreviewFieldPanel.*`, `DossierParcelLookupWindow`, `DataPageConverters`). Der Dossier-Bereich ist nach dem Symbol-Plan vom Juli entstanden und hat die Regel „Glyphen statt Textzeichen" nicht mitbekommen. Aufwand S.
- **Ein Rest `FontFamily="Segoe MDL2 Assets"`** direkt statt `{DynamicResource FontIcon}`. Aufwand XS.
- Positiv: Die Player-Steuerleiste ist komplett Fluent (Play, Pause, Frame vor/zurueck, Schnappschuss, Vollbild). Geschwindigkeit `1x 2x 4x 8x` als Text ist richtig — das ist Inhalt, kein Symbol.

### 3.4 Fest eingebrannte Farben (P2)

**95 hardcodierte Farben in 10 Dateien.** Zwei Gruppen:

| Bewusst dunkel (Video-Umfeld, in Ordnung) | Fraglich (brechen im Dunkel-Design) |
| --- | --- |
| `PlayerWindow` (schwarze Overlays `#DD111318`) | `HaltungsansichtView` (`Foreground="#000000"`) |
| `PlayerCodingSidePanel` | `SchachtansichtView` |
| `LiveFrameWindow` | `SanierungsmassnahmenWindow` |
| `PhotoMeasurementWindow` | `BusyOverlay` |
| `StartupSplashWindow` | `PipeGraphTimeline` |

Die linke Spalte sollte einen eigenen kleinen Token-Satz bekommen (`VideoOverlayBrush`, `VideoOverlayStrongBrush`), damit die Werte wenigstens an einer Stelle stehen. Die rechte Spalte gehoert auf `DynamicResource` umgestellt — im Dunkel-Design ist schwarzer Text auf dunkler Karte unlesbar. Aufwand S-M. Waechter: `DesignAuditThemeResourceTests` um eine Positivliste der erlaubten Dateien erweitern.

### 3.5 Abstaende und Rundungen — Tokens vorhanden, nicht benutzt (P2)

- **216 verschiedene `Margin`-Werte.** Ein 8-Pixel-Raster (4/8/12/16/24) wuerde davon ca. 12 uebrig lassen.
- **8 Rundungsvarianten** (`1, 2, 3, 4, 5, 6, 8, 10`) obwohl `RadiusS/M/L/XL` existieren. Meist: 6 (94x), 4 (65x), 8 (55x). Die Werte 1, 2, 3, 5 sind Streuung.

**Empfehlung:** Neue XAML nur noch mit Tokens; Bestand nur bei Beruehrung anpassen (kein Grossumbau). Ein Waechter meldet neue `CornerRadius`-Zahlen ausserhalb der Tokens. Aufwand S fuer den Waechter, laufend fuer den Bestand.

### 3.6 Animationen — Fundament da, Reichweite klein (P2)

| Effekt | Einsatz | Potential |
| --- | --- | --- |
| `WindowFx.Entrance` | 19 von 45 Fenstern | die 26 restlichen anschliessen (je 1 Zeile) |
| `EntranceFx.Stagger` | 2 Stellen | Startseite, Dossier-Cockpit, Import-Bericht |
| `HoverFx.Lift` | 1 Stelle | Karten auf Startseite, Dossier-Kacheln, Foto-Galerie |
| Seitenwechsel | `AnimatedContentControl` | vorhanden |
| Sidebar | `GridLengthAnimation` | vorhanden |
| Toast | 3 Aufrufe im ganzen Programm | Speichern, Export fertig, Import fertig, Gold gespeichert |

Die Animationen sind gut gebaut (alle `CubicEase`, alle ueber Tokens, alle respektieren den Ruhe-Schalter). Sie sind nur selten eingesetzt. **Das groesste Loch ist das Erfolgs-Feedback:** Nach „Speichern", „Excel erstellt", „XTF geschrieben" passiert optisch fast nichts — der Nutzer liest die Statuszeile oder bekommt einen Dialog. Ein Toast mit Haekchen (und „Ordner oeffnen"-Link) waere die spuerbarste einzelne Verbesserung. Aufwand S-M.

Was **nicht** gebaut werden sollte: Endlos-Animationen im Hauptfenster, Parallax, Partikel. Das Programm ist ein Arbeitswerkzeug; das Leitbild „Neural Elegance" (Juli) hat das richtig festgelegt.

### 3.7 Bedienbarkeit und Barrierefreiheit (P2/P3)

- **Tastenkuerzel:** nur 6 `KeyBinding` und 7 `InputGestureText` im ganzen Programm. Ein Codierer im Player braucht die Haende auf der Tastatur: Frame vor/zurueck, Ereignis anlegen, Uebernehmen, Abbrechen — das gehoert als sichtbares Kuerzel in den Tooltip. Aufwand S (nur anzeigen, was schon gebunden ist) bis M (neue Bindungen).
- **`AutomationProperties.Name`: 12 Stellen** bei 470 Buttons. Icon-Buttons ohne Text sind fuer Screenreader stumm; ausserdem nutzt die Windows-Sprachsteuerung dieselben Namen. Aufwand M, mechanisch.
- **Fokus-Ring:** 8 `FocusVisualStyle`-Stellen. Tastaturnutzer sehen in den meisten Fenstern nicht, wo sie sind. Ein zentraler Fokusstil im Theme (Akzent-Rahmen 2 px) loest das an einer Stelle. Aufwand S.
- **Einstellungen:** 1591 Zeilen, 6 Reiter, 17 Gruppen, **keine Suche**. Wer „Fotos pro Seite" sucht, klickt Reiter durch. Ein Suchfeld oben, das Gruppen ausblendet, ist bei dieser Groesse Standard. Aufwand M.
- **Fensterflut:** 45 Fenster, davon 39 in `Views/Windows`. Player, Training Studio, Pipeline, Dossier-Vorschau oeffnen jeweils eigene Fenster. Das ist bei einem Zwei-Bildschirm-Arbeitsplatz gewollt; auf dem Laptop stapeln sie sich. **Keine Empfehlung fuer einen Umbau** (zu gross, zu riskant) — aber: alle 45 haben `WindowStartupLocation` gesetzt, das ist bereits sauber.

### 3.8 Kleine Konsistenzpunkte (P3)

- Buttons, die einen Dialog oeffnen, enden mal mit `...` (9x), mal nicht (`Öffnen`, `Durchsuchen...`). Eine Regel: `...` nur, wenn ein Dialog folgt.
- `FontFamily="Consolas"` 69x direkt und 18x `"Consolas, Cascadia Mono"` statt `{DynamicResource FontMono}` (2x). Token existiert.
- `Segoe UI Variable Display/Text` an 4 Stellen direkt — gehoert als `FontDisplay`/`FontBody` ins Theme.

---

## 4. Was bewusst NICHT empfohlen wird

- **Kein Wechsel auf ein UI-Framework** (WPF-UI, MahApps, Fluent-Theme von .NET 9+). Das eigene Theme ist vollstaendig, testgeschuetzt und passt zur Domaene. Ein Wechsel kostet Wochen und bringt optisch wenig.
- **Kein Umbau der Fensterstruktur** (Docking, Tabs statt Fenster). Zu gross ohne Rueckfrage, Nutzen unklar.
- **Keine Emoji im Bedienbereich**, auch nicht „zur Auflockerung". Das Leitbild vom Juli gilt.
- **Keine neuen NuGet-Pakete** fuer Icons oder Animationen.

---

## 5. Massnahmenplan (priorisiert)

| # | Massnahme | Aufwand | Wirkung | Risiko |
| --- | --- | --- | --- | --- |
| **Q1** | 53 Umlaut-Ersatz-Texte auf echte Umlaute + Waechtertest | S | hoch (jeder sieht es) | keins |
| **Q2** | 31 Menuepunkte mit Fluent-Icon bestuecken (checkbare ausgenommen) | S | mittel | keins |
| **Q3** | 15 + 17 Text-/Emoji-Zeichen im Dossier-Bereich und Player durch Glyphen ersetzen | S | mittel | keins |
| **Q4** | `WindowFx.Entrance` in die 26 restlichen Fenster | S | mittel (Wertigkeit) | keins |
| **Q5** | Zentraler Fokusstil im Theme | S | mittel (Tastatur) | keins |
| **Q6** | Fest eingebrannte Farben in Haltungs-/Schachtansicht, Sanierung, BusyOverlay, Timeline auf Tokens; Video-Overlay-Tokens fuer den Player | S-M | hoch im Dunkel-Design | gering |
| **M1** | Schriftgroessen-Tokens `TextXS..L`; 10 px und 8 px abschaffen; Waechter | M | **sehr hoch** (Lesbarkeit in den Arbeitsfenstern) | gering (Layouts pruefen) |
| **M2** | Erfolgs-Toasts fuer Speichern, Excel, XTF, Import, Gold; `HoverFx.Lift` auf Karten/Kacheln; `EntranceFx.Stagger` auf Startseite und Dossier-Cockpit | M | hoch (spuerbares Feedback) | keins |
| **M3** | `AutomationProperties.Name` fuer alle Icon-Buttons; Tastenkuerzel im Player sichtbar machen | M | mittel | keins |
| **M4** | Suchfeld in den Einstellungen | M | mittel | gering |
| **L1** | Abstands-/Radius-Tokens durchziehen (nur bei Beruehrung, mit Waechter fuer Neues) | laufend | mittel | keins |

**Empfohlene Reihenfolge:** Q1-Q6 an einem Tag als ein Commit-Paket („Design-Feinschliff"), danach M1 (der eigentliche Hebel), dann M2. M3/M4 nach Bedarf.

### Stand 2026-09-03 abends: Q1-Q6 UMGESETZT

Waechter: `tests/AuswertungPro.Next.UI.Tests/DesignAuditFeinschliffTests.cs` (7 Tests, alle zuerst rot gesehen).
UI-Testprojekt: 6257 gruen, 1 rot — `NachschlagKontextmenueTests` (isolierter WPF-Kindprozess,
60-s-Limit) faellt nur im Gesamtlauf unter Last; allein besteht er in 26 s. Nicht durch dieses Paket verursacht.

- **Q1:** 74 sichtbare Texte auf echte Umlaute (die 53 aus dem Audit plus 21, die das genauere
  Testmuster zusaetzlich fand — z. B. `Schaechte.xlsx` in der Exportseite, obwohl die Datei
  wirklich `Schächte.xlsx` heisst). Schweizer `ss` bleibt bewusst stehen.
- **Q2:** 16 Menuepunkte mit Fluent-Symbol (RecordDetailsView, BuilderPage NPK-Menue,
  MediaConflictsPage). Der Rest der 31 waren Menueleisten-Koepfe und Schieberegler-Punkte,
  die laut Icon-Leitbild leer bleiben.
- **Q3:** 21 Textsymbole ersetzt: `▲▼✕⟲⟳` in der Dossier-Vorschau ueber die neue Erweiterung
  `FluentGlyphKnopf.MitGlyph` (setzt zugleich den zugaenglichen Namen aus dem Tooltip),
  `↶↷` im Undo-Controller, `📷` auf der Planseite, `⚠☎✉` in der Parzellensuche, `▾▴` am
  Kennzahlen-Umschalter, `▲▼` im Gebietsfenster, `⟲⟳` im Planfenster.
- **Q4:** 22 Fenster treten jetzt ueber `ui:WindowFx.Entrance` auf; ausgenommen bleiben
  MainWindow, PlayerWindow, LiveFrameWindow, StartupSplashWindow.
- **Q5:** `KeyboardFocusVisual` gab es bereits fuer Button/ToggleButton; jetzt auch fuer
  CheckBox, RadioButton, ComboBox, Expander, TreeViewItem, TabItem, Slider, GridViewColumnHeader.
- **Q6:** Neue Tokens `ScrimBrush` und `StatusBadgeTextBrush` (hell und dunkel) sowie sieben
  `Video*Brush`-Tokens in `Controls.xaml`. Haltungs-/Schachtansicht, Sanierungsfenster,
  BusyOverlay und die Player-Abdunkelungen lesen sie. `PipeGraphTimeline` ist Player-intern
  und bleibt in der Video-Positivliste.

---

### Stand 2026-09-03 spaet: M1 Schriftskala UMGESETZT (Commit nach 1adbbcb90)

Entscheid Pascal: Reihenfolge „Schrift zuerst", Untergrenze **11 px**. Waechter
`DesignAuditSchriftskalaTests` (4 Tests, zuerst rot). UI-Testprojekt danach 6262 gruen.

- Tokens in `Controls.xaml`: `TextXS` 11 · `TextS` 12 · `TextM` 13 · `TextL` 15 · `TextXL` 18 ·
  `TextTitle` 22 · `TextDisplay` 28, dazu `IconHero` 36 fuer die vier grossen Leerzustand-Glyphen.
- **885 `FontSize`-Stellen** in Seiten, Fenstern, Controls und Dialogen lesen jetzt Tokens:
  8-11 -> XS (437), 12 -> S (216), 13/13.5 -> M (89), 14-16 -> L (87), 17-21 -> XL (36),
  22/24 -> Title (15), 30-40 -> Display/IconHero (4). Die Theme-Dateien behalten Zahlen
  (zwei 10er auf 11 gehoben). Der Splash bleibt aussen vor.
- Im Code: VSA-Explorer-Kacheln (10 und 8 -> 11), CategoryBars, Hydraulik-Panel und
  DonutChart-Minimum auf 11. Gezeichnete Beschriftungen auf Video/Grafik/PDF bleiben klein
  (Positivliste im Test).
- **Am Bildschirm pruefen:** Die Zusammenlegung 14/16 -> 15 und 20 -> 18 veraendert
  Abschnitts- und Untertitel um 1-2 px; die dichten Panels (PlayerCodingSidePanel,
  VsaCodeExplorer, VideoAnalysisPipelineWindow) sind von 10 auf 11 px gewachsen.

## 6. Waechter, die dieses Audit dauerhaft machen

Neu in `tests/AuswertungPro.Next.UI.Tests/DesignAudit*`:

1. `Sichtbare_Texte_verwenden_echte_Umlaute` — sucht `Content/Header/ToolTip/Text/Title` nach Ersatzschreibweisen.
2. `Keine_Schrift_unter_11_Pixel_ausser_Achsen` — mit Positivliste fuer Chart-Beschriftungen.
3. `Rundungen_nur_ueber_Tokens` — neue Zahlen ausserhalb `RadiusS..XL` sind rot.
4. `Hardcodierte_Farben_nur_in_Video_Fenstern` — Positivliste der fuenf Video-Dateien.
5. `Menuepunkte_haben_Icon_oder_sind_checkbar`.

Jeder dieser Tests folgt dem Muster der bestehenden `DesignAuditChromeAndGlyphTests`.
