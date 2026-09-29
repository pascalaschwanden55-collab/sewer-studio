# Plan: Optik und Bedienung professionell (28.09.2026)

Spec: Optikanalyse vom 28.09.2026 (Gesprächsergebnis, zusammengefasst in Abschnitt «Befunde» unten).
Auftrag Pascal: «Setze alles um.» Zweig `feature/optik-professionell`, abgezweigt von
`feature/webgis-uebertragung` (HEAD zum Zeitpunkt der Anlage), Arbeitsbaum
`C:\Sewer-Studio_KI_5.0\.claude\worktrees\optik-professionell`.

## Global Constraints (gelten für JEDE Aufgabe)

1. **Arbeitsort:** ausschliesslich im Arbeitsbaum `C:\Sewer-Studio_KI_5.0\.claude\worktrees\optik-professionell`.
   Nie im Hauptordner `C:\Sewer-Studio_KI_5.0` schreiben, bauen oder committen. Nie in
   `C:\Users\Besitzer\Documents\ChatGPT\SewerStudio_*` (Ferienautomation) etwas tun.
2. **Geschützte Dateien** (dort liegt fremde, noch nicht gesicherte Arbeit; NICHT ändern):
   alle Dateien unter `src/AuswertungPro.Next.Application/WebGis/`, `src/AuswertungPro.Next.UI/ViewModels/Pages/ExportWebGisBereich.cs`,
   `src/AuswertungPro.Next.UI/Views/Windows/WebGisVorschauWindow.xaml(.cs)`, `WebGisSchreibBestaetigungWindow.*`,
   `WebGisHolenWindow.*`, `tests/**/WebGis*`. ExportPage.xaml darf umgeordnet werden, die Bindungen an
   `WebGis.*` bleiben wörtlich gleich (Wächter `ExportWebGisBereichTests`).
3. **Verhalten bleibt**: keine Fachlogik, keine Datenformate, keine Feldschlüssel (`FieldKeys`, Spaltennamen,
   JSON-Namen) ändern. Nur sichtbare Texte, Darstellung, Anordnung, Bedienwege. Ausnahme: Aufgaben 15/16 fügen
   Funktionen hinzu (Rückgängig, Befehle in der Suche) – additiv.
4. **Designsystem Nova** (CLAUDE.md): nur `{DynamicResource …}`-Tokens für Farben, `Text*`-Tokens für Schrift
   (Untergrenze 11 px), `Radius*`-Tokens, `ui:FluentIcon` für Symbole, `WindowFx.Entrance="True"` auf Fenstern,
   Fenstertitel `SewerStudio — <Aufgabe>`, `MinWidth`/`MinHeight` bei veränderbaren Fenstern, Icon-Knöpfe mit
   `AutomationProperties.Name` UND `ToolTip`. Sichtbare Texte mit echten Umlauten, Schweizer «ss» (kein «ß»).
   Quellcode/Kommentare/Bezeichner bleiben ae/oe/ue.
5. **Knopfregel für Fenster** (wird in Aufgabe 3 als Wächter festgeschrieben): Knopfleiste unten rechts,
   Hauptaktion ganz rechts mit Stil `PrimaryButton` und `IsDefault="True"`, «Abbrechen»/«Schliessen» direkt
   links daneben mit `IsCancel="True"`, weitere Aktionen links davon. Höchstens EIN Hauptknopf je Fenster/Seite.
6. **Grenzen:** keine neuen NuGet-Pakete. `DataPage`-Teildateien zusammen < 2000 Zeilen, `PlayerWindow` steht am
   Zeilenlimit (neue Player-Logik in Controller). `UI/Ai` ist eingefroren (`UiAiFreezeArchitectureTests`) –
   neue Ablaufklassen nach `Application/UseCases`. Neue Dienste mit Interface, Registrierung im ServiceProvider
   und `ServiceProviderRegistrationMap` + Zählertest anpassen.
7. **Tests:** jede Aufgabe mit fokussiertem Test/Wächter. WPF-Tests nur im isolierten Kindprozess-Muster der
   bestehenden `*IsolatedSmokeTests`; NIE im Testprozess `new App()` erzeugen und den Dispatcher pumpen.
   Das echte Programm nie mit dem echten Benutzerprofil starten. Vor dem Commit:
   `dotnet build AuswertungPro.sln -c Debug` (0 Fehler, keine neuen Warnungen) und die betroffenen
   Testklassen + alle `DesignAudit*`-Tests grün. Bestehende Wächter nicht aufweichen; müssen sie wegen einer
   gewollten Änderung angepasst werden, im Bericht begründen.
