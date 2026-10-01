# Nova WPF-Etappe 2 — Abnahme

Stand: 07.09.2026 (Fixwelle) · Branch `feature/nova-etappe-2` · Worktree `C:\Sewer-Studio_KI_4.5-nova`

Alle zwölf Bilder wurden nach der Fixwelle neu erzeugt; die Befunde B1–B8 und die
Schlussreview F1–F7 sind unten mit Status geführt.

Grundlage: freigegebener Prototyp `docs/reviews/2026-09-06-nova/optimiert/v2/`,
Inventar `docs/reviews/2026-09-06-nova/wpf-etappe-2/PROTOTYP-INVENTAR.md`,
Aufgaben 1–19 unter `.superpowers/sdd/2026-09-06-nova-wpf-etappe-2/`.

Alle Bilder stammen aus dem isolierten Prüfhost
(`werkzeug/Program.cs`, eigener AppData- und Wissensordner, kein produktiver Programmstart)
bei Full HD 1920 × 1080, DPI 96, mit einem rein künstlichen Testprojekt
(14 Haltungen, 6 Schächte, zwei Beobachtungen mit Uhrlage und Stufe, ein offener KI-Befund,
ein ovaler Schacht 1100 × 900, ein künstlicher Videoclip).

---

## 1 Bildnachweise

| Datei | Was sie zeigt |
|---|---|
| `bilder/uebersicht-hell.png` | Projektübersicht, Palette Hell · Glas |
| `bilder/uebersicht-dunkel.png` | Projektübersicht, Palette Dunkel · Cockpit |
| `bilder/haltungen-hell.png` | Haltungen mit Rohrring, KI-Hinweis, Eingabefelder |
| `bilder/haltungen-dunkel.png` | dasselbe im dunklen Theme |
| `bilder/schaechte-hell.png` | Schächte im Nova-Layout mit Schachtansicht |
| `bilder/schaechte-dunkel.png` | dasselbe im dunklen Theme |
| `bilder/import-hell.png` | Seitenkopf `NovaPageHeader` und Werkzeugleiste |
| `bilder/import-dunkel.png` | dasselbe im dunklen Theme |
| `bilder/einstellungen-hell.png` | Bereichsliste 01–06, Design-Umschalter, Hintergrund-Engine |
| `bilder/einstellungen-dunkel.png` | dasselbe im dunklen Theme |
| `bilder/player-hell.png` | Player-Kopf, Zeitleiste mit Schadensmarken, Bedienleiste |
| `bilder/training-hell.png` | Training Studio, drei Spalten, drei nummerierte Schritte |

---

## 2 Prüfpunkte je Aufgabe

