# Übergabe an Codex — Aufklapp-Liste, Haltungs- und Schachtgrafik (08.09.2026)

Stand beim Übergeben: Branch `feature/nova-haltungsliste` im Worktree `C:\Sewer-Studio_KI_4.5-nova`,
HEAD `ed3268616`, Arbeitsbaum sauber, `dotnet build AuswertungPro.sln` 0 Warnungen / 0 Fehler.
Der Hauptbaum `C:\Sewer-Studio_KI_4.5` steht auf `feature/eval-pruefsatz-review` (`cfccda464`) und hat
38 ungesicherte Dateien einer anderen Sitzung (XTF-Arbeit) — **nie committen, nie überschreiben**.

## Was schon fertig ist (nicht erneut bauen)

| Bereich | Stand |
|---|---|
| Haltungen: Aufklapp-Liste als Standardansicht | fertig, 2 Fix-Runden, Review clean |
| Haltungen: Haltungsgrafik des Protokolls in der Übersicht | fertig, 1 Fix-Runde, Review clean |
| Schächte: Schachtgrafik (WinCan-Stil) | fertig, 2 Fix-Runden, Review clean |
| Schächte: Aufklapp-Liste im gleichen Stil | fertig, Fix-Runde 1 gerade committet (`ed3268616`), Nachprüfung offen |

Wichtige Klassen: `Views/Pages/Haltungsansicht/HaltungAufklappListe`, `Views/Pages/Schachtansicht/SchachtAufklappListe`,
`DataPage/DataPageAufklappListeController`, `DataPage/SchaechteAufklappListeController`, `DataPage/HaltungenAnsichtRegel`,
`DataPage/HaltungThemenGruppierung`, `DataPage/HaltungAufklappTastenregel`, `Controls/SvgTeilmengeZeichner` (+ `SvgTeilmengeDefinitionen`, `SvgWert`),
`Application/Reports/HaltungsgrafikSvgBuilder` (Option `nurRohr`), `Application/Reports/SchachtgrafikSvgBuilder`,
`Application/Reports/SchachtSchadenOrtRegel`, `SchachtSchadenKategorieRegel`, `SchachtBauteilNamen`.
Einstellungen: `AppSettings.HaltungenAnsicht` und `SchaechteAnsicht`, beide Standard `"liste"`.

## Regeln, die nicht gebrochen werden dürfen

- Nur im Worktree `C:\Sewer-Studio_KI_4.5-nova` arbeiten. Hauptbaum nur lesen, bis Schritt 5.
- Die App nie mit echtem Profil starten (Autosave). Bilder nur über den isolierten Prüfhost.
- Deutsch: sichtbare Texte mit echten Umlauten und Schweizer `ss`, Quellcode-Kommentare mit ae/oe/ue.
- Farben, Schriftgrössen, Rundungen ausserhalb `Theme/*.xaml` nur als `DynamicResource`-Tokens.
- Wächter dürfen angepasst, nie entkernt werden. 1000 Zeilen je `.cs`; die `DataPage`-Teildateien zusammen ≤ 2000
  (aktuell 2434 verteilt auf 20+ Dateien, grösste Einzeldatei unter der Grenze), `SchaechtePage.xaml.cs` steht bei 988/1000 —
  neue Logik in einen Controller auslagern, nicht anhängen.
- Ein bekannter Test ist rot und war es vorher schon: `NachschlagKontextmenueTests` (60-Sekunden-Limit, Befund A05 in CLAUDE.md).
  Er ist kein Regress dieser Arbeit; jede andere rote Stelle ist einer.
- Nie zwei Agenten gleichzeitig im selben Worktree an `src/` arbeiten lassen — in dieser Runde haben sich dadurch
  Commits vermischt (`c46547110` enthält Dateien einer anderen Aufgabe).

## Schritt 1 — Nachprüfung der Schacht-Fixrunde (`ed3268616`)

Der Review hatte gefunden: In der Listenansicht liefen zwei Formulare desselben Schachts (die unsichtbare
Eingabefelder-Schublade und die Liste), weil `SchaechteNovaWorkspaceController` kein Gegenstück zu
`DataPageNovaWorkspaceController.LeereFelderDrawer()` hatte. Der Fix ist committet, aber nicht nachgeprüft.

```
cd C:\Sewer-Studio_KI_4.5-nova
dotnet build AuswertungPro.sln
dotnet test tests/AuswertungPro.Next.UI.Tests --no-build --filter "SchaechteAufklapp|SchaechteAnsichtRegel|SchaechteNovaLayoutIsolated|DataPageNovaLayoutIsolated|DesignAudit|XamlActionWiring|UiArchitectureGuard|MaintainabilityFitness|UebersprungeneTests"
```

