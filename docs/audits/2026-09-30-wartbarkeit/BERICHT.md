# SewerStudio – Code-Audit Wartbarkeit (30.09.2026)

Stand: Commit `3211155cd` auf `feature/webgis-uebertragung` (nach dem Merge des Optik-Umbaus, PR #25).
Vergleichsbasis: die [Wartbarkeitsanalyse vom 27.09.2026](../2026-09-27-wartbarkeit/BERICHT.md)
(Befunde W01–W14, Plan AP01–AP11). Seither: 93 Commits, rund 38'500 neue und 8'900 entfernte Zeilen
in `src` und `tests`.

Teilberichte mit allen Belegen: [Oberfläche und Theme](teilberichte/oberflaeche-theme.md),
[WebGIS](teilberichte/webgis.md), [Tests und Prüfweg](teilberichte/tests-pruefweg.md).
Messwerte: [nachweise/messwerte.json](nachweise/messwerte.json).

Dieses Audit hat keinen Produktcode verändert.

## 1. Kurzfazit

**Die Architektur ist gesund und wird es bleiben, wenn vier Querschnittsprobleme jetzt angegangen werden.**
Die Schichten sind sauber (keine WPF-Verweise in Domain, Application oder Infrastructure), der
eingefrorene Bereich `UI/Ai` wächst nicht mehr, und die neuen Bausteine des Optik-Umbaus
(Rückgängig-Verlauf, Nova-Dialog, `UserError`) sind klein und an einer Stelle verdrahtet. Das
WebGIS-Teilsystem ist trotz einer Woche Bauzeit gut geschnitten.

Die Wartbarkeit wird heute aber nicht mehr hauptsächlich von einzelnen langen Methoden gebremst,
sondern von vier Dingen, die bei **jeder** Änderung mitwirken:

1. **Die Projektanleitung `CLAUDE.md` ist zu gross geworden.** 6'756 Zeilen, 548 KB, geschätzt
   rund 154'000 Tokens. In drei Tagen kamen 1'500 Zeilen dazu. Sie wird in jede KI-Sitzung geladen,
   mischt geltende Regeln mit Arbeitsprotokollen und widerspricht sich (Z1).
2. **Die Grössenwächter werden aufgefüllt statt eingehalten.** Code wächst bis knapp unter
   1'000 Zeilen je Datei oder 2'000 je Klasse und wandert dann in eine weitere Teildatei. Die
   Verantwortung der Klasse wird dadurch nicht kleiner (Z2).
3. **Dieselbe Sache wird an mehreren Stellen gepflegt.** Beide Theme-Dateien enthalten 18 von 21
   benannten Stilen doppelt. Die Haltungsidentität ist in Python fünfmal leicht verschieden
   umgesetzt. WebGIS-Kennungen stehen ausserhalb ihrer Karten (Z3, Z5, Z6).
4. **Die Testwächter werden selbst zur Wartungslast.** Rund 270 Testdateien lesen Quelltext oder
   XAML als Text, 14 Listen werden von Hand gepflegt, Hilfsfunktionen sind mehrfach kopiert, und ein
   schneller Testlauf für den Alltag fehlt (Z4, Z7).

Keiner dieser Punkte ist ein nachgewiesener Laufzeitfehler. Es sind Wartungsprioritäten: Sie
bestimmen, wie viel man bei einer Änderung suchen, nachziehen und prüfen muss.

**Empfehlung:** Zuerst die vier Querschnittsprobleme in einer kurzen ersten Welle von 4–7
Arbeitstagen beheben (Abschnitt 5). Erst danach die noch offenen grossen Abläufe aus dem Plan vom
27.09. einzeln angehen. Eine Arbeit an den grossen Abläufen ohne diese erste Welle würde weiterhin in
Teildateien, doppelten Listen und einer weiter wachsenden Anleitung enden.

## 2. Was seit dem 27.09. besser wurde

| Bereich | 27.09. | 30.09. | Einordnung |
| --- | ---: | ---: | --- |
| Dateien über 1'000 Zeilen | 2 | 0 | AP01 erledigt |
| Import, Hauptablauf `ProjectImportOrchestrator.Import` | 490 Z. | 401 Z. | AP07: Medienphase ausgelagert |
| Verteilung `ParsedHoldingDistributionController.Distribute` | 457 Z., 17 Parameter | 362 Z., 5 Parameter | AP07: Parameter gruppiert |
| Mehrmodell-Analyse `AnalyzeAsync` | 825 Z. | 782 Z. | AP01 ein Schritt, AP05 offen |
| Trainingsskript-Tests in der CI | nein | ja | AP02 |
| Preis- und Klassenkartenregel doppelt | ja | nein | AP03 |
| Produktabdeckung getrennt messbar | nein | ja | AP04 |
| Exakt gleiche Methodenrümpfe | 12 Gruppen | 11 Gruppen | |
| Dateien unter `UI/Ai` | 599 | 599 | Einfrierung wirkt |

Unverändert oder leicht gewachsen: Methoden über 100 Zeilen (191 → 198), leere `catch`-Blöcke
(232 → 237), `async void` (30 → 32), Application-Dateien mit direktem Dateizugriff (34 → 37).

**Stand des Plans vom 27.09.:** AP01–AP04 und AP07 erledigt. Offen bleiben AP05 (Mehrmodell-Analyse),
AP06 (Goldsample-Speichern), AP08 (XTF-Parser), AP09 (Player und Diensterzeugung),
AP10 (Python-Negativsatzprüfung) und AP11 (Architekturübersicht). AP11 ist inzwischen der
dringendste davon, siehe Z1.

## 3. Befunde

P1 = bremst sichere Änderungen jetzt spürbar. P2 = gezielt verbessern. P3 = bei Gelegenheit.

| ID | Prio | Befund | Teilbericht |
| --- | --- | --- | --- |
| Z1 | P1 | `CLAUDE.md` ist zu gross, mischt Regeln und Protokolle und widerspricht sich | – |
| Z2 | P1 | Grössenwächter werden aufgefüllt; Wachstum wandert in weitere Teildateien | U06 |
| Z3 | P1 | Hell- und Dunkel-Theme pflegen 18 von 21 Stilen doppelt | U01 |
| Z4 | P1 | Text- und XAML-Wächter: 14 Handlisten, kopierte Hilfen, ungetesteter 917-Zeilen-Parser | T01–T03, T06 |
| Z5 | P2 | Haltungsidentität mehrfach und verschieden umgesetzt (Python 5×, C# mehrere Normalisierer) | – |
| Z6 | P2 | WebGIS: Kennungen ausserhalb der Karten, gleichnamige gegenläufige Regel, Zustand nur aus Flags | WG-A, WG-B, WG-D |
| Z7 | P2 | Kein schneller Testlauf; Kindprozess-Tests teuer und anfällig | T04, T05 |
| Z8 | P2 | Rückgängig-Verlauf: doppelte Seitenhüllen und 19 verstreute Sperraufrufe | U04, U05 |
| Z9 | P2 | Neuer Dienst: harter Zählertest (177) erzeugt Konflikte zwischen Zweigen | U08 |
| Z10 | P2 | Tastenkürzel-Fenster kopiert Tasten von Hand; Handbuch hängt an Seitennamen | U02, U03 |
| Z11 | P3 | Python-Hilfsfunktionen vielfach kopiert (58× SHA-256, 25 Dateien atomar schreiben) | – |
| Z12 | P3 | Kleinere Reste: WebGIS-Ablauf im ViewModel, lokale Knopfstile, 16 Planungsdateien im Stammordner | WG-E, U07 |

### Z1 (P1) – Die Projektanleitung ist zum Wartungsproblem geworden

**Beleg.** `CLAUDE.md`: 6'756 Zeilen, 548'074 Bytes (27.09.: 5'260 Zeilen, 411'362 Bytes).
Geschätzt rund 154'000 Tokens (Zeichen / 3,5). Die grössten Abschnitte sind Arbeitsprotokolle:
«Optik und Bedienung professionell» mit 129'000 Zeichen, «Wichtige Klassen» mit 79'000,
«Plan-gesteuerter YOLO-Export» mit 45'000 und «Gesamtaudit 2026-08-14» mit 43'000.

**Widersprüche.** Die Zahl der registrierten Dienste steht als geltende Aussage mit 141, 159, 167, 168,
169, 171, 175, 176 und 177 in der Datei. Der Test erwartet 177. «`PipelineResultPresenter` …
höchstens 250 sichtbare Befunde» steht neben «`Take(250)` ist entfernt». Drei Absätze stehen doppelt
wörtlich in der Datei, zum Beispiel die `ShaftPdfRelevance`-Regel und die BCC-Archivmessung.
Zwei Abschnitte sind ausdrücklich «überholt», stehen aber weiter vollständig da.

**Warum es bremst.** SewerStudio wird überwiegend mit KI-Unterstützung weiterentwickelt. Jede
Sitzung liest diese Datei zuerst. Je grösser sie wird, desto weniger Platz bleibt für den
eigentlichen Code, und desto eher folgt eine Sitzung einer veralteten Zahl oder Regel. Die Datei
wächst ausserdem mit jeder Aufgabe um Dutzende bis Hunderte Zeilen. Das ist heute der grösste
einzelne Hebel für die Wartbarkeit.

**Empfehlung.**
1. `CLAUDE.md` auf das Verbindliche kürzen, Ziel unter 400 Zeilen: Zweck, Schichten und ihre Regeln,
   Architekturprinzipien, Checkliste für neue Dienste, Build- und Testweg, harte Verbote (Originale,
   VRAM, Qualitätsampel, geschützte Bereiche) und eine Verweistabelle.
2. Fachregeln je Bereich in `docs/architektur/<bereich>.md` verschieben, zum Beispiel Import,
   XTF/DSS, WebGIS, Dossiers, KI-Pipeline, Training, Oberfläche und Sicherung. Dort stehen nur
   **geltende** Regeln mit ihrer Begründung, ohne Datums-Erzählung.
3. Arbeitsprotokolle («Fix-Runde 2 …») und Messverläufe unverändert nach
   `docs/verlauf/` oder zu den bestehenden Audit-Ordnern verschieben. Sie bleiben als Beleg erhalten.
4. Zahlen, die sich ändern (Dienste, Tests, Klassen), nicht mehr ausschreiben, sondern auf den
   Test verweisen, der sie festhält.
5. Neue Regel in der gekürzten Datei: Neue Erkenntnisse kommen in die passende Bereichsdatei,
   nicht in `CLAUDE.md`.

**Abnahme.** `CLAUDE.md` unter 400 Zeilen. Jede heute darin stehende **Regel** ist entweder dort oder
in genau einer Bereichsdatei auffindbar; das wird mit einer Liste der Abschnitte abgehakt. Keine
Zahl steht mehr in zwei Fassungen. Die Umstellung braucht deine Freigabe, weil sie die Arbeitsweise
aller Sitzungen ändert.

### Z2 (P1) – Grössenwächter werden aufgefüllt statt eingehalten

**Beleg.** 10 Produktdateien liegen zwischen 975 und 1'000 Zeilen, am 27.09. waren es 8.
`SchaechtePage.xaml.cs` und `TrainingStudioViewModel.cs` stehen exakt auf 1'000, `ShellViewModel.cs`
auf 999. Bei den Teilklassen (Grenze 2'000) liegen 8 zwischen 1'800 und 2'000, zum Beispiel
`DossierPreviewFieldPanel` 1'999, `DataPage` 1'978, `BuilderPageViewModel` 1'964 und
`TrainingStudioViewModel` 1'951. Die Zahl der Teildateien stieg in drei Tagen von 219 auf 228.
Beispiele aus dieser Zeit:

| Klasse | 27.09. | 30.09. |
| --- | --- | --- |
| `ShellViewModel` | 1'583 Z. in 4 Dateien | 1'783 Z. in 5 Dateien |
| `ExportPageViewModel` | 1'793 Z. in 6 Dateien | 1'889 Z. in 8 Dateien |
| `DataPage` | 1'898 Z. in 10 Dateien | 1'978 Z. in 11 Dateien |
| `HoldingFolderDistributor` | 2'888 Z. | 2'981 Z. (Ausnahme erlaubt bis 3'064) |

Beim Rückgängig-Umbau blieb `SchaechtePage.xaml.cs` bei 1'000 Zeilen. Die neue Logik ging in
`SchaechtePage.Verlauf.cs` (siehe Teilbericht U06). Nicht jede Aufteilung ist schlecht:
`DossierPreviewFieldPanel` und `ShellViewModel` sind nach echten Fachbereichen getrennt.

**Warum es bremst.** Die Wächter messen Grösse, nicht Verantwortung. Eine Klasse mit elf Teildateien
ist nicht leichter zu verstehen als eine grosse Datei, man muss sie nur an mehr Stellen suchen. Und
die Ausnahme für `HoldingFolderDistributor` erlaubt weiteres Wachstum bis zum eingetragenen Wert.

**Empfehlung.**
1. Einen Sperrklinken-Wächter einführen: Für die rund 25 Dateien über 900 Zeilen und die rund 10
   Teilklassen über 1'500 Zeilen wird der heutige Stand eingefroren, sie dürfen nur noch schrumpfen.
   Neue Dateien und Klassen behalten die bestehenden Grenzen.
2. Die Ausnahmewerte in `MaintainabilityFitnessTests` auf den heutigen Stand senken:
   `HoldingFolderDistributor` 2'981 statt 3'064, `PlayerWindow` auf den gemessenen Wert.
3. Eine neue Teildatei für eine bestehende grosse Klasse braucht einen fachlichen Namen und keinen
   technischen wie `.Verlauf2` oder `.Rest`. Das gehört in die Checkliste für Prüfungen.

**Abnahme.** Der Wächter wird rot, wenn eine eingefrorene Datei oder Klasse auch nur um eine Zeile
wächst. Eine Sabotageprobe mit einer Zeile mehr in `SchaechtePage.xaml.cs` belegt das. Die
bestehenden Grenzen bleiben unverändert.

### Z3 (P1) – Stile beider Themes doppelt gepflegt

**Beleg.** `Theme/Theme.xaml` (1'252 Zeilen) und `Theme/ThemeLight.xaml` (1'207 Zeilen). Nachgezählt:
Von 21 Stilen und Vorlagen mit Schlüssel sind **18 nach Entfernen der Kommentare und Leerzeichen
identisch**, zusammen rund 11'500 Zeichen. Es sind zum Beispiel `PrimaryButton`,
`FilledButtonTemplate`, `CompactButton` und `ToolbarButtonAccent`. Nur `Card`, `IconButton` und
`NavItemStyle` weichen ab. Der Teilbericht zählte 17, weil er Leerraum anders behandelte.
Kommentare in `ThemeLight.xaml` verweisen auf «siehe Kommentar in Theme.xaml». Die Fix-Runden 2–4
der Aufgabe 13 mussten jeweils beide Dateien gleich ändern.

**Warum es bremst.** Die Farben sind je Theme verschieden, die Formen nicht. Wer einen Knopfstil
ändert, muss zwei Dateien treffen. Vergisst er eine, sieht man den Fehler nur im anderen Theme.
Genau diese Fehlerart hat der Hochkontrast-Umbau mehrfach gezeigt.

**Empfehlung.** Die 18 gleichen Stile einmal nach `Theme/Controls.xaml` verschieben, wo `DangerButton`
und `WarningButton` schon liegen. `Theme.xaml` und `ThemeLight.xaml` behalten nur Farbtokens und die
drei echten Abweichungen. Dazu kommt ein kleiner Test, dass beide Themes dieselben Token-Schlüssel
definieren. Zu beachten ist die Reihenfolge der Ressourcen in `App.xaml` und in
`ThemeManager`. Ein Stil in `Controls.xaml` muss seine Farben per `DynamicResource` beziehen, was
der bestehende Wächter bereits verlangt.

**Abnahme.** Jeder der 18 Stile steht genau einmal im Repo. Kontrast-, Hochkontrast-Farbpaar- und
`FuellknopfFarbenIsolatedSmokeTests` bleiben unverändert grün. Eine Sichtprobe hell und dunkel zeigt
keine Änderung.

### Z4 (P1) – Die Testwächter brauchen selbst Ordnung

**Beleg** (Teilbericht T01–T03, T06, nachgeprüft):
- Rund 260–290 Testdateien lesen Quelltext oder XAML als Text. Von 77 seit dem 27.09. neuen
  Testdateien gehören bis zu 23 dazu; 30 `DesignAudit*`-Dateien umfassen 7'831 Zeilen.
- 14 von Hand gepflegte Wort-, Ausnahme- und Positivlisten in 13 Dateien. Die grösste,
  `Aufgabe10c1BereinigteWoerter`, hat rund 165 Wörter. `GeschuetzteGanzeZeichenketten` steht fast
  gleich in zwei Dateien. Wer einen sichtbaren Text ändert, pflegt oft zwei bis fünf Listen.
- `XamlFarbpaarModell.cs` hat 917 Zeilen und ist ein eigener XAML-Zustandsparser, aber ohne eigene
  Testdatei. Nachgezählt: nur zwei Dateien verweisen darauf.
- Die Hilfen sind kopiert: «alle XAML-Dateien auflisten» in mindestens 8 Dateien,
  jeweils mit eigenen Ausschlüssen, die Baumsuche `Nachfahren` in mindestens 5 Dateien. Die Suche
  nach der Repo-Wurzel über `"AuswertungPro.sln"` steht in 15 Dateien, obwohl `TestRepoPaths` in
  weiteren 15 Dateien dafür verwendet wird.

**Warum es bremst.** Die Wächter schützen wertvolle Regeln wie Kontrast, Knopfregel und Sprache.
Ihre Kosten wachsen aber mit jeder neuen Regel, und die Aufgabe 10c1 zeigt die Fehlerart: Ein
Bereinigungsskript korrigierte sein eigenes Prüfmuster weg, und Wortlisten trafen Datenschlüssel.

**Empfehlung.**
1. Gemeinsame Testhilfen je Testprojekt: `TestXaml.Alle(ausgenommen)`, `TestXaml.Laden`,
   `WpfTestHilfe.Nachfahren`, `WpfTestHilfe.WarteAufLayout` und `TestRepoPaths` überall.
   Die kopierten Fassungen fallen weg.
2. Die Sprachwächter in eine Datei mit **einer** Wort- und **einer** Ausnahmeliste zusammenführen.
   Kein Wert darf in zwei Listen stehen.
3. `XamlFarbpaarModell` bekommt 5–8 eigene Tests mit kleinen XAML-Beispielen.
4. Regel für neue Wächter: nur wenn die Regel nicht als Verhaltens- oder typisierter Test prüfbar ist.

**Abnahme.** Je Testprojekt gibt es genau eine Umsetzung für XAML-Auflistung, Baumsuche und
Repo-Wurzel. Die Zahl der Handlisten sinkt von 14 auf höchstens 8, und kein Wert steht doppelt. Alle
Wächter bleiben grün. Die Sabotageproben aus CLAUDE.md schlagen weiterhin an.

### Z5 (P2) – Die Haltungsidentität hat keinen einzigen Besitzer

**Beleg.** Die Regel «dieselbe physische Haltung, auch in Gegenrichtung» schützt die Trennung von
Trainings- und Prüfdaten. In Python ist sie fünfmal einzeln umgesetzt:
`training/scripts/bcc_release_holdout.py:333`, `gold_stock_audit.py:672`,
`prepare_detect_gold.py:323`, `tools/EvalVisibilityReview/bcc_release_holdout_review_server.py:1294`
und `detect_release_holdout_review_server.py:1029`. Die Fassungen unterscheiden sich: Drei
normalisieren vorher und vergleichen ohne Gross/Klein, die in `detect_release_holdout_review_server.py`
tut beides nicht. In C# delegieren die meisten Importe an gemeinsame Normalisierer. Davon gibt es
aber mehrere mit verschiedenem Zweck: `HoldingKeyNormalizer`, `HoldingIdNormalizer`,
`EvalContaminationGuard.NormalizeHaltungKey` und `TrainingExportRegistryFileStore.NormalizeStrictHoldingKey`.
`KinsImportService.NormalizeHoldingKey` (Zeile 491) macht eigenständig nur Trim und Grossschreibung.
Ein Kommentar in WinCan behauptet dagegen «einheitlich zu IBAK/KINS».

**Warum es bremst.** Das ist der zentrale Schlüssel des Fachmodells. Eine Änderung, zum Beispiel an
Buchstaben-Präfixen wie `u-80792`, muss heute an mehreren Stellen gleich gemacht werden. Eine
vergessene Stelle lässt Prüfdaten unbemerkt ins Training rutschen. Ein aktueller Fehler ist damit
**nicht** belegt: Bei rein numerischen Namen verhalten sich die Varianten gleich.

**Empfehlung.** In Python ein gemeinsames Modul, zum Beispiel `training/scripts/haltungsidentitaet.py`,
das alle fünf Stellen verwenden. Dazu eine kleine gemeinsame Beispieldatei mit Namen und erwartetem
Schlüssel, die ein Python- **und** ein C#-Test lesen. In C# die vier Normalisierer im Kopfkommentar
nach Zweck abgrenzen. KINS prüfen und entweder angleichen oder die Abweichung begründen.

**Abnahme.** In Python gibt es genau eine Definition. Die Beispieldatei ist in beiden Sprachen grün,
darunter Gegenrichtung, Buchstaben-Präfix und Leerzeichen.

### Z6 (P2) – WebGIS: drei gezielte Aufräumarbeiten

Belege im [Teilbericht WebGIS](teilberichte/webgis.md). Nachgeprüft:
- **WG-A: Kennungen ausserhalb der Karten.** `WebGisImportAktenfelder.cs` enthält 27
  refId-Literale. `WebGisImportPlanBuilder.cs:183-184` wiederholt Breite und Höhe aus
  `WebGisHandwertKarte.cs:45-47`, und `WebGisGeschuetzteFelder.cs:29` enthält ein weiteres. Die Höhe
  `d06f8d1f` gilt laut Dokumentation als ungeklärt, ist im Code aber nicht so markiert.
  **Abnahme:** Jede refId steht genau einmal mit dem Etikett «live geprüft» oder «offen».
- **WG-B: `WebGisFuehrt` gibt es mit gegenläufiger Bedeutung.** Beim Senden in
  `WebGisHandwertKarte.cs:95` heisst sie «nie schreiben» und gilt für Eigentümer und Betreiber. Beim
  Holen in `WebGisImportUseCase.cs:486` heisst sie «WebGIS überschreibt sogar die Handeingabe» und
  gilt nur für den Eigentümer. Dazu kommt `FuehrtWebGis` in `WebGisImportPlanBuilder.cs:245`.
  **Abnahme:** zwei klar benannte Regeln (`NieSenden`, `HolenUeberschreibtHand`) an einer Stelle,
  getestet mit Eigentümer **und** Betreiber.
- **WG-D: Zustand einer Position nur aus Flags.** Der Zustand ergibt sich aus der Kombination von
  `Geschrieben`, `VomServerBestaetigt`, `Nachgeprueft`, `SchreibFehler` und `Ungeklaert`.
  Übersicht, Bericht und ViewModel leiten ihn jeweils selbst ab. **Abnahme:** Eine berechnete
  Eigenschaft `Ausgang` im Modell, und kein Leser kombiniert die Flags mehr selbst.

Das WebGIS wird auf diesem Zweig weiterhin aktiv entwickelt, und der Schreibweg ist live noch nicht
voll abgenommen. Diese Aufräumarbeiten sollten deshalb mit der laufenden WebGIS-Arbeit abgestimmt
und nicht nebenbei gemacht werden.

### Z7 (P2) – Testlauf: kein Alltagsweg, teure Kindprozesse

**Beleg.** Pre-Push-Hook und CI führen dieselben vier Projekte aus, das ist gut. Die reine Testzeit
betrug am 27.09. rund 10 Minuten, dazu kommt der Build. Es gibt kaum Filter (5 `Trait`-Vorkommen).
45 `[IsolatedWpfFact]`-Tests starten je einen eigenen Testprozess mit 60-Sekunden-Grenze. Die
bekannte `ApplicationIdle`-Falle steht nur als Text in CLAUDE.md, obwohl es 23 Vorkommen in
`tests/` gibt. In dieser Sitzung kam der zeitweise Absturz am Ende des WPF-Testthreads dazu. Seine
Ursache ist mit Absturzabzügen belegt, die Behebung in `StaTestRunner` ist noch nicht eingecheckt.

**Empfehlung.** Traits `Kategorie=Wächter` und `Kategorie=Kindprozess` einführen;
`IsolatedWpfFact` setzt den Trait selbst. Ein dokumentierter Schnelllauf
`dotnet test --filter "Kategorie!=Kindprozess"` mit Ziel unter 3 Minuten. Hook und CI bleiben
vollständig. Ein Wächter meldet `ApplicationIdle` in Kindprozess-Tests. Den `StaTestRunner`-Fix
einchecken.

**Abnahme.** Der Schnelllauf ist dokumentiert und gemessen. Der volle Lauf bleibt unverändert grün.

### Z8 (P2) – Rückgängig-Verlauf: gut gebaut, aber doppelt angebunden

**Beleg.** Der Baustein in Application ist klein und klar (479 + 174 + 80 Zeilen). Die Hüllen
`DataPage.Verlauf.cs` und `SchaechtePage.Verlauf.cs` sind fast Zeile für Zeile gleich. Neben jedem
neuen Handlernamen existiert der alte weiter; wer den alten wieder bindet, verliert den Verlauf still.
Die Sperre bei externen Übernahmen steht an 19 Stellen in 11 Dateien. Jeder künftige neue Schreibweg
braucht sie von Hand.

**Empfehlung.** Eine gemeinsame kleine Anbindung für Tabellenzellen und Auswahlfelder, die beide
Seiten aufrufen. Ein XAML-Test, dass die alten Handler nicht mehr direkt gebunden sind. Im
Kopfkommentar von `IDatenaenderungsVerlauf` stehen die Regel «neuer externer Schreibweg →
Sperre» und die heutige Liste.

**Abnahme.** Beide Hüllen haben je unter 30 Zeilen. Der XAML-Test ist rot, wenn ein alter Handler
gebunden wird.

### Z9 (P2) – Neuer Dienst: Zählertest erzeugt Konflikte

**Beleg.** `ServiceProvider` umfasst 19 Dateien mit 1'819 Zeilen. Ein neuer Dienst braucht eine
Eigenschaft, einen Eintrag in der Registrierungskarte und die Zahl in
`ServiceProviderRegistrationTests` (heute 177). An dieser Zahl kollidieren parallele Zweige
regelmässig. Das zeigen die Einträge 175 → 176 → 177 aus zwei Zweigen.

**Empfehlung.** Den Test von «Anzahl = N» auf «jede öffentliche Dienst-Eigenschaft mit
Schnittstellentyp steht in der Karte» umstellen. W07 und AP09 bleiben für den grösseren Umbau.

**Abnahme.** Ein neuer Dienst braucht keine Zahlenänderung im Test. Ein vergessener Karteneintrag
macht ihn rot.

### Z10 (P2) – Von Hand kopierte Oberflächentexte

**Beleg.** `TastenkuerzelWindow.xaml.cs:59-76` schreibt die globalen Tasten als Text. Die echten
`KeyBinding` stehen in `MainWindow.xaml:15-30`. Eine neue Taste ohne Listeneintrag fällt keinem Test
auf. `HandbuchInhalt.cs` wurde in drei Tagen zehnmal nur als Nebenwirkung anderer Aufgaben geändert.

**Empfehlung.** Ein Test liest die `KeyBinding` aus `MainWindow.xaml` und verlangt, dass jede Taste
im Fenster vorkommt. Handbuch: In der Aufgaben-Checkliste «Handbuch mitprüfen» ergänzen; eine
Auslagerung in Textdateien bleibt optional.

**Abnahme.** Eine Sabotageprobe mit einem zusätzlichen `KeyBinding` macht den Test rot.

### Z11 (P3) – Python: Hilfsfunktionen vielfach kopiert

**Beleg.** Unter `training/scripts` und `tools` definieren 58 Dateien eine eigene SHA-256-Funktion.
25 Dateien bauen atomares Schreiben (`os.replace`) selbst, 8 lesen die Klassenkarte selbst.
Die Review-Server unter `tools/EvalVisibilityReview` umfassen 12'249 Zeilen einschliesslich Tests.
Nur 9 teilen sich `review_server_security.py`. Die grössten Skripte haben 2'917
(`gold_stock_audit.py`, AP10) und 2'832 Zeilen (`bcc_release_holdout.py`).

**Empfehlung.** Ein kleines gemeinsames Modul für Prüfsumme, atomares Schreiben, Klassenkarte und
Haltungsidentität (siehe Z5). Bei jeder Arbeit an einem Skript wird die lokale Kopie ersetzt,
nicht alles auf einmal.

**Abnahme.** Neue Skripte verwenden das Modul. Die Zahl der eigenen SHA-256-Funktionen sinkt mit
jeder Berührung.

### Z12 (P3) – Kleinere Reste

- `ExportWebGisBereich.SchreibeAsync` hat 121 Zeilen, und die Zuordnung von Ausgang zu
  Meldungstext liegt im ViewModel (WG-E). Sie gehört nach `WebGisSendenAblauf`.
- `NovaDialogDangerButton` ist laut `Controls.xaml` optisch identisch mit `DangerButton` (U07).
- Im Stammordner liegen 16 Markdown-Dateien, darunter mehrere alte Pläne wie
  `MAINTAINABILITY_PLAN.md`, `PRODUKTIONSREIFE_VERBESSERUNGSPLAN.md` und
  `WEBGIS-CODEOPTIMIERUNGEN-PLAN.md`. Sie gehören mit Z1 nach `docs/`.
- Die 237 leeren `catch`-Blöcke bleiben eine Kandidatenliste, kein Befund. Die Regel vom 27.09.
  gilt weiter: Bei jeder Berührung eines kritischen Ablaufs wird entschieden, ob der Fehler
  weitergereicht, als Warnung gemeldet oder als Sperre zurückgegeben wird.

## 4. Was erhalten bleiben soll

- **Schichtung:** Domain, Application und Infrastructure verweisen nicht auf WPF. Neue Abläufe
  entstehen in `Application/UseCases`, `UI/Ai` bleibt eingefroren.
- **Querschnittsbausteine mit genau einem Einstieg:** `NovaDialog`/`DialogService` (Wächter gegen
  `MessageBox.Show`), `UserError` mit Sprachwächter, `DatenaenderungsVerlauf`.
- **WebGIS:** Die Planer sind rein, die Faltung läuft wirklich über eine Funktion, und der
  Massnahmen-Vergleich ist eine einzige Regel.
- **Prüfweg:** Hook und CI sind gleich. Übersprungene Tests sind namentlich begründet, und verwaiste
  Ausnahmen machen Wächter rot.
- **Plan vom 27.09.:** Seine Pakete AP05–AP10 bleiben richtig. Sie kommen nach der ersten Welle.

## 5. Umsetzungsplan

Ein Arbeitstag bedeutet 4–6 konzentrierte Stunden inklusive Tests und Dokumentation. Die Spannen
sind Schätzungen, keine Zusagen.

**Welle 1 – Querschnitt (4–7 Tage).** Diese Welle wirkt auf jede spätere Änderung und sollte zuerst
kommen.

| Paket | Inhalt | Befund | Aufwand |
| --- | --- | --- | ---: |
| Q1 | `CLAUDE.md` kürzen, Bereichsdateien und Verlauf anlegen | Z1, Z12 | 1,5–2,5 Tage |
| Q2 | Sperrklinken-Wächter für grosse Dateien und Klassen, Ausnahmen senken | Z2 | 0,5 Tage |
| Q3 | Gleiche Theme-Stile nach `Controls.xaml` | Z3 | 1–1,5 Tage |
| Q4 | Testhilfen zusammenführen, Sprachlisten vereinen, Farbpaar-Modelltests | Z4 | 1–2 Tage |
| Q5 | Schnelllauf-Traits, `ApplicationIdle`-Wächter, `StaTestRunner`-Fix | Z7 | 0,5 Tage |

**Welle 2 – Regeln an eine Stelle (3–5 Tage).**

| Paket | Inhalt | Befund | Aufwand |
| --- | --- | --- | ---: |
| R1 | Haltungsidentität: Python-Modul und gemeinsame Beispieldatei | Z5, Z11 | 1 Tag |
| R2 | WebGIS: refIds, Führungsregeln, `Ausgang` (mit WebGIS-Arbeit abgestimmt) | Z6, Z12 | 1–2 Tage |
| R3 | Verlauf-Anbindung, Registrierungstest, Tastenkürzel-Test | Z8–Z10 | 1–1,5 Tage |

**Welle 3 – grosse Abläufe (Plan vom 27.09.).** Jeweils nur eines zur gleichen Zeit, in dieser
Reihenfolge nach Änderungshäufigkeit: AP06 Goldsample-Speichern, AP05 Mehrmodell-Analyse,
AP09 Player und Diensterzeugung, AP08 XTF-Parser, AP10 Python-Negativsatz.

Nach Welle 1 werden die Messwerte mit demselben Analysator erneut erhoben
(`docs/audits/2026-09-27-wartbarkeit/nachweise/analysator`). Gleichzeitig wird festgehalten, wie viele
Dateien eine typische kleine Änderung berührt.

## 6. Vorgehen und Grenzen

- Messung: der unveränderte Roslyn-Analysator vom 27.09. auf Commit `3211155cd`. Er hat 6'123
  C#-Dateien und 37'071 Methoden erfasst, ohne Syntaxfehler. Die Vergleichswerte stammen aus den
  Nachweisen vom 27.09.
- Drei unabhängige Prüfungen, die nur gelesen haben: Oberfläche und Theme, WebGIS, Tests und
  Prüfweg. Ihre zentralen Zahlen habe ich nachgemessen: Theme 18 statt 17 gleiche Stile,
  `XamlFarbpaarModell` 917 Zeilen, 45 Kindprozess-Tests, `WebGisFuehrt` dreifach, 27 refIds in
  `WebGisImportAktenfelder`. WG-C, eine zweite Normalisierung im Sanierungskatalog, bleibt im
  Teilbericht als Vermutung.
- Es wurden kein Build und keine Tests für dieses Audit ausgeführt. Laufzeiten stammen vom 27.09.
- Zählungen per Textsuche (Wächterdateien, Python-Kopien) sind Näherungen und im Teilbericht mit
  ihrer Suchregel genannt.
- Kein Befund behauptet einen Laufzeitfehler. Wo eine Abweichung ein Risiko ist (Z5, WG-B), steht
  ausdrücklich, dass kein aktueller Fehler belegt ist.
- Nicht geprüft: Python-Sidecar im Detail, QGIS-Brücke, Dossier-Teilsystem, alle 37'071 Methoden
  von Hand.