| Aufgabe | Prüfpunkt | Nachweis | Ergebnis |
|---|---|---|---|
| 1 Paletten-Tokens | Hell · Glas und Dunkel · Cockpit als Theme-Tokens, Karten und Leiste | alle zwölf Bilder | bestanden |
| 2 Pillen, Chips, Tabellenkopf | Chips mit Zählern, Tabellenkopf in Grossbuchstaben, runde Knöpfe | `haltungen-hell`, `schaechte-hell` | bestanden (B1 behoben: Kapseln mit geraden Flanken) |
| 3 Tabellen-Feinschliff | Name fett, Zahlen rechts in Datenschrift | `haltungen-hell` | bestanden |
| 4 Prüfstatus und „Nächste Aufgabe" | Regel liefert die KI-analysierte Haltung | Chip „Nächste Aufgabe: 10001-10002 prüfen" in allen Bildern | bestanden |
| 5 Kopfzeile | Brotkrume `Projekt / Seite`, Aufgaben-Chip, Speicherstand | Kopfzeile aller Bilder, Fusszeile „Geladen: projekt.json" | bestanden |
| 6 Globale Suche Strg+K | Suchfeld mit Tastenmarke in der Kopfzeile | `uebersicht-hell` | bestanden (B1/B2 behoben: Kapsel, Marke innen, Platzhaltertext) |
| 7 KI-Bereitschaft | Aufklapper unten links mit echtem Zustand | „KI nicht gestartet" in allen Bildern | bestanden |
| 8 Kennzahlen | Haltungen/Schächte/dringend/Kosten aus einem Bestand | `uebersicht-hell` (14 · 6 · 6 · 0 CHF) | bestanden |
| 9 Sitzungsregister | Karte „KI-Vorabdurchlauf diese Sitzung" | `uebersicht-hell` | bestanden |
| 10 Seite „Übersicht" | Hero, Kennzahlen, Zustand, häufigste Schäden, Projekte, Stammdaten | `uebersicht-hell`, `uebersicht-dunkel` | bestanden (Donut bewusst durch Legende ersetzt) |
| 11 Umschalter, Zähler, gross anzeigen | Spaltenansichten mit Zählern, Themen mit Zählern | `haltungen-hell` (Kompakt 7 … Alle Spalten 52) | bestanden |
| 12 Übersicht rechts | Rohrring mit echter Uhrlage, Fakten, Schadenliste, KI-Hinweis | `haltungen-hell` | bestanden (B3 behoben: Bildlauf, alle Werte vollständig, „–" statt leerer Einheit) |
| 13 Schächte-Werkzeugleiste | Hauptaktionen, „Weitere Aktionen", fünf Spaltenansichten | `schaechte-hell` | bestanden |
| 14 Schachtansicht rechts | Schachtform mit zwei Massen, Fakten, Hinweis „nie berechnet" | `schaechte-hell` (1100 × 900) | bestanden |
| 15 Player | kompakter Kopf, Bedienleiste, „Weitere ▾", Schadensmarken | `player-hell` | bestanden (Videobild siehe Grenze G2) |
| 16 Training Studio | drei Spalten 240 \| * \| 330, drei nummerierte Schritte | `training-hell` | bestanden (B4 behoben: Spalte 210–240, Umbruch, Liste und Vorschau untereinander) |
| 17 Einstellungen | Design-Umschalter, Hintergrund-Engine, Bewegung | `einstellungen-hell`, `einstellungen-dunkel` | bestanden (B5 behoben: Beschriftung statt Rohtext; B8: keine Leerfläche mehr) |
| 18 Seitenkopf | `NovaPageHeader` mit Untertitel | `import-hell` („Import — Kanalfernseh-Projekte, Protokolle, Medien"), `einstellungen-hell` | bestanden |
| 19 Abnahme | Prüfhost, zwölf Bilder, Release-Gesamtlauf, Doku | diese Datei, `werkzeug/`, `bilder/` | bestanden |

---

## 3 Befunde

Die folgenden Punkte waren an den Bildern der ersten Abnahme belegt. Sie sind in der
Fixwelle vom 07.09.2026 abgearbeitet; der Status steht bei jedem Punkt. Die Bilder in
`bilder/` zeigen den Stand NACH der Fixwelle.

### B1 — Runde Knöpfe werden zu Ellipsen (durchgängig, wichtigster Punkt)

`Theme.xaml` und `ThemeLight.xaml` setzen in den drei Vorlagen `ToolbarButton`,
`ToolbarButtonAccent` und `CompactToggleButton` sowie in der Ressource `RadiusPill`
den Wert `CornerRadius="999"` (vorher 5 bzw. 4). Im Prototyp ist das CSS
`border-radius: 999px` und ergibt eine **Kapsel** mit geraden Ober- und Unterkanten.
WPF begrenzt den Eckenradius dagegen je Ecke auf die halbe Breite **und** die halbe
Höhe und zeichnet deshalb eine **Ellipse**.

Sichtbar wird das überall, am deutlichsten in `import-hell.png`: bei
„Import Kanalfernseh-Projekt" und „Import PDF" liegen Symbol und Textenden ausserhalb
der blauen Fläche. Ebenso bei „Im Player prüfen" (`haltungen-hell.png`) und beim
Suchfeld der Kopfzeile.

Vorschlag: in beiden Themes `CornerRadius="999"` durch die halbe Knopfhöhe ersetzen
(alle drei Vorlagen haben `MinHeight = 30`, also `15`). Dazu gehören die drei Zeilen
`DesignAuditNovaPaletteTests.cs:63-65`, die den Wert `999` heute festhalten.

### B2 — Tastenmarke „Strg K" ragt aus dem Suchfeld

Die Marke ist ein `Border` mit `DockPanel.Dock="Right"`, der die volle Höhe des
Suchfelds füllt. Zusammen mit der Ellipse aus B1 steht sie oben, unten und rechts
über der Umrandung (`uebersicht-hell.png`, Kopfzeile). Im Prototyp liegt sie innerhalb
der Kapsel. Zusätzlich fehlt der Platzhaltertext „Haltung, Schacht oder Strasse suchen",
den der Prototyp im leeren Feld zeigt.

### B3 — Fakten der Haltungsübersicht werden mittendrin abgeschnitten

In `haltungen-hell.png` sind die Werte unter „Material", „DN / Profil", „Länge",
„Inspektion", „Prüfung" und „Video" nur zur oberen Hälfte sichtbar. Ursache ist kein
Bindungsfehler, sondern Platzmangel: Im `DockPanel` von `HaltungUebersichtPanel`
bekommen zuerst der KI-Hinweis (unten) und der Rohrring (oben) ihre Höhe; das
`UniformGrid` mit den acht Eckdaten wird danach mit zu wenig Höhe gemessen und
schneidet die zweite Zeile jeder Zelle ab. Ohne offene KI-Vorschläge und ohne Werte
(Etappe-1-Stand) fiel das nicht auf.

Vorschlag: die Eckdaten in einen `ScrollViewer` legen oder das ganze Panel scrollbar
machen; ein Wert darf nie halb dastehen.

### B4 — Linke Spalte des Training Studios ist zu schmal

In `training-hell.png` schneidet die 210 px breite linke Spalte die Karte
„Vorschläge aus dem Video-Durchlauf" hart ab: der Knopf „Durchlauf starten" ist nur
als „Dur…" sichtbar, die Hinweistexte enden mitten im Wort. Entweder die Spalte
verbreitern oder die Karte umbrechen lassen.

### B5 — Auswahlfelder zeigen den Rohtext des Optionsobjekts

`einstellungen-hell.png` zeigt
`AutoSaveModeOption { Value = OnEachChange, Label = Bei jeder Aenderung }` und
`IntOption { Value = 2, Label = 2 (Standard) }`;
`training-hell.png` zeigt
`TrainingStudioPreviewModelOption { Kind = ActiveStandard, DisplayName = … }`.
An allen drei Stellen ist `DisplayMemberPath` im XAML gesetzt.

Die eigene ComboBox-Vorlage in `Theme/Controls.xaml` bindet `SelectionBoxItem`,
`SelectionBoxItemTemplate` und `SelectionBoxItemStringFormat` korrekt; die Ursache ist
damit noch nicht abschliessend geklärt. `Controls.xaml` wurde in Etappe 2 **nicht**
verändert — der Punkt ist Altbestand und kein Regress dieser Etappe, wird hier aber
erstmals bildlich belegt. Bitte beim Merge im echten Programmstart gegenprüfen.

### B6 — Zellentinte der Zustandsklassen im dunklen Theme

`schaechte-dunkel.png`: Die Ziffern in den farbigen Zustandsklassenzellen sind weiss
auf Gelb und Hellgrün und damit kaum lesbar. `ZustandsklasseCellStyleFactory` setzt
`DataGridCell.Foreground` auf Schwarz, doch der implizite Style
`TargetType="{x:Type TextBlock}"` der Themes setzt `Foreground` selbst und schlägt die
Vererbung in die Textspalte. Im hellen Theme fällt es nicht auf, weil `TextBrush` dort
ohnehin dunkel ist. CLAUDE.md hält für diese Zellen ausdrücklich schwarze Tinte fest.

### B7 — Beschriftung von Akzentknöpfen ist dunkel statt weiss

Gleiche Wurzel wie B6: Ein Akzentknopf, dessen Beschriftung als reiner
`Content`-String gesetzt ist, erhält vom impliziten `TextBlock`-Style den normalen
Textvordergrund statt des weissen Knopfvordergrunds. Sichtbar bei „Im Player prüfen"
(`haltungen-hell.png`) und „Import Kanalfernseh-Projekt" (`import-hell.png`).
Knöpfe, deren Inhalt ein eigener `TextBlock` mit gesetzter Farbe ist (zum Beispiel
„Speichern"), sind korrekt weiss.

### B8 — Kleinere Punkte (Stand der ersten Abnahme)

- Brotkrume und Seitentitel waren im dunklen Theme kontrastarm (`haltungen-dunkel.png`).
- Das Symbol des aktiven Leisteneintrags war im dunklen Theme kaum zu sehen
  (`uebersicht-dunkel.png`).
- Auf der Einstellungsseite blieb zwischen Seitenkopf und der ersten Gruppe rund 200 px
  leere Fläche (`einstellungen-hell.png`).
- Die Zahlen der Zustandslegende standen hochgestellt direkt am Text
  („Z4 · kein Handlungsbedarf²").
- Im Training Studio meldete die Statuszeile „Pruefe lokale Vision-KI…" — ein sichtbarer
  Text ohne Umlaut.

---

### Status der Befunde nach der Fixwelle (Commit siehe `final-fix-report.md`)

| Befund | Status | Was gemacht wurde |
|---|---|---|
| B1 Ellipsen statt Kapseln | **behoben** | `RadiusPill` ist 15 (halbe Höhe der 30-px-Bedienelemente); neu `RadiusChip` 11, `RadiusBar` 5, `RadiusCircle` 36. Nachweis: gerade Flanken an „Speichern", den Spaltenchips und den Balken der Übersicht. |
| B2 Tastenmarke ragt aus dem Suchfeld | **behoben** | Suchfeld mit fester Höhe 30, Marke vertikal zentriert innerhalb der Kapsel, Platzhalter „Haltung, Schacht oder Strasse suchen" im leeren Feld. |
| B3 Fakten abgeschnitten | **behoben** | Das Panel liegt in einem `ScrollViewer`; nur die Schadenliste bekommt die Reststrecke. Leere Felder zeigen „–" statt „ m" oder „ · ". |
| B4 Linke Spalte des Training Studios | **behoben** | Spalte 240 px mit `MinWidth` 210 / `MaxWidth` 240, kein waagrechter Bildlauf mehr (dadurch brechen Texte um), Liste und Vorschau untereinander. Grenze: Die Vorschlagstabelle bringt in der schmalen Spalte ihren eigenen waagrechten Bildlauf mit. |
| B5 Auswahlfelder zeigen ToString | **behoben, Produktfehler** | Mit `ComboBoxAnzeigeIsolatedSmokeTests` nachgewiesen: WPF leitet `SelectionBoxItemTemplate` NICHT aus `DisplayMemberPath` ab. `ComboBoxAnzeigeTemplateSelector` schliesst die Lücke. |
| B6 Zellentinte der Zustandsklassen | **behoben** | Die Zustandsklasse der Schachtliste ist eine eigene Vorlagenspalte; ihr Anzeigetext bindet die Tinte an die `DataGridCell`. Gemessen im Bild: reines Schwarz auf Gelb. |
| B7 Akzentknöpfe dunkel beschriftet | **behoben** | Die vier Knopfvorlagen reichen ihre Tinte über `ContentPresenter.Resources` an den Inhalt durch. |
| B8 Kleinere Punkte | **behoben / Grenze** | Brotkrume und Leistensymbol: **Prüfhost-Effekt**, siehe Grenze G6 — der Prüfhost malt die Mica-Fläche jetzt aus, die Kopfzeile ist im Bild lesbar. Leerfläche der Einstellungen: behoben (der Reiterinhalt war mittig ausgerichtet). Legendenzähler: eigene rechte Spalte. „Pruefe lokale Vision-KI…" und „Bei jeder Aenderung": echte Umlaute. |

### Schlussreview F1–F7

| Befund | Status | Was gemacht wurde |
|---|---|---|
| F1 Rohrring/Schadenliste zeigen Bestandscodes | **behoben** | `SchadensgruppenRegel` (Application/Common) ist die gemeinsame Quelle für Cockpit, Rohrring und Schadenliste. Nur BA-/BB-Hauptcodes, Stufe absteigend, bei Gleichstand kleinerer Meterwert. |
| F2 Schacht-Spaltenansichten vergleichen ordinal | **behoben** | Vergleich und Zähler laufen über `SchachtFeldnamen.Falte`; `SchachtUebersichtPanel` und `ProjektUebersichtKennzahlen` lesen Schachtfelder ebenso. |
| F3 Alte Übersicht unerreichbar | **behoben, bewusste Änderung** | Menü `Ansicht → Klassische Übersicht` (`AppSettings.ShowUebersichtNovaLayout`, Standard `true`). Die neue Seite bekam „Vorschau-PDF" und anklickbare Schadenzeilen; beide Wege laufen über `ProjektVorschauPdfUseCase`. `OverviewPreviewPdfCommandTests` prüft jetzt über den Bedienweg. |
| F4 Sitzungsregister merkt leere Durchläufe | **behoben** | `CodingSuggestionMerkRegel`: erst nach der Staleness-Prüfung merken und nur, wenn ein Teil wirklich lief. Ohne Fund zeigt die Karte den Grund. |
| F5 `PruefungText`/`VideoText` nur bei Record-Wechsel | **behoben** | Das Panel hört auf `HaltungRecord.PropertyChanged`, mit derselben An-/Abmeldung wie bei den Entries. |
| F6 Threadvertrag `SchachtRecord.Protocol` | **behoben** | `SchachtUebersichtPanel` marshallt auf den Dispatcher; der Vertrag steht am Setter. |
| F7 `ColorBorder` zu kontrastarm | **behoben** | Neuer Token `InputBorderBrush` (hell `#828FA4`, dunkel `#64769C`) für Eingabefelder und Knopf-Umrisse, ≥ 3:1 gegen `CardBrush`. Der Kartenrand bleibt hell. |

### Bewusste Änderung: Umschalter „Klassische Übersicht"

Bei offenem Projekt zeigt „Übersicht" weiterhin die neue Projektübersicht. Der checkbare
Menüpunkt `Ansicht → Klassische Übersicht` gibt die bisherige Seite zurück — sie trägt
Projektliste, Vorschau und Vorschau-PDF und bleibt ohne Projekt ohnehin der Startbildschirm.
Muster und Speicherort entsprechen dem Umschalter „Alte Haltungsansicht".

### In dieser Aufgabe behobene Kleinigkeiten

| Fund | Datei | Korrektur |
|---|---|---|
| Zwei Icon-Knöpfe neben „Verschieben auf Pos." und „Gehe zu Zeile" zeigten ein leeres Kästchen statt eines Pfeils | `Views/Pages/DataPage.xaml`, `Views/Pages/SchaechtePage.xaml` | Glyph als `ui:FluentIcon` statt als Button-`Content`; der implizite `TextBlock`-Style überschrieb sonst die Icon-Schrift. `AutomationProperties.Name` ergänzt. |
| Gleicher Fehler beim Ausblenden-Knopf der Feldkarten | `Views/Controls/RecordDetailsView.xaml` | dito |
| Titel des KI-Hinweises brach nicht um und wurde abgeschnitten | `Views/Pages/Haltungsansicht/HaltungUebersichtPanel.xaml` | `TextWrapping="Wrap"` ergänzt |

Der leere Kasten war auch in der Etappe-1-Sichtprobe
(`wpf-etappe-1/nachweise/sichtprobe/p1-01-haltungen-1366x768.png`) vorhanden und ist
damit kein Prüfhost-Artefakt gewesen.

---

## 4 Bewusste Abweichungen vom Prototyp

Diese Punkte sind in den Global Constraints des Umsetzungsplans festgelegt und
gelten als abgenommen, nicht als Fehler:

- **Dunkler Akzent bleibt `#2563EB`.** Das Prototyp-Hellblau wird im dunklen Theme
  nicht als Flächenakzent übernommen.
- **Zustandsfarben Z0–Z4 bleiben unverändert.** Der Prototyp schlägt andere Töne vor;
  die Farben sind fachlich gesetzt.
- **Kein „Verwerfen"-Knopf** in der Codierung.
- **Kein Benutzer-Avatar** in der Kopfzeile; dort steht „Projekt wechseln".
- **Kein Schein-Knopf „Für Training freigeben".** Die Freigabe läuft weiter über das
  Export-Register im Training Center.
- **Donut ersetzt durch anklickbare Legende** auf der Übersichtsseite.
- **de-CH mit Dezimalpunkt** in allen Zahlen.
- Demo-Inhalte des Prototyps (Import-Baum, Beispiel-Log und ähnliche Beispieldaten)
  wurden nicht nachgebaut; das sind Prototyp-Füllungen, keine Funktionen.

---

## 5 Aufgeschobene Kleinbefunde aus den Aufgaben 1–18

Aus den Zwischenberichten übernommen, bewusst offen gelassen:

- Das `TextTrimming` des Untertitels im `NovaPageHeader` greift im `StackPanel` nie,
  weil dort keine Breite begrenzt wird.
- Der Untertitel tritt zweimal auf: im Seitenkopf und als Beschriftung der Matrix.
- Auf der Projektseite stehen alle Knöpfe rechts.
- Die Icon-Zeile der VSA-Seite wurde entfernt.
- Der `NetzHintergrund` ist nur in der Kopfzeile, an den Rändern und hinter der Leiste
  sichtbar — hinter den Karten deckt die Kartenfläche ihn ab.
- `ReduceMotion` greift erst beim nächsten Auslöser, nicht rückwirkend auf laufende
  Animationen.
- Der Aufgaben-Chip hat noch keinen fachlichen Prüfstatus-Nachweis aus echten
  Projekten; die Regel ist an künstlichen Daten geprüft.

---

## 6 Gesamtlauf

Erste Abnahme (Release):

```
dotnet build AuswertungPro.sln -c Release      → 0 Fehler, 0 Warnungen
dotnet test  AuswertungPro.sln -c Release --no-build
```

| Testprojekt | bestanden | übersprungen | Fehler |
|---|---|---|---|
| ProjectModernizer.Tests | 62 | 0 | 0 |
| AuswertungPro.Next.Pipeline.Tests | 2562 | 3 | 0 |
| AuswertungPro.Next.Infrastructure.Tests | 6192 | 6 | 0 |
| AuswertungPro.Next.UI.Tests | 6502 | 8 | 0 |

Summe 13 318 bestanden, 17 übersprungen, 0 Fehler.

Im ersten Lauf fiel ein Test um:
`SidecarRestartServiceTests.Lifetime_stop_tracked_beendet_nur_den_eigenen_prozess`.
Er startet einen echten PowerShell-Prozess und erwartet, dass er innerhalb einer Frist
beendet ist; gezielte Wiederholung und der zweite vollständige Gesamtlauf sind grün.
Der Fehler ist damit last- und zeitabhängig und hat nichts mit dieser Etappe zu tun —
weder der Dienst noch sein Test wurden angefasst.

Nach der Fixwelle (Debug):

```
dotnet build AuswertungPro.sln -c Debug        → 0 Fehler, 0 Warnungen
dotnet test  AuswertungPro.sln -c Debug --no-build
```

| Testprojekt | bestanden | übersprungen | Fehler |
|---|---|---|---|
| ProjectModernizer.Tests | 62 | 0 | 0 |
| AuswertungPro.Next.Pipeline.Tests | 2562 | 3 | 0 |
| AuswertungPro.Next.Infrastructure.Tests | 6234 | 6 | 0 |
| AuswertungPro.Next.UI.Tests | 6521 | 10 | 0 |

Summe 15 379 bestanden, 19 übersprungen, 0 Fehler.

In einem Zwischenlauf fiel dabei einmal
`KatasterHaltungFeldNachschlagTests.Die_Suche_laeuft_nicht_auf_dem_aufrufenden_Thread` um —
er misst, auf welchem Thread eine Suche läuft, und ist damit last- und zeitabhängig. Gezielt
wiederholt und im abschliessenden Gesamtlauf ist er grün. Weder der Dienst noch sein Test
wurden angefasst.

Die übersprungenen Tests sind die bekannten maschinengebundenen Abnahmen
(Sidecar, echtes Video, Live-Dienste) sowie der Selbst-Übersprung der isolierten
WPF-Smoketests, die ihre Arbeit in einem Kindprozess leisten.
`NachschlagKontextmenueTests` hat in beiden Läufen nicht gehangen.

---

## 7 Grenzen dieser Abnahme

- **G1 — Kein produktiver Programmstart.** Der Prüfhost unterdrückt
  `Application.OnStartup`: kein Echtzeitspiegel, keine QGIS-Brücke, kein KI-Start,
  eigenes AppData- und Wissensverzeichnis, künstliches Projekt. Das ist bewusst so,
  weil ein produktiver Start mit dem echten Profil bei „Autosave bei jeder Änderung"
  Kundendaten berühren würde. Er belegt daher **keine** vollständige Programmabnahme.
- **G2 — Das Videobild bleibt schwarz.** LibVLC zeichnet über ein eigenes Fenster;
  ein `RenderTargetBitmap` erfasst diese Fläche nicht. Kopf, Zeitleiste,
  Schadensmarken und Bedienleiste sind echte WPF-Elemente und im Bild vollständig.
- **G3 — Keine Skalierungsmessung.** Alle Bilder sind Full HD bei 100 %
  (DPI 96). Windows-Skalierung 125 % und 150 % wurde in dieser Etappe **nicht**
  gemessen. Das bleibt eine **offene Sichtprüfung durch Pascal beim Merge** —
  in Etappe 1 war gerade dort der Platz knapp.
- **G4 — Kein echter Bedienweg.** Die Bilder entstehen über ViewModel-Aufrufe
  (`TryOpenProject`, `EnterWorkspaceOn`, Auswahl setzen, `PlayVideoCommand`),
  nicht über Maus- und Tastatureingaben. Menüs, Kontextmenüs, Aufklapper und
  Tastenkürzel sind damit nicht bildlich belegt.
- **G6 — Der Prüfhost malt die Mica-Fläche aus.** `Fluent.Backdrop="Mica"` setzt
  `Window.Background` auf Transparent; die Fläche zeichnet der Windows-Compositor, den ein
  `RenderTargetBitmap` nicht erfasst. In der ersten Abnahme war die Kopfleiste im Bild deshalb
  durchsichtig, und die daraus abgeleitete Aussage „Brotkrume kontrastarm" war ein Messfehler
  des Prüfhosts, kein Produktfehler (gemessen: die Schrift trägt `#EAF0FA`, der Hintergrund
  hatte Alpha 0). Der Prüfhost setzt vor dem Foto die Theme-Fläche ein.
- **G7 — Vorschlagstabelle im Training Studio.** Die Liste „Vorschläge aus dem
  Video-Durchlauf" hat fünf Spalten und steht in einer 240 px breiten Werkzeugspalte. Sie bringt
  dafür ihren eigenen waagrechten Bildlauf mit; abgeschnitten wird nichts, eng ist es trotzdem.
- **G5 — Nur zwei Themes, sieben Ansichten.** Die übrigen Seiten (Projekt, Export,
  Medienkonflikte, Druckcenter, Dossiers, die drei Bewertungsseiten, Diagnose)
  sind nur über ihre Wächtertests abgedeckt, nicht über Bilder.