Zu prüfen (Codelesung genügt, plus die Tests oben):
- `SchaechteNovaWorkspaceController.LeereFelderDrawer()` existiert, entsorgt den `DataPageDetailLiveSync` und leert `Groups`/`Titel`.
- `SchaechtePage.NovaWorkspace.cs` verzweigt gegen `ListeSichtbar`, genau wie `DataPage.NovaWorkspace.cs`.
- Beim Zurückwechseln auf die Tabelle wird die Schublade wieder gefüllt (Test mit echtem ViewModel; der alte
  Schacht-Smoketest baut die Seite ohne ViewModel und greift deshalb nicht).
- Es gibt in der Listenansicht genau einen Live-Sync.

## Schritt 2 — Abnahme-Bilder über den Prüfhost

Der Prüfhost liegt unter `docs/reviews/2026-09-06-nova/wpf-etappe-2/werkzeug/` und ist bewusst nicht Teil der Solution.

```
dotnet build src/AuswertungPro.Next.UI/AuswertungPro.Next.UI.csproj -c Debug
dotnet build docs/reviews/2026-09-06-nova/wpf-etappe-2/werkzeug/Pruefhost.csproj
set P=docs\reviews\2026-09-06-nova\wpf-etappe-2\werkzeug\bin\Debug\net10.0-windows10.0.19041\Pruefhost.exe
%P% Light  Haltungen docs\reviews\2026-09-06-nova\aufklapp-liste\bilder\haltungen-liste-hell.png   1920 1080 haltungenliste
%P% Dark   Haltungen docs\reviews\2026-09-06-nova\aufklapp-liste\bilder\haltungen-liste-dunkel.png 1920 1080 haltungenliste
%P% Light  Schaechte docs\reviews\2026-09-06-nova\aufklapp-liste\bilder\schaechte-liste-hell.png   1920 1080
%P% Dark   Schaechte docs\reviews\2026-09-06-nova\aufklapp-liste\bilder\schaechte-liste-dunkel.png 1920 1080
```

Die Haltungs-Variante `haltungenliste` schreibt zwei Bilder je Aufruf (`-zu-` und `-auf-`). Für die Schächte gibt es
diese Variante noch nicht — **Aufgabe**: in `werkzeug/Program.cs` eine Variante `schaechteliste` nach demselben Muster
ergänzen (Visual-Tree-Suche nach `SchachtAufklappListe`, `KlappeAuf(erster Schacht)`, 2 s warten, zweites Foto).

Jedes Bild ansehen und gegen diese Liste prüfen:
- Kopfzeilen einzeilig, alle Spalten sichtbar, Zahlen rechts, Zustandsmarke farbig.
- Pfeil dreht beim Aufklappen; darunter die Themen mit Zählern (Haltung 17/9/11/3 + Weitere zu; Schacht 10/9/7/4 + Weitere zu).
- Felder editierbar sichtbar, keine Bindungsfehltexte (`{DependencyProperty…}`).
- Rechts die Grafik: Haltung Rohrsäule mit Meterskala, Knoten, Symbolen; Schacht Konus/Schachtwand/Sohle mit Tiefe,
  Innenmass, Zu-/Ablauf und Symbolen je Zone. Beschriftungen lesbar, nichts überlappt.
- Dunkles Thema lesbar.

Bekannte offene Kleinigkeiten aus den bisherigen Bildern (bitte beheben oder in der Abnahme als Grenze führen):
Kopftext „PROTOK…" und „INNENMAS…" werden gekürzt (Spalte breiter oder kürzerer Titel); im dunklen Thema wirkt das
Badge „fachlich geprüft" kontrastarm; die Zustandsmarke steht doppelt (Kopfzeile der Liste und Kopf des Panels).

## Schritt 3 — Abnahme und Doku

- `docs/reviews/2026-09-06-nova/aufklapp-liste/ABNAHME.md` anlegen: Anlass (Pascals Wunsch vom 08.09.: Übersichtsliste
  mit aufklappbaren Haltungen statt Tabelle, Grafiken wie im AWU-Haltungsprotokoll beziehungsweise WinCan-Schachtschnitt),
  Prüfpunkttabelle je Aufgabe 1–7 mit Bildverweis und bestanden/abweichend, die Rulings und die aufgeschobenen
  Kleinbefunde aus `.superpowers/sdd/2026-09-08-nova-haltungen-aufklapp-liste/progress.md` (Ledger, git-ignoriert —
  Inhalte in die Abnahme übernehmen, bevor der Ordner gelöscht wird), Grenzen (kein produktiver Start,
  Windows-Skalierung 125/150 % nicht gemessen, Prüfhost erfasst die Mica-Fläche nicht).
