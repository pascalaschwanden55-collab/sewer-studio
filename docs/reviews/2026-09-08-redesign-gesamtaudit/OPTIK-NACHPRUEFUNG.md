# Optische Nachprüfung des Redesigns · 8. September 2026

**Ergebnis in einem Satz: Das Redesign wirkt — die Hauptseiten sind ruhig, klar und
zusammengehörig. Was noch stört, sind Reste aus der Zeit davor, ein paar leere Flächen
und drei Sichtfehler, die jeder sofort sieht.**

Grundlage sind die 40 Bildschirmfotos der Morgenprüfung unter `bilder/` (hell, dunkel,
1280 × 720, Tabellenvarianten) plus die zugehörigen Codestellen. Nichts davon wurde am
laufenden Programm nachgemessen; die Fotos stammen aus dem isolierten Prüfhost mit
künstlichem Projekt.

Was gut ist, zuerst — damit klar ist, was **nicht** angefasst werden sollte: die
gruppierte Leiste, die Kopfzeile mit Suche und „Nächste Aufgabe", die Chips für
Spaltenansichten, die Zustandsklassen-Marken, der Rohrring rechts, die numerierten
Einstellungsbereiche, der Leerzustand auf der Dossierseite. Das ist die Linie, an der
sich der Rest ausrichten sollte.

## A · Sichtfehler — sieht jeder sofort

### O1 · Untertitel ist durchgestrichen (Sanierungs-Matrix, Schacht-Matrix)