8. **Commits:** Deutsch, Präfix `ui(optik): …`, Schluss `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
   Kein Push, kein Merge.
9. **CLAUDE.md ergänzen**: Jede Aufgabe, die eine neue Regel schafft (z. B. Knopfregel, Dialogdienst),
   trägt einen knappen Absatz unter einer neuen Überschrift «## Optik und Bedienung professionell (28.09.2026)»
   in `CLAUDE.md` des Arbeitsbaums nach (Regeln, die nicht zurückfallen dürfen, + Wächtername).

## Befunde (Spec, Kurzfassung der Analyse)

- ~400 Meldungen laufen über `DialogService` → Windows-MessageBox (grau, ignoriert Dunkelmodus, Titel «Bestaetigung»).
- Knopfleisten in ~50 Fenstern: Reihenfolge 50:50 gemischt, 6 Hauptknopf-Stile, ~15 Fenster ohne Hauptknopf,
  eigene Köpfe in 3 Grössen, Dossier-Fenster ohne Kopf, 219 Knöpfe ohne Stil, 13 lokale Knopfstile
  (u. a. `CompactButton` in DataPage.xaml doppelt), 23× Background direkt am Knopf.
- «Weitere Aktionen»: Haltungen 24, Schächte 23 Einträge ohne Gruppentitel; Doppelungen; uneinheitliche Namen.
- Alte/neue Ansichten parallel im Menü.
- Export-Seite überladen, Überschrift «Zielordner & Verzeichnisbaum» am falschen Ort, Untertitel ohne WebGIS.
  Import-Seite: zwei Hauptknöpfe, Katalogpfad sichtbar, «Report/Bericht» uneinheitlich.
- Entwicklersprache (Einstellungen, Training Center englisch, «Pipeline Analysis», TIDs, FME …).
- Kein Hilfe-Menü, kein F1, kein «Über»-Dialog, Handbuch veraltet mit Entwicklerteil.
- Fenstersymbol = Kundenlogo `abwasser-uri-logo.png` statt `app.ico`; Schreibweisen «SEWER STUDIO»/«Sewer Studio».
- ~40 rohe `ex.Message`/`ex.ToString()` in der UI (Import-Seite zeigt Stacktrace), ~94 C#-Meldungen mit ae/oe/ue,
  52× «ß».
- ~30 Listen ohne Leerzustand; `StatusHost` 1×, `BusyOverlay` 2×; 27 Fortschrittsbalken in 9 Höhen.
- Feste Farben in C# (HydraulikPanel, Sanierungsmassnahmen, ViewModels), 86× «Consolas» statt `FontMono`.
- Kein Undo für Haltungs-/Schachtdaten. Strg+K findet keine Befehle.
- Dunkelmodus folgt Windows nicht; keine Hochkontrast-Unterstützung; kein Taskleistenfortschritt;
  kein app.manifest.
- Logo-Pfad an ~7 Stellen fest; keine gemeinsame Quelle für Berichtskopf/-fuss.
- Kleinigkeiten: Statuszeile «Schaechte», rohe Feldnamen «Schacht_oben» als Beschriftung, Statuszeile
  «Spalten geladen: 30», Training Studio schneidet Schadensstufen 4/5 ab, Übersicht «Häufigste Schäden»
  doppelt beschriftet, Player-Kopf zeigt vollen Pfad, Player «Play/Stop» englisch, Einstellungs-Suchfeld ohne
  Platzhalter, «Geladen: projekt.json».

---

### Task 1: Nova-Dialog statt Windows-MessageBox

**Ziel:** Alle Meldungen und Rückfragen im Programm-Design, im Hell- und Dunkelmodus.

- Neues Fenster `Views/Windows/NovaDialogWindow.xaml(.cs)`: Nova-Karte, Symbol je Art (Info, Warnung, Fehler,
  Frage) als `ui:FluentIcon` in Status-Farbe (`AccentBrush`/`WarningBrush`/`DangerBrush`), Titel, Text
  (auswählbar/kopierbar, umbrechend, bei langem Text scrollbar, max. Höhe ~60 % Bildschirm), Knopfleiste nach
  Knopfregel. Knöpfe deutsch: «OK»; «Ja»/«Nein»; «Ja»/«Nein»/«Abbrechen». Enter = Standardknopf, Esc = Abbrechen
  bzw. «Nein» bzw. «OK». `ConfirmWarn(defaultNo:true)` macht «Nein» zum Standardknopf. Owner = aktives Fenster
  der Anwendung (sonst MainWindow), `WindowStartupLocation=CenterOwner`, `ShowInTaskbar=false`,
  `SizeToContent=Height`, feste Breite ~460 px, `WindowFx.Entrance`. Knopf «Kopieren» (Text in Zwischenablage)
  als kleiner Icon-Knopf links nur bei Fehlern.
- `Services/DialogService.cs`: `Info/Warn/Error/Confirm/ConfirmWarn/ConfirmCancel` nutzen den Nova-Dialog.
  Standardtitel mit Umlaut («Bestätigung»). Aufruf vom Nicht-UI-Thread wird auf den Dispatcher gemarshallt
  (`Dispatcher.Invoke`, nur hier erlaubt). Fehlt `Application.Current` (Kommandozeilen-/Testumgebung), bleibt
  der bisherige `MessageBox`-Weg als Rückfall.
- `DossierPreviewWindow.xaml.cs:522` direkten `MessageBox.Show` über den Dialogdienst leiten (oder, falls dort
  kein Dienst verfügbar ist, über eine statische Hilfe `NovaDialog.Zeige…`, die derselbe Dienst benutzt).
- Wächter `DesignAuditDialogeTests`: ausserhalb `DialogService`/`NovaDialog*` kein `MessageBox.Show` im UI-Projekt.
- Test: isolierter WPF-Kindprozess öffnet den Dialog je Art, prüft Knopfbeschriftungen/Reihenfolge,
  Standard-/Abbrechen-Knopf, Rückgabewerte (per programmatischem Klick), und Darstellung im Dunkeltheme
  (Hintergrund = CardBrush des Dunkeltheme).
- CLAUDE.md-Absatz.

### Task 2: Erfolgsmeldungen als Toast statt blockierendem Dialog

- Durchsuche alle `Dialogs.Info(`/`_dialogs.Info(`-Aufrufe (ca. 124). Stelle NUR reine Erfolgsbestätigungen um
  («… gespeichert», «… exportiert/erstellt/geschrieben nach …», «Fertig», Titel «OK»), wenn im Kontext ein
  `IToastService` erreichbar ist oder sauber hereingereicht werden kann (Konstruktor/Dienste-Parameter,
  kein Service-Locator in Seiten). Hat die Erfolgsmeldung einen Ordner/Datei, Toast mit Aktion
  «Ordner öffnen»/«Datei öffnen» (vorhandene `Success(message, aktionText, aktion)`-Überladung, Explorer über
  vorhandenen `IExplorerRevealService`). Hinweise, die der Benutzer lesen/entscheiden muss (Warnungen, Listen
  von Problemen, Teil-Erfolge mit Fehlern), bleiben Dialoge.
- Kein Dialogtitel «OK» mehr (Titel sagt, worum es geht).
- Liste aller umgestellten Stellen in den Bericht.
- Tests: bestehende Tests mit Fake-Dialogdienst, die eine Info erwarten, auf Toast-Fake umstellen; neue Fakes nur
  wo nötig. Mindestens ein Test je umgestelltem ViewModel-Typ, der den Toast statt der Info belegt (Stichprobe
  der 5 meistgenutzten reicht, wenn die übrigen gleich gebaut sind – im Bericht begründen).

### Task 3: Fensterregel – gemeinsamer Kopf und Knopfleiste (Teil 1: Grundlage + Dossier-Fenster)

- In `Theme/Controls.xaml` (beide Themes wirksam): Stil/Vorlage für einen Fensterkopf. Bevorzugt das
  vorhandene `NovaPageHeader`-Control in Fenstern wiederverwenden (Titel + Untertitel + optionale Aktionen);
  nur wenn es dafür nicht taugt, ein schlankes `NovaDialogHeader` daneben. Dazu ein Stil `DialogButtonBar`
  (rechtsbündig, einheitlicher Abstand 8 px zwischen Knöpfen, Rand oben 16 px, Trennlinie `BorderLightBrush`).
- Primärstil vereinheitlichen: Fenster-Hauptknopf = `PrimaryButton`. `SuccessButton`, lokale `BtnPrimary`,
  Knöpfe mit direkt gesetztem Background werden auf `PrimaryButton`/`SecondaryButton`/`DangerButton`
  (falls vorhanden; sonst bestehende Danger-Variante) umgestellt. Lokale Doppeldefinition `CompactButton` in
  `DataPage.xaml:31` entfernen, wenn die Theme-Fassung gleich wirkt (sonst Theme-Fassung angleichen und lokale
  löschen).
- Anwenden auf alle 11 Dossier-Fenster (Area, Batch, Edit, HoldingPicker, PageSelection, ParcelLookup, Plan,
  Preview, PrintDialog, Refresh, ShaftPicker): Kopf mit Titel + bisherigem Einleitungssatz als Untertitel,
  Knopfleiste nach Knopfregel.
- CLAUDE.md-Absatz mit der Knopfregel.

### Task 4: Fensterregel – übrige Fenster + Wächter

- Alle übrigen Fenster in `Views/Windows`, `Views/Protocol*`, `Dialogs` (ausser Video-Fenster Player,
  LiveFrame, Splash, PhotoMeasurement und den geschützten WebGIS-Fenstern) auf Kopf + Knopfleiste nach
  Knopfregel. Fenster mit eigener Werkzeugleiste (TrainingCenter, TrainingStudio, VideoAnalysisPipeline,
  Beobachtungen, VsaCodeExplorer, HydraulikPanel) behalten ihre Arbeitsfläche; nur Kopf vereinheitlichen und
  die Abschluss-Knopfleiste nach Regel.
- Knöpfe ohne Stil in Fenstern bekommen `SecondaryButton` bzw. `ToolbarButton` (Werkzeugleisten).
- Wächter `DesignAuditKnopfleistenTests`: in jedem geprüften Fenster-XAML (a) höchstens ein `PrimaryButton`,
  (b) ein Knopf mit `IsCancel="True"` steht im selben Container direkt links vom `IsDefault`-Knopf, falls beide
  existieren, (c) keine lokalen `Style TargetType="Button"`-Definitionen mit x:Key in Fenstern
  (Ausnahmeliste mit Begründung erlaubt), (d) kein `Background=` direkt an `<Button`. Ausnahmen namentlich.

### Task 5: Programmidentität

- Fenstersymbol: `App.xaml.cs LoadDefaultWindowIcon` lädt `Assets/Brand/app.ico` (Kundenlogo bleibt in
  Berichten/Dossiers unverändert). Prüfen, dass `app.ico` ins Ausgabeverzeichnis kopiert wird.
- Neues Fenster «Über SewerStudio» (`AboutWindow`): Programmsymbol, Name, Version aus `AppIdentity`,
  Build-Datum und – falls ohne Aufwand verfügbar – Git-Commit (nur wenn bereits als Assembly-Attribut vorhanden;
  sonst weglassen), Ordner für Daten/Logs/Einstellungen mit «Öffnen»-Knöpfen, «Systeminfo kopieren»
  (Version, Windows, .NET, GPU-Name falls vorhandener Dienst ihn kennt). Knopfregel.
- Schreibweise: sichtbare Texte «SewerStudio» (Wortmarke in der Seitenleiste darf gestaltet bleiben, aber als
  «SewerStudio»; entscheide nach Optik und begründe). Fundstellen: `AiDocumentationPdfBuilder.cs:67,94`,
  `AiStartupOrchestrator.cs:68`, weitere per Suche nach `Sewer Studio`/`SEWER STUDIO` in sichtbaren Texten.
- Statuszeile `MainWindow.xaml:314` «Schaechte» → «Schächte»; die tote, ausgeblendete Doppelzeile entfernen.
  «Geladen: projekt.json» unter der Wortmarke → Projektname statt Dateiname (Dateipfad als ToolTip), falls die
  Bindung das ohne neue Fachlogik hergibt.
- Test: Wächter, dass keine sichtbare XAML-Zeichenkette «Sewer Studio»/«AuswertungPro» enthält; Smoke-Test
  About-Fenster im Kindprozess.

### Task 6: Hilfe-Menü, F1, Tastenkürzel, Handbuch

- `MainWindow.xaml`: neues Menü «_Hilfe» mit «Handbuch (F1)», «Tastenkürzel (Strg+F1)», «Über SewerStudio».
  F1 öffnet das Handbuch beim Abschnitt der aktuell gewählten Seite (Zuordnung Seitenschlüssel → Abschnitt).
- Handbuch aus dem Einstellungs-Reiter «Hilfe» in ein eigenes, nicht-modales Fenster `HandbuchWindow`
  (Inhaltsverzeichnis links, Text rechts, Suchfeld). Der Einstellungsreiter «Hilfe» verweist nur noch darauf
  (Knopf «Handbuch öffnen»). Inhalt aktualisieren: alle 16 aktuellen Seiten (Übersicht, Projekt, Haltungen,
  Schächte, Import, Export, Medienkonflikte, Druckcenter, Dossiers, Sanierungs-Matrix, Schacht-Matrix,
  Schattenauswertung, VSA, Diagnose, Einstellungen) mit kurzem Zweck + wichtigsten Abläufen, nichts
  beschreiben, was es nicht gibt (gegen XAML prüfen). Entwicklerteil (Umgebungsvariablen, Sidecar,
  Modell-Registry, Merge-Engine) in einen Abschnitt «Für Fachleute (technisch)» am Ende, standardmässig zu.
- Fenster «Tastenkürzel»: alle globalen Kürzel (Strg+K Suche, F3, F11 Fokus, Strg+S, Strg+Z/Y sobald Task 16
  fertig – hier nur vorhandene), Player-Kürzel aus `PlayerKeyboardShortcutPolicy` (eine Quelle, nicht
  abschreiben).
- Tests: Wächter, dass jede Seite der Leiste (`ShellViewModel`-Navigation) einen Handbuchabschnitt hat;
  Kürzelfenster liest Player-Kürzel aus der Policy.

### Task 7: «Weitere Aktionen» gliedern, Doppelungen und Namen bereinigen

- Haltungen (`DataPage.xaml`) und Schächte (`SchaechtePage.xaml`): gleiche Gliederung mit Untermenüs:
  «Daten abgleichen ▸» (Vom WebGIS holen, GeoShop-Abgleich (XTF), Leere Felder aus QGIS),
  «Bearbeiten ▸» (fachliche Einzelaktionen), «Reihenfolge ▸» (Nach oben, Nach unten, Auf Position…,
  Gehe zu Zeile…), «Ansicht ▸» (Ansicht wechseln, Ansicht anpassen, Spaltenausrichtung …), «Ausgabe ▸»
  (Dossier, Drucken, PDF …). Beide Seiten gleiche Namen («Nach oben/Nach unten»). Keine deaktivierten
  Menüpunkte als Gruppenköpfe (`XamlActionWiringGuard`).
- Doppelungen entfernen: «Sanierungsmaßnahme bearbeiten» (gleich wie Zeilenmenü) nur einmal, Schreibweise
  «Sanierungsmassnahmen…»; Fokusmodus aus «Weitere Aktionen» raus (bleibt im Menü Ansicht); Schächte:
  Zeilenmenü-Duplikate in «Weitere Aktionen» entfernen, wenn sie im Zeilenmenü bleiben.
- Unklare Namen: «Strassen» → sprechend (Funktion lesen), «PDF-Daten» → sprechend, «Aktualisieren» →
  «Protokoll neu einlesen». «Messvorlagen…» im Werkzeug-Menü → «Massnahmenvorlagen…» (passend zu Tooltip und
  Fenstertitel «Massnahmen bearbeiten»).
- «Vom WebGIS holen» ohne Anmeldung: Tooltip/Status sagt klar «Zuerst auf der Seite Export am WebGIS anmelden».
  Nur Beschriftung/Tooltip – Befehlslogik NICHT anfassen.
- Namen Leiste ↔ Seitentitel angleichen («Dossiers» ↔ «Eigentümerdossiers»: Leiste «Eigentümerdossiers» oder
  Seitentitel anpassen; «VSA» ↔ «VSA-Bewertung»; Leiste mit Umlauten «Übersicht», «Schächte» – nur
  Anzeigenamen, Schlüssel bleiben, `ShellNavigationTitles.Anzeige` beachten).
- Tests: bestehende Menü-/Wiring-Wächter grün; neuer Wächter: beide Seiten haben dieselben Untermenü-Titel.

### Task 8: Alte Ansichten aus den Menüs nehmen

- «Alte Haltungsansicht», «Alte Schachtansicht» (Weitere Aktionen) und «Klassische Übersicht» (Menü Ansicht)
  aus den Menüs entfernen. In Einstellungen › Allgemein neue Gruppe «Frühere Ansichten» mit drei Häkchen, die
  dieselben AppSettings-Schalter setzen (`ShowHaltungenNovaLayout`, `ShowSchaechteNovaLayout`,
  `ShowUebersichtNovaLayout`, bzw. die tatsächlich verwendeten). Umschalten wirkt wie bisher.
- Ansichtswahl «Aufklapp-Liste»/«Tabelle» bleibt (Untermenü Ansicht aus Task 7).
- Tests: Einstellungs-ViewModel setzt die Schalter; Menü enthält die alten Punkte nicht mehr.

### Task 9: Export- und Import-Seite ordnen

- Export (`ExportPage.xaml`): vier klar betitelte Abschnitte in dieser Reihenfolge: «Excel-Listen»,
  «Dateien verteilen», «Kataster (XTF)», «WebGIS». Untertitel der Seite nennt alle vier. Überschrift
  «Zielordner & Verzeichnisbaum» nur über dem echten Verzeichnisbaum-Bereich. Die zwei XTF-Wege je mit einem
  Satz Klartext, wann man welchen nimmt (aus `XtfExportAuswahl`-Logik: mit Importkopie → «Bestehende
  Katasterdaten aktualisieren», ohne → «Neue XTF erstellen»); Fachbegriffe (FME, .ili, DSS, SIA405,
  Zusatzmodell) in einen ToolTip/aufklappbaren «Technische Details». Genau ein Hauptknopf je Abschnitt
  (Akzent), Rest `SecondaryButton`/`ToolbarButton`. Bindungen/Befehle unverändert.
- Import (`ImportPage.xaml`): ein Hauptknopf («Ordner wählen und importieren»); «Letzter Bericht»,
  «Alle Berichte», «Import-Report» → einheitlich «Bericht» (z. B. «Letzter Bericht», «Alle Berichte»,
  «Bericht erstellen» – Funktion prüfen und benennen). Katalogpfad-Zeile ersetzen durch kurzen Status
  «VSA-Katalog geladen (2019)» mit Pfad im ToolTip; «Katalog neu laden» dort lassen.
  «Eigene Protokolle neu erzeugen» und «Projekt portabel machen» unter «Nacharbeiten» klar beschriften.
- Stacktrace auf der Import-Seite (`ImportRunWorkflowController.cs:335-336` + `ImportPage.xaml:270`):
  sichtbar nur verständliche Fehlermeldung; technischer Text in einem zugeklappten «Technische Details».
- Tests: `ExportWebGisBereichTests`, Export-/Import-Seiten-Wächter grün; Wächter «höchstens ein
  ToolbarButtonAccent je Abschnitt» für beide Seiten.

### Task 10: Sprache – Deutsch, verständlich, ss

- Englisch → Deutsch in sichtbaren Texten: Training Center («Approve», «Reject», «Set New», «Review Queue»,
  «KB Qualität», «CaseId», «YOLO Export…», «auto-approve», «Samples», «Reset» …), Fenstertitel
  «Pipeline Analysis» → «SewerStudio — Videoanalyse», Einstellungen («Drop late frames», «Skip frames»).
- Einstellungen: technische Gruppen (Codex-Agenten-Daten, pdftotext.exe, Codec-Threads, YOLO/DINO-Schwellen,
  «Programm sichern» mit Quellcode) in einen je Reiter zugeklappten Bereich «Erweitert (für Fachleute)».
  Keine Einstellung entfernen, keine Schlüssel ändern. Suchfeld der Einstellungen bekommt Platzhalter
  «Einstellung suchen…» (Suche muss eingeklappte Bereiche weiter finden und aufklappen –
  `SettingsSearchController` prüfen).
- Sichtbare C#-Meldungen mit ae/oe/ue → Umlaute (ca. 94, u. a. `UserError.cs`, `App.xaml.cs:69`,
  `SettingsFullBackupWorkflow.cs`, `SettingsPathWorkflow.cs`, `DataPagePrintController.cs`). NUR Zeichenketten,
  die dem Benutzer gezeigt werden; Schlüssel, Dateinamen, Log-Kategorien, JSON bleiben. Tests, die Texte
  wörtlich prüfen, mitziehen. `DesignAuditLaufzeittexteTests` um die neu bereinigten Dateien erweitern.
- «ß» → «ss» in sichtbaren Texten (52 Stellen, z. B. «Maßnahmen», «Straße», «schließen»).
- Rohe Fehlertexte: ~40 Stellen mit `ex.Message`/`ex.ToString()` in UI-Anzeigen (u. a.
  `DossierBatchWindow.xaml.cs:100,155`, `DossiersPageViewModel.Actions.cs:36,462,655,900`,
  `Ai/Training/*Workflow.cs`, `Ai/Live/LiveDetection*CommandWorkflow.cs`) → über vorhandenes `UserError`
  (Application/Common) eine deutsche, verständliche Meldung; technischer Text nur im Log bzw. aufklappbar.
  `ExportWebGisBereich.cs`/`WebGisVorschauWindow.xaml.cs` sind geschützt – auslassen und im Bericht nennen.
  Achtung: `UI/Ai` ist eingefroren nur für NEUE Dateien – Textänderungen in bestehenden Dateien sind erlaubt.
- Rohe Feldnamen als Beschriftung (z. B. «Schacht_oben», «Schacht_unten», «Referenzpruefung»): Anzeigenamen
  über den Feldkatalog (Label) korrigieren, NICHT die Schlüssel.
- Wächter: kein «ß» in sichtbaren XAML-Texten; keine sichtbare XAML-Zeichenkette mit typischen
  ASCII-Umlautwörtern (bestehenden Wächter erweitern statt neuen bauen, falls vorhanden).

### Task 11: Leer- und Ladezustände, Fortschrittsbalken

- `EmptyStateControl` in allen Listen/Tabellen ohne Leerzustand (ca. 30; u. a. Sanierungs-Matrix,
  Schacht-Matrix, Medienkonflikte, Schattenauswertung, ImportPreview, CodeCatalogEditor, MeasureTemplateEditor,
  ObservationCatalog, Verteilen, XtfLieferung, Dossier-Picker). Text sagt, was fehlt und was zu tun ist
  («Noch keine Haltungen – Projekt über Import laden»). Liste der bearbeiteten Stellen in den Bericht.
  Geschützte WebGIS-Fenster auslassen.
- Fortschrittsbalken: zwei Stile im Theme (`ProgressBarThin` 4 px, `ProgressBarStandard` 8 px, Radius-Token),
  alle 27 Balken darauf (Video-Fenster dürfen eigenen Stil behalten, wenn er begründet ist). VsaCodeExplorer:
  selbstgebauten Balken aus Borders durch echten ProgressBar ersetzen, falls gleichwertig.
- Lade-Hinweise: selbst gebaute «… wird geladen»-Texte, wo `StatusHost`/`BusyOverlay` passt, darauf umstellen
  (nur wo ohne Umbau der ViewModels möglich; sonst im Bericht als offen nennen).
- Wächter: jede `ProgressBar` hat einen der beiden Stile (Ausnahmeliste); Liste der Seiten mit
  EmptyStateControl.

### Task 12: Feste Farben und Schriften auf Tokens

- `HydraulikPanelWindow.xaml.cs:16-34`, `SanierungsmassnahmenWindow.xaml` (5 Hex), `CodingSessionViewModel`
  (10) und weitere ViewModels mit Farben, `VsaCodeExplorer` Ersatzfarben, `TrainingCenter`/`TrainingStudio`
  «White» → Theme-Tokens (per `TryFindResource`/`DynamicResource`), damit Dunkelmodus stimmt. Fachfarben
  (Zustandsklassen, Nutzungsarten, SVG-Grafik, PDF) NICHT ändern – die haben eigene Regeln.
- `FontFamily="Consolas"`/`"Consolas, Cascadia Mono"` (86×) → `{DynamicResource FontMono}`.
- Icon-Schrift: TextBlock mit `FontFamily=FontIcon` + Glyph (100×) → `ui:FluentIcon`, wo es ein reines Symbol
  ist (Video-Dateien ausgenommen).
- Wächter erweitern: `DesignAuditNovaPaletteTests`/bestehende Farbwächter um die bereinigten Dateien;
  neuer Wächter kein «Consolas» ausserhalb Theme.

### Task 13: Windows-Integration

- Design «Wie Windows» als dritte Wahl neben Hell/Dunkel (Einstellungen › Allgemein). Liest
  `HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize\AppsUseLightTheme`, reagiert auf
  `SystemEvents.UserPreferenceChanged`. Standard bleibt die bisherige Wahl des Benutzers (Bestand unverändert).
  Logik (Registry-Wert → Theme) als reine, getestete Regel.
- Hochkontrast: bei `SystemParameters.HighContrast` ein Theme-Wörterbuch, das die Kern-Tokens auf
  `SystemColors` abbildet (Text, Hintergrund, Karte, Rand, Akzent, Auswahl). Wechsel zur Laufzeit beachten.
- Taskleistenfortschritt: kleiner Dienst `ITaskbarFortschritt` (UI/Services), am MainWindow `TaskbarItemInfo`;
  angebunden an lange Läufe, die heute schon Fortschritt melden (Datensicherung, Ein-Knopf-Import,
  Videoanalyse-Fenster falls ohne Eingriff in eingefrorene UI/Ai-Dateien möglich). Fehler = roter Zustand.
- DPI: prüfe belegbar (Microsoft-Doku oder Laufzeitabfrage im Kindprozess), ob die App bereits
  PerMonitorV2 läuft. Nur wenn nicht: `app.manifest` mit `<dpiAwareness>PerMonitorV2</dpiAwareness>` +
  `ApplicationManifest` im csproj. Ergebnis im Bericht belegen.
- Tests: Theme-Regel (Registry-Wert, fehlender Wert, Hochkontrast), Taskbar-Dienst mit Fake.

### Task 14: Befehle in der Strg+K-Suche

- `GlobaleSucheRegel` (Application) um Befehle erweitern: alle Seiten der Leiste («Gehe zu: Export» …) und
  Hauptbefehle (Neues Projekt, Projekt öffnen, Speichern, Speichern unter, Import starten, Einstellungen,
  Handbuch, Tastenkürzel, Über SewerStudio, Fokusmodus). Befehle erscheinen als eigene Gruppe mit Symbol;
  Daten-Treffer (Haltungen/Schächte/Strassen) bleiben zuerst, wenn der Suchtext eine Nummer ist.
  Umlaut-tolerant wie `SettingsSearchMatcher`. Ausführung über vorhandene ShellViewModel-Befehle.
- Platzhalter der Suche: «Suchen oder Befehl eingeben…».
- Tests: Regeltests (Befehl gefunden, Reihenfolge, Umlaut), ViewModel-Test Ausführung.

### Task 15: Gemeinsame Quelle für Berichtskopf, Logo und Fusszeile

- `IBerichtsMarke` (Application, reine Daten: Logo-Pfad, Firmen-/Auftraggebername, Fusszeilentext) +
  Umsetzung (Infrastructure oder UI/Services), registriert im ServiceProvider. Liest Logo aus Einstellung
  (neu: «Logo für Berichte» in Einstellungen › Allgemein, Standard = bisheriger Pfad
  `Assets/Brand/abwasser-uri-logo.png`).
- Die ~7 fest eingetragenen Logo-Pfade (`DataPagePrintController.cs:396,488`, `CodingProtocolPdfExportPlanner.cs:30`,
  `ProtocolObservationsWindow.xaml.cs:392`, `ProtocolRegenerationAdapter.cs:77`,
  `NpkLeistungsverzeichnisExcelExporter.cs:351`, weitere per Suche) über diese Quelle.
- Layout der PDFs/Excel/Dossiers NICHT ändern (Dossier-Word-Vorlage ist verbindlich). Nur die Quelle
  zentralisieren. Bestehende PDF-/Excel-Tests müssen unverändert grün sein.
- Tests: Quelle liefert Standard, Einstellung überschreibt, fehlende Datei = sauberer Rückfall.

### Task 16: Rückgängig/Wiederholen für Haltungs- und Schachtdaten

- Design zuerst (im Bericht festhalten): `IDatenaenderungsVerlauf` (Application) – Stapel von
  Änderungseinträgen (Datensatz-Referenz, Feldschlüssel, alter Wert + alte FieldMeta, neuer Wert + neue
  FieldMeta). Erfasst werden NUR Benutzereingaben in Haltungs-/Schachttabelle, Formular/Aufklappliste und
  Objektakte-Tabellenfelder über die vorhandenen zentralen Schreibwege (`DataPageCellEditController`,
  `DataPageDetailItemFactory`, Schacht-Gegenstücke, Objektakte-Bearbeitung falls sie Tabellenfelder schreibt).
- Rückgängig stellt Wert UND Metadaten (Herkunft, Handmarke, Zeitstempel) exakt wieder her – über einen
  Schreibweg, der Katasterschutz/Handmarken-Logik nicht umgeht, aber auch keine neue Handmarke stempelt.
- Sperren (Verlauf wird geleert, Toast-Hinweis): Import, GeoShop/QGIS/WebGIS-Übernahme, Projektwechsel,
  Neu/Löschen von Datensätzen, und jede Feldänderung mit Dateifolgen (Haltungsname/Schacht_oben/Schacht_unten →
  Umbenennung, Schachtnummer). Solche Änderungen sind nicht rückgängig machbar.
- Strg+Z / Strg+Y (und Strg+Umschalt+Z) auf Haltungs- und Schachtseite, wenn kein Texteditor den Fokus hat
  (dort gilt das native Textfeld-Undo). Menü «Bearbeiten ▸ Rückgängig/Wiederholen» mit Beschreibung
  («Rückgängig: Material 10001-10002»). Tiefe 100. Nach Rückgängig wird automatisch gespeichert wie bei einer
  normalen Änderung (vorhandener Autosave-Weg).
- Tests: Verlaufsregel (Rückgängig/Wiederholen/Tiefe/Sperren), Metadaten exakt wiederhergestellt, Handmarke
  nicht neu gestempelt, Projektwechsel leert, Umbenennungsfeld nicht rückgängig machbar.
- CLAUDE.md-Absatz.

### Task 17: Kleinigkeiten und Endkontrolle der Seiten

- Übersicht (`ProjektUebersichtPage`): «Häufigste Schäden» nicht doppelt beschriften (Code links, Klartext +
  Zahl rechts → einmal Code + Klartext, Zahl rechts); Zustandsliste von Z0 (dringend) nach Z4 sortieren, falls
  sie heute Z4 zuerst zeigt; leere Karten («Sanierungsverfahren») mit Leerzustand.
- Haltungen/Schächte: Status «Spalten geladen: 30» ausblenden (nur Log). Kopfband «Rot · Lernbasis: 0 Fälle»
  über der Werkzeugleiste: verständlich beschriften oder nur zeigen, wenn relevant (Befund prüfen, Fachlogik
  nicht ändern).
- Player: Kopf zeigt Dateinamen, voller Pfad im ToolTip; «Play/Pause/Stop» → «Abspielen/Pause/Stopp»;
  unbeschrifteter «···»-Knopf bekommt ToolTip + AutomationProperties.Name; nur ein Akzentknopf in der
  Bedienleiste (Codier-Modus bleibt Akzent, Abspielen normal). PlayerWindow-Zeilenlimit beachten.
- Training Studio: Schadensstufen-Knöpfe 1–5 vollständig sichtbar (Umbruch/WrapPanel oder gleichmässige
  Breite); gequetschte Tabellenköpfe links («Art | S | Kc») lesbar.
- Einstellungen: «Anwenden» neben dem Design-Schalter und «Speichern» oben – ein klarer Weg (Design wirkt
  sofort beim Umschalten oder beim Speichern; Hinweis «Neustart nötig» nur wenn wirklich nötig).
- Abschluss: isolierten Prüfhost (`docs/reviews/2026-09-06-nova/wpf-etappe-2/werkzeug/`) nutzen, um neue
  Bildschirmfotos der Hauptseiten hell+dunkel nach `docs/reviews/2026-09-28-optik/bilder/` zu erzeugen
  (eigenes Profil, nie das echte). Kurzabnahme `docs/reviews/2026-09-28-optik/ABNAHME.md`: was umgesetzt,
  was offen, welche Sichtprüfung Pascal machen muss (125/150 % Skalierung, echte Projekte).