- Release-Gesamtlauf und Zahlen in die Abnahme:
  ```
  dotnet build AuswertungPro.sln -c Release
  dotnet test AuswertungPro.sln -c Release --no-build
  ```
  Erwartung: 0 Fehler, 0 Warnungen; vier Testprojekte grün ausser dem bekannten `NachschlagKontextmenueTests`.
- `CLAUDE.md`: Abschnitt „Nova: Aufklapp-Listen und Grafiken (2026-09-08)" nach „Nova-Fixwelle 2b" mit den Regeln:
  Standardansicht „liste" für beide Seiten (`AppSettings.HaltungenAnsicht`/`SchaechteAnsicht`), genau EIN Formular je
  Datensatz (das jeweils unsichtbare wird entsorgt), Tasten- und Fokusregel geteilt (`HaltungAufklappTastenregel`;
  Escape aus einem Editor gibt zuerst den Fokus an die Zeile zurück, damit die Eingabe zurückgeschrieben wird),
  SVG-Teilmenge als Vertrag (wer im Builder ein neues SVG-Element einführt, muss `SvgTeilmengeZeichner` erweitern;
  der Zeichner wirft sonst und das Control zeigt einen Leerzustand), Schachtgrafik-Kategorie nur bei bekanntem
  Bauteilnamen aus dem Text (mit Wortgrenze und Negationswächter), Haltungsgrafik liest ausschliesslich
  (nie `ResolveEntriesForExport`, das repariert Daten). Klassennamen per Glob prüfen, keine erfundenen.
- Commit: `git add docs CLAUDE.md` (nicht `-A`), deutsche Commit-Message, Ende
  `Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>`.

## Schritt 4 — Schlussprüfung des ganzen Branches

Diff-Paket erzeugen und lesen (nicht in die Antwort kopieren, es ist gross):

```
bash "C:/Users/Besitzer/.claude/plugins/cache/claude-plugins-official/superpowers/6.3.0/skills/subagent-driven-development/scripts/review-package" docs/superpowers/plans/2026-09-08-nova-haltungen-aufklapp-liste.md cfccda464 HEAD
```

Worauf zu achten ist (das hat in dieser Runde jedes Mal etwas gefunden):
- Datensicherheit: Schreibt eine Ansicht still in den Datensatz? Wird beim blossen Anzeigen etwas repariert?
  Wird ein Handwert (Zustandsklasse) nur bei echter Auswahl gestempelt?
- Zwei Wege zur selben Entscheidung: Auf der Schachtseite gab es einen zweiten Rechtsklickpfad ohne Schutz —
  suche nach weiteren Doppelwegen (Löschen, Kontextmenü, Tastenkürzel) zwischen Liste und Tabelle.
- Abo-Lecks: `PropertyChanged` von Datensätzen, Events des ViewModels, `Unloaded`/`Loaded`-Symmetrie.
- Still grüne Tests: Kindprozess-Tests, die nur sich selbst überspringen; Assertions, die den vorbereiteten Wert prüfen
  statt die Wirkung (in dieser Runde zweimal passiert).
- Danach eine Fixwelle, eine Nachprüfung, fertig.

## Schritt 5 — Merge in den Hauptbaum und Push

Der Hauptbaum trägt ungesicherte XTF-Arbeit. Bewährter Weg (zweimal ohne Konflikt gelaufen):

```
cd C:\Sewer-Studio_KI_4.5
git status --short          # 38 Dateien der anderen Sitzung, nicht committen
git stash push -u -m "XTF-Arbeitsstand vor Nova-Aufklappliste"
git merge --no-ff feature/nova-haltungsliste -m "Nova: Aufklapp-Listen und Grafiken uebernehmen"
git stash pop               # Überschneidungen prüfen, es waren bisher nur CLAUDE.md und zwei Dateien
dotnet build AuswertungPro.sln -c Debug
git push origin feature/eval-pruefsatz-review
```

Vor dem Merge sicherstellen, dass `SewerStudio.exe` nicht läuft (sonst sind DLLs gesperrt und der Build täuscht).
Nach dem Merge kurz melden, was Pascal sieht und was offen bleibt (Sichtprobe im echten Projekt, Skalierung 125/150 %).

## Wo alles steht

- Plan mit allen Aufgaben: `docs/superpowers/plans/2026-09-08-nova-haltungen-aufklapp-liste.md`
- Ledger, Briefs, Berichte, Diff-Pakete, Bilder der Zwischenstände:
  `.superpowers/sdd/2026-09-08-nova-haltungen-aufklapp-liste/` (git-ignoriert; Inhalte vor dem Löschen in die Abnahme übernehmen)
- Bisherige Bilder der Liste: `docs/reviews/2026-09-06-nova/aufklapp-liste/bilder/`
- Vorbild-Abnahmen früherer Etappen: `docs/reviews/2026-09-06-nova/wpf-etappe-2/ABNAHME.md` und `wpf-etappe-2b/ABNAHME.md`