„~~Pro Haltung eine Hauptarbeit wa~~ehlen" und „~~Massnahmen und Kosten je~~ Schacht" —
die Akzentlinie unter dem Titel läuft mitten durch den Untertitel. Ursache: Die Linie ist
eine `TextDecoration` mit `PenOffset="7"` am Stil `PageTitle`
([Theme.xaml:196-203](../../../src/AuswertungPro.Next.UI/Theme/Theme.xaml#L196-L203)),
und auf diesen zwei Seiten steht der Untertitel **unter** dem Titel mit nur 2 px Abstand
([SanierungsMatrixPage.xaml:21-25](../../../src/AuswertungPro.Next.UI/Views/Pages/SanierungsMatrixPage.xaml#L21-L25)).
Alle anderen Seiten setzen den Untertitel **rechts** neben den Titel (`NovaPageHeader`),
dort passiert nichts.

**Korrektur:** Beide Matrix-Seiten auf `NovaPageHeader` umstellen — behebt den Fehler und
macht die Köpfe gleich wie auf den elf anderen Seiten.

### O2 · Grünes Feld leuchtet im Dunkelmodus

„Sanieren Ja/Nein" hat im Dunkelmodus eine hellgrüne Karte mit hellem Text — praktisch
unlesbar (Bild `tabellen/Dark-Haltungen-standard.png`, unten Mitte). Die Farben sind fest
eingetragen: `#EAF6E3` / `#6AA84F`, daneben `#E4F3FA` / `#00A2D6` für „Ausgeführt durch"
([RecordDetailsView.xaml:498-504](../../../src/AuswertungPro.Next.UI/Views/Controls/RecordDetailsView.xaml#L498-L504)).

Das widerspricht der Regel vom 03.09. (feste Farbwerte nur in den sechs Video-Dateien).
Der Wächter `DesignAuditFeinschliffTests` hat es nicht gemeldet — er sieht offenbar keine
`Setter` innerhalb von `DataTrigger`. **Korrektur:** vier Theme-Tokens (`SanierenSubtleBrush`,
`SanierenBorderBrush`, …) in beiden Themes; Wächter auf Trigger-Setter erweitern.

### O3 · Stufen 4 und 5 im Training Studio abgeschnitten

Im Bild `Light-TrainingStudio-1920x1080.png` sind nur die Knöpfe 1, 2, 3 zu sehen. Das ist
R3 aus dem Morgenbericht; hier nur der Sichtbeleg. Die fünf Knöpfe stehen in einem
horizontalen `StackPanel` mit `Width="40"`
([TrainingStudioWindow.xaml:506-521](../../../src/AuswertungPro.Next.UI/Views/Windows/TrainingStudioWindow.xaml#L506-L521)),
werden aber deutlich breiter gezeichnet und laufen rechts aus der 330-px-Spalte.
Ein `UniformGrid Columns="5"` löst das ohne Rechnerei.

## B · Schreibweise — Regeln vom 03.09., die noch nicht überall greifen

### O4 · Scharfes ß in sichtbaren Texten

Die Regel lautet Schweizer `ss`, kein `ß`. Sichtbar geblieben:

| Text | Stelle |
|---|---|
| „Straße", „Programm schließen" | [ProjectPage.xaml:34, 76](../../../src/AuswertungPro.Next.UI/Views/Pages/ProjectPage.xaml#L34) |
| „Maßnahmen", „0 Haltungen mit Maßnahme" | [SanierungsMatrixPage.xaml:33, 86](../../../src/AuswertungPro.Next.UI/Views/Pages/SanierungsMatrixPage.xaml#L33) |
| „Maßnahme", „Schächte mit Maßnahme" | [SchachtSanierungsMatrixPage.xaml:27, 52](../../../src/AuswertungPro.Next.UI/Views/Pages/SchachtSanierungsMatrixPage.xaml#L27) |
| „Straße", „Maßnahmen (Vorschau)", „Nur mit Maßnahmen" | [BuilderPage.xaml:171, 470, 478](../../../src/AuswertungPro.Next.UI/Views/Pages/BuilderPage.xaml#L470) |
| „Sanierungsmaßnahmen…" | [DataPage.xaml:77, 266](../../../src/AuswertungPro.Next.UI/Views/Pages/DataPage.xaml#L77), [HaltungsansichtView.xaml:106](../../../src/AuswertungPro.Next.UI/Views/Pages/Haltungsansicht/HaltungsansichtView.xaml#L106), [SettingsPage.xaml:1206](../../../src/AuswertungPro.Next.UI/Views/Pages/SettingsPage.xaml#L1206) |
| „Schließen" | CostCatalogEditorDialog, ProtocolHistoryWindow, ProtocolObservationsWindow |
| Laufzeittexte | DataPageCostRestoreController, DataPageMeasureSuggestionController, DataPageViewModel:733, ProtocolEntryEditorDialog:618, PhotoMeasurement* |

Der Umlaut-Wächter prüft nur `ae/oe/ue`
([DesignAuditFeinschliffTests.cs:25](../../../tests/AuswertungPro.Next.UI.Tests/DesignAuditFeinschliffTests.cs#L25)),
nicht `ß`. Eine Zeile mehr im Regex, und die Liste oben wird rot.

### O5 · ASCII-Umlaute in sichtbaren Texten aus dem ViewModel

„Pro Haltung eine Hauptarbeit **waehlen** – Meter, DN und **Anschluesse** kommen
automatisch" ([SanierungsMatrixPageViewModel.cs:102, 433](../../../src/AuswertungPro.Next.UI/ViewModels/Pages/SanierungsMatrixPageViewModel.cs#L102)),
„6 **Schaechte** geladen", „Projekt … **oeffnen**"
([SchachtSanierungsMatrixPageViewModel.cs:198-199](../../../src/AuswertungPro.Next.UI/ViewModels/Pages/SchachtSanierungsMatrixPageViewModel.cs#L198)),
„Keine Haltungen geladen (… **oeffnen**)" (SanierungsMatrixPageViewModel.cs:331).

`DesignAuditLaufzeittexteTests` prüft nur drei C#-Quellen. Die zwei Matrix-ViewModels
gehören dazu.

### O6 · Entwicklertexte, die der Nutzer liest

- „Spalten geladen: 32" unten links auf der Schachtseite
  ([SchaechtePageViewModel.cs:486](../../../src/AuswertungPro.Next.UI/ViewModels/Pages/SchaechtePageViewModel.cs#L486))
- „Records: 14" auf der Projektseite
  ([ProjectPage.xaml:88](../../../src/AuswertungPro.Next.UI/Views/Pages/ProjectPage.xaml#L88))
- „Treffer=14/14" kursiv im Druckcenter-Filter
  ([BuilderPageFilterSummaryBuilder.cs:40](../../../src/AuswertungPro.Next.UI/ViewModels/Pages/BuilderPageFilterSummaryBuilder.cs#L40))
- „● Rot  Lernbasis: 0 Fälle" über der Haltungstabelle — ein nacktes Farbwort als
  KI-Zustand. „KI-Ampel: Rot — noch keine Lernbasis" sagt, was gemeint ist.

## C · Leere Flächen und fehlende Beschriftungen

### O7 · Leere Textfelder ohne Hinweis

- Die Feldsuche in der Kopfzeile der Eingabefelder ist ein leerer weisser Kasten — nur
  der Tooltip verrät, dass man dort tippen kann
  ([HaltungFelderDrawer.xaml:66-68](../../../src/AuswertungPro.Next.UI/Views/Pages/Haltungsansicht/HaltungFelderDrawer.xaml#L66-L68)).
  Gleiche Kopfzeile bei den Schächten.
- Die Suche oben rechts in den Einstellungen ebenso
  ([SettingsPage.xaml:279](../../../src/AuswertungPro.Next.UI/Views/Pages/SettingsPage.xaml#L279)).
- Auf Export und Dossiers steht ganz unten ein leerer, umrandeter Kasten — die Statuszeile
  ohne Text. Leer sollte sie unsichtbar sein.

Ein Platzhalter „Feld suchen …" / „Einstellung suchen …" genügt.

### O8 · Leerzustände: die Dossierseite zeigt, wie es geht

Ohne gewählte Zeile ist auf Haltungen und Schächten rund die Hälfte des Bildschirms
weiss: rechts eine einzelne Zeile „Keine Haltung gewählt. Links eine Zeile wählen." oben
in der Ecke, unten ein leerer 250-px-Kasten mit vier Knöpfen. Die Dossierseite macht es
vor — Symbol, zentrierter Satz, Hinweis auf die Aktion. Denselben Baustein auf die
Übersicht rechts und auf die zugeklappten Eingabefelder anwenden.

### O9 · Die Übersicht hat zwei halbleere Karten

- „KI-Vorabdurchlauf" ist zu vier Fünfteln leer; ein Satz klebt unten. Ohne Durchlauf
  könnte die Karte schmal sein oder den Satz zentriert zeigen.
- „Projekte" zeigt genau eine Pille „projekt" — was passiert beim Klick? Der Kartentitel
  müsste „Zuletzt geöffnet" heissen, und die Pille braucht ein Symbol.
- „Sanierungsverfahren" hängt „Keine Kosten erfasst" an den **unteren** Rand
  ([ProjektUebersichtPage.xaml:203](../../../src/AuswertungPro.Next.UI/Views/Pages/ProjektUebersichtPage.xaml#L203)),
  die Nachbarkarte „Stammdaten" füllt von **oben**. Nebeneinander sieht das aus wie ein
  Versehen.
- Kennzahlen „14 geprüft 0", „6 mit Protokoll 0": grosse Zahl und Beschriftung stehen auf
  einer Zeile und lesen sich als „vierzehn geprüft null". Besser „14" gross, darunter
  „davon geprüft: 0".

### O10 · Nutzlose Symbole

Auf der VSA-Seite steht ein grüner Haken vor „Ergebnis — Noch keine Berechnung"
([VsaPage.xaml:70](../../../src/AuswertungPro.Next.UI/Views/Pages/VsaPage.xaml#L70)).
Ein Haken für „noch nichts" verwirrt; das Symbol sollte dem Zustand folgen.

## D · Zwei Programme in einem — Konsistenz

### O11 · Zwei verschiedene Hauptknöpfe

Der wichtigste Knopf einer Seite ist auf Haltungen, Schächten, Import, Export und
Schattenauswertung eine blaue **Pille** (`ToolbarButtonAccent`), auf Sanierungs-Matrix,
Schacht-Matrix, Druckcenter und Einstellungen ein blaues **Rechteck** (`PrimaryButton`).
„Speichern" sieht auf der Haltungsseite anders aus als auf der Matrix daneben.

| Seite | Pille | Rechteck |
|---|---|---|
| DataPage, ImportPage | 2 | – |
| ExportPage, SchaechtePage, Schattenauswertung, ProjektUebersicht, MediaConflicts | 1 | – |
| SanierungsMatrixPage | – | 2 |
| SchachtSanierungsMatrix, SettingsPage, BuilderPage (Druckcenter) | – | 1 |
| OverviewPage (klassisch) | 1 | 1 |

Dazu die **Projektseite**: drei gleich aussehende graue Knöpfe, keiner ist Hauptaktion —
und „Programm schliessen" steht direkt neben „Projekt speichern"
([ProjectPage.xaml:28-35](../../../src/AuswertungPro.Next.UI/Views/Pages/ProjectPage.xaml#L28-L35)).
Beenden gehört ins Menü „Datei", nicht neben Speichern.

### O12 · Import: zwei Hauptknöpfe, umgebrochene Leiste

„Import Kanalfernseh-Projekt" **und** „Import PDF" sind beide blau
([ImportPage.xaml:23, 45](../../../src/AuswertungPro.Next.UI/Views/Pages/ImportPage.xaml#L23)) —
auf der Haltungsseite gilt seit Etappe 1 bewusst „eine Hauptaktion". Die Leiste bricht
um, „Fotos zuordnen" steht allein in der zweiten Zeile, und „Manuell:" hängt als kleines
Wort zwischen den Knöpfen. Der Katalogpfad wird in voller Länge angezeigt (zwei
Windows-Pfade in einer Zeile) — das ist ein Fall für Kürzung mit Tooltip.

Vorschlag: ein Hauptknopf, der Rest unter „Weitere Quellen" (den Aufklapper gibt es
schon), Pfad gekürzt.

### O13 · Druckcenter-Tabelle ist die alte Tabelle

Kleinbuchstaben im Kopf, dünne Linien, Zustand als nackte Ziffer, „(unbekannt)" in
Klammern, „Straße" mit ß — neben den Nova-Tabellen der Haltungen sieht das aus wie ein
anderes Programm ([BuilderPage.xaml:399](../../../src/AuswertungPro.Next.UI/Views/Pages/BuilderPage.xaml#L399)).
Grossschrift im Kopf gilt laut Etappe 2b programmweit; hier fehlt sie. Die
Zustandsklassen-Marke aus `ZustandsklasseChipColumnFactory` liesse sich direkt verwenden.

### O14 · Abgeschnittene Spaltenköpfe in „Kompakt"

„LICHTE…", „HALTUN…", „ZUSTAN…" bei Haltungen, „Kontrollscha…" bei Schächten. Die
Startbreiten stammen aus dem Prototyp mit Kleinbuchstaben; die Grossschrift ist rund ein
Fünftel breiter. Entweder die Startbreiten in `NovaSpaltenbreiten` am Grossschrift-Kopf
messen oder dem Kopf einen Tooltip mit dem vollen Namen geben (die Zellen haben den
Volltext-Hinweis schon, der Kopf nicht).

### O15 · Die Zustandsklasse hat drei Gesichter

In der Tabelle eine Marke „Z0" auf Rot, im Formular eine nackte „0" im Dropdown, in der
Schattenauswertung eine gelbe „2" als Text. Ein Nutzer, der die Marke kennt, sucht sie an
den anderen zwei Orten vergeblich.

## E · Kontrast und Dunkelmodus

### O16 · Gelbe Ziffer auf Weiss

Die Schattenauswertung färbt die Zustandsklasse als **Textfarbe** mit der
Hintergrundpalette der Marken
([SchattenauswertungPage.xaml:126](../../../src/AuswertungPro.Next.UI/Views/Pages/SchattenauswertungPage.xaml#L126),
`ZustandsklasseToBrushConverter` → `ZustandsklasseColorPalette.TryGetBackground`). Die „2"
ist damit Gelb auf Weiss — weit unter 4,5:1. Genau dafür gibt es seit Etappe 1 die
`ZustandsklasseInkPolicy`; hier ist sie nicht angebunden. Am einfachsten: dieselbe Marke
wie in der Haltungstabelle.

### O17 · Abzeichen im Dunkelmodus kaum lesbar

„fachlich geprüft" grün auf dunkelgrün, „nicht analysiert" grau auf grau
(`tabellen/Dark-Haltungen-standard.png`, Spalte Prüfung). Im hellen Thema stimmt der
Kontrast; im dunklen fehlen eigene Töne für den Abzeichen-Hintergrund. Betroffen ist die
Prüfung-Spalte der Statusspalten (Nova 2b). Der Kontrastwächter `DesignAuditContrastTests`
misst Text auf Karte, nicht Text auf Abzeichen — deshalb blieb es unentdeckt.

### O18 · Filterchip „2" im Dunkelmodus

Der gelbe Zustandsklassen-Filterchip trägt im Dunkelmodus helle Schrift
(`Dark-Haltungen-1280x720.png`, Filterzeile). `FilterChipBar.xaml` bindet die Tinte per
`TemplateBinding Foreground`, also die Normaltinte des Themas — hell auf Gelb. Die Marken
in der Tabelle lösen das über `ZustandsklasseInkConverter`; der Chip sollte denselben
Konverter nehmen.

### O19 · Einstellungen im Dunkelmodus: dicke Doppelrahmen

Die drei Gruppen „Darstellung und Diagnose", „Speichern", „Haltungsprotokoll (PDF)"
tragen im Dunkelmodus einen breiten hellen Doppelrahmen mit dem Titel im Rahmen — die
klassische WPF-`GroupBox`. Im hellen Thema ist es eine feine Linie. Beides passt nicht zur
Kartenoptik der übrigen Seiten; die Gruppen wären als `Card` mit Titelzeile stimmig.

## F · Kleineres, gesammelt

- **Dreimal derselbe Name.** „Geladen: projekt.json" steht unter dem Logo **und** in der
  Statuszeile; der Projektname steht in der Brotkrume, unten in der Leiste **und** rechts
  in der Statuszeile. Einmal reicht.
- **Eingabefelder zu flach.** Bei 1920 × 1080 mit offener Schublade sind je Thema
  anderthalb Felder sichtbar (`tabellen/Light-Haltungen-standard.png`); der Anteil ist
  fest 36 % (`DataPageWorkspaceLayoutPolicy.Anteil`). Die Schublade scrollt zwar, aber
  der Nutzer sieht je Karte nur ein Feld und einen Anschnitt.
- **Rechte Übersicht kürzt technische Texte.** „Verschobene Rohrverbin…" und „Stufe 3 ·
  KI-Vorschlag, Konfidenz 0.72 (Modells…" — eine Konfidenz mit zwei Nachkommastellen
  gehört nicht in die Kurzansicht; „Stufe 3 · KI-Vorschlag" reicht, der Rest in den
  Tooltip.
- **Schacht-Formular beginnt mit „NR."** — der laufenden Nummer — vor der Schachtnummer.
  Das ist R7 aus dem Morgenbericht in Reinform.
- **Sanierungs-Matrix:** die gewählte Zeile trägt an jeder Zelle einen dicken blauen
  Rahmen; „DN" steht fett in Monospace, „Länge m" daneben normal; jede Zeile zeigt ein
  „— keine —"-Dropdown, auch ohne Auswahl. Ruhiger: Zeilenmarkierung statt Zellrahmen,
  eine Schrift, Dropdown erst bei Auswahl oder Mausberührung.
- **Export:** die orange Zeile „Vorhandene Kennungen werden beibehalten …" liest sich
  wie eine Warnung, ist aber eine Erklärung. Warnfarbe nur für Warnungen.
- **Schattenauswertung:** die grauen Punkte in „Vergleich" haben keine Legende.
- **1280 × 720:** die Leiste schneidet „Schattenauswertung" an und versteckt VSA,
  Diagnose und Einstellungen hinter dem Fussbereich, ohne sichtbaren Bildlauf. Unter Full
  HD, deshalb nachrangig — aber ein Laptop im Feld hat genau das.

## Was ich nicht geprüft habe

- Nichts am laufenden Programm; nur Fotos aus dem isolierten Prüfhost mit künstlichem
  Projekt. Echte Projekte mit langen Strassennamen oder 500 Haltungen sehen anders aus.
- Player, Codierfenster, Fotomessung, Training Center, Dialoge — davon gibt es keine
  Fotos aus dem Morgenlauf.
- Windows-Skalierung 125/150 % (bekanntermassen offen).
- Bewegung und Übergänge — auf Standbildern nicht sichtbar.

## Reihenfolge, wenn es schnell gehen soll

1. **O1, O2, O16** — drei Sichtfehler, zusammen unter einer Stunde, alle drei mit Wächter.
2. **O4, O5, O6** — Schreibweise und Entwicklertexte: mechanisch, Wächter erweitern.
3. **O7, O10** — Platzhalter und der falsche Haken: Minuten.
4. **O11, O12** — ein Hauptknopf je Seite, Projektseite aufräumen.
5. **O17, O18, O19** — Dunkelmodus in einem Rutsch.
6. **O8, O9, O13, O14, O15** — die grösseren Angleichungen, je ein Nachmittag.
