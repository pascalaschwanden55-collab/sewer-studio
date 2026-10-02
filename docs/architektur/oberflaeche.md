# Oberfläche, Nova-Design und Bedienung

> Aus `CLAUDE.md` ausgelagert am 30.09.2026 (Wartbarkeitsaudit, Befund Z1). Der Text ist
> **unverändert** übernommen: Geltende Regeln stehen neben datierten Arbeitsständen und
> Messverläufen. Bei Widersprüchen gilt der jüngere Abschnitt und im Zweifel der Code
> samt seinem Test. Veränderliche Zahlen (Dienstanzahl, Testanzahl) sind Momentaufnahmen.
>
> Neue Erkenntnisse zu diesem Bereich hier eintragen, **nicht** in `CLAUDE.md`.

## Inhalt

- Haltungs- und Schachtseite: gemeinsame Ablaeufe (02.10.2026)
- Optik und Bedienung professionell (28.09.2026)
- Startanimation 5.0: Kugel, deutlich neu (14.09.2026)
- Persoenliche Erledigt-Markierung (13.09.2026)
- Aufklapplisten: Reihenfolge (09.09.2026)
- Nova-Abschluss: Video-Kopienregel (2026-09-06)
- Nova-Nachpruefung abgeschlossen (2026-09-06)

## Haltungs- und Schachtseite: gemeinsame Ablaeufe (02.10.2026)

Deepscan 02.10.2026, Befund A2: Die beiden Datenseiten bestanden aus Datei-Paaren, die zu
59-91 % gleich waren, und liefen auseinander. Regel: **Ein Ablauf, der fachlich gleich ist,
steht einmal** und beide Seiten rufen ihn; die Seiten liefern nur, was wirklich verschieden ist.

- **Uebernahme abgeschlossen** (QGIS-Nachfuellen, GeoShop-Abgleich, WebGIS-Holen): jeder Weg
  beider Seiten ruft `MeldeUebernahme()`, beide nur `SeitenUebernahme.Abschliessen` — Projekt
  geaendert (`MarkProjectDirty`), Autosave nach Einstellung, Rueckgaengig-Verlauf leeren,
  `FelderExternErgaenzt` (offenes Formular und Objektakte zeichnen neu). Vorher meldete die
  Schachtseite bei QGIS kein Ergebnis und zeichnete nichts neu, und QGIS liess auf BEIDEN Seiten
  das Projekt «ungeaendert»: keine Titelmarke, keine Rueckfrage beim Schliessen, kein Autosave,
  obwohl die Werte schon im Datensatz standen. Die Ergebnismeldung zeigt jede Seite in ihrer
  Statuszeile (Haltungen `SaveStatus`, Schaechte `LastResult`).
  Waechter: `SeitenUebernahmeTests` (QGIS beide Seiten echt, Reihenfolge, alle sechs Wege).
- **Rueckgaengig/Wiederholen**: `SeitenVerlauf.Wende` (auch der Fall «nicht vollständig»). Die
  Seite behaelt ihre Schranke. Tests: `DatenVerlaufShellTests` (Teilweise-Fall je Seite).
- **Aufklapp-Liste**: `AufklappListeController<TListe, TRecord>` mit `IAufklappListe<TRecord>`;
  `DataPageAufklappListeController`/`SchaechteAufklappListeController` liefern nur Datensaetze,
  Schranke und Layout. Den Konflikthinweis W01 zeigen beide Listen (`IAufklappListe.Hinweis`).
- **Gewollte Unterschiede** (nicht angleichen): Die Schachtseite sperrt Aenderungen zusaetzlich
  ueber `CanMutateShaftData` (laufender Protokollimport), die Haltungsseite nur ueber
  `IsProjectReady`. Nur die Haltungsseite hat Abdocken, die getrennte alte Suchzeile, die
  Mehrfachauswahl zum Loeschen. Die Schacht-Matrix braucht weder den
  Mehrfach-Massnahmen-Schutz noch das Leeren von Tabellenfeldern der Haltungs-Matrix:
  `schacht_costs.json` schreibt nur sie selbst, immer mit genau einer Massnahme, und sie schreibt
  keine Kostenfelder in die Schachtdatensaetze.
- **W01-Konfliktschutz auch im Schachtformular (02.10.2026).** Eine Formulareingabe, die auf
  einem aelteren Stand beruht, ueberschreibt keine neuere Tabellenkorrektur (Inline-Edit,
  Rueckgaengig, Uebernahme) mehr still. Regel und Ablauf stehen fuer beide Seiten an EINER Stelle:
  `FormularKonfliktschutz` (Ausgangswert vergleichen, bei Abweichung neuere Korrektur behalten
  und melden, sonst schreiben und den echten Datensatzwert uebernehmen). Folge der gleichen Regel:
  Lehnt die Seite das Schreiben ab (laufender Protokollimport, abgelehnte Umbenennung), zeigt das
  Schachtformular danach wieder den Datensatzwert, wie das Haltungsformular. Schachtfelder lesen
  fuer Aufbau, Live-Abgleich und Vergleich denselben Wert ueber alle Schreibweisen
  (`SchachtFeldnamen.AktuellerWert`, ueber `RecordDetailItem.LiesDatensatzwert`): die zuletzt
  geaenderte Schreibweise (`LastUpdatedUtc`), mitgezaehlt nur mit Inhalt oder bewusst leer (E3);
  ohne Zeitangabe die erste nicht-leere (Review PR #75: sonst ging eine Leer-Korrektur an einer
  Schreibweise verloren). Vorher leerte der Abgleich ein Feld, dessen Wert nur unter einer zweiten
  Schreibweise stand. Den Hinweis verteilt `SchaechteAnsichtUmschalter.MeldeKonflikt` (Liste oder
  Schublade). Das Detailfenster (`RecordDetailsWindow`, Haltungen und Schaechte) zeigt einen
  Konflikt aus seinen eigenen Feldern selbst unter dem Fensterkopf (`FormularKonfliktAnzeige`);
  nur ohne angemeldetes Formular geht er an die Seite.
  Waechter: `SchaechteFormularTabelleAbgleichTests`, `SchaechteFormularKonfliktIsolatedTests`.
- **Offen (Entscheid)**: Sprung von aussen (Dossier, Suche) klappt in der Schachtliste nicht auf
  wie bei den Haltungen (`ZeigeHaltung`).

## Optik und Bedienung professionell (28.09.2026)

Zweig `feature/optik-professionell` (Optikanalyse 28.09.2026, Auftrag Pascal «Setze alles
um»). Ziel: einheitliches, professionelles Erscheinungsbild und einheitliche Bedienwege im
ganzen Programm, ohne Fachlogik/Datenformate/Feldschluessel zu aendern.

- **Aufgabe 1 — Nova-Dialog statt Windows-MessageBox.** `Views/Windows/NovaDialogWindow`
  (`NovaDialogArt`: Info/Warnung/Fehler/Frage, `NovaDialogKnopfsatz`: Ok/JaNein/JaNeinAbbrechen)
  ist der EINE Meldungs-/Rueckfragedialog im Nova-Design: Symbol in Status-Farbe (Info/Frage
  `AccentBrush`, Warnung `WarningBrush`, Fehler `DangerBrush`), Titel, auswaehlbarer/kopierbarer
  Text (umbrechend, ab ~60 % Bildschirmhoehe scrollbar), feste Breite 460 px,
  `WindowFx.Entrance`, Owner = aktives Fenster (sonst Hauptfenster). «Kopieren» erscheint nur
  bei Fehlern, klein, links in der Fussleiste; ein gesperrtes Clipboard darf den Dialog nicht
  zum Absturz bringen (try/catch). `Services/DialogService` (`IDialogService`) ruft ihn ueber
  die statische Fassade `Views/Windows/NovaDialog` (`ZeigeInfo/ZeigeWarnung/ZeigeFehler/
  ZeigeBestaetigung/ZeigeWarnendeBestaetigung/ZeigeDreiWegeBestaetigung`) auf; Fenster ohne
  injizierten `IDialogService` (z. B. `DossierPreviewWindow`) rufen dieselbe Fassade direkt.
  **Marshalling nur an einer Stelle:** `DialogService.AufUiThread` ist der einzige erlaubte Ort
  fuer `Dispatcher.Invoke` — ein Aufruf vom Nicht-UI-Thread wird dorthin marshallt. Fehlt
  `Application.Current` oder ist dessen Dispatcher bereits heruntergefahren
  (Kommandozeilen-/Testumgebung), bleibt bewusst der alte `MessageBox.Show`-Weg als Rueckfall —
  ein WPF-Fenster braucht einen laufenden Application-Kontext.
  **Knopfregel bei Ja/Nein-Dialogen:** Die Reihenfolge ist IMMER `[Nein][Ja]`
  (`ConfirmCancel`: `[Abbrechen][Nein][Ja]`), «Ja»/«OK» bleibt ganz rechts. Der STANDARDKNOPF
  (Enter + Anfangsfokus) ist normalerweise «Ja»/«OK» (`PrimaryButton`, `IsDefault`). Bei
  `ConfirmWarn(defaultNo:true)` wird STATTDESSEN «Nein» zum Standardknopf (`IsDefault` +
  Fokus) — «Ja» bleibt an seinem Platz ganz rechts, verliert aber `PrimaryButton` und wird als
  `NovaDialogDangerButton` (Fensterressource, `DangerBrush`-Umriss) markiert: eine blau gefuellte
  Hauptaktion links vom eigentlichen Standardknopf waere ein zweites, widerspruechliches
  Signal. Esc loest immer denselben Knopf aus wie «Abbrechen» bzw. «Nein» bzw. «OK»
  (`IsCancel`); Klick auf das Fenster-X hat dieselbe Bedeutung (`Ergebnis` ist schon vor jedem
  Knopfklick auf diesen Wert vorbelegt). Standardtitel tragen den echten Umlaut
  («Bestätigung»). Waechter `DesignAuditDialogeTests`: Ausserhalb von `DialogService.cs` und
  `NovaDialog*` darf im ganzen UI-Projekt kein `MessageBox.Show` mehr stehen (vor der Aufgabe
  nur zwei Fundstellen im ganzen Projekt — dieselben zwei, die jetzt die Ausnahme bilden).
  Test `NovaDialogWindowIsolatedSmokeTests` (Kindprozess-Muster) prueft Knopfbeschriftungen,
  Reihenfolge, Standard-/Abbrechen-Knopf, Rueckgabewerte per programmatischem Klick und die
  Darstellung im Dunkeltheme (Hintergrund = `CardBrush` des Dunkeltheme).
- **Aufgabe 2 — Toast statt Dialog fuer reine Erfolgsmeldungen.** Regel: eine reine, vollstaendige
  Erfolgsmeldung (nichts zu entscheiden, nichts wurde ausgelassen) wird ein Toast mit optionaler
  Aktion «Ordner öffnen»/«Datei öffnen», nie ein Dialog. Ein TEILerfolg, eine Warnung oder etwas,
  das eine bewusste Entscheidung braucht, bleibt ein Dialog — ein Toast verschwindet von selbst
  und darf so etwas nicht verschlucken. Beispiel `DiagnosticsPageViewModel.CreatePackageAsync`
  (Fix-Runde 1): `result.Success && result.SkippedLogFileCount == 0` (kein Teilerfolg — keine
  einzelne Logdatei musste ausgelassen werden) toastet ueber `IToastService.Success(...)` mit der
  Aktion «Datei öffnen» (`ExplorerRevealService.TryReveal`); `result.Success` mit ausgelassenen
  Logdateien bleibt `_dialogs.Info(...)`, ein Fehlschlag `_dialogs.Warn(...)`. Diese Unterscheidung
  gilt programmweit als Vorbild fuer neue Erfolgs-/Fehlermeldungen, nicht nur an dieser Stelle.
- **Aufgabe 3 — Fensterregel: gemeinsamer Kopf und Knopfleiste (Teil 1, Grundlage +
  Dossier-Fenster).** Regel fuer JEDES Fenster im Programm: **Seiten verwenden
  `NovaPageHeader`** (Titel + Untertitel INLINE nebeneinander, kein Zeilenumbruch, Ellipsis —
  unveraendert), **Fenster (`Window`) verwenden das neue `Controls/NovaDialogHeader`**
  (Titel oben, darunter ein UMBRECHENDER Untertitel fuer den bisherigen Einleitungssatz,
  kollabiert restlos bei leerem Untertitel, rechts optional ein Aktionsbereich). Lookless
  Control nach demselben Muster wie `NovaPageHeader`/`StatusHost`; sein Stil liegt bewusst
  IN `Theme/Controls.xaml` (nicht in einer eigenen mitgemergten Datei wie `NovaPageHeader`),
  weil dort schon `StatusHost` denselben Weg geht. Grund fuer ein zweites Kopf-Control statt
  Wiederverwendung von `NovaPageHeader`: dessen Untertitel ist absichtlich einzeilig/inline
  fuer kurze Seiten-Taglines: die meisten Fenster-Einleitungssaetze sind ganze erklaerende
  Saetze, die dort truemmerhaft abgeschnitten wuerden.
  **`DialogButtonBar`** (`Style x:Key`, `TargetType="Border"`, in `Controls.xaml`) ist die
  gemeinsame Fussleiste: Trennlinie oben (`BorderLightBrush`, `BorderThickness="0,1,0,0"`),
  16 px Abstand darunter (`Padding="0,16,0,0"`). Genaues XAML-Muster (mechanisch zu
  uebernehmen, auch fuer Aufgabe 4):

  ```xml
  <Border Grid.Row="…" Style="{StaticResource DialogButtonBar}">
      <StackPanel Orientation="Horizontal" HorizontalAlignment="Right">
          <Button Content="Abbrechen" Style="{StaticResource SecondaryButton}"
                  Margin="0,0,8,0" IsCancel="True" Click="OnCancel"/>
          <Button Content="Speichern" Style="{StaticResource PrimaryButton}"
                  IsDefault="True" Click="OnSave"/>
      </StackPanel>
  </Border>
  ```

  Ein zusaetzlicher Statustext links steht als weiteres Grid-Kind mit
  `HorizontalAlignment="Left"` im selben `Grid` (Muster wie das bisherige `CountText` in
  `DossierHoldingPickerWindow`/`DossierShaftPickerWindow`), NICHT im `StackPanel`. 8 px
  Abstand zwischen Knoepfen bleibt ein manuelles `Margin="0,0,8,0"` an jedem Knopf ausser dem
  letzten (WPF `StackPanel` kennt in dieser .NET-Version keine `Spacing`-Eigenschaft — belegt
  am Referenzassembly geprueft, nicht angenommen) — kein Setter dafuer, damit die Reihenfolge
  im XAML sofort sichtbar bleibt.
  **Primaerstil vereinheitlicht:** Fenster-Hauptknopf = `PrimaryButton`. Neu:
  **`DangerButton`** (`Controls.xaml`, `BasedOn SecondaryButton`, `Foreground=DangerTextBrush`,
  `BorderBrush=DangerBrush`) ist optisch identisch mit dem bisherigen fensterlokalen
  `NovaDialogDangerButton` aus Aufgabe 1 — bewusst NICHT dorthin verschoben (Aufgabe 1 bleibt
  unangetastet), aber ab sofort die Vorlage fuer jede neue destruktive Aktion.
  **`CompactButton`** hatte in `DataPage.xaml` eine lokale Doppeldefinition
  (`BasedOn SecondaryButton`, kompakt) neben der Theme-Fassung (`BasedOn ToolbarButton`,
  Pillenform) — beide sahen verschieden aus. Die Theme-Fassung (`Theme.xaml`/`ThemeLight.xaml`)
  ist jetzt an die lokale angeglichen, die lokale Kopie in `DataPage.xaml` entfernt.
  **Knopfregel** (Plan-Konstante, ab jetzt verbindlich, Wächter folgt vollstaendig in
  Aufgabe 4): Knopfleiste unten rechts, Hauptaktion ganz rechts (`PrimaryButton`,
  `IsDefault="True"`), «Abbrechen»/«Schliessen» direkt links davon (`IsCancel="True"`),
  weitere Aktionen links davon. **Hoechstens EIN Hauptknopf je Fenster** — traf ein Fenster
  bereits vorher einen mittigen Knopf mit `IsDefault="True"` (z. B. «Daten holen» in
  `DossierParcelLookupWindow`), verliert DIESER es zugunsten des Fuss-Hauptknopfs, damit
  Enter nicht auf zwei Knoepfe gleichzeitig zielt. **Der dadurch verlorene Enter-Weg wird NICHT
  ersatzlos gestrichen:** `DossierParcelLookupWindow` faengt Enter in genau den betroffenen
  Eingabefeldern (`MunicipalityBox`/`ParcelBox`, `KeyDown="OnLookupInputKeyDown"`) gezielt ab,
  ruft denselben Weg wie der frühere Knopf auf und markiert `e.Handled = true`, damit es nicht
  zusaetzlich beim eingebauten Standardknopf-Mechanismus ankommt. Ausserhalb dieser Felder greift
  unveraendert der `IsDefault`-Fusshauptknopf. Test `DossierParcelLookupWindowKeyboardIsolatedSmokeTests`
  (Kindprozess, echtes `KeyEventArgs(Key.Enter)` per `RaiseEvent`, Muster wie
  `ListenReihenfolgeIsolatedTests`).
  Angewendet auf alle 11 Dossier-Fenster (Area, Batch, Edit, HoldingPicker, PageSelection,
  ParcelLookup, Plan, Preview, PrintDialog, Refresh, ShaftPicker): Kopf mit Titel + bisherigem
  Einleitungssatz als Untertitel (leer bei `DossierEditWindow` — dessen doppelte
  „Liegenschaft“-Ueberschrift im Formular entfaellt dafuer), Fussleiste nach Knopfregel.
  Dabei aufgefallene und mitbehobene Abweichungen vom alten Bestand: `DossierBatchWindow`
  und `DossierRefreshWindow` hatten den Hauptknopf LINKS vom Abbrechen-Knopf stehen (jetzt
  vertauscht); `DossierPageSelectionWindow` hatte zwei fern voneinander stehende
  Knopfgruppen (jetzt EINE rechtsbuendige Leiste: Alle waehlen, Keine, Abbrechen, Erzeugen);
  `DossierPrintDialog` verwendete `SuccessButton` (jetzt `PrimaryButton`, dazu `IsDefault`/
  `IsCancel` ergaenzt, die vorher fehlten). Wächter `DesignAuditDossierFensterTests` (Kopf,
  Fussleiste, genau ein Hauptknopf mit `PrimaryButton`, ein Abbrechen-/Schliessen-Knopf, kein
  `SuccessButton` mehr, `CompactButton`-Angleichung) und
  `NovaDialogHeaderIsolatedSmokeTests` (Kindprozess-Muster: Titel/Untertitel-Kollaps,
  Aktionen-DataContext, `DialogButtonBar`-Masse, `DangerButton`-Farben).
- **Aufgabe 4 — Fensterregel Teil 2 (uebrige Fenster) + umfassender Waechter.** Die Knopfregel
  aus Aufgabe 3 gilt fuer ALLE Fenster, nicht nur die Dossier-Fenster: 19 weitere Fenster in
  `Views/Windows`, `Views/*.xaml` und `Dialogs/*.xaml` (u. a. `ObservationCatalogWindow`,
  `RecordDetailsWindow`, `SanierungsmassnahmenWindow`, `SchachtMassnahmenWindow`/
  `-KatalogEditorWindow`, `StrassenUebernahmeWindow`, `TextPreviewWindow`, `VerteilenWindow`,
  `XtfExportVorschauWindow`, `ProtocolCodePickerDialog`, `ProtocolEntryEditorDialog`,
  `ProtocolHistoryWindow`, `ProtocolObservationsWindow`) wurden auf `NovaDialogHeader` +
  `DialogButtonBar`/Knopfregel umgestellt. **Fenster mit eigener Werkzeugleiste**
  (`TrainingCenterWindow`, `TrainingStudioWindow`, `VideoAnalysisPipelineWindow`,
  `VsaCodeExplorerWindow`, `ProtocolObservationsWindow`) behalten ihre Arbeitsflaeche; nur Kopf
  und Abschluss-Knopfleiste wurden vereinheitlicht. `RecordDetailsWindow` blendet den
  eingebauten Kopf von `RecordDetailsView` dafuer aus (`IsHeaderVisible=False`, dasselbe Muster
  wie `DataPageNovaWorkspaceController`) und zeigt stattdessen den gemeinsamen Fensterkopf.
  `VideoAnalysisPipelineWindow` behaelt seine bewusst gestaltete Sci-Fi-Kopfzeile (NeuralSphere,
  Akzentbalken) unveraendert; seine lokalen `BtnPrimary`/`BtnCancel`-Stile sind entfernt, die
  Fussleiste nutzt jetzt `PrimaryButton`/`SecondaryButton` samt `IsDefault`/`IsCancel`, und der
  Abdocken-Knopf im Kopf verwendet seit der Nachbesserung `Style="{StaticResource
  ToolbarButton}"` statt eines eigenen `Button.Template` mit lokalem `Background=` (genau wie
  `FloatingGridWindow`s Andocken-Knopf) — die uebrige Arbeitsflaeche bleibt unveraendert.
  `TextPreviewWindow` verwendet fuer `DialogButtonBar`/`SecondaryButton` bewusst
  `DynamicResource` statt `StaticResource` (wie `FloatingGridWindow` in Aufgabe 4a): Der
  bestehende `WindowOpenCloseSmokeTests` instanziiert dieses Fenster ohne laufende
  `Application`, eine `StaticResource` waere dort beim BAML-Laden eine haerte Ausnahme.
  **Farbige Aktionsknoepfe bekamen programmweite Theme-Stile statt lokalem `Background=`**
  (Fix-Runde 1, Nachbesserung): `WarningButton` (Controls.xaml, `BasedOn SecondaryButton` wie
  das bestehende `DangerButton` — Umriss statt Flaeche, `WarningTextBrush`/`WarningBrush`,
  Kontrast auf `CardBrush` gemessen und in `DesignAuditContrastTests` festgehalten: 6,28:1
  dunkel / 5,02:1 hell) sowie `Severity1Button` … `Severity5Button` (eigene Stilfamilie fuer die
  fuenf Schadensstufen-Knoepfe in `TrainingStudioWindow`, kein Primary/Secondary/Danger/Warning
  — eine 5-stufige Farbskala ist ein Stufen-Auswahlwidget, keine der vier semantischen
  Aktionsklassen). `TrainingCenterWindow`s Selbsttraining-Knoepfe (Start/Pause/Stop) verwenden
  jetzt `SuccessButton`/`WarningButton`/`DangerButton` statt `Background="{DynamicResource
  Success/Warning/DangerBrush}" Foreground="White"` direkt am Knopf und sind dadurch komplett
  aus der Waechter-Ausnahmeliste entfernt (0 Regelverstoesse). `TrainingStudioWindow` folgt
  demselben Muster fuer seine bisher primaerklassigen Knoepfe ausserhalb der eigentlichen
  Codier-Entscheidung: „Durchgang starten“, „Foto mit gewaehltem Modell pruefen“ und
  „Codieren… (Katalog)“ sind jetzt `ToolbarButtonAccent` (passend zur toolbar-lastigen
  Arbeitsflaeche daneben), „Weiteres Ereignis auf diesem Bild“ ist `SecondaryButton`, „Bild
  fertig“ `ToolbarButtonAccent` — nur „Akzeptieren (A)“ (`SuccessButton`) und „Korrektur
  speichern (K)“ (`PrimaryButton`) bleiben primaerklassig (siehe Waechter-Ausnahme unten).
  **Waechter `DesignAuditKnopfleistenTests`** deckt ALLE Fenster-XAMLs im UI-Projekt ab
  (`Views/*.xaml` nur oberste Ebene, `Dialogs/*.xaml`, `Views/Windows/*.xaml`; nur echte
  `<Window`-Wurzeln, UserControls/ResourceDictionaries im selben Ordner fallen automatisch weg)
  und prueft vier Regeln je Fenster: (a) hoechstens EIN primaerklassiger Knopf — `PrimaryButton`
  UND `SuccessButton` zaehlen zusammen (SuccessButton ist `BasedOn PrimaryButton`, dieselbe
  gefuellte Sichtgewichtsklasse, nur andere Akzentfarbe); `DangerButton`/`WarningButton`
  (`BasedOn SecondaryButton`, Umriss) sowie `ToolbarButtonAccent` und die
  `Severity1..5Button`-Stile zaehlen NICHT mit, (b) traegt ein Fenster ein `IsDefault="True"`,
  steht der unmittelbar vorangehende `<Button`-Tag (Dokumentreihenfolge, kein anderer Button
  dazwischen) mit `IsCancel="True"` da, (c) kein lokal definierter `Style x:Key="..."
  TargetType="Button"` mehr im Fenster, (d) kein `Background=` direkt an einem `<Button`-Tag.
  **Ausnahmen sind regelgranular, nicht Datei-weit** (Fix-Runde 1: die urspruengliche
  Datei-weite Liste war zu grob — drei der fuenf zuvor ausgenommenen Fenster
  (`ObjektakteWindow`, `FloatingGridWindow`, `TrainingCenterWindow`, `VideoAnalysisPipelineWindow`)
  hatten nach der Nachbesserung schlicht KEINEN Regelverstoss mehr und sind komplett aus der
  Liste entfernt): eine Ausnahme nennt Datei UND genau die Regel(n) `[Flags] enum Regel`, von
  denen sie befreit ist — alle anderen Regeln gelten unveraendert weiter. Nur die geschuetzten/
  aus dem Auftrag ausgeschlossenen Fenster bleiben Datei-weit ausgenommen: die vier Video-Fenster
  (`PlayerWindow`, `LiveFrameWindow`, `StartupSplashWindow`, `PhotoMeasurementWindow`) und die
  drei geschuetzten WebGIS-Fenster (`WebGisVorschauWindow`, `WebGisSchreibBestaetigungWindow`,
  `WebGisHolenWindow`). Zwei echte regelgranulare Ausnahmen bleiben: `NovaDialogWindow` NUR
  Regel (c) — der lokale Stil `NovaDialogDangerButton` (Aufgabe 1) ist laut CLAUDE.md-Entscheid
  bewusst NICHT zum programmweiten `DangerButton` verschoben, `IsDefault`/`IsCancel` werden dort
  vollstaendig im Code-Behind gesetzt (kein XAML-`IsDefault`, Regel (b) hat nichts zu pruefen);
  `TrainingStudioWindow` NUR Regel (a) — „Akzeptieren (A)“ und „Korrektur speichern (K)“ sind
  zwei gleichwertige, gemeinsam sichtbare/aktivierte Abschluesse DESSELBEN Codierschritts (KI-
  Vorschlag war richtig vs. wurde korrigiert), kein Rang zwischen beiden. Der Waechter faengt
  jede der vier Regelverletzungen nachweislich ab (Sabotageprobe: zweiter Primaerknopf,
  vertauschte Abbrechen/Speichern-Reihenfolge, lokaler Button-Stil, `Background=` direkt am
  Button — alle vier Male rot, nach Ruecknahme wieder gruen) UND dass eine regelgranulare
  Ausnahme nicht auf andere Regeln derselben Datei ausstrahlt (lokaler Stil in
  `TrainingStudioWindow`, das nur von Regel (a) befreit ist, faengt Regel (c) trotzdem ab).
- **Aufgabe 5 — Programmidentitaet.** Das Fenstersymbol kommt jetzt aus `Assets/Brand/app.ico`
  (`App.LoadDefaultWindowIcon`, dieselbe Datei wie `ApplicationIcon` im csproj) statt aus dem
  Uri-Wappen-PNG; das Kundenlogo bleibt in Berichten/Dossiers/Excel unveraendert das Wappen —
  nur das Fenstersymbol selbst wechselt. `LoadDefaultWindowIcon` ist `internal` (statt `private`),
  damit ein zweiter Aufrufer dasselbe geladene Symbol wiederverwenden kann, ohne die Ladelogik zu
  duplizieren.
  Neues Fenster **„Über SewerStudio"** (`Views/Windows/AboutWindow`, `NovaDialogHeader` +
  `DialogButtonBar`, Knopfregel: fuenf `SecondaryButton`, kein Hauptknopf, „Schliessen"
  `IsCancel="True"`). Die reinen Angaben (Name/Version aus `AppIdentity`, Build-Datum aus dem
  Schreibzeitpunkt der ausgefuehrten Assembly-Datei — kein neues Assembly-Attribut noetig,
  Windows/.NET aus `RuntimeInformation`, optional GPU-Name vom Aufrufer, drei Ordner aus
  `AppSettings.AppDataDir`) liefert der WPF-freie `Services/AboutInfoProvider`
  (`AboutSystemInfo`), damit sie ohne WPF-Testprozess pruefbar bleiben. **Kein Git-Commit**: Das
  Projekt hat kein Assembly-Attribut dafuer (kein `AssemblyMetadata`/`InformationalVersion` mit
  Commit-Hash); das neu einzufuehren waere neue Build-Infrastruktur, nicht „ohne Aufwand
  verfuegbar" — deshalb bewusst weggelassen statt erfunden. Der Ordner „Einstellungen" zeigt
  denselben Pfad wie „Daten" (`settings.json` liegt direkt in `AppDataDir`, kein eigener
  Unterordner) — ehrlich derselbe Ordner statt ein erfundener zweiter Pfad. Die drei
  „Öffnen"-Knoepfe nutzen den bestehenden `SettingsPathWorkflow.OpenFolder(path, dialogs)` (wie
  die Einstellungsseite); „Systeminfo kopieren" faengt eine gesperrte Zwischenablage ab wie
  `NovaDialogWindow.OnKopieren` (Aufgabe 1). Geoeffnet wird ueber `ShellViewModel.
  ShowAboutCommand` (`Monitor.GpuName` synchron mitgegeben, kein neuer Sensor) — noch OHNE
  Hilfe-Menuepunkt; Aufgabe 6 verdrahtet ihn.
  **Schreibweise „SewerStudio"** (kein Leerzeichen) programmweit in sichtbaren Texten: die
  Seitenleisten-Wortmarke zeigt „SewerStudio" (Grossbuchstaben-Look kam nur vom Literaltext
  „SEWER STUDIO", nicht von echtem Kapitaelchen-Styling — `FontFamily=FontMono`/`Bold` bleiben),
  `AiDocumentationPdfBuilder` (PDF-Kopf/Fuss) und die `AiStartupOrchestrator`-Warnung sind
  korrigiert. Waechter `DesignAuditProgrammidentitaetTests` prueft ALLE sichtbaren XAML-Attribute
  (`Text`/`Content`/`Title`/`Header`/`ToolTip`) im UI-Projekt auf „Sewer Studio"/„AuswertungPro" —
  bewusst NUR XAML, nicht C#-Quellcode (dort steht „AuswertungPro" programmweit in jedem
  Namensraum/`using`, eine Textsuche dort waere kein sinnvoller Waechter fuer Oberflaechentexte).
  Statuszeile `MainWindow.xaml`: „Schaechte" → „Schächte" und die tote, mit
  `Visibility="Collapsed"` ausgeblendete Doppelzeile (dieselben zwei Werte nochmals, MIT
  Umlauten) sind entfernt. Die Zeile unter der Wortmarke (`SpeicherstandText`) zeigte bereits den
  Projektnamen, nicht den Dateinamen; sie bekommt zusaetzlich den vollen Projektpfad als ToolTip
  (`ShellViewModel.Nova.cs`, neue Eigenschaft `ProjektPfad` aus `_sp.Settings.LastProjectPath`,
  keine neue Fachlogik).
  **Im isolierten WPF-Kindprozess kann `Dispatcher.Invoke(…, DispatcherPriority.ApplicationIdle)`
  nach `Show()` echt haengen bleiben** — real gemessen an `AboutWindowIsolatedSmokeTests`: der
  Aufruf blockierte dort 60 s ohne Ausnahme, mit UND ohne `ui:WindowFx.Entrance="True"` am
  Fenster; die genaue Ursache ist damit NICHT auf die Eintritts-Animation eingegrenzt (die
  urspruengliche Vermutung anhand des Kommentars in `BeobachtungenWindowIsolatedSmokeTests` hat
  sich bei der Gegenprobe ohne `WindowFx.Entrance` nicht bestaetigt). Belegt und ab sofort die
  Regel: nach `Show()` in einem `[IsolatedWpfFact]`-Kindprozess `UpdateLayout()` und
  `Assert.True(window.IsLoaded)` statt eines `ApplicationIdle`-Invoke verwenden (Muster wie
  `BeobachtungenWindowIsolatedSmokeTests`); `DispatcherPriority.Send` liess in derselben Probe
  denselben Aufruf sofort durchlaufen, ist als Test-Ruhepunkt aber bedeutungslos (er wartet auf
  nichts). Nie ungeprueft annehmen, dass ein bestehender ApplicationIdle-Invoke-Test (z. B.
  `NovaDialogHeaderIsolatedSmokeTests`) denselben Weg fuer ein neues Fenster sicher macht.
  Test `AboutWindowIsolatedSmokeTests` (Kindprozess-Muster).
- **Aufgabe 6 — Hilfe-Menü, F1, Tastenkürzel, Handbuch.** Neues Menü «_Hilfe» in `MainWindow.xaml`
  (Handbuch F1, Tastenkürzel Strg+F1, Über SewerStudio) nach `_Ansicht`. **`HandbuchInhalt`**
  (`Services/HandbuchInhalt.cs`, WPF-frei) ist die EINE Textquelle des Handbuchs: ein
  `HandbuchAbschnitt` je Seite der Leiste, `Schluessel` ist wortgleich der `NavItem.Title` aus
  `ShellViewModel.NavItems` (15 Seiten im aktuellen Stand — der Brief nennt „16“, zählt aber nur
  15 Namen auf; massgeblich ist der Code), dazu genau EIN Abschnitt `FachleuteSchluessel`
  («Für Fachleute (technisch)», `IstFachlich=true`) mit dem verschobenen Entwicklermaterial
  (Umgebungsvariablen, Sidecar, Modell-Registry, Merge-Engine) aus dem alten Einstellungen-Reiter
  „Hilfe“. Jeder Seitentext ist gegen die echte XAML der Seite geprüft (NovaPageHeader-Untertitel,
  „Weitere Aktionen“-Menüpunkte) statt vom alten, teils veralteten Hilfetext übernommen.
  **`HandbuchWindow`** (nicht-modal, Einzelstück über `HandbuchWindow.ZeigeAn(seitenSchluessel)`
  — ein bereits offenes Handbuch wird nur weitergeschaltet und aktiviert statt neu geöffnet,
  Muster wie `DataPage.ShowOrUpdateBeobachtungenWindow`) zeigt links ein nach
  `ShellNavigationGroups` gruppiertes Inhaltsverzeichnis, rechts den Text, oben im
  `NovaDialogHeader`-Aktionsbereich ein Suchfeld (filtert Titel UND Fliesstext). Der
  Fachleute-Abschnitt steckt beim Anzeigen in einem `Expander` mit `IsExpanded=False`
  („standardmässig zu“). F1 in `MainWindow` übergibt `SelectedNavItem.Title` als
  `CommandParameter`; ein unbekannter/leerer Schlüssel fällt über `HandbuchInhalt.Finde` auf
  „Übersicht“ zurück. Das `PlayerWindow` hat sein eigenes F1-Overlay
  (`PlayerShortcutOverlayController`) in einem eigenen Fenster und ist von der neuen
  `MainWindow`-Bindung unberührt.
  **`TastenkuerzelWindow`** (gleiches Einzelstück-Muster, `ZeigeAn()`) listet zuerst die globalen
  Kürzel (F11, Strg+N/O/S/K, F1, Strg+F1) und das Seiten-Kürzel F3 (Haltungen), danach je eine
  Gruppe „Videoplayer – <Kategorie>“ — gelesen aus
  **`PlayerKeyboardShortcutPolicy.Beschreibungen`** (`PlayerShortcutBeschreibung`: Action/Gruppe/
  Taste/Text), NIE hier von Hand abgeschrieben. Ein Test bindet jede
  `PlayerKeyboardAction` an mindestens eine Beschreibung, ein zweiter prüft im echten
  Kindprozess, dass jede Zeile der Policy wortgleich im Fenster ankommt.
  Der Einstellungen-Reiter „Hilfe“ ist auf einen kurzen Verweis plus die Knöpfe „Handbuch öffnen“/
  „Tastenkürzel anzeigen“ geschrumpft (`SettingsPage.xaml.cs`, ruft dieselben `ZeigeAn`-Einstiege).
  Tests: `HandbuchInhaltAbdeckungTests` (liest die `NavItem`-Titel per Regex direkt aus
  `ShellViewModel.cs` und prüft Abdeckung — kein ServiceProvider/keine WPF-App nötig),
  `PlayerKeyboardShortcutPolicyTests` (Beschreibungen-Abdeckung), `HandbuchWindowIsolatedSmokeTests`,
  `TastenkuerzelWindowIsolatedSmokeTests` (beide Kindprozess-Muster).
- **Aufgabe 7 — «Weitere Aktionen» gliedern, Doppelungen und Namen bereinigen.** Haltungen
  (`DataPage.xaml`) und Schächte (`SchaechtePage.xaml`) gliedern das Menü «Weitere Aktionen»
  jetzt in EXAKT denselben fünf Untermenüs, in derselben Reihenfolge: «Daten abgleichen ▸»,
  «Bearbeiten ▸», «Reihenfolge ▸», «Ansicht ▸», «Ausgabe ▸» (Wächter
  `WeitereAktionenUntermenueTests`, prüft `Button.ContextMenu` von `WeitereAktionenDropdown`
  über `XDocument`, keine Text-Heuristik). **Controller-Entscheid Fix-Runde 1 (28.09.2026):
  «Auffindbarkeit schlägt Entdoppelung»** — ein Eintrag, der einen Handler mit dem Zeilenmenü
  (`HaltungZeilenMenue`/`SchachtZeilenMenue`) teilt, darf SOWOHL dort ALS AUCH unter «Weitere
  Aktionen» stehen; zwei Wege zum selben Handler sind hier bewusst KEINE zu entfernende
  Doppelung. Schächte «Bearbeiten ▸» (Sanierungsmassnahmen…) und «Ausgabe ▸» (Protokoll
  (PDF)…, Gehe zu Ordner) rufen deshalb dieselben Handler wie das Zeilenmenü auf, das
  unverändert bleibt. Auch Haltungen «Bearbeiten ▸» ruft seit Fix-Runde 2 zusätzlich
  «Sanierungsmassnahmen…» auf (`Click="CostsMenu_Click"`, exakt derselbe Handler und dieselbe
  Schreibweise wie im Zeilenmenü) — das ehemalige `HaltungZeilenMenue`-Duplikat wurde also
  NICHT entfernt, sondern (wie bei den Schächten) zusätzlich unter «Weitere Aktionen»
  reichbar gemacht. **Ausnahme, die bestehen bleibt:** Haltungen «Sanierungsmaßnahme
  bearbeiten» (der eigene, abweichend benannte Menüpunkt mit demselben Handler) ist weiterhin
  entfernt — reiner Namens-Doppelgänger ohne Mehrwert neben «Sanierungsmassnahmen…» — und
  «Fokusmodus (F11)» ebenso (Doppelung zum globalen Menü _Ansicht, dieselbe
  `ShellViewModel.IsFocusMode`) — diese zwei bleiben die einzigen echten Streichungen.
  «Reihenfolge ▸» heisst auf BEIDEN Seiten «Nach oben»/«Nach unten» (nie «Hoch»/«Runter»);
  das gemeinsame Popup (Haltungen) bzw. die eingebetteten Eingabefelder (Schächte) für «Auf
  Position…»/«Gehe zu Zeile…» sind unverändert übernommen. **Reihenfolge-Popup mit zwei
  Zielfeldern (Fix-Runde 1):** `DataPage.NovaSucheUndReihenfolge.cs` — `ReihenfolgeMenu_Click`
  liest das `Tag` des Absenders (`"position"`/`"zeile"`) und merkt sich in
  `_reihenfolgeFokusZiel`, welches Textfeld `ReihenfolgePopup_Opened` fokussiert; ohne
  erkanntes Tag bleibt es beim Positionsfeld. Umbenennungen für sprechende Namen: Schächte
  «PDF-Daten» → «Stammdaten aus PDFs ergänzen», «Aktualisieren» → «Protokoll neu einlesen»;
  beide Seiten «Strassen» → «Strassennamen ergänzen»; Werkzeuge-Menü «Messvorlagen…» →
  «Massnahmenvorlagen…». «Vom WebGIS holen» nennt im Tooltip jetzt ausdrücklich «Ohne
  Anmeldung: zuerst auf der Seite Export am WebGIS anmelden.» (nur Text —
  `WebGisHolenAblauf.OeffneAsync` zeigte diesen Hinweis als Laufzeit-Dialog schon vorher,
  Befehlslogik unverändert). **Leiste ↔ Seitentitel:** `ShellNavigationTitles.Anzeige`
  (Schlüssel bleiben `"Dossiers"`/`"VSA"`) zeigt jetzt «Eigentümerdossiers»/«VSA-Bewertung»
  passend zu den bestehenden `NovaPageHeader`-Titeln; `HandbuchInhalt.Titel` der beiden
  Abschnitte folgt. Wächter: `WeitereAktionenUntermenueTests` (exakt dieselben fünf Titel in
  derselben Reihenfolge, «Reihenfolge» nennt auf beiden Seiten «Nach oben»/«Nach unten»,
  «Bearbeiten»/«Ausgabe» sind nicht leer), `ShellNavigationTitlesTests`, angepasste
  `DesignAuditCommandReachabilityTests`/`DesignAuditNovaSchaechteTests`/
  `DesignAuditThemeResourceTests`/`SchaechtePageProtocolToolbarTests`/
  `DesignAuditNovaHaltungenTests` (neuer Tag-Nachweis für das Reihenfolge-Popup).
- **Aufgabe 8 — Alte Ansichten aus den Menüs, neue Einstellungsgruppe «Frühere Ansichten».**
  «Alte Haltungsansicht» (Haltungen, `DataPage.xaml`, Untermenü «Weitere Aktionen ▸ Ansicht»),
  «Alte Schachtansicht» (Schächte, `SchaechtePage.xaml`, dasselbe Untermenü) und «Klassische
  Übersicht» (`MainWindow.xaml`, Menü «_Ansicht») sind aus ihren Menüs entfernt. «Aufklapp-
  Liste»/«Tabelle» bleiben unverändert im Untermenü «Ansicht» stehen. Die drei Schalter setzen
  weiterhin genau `AppSettings.ShowHaltungenNovaLayout`/`ShowSchaechteNovaLayout`/
  `ShowUebersichtNovaLayout` (`false` = alte Ansicht/klassisch) und stehen jetzt als drei
  Häkchen in einer neuen Gruppe «Frühere Ansichten» in Einstellungen ▸ Allgemein, direkt unter
  «Darstellung und Diagnose» (`SettingsPageViewModel.AlteHaltungsansicht`/`AlteSchachtansicht`/
  `KlassischeProjektuebersicht`, jeweils mit `OnXChanged` nach dem Muster von `ReduceMotion`/
  `HintergrundEngine`: sofort `_settings.Show…NovaLayout = !value` setzen und
  `SaveImmediate()`). Ein eigenes Live-Ereignis wie `MotionSettings.EngineChanged` ist dafür
  NICHT nötig: Haltungen, Schächte, Übersicht und Einstellungen sind eigene Seiten im selben
  Inhaltsbereich und können nicht gleichzeitig sichtbar sein — jede Navigation zu einer der drei
  Seiten baut sie über `ShellViewModel.NavItems` neu auf und liest die Einstellung dabei frisch
  (bei Übersicht schon bisher so: `ShowUebersichtNovaLayout` wird bei jedem `CreatePage()`
  ausgewertet). Die Änderung wirkt also beim nächsten Öffnen der jeweiligen Seite — bei
  Haltungen/Schächte/Übersicht ist „nächstes Öffnen" das einzig mögliche „sofort", weil man beim
  Umschalten zwingend auf der Einstellungen-Seite steht. `ShellViewModel.KlassischeUebersicht`
  bleibt als Kompatibilitäts-/Testeigenschaft bestehen (u. a. `OverviewPreviewPdfCommandTests`),
  bindet aber nirgends mehr an XAML.
  **Die beiden Umschalter `HaltungsansichtToggle`/`SchachtansichtToggle` bleiben als
  UNSICHTBARE `MenuItem`-Zustandshalter im Seitencode erhalten** (`Visibility="Collapsed"`, kein
  `Header` mehr, ausserhalb jedes `ContextMenu` direkt hinter dem geschlossenen
  `WeitereAktionenDropdown`-Button): `DataPageAnsichtUmschalter`/`DataPageDockingHost`
  (Haltungen, inkl. Abdocken-Sperre) und `SchaechteAnsichtUmschalter` (Schächte) lesen ihr
  `IsChecked`/`IsEnabled` weiterhin genauso wie zuvor — nur `InitNovaWorkspace`/
  `SchaechtePageViewModel`-Aufbau setzt `IsChecked` jetzt ausschliesslich aus
  `AppSettings.Show…NovaLayout`, nie mehr ein Klick. Diese vier Klassen wurden bewusst NICHT
  umgebaut (kein Typwechsel von `MenuItem` auf einen eigenen POCO): Das hätte
  `DataPageAnsichtUmschalterTests` (deckt `WendeAn()`, Abdocken-Sperre, Konfliktweiche
  vollständig mit echten WPF-Controls ab) unnötig invasiv gemacht, ohne einen Verhaltensvorteil.
  Wächter: `DesignAuditNovaHaltungenTests.Der_Umschalter_zur_alten_Haltungsansicht_ist_kein_
  sichtbares_Menue_mehr`, `.Das_Menue_fuehrt_Aufklapp_Liste_und_Tabelle`,
  `DesignAuditNovaSchaechteTests.Ansicht_Liste_und_Tabelle_stehen_als_Gruppe`,
  `.Werkzeugleiste_zeigt_nur_Hauptaktionen_und_ein_Menue_Weitere_Aktionen` (kein
  `Header="Alte Schachtansicht"` mehr), `SettingsPageViewModelFruehereAnsichtenTests` (Häkchen
  setzen/lesen den richtigen Schalter, sofortiges Speichern, Häkchen sind unabhängig
  voneinander). `HandbuchInhalt` (Übersicht, Haltungen, Schächte, Einstellungen) verweist auf
  den neuen Ort.
- **Aufgabe 9 — Export- und Import-Seite ordnen.** `ExportPage.xaml` gliedert sich jetzt in
  genau vier betitelte Abschnitte, in dieser Reihenfolge: «Excel-Listen», «Dateien verteilen»,
  «Kataster (XTF)», «WebGIS» — der `NovaPageHeader`-Untertitel nennt alle vier wörtlich. Jeder
  Abschnitt ist ein `Border` mit `x:Name="Abschnitt…"` (Konvention für den neuen Wächter
  `ExportImportAbschnittsKnopfTests`, siehe unten); alle bisherigen Bindungen/Befehle blieben
  wörtlich gleich, insbesondere der komplette WebGIS-Teil (`WebGis.*`, geschützt laut Global
  Constraints, geprüft von `ExportWebGisBereichTests`). Die Überschrift «Zielordner &
  Verzeichnisbaum» steht seither NUR NOCH direkt über dem echten Verzeichnisbaum
  (`ItemsControl ItemsSource="{Binding DistributionTargets}"`) im Abschnitt «Dateien
  verteilen» — vorher stand sie über der ganzen restlichen Seite (Kataster/WebGIS eingeschlossen)
  und war damit irreführend. Der Gemeinsame-Zielordner-Bereich für Excel ist in den Abschnitt
  «Excel-Listen» gewandert (vorher stand er, vom Excel-Export-Knopf getrennt, im
  Verzeichnisbaum-Bereich).
  **Die zwei XTF-Wege sprechen weiter Klartext**, jetzt zusätzlich mit einem sichtbaren
  Klartext-Satz, wann man welchen nimmt: «Bestehende Katasterdaten aktualisieren» nennt «Nimm
  diesen Weg, wenn oben schon eine Importkopie genannt ist…», «XTF erstellen» zeigt den bereits
  vorhandenen, bisher nirgends gebundenen `ExportPageViewModel.XtfNeuHinweis`
  (`XtfExportAuswahl.NeuHinweis`, reine Regel — unverändert, nur erstmals in der XAML
  angeschlossen). Die vier Fachbegriffe FME, `.ili`, DSS und SIA405/Zusatzmodell stehen nicht
  mehr als eigener Fliesstext, sondern nur noch in einem eingeklappten `Expander
  Header="Technische Details" IsExpanded="False"` im «XTF erstellen»-Weg. Alle bisher unstilisierten
  Knöpfe (beide XTF-Wege, «Paket für GEONIS erstellen», «Ordner öffnen», die vier
  WebGIS-Aktionsknöpfe) tragen jetzt `Style="{StaticResource ToolbarButton}"` statt gar keinen
  Stil. **Knopfregel je Abschnitt:** «Excel-Listen» behält seinen einen `ToolbarButtonAccent`
  («Export Haltungen.xlsx»); die übrigen drei Abschnitte haben bewusst KEINEN Akzentknopf (die
  «empfohlen»-Marke an den XTF-Wegen übernimmt dort die visuelle Führung) — das erfüllt
  «höchstens ein Akzentknopf je Abschnitt» ohne einen fragilen datengebundenen Stilwechsel
  zwischen zwei gleichrangigen Alternativwegen zu bauen.
  `ImportPage.xaml` bekam dieselbe `x:Name="Abschnitt…"`-Konvention auf seinen drei bereits
  bestehenden Karten (Normalfall, Einzelne Quellen, Nacharbeiten — diese Gliederung selbst
  stammt aus einer früheren, plan-externen Änderung und war schon vorher weitgehend so
  aufgeräumt). Die drei Berichtsknöpfe im Seitenkopf heissen jetzt einheitlich «Letzter
  Bericht», «Alle Berichte», «Bericht erstellen» (vorher «Import-Report» — der Knopf ruft
  `ExportImportSummaryCommand`/`IImportSummaryExporter` auf und erzeugt einen NEUEN CSV-Bericht
  über den aktuellen Projektstand; das ist bewusst ein anderer Bericht als «Letzter
  Bericht»/«Alle Berichte», die den TXT-Laufbericht eines Imports öffnen — nur der Name war
  uneinheitlich, nicht die Funktion). Die Katalogpfad-Zeile zeigt jetzt
  `ImportPageViewModel.CatalogStatusKurz` («VSA-Katalog geladen (2019)» bzw. «… nicht
  gefunden»/«… nicht konfiguriert»/«… Problem beim Laden»); die bisherige vollständige Fassung
  `CatalogStatus` (mit Pfad) bleibt unverändert als Quelle der Wahrheit erhalten und steht jetzt
  im `ToolTip` der Zeile. Der bestehende Zusammenfassungs-Bereich trägt den umbenannten,
  weiterhin standardmässig zugeklappten `Expander Header="Technische Details"` (vorher
  «Details», ohne erkennbaren Grund, warum er wichtig ist).
  **Stacktrace-Fix:** `ImportRunWorkflowController`s äusserer `catch (Exception ex)`-Block
  zeigte bisher `ex.Message` roh in der sichtbaren `SummaryText`-Zusammenfassung. Er ruft jetzt
  `UserError.DescribeAndReport(ex, "Import <Label>")` (`Application/Common/UserError.cs`,
  bereits vorhanden): liefert eine verständliche deutsche Meldung für die Anzeige UND loggt die
  volle Ausnahme zusätzlich über `BestEffort` (denselben Weg, den `App.xaml.cs` schon ans
  Programmlog anschliesst) — nichts geht verloren, nur die Oberfläche wird lesbar. Der volle
  `ex.ToString()` bleibt unverändert in `DetailsText` und ist damit nur noch im zugeklappten
  «Technische Details»-Bereich sichtbar, nie mehr in der Zusammenfassung.
  **Neuer Wächter `ExportImportAbschnittsKnopfTests`** definiert «Abschnitt» für diese Regel
  konkret: ein Container-Element mit `x:Name`, das mit dem Präfix `"Abschnitt"` beginnt; darin
  darf höchstens ein `<Button Style="{...Resource ToolbarButtonAccent}">` stehen, egal wie
  tief verschachtelt (Unter-Karten wie die zwei XTF-Wege zählen zum selben Abschnitt). Zwei
  Sabotageproben (zwei bzw. ein Akzentknopf im selben Abschnitt) belegen, dass der Wächter
  wirklich zählt statt zufällig grün zu sein. Weitere Tests:
  `ImportRunWorkflowControllerTests.RunAsync_unerwarteter_fehler_zeigt_verstaendliche_meldung_
  und_loggt_die_ganze_ausnahme` (verständliche Meldung sichtbar, `IOException`/Originaltext nur
  in `DetailsText`, zusätzlich im Programmlog). `DesignAuditNovaSeitenkoepfeTests` wurde für den
  neuen ExportPage-Untertitel angepasst (bewusste Änderung, siehe Aufgabenbeschreibung — nennt
  jetzt alle vier Abschnittsnamen statt der alten Kurzfassung «Excel, Verteilung, Kataster»).
  `HandbuchInhalt` (Abschnitte «Export»/«Import») beschreibt die neue Gliederung.
- **Aufgabe 10a — Sprache: Englisch raus, «ß» wird «ss», Einstellungen bekommen einen
  «Erweitert»-Bereich.** Rohe englische Entwicklerbegriffe in sichtbaren Texten (Training
  Center: «Approve/Reject/Set New» → «Akzeptieren/Ablehnen/Zurücksetzen auf Neu», «Samples» →
  «Trainingsdaten», «Review Queue» → «Prüfliste», «CaseId»-Spalte → «Fall», «YOLO Export...» →
  «YOLO exportieren…», «auto-approve»/«Knowledge Base» in Tooltips → «automatisch
  akzeptieren»/«Wissensdatenbank»; Fenstertitel «Pipeline Analysis» → «Videoanalyse»;
  Einstellungen «Drop late frames»/«Skip frames» → «Verspätete Bilder verwerfen»/«Bilder bei
  Rückstand überspringen»; drei «Reset»-Knöpfe bei den KI-Schwellwerten und einer im
  VSA-Codierfenster → «Standard»/«Zurücksetzen»; Medienkonflikte «Auto-Resolve (gelernt)»/
  «Mappings löschen» → «Alle gelernten Zuordnungen übernehmen»/«Zuordnungen löschen», inkl. des
  gebundenen Ergebnistexts). **Etablierte Feature-Eigennamen bleiben englisch** («Training
  Center», «Training Studio», YOLO/DINO/SAM/Qwen als Modellnamen, «Export»/«Import» als im
  Deutschen gleichlautende Wörter) — nur rohe, dem Anwender unverständliche Entwicklerbegriffe
  wurden ersetzt.
  **Einstellungen: «Erweitert (für Fachleute)».** Rein technische Einzelfelder/-gruppen
  (Codex-Agenten-Daten, `pdftotext.exe`, Codec-Threads, YOLO-/DINO-Schwellwerte, «Programm
  sichern» mit Quellcode) stehen jetzt je Reiter in einem eigenen, standardmässig zugeklappten
  `<Expander Header="Erweitert (für Fachleute)" IsExpanded="False">` am Ende des Reiters — kein
  Feld entfernt, kein Schlüssel/keine Bindung geändert, nur verschoben. Nur Reiter, die
  tatsächlich solche Felder enthalten, bekommen den Bereich (nicht jeder Reiter). Das
  Suchfeld-Textfeld `SucheBox` trägt einen Platzhalter «Einstellung suchen…» nach demselben
  Muster wie die globale Suche Strg+K (`MainWindow.xaml`: überlagerter `TextBlock`,
  `IsHitTestVisible="False"`, `DataTrigger` auf leeren Text).
  **`SettingsSearchController.Anwenden` klappt einen zugeklappten Erweitert-Bereich automatisch
  auf**, wenn eine Suche mindestens eine `GroupBox` darin sichtbar schaltet, und zurück zu, wenn
  die Suche geleert wird — eine neue `Aufklapper(TabItem)`-Hilfsmethode neben der bestehenden
  `Gruppen(TabItem)` nutzt dieselbe `Nachfahren`-Rekursion über den WPF-Logical-Tree, die auch
  durch einen kollabierten `Expander` hindurch findet. Ein neuer Erweitert-Bereich MUSS über
  eine `GroupBox` gehen (keine nackten Felder direkt im Expander), sonst findet ihn die Suche
  nicht. Test `SettingsSearchTests.Controller_klappt_einen_Erweitert_Bereich_bei_Treffer_darin_
  auf_und_wieder_zu`.
  **«ß» wird im ganzen sichtbaren Text zu «ss»** (Schweizer Schreibweise, wie an anderer Stelle
  in CLAUDE.md schon für den Export/Domain-Layer festgehalten). Der bestehende Umlaut-Wächter
  `DesignAuditFeinschliffTests.Sichtbare_Texte_verwenden_echte_Umlaute` hat dafür einen
  Geschwistertest `Sichtbare_Texte_enthalten_kein_scharfes_S` (gleiche Regex/gleicher Helfer,
  prüft nur auf `ß`) statt eines eigenen neuen Wächters. **Ausnahmen, die bewusst «ß» behalten
  (Programmlogik, nicht sichtbarer Text):** `.Replace("ß", "ss", …)`-Normalisierungen (u. a.
  `SettingsSearchMatcher`, `SchachtAbdeckungStkAutoFill`, `ZustandsklasseCellStyleFactory`),
  der Eingabe-Alias-Schlüssel `["weiß"]` in `LiveControlColorParser` (Farbname aus Text lesen)
  und der Identitätsvergleich `RecordDetailsModels.IstStrassenfeld` gegen echte, historisch mit
  ß gespeicherte Projektfeldnamen («Beide Schreibweisen kommen in echten Projekten vor») — diese
  drei Muster nie versehentlich mit umschreiben.
  **Rohe Feldnamen als Beschriftung:** `FieldCatalog.Definitions["Referenzpruefung"]` hatte den
  rohen Schlüssel wortwörtlich als `Label` (zweites Konstruktor-Argument) kopiert, sichtbar als
  Spaltenkopf/Feldbeschriftung ohne Umlaut. Behoben durch Ändern NUR des Labels
  (`"Referenzprüfung"`); der Schlüssel selbst (`ComboItems`-Lookup, `ColumnOrder`,
  Excel/CSV/XTF-Exportkopf) bleibt `"Referenzpruefung"` — Anzeigename und Datenschlüssel sind
  in diesem Katalog zwei getrennte Argumente, nie den Schlüssel für die Anzeige kopieren.
  Ae/oe/ue in C#-Laufzeitmeldungen (`UserError.cs` u. a.) und rohe `ex.Message`/`ex.ToString()`
  in der UI sind ein separater Schritt (Aufgabe 10b) und noch offen.
- **Aufgabe 10b — Ae/oe/ue in C#-Meldungen, rohe `ex.Message` über `UserError`.**
  `Application/Common/UserError.cs` (`Describe`/`DescribeAndReport`) ist jetzt selbst
  vollständig umlautrein — alle zwölf Antwortsätze (Zugriff verweigert, Datei/Ordner nicht
  gefunden, Pfad zu lang, Datei nicht verfügbar, Dienst nicht erreichbar, Daten
  beschädigt/ungültig, Arbeitsspeicher, nicht unterstützt, generischer Fallback). Dazu
  bereinigt: `App.xaml.cs` (Bereits-gestartet-Meldung), beide `Settings/*Workflow.cs` und alle
  `DataPage/*Controller.cs` sowie deren Dialogtitel/-texte (Auswählen-Dialoge, Löschen-Bestätigungen,
  Fehlermeldungen). **Rein technische/Log-Zeilen (`BestEffort.ReportWarning`, `request.Log`
  im Training Center) behalten bewusst ae/oe/ue** — sie sind nicht das sichtbare Ziel dieser
  Aufgabe, nur die kurze Statuszeile/der Dialog daneben wurde umgestellt. Ebenso bewusst
  unverändert: **Datenwerte, die exakt mit Excel-Farbregeln übereinstimmen müssen**
  (`DataPageDropdownOptionGroupFactory.cs`, `SchaechteDropdownCommandFactory.cs`,
  `DropdownOptionsStore.cs`: „Pruefung bestanden" bleibt ae, weil `ExcelReportStyle.Farbregeln`
  exakt diese Zeichenkette matcht — nur der Listentitel „Pruefungsresultat-Liste" wurde zu
  „Prüfungsresultat-Liste") und **`[Obsolete(...)]`-Hinweise** (Entwicklertext, kein
  Nutzertext). `ProjectPathResolver.EnsureWritableProjectPath` wirft absichtlich schon
  deutschen, umlautrichtigen Klartext als Ausnahmemeldung (`DataPageHoldingRenameController.cs`,
  `SchaechteShaftRenameController.cs` zeigen `ex.Message` deshalb weiterhin direkt, ohne
  `UserError` — ein Wrap hätte die spezifische Meldung durch den generischen Fallback ersetzt).
  **Rohe `ex.Message`/`ex.ToString()` in Dialog-/Status-/Toast-Sinks** (`DossierBatchWindow.xaml.cs`,
  `DossiersPageViewModel.Actions.cs`, mehrere `Ai/Training/*Workflow.cs`, drei
  `Ai/Live/LiveDetection*CommandWorkflow.cs`, `CodingEingabemarkerSubmissionWorkflow.cs`,
  `CodingProtocolPdfExportService.cs`) laufen jetzt durch `UserError.Describe`/
  `DescribeAndReport`. **Wo daneben schon ein technischer `Log(...)`-Aufruf mit dem vollen
  `ex.Message` existiert, bleibt der unverändert** (das ist die aufklappbare/Log-Ansicht, die
  Kurzmeldung daneben wird umgestellt) — kein doppeltes Loggen. `ExportWebGisBereich.cs`,
  `WebGisVorschauWindow.xaml.cs`, `WebGisHolenWindow.xaml.cs` und alle weiteren `WebGis*`-Dateien
  sind laut Auftrag geschützt und unangetastet. **~85 weitere `ex.Message`-Stellen bleiben
  bewusst offen**: eine erste Stichprobe zeigte, dass ein Teil davon (z. B.
  `CodingSessionStartWorkflow.cs`, `LiveDetectionStartupWorkflow.cs`) deliberate deutsche
  Domänenmeldungen weiterreicht (`InvalidOperationException`/`SidecarInsufficientVramException`
  mit eigenem Text) — ein pauschales `UserError`-Wrapping würde dort Informationsverlust
  bedeuten, und jede Stelle einzeln nachzuverfolgen sprengte den Rahmen dieser Aufgabe.
  `DesignAuditLaufzeittexteTests` (Application/UI.Tests) hat dafür zwei neue Prüfungen: `UserError.cs`
  steht jetzt zusätzlich in der vollständigen `Quellen`-Positivliste (jede Zeichenkette dort ist
  Nutzertext, keine Ausnahmen nötig); für die übrigen, gemischten Dateien liefert
  `BehobeneEinzeltexte` eine gezielte Rückfall-Sperre je Datei (ein konkreter, tatsächlich
  behobener Altwert darf nicht wiederkehren) statt der ganzen Datei — die bestehende
  Quellen-Prüfung verlangt sonst, dass JEDE Zeichenkette der Datei sauber ist, was bei
  Log-/Obsolete-/Datenwert-Mischdateien falsch rot würde.
- **Aufgabe 10b Fix-Runde 1 (Review) — `UserError.Describe` zeigt eigene Meldungen zentral,
  nicht mehr pro Aufrufstelle.** Zwei konkrete Regressionen aus der Prüfung: `CodingSessionService`
  wirft „Keine Codier-Session aktiv."/„Session ist nicht aktiv (…)." und
  `DocxPlaceholderFiller`/`DocxImagePlaceholderFiller` werfen „Die Word-Vorlage hat keinen
  Hauptteil." — beide `InvalidOperationException` mit fertigem deutschem Text, beide gingen durch
  das simple `UserError.Describe`-Fallback verloren. Statt jede der zwölf Aufrufstellen einzeln zu
  flicken, prüft `Describe` jetzt VOR dem `switch`, ob eine `InvalidOperationException`/
  `ArgumentException` nachweislich aus einer der drei unteren SewerStudio-Schichten geworfen wurde
  (`Exception.TargetSite?.DeclaringType?.Assembly`-Name exakt `AuswertungPro.Next.Domain`,
  `.Application` oder `.Infrastructure` — eine Allowlist mit den echten Assembly-Namen, kein
  Präfix): dann `"{ex.Message} Technische Details stehen im Programmlog."`, sonst wie gehabt die
  typbasierte Übersetzung. `UserFacingException` bleibt unverändert vorrangig (ihr Text ist immer
  der Nutzertext, ohne den technischen Zusatz).
  **Ein Präfix-Vergleich auf „AuswertungPro" reicht NICHT** — geprüft und verworfen: Er hätte auch
  jedes Testprojekt getroffen (`AuswertungPro.Next.UI.Tests` fängt ebenfalls mit „AuswertungPro"
  an) und `tools/AuswertungPro.MeasureCatalogCli`; umgekehrt heisst die UI-Assembly nicht
  „AuswertungPro.Next.UI", sondern „SewerStudio" (`AssemblyName` in der csproj).
  **Die UI-Schicht ist bewusst NICHT in der Allowlist**, obwohl ihre Assembly „SewerStudio" leicht
  ergänzbar gewesen wäre: `SettingsPathWorkflow.OpenFolderCore` wirft
  `throw new InvalidOperationException(result.Error ?? "Unbekannter Fehler")` — `result.Error`
  kommt aus `FolderOpenService.EnsureAndOpen` und kann im Fehlerfall ein rohes `ex.Message` eines
  fremden/OS-Fehlers sein, kein von uns verfasster Satz. Mit „SewerStudio" in der Liste wäre genau
  dieser durchgereichte Fremdtext ungefiltert erschienen (belegt: der bestehende Test
  `SettingsPathWorkflowTests.OpenFolder_failure_shows_error_dialog` schlug fehl, bis die Zeile
  wieder entfernt wurde). Die drei unteren Schichten werfen dagegen durchgehend fest verfasste
  Sätze, keine durchgereichten Fremdfehler — deshalb dort sicher, in der UI-Schicht nicht.
  **Vor der Umstellung wurden alle eigenen `throw new InvalidOperationException(`/
  `throw new ArgumentException(` in `src/` auf Englisch/Technik durchsucht** (383 Fundstellen);
  sechs echte Treffer wurden auf Deutsch übersetzt, weil sie sonst durch die neue Regel
  ungefiltert sichtbar geworden wären: `VisionPipelineClient.cs` („Failed to deserialize
  response…"), `CategoryWeights.cs` („Expected 8 weights."), `VideoFrameStream.cs` („Failed to
  start ffmpeg process."), `OfferHtmlToPdfRenderer.cs` („Template errors: "), `App.xaml.cs`
  („Services are not initialized."), `CodingAiController.cs` („Pipeline health monitor has not
  been started."). Neue Tests `UserErrorTests` (Pipeline.Tests): eigene `InvalidOperationException`
  (`Project.AddRecord`, Domain) und eigene `ArgumentException` (`CategoryWeights.FromArray`,
  Infrastructure) zeigen ihre Meldung + Hinweis; eine echt geworfene Framework-
  `InvalidOperationException` (`Enumerable.First()` auf leerer Liste) bleibt generisch. Eine
  Ausnahme, die nur KONSTRUIERT, nie GEWORFEN wird (`new InvalidOperationException(...)` ohne
  `throw`+`catch`), hat kein `TargetSite` und bleibt ebenfalls generisch — das deckt zugleich den
  Fall ab, dass eine in einer Test-Fixture geworfene Exception (Testassembly, nicht gelistet)
  korrekt generisch bleibt.
- **Aufgabe 10b Fix-Runde 2 — der erste Audit-Sweep (Fix-Runde 1) suchte nur zeilenweise und
  übersah dadurch eine zweite Stelle.** `VisionPipelineClient.GetAsync` (eine kleine
  Sidecar-Metadaten-Route neben der schon übersetzten `PostAsync`-Route) warf ebenfalls
  `"Failed to deserialize response from {endpoint}"`, aber mit der Zeichenkette in einer
  EIGENEN Zeile hinter `throw new InvalidOperationException(` — genau das Muster, das der
  ursprüngliche zeilenbasierte `grep`-Audit nicht fand. Übersetzt wie die Schwesterstelle:
  `"Antwort von {endpoint} konnte nicht gelesen werden."`.
  **Neuer Wächter `UserErrorEigeneMeldungenSpracheTests`** (Pipeline.Tests, WPF-frei) macht
  diese Fehlerklasse strukturell unmöglich statt sie erneut per Stichprobe zu suchen: Er
  durchsucht ALLE `new InvalidOperationException(`/`new ArgumentException(`-Konstruktionen in
  `Domain`/`Application`/`Infrastructure` (regex-basiert, klammer-/zeilenumbruch-tolerant —
  die Zeichenkette darf beliebig weit hinter der öffnenden Klammer stehen, solange sie das
  ERSTE Argument direkt ist) und schlägt fehl, sobald der Text mit einem typischen englischen
  Wort beginnt (`Failed|Cannot|Can't|Could not|Unable|Invalid|Unknown|Missing|Expected|No |
  The |Not |Only |Must |Value |Unexpected|Error`, gross-/kleinschreibungsunabhängig — Vorgabe
  des Reviews). Ein Fund muss entweder übersetzt oder in der Datei-Konstante `Ausnahmen` mit
  Begründung eingetragen werden (aktuell leer: kein bekannter Fund brauchte eine Ausnahme).
  Ein zweiter Test prüft, dass die Ausnahmenliste keine toten Einträge behält. Sabotageprobe
  bestanden: Die ursprüngliche englische Zeichenkette an `VisionPipelineClient.cs:326` wieder
  eingesetzt → Wächter meldet exakt Datei, Zeile und Text; danach zurückgesetzt.
  **Nie wieder ein rein zeilenbasierter `grep`-Audit für diese Fehlerklasse** — der Wächter
  läuft bei jedem Testlauf automatisch mit und ersetzt die einmalige Stichprobe dauerhaft.
- **Aufgabe 10b Fix-Runde 3 — `IstEigeneVorsaetzlicheMeldung` prüfte den Ausnahmetyp per
  `is`-Muster (`exception is (InvalidOperationException or ArgumentException)`), das auch
  UNTERKLASSEN trifft** (`ArgumentNullException`, `ArgumentOutOfRangeException`,
  `ObjectDisposedException : InvalidOperationException`, …) — genau wie ein
  `catch (ArgumentException ex)`-Block auch `ArgumentNullException` fängt. Diese Unterklassen
  sind Programmierfehler-Schutzklauseln mit Framework- oder knapp-englischem Text, keine von uns
  für Nutzer verfassten Sätze: `BestEffort.Try(null!, …)` (Application) wirft
  `ArgumentNullException` mit dem BCL-Text „Value cannot be null. (Parameter 'action')";
  `PhotoMeasurementAnglePlanBuilder.BuildAngleGeometry` (Application, `internal`) wirft bei einem
  nicht unterstützten `OverlayToolType` eine `ArgumentOutOfRangeException` mit dem selbst
  geschriebenen, aber englischen Text „Only LateralCircle and PipeBend are supported." — beide
  wären mit der alten `is`-Prüfung fälschlich als „eigene Meldung" durchgereicht worden.
  **Behoben durch exakten Laufzeittyp-Vergleich** statt Musterabgleich:
  `exception.GetType() == typeof(InvalidOperationException) ||
  exception.GetType() == typeof(ArgumentException)` — `GetType()` liefert immer den Laufzeittyp,
  der `==`-Vergleich mit `typeof(...)` lässt bewusst keine Unterklassen zu. Der Sprachwächter
  `UserErrorEigeneMeldungenSpracheTests` (Fix-Runde 2) brauchte dabei KEINE Änderung: sein Regex
  sucht ohnehin nur nach den exakten Klassennamen als Text und konnte eine Unterklasse wie
  `ArgumentNullException` schon aus Textgründen nie treffen — nur die Produktionslogik in
  `UserError.cs` war zuvor weiter gefasst als das, was der Wächter tatsächlich verifizierte. Seit
  der Korrektur sind beide deckungsgleich (eine frühere Angabe im Fix-Runde-2-Bericht, die dies
  schon vorher behauptete, war ungenau und wurde im Bericht richtiggestellt). Neue Tests
  `UserErrorTests`: eigene `ArgumentNullException`/`ArgumentOutOfRangeException` (echt geworfen
  aus Application-Code) zeigen weiterhin den generischen Satz, nicht ihren Rohtext.
  Sabotageprobe bestanden: mit der alten `is`-Prüfung schlugen beide neuen Tests exakt mit dem
  vom Review vorhergesagten Leck fehl („Value cannot be null…" bzw. „Only LateralCircle…").
  **Fazit: `is`-Musterabgleich und `GetType() == typeof(...)` sind bei Ausnahmetypen NICHT
  austauschbar** — `is` ist die richtige Wahl, wenn Unterklassen fachlich dazugehören sollen
  (z. B. „irgendein Argumentfehler"), `GetType() ==` die richtige Wahl, wenn nur die exakt
  konstruierten Basistypen gemeint sind, deren Text wir selbst verfasst haben.
- **Aufgabe 10c1 — Ae/oe/ue-Restbereinigung im ganzen UI-Projekt (nicht nur die 10b-Dateien).**
  Rund 150 ASCII-Ersatzwörter (waehlen, fuer, pruefen, Schaeden, Aenderungen, Goldpruefung,
  Pruefungsresultat als Fliesstext, Eintraege, Laenge, groesser, Faelle, beschaedigt, enthaelt,
  muessen, laesst, koennen, Schaerfe, Anschluesse, Fuellung, laedt, Qualitaet, Aufraeumen,
  nachgeruestet, aufloesbar, Saetze, Verfuegung, schlaegt, Zeitueberschreitung, duerfen,
  Schaetzung u.v.a., siehe `DesignAuditLaufzeittexteTests.Aufgabe10c1BereinigteWoerter`) sind
  jetzt in `src/AuswertungPro.Next.UI` auf echte Umlaute umgestellt — Kommentare, `///`-Doku
  und `[Obsolete(...)]`-Hinweise bleiben unveraendert ae/oe/ue (Entwicklertext).
  **Datenschluessel mit Unterstrich bleiben automatisch geschuetzt**, weil der Unterstrich ein
  Wortzeichen ist und die Wortgrenze `\b` direkt davor/danach aufhebt: `Ausgefuehrt_durch`,
  `Nova_Pruefung`, `Primaere_Schaeden`, `vsa.meter.quelle` (Punkt ist dagegen KEIN Wortzeichen,
  wirkt also wie ein Leerzeichen — `vsa.hoehe.mm`/`deckel.hoehe`/`haltungspunkt.hoehe` mussten
  deshalb einzeln als Ausnahme erkannt werden, nicht automatisch geschuetzt).
  **Bewusst NICHT umgestellt** (Grund jeweils genannt): `Ausgefuehrt`/`ausgefuehrt` — lebt
  parallel als Feldschluessel-Alias auch ohne Unterstrich
  (`SchachtSanierungPflichtfeldValidator.AusgefuehrtDurchAliases`,
  `SchachtFeldWert(record, "Ausgefuehrt durch", "Ausgefuehrt_durch")`) UND als echtes Label;
  genau 3 Stellen mit eindeutig anzeigendem Zweck (`BuilderPageFilterSummaryBuilder.cs` Filter-
  Zusammenfassung, `SchachtSanierungPflichtfeldValidator.cs` fehlende-Felder-Meldung,
  `SchaechtePageViewModel.cs` Fallback-Spaltenkopf) wurden EINZELN von Hand auf „Ausgeführt
  durch" umgestellt, die Alias-Arrays bleiben ASCII. `Schaechte`/`Uebersicht` bleiben ASCII-
  Navigationsschluessel (`ShellNavigationTitles.Anzeige` übersetzt sie separat) — nur echte
  Fliesstext-Stellen mit demselben Wort in einem laengeren Satz (z. B. „Schächte gespeichert.",
  die ToolTipDescription-Saetze in `ShellViewModel.NavigationSupport.cs`) wurden gefixt.
  `Eigentuemer` bleibt ueberall ASCII (dokumentierte, absichtliche Doppelspur: Excel-Vorlage
  fuehrt „Eigentümer" mit Umlaut, Katalog/Import/Code immer „Eigentuemer" — siehe
  `SchaechtePageViewModel.cs`-Kommentar „das Feld heisst Eigentuemer — beides ist dieselbe
  Spalte"). `Pruefungsresultat`/`Referenzpruefung` bleiben ASCII-Feldschluessel; die drei
  Excel-Farbregel-Werte „Pruefung bestanden" u. a. bleiben unveraendert (`ExcelReportStyle`
  matcht exakt). `geschaetzt`/`Gefuellt` bleiben interne Datenwerte (CodeMeta-Parameter bzw.
  Converter-Konstante), keine Beschriftung. `gruen`/`gruene` bleiben ASCII NUR als
  Eingabe-Alias-Schluessel in `LiveControlColorParser.NamedColors` (dieselbe Ausnahme wie das
  dort dokumentierte „weiss"/„weiß"-Paar); echte Fliesstext-Stellen („gruene Treffer") sind
  einzeln bereinigt. Reine Log-/Diagnose-Zeilen (`Logger.LogError/LogWarning`) bleiben ae/oe/ue.
  **Neuer Waechter** `DesignAuditLaufzeittexteTests.Aufgabe10c1_Bereinigte_Ersatzschreibweisen_fallen_im_gesamten_UI_Projekt_nicht_zurueck`
  durchsucht ALLE `.cs`-Dateien in `src/AuswertungPro.Next.UI` (ausser WebGis*/
  ExportWebGisBereich.cs) statt einer festen Dateiliste; er entfernt vor der Wortpruefung den
  Inhalt von `{...}`-Interpolationsausdruecken (sonst waere `{result.BereitsVollstaendig}` ein
  falscher Treffer) und ueberspringt Kommentar- und `[Obsolete(`-Zeilen.
  **Lehre aus der Umsetzung (Selbstkorrektur, drei Fehlerklassen):**
  (1) Ein automatisiertes Bereinigungsskript darf NIE auf Dateien laufen, die selbst eine
  ASCII-Wortliste als Pruefmuster enthalten — es korrigiert sonst sein eigenes Suchmuster
  weg (`DesignAuditLaufzeittexteTests.Aufgabe10c1BereinigteWoerter` und
  `DesignAuditFeinschliffTests.UmlautErsatz` wurden so einmal versehentlich zerstoert und
  aus `git show HEAD:...` wiederhergestellt).
  (2) Ein Testprojekt-weiter Lauf desselben Woerterbuchs darf nur auf Zeichenketten wirken,
  die tatsaechlich einen in DIESER Aufgabe geaenderten Produktionscode widerspiegeln — mehrere
  Tests pruefen Zeichenketten aus `Application`/`Infrastructure` (out of scope fuer 10c1, z. B.
  `ProtocolEntryValidator.cs`, `AutoApprovalService.cs`, `SafeShellOpenService.cs`,
  `CatalogPriceResolver.cs`, `ImportPlausibilitaetsTor.cs`, `ProtocolTrainingFileStore.cs`) oder
  reine C#-Bezeichner (Methodennamen wie `Waehle`/`Pruefe` in Architektur-Tests, die Quellcode
  als Text lesen) und wurden faelschlich mitgezogen; alle per echtem Testlauf gefunden und
  einzeln zurueckgesetzt. Alle Aenderungen an `tests/AuswertungPro.Next.Infrastructure.Tests`,
  `tests/AuswertungPro.Next.Pipeline.Tests` und `tests/ProjectModernizer.Tests` wurden komplett
  verworfen (ausserhalb des 10c1-Umfangs).
  (3) Architektur-Tests, die Quellcode als Rohtext mit ESCAPETEN Anfuehrungszeichen
  (`\"...\"`) oder nach Whitespace-Entfernung vergleichen, wurden von der automatischen
  Testkorrektur nicht erkannt (die Erkennung sucht unescapte `"..."`); solche Stellen wurden
  einzeln nach dem realen Testlauf von Hand nachgezogen (`ObservationCatalogWindowInputNormalizerArchitectureTests`,
  `SchaechtePageArchitectureGuardTests`).
- **Aufgabe 10c2 — Umlaute in Application/Infrastructure/Domain, rohe `ex.Message` ueber `UserError`.**
  Rund 1330 Meldungs-, Bericht-, Status- und Ausnahmetexte der drei unteren Schichten tragen echte Umlaute.
  Geaendert wurde NUR ein Literal mit Leerzeichen, das vorher per Suche (Quelltext, XAML, JSON, Tests,
  Python-Werkzeuge) als reiner Anzeigetext belegt war. **Bleiben ASCII:** Log-/Trace-/`BestEffort`-Zeilen
  (auch der Kontexttext von `BestEffort.Try`), KI-Prompts (`EnhancedVisionPromptBuilder`,
  `GuidedVerificationService`, `PdfKiSchiedsrichter`), Parsermuster (`PdfProjectMetadataParser`), der
  VSA-Codebaum (`VsaCodeTree`), der Feldschluessel `Ausfuehrung Datum/Jahr`, der Inhalt der
  Sicherungs-Markerdatei (`BackupTargetMarkerGuardService.MarkerContent` — alte Sicherungen werden daran
  erkannt), der XTF-Kopfkommentar (`XtfNeuWriter`), der Paketordner `2 Vollstaendig`, die Goldplatzhalter
  «ausmass ergaenzen», die Standardbeschreibung «… - persoenlich bestaetigt» und der gespeicherte
  `SkipReason` «Automatisch ergaenzte Rohrgrenze». **Zwei Texte sind Gegenstuecke und aendern nur
  gemeinsam:** `SamMaskFormatValidator` «Hand-Box ist ungültig …» ↔ `TrainingStudioBoxAnalysisUseCase`
  (`StartsWith`), `XtfBauwerkFelder` «… vollständig in den Zusatzangaben.» ↔ `XtfNeuExportService.HinweisOhneZusatz`
  (`EndsWith`). `MediaDistributionService.IstVerknuepfungsFehler` erkennt die Meldung des
  `ImportSourcePathGuard` in BEIDEN Schreibweisen («Verknüpfung»/«Verknuepfung»).
  **`UserError` erkennt eigene Meldungen jetzt breiter** (`IstEigenerMeldungstyp`): Neben
  `InvalidOperationException`/`ArgumentException` gelten auch exakt `IOException`, `InvalidDataException`,
  `JsonException` sowie jeder in Domain/Application/Infrastructure DEKLARIERTE Ausnahmetyp
  (`SidecarInsufficientVramException`, `SidecarRequestTimeoutException`, `TrainingExportPlanException`,
  `SchachtProArchiveException` …) als eigene Meldung — immer nur mit Wurfort in einer eigenen Schicht.
  Ausgenommen: `SidecarBadRequestException` (roher Sidecar-Antwortkoerper) und alle Typen im Namensraum
  `…WebGis…` (Anzeige regeln die geschuetzten WebGIS-Ablaeufe). Dieselbe `IOException` aus `File.Copy` oder
  `JsonException` aus System.Text.Json bleibt generisch. Damit wurden rund 95 Anzeigen von `ex.Message` auf
  `UserError.DescribeAndReport` umgestellt, ohne die fachliche Auskunft (VRAM, Zeitlimit, Pfadschutz,
  Session-Konflikt) zu verlieren. Bewusst roh bleiben nur Log-/Trace-Zeilen, `DataPageVideoPlaybackController`
  (Textvergleich «native side»), `ImportRunWorkflowController.SetDetailsText(ex.ToString())` (zugeklappte
  «Technische Details»), Ergebnisfelder, die in Sampledaten/Klassifikation weiterlaufen
  (`CodingTrainingFrameStore`, `EnhancedFrameAnalysis.EmptyFromException`), die Werkzeugausgabe von
  `PlaywrightInstallService`, die KI-Vorschlagsdurchlaeufe (`BendSuggestionScanWorkflow`,
  `PipeEndSuggestionScanWorkflow`, `CodingSuggestionScanUseCase`, `BendSuggestionListViewModel`,
  `PlayerWindow.Coding.Suggestions` — bewusste Entscheidung «technischer Fehler woertlich, nie glaetten»,
  z. B. «ffmpeg ist fehlgeschlagen: moov atom not found», per Test festgehalten), das technische Laufprotokoll
  des Trainings-Stapelimports (`TrainingBatchImportWorkflow`, die Zusammenfassung ist deutsch), die
  Sensor-Diagnoseliste (`LibreHardwareMonitorSensor`), die gezielt gefangene eigene Validierungsmeldung in
  `XtfLieferungsNorm` (InvalidOperationException) und alle WebGIS-Dateien. `UserErrorEigeneMeldungenSpracheTests`
  prueft seither auch `IOException`/`InvalidDataException`/`JsonException` auf englische Texte (zwei gefunden
  und uebersetzt: «Kein freier Dateiname für … gefunden.»). **Neuer Waechter**
  `DesignAuditLaufzeittexteSchichtenTests` (UI.Tests) prueft alle Literale mit Leerzeichen der drei
  Schichten gegen die bereinigten Wortformen; Log-Anweisungen (ueber mehrere Zeilen zusammengesetzt),
  `AusgenommeneDateien` und `GeschuetzteGanzeZeichenketten` sind mit Grund aufgefuehrt.
  **Fix-Runde 1:** (1) `UserError.OhneFremdtext` schneidet eine eigene Meldung dort ab, wo der Text
  einer INNEREN, nicht-eigenen Ausnahme eingebettet ist (englische HTTP/ZIP/JSON/SQLite-Texte, auch in
  `AggregateException`), der deutsche Vorspann bleibt; eine eingebettete EIGENE Meldung bleibt stehen.
  Wo der Vorspann allein reicht, ist `{ex.Message}` an der Quelle entfernt und die Ausnahme als
  `innerException` uebergeben. (2) `UserErrorEigeneMeldungenSpracheTests` leitet die Typliste per
  Quelltextsuche ab (alle `class X : …Exception` der drei Schichten + fuenf Standardtypen), erkennt
  Code-vor-Meldung-Konstruktoren (`SchachtProArchiveException("CODE", "…")`) und hat die zweite Regel
  «keine `.Message` einer anderen Ausnahme in eine eigene Meldung einbauen» mit begruendeter Liste
  `MessageEinbettungErlaubt`. (3) Fachliche deutsche Meldungen, die in der UI-Assembly (SewerStudio)
  geworfen werden, sind `UserFacingException` (UserError zeigt nur die drei unteren Schichten als eigen).
  (4) Einfache Eingabehinweise (`ObjektFeldViewModel`, `ListenErgaenzungViewModel`) laufen ueber
  `UserError.DescribeInputHint`: eigene Meldung ohne Log-Zusatz und ohne Protokolleintrag.
  **Fix-Runde 2:** (1) `TrainingAnnotationResult.Error` ist deutsch (`UserError.DescribeAndReport`), die Ausnahme
  steht in `Failure`; `AnnotationWorkbenchService.TeacherExportGrund` zeigt nie `Error` woertlich (ein fremder
  Exporteur koennte Rohtext liefern), sondern `Describe(Failure)` oder einen festen Satz und protokolliert den
  Rest. (2) `UserError.SchneideFremdtext` (rein, testbar) schneidet erst ab 12 Zeichen Fremdtext, bevorzugt ein
  Vorkommen hinter «: », « – », « (» usw. und gibt `null` zurueck, wenn die Meldung MIT dem Fremdtext beginnt —
  dann gilt sie als fremd und bekommt den Satz fuer ihren Typ. (3) Der Waechter erkennt Ausnahmeklassen mit
  Primaerkonstruktor, voll qualifizierte `new System.IO.IOException(`, `?.Message`, `).Message`, `].Message`,
  und neu: eine eigene Ausnahme (ausser `UserFacingException`) nennt den Programmlog nie selbst (der Zusatz
  kaeme doppelt). (4) Dossier-Abfragen (`DossierParcelLookupUseCase`, `OwnerDirectoryLookupUseCase`,
  `DossierBatchProposalUseCase`), `Quellenwahl`, `XtfRevisionExportService` (4 Stellen) und die XmlException
  in `XtfLieferungsNorm` melden deutsch und protokollieren den Rohtext; `ProjektPruefregeln` und
  `GeoShopAbgleichPlanBuilder` laufen ueber `DescribeInputHint` (eigene Meldung bleibt, fremde wird generisch).
- **Aufgabe 11 — Leer- und Ladezustände, Fortschrittsbalken.** `EmptyStateControl` (Icon-Kreis, Titel,
  Nachricht, optionale Aktion, schwebt sanft mit Ruhe-Schalter) ist die EINE Leerzustandsanzeige — nie eine
  eigene Textzeile mit derselben `HasItems`-Pruefung danebenbauen (Ausnahme sofort behoben, siehe
  `PersonalGoldAlbumWindow`, das eine solche Altfassung auf das Control umgestellt hat). Bindung immer ueber
  `Visibility="Collapsed"` als Standard und einen `DataTrigger`/`MultiDataTrigger` auf
  `{Binding HasItems, ElementName=<Liste>}` (built-in `ItemsControl.HasItems`, kein neues Feld); wo ein
  `IsBusy`/`IstBerechnung`-Flag existiert, zaehlt es als zweite `MultiDataTrigger`-Bedingung, damit der
  Leerzustand waehrend eines laufenden Ladevorgangs nicht aufblitzt. Neu versorgt: Sanierungs-/
  Schacht-Matrix, Schattenauswertung, Import-Vorschau, Code-Katalog-, Massnahmen- und Beobachtungs-Editor,
  Verteilen-Vorschau, SIA405-Lieferung, beide Dossier-Picker, Beobachtungen-Fenster, Protokoll-Historie/
  -Beobachtungen, Goldalbum, Strasse-uebernehmen, Schacht-Massnahmen(-Katalog), Druckcenter- und
  Dossiers-Hauptliste — Liste und Wortlaut je Stelle in `DesignAuditLeerUndLadezustandTests`. Medienkonflikte
  hatte den Leerzustand bereits ueber `StatusHost` (EmptyIcon/EmptyTitle/EmptyMessage); dort nichts geaendert.
  Bewusst ausgelassen: `XtfExportVorschauWindow` (eigene, an mehr als Item-Anzahl haengende
  `OhneZeilen`/`OhneTabelleHinweis`-Regel im ViewModel, keine reine Zaehlanzeige), `FeldVorschlagWindow`/
  `StrassenUebernahmeWindow`-Auswahlbereiche, die nur bei bereits vorhandenen Kandidaten ueberhaupt sichtbar
  werden (kein echter Leerzustand einer sichtbaren Liste).
  **Fortschrittsbalken:** genau zwei Theme-Stile in `Theme/Controls.xaml`, `ProgressBarThin` (4 px, dünne
  Werkzeugleisten-/Karten-Hinweise wie „laedt") und `ProgressBarStandard` (8 px, echte Fortschrittsanzeigen:
  Import, Sicherung, Export, Batch-Laeufe). Beide teilen eine Vorlage (Spur + Indikator + wandernder
  Lichtstreif bei `IsIndeterminate`, wie zuvor der programmweite Default-Stil), Radius `RadiusBar` (rundet
  beide Hoehen voll zur Kapsel), Indikatorfarbe ueber `TemplateBinding Foreground` (Standard `AccentBarBrush`,
  aber je Balken ueberschreibbar — Zustandsklassen-/Confidence-Faerbung bleibt moeglich). JEDES
  `<ProgressBar>`-Element traegt seither `Style="{StaticResource ProgressBarThin|Standard}"` (oder, bei einem
  lokalen `<ProgressBar.Style>` mit eigenen Triggern, `BasedOn="{StaticResource ...}"` — siehe `VsaPage.xaml`).
  Die alte `ImportProgressBar`/`ImportBarHeight`-Sondervorlage der Importkarte und das lokale `ThinProgress`
  in `VideoAnalysisPipelineWindow` sind zugunsten der zwei einheitlichen Stile entfernt. **Zwei benannte
  Ausnahmen ohne echtes `<ProgressBar>`-Element** (Waechter `DesignAuditLeerUndLadezustandTests`):
  `VsaCodeExplorerWindow` (vier `Border x:Name="ProgressBar0.."3"` sind eine Wizard-Schrittanzeige mit VIER
  unabhaengig eingefaerbten Segmenten — ein einzelner Balken mit einem Value/Maximum kann das nicht abbilden,
  „falls gleichwertig" aus dem Auftrag trifft hier nicht zu) und `StartupSplashWindow` (Border-Splashbalken der
  eigenen Choreografie, ohnehin als Video-/Startfenster ausgenommen). Der implizite typweite Standard-Stil
  bleibt als Sicherheitsnetz unveraendert bestehen (Fallback, falls ein kuenftiges `<ProgressBar>` den
  Style vergisst), gilt aber nicht als eine der zwei erlaubten Formen im Waechter.
  **Lade-Hinweise:** `MediaSearchWindow`s Text+Balken-Panel ist bewusst UNVERAENDERT (echte
  Prozent-Fortschrittsanzeige mit eigenem Value/Maximum — kein reiner Spinner-Hinweis, `BusyOverlay` haette
  die Prozentanzeige entfernt). Offen: `OverviewPage`s „Vorschau wird geladen…"-Karte (eigene, gegen ein
  Geschwister-`Border` per `Visibility` ausgetauschte Karte statt Overlay-ueber-Inhalt) liesse sich nur mit
  einer Umstrukturierung der drei nebeneinanderstehenden Zustands-Borders (Projekt waehlen/Laden/Inhalt) auf
  `BusyOverlay` oder ein `StatusHost`-`State`-Enum umstellen — das ginge ueber eine reine Optikaenderung
  hinaus und ist deshalb nicht angefasst.
  **Fix-Runde 1 (Pruefung, Koordinator-Rueckmeldung):** (1) Die Vorlage (Spur/Indikator/Wanderstreif)
  stand nach der ersten Fassung ZWEIMAL im Dokument — einmal im gemeinsamen Basisstil
  `ProgressBarBaseFuerZweiHoehen`, einmal (mit hart kodiertem Radius `3` statt `RadiusBar` und fest
  verdrahteter `AccentBarBrush` statt `TemplateBinding Foreground`) im impliziten typweiten
  Sicherheitsnetz. Der Basisstil ist jetzt die EINE Stelle mit der echten `ControlTemplate`; das
  implizite Sicherheitsnetz ist `BasedOn="{StaticResource ProgressBarBaseFuerZweiHoehen}"` und traegt
  nur noch den einen abweichenden Setter (`Height=6`) — visuell unveraendert, weil `RadiusBar` (5) ein
  6-px-Band genauso voll zur Kapsel rundet wie zuvor der hart kodierte Radius 3, und die geerbte
  `Foreground=AccentBarBrush` denselben Indikatorton ergibt wie der alte feste Wert. Der Pruefer-Test
  `ProgressBarIndeterminateTemplateTests` (schneidet den echten Stil per String-Marker aus
  `Controls.xaml` und rendert ihn in einem echten Fenster) zielt seither auf
  `x:Key="ProgressBarBaseFuerZweiHoehen"` statt auf den (jetzt leeren) impliziten Stil — er testet
  damit direkt die Vorlage, die auch `ProgressBarThin`/`ProgressBarStandard` tatsaechlich verwenden.
  (2) Weitere EmptyStateControl-Versorgung fuer «alles» (Entscheid Pascal): Haltungs-/Schacht-Uebersicht
  (`HaltungUebersichtPanel`/`SchachtUebersichtPanel`, sowohl der «keine Auswahl»-Leerzustand — jetzt
  ebenfalls `EmptyStateControl` statt eines eigenen `TextBlock` — als auch die Schadenliste selbst),
  die abgedockten Voll-Ansichten `HaltungsansichtView`/`SchachtansichtView` (Haupt- UND Schadenliste),
  `DossierAreaWindow` (Themenliste) und `DossierBatchWindow` (Vorschlagsliste) — alle in
  `DesignAuditLeerUndLadezustandTests.EmptyStateDateien` gefuehrt.
  **Fix-Runde 2: die drei bare-construction-Ausnahmen sind behoben, nicht mehr ausgenommen.**
  Ursache war `EmptyStateControl.xaml:67` selbst, nicht die drei Dateien: der optionale
  Aktionsknopf setzte `Style="{StaticResource SecondaryButton}"` — eine Ressource aus den per
  `Application` gemergten Theme-Woerterbuechern, die beim BAML-Laden (nicht erst bei Sichtbarkeit)
  aufgeloest wird und ohne `Application`-Kontext eine `XamlParseException` wirft. Umgestellt auf
  `Style="{DynamicResource SecondaryButton}"` (loest lazy zur Laufzeit ueber den Baum auf,
  faellt ohne `Application`-Ressourcen auf ungestylt zurueck statt zu werfen; die einzige weitere
  `StaticResource` im Control, `NullToCollapsed`, ist ein lokal im selben `UserControl.Resources`
  definierter Konverter und bleibt unveraendert, weil er immer im Dateiscope aufloest). `HaltungAufklappListe.xaml`, `SchachtAufklappListe.xaml` und `PlayerCodingSidePanel.xaml`
  (beide Listen: `LstCodingEvents`, `LstImportEvents`) tragen seither ganz normal ein
  `EmptyStateControl` wie jede andere Datei der Liste; die fuenf vorher betroffenen Tests
  (`DataPageAnsichtUmschalterTests`, `NovaListenWiederverbindenTests`,
  `PlayerCodingSidePanelControllerInitializerTests`, `PlayerCodingSidePanelEventBinderTests`,
  `HaltungAufklappListeFokusTests`) laufen wieder gruen. Der Ausnahme-Waechter
  `Ausnahmen_ohne_EmptyStateControl_sind_real_bare_construction_Dateien` ist entfernt, die drei
  Dateien stehen jetzt in `DesignAuditLeerUndLadezustandTests.EmptyStateDateien`.
  Zwei weitere DossierEditWindow-Tabellen (Eigentuemer/Themen/Aenderungswesen, Hoehe 110-170 px)
  bleiben weiterhin bewusst NICHT versorgt (unveraendert seit Fix-Runde 1): `EmptyStateControl`s
  minimaler Platzbedarf (72-px-Icon + Titel + Nachricht + 24-px-Rand, real ueber 180 px) wuerde die
  absichtlich kurze feste Tabellenhoehe ueberragen und die direkt darunterstehenden „+ Zeile"/„Zeile
  entfernen"-Knoepfe ueberlappen (der umgebende Grid clippt nicht). `HandbuchWindow`s
  Inhaltsverzeichnis ist eine feste, im Programm eingebaute Liste und kann nie leer sein — ebenfalls
  weiterhin ausgenommen (kein eigener Waechter noetig, da keine bare-construction-Falle).
- **Aufgabe 12 — Feste Farben und Schriften auf Tokens.** Fachfarben (Zustandsklassen,
  Nutzungsarten, SVG-Grafik, PDF/Excel-Berichte) bleiben unveraendert von dieser Regel ausgenommen.
  Betroffen: `HydraulikPanelWindow` (17 im Konstruktor gefrorene Pinsel/Font-Felder, nie
  themefaehig — jetzt `SetResourceReference` je Zeichnung, selbst gebaute Farbverlaeufe ueber
  `ResolveColor`), `SanierungsmassnahmenWindow.xaml`/`.xaml.cs` (5 Hex + 2 feste Blitzfarben),
  `CodingSessionViewModel` (10 direkt konstruierte Pinsel), `TrainingCenterViewModel`/
  `TrainingKnowledgeBaseStatusPresentationBuilder` (feste Bereitschaftsfarben) und
  `VsaCodeExplorer` (Hover/Press/Badge-Ersatzfarben). Muster durchgehend: ein Theme-Token-Name
  wird ueber `TryFindResource`/`SetResourceReference` aufgeloest, der alte Hex-Wert bleibt nur
  Rueckfall fuer den Fall ohne laufende `Application` (Unit-Test).
  Alle **89 Fundstellen** von `FontFamily="Consolas"` (alle Schreibweisen, in 21 Dateien inkl. der
  vier Video-Fenster PlayerWindow/PlayerWindow.Resources/PlayerCodingSidePanel/PipeGraphTimeline —
  die Farb-Ausnahme der sechs Video-Dateien gilt NICHT fuer die Schriftart) zeigen jetzt auf
  `{DynamicResource FontMono}`/`SetResourceReference(..., "FontMono")`. Die drei etablierten
  Rueckfallausdruecke (`DataGridStandardTextColumnFactory`/`DataGridWrappingTextColumnFactory`/
  `VsaCodeExplorerWindow`: `TryFindResource("FontMono") ?? new FontFamily("Consolas")`) bleiben.
  Alle **89 Fundstellen** `FontFamily=FontIcon`+`Text="..."` in `TextBlock` (Video-Fenster
  PlayerWindow/PlayerCodingSidePanel ausgenommen) sind `ui:FluentIcon Glyph="..."` (`FluentIcon`
  ist eine `TextBlock`-Unterklasse). Drei Faelle mit `<TextBlock.Style>`-Trigger brauchten
  `<ui:FluentIcon.Style>` statt `<TextBlock.Style>` (ein `Style TargetType="TextBlock"` gilt auch
  auf der Unterklasse, nur das Element selbst muss `ui:FluentIcon` heissen). Drei XAML-Dateien ohne
  `xmlns:ui` bekamen die Zeile ergaenzt (`EmptyStateControl`, `SystemMonitorPanel`, `ImportPage`).
  **Sieben Icon-Knoepfe, die dem bestehenden Waechter `Icon_Knoepfe_haben_einen_vorlesbaren_Namen_
  und_einen_Tooltip` vor der Umstellung als reine `TextBlock`-Icons unsichtbar waren**, bekamen
  dabei einen fehlenden `AutomationProperties.Name` nachgetragen (hatten schon einen `ToolTip`).
  **Waechter:** `DesignAuditOptikTokenTests` (kein "Consolas" ausserhalb `FontMono`-Token/
  Rueckfallausdruecke, plus Regressionswaechter je bereinigter Datei) und
  `DesignAuditFeinschliffTests.Feste_Farben_gibt_es_nur_in_Video_Fenstern` (zweites Regex-Muster
  fuer `<Setter Property="..." Value="#hex">` — diese Schreibweise verbarg die fuenf Hex-Werte in
  SanierungsmassnahmenWindow.xaml vor dem alten Muster, das nur `Attribut="#hex"` sah).
  **Fix-Runde 1 (Review 29.09.2026) — fuenf Nachbesserungen:**
  (1) `TrainingStudioWindow.xaml`s `QualityWarning`-Anzeige stand auf `WarningBrush` mit
  `Foreground="White"` (2,52:1 im Dunkelmodus) — jetzt `WarningSubtleBrush`-Hintergrund +
  `WarningTextBrush`-Text (4,51/4,53:1).
  (2) Drei Text-auf-Fuellfarbe-Kontrastregressionen behoben: `GetConfidenceBrush` (nur fuer
  Text verwendet) nutzt `SuccessTextBrush`/`WarningTextBrush`/`DangerTextBrush` statt der
  Fuellfarben; `HydraulikPanelWindow.AuslastungRun` ebenso; `AblagerungVerdictText` (sitzt auf
  `SuccessSubtleBrush`/`DangerSubtleBrush`) nutzt bewusst die normale `TextBrush` statt
  `SuccessTextBrush`/`DangerTextBrush` — Letzteres erreicht auf `DangerSubtleBrush` im Hellmodus
  nur 3,95:1, `TextBrush` dagegen 9,8-14,6:1 in beiden Themes (Subtle-Flaechen sind wie
  `CardBrush` je Theme hell bzw. dunkel).
  (3) `SanierungsmassnahmenWindow`s Fehler-/Warnungs-Zaehlerabzeichen: weder "White" noch
  `StatusBadgeTextBrush` erreichen auf `DangerBrush`/`WarningBrush` 4,5:1 in beiden Themes
  (`StatusBadgeTextBrush` ist fuer die theme-gleichen Zustandsklassen-Abzeichen Z0-Z4 gedacht,
  nicht fuer theme-abhaengige Fuellfarben) — neue Tokens `DangerBadgeTextBrush`/
  `WarningBadgeTextBrush` (dunkel nahezu schwarz, hell weiss, siehe Theme.xaml-Kommentar); dazu
  war der Danger-Abzeichenhintergrund noch `StaticResource` statt `DynamicResource`.
  (4) `HydraulikPanelWindow`s zwei selbst gebauten Farbverlaeufe sind Momentaufnahmen aus
  `ResolveColor` — das Fenster haengt sich jetzt an `ThemeManager.ThemeChanged` (neu zeichnen)
  und meldet sich beim `Closed` wieder ab, wie `RohrquerschnittControl` es fuer denselben Fall tut.
  (5) `TrainingKnowledgeBaseStatusPresentationBuilder.Build` lief nach einem
  `ConfigureAwait(false)` auf einem Threadpool-Thread und rief dort `Application.Current.
  TryFindResource` auf — nicht threadsicher. `Build` liefert seither nur noch `ReadinessBrushKey`
  (Token-Name) + `ReadinessFallbackColor`; aufgeloest wird erst in
  `TrainingKnowledgeBasePresentationController.ApplyStatus`, das ueber `OnUi(...)` auf dem
  UI-Thread laeuft.
  Dazu: `VideoAnalysisPipelineWindow`s `StartButton` hatte weisses Icon+Text, aber das
  `ui:FluentIcon`-Glyph im StackPanel-Inhalt verlor die Vererbung an den impliziten
  `TextBlock`-Stil (dieselbe B7-Falle wie bei den vier grossen Knopfvorlagen) — die
  `ContentPresenter.Resources`-Weiterleitung ist ergaenzt. `VsaCodeExplorerColumnTileBadge.ColorHex`
  traegt entweder eine echte Fachfarbe (Klartext-Hex) oder, ohne Fachwert, einen
  Theme-Token-Namen (Doku am Record); `ResolveBadgeColor` hat jetzt einen eigenen
  Verhaltenstest (Hex bleibt Hex, Token-Name loest gegen das Theme auf).
  Vier neue Testmethoden in `DesignAuditContrastTests` (je beide Themes) decken die sechs neu
  eingefuehrten Text-/Hintergrund-Paarungen ab.
- **Aufgabe 13 — Windows-Integration.** Drei unabhaengige Bausteine.
  **Design "Wie Windows":** `ThemeManager.System` ist eine dritte, GESPEICHERTE Design-Wahl neben
  `Light`/`Dark` (`AppSettings.UiTheme` kennt jetzt drei Werte). `NormalizeTheme` bleibt
  zweiwertig (fuer alles, was eine konkrete ladbare Ressource braucht — Fenster-Rand, dunkle
  Titelleiste); `NormalizePreference` ist die dreiwertige Fassung fuer die gespeicherte Wahl;
  `ResolveEffectiveTheme(preference, reader?)` loest "System" ueber die reine Regel
  `WindowsThemePreferenceRule.Resolve(int? AppsUseLightTheme)` auf (0 = Dunkel, alles andere
  inkl. fehlendem Wert = Hell — Windows' eigener Standard). Der echte Registry-Zugriff liegt
  getrennt in `WindowsThemeRegistry.ReadAppsUseLightTheme()`
  (`HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize`), damit die Regel ohne
  Registry testbar bleibt (ein `Func<int?>` ersetzt sie in Tests). `WindowsThemeFollowService`
  (App.xaml.cs, `IDisposable`, in `OnExit` abgemeldet) haengt an
  `Microsoft.Win32.SystemEvents.UserPreferenceChanged` (Kategorie General ODER Color — welche
  genau feuert ist nicht belegt, deshalb beide) und wendet bei aktiver "System"-Wahl sofort neu
  an; da dieses Ereignis nicht sicher auf dem UI-Thread feuert, marshallt es ueber den
  uebergebenen `Dispatcher` (`Dispatcher.Invoke`, wie das bestehende
  `ThemeManager.ThemeChanged`-Abonnement in `HydraulikPanelWindow`). **Die Entscheidung selbst
  steckt in `WindowsThemeFollowPolicy.SollNeuAnwenden`** (reine, ohne SystemEvents/Dispatcher
  testbare Funktion, Fix-Runde 1): relevante Kategorie? Wahl = "System"? UND das aufgeloeste
  Theme WEICHT vom aktuell angewendeten ab (`ThemeManager.CurrentTheme`) — bei einer
  Windows-Einstellung, die Hell/Dunkel gar nicht betrifft (z. B. nur die Akzentfarbe), wird nicht
  neu gezeichnet.
  **Ein Weg statt zwei:** Der fruehere Hell/Dunkel-Umschalter (`IsDarkTheme`, bi-state) samt
  eigenem "Anwenden"-Knopf und `SettingsThemeWorkflow.SyncUiThemeChanged`/
  `SyncIsDarkThemeChanged` ist entfernt — ein bi-state-Umschalter kann keine drei Zustaende
  abbilden. Drei `RadioButton` (Hell/Dunkel/Wie Windows, `ThemePreferenceToBoolConverter`,
  ConverterParameter = Zielwert) binden direkt an `UiTheme`; `OnUiThemeChanged` ruft
  `SettingsThemeWorkflow.ApplyTheme` (speichert die Wahl sofort UND wendet das aufgeloeste Theme
  sofort an) — die Design-Wahl wirkt beim Klick, kein Neustart-Hinweis mehr. Geprueft (nicht nur
  behauptet): `WindowBackdropHelper.ApplyToOpenWindows` malt alle offenen Fenster live um,
  `Fluent.OnBackdropChanged`/`ApplyOnLoaded` liest fuer NEUE Fenster immer `ThemeManager.
  CurrentTheme` frisch, drei Renderer (`RohrquerschnittControl`, `HydraulikPanelWindow`, dieselbe
  Stelle testet `ThemeChanged`) haengen bereits an `ThemeManager.ThemeChanged` — nichts im
  geprueften Bestand haelt eine Theme-Momentaufnahme fest, die einen Neustart brauchen wuerde.
  **Beim Initialisieren des ViewModels wird `_uiTheme` DIREKT ins Feld geschrieben** (wie
  `_protocolPhotosPerPage` daneben) — ueber die Eigenschaft wuerde das blosse Oeffnen der Seite
  sofort erneut speichern.
  **Hochkontrast:** `Theme/ThemeHighContrast.xaml` ist eine UEBERLAGERUNG, kein drittes
  Vollthema: `ThemeManager.SetHighContrastOverlay` haengt sie IMMER NACH dem normalen Hell-/
  Dunkel-Theme in `MergedDictionaries` ein (hoeherer Index = hoehere Prioritaet bei der
  WPF-Ressourcensuche) und ersetzt nur 23 Kern-Tokens (Text, Hintergrund, Karte, Kopf, Rand,
  Akzent, Auswahl, Erfolgs-/Warn-/Fehlertext) durch `{DynamicResource {x:Static
  SystemColors.XyzColorKey}}`. **Alles NICHT genannte faellt automatisch auf den Wert des
  darunterliegenden Hell-/Dunkel-Themes zurueck** — kein fehlender Schluessel kann je einen
  Absturz ausloesen, und die Liste muss nicht alle rund 150 Tokens des normalen Themes
  duplizieren (bewusste, dokumentierte Teilmenge statt Vollstaendigkeit). Erfolgs-/Warn-/
  Fehlertext bekommen bewusst KEINE eigene SystemColors-Farbe (die gibt es dort nicht; ein
  geratener Schluessel koennte zufaellig mit Highlight/Accent kollidieren) — die Unterscheidung
  traegt in Hochkontrast der Text/das Symbol, nicht die Farbe allein. `HighContrastFollowService`
  (App.xaml.cs) haengt an `SystemParameters.StaticPropertyChanged` (WPF-eigen, feuert bereits auf
  dem UI-Thread, kein Marshalling noetig) und wendet/entfernt die Ueberlagerung bei jedem
  Hochkontrast-Wechsel WAEHREND SewerStudio laeuft. Ein Design-Wechsel (Hell -> Dunkel) laesst
  eine aktive Ueberlagerung unangetastet (verschiedene Indizes in `MergedDictionaries`).
  **`ThemeManager.GetThemeUri`/die Ueberlagerungs-URI sind vollqualifizierte pack-URIs**
  (`pack://application:,,,/<Assembly>;component/<Pfad>`) statt blosser relativer URIs: Eine
  relative URI haengt an `System.Windows.Application.ResourceAssembly` — einem prozessweiten,
  nur EINMAL setzbaren Wert, der im echten `SewerStudio.exe` zufaellig passt (Einstiegs-Assembly
  = UI-Assembly), aber z. B. im isolierten WPF-Testprozess (`testhost.exe`) bereits falsch
  fixiert ist, bevor eigener Code eingreifen kann (real beim Schreiben dieses Tests
  aufgetreten). Die vollqualifizierte URI braucht diesen globalen Zustand gar nicht erst.
  **DPI:** `SewerStudio.exe` trug bisher KEIN `dpiAware`/`dpiAwareness`-Element im eingebetteten
  Manifest — belegt durch Auslesen des gebauten `SewerStudio.exe` per
  `LoadLibraryEx(..., LOAD_LIBRARY_AS_DATAFILE)` + `FindResource(RT_MANIFEST)` (keine Ausfuehrung
  des Programms): Der .NET-SDK-Standardweg bettet ohne `app.manifest` nur ein leeres
  Platzhaltermanifest ein. Neues `src/AuswertungPro.Next.UI/app.manifest`
  (`<dpiAwareness>PerMonitorV2</dpiAwareness>` + `dpiAware=true/PM`, Windows-10/11-`supportedOS`;
  `gdiScaling` bewusst NICHT gesetzt — skaliert nur klassisches GDI-Rendering mit, SewerStudio hat
  keine einzige `System.Drawing`-/WinForms-Stelle, siehe Fix-Runde 1) +
  `<ApplicationManifest>app.manifest</ApplicationManifest>` im csproj; nach dem Build zeigt
  derselbe Auslesevorgang das Element korrekt eingebettet. Fix-Runde 1 hat das zusaetzlich zur
  Laufzeit bewiesen (nicht nur am Manifest-Byte-Inhalt): ein eigenstaendiges Test-EXE mit
  demselben `app.manifest` meldet per `GetThreadDpiAwarenessContext`/
  `AreDpiAwarenessContextsEqual` echtes `DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2`; dieselbe
  EXE ohne das Manifest meldet `Unaware` — Gegenprobe bestanden. Das echte `SewerStudio.exe` wird
  dafuer nie gestartet.
  **Taskleistenfortschritt:** `ITaskbarFortschritt` (`UI/Services`, 175. ServiceProvider-
  Registrierung) mit vier Methoden (`SetzeFortschritt(0..1)`, `SetzeUnbestimmt`, `Fehler` =
  roter Zustand, `Beenden`). `TaskbarFortschritt` traegt `Window.TaskbarItemInfo` erst bei
  Bedarf am Hauptfenster nach (`??=`, kein XAML-Eintrag noetig) und loest das Fenster ueber einen
  `Func<Window?>` bei JEDEM Aufruf neu auf (`Application.Current?.MainWindow` als Standard) —
  die Dienste entstehen im `ServiceProvider`, bevor `MainWindow` existiert. `ServiceProvider.
  Taskbar` ist wie `Dialogs` `{ get; internal set; }` mit Feldinitialisierer, damit Tests einen
  Fake einsetzen koennen (`new ServiceProvider(...) { Taskbar = fake }`). Angebunden: Vollsicherung
  (`SettingsFullBackupWorkflowRequest.Taskbar`, optional; unbestimmt waehrend der Groessenanalyse,
  dann Fortschritt aus dem Kopiervorgang; bei Fehlschlag/Ausnahme `Fehler()`, dann — NACH dem
  Fehlerdialog, den der Benutzer noch mit rotem Symbol sehen soll — `Beenden()`, siehe
  Fix-Runde 1; bei Erfolg/Abbruch sofort `Beenden()`), Ein-Knopf-Import
  (`ImportPageViewModel`: `AktualisiereTaskbarFortschritt()` liest `IsImportInProgress`/
  `ImportIsIndeterminate`/`ImportProgressPercent` bei JEDEM der drei zugehoerigen
  `OnXChanged`-Ereignisse frisch neu, statt Werte durchzureichen — die drei Ereignisse koennen in
  beliebiger Reihenfolge feuern) und das Videoanalyse-Fenster (`VideoAnalysisPipelineWindow`,
  liegt unter `Views/Windows`, NICHT im eingefrorenen `UI/Ai` — optionaler dritter
  Konstruktorparameter, `DataPageWindowLauncher` reicht `_services.Taskbar` durch; zwei
  Phasen Video/Mapping, `VideoPhaseDone` entscheidet welcher Prozentwert gilt). Kein roter
  Fehlerzustand fuer den Ein-Knopf-Import (kein dediziertes Fehler-Flag am ViewModel ohne
  groesseren Eingriff; bewusst ausgelassen, siehe Bericht).
  Tests: `WindowsThemePreferenceRuleTests`, `ThemeManagerPreferenceTests` (Reader injiziert, kein
  echter Registry-Zugriff), `ThemeHighContrastIsolatedSmokeTests` (echter WPF-Kindprozess, laedt
  die echte `Theme/ThemeHighContrast.xaml`), `SettingsThemeWorkflowTests`,
  `SettingsFullBackupWorkflowTests` (vier neue Faelle mit `FakeTaskbarFortschritt`),
  `ImportPageViewModelTaskbarTests`. `ProjektEroeffnungSettingsGuardTests.
  SettingsPageViewModel_delegates_theme_workflow` ist an die neue Architektur angepasst
  (`Sync*`-Methoden existieren nicht mehr).
- **Aufgabe 13, Fix-Runde 1 (29.09.2026, Coordinator-Rueckmeldung).** Acht Befunde behoben.
  **CRITICAL — Taskleiste liess jede echte Datensicherung scheitern:** Der
  Fortschritts-Callback von `FullBackupService.RunAsync` laeuft in `Task.Run` auf einem
  Threadpool-Thread (kein `System.Progress<T>` mit eigenem Marshalling); `TaskbarFortschritt`
  griff dort direkt auf `Window.TaskbarItemInfo` zu -> `InvalidOperationException` -> vom
  `catch` als Sicherungsfehlschlag gewertet, der ganze Lauf rollte zurueck (real reproduziert).
  `TaskbarFortschritt.Anwenden` marshallt jetzt selbst (`Application.Current?.Dispatcher`,
  `CheckAccess`/`BeginInvoke` — NIE `Invoke`, ein Anzeige-Aufruf darf einen Hintergrundthread
  nie blockieren) und faengt JEDE Ausnahme intern ab (`BestEffort.ReportWarning`, nie werfen).
  Zusaetzliche Verteidigungslinie in `SettingsFullBackupWorkflow.SicherTaskbar`: jeder
  `taskbar?.X()`-Aufruf ist einzeln try/catch-geschuetzt, unabhaengig davon, welche
  `ITaskbarFortschritt`-Implementierung injiziert wurde. Import und Videoanalyse-Fenster
  geprueft: beide konstruieren ihr `System.Progress<T>` auf dem UI-Thread (Button-Klick-Handler)
  und sind dadurch bereits von Haus aus threadsicher; `TaskbarFortschritt`s eigener Schutz greift
  dort zusaetzlich, aendert aber nichts am bestehenden Verhalten.
  Beweis mit der ECHTEN Klasse (nicht nur einem Fake): `TaskbarFortschrittThreadSafetyIsolatedSmokeTests`
  (echter WPF-Kindprozess) startet einen echten `Thread`, ruft `SetzeFortschritt` auf, prueft
  dass der Aufruf sofort zurueckkehrt (kein blockierendes `Invoke`) und keine Ausnahme wirft,
  pumpt den UI-Dispatcher (`Dispatcher.PushFrame`, `StaTestRunner` pumpt selbst nicht) und
  prueft danach den tatsaechlich gesetzten `TaskbarItemInfo`-Zustand.
  **Roter Zustand bleibt bis zur Bestaetigung, dann zurueckgesetzt:** `Fehler()` steht VOR dem
  Fehlerdialog (der Benutzer soll den Fehlschlag am Symbol sehen), `Beenden()` NACH dem Dialog
  (modal/blockierend) — nicht mehr dauerhaft rot. Bewiesen ueber eine GETEILTE Ereignisliste
  zwischen `FakeTaskbarFortschritt` und dem Dialog-Fake (`fehler` -> `dialog:error` ->
  `beenden`), nicht nur ueber die letzte Zeile.
  **Abbruch ist kein Fehler:** `VideoAnalysisPipelineWindow` setzt bei `OperationCanceledException`
  weiterhin `Vm.HasError=true` (die BESTEHENDE Fehlerbanner-Anzeige bleibt unveraendert), aber ein
  neues privates `_abgebrochen`-Flag (VOR `Vm.SetError("Abgebrochen.")` gesetzt) laesst die
  Taskleiste in diesem Fall `Beenden()` statt `Fehler()` zeigen.
  **Hochkontrast deckt jetzt auch Hintergrundflaechen ab.** `BgBrush` (Hauptfenster-Hintergrund,
  im normalen Theme ein `LinearGradientBrush` — Farbverlaeufe widersprechen Hochkontrast, hier
  bewusst flach auf `WindowColor`), `SurfaceSubtleBrush`, `HoverBrush`, `RowHoverBrush`,
  `OverlayBrush`, `NavPanelBrush` sind ergaenzt (alle per XAML-Fundstelle verifiziert: Text
  sitzt direkt darauf, z. B. "Strg K"-Chip auf `SurfaceSubtleBrush`, Navigationstext auf
  `NavPanelBrush`, "Bitte zuerst ein Projekt speichern" auf `OverlayBrush`).
  **`ThemeHighContrastCoverageTests` ist ein echter Scanner, keine Behauptung:** parst ALLE
  XAML-Dateien (`XDocument`), sammelt jedes `Background="{DynamicResource XBrush}"`, auf dem
  direkt (nicht hinter einer eigenen verschachtelten Flaeche) normaler Text sitzt (Foreground
  Text/TextSecondary/Header/Muted/Faint ODER ein `TextBlock`/`TextBox`/`Run`/`AccessText`/
  `Label` ohne eigene Foreground-Angabe), und verlangt, dass jeder gefundene Schluessel entweder
  in `ThemeHighContrast.xaml` steht oder in einer begruendeten, im Test selbst dokumentierten
  Ausschlussliste (Farbe TRAEGT dort Bedeutung: Success-/Warning-/Danger-Untergrund,
  Zustandsstufen, Markierungsfarben, Code-Gruppen, Video-Scrims). Der Scanner fand dabei
  nebenbei einen ECHTEN, unabhaengigen Bug: **`SurfaceBrush` war in sieben XAML-Stellen
  referenziert (ExportPage, vier Dossier-Fenster), aber NIE definiert** — `DynamicResource` auf
  einen fehlenden Schluessel wirft nicht, laesst die Flaeche aber unsichtbar/durchsichtig statt
  zu werfen. Behoben (gleiche Farbe wie `CardBrush` in beiden Themes) und in die Ueberlagerung
  aufgenommen.
  **`ThemeManager.ComponentUri` braucht zwei zusaetzliche einmalige Registrierungen**, damit
  `pack://application:,,,/...`-URIs auch OHNE je instanziierte `System.Windows.Application`
  funktionieren (real in einem isolierten Testlauf aufgetreten): `PackUriHelper.UriSchemePack`
  lesen (registriert das "pack:"-Schema beim generischen .NET-Uri-Parser — ohne das wirft
  `new Uri("pack://application:,,,/...")` faelschlich "Invalid port specified", das Komma-Tripel
  sieht dem Parser wie ein Portanteil aus) und `Application.Current` lesen (blosses Lesen der
  statischen Eigenschaft, KEIN neues Application-Objekt, `Current` bleibt `null` — registriert
  den WebRequest-Praefix fuer "pack", den `ResourceDictionary.Source` beim tatsaechlichen Laden
  braucht, sonst "The URI prefix is not recognized."). Beide stehen jetzt im statischen
  Konstruktor von `ThemeManager`. Achtung Namenskollision: `ThemeManager.System` (die
  Design-Konstante) verdeckt den Namespace `System` innerhalb der Klasse — deshalb
  `global::System....` statt `System....`.
  **StaticResource friert Theme-Farben beim Laden ein, DynamicResource nicht:** Fuenf Dateien mit
  ~150 `{StaticResource *Brush}`-Stellen liessen ein bereits offenes Fenster beim Design-Wechsel
  farblich zurueck (die urspruengliche Behauptung "kein Neustart noetig" war fuer diese Dateien
  falsch). Mechanisch auf `DynamicResource` umgestellt (nur Farb-/Brush-Token, Styles/Konverter/
  Templates blieben `StaticResource`): `Views/Controls/RecordDetailsView.xaml` (lebt in den
  Datenseiten, dauerhaft offen), `Views/Windows/VsaCodeExplorerWindow.xaml`,
  `SanierungsmassnahmenWindow.xaml`, `SchachtMassnahmenWindow.xaml`, `ImportPreviewWindow.xaml`.
  Keine `DataGridColumn`-Direktattribute betroffen (gezielt geprueft — dort haette
  `DynamicResource` ohne Vererbungskontext nicht zuverlaessig aktualisiert). **Diese fuenf
  Dateien sind eine gezielte Stichprobe, kein vollstaendiger Codebase-Sweep** — andere
  `StaticResource`-Farbstellen ausserhalb dieser fuenf Dateien wurden in dieser Runde nicht
  systematisch gesucht.
  **DPI:** `gdiScaling` aus dem Manifest entfernt (skaliert nur klassisches GDI-Rendering,
  SewerStudio hat keine `System.Drawing`-/WinForms-Stelle — unbelegter Schalter). Zusaetzlich
  zur statischen Manifest-Pruefung jetzt ein echter LAUFZEIT-Beleg: ein eigenstaendiges,
  minimales Test-EXE mit demselben `app.manifest` meldet per
  `GetThreadDpiAwarenessContext`/`AreDpiAwarenessContextsEqual` echtes
  `DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2`; dieselbe EXE ohne das Manifest meldet
  `Unaware` (Gegenprobe). Das echte `SewerStudio.exe` wurde dafuer nie gestartet.
  Tests: `TaskbarFortschrittThreadSafetyIsolatedSmokeTests`, vier neue Faelle in
  `SettingsFullBackupWorkflowTests` (inkl. `ThrowingTaskbarFortschritt` — die Sicherung
  schliesst erfolgreich ab, selbst wenn die Taskleiste bei JEDEM Aufruf wirft),
  `PipelineCompletionWindowTests.Kindprozess_prueft_Abbruch`, `ThemeHighContrastCoverageTests`
  (echter XAML-Scanner), `WindowsThemeFollowPolicyTests`, `HighContrastFollowServiceTests`
  (kein WPF-Kindprozess noetig — `ThemeManager`s pack-URI ist seit diesem Fix von einer
  laufenden `Application` unabhaengig).
- **Aufgabe 13, Fix-Runde 2 (29.09.2026, Re-Review).** IMPORTANT 2 war unvollstaendig, drei MINOR
  Nacharbeiten.
  **Der Scanner sah nur `Background="..."` als Attribut, nicht `<Setter Property="Background"
  Value="..."/>` in einem `Style`/`ControlTemplate`.** `ThemeHighContrastCoverageTests` bekam
  einen zweiten Fundweg: fuer jeden Background-`Setter` in einem `Style`/`ControlTemplate` mit
  textfaehigem `TargetType` (Button, ListBoxItem, ...) wird der Foreground im selben Trigger-
  Zustand bzw. dem Style-Grundzustand geprueft. Das fand echte, bis dahin uebersehene Faelle:
  `RecordDetailsView.xaml`s `DataTrigger`-Paar `Background=SuccessSubtleBrush`/
  `Foreground=SuccessTextBrush` und `Controls.xaml`s `BearbeitungErledigtKnopf`-Style. Per
  Sabotageprobe belegt: Mit nur diesem zweiten Fundweg (erster Weg testweise abgeschaltet)
  schlaegt der Test bei einem entfernten `SuccessSubtleBrush`-Eintrag weiterhin an.
  **Die eigentliche Ursache: `SuccessTextBrush`/`WarningTextBrush`/`DangerTextBrush` werden in
  `ThemeHighContrast.xaml` bereits auf `WindowText` gezwungen** (bewusst, siehe Runde 1) — ihr
  Hintergrund `SuccessSubtleBrush`/`WarningSubtleBrush`/`DangerSubtleBrush` durfte deshalb NICHT
  laenger in der Ausschlussliste stehen (Runde-1-Fehler: als traege er dieselbe unangetastete
  Bedeutung wie z. B. `KiSubtleBrush`/`KiTextBrush` — dieses Paar bleibt zu Recht ausgenommen,
  weil BEIDE Seiten unveraendert bleiben und deshalb weiterhin denselben, bereits geprueften
  Kontrast wie ausserhalb von Hochkontrast haben). Jetzt auf `WindowColor` abgebildet, dazu
  `AccentSubtleBrush` (war in Runde 1 bereits auf `ControlColor` gesetzt, aber `SchachtUebersichtPanel.xaml`
  legt normalen `TextBrush`-Text direkt darauf — auf `WindowColor` vereinheitlicht) und
  `InputWarmBrush`/`VsaInputHighlightBrush` (Eingabefelder wie `WarmTextBox`,
  `ProtocolEntryEditorDialog.xaml`/`ObservationCatalogWindow.xaml` — dieselbe Flaeche, die eine
  normale `TextBox` in Hochkontrast bekaeme).
  **`ColorHeader`/`ColorWarning` sind rohe `Color`-Schluessel, keine Brush-Schluessel** — `Color`
  ist kein Freezable/DependencyObject, ein `DynamicResource` kann deshalb NICHT als Inhalt eines
  blossen `<Color x:Key="...">`-Ressourceneintrags stehen (anders als `SolidColorBrush.Color`,
  das ueber die DependencyProperty-Vererbungskette dynamisch bindet — das war die Grundlage der
  Runde-1-Loesung fuer `TextBrush` & Co.). `VsaCodeExplorerWindow.xaml` nutzte `ColorHeader` an
  sechs Stellen ueber den Umweg `<Border.Background><SolidColorBrush Color="{DynamicResource
  ColorHeader}"/></Border.Background>` — architektonisch bereinigt zu `Background="{DynamicResource
  HeaderBrush}"` (HeaderBrush ist exakt derselbe Wert, bereits korrekt ueberlagert). Fuer
  `ColorWarning` (ein 15-%-Opazitaets-Tupfer hinter dem „OPTIONAL"-Abzeichen) traegt die neue
  `WarningTintBrush` (Theme.xaml/ThemeLight.xaml, Opazitaet in der Brush selbst statt am
  Verwendungsort) jetzt die Farbe; in Hochkontrast ebenfalls auf `WindowColor`.
  **Drei weitere Dateien konvertiert:** `RecordDetailsWindow.xaml`,
  `SchachtMassnahmenKatalogEditorWindow.xaml`, `CostCatalogEditorDialog.xaml`,
  `PositionTemplateEditorDialog.xaml`, `ProtocolEntryEditorDialog.xaml`,
  `ObservationCatalogWindow.xaml`, `PlayerWindow.xaml`, `MeasureTemplateEditorWindow.xaml` (2 von
  3 Stellen). **Eine Stelle bewusst NICHT konvertiert** (`MeasureTemplateEditorWindow.xaml`,
  Wasserzeichen-Text „Suchen..." in einer `VisualBrush.Visual`): genau der von der Runde-1-
  Warnung genannte Risikofall (Freezable-Teilbaum ohne die normale FrameworkElement-
  Vererbungskette) — ohne echten WPF-Kindprozess-Test fuer dieses sehr kleine, rein kosmetische
  Detail ist ein blinder Wechsel ein ungeprueftes Risiko. **Damit ist der Sweep jetzt vollstaendig
  statt Stichprobe:** eine repo-weite Suche nach `StaticResource [A-Za-z0-9]*Brush` ausserhalb der
  Theme-Definitionsdateien selbst findet nur noch diese eine dokumentierte Ausnahme; alle anderen
  Treffer eines aehnlichen Musters sind Value-Converter (`ZkBrushConv`, `ZustandsklasseBrush`,
  `CodeGroupBrush`), keine Theme-Farbtoken. `SettingsPage.xaml`s Kommentar „in allen offenen
  Fenstern" ist jetzt GEPRUEFT statt behauptet (Kommentar nennt die eine Ausnahme).
  **`PrimaryButton`** (`Theme.xaml`/`ThemeLight.xaml`) fror `ColorAccent` ueber
  `<SolidColorBrush Color="{StaticResource ColorAccent}"/>` beim Laden ein — derselbe Fehler wie
  bei `ColorHeader`, nur mit Brush statt rohem Color; ersetzt durch `Value="{DynamicResource
  AccentBrush}"` (exakt derselbe Wert, kein Umweg).
  **`SurfaceBrush` je Fundstelle geprueft** (sieben Stellen, nicht sechs — Runde-1-Zaehlfehler
  korrigiert): ExportPage (2x, Pattern-Chips — Fuellung ist die offensichtlich beabsichtigte
  Pillenoptik), drei Dossier-Listenpanels (Blaetterliste, Suchergebnisse, Kapitelkopf — normale
  Panelflaechen) und zwei „Papier auf Arbeitsflaeche"-Stellen (`DossierPlanWindow`,
  `DossierPreviewWindow`): dort ist die eigentliche Papierseite ein separates inneres `Border`
  mit fest codiertem `Background="White"` (Schatten-Effekt, eigene Flaeche) — `SurfaceBrush`
  faerbt nur die umgebende Bildlauf-Arbeitsflaeche dahinter, das ist die beabsichtigte
  "Blatt auf farbigem Untergrund"-Optik, keine Regression. Keine der sieben Stellen musste auf
  `Transparent` zurueckgesetzt werden.
  Tests: `ThemeHighContrastCoverageTests` (zweiter Scan-Weg, per Sabotageprobe gegen echten
  Bug belegt), Build 0/0, `AuswertungPro.Next.UI.Tests` erneut vollstaendig gruen.
- **Aufgabe 13, Fix-Runde 4 (29.09.2026): Farbpaare je Zustand statt Attributpaare.**
  **Gefuellte Knoepfe tragen keine Hover-Toenung mehr.** Die Vorlage des impliziten Button-Stils
  stellt `MainBorder` unter der Maus per TargetName auf eine 4-%/8-%-Akzenttoenung - PrimaryButton,
  SuccessButton und die SeverityXButton erbten das: weisse Schrift auf fast weisser Flaeche (helles
  Theme), unter Hochkontrast HighlightText auf Window. Sie verwenden jetzt `FilledButtonTemplate`
  (Theme-Dateien, gleiche Form ohne Toenung); den Hover-/Druckton setzt der Style ueber
  `Style.Triggers` in VOLLER Farbe (Primary: AccentHover/AccentPressed, Success: neue
  `SuccessFillBrush`/`-HoverBrush`/`-PressedBrush`). ToolbarButtonAccent (beide Themes) nutzt
  `ToolbarAccentFillBrush` (hell: der bisherige Verlauf, dunkel: Akzent), AccentHover/AccentPressed
  (hell neu `#FF1E40AF`, vorher hart kodiert) und OnAccentBrush.
  **Ein eigener TextBlock im Knopfinhalt sucht seinen impliziten Stil im LOGISCHEN Baum** (Knopf,
  Fenster, App) - der B7-Block in `ContentPresenter.Resources` erreicht nur Text-Inhalt (String).
  Zur Laufzeit belegt: `<Button Style=PrimaryButton><StackPanel><TextBlock/></StackPanel></Button>`
  war TextBrush (#FF14213A auf Akzentblau). Deshalb tragen die gefuellten Stile und
  `CompactToggleButton`, `MarkToolPopupButton` (Player), `ActionBtn` (Fotofenster) und der
  Start-Knopf der Videoanalyse den Durchreich-Stil in `Style.Resources`/`Button.Resources` (gilt
  auch ueber BasedOn). Nie wieder nur ContentPresenter.Resources fuer eigenen Inhalt.
  **OnAccentBrush nur auf einer Flaeche, die unter Hochkontrast Highlight wird** (sonst ist sie in
  den normalen Themes Weiss auf beliebigem Grund). **Hochkontrast-only-Darstellung ueber
  transparente Tokens:** `HcAuswahlRahmenBrush` (normal Transparent, HC Highlight) auf einer eigenen,
  layoutneutralen Rahmenebene (TreeViewItem `HcAuswahlRahmen`, Zeilen der Sanierungsmatrix) - die
  Runde-3-Rahmen verschoben Knoten/Zeilen um 1 px und sind zurueckgebaut; `ChipZaehlerAktivBrush`
  (normal MutedBrush-Wert, HC HighlightText) fuer den Zaehler im gewaehlten Spalten-Chip.
  `AccentTextBrush` faellt unter Hochkontrast auf **HotTrack** (nicht HighlightText: das ist Schrift
  AUF Highlight, AccentTextBrush steht auf normalen Flaechen). Das Fotomessfenster (Video-Datei mit
  festen Farben) nutzt `FotoAkzentBrush` (roher `ColorAccent`, von Hochkontrast nicht ueberlagert)
  fuer gewaehlte Werkzeuge/Voreinstellungen, damit die festen weissen/farbigen Beschriftungen ihr
  Paar behalten. Sanierungsmatrix: der Zebra-Trigger las `ItemsControl.AlternationIndex` an der
  Border statt am Item-Container und griff nie - jetzt DataTrigger ueber `TemplatedParent`.
  **Waechter `ThemeHighContrastFarbpaarTests`** mit dem Zustandsmodell `XamlFarbpaarModell` (Tests):
  je Style/Vorlage/Ansicht/DataTemplate und je Zustand (Grundzustand, jede Trigger-Bedingung,
  IsPressed schliesst IsMouseOver ein, DataTrigger an Vorfahr-Eigenschaften als "^IsChecked=True")
  das wirksame Paar aus Flaeche und Schrift, mit WPF-Rangfolge (lokal > Style-Trigger >
  Template-Trigger > Setter; TargetName > Vorlagenattribut; TemplateBinding; BasedOn;
  Style.Resources; impliziter TextBlock-Stil; Knopfinhalt am ContentPresenter), fuer Hell UND
  Dunkel. Regeln: R1 Highlight-Flaeche nur mit HighlightText, andere Systemflaeche nie mit literaler
  oder HighlightText-Schrift; R2 OnAccent/SelectionText/NavSelectedText nur auf Highlight-Flaeche.
  Sieben dauerhafte Fundweg-Proben im Test, sieben Sabotageproben an echten Dateien (alle rot).
  Zwei begruendete Ausnahmen (geschuetzte WebGisHolenWindow, Legendenfarbe DeepSkyBlue); eine
  verwaiste Ausnahme macht den Waechter rot. Grenzen: Item-Container werden nicht mit ihrem
  ItemTemplate verbunden, Kombinationen unabhaengiger Trigger nur ueber IsPressed.
  `FuellknopfFarbenIsolatedSmokeTests` prueft dieselben Zustaende zur Laufzeit (echte Ressourcen,
  IsPressed per geschuetztem Setter, Hochkontrast eingeschaltet). **Solche Tests muessen die
  Elemente in ein echtes Fenster legen**: eine Aenderung der App-Ressourcen erreicht lose Elemente
  nicht, der Test waere blind (so beim ersten Lauf passiert).
- **Aufgabe 14 — Befehle in der Strg+K-Suche.** `GlobaleSucheRegel` (Application, weiterhin
  WPF-frei) sucht jetzt neben Haltung/Schacht/Strasse auch Befehle: `GlobaleSucheBefehlEintrag`
  (Schluessel, Anzeigename, Glyph, `Verfuegbar`, `Ausfuehren`-Action) ist der reine Vertrag; die
  Regel filtert nach `Verfuegbar`, matcht umlaut-tolerant und liefert hoechstens 6 Befehlstreffer
  neben den bisherigen hoechstens 12 Datentreffern. **Reihenfolge:** Enthaelt der Suchtext eine
  Ziffer (wirkt wie eine Haltungs-/Objektnummer), bleiben Datentreffer zuerst wie bisher; sonst
  gehen gut treffende Befehle voran (Wortsuchen wie «neu», «einstellungen» meinen meist einen
  Befehl). Die Umlautfaltung liegt seit dieser Aufgabe zentral in `Application/Common/
  SucheTextFaltung` (`Falte`/`PasstAlle`) — `SettingsSearchMatcher` (Einstellungssuche) delegiert
  dorthin, damit es nur eine Faltung im ganzen Programm gibt.
  `GlobaleSucheViewModel.BaueBefehle()` baut den Katalog bei jeder Eingabe frisch aus dem
  aktuellen `ShellViewModel`: alle Seiten der Leiste als «Gehe zu: `<Anzeigename>`» (Glyph und
  `Verfuegbar` direkt vom jeweiligen `NavItem.Icon`/`IsAvailable`) sowie die Hauptbefehle Neues
  Projekt, Projekt oeffnen, Speichern, Speichern unter, Einstellungen, Handbuch, Tastenkuerzel,
  Ueber SewerStudio und Fokusmodus — `Verfuegbar` kommt dort vom echten `IRelayCommand.
  CanExecute(...)`. **Ein Befehl ohne offenes Projekt ist deshalb kein deaktivierter, sondern ein
  UNSICHTBARER Treffer** (Entscheid: einfacher als ein zweiter IsEnabled-Zustand je Zeile, und
  konsistent mit «Gehe zu: Haltungen» vor dem Projektwechsel). «Import starten» ist bewusst ein
  ZWEITER Weg zur Import-Seite neben «Gehe zu: Import» (derselbe `NavigateTo("Import")`), damit
  der Suchtext «start» unabhaengig vom Wort «Gehe» trifft. Ausgefuehrt wird ausschliesslich ueber
  bestehende `ShellViewModel`-Befehle/-Methoden (`NavigateTo`, `IRelayCommand.Execute`) — keine
  Fachlogik verdoppelt.
  **Gruppenkopf «Befehle» ist reine UI-Deko, nie Teil der Regel:** `GlobaleSucheArt.Gruppenkopf`
  wird ausschliesslich von `GlobaleSucheViewModel.OnTextChanged` vor den ersten Befehlstreffer
  eingefuegt (Domain-/Application-Layer kennt keine Darstellungsfragen). Er ist nie auswaehlbar:
  `Waehle(...)` ignoriert ihn, `MarkiereNaechsten`/`MarkiereVorherigen` springen ueber ihn hinweg
  (`NaechsterAuswaehlbarerAufwaerts`/`…Abwaerts`), und im Popup (`MainWindow.xaml`) setzt
  `ListBox.ItemContainerStyle` bei `Art=Gruppenkopf` `IsHitTestVisible=False`/`Focusable=False`,
  damit ein Klick ihn nicht selektiert. Ein Befehlstreffer zeigt sein `Glyph` als `ui:FluentIcon`
  (dieselben Codepunkte wie im Menue Datei/Hilfe), Datentreffer bleiben ohne Icon. Der
  Suchfeld-Platzhalter heisst jetzt «Suchen oder Befehl eingeben…».
  Tests: `GlobaleSucheRegelTests` (Befehl gefunden, Umlautfaltung, Reihenfolge bei Zahl/Wort,
  Sperre ohne `Verfuegbar`, Obergrenze 6, Ruecksicherung ohne Katalog) und
  `GlobaleSucheBefehleTests` (echtes `ShellViewModel`, kein Fake: Befehl fuehrt wirklich aus,
  Navigation trifft die echte Seite, «Speichern» ist ohne offenes Projekt kein Treffer, Gruppenkopf
  nur bei Befehlstreffer, Pfeiltasten ueberspringen ihn).
  **Fix-Runde 1 (29.09.2026):** `Verfuegbar` ist nur der beim Bauen der Liste erfasste Stand —
  zwischen Anzeige und Klick kann ein Betriebs-Schutz (Import/Export laeuft) aktiv werden. Der
  `Befehl(...)`-Helfer prueft `CanExecute` deshalb UNMITTELBAR vor `Execute` ein zweites Mal;
  ein inzwischen gesperrter Treffer fuehrt beim Klick nichts mehr aus. Zusaetzlich abonniert
  `GlobaleSucheViewModel` `CanExecuteChanged` von `SaveCommand`/`SaveAsProjectCommand`/
  `NewProjectCommand`/`OpenProjectCommand`: Aendert sich deren Verfuegbarkeit, waehrend die Liste
  bereits offen ist, baut sie sich sofort neu auf — ein jetzt gesperrter Treffer verschwindet von
  selbst, statt bis zum naechsten Tastendruck stehen zu bleiben. Die sieben Hauptbefehle-Glyphen
  (dieselben Segoe-Fluent-Codepunkte wie `MainWindow.xaml`) waren von Anfang an korrekt gesetzt,
  standen im Quelltext aber als rohe, in den meisten Editoren unsichtbare PUA-Unicode-Zeichen statt
  als lesbare `\uXXXX`-Escapes — inhaltlich unveraendert, jetzt aber auch im Diff/Review erkennbar.
  Tests: `Speichern_fuehrt_nichts_mehr_aus_wenn_waehrend_der_anzeige_ein_betriebs_schutz_aktiv_wird`,
  `Ein_treffer_verschwindet_aus_der_offenen_liste_sobald_ein_betriebs_schutz_aktiv_wird`,
  `Jeder_hauptbefehl_hat_ein_sichtbares_glyph` (Theory, alle acht Hauptbefehle).
- **Aufgabe 15 — Gemeinsame Quelle fuer das Logo in Berichten.** `IBerichtsMarke`
  (`Application/Reports`, reine Daten: `LogoPfad`) ersetzt die rund sieben Stellen, die den
  Programmordner-Pfad `Assets/Brand/abwasser-uri-logo.png` je einzeln zusammensetzten und
  pruefen. `BerichtsLogoResolver.Resolve(configuredPath, appBaseDirectory, fileExists)`
  (`Application/Reports`, reine Regel ohne echten Dateizugriff) ist die eine Aufloesung:
  eine gesetzte UND vorhandene Einstellung geht vor, sonst das mitgelieferte Standardlogo,
  sonst kein Logo (`null`) — nie ein erfundener Pfad. `AppSettingsBerichtsMarke`
  (`UI/Services`, Infrastruktur kann `AppSettings` aus der UI-Schicht nicht referenzieren)
  liest `AppSettings.BerichtsLogoPfad` bei JEDEM Zugriff frisch (Muster wie
  `AppSettingsProtocolPdfLayoutSettings`), damit eine geaenderte Einstellung ohne
  Programmneustart beim naechsten Export wirkt; ein Lesefehler liefert `null` statt
  abzustuerzen. Registriert im `ServiceProvider` als `BerichtsMarke` (175 -> 176,
  `ServiceProviderRegistrationMap`).
  Neue Einstellung «Logo für Berichte» in Einstellungen ▸ Allgemein ▸ Gruppe «Berichte»
  (direkt unter «Frühere Ansichten»): Pfadanzeige (nur lesbar — «Auswählen…»/«Zurücksetzen»
  sind die einzigen Schreibwege, kein Speichern je Tastenanschlag), Vorschau-Miniatur
  (`FileToImageConverter`, bereits vorhanden aus dem Training Center) zeigt das gerade
  WIRKSAME Logo (Einstellung oder Standard). `SettingsPageViewModel.OnBerichtsLogoPfadChanged`
  speichert sofort (Muster wie `ReduceMotion`) und meldet `BerichtsLogoVorschauPfad`/
  `BerichtsLogoAnzeige` neu, damit die Vorschau sofort nachzieht.
  **Alle sieben Alt-Stellen sind umgestellt**, ohne Layout/Feldnamen zu aendern — jede behaelt
  ihren bisherigen Rueckfall, wenn keine `IBerichtsMarke` injiziert ist (Alt-/Testkonstruktoren):
  `DataPagePrintController` (Dossier- UND Hydraulik-PDF, neuer privater `ResolveLogoPath()`),
  `ProtocolObservationsWindow.xaml.cs` (liest direkt `_sp.BerichtsMarke`),
  `ProtocolRegenerationAdapter` (Infrastructure, neuer optionaler Konstruktorparameter),
  `NpkLeistungsverzeichnisExcelExportService.ResolveLogoPath` (von `private static` zu
  `internal` fuer Testbarkeit ohne echte Bilddatei/ClosedXML), `OfferPdfExportService`/
  `NpkOfferPdfExportService` ueber den gemeinsamen `OfferPdfTemplateExport.RenderAsync(...,
  berichtsMarke)`. **`CodingProtocolPdfExportPlanner.Build`** (Coding-Modus, «Als PDF
  exportieren») ist die EINE bewusste Ausnahme, die nur den Standardpfad-STRING teilt
  (`BerichtsLogoResolver.DefaultLogoPath(...)`), nicht die Einstellung: `UI/Ai` ist fuer neue
  Ablaufklassen eingefroren, und `UiArchitectureGuardTests.Ui_code_accesses_App_Services_only_
  at_composition_root` erlaubt den zentralen Dienstecontainer wortwoertlich nur in
  `MainWindow.xaml.cs` — erst real gemessen, weil der erste Versuch (Zugriff ueber
  `CodingProtocolPdfExportServiceFactory`) genau diesen Waechter rot schlug. Ein optionaler
  `resolveLogoPath`-Parameter bleibt an `Build` fuer Tests/spaetere Verdrahtung, wird aber vom
  produktiven Aufrufer nicht gesetzt. Zwei weitere tiefere Fallback-Stellen
  (`HaltungsDossierPdfBuilder.ResolveLogoBytes`, projektinterner Rueckfall wenn der Aufrufer
  gar keinen Pfad liefert; `ProtocolPdfAssetFileResolver.BuildLogoCandidates`, sucht NUR
  projektrelative Logo-Ueberschreibungen, nie den App-Standardpfad) bleiben aus demselben Grund
  (statische Klasse ohne DI) bewusst bei der reinen Zeichenkette — `HaltungsDossierPdfBuilder`s
  App-Basis-Rueckfall verwendet dafuer ebenfalls `BerichtsLogoResolver.DefaultLogoPath(...)`
  statt einer eigenen Literalkopie. **Eine per Einstellung ausgetauschte Logodatei wirkt daher
  ueberall AUSSER im Coding-Modus-PDF-Export** (dort weiterhin nur das mitgelieferte
  Standardlogo neben dem Programm) — ein groesserer Umbau des `UI/Ai`-Composition-Roots waere
  fuer diese eine Stelle noetig und war nicht Teil dieser Aufgabe. Layout/Aussehen der
  PDFs/Excel/Dossiers ist unveraendert — nur die Pfadquelle ist zentral. Tests:
  `BerichtsLogoResolverTests` (Standard/Einstellung/fehlende Datei/leere Einstellung),
  `AppSettingsBerichtsMarkeTests`, `SettingsPageViewModelBerichtsLogoTests`
  (Auswaehlen/Zuruecksetzen/Vorschau/Sofortspeicherung), plus je ein Fall in
  `DataPagePrintControllerTests`, `ProtocolRegenerationServiceTests`,
  `NpkLeistungsverzeichnisExcelExporterTests` und `OfferPdfExportServiceTests`, der die
  injizierte `IBerichtsMarke` tatsaechlich ankommen sieht, sowie ein Fall in
  `CodingProtocolPdfExportPlannerTests` fuer den optionalen `resolveLogoPath`-Parameter.
- **Aufgabe 16 — Rückgängig/Wiederholen für Haltungs- und Schachtdaten.** `IDatenaenderungsVerlauf` /
  `DatenaenderungsVerlauf` (Application/UseCases/Datenaenderungen, WPF-frei, 177. Registrierung — 176 war
  bereits an Aufgabe 15/`IBerichtsMarke` vergeben) haelt je
  Bereich (Haltungen, Schaechte) einen Stapel, Tiefe 100; eine neue Eingabe leert Wiederholen.
  **Erfasst wird als BEREICH, nicht je Schreibweg:** `using (verlauf.Erfasse(datensatz, feld))` merkt VOR
  der Eingabe Wert UND `FieldMeta` aller Felder (Kopie, `FieldMetadataKopie` — die Schreibwege aendern Meta
  an Ort und Stelle) und vergleicht beim Schliessen. Alles darin ist EIN Schritt (Sanieren+Kosten,
  DN+Breite, «Spalte leeren» ueber `ErfasseMehrere`, Objektakte samt `ZieheAbhaengigeFelderNach`); ein
  innerer Bereich wird Teil des aeusseren. Ein blosses Nachstempeln ohne Wertaenderung ist kein Schritt.
  Eingaenge: Tabellenzelle (Beginn beim OEFFNEN — `PreparingCellForEdit`/`BeginningEdit`, die
  Zustandsklassen-Marke schreibt vor dem Schliessen —, Ende nach dem Commit mit Input-Prioritaet ueber
  `DatenVerlaufZellErfassung`), Auswahlfelder der Tabelle, Formular/Aufklappliste (Commit-Delegate), «Spalte
  leeren» und die Objektakte (`ObjektaktenBearbeitung.Verlauf`, gesetzt in `ObjektaktenDialog`; erfasst auch
  die Aktenwerte des Verbunds und entfernt eine erst dabei angelegte Wurzelakte wieder). Die Seiten haben
  dafuer Huellen `…MitVerlauf` (`DataPage.Verlauf.cs`, `SchaechtePage.Verlauf.cs`); XAML und Konstruktor
  zeigen auf die Huellen, die alten Handler bleiben unveraendert (SchaechtePage.xaml.cs steht bei 1000
  Zeilen). **Wiederherstellen** laeuft ueber `HaltungRecord/SchachtRecord.StelleFeldzustandWiederHer`:
  Wert und Meta zeichengenau (keine WebGIS-Umwandlung, keine neue Handmarke, alter Zeitstempel; `null` =
  Feld/Meta gab es nicht). Den Schutz der Schreibwege ersetzt die **Vorbedingung**: zurueckgesetzt wird nur,
  wenn das Feld noch exakt den eigenen Stand traegt (Wert, Herkunft, Handmarke, Konfliktnotiz — Zeitstempel
  ausgenommen) und der Datensatz noch im gebundenen Projekt ist; sonst wird der Eintrag verworfen, nichts
  geschrieben, Hinweis. So ueberschreibt Rueckgaengig nie einen Wert eines anderen Schreibers.
  **Sperren (Verlauf leer, Toast nur wenn er Eintraege hatte):** Umbenennungsfelder (Haltungsname,
  Schacht_oben/_unten, Schachtnummer — Aenderung wird nie erfasst), Projektwechsel (`ShellViewModel.
  ReplaceProject` → `Binde`), jede Listenaenderung von `Data`/`SchaechteData` (neu, loeschen, verschieben,
  auch aus Importen), jeder laufende Projektvorgang (`NotifyShellOperationCommands` →
  `PruefeDatenVerlaufBeiVorgang`: Import, Verteilung, WebGIS, Laden) und Uebernahmen (GeoShop, QGIS,
  WebGIS-Holen ueber `MeldeUebernahme` → `SeitenUebernahme.Abschliessen`). Nicht erfasst (Vorbedingung schuetzt):
  Nachschlagen, Strassennamen, Durchnummerieren, GeoShop-Einzelergaenzung in der Akte.
  **Bedienung:** Menue «_Bearbeiten» zwischen Datei und Werkzeuge («Rückgängig: Rohrmaterial 10001-10002»,
  Unterstrich im Namen verdoppelt), Strg+Z / Strg+Y / Strg+Umschalt+Z als Fenster-KeyBindings; die
  Tastenbefehle sind nicht ausfuehrbar, solange ein `TextBoxBase`/`PasswordBox` den Fokus hat (dann gilt
  dessen eigenes Rueckgaengig). Nur auf Haltungen/Schaechte und ohne laufenden Vorgang ausfuehrbar; die Shell
  reicht an `DataPageViewModel/SchaechtePageViewModel.WendeVerlauf` weiter (MarkProjectDirty, vorhandener
  Autosave, `FelderExternErgaenzt` fuer Formular/Objektakte). Tastenkuerzel-Fenster (Gruppe «Haltungen und
  Schächte») und Handbuch nennen es. Tests: `DatenaenderungsVerlaufTests` (Infrastructure, 18: exakte Meta,
  keine Handmarke, WebGIS-Begriff zeichengenau, Tiefe, Gruppen, Sperren, Vorbedingung, geloeschter
  Datensatz, Objektakte), `DatenVerlaufShellTests` (UI, echte Shell/Seiten, Projektwechsel, Vorgang,
  Verdrahtung, Menue/Tasten).
  **Fix-Runde 1:** Eine im Schritt neu angelegte Objektakte wird beim Rueckgaengig nur entfernt, wenn sie
  GANZ dem eigenen Nachher-Stand entspricht (JSON der ganzen Akte: Werte, Quellen, Bezuege, Unterlisten,
  Hauptdeckel, Zusatzdaten); Wiederholen fuegt sie nur an, wenn keine Akte mit derselben Kennung existiert —
  sonst Schritt verworfen, Warnung. Aktenwerte vergleichen Zusatzdaten tief. **Tabellenzelle und
  Auswahlspalte erfassen nur das eigene Feld plus das, was es ableitet** (`DatenaenderungsVerlauf.
  ZellSchrittFelder`: Sanieren Ja/Nein → `SanierungCostFieldMapper.CostFieldNames`); Formular, «Spalte
  leeren» und Objektakte den ganzen Datensatz. Scheitert das Anwenden mitten im Schritt, werden alle
  angewendeten Teile zurueckgesetzt und der Eintrag faellt weg (nie halb wiederholbar); scheitert auch das,
  wird der Verlauf geleert (`GrundFehler`). Weitere Uebernahme-Sperren: WebGIS-Holen leert im gemeinsamen
  `WebGisHolenAblauf` (auch fuer den Export-Einstieg ohne eigenen Rueckruf), Zusatzdatei und
  GeoShop-Ergaenzung in der Objektakte ueber `ObjektaktenDialog.MitSperre`. Strg+Z ist waehrend einer
  offenen Eingabe (`EingabeOffen`) nicht ausfuehrbar; «nicht möglich» ist eine Warnung, das Leeren ein
  Hinweis (auf den UI-Thread verschoben). Das abgedockte Tabellenfenster (`FloatingGridWindow`) hat dieselben
  Tasten (`DataPageViewModel.RueckgaengigTasteCommand` reicht die Shell-Befehle durch). `ShellViewModel.
  Dispose` loest alle Abos (`LoeseDatenVerlauf`). Tests `DatenaenderungsVerlaufFixRundeTests`,
  `DatenVerlaufFixRundeTests`.
- **Aufgabe 17 — Kleinigkeiten und Endkontrolle.** Uebersicht: `ProjektUebersichtPageViewModel.
  BaueZustandLegende` sortiert jetzt Z0 (sofort) zuerst bis Z4 (kein Handlungsbedarf) zuletzt —
  vorher stand die dringendste Klasse ganz unten, im Widerspruch zur KPI-Karte "Dringend
  (Z0/Z1)" direkt darueber. Die Karte "Haeufigste Schaeden" zeigte den Hauptcode DOPPELT
  (eine eigene Mono-Spalte links UND nochmals eingebettet im `Klartext` der Zeile rechts, z. B.
  "BAB" links und "12 BAB (Riss)" rechts) — jetzt einmal `Klartext` ("BAB (Riss)") links,
  `Anzahl` allein rechts. Die Karte "Sanierungsverfahren" (und "Haeufigste Schaeden" ohne
  Befunde) zeigen bei leerem Bestand `EmptyStateControl` statt einer eigenen Textzeile (Regel
  aus Aufgabe 11). **Die Lernbereitschafts-Ampel ("Rot · Lernbasis: 0 Faelle") ueber der
  Haltungen-Werkzeugleiste ist unsichtbar, solange kein einziger Fall gelernt wurde**
  (`LearningReadinessPresenter.Build`, `IsVisible = stats.TotalSamples > 0`) — vorher stand sie
  dauerhaft rot auf jedem frischen Projekt, ohne etwas Handlungsleitendes zu sagen. Ampel-
  Schwellenwerte (25/100 Faelle) und der Infotext sind unveraendert, nur die Anzeige-Entscheidung
  ist neu. Schaechte: `SchaechtePageViewModel.LoadColumnsFromTemplate` meldet einen erfolgreichen
  Ladevorgang nicht mehr als sichtbaren Status "Spalten geladen: N" — `LastResult` bleibt wie an
  den beiden Fehlerzweigen daneben fuer echte Probleme reserviert.
  **`ShellViewModel.ProjektPfad`** (ToolTip unter der Wortmarke) verlangt jetzt zusaetzlich
  `HasPersistedProject`: `Settings.LastProjectPath` ist eine programmweite "zuletzt benutzt"-
  Einstellung, kein Attribut des aktuellen Projekts — ohne diese zweite Pruefung (derselbe
  Schutz, den `TrySaveProjectCore` bereits gegen ein Ueberschreiben verwendet) haette ein neues,
  noch nicht gespeichertes Projekt den Pfad des zuletzt geoeffneten Projekts gezeigt.
  `InitNova`s PropertyChanged-Filter reagiert deshalb zusaetzlich auf `HasPersistedProject`
  (sonst zieht der ToolTip nicht nach, wenn nur dieses Flag zuletzt wechselt, ohne dass sich
  `IsProjectReady` dabei tatsaechlich noch aendert).
  Player: Kopf zeigt nur noch den Dateinamen; `VideoPathText` traegt weiterhin (unveraendert von
  `PlayerWindowHeaderControls.ApplyVideoInfo`) den vollen Pfad, ist aber `Visibility="Collapsed"`
  und der volle Pfad haengt stattdessen als ToolTip an der ganzen Kopfzeile (`PlayerKopfzeile`).
  "Play"/"Stop" heissen "Abspielen"/"Stopp"; der Play-Knopf ist kein Akzentknopf mehr (Knopfregel:
  hoechstens ein Akzentknopf je Leiste) — der Codier-Modus-Knopf bleibt der einzige Akzent in der
  Bedienleiste. Der unbeschriftete "···"-Geschwindigkeitsstufen-Knopf (`SpeedPresetButton`)
  bekommt `AutomationProperties.Name`; sein Tooltip "Schneller — Taste +" bleibt woertlich
  stehen (Gegenstueck zu "Langsamer — Taste −" am Regler daneben, `DesignAuditPlayerShortcutTests`
  verlangt beide Zeichenketten irgendwo im Fenster).
  Training Studio: Die fuenf Schadensstufen-Knoepfe stehen in einem `UniformGrid Columns="5"`
  statt einer `StackPanel` mit festen Pixelbreiten — sie teilen sich immer die volle verfuegbare
  Breite, statt bei einem schmaleren Fenster abgeschnitten zu werden. Die Bogen-/Rohrende-
  Vorschlagstabelle in der schmalen Werkzeugspalte (~210-240 px) hat engere, weiterhin volle
  Spaltenbreiten (Art 100/Ort mind. 70/Stufe 55/Konfidenz 70/Bilder 50 statt 130/*/70/80/60) und
  `HorizontalScrollBarVisibility="Auto"`; der bestehende waagrechte Bildlauf (Aufgabe/B4) bleibt
  fuer den Rest.
  Vier fensterferne Fundstellen tragen den Standard-Logopfad seit Aufgabe 15 zwar bereits ueber
  `IBerichtsMarke`, ihr jeweiliger Rueckfall OHNE Injektion baute den Pfad aber noch als eigene
  Zeichenkette nach statt `BerichtsLogoResolver.DefaultLogoPath(...)` zu rufen
  (`NpkLeistungsverzeichnisExcelExporter`, `ProtocolRegenerationAdapter`, `OfferPdfTemplateExport`,
  `DataPagePrintController`) — jetzt vereinheitlicht; Verhalten unveraendert, nur eine Quelle
  weniger. `ServiceProviderRegistrationTests`-Kommentar korrigiert (`IBerichtsMarke` hat nur
  `LogoPfad`, keine Fusszeile). `AboutWindow`-Fenstertitel heisst "SewerStudio — Über das
  Programm" (vorher "SewerStudio — Über SewerStudio", eine Verdopplung der Wortmarke).
  `GeoShopAbgleichPlanBuilder`: ein Hinweistext, dessen Ursache bereits mit "Technische Details
  stehen im Programmlog." endete, bekam durch das angehaengte " – ausgelassen." einen Punkt
  direkt vor einem Gedankenstrich — der eingebettete Meldungstext wird jetzt vor dem Anhaengen
  getrimmt (`.TrimEnd('.')`). `SchachtSanierungsMatrixPageViewModel`: "Schacht/Schaechte" ->
  "Schacht/Schächte" (Umlaut).
  **Neuer Token `SuccessBadgeTextBrush`** (Theme.xaml/ThemeLight.xaml, beide Weiss — `ColorSuccess`
  ist in beiden Themes derselbe Wert, schwarze Schrift erreicht darauf nur 4,19:1): Die drei
  Overlay-Badges im Training Studio (`TrainingStudioWindow.AddOverlayBadge`: Segmentierung
  erfolgreich/nicht darstellbar, Hand-Box) verwendeten alle `StatusBadgeTextBrush` — den Token
  fuer die theme-gleichen Z0-Z4-Zustandsklassen-Abzeichen, nicht fuer eine theme-abhaengige
  Fuellfarbe wie `SuccessBrush` (Aufgabe-12-Regel). `AddOverlayBadge` nimmt jetzt Hintergrund-
  UND Vordergrund-Token als Parameter (Success/Warning/DangerBadgeTextBrush je Fall).
  **Isolierter Pruefhost repariert:** `docs/reviews/2026-09-06-nova/wpf-etappe-2/werkzeug/`
  (Program.cs, Pruefhost.csproj) zeigte seit dem Ordnerumzug auf 5.0 (13.09.2026) noch auf den
  nicht mehr vorhandenen alten Worktree `C:\Sewer-Studio_KI_4.5-nova` — drei Konstanten und ein
  MSBuild-Property zeigen jetzt auf den aktuellen Arbeitsbaum, der Root-Profilordner bleibt ein
  eigener `.tmp`-Ordner (nie das echte Benutzerprofil). **Offener, real geprüfter Befund im
  Werkzeug selbst (nicht im Produkt):** Seiten, die `BearbeitungErledigtKnopf`
  (`BasedOn="{StaticResource ToolbarButton}"`, `Controls.xaml`) lazy laden — Haltungen, Schaechte
  und darueber auch Player/TrainingStudio, die zuerst zur Haltungsseite navigieren — werfen im
  Pruefhost `XamlParseException: Ressource "ToolbarButton" nicht gefunden`, real reproduziert
  auch nach Umstellung auf einen einzigen in sich konsistenten `XamlReader.Parse`-Aufruf (kein
  nachtraeglicher `MergedDictionaries[0]`-Tausch mehr). Vermutete Ursache: `XamlReader.Parse`
  einer nachgebauten `App.xaml` kennt beim deferred Laden eines Stils aus einer per `Source=`
  gemergten Datei (`Controls.xaml`) nicht denselben App-weiten Aufloesungskontext wie echtes
  kompiliertes BAML — im Produkt selbst funktioniert dasselbe `BasedOn` uebergreifend nachweislich
  (die Bearbeitung-erledigt-Markierung ist seit dem 13.09.2026 produktiv). Uebersicht, Import und
  Einstellungen sind davon NICHT betroffen und wurden erfolgreich fotografiert (hell + dunkel,
  `docs/reviews/2026-09-28-optik/bilder/`); Haltungen, Schaechte, Player und Training Studio
  brauchen fuer neue Bildschirmfotos entweder eine tiefere Reparatur dieses Werkzeugs (echtes
  `InitializeComponent()` statt nachgebautem XAML) oder eine Sichtpruefung im echten Programm.
- **Nachtrag zur Schlusswelle — drei Regeln, die nicht zurueckfallen duerfen.**
  **`DatenaenderungsErgebnis.Teilweise`:** Scheitert ein Rueckgaengig/Wiederholen NACH dem
  Anwenden beim Rueckbau eines Teils (`DatenaenderungsVerlauf.Wende`, `zurueckgesetzt=false`),
  koennen an den betroffenen Datensaetzen bereits Feldwerte stehen geblieben sein, obwohl
  `Angewendet=false` bleibt — ein Aufrufer, der nur auf `Angewendet` prueft, wuerde Projekt/
  Anzeige NICHT aktualisieren, obwohl sich Daten geaendert haben. `Teilweise=true` traegt in
  diesem Fall zusaetzlich die betroffenen Datensaetze mit, damit Dirty-Markierung und Anzeige
  trotzdem nachziehen; im echten "nichts geaendert"-Fall (`zurueckgesetzt=true`) bleibt es beim
  leeren `[]`/`Teilweise=false`. **Verschachtelte Erfassung eines fremden Datensatzes wird NICHT
  in den aeusseren Schritt gemischt:** Ein innerer `Erfasse(...)`/`ErfasseObjektakte(...)`-Bereich
  waehrend eines bereits offenen aeusseren wird nur dann Teil desselben Strg+Z-Schritts, wenn er
  ausschliesslich Datensaetze betrifft, die der aeussere Bereich bereits kennt (abhaengiges Feld,
  Auswahlfeld in derselben offenen Zelle — immer dasselbe Objekt). Oeffnet er waehrenddessen einen
  dem aeusseren VOELLIG fremden Datensatz, wird diese innere Erfassung ignoriert (kein eigener
  zweiter Verlaufseintrag) — der eigentliche Schreibvorgang laeuft unveraendert weiter, landet nur
  nicht im Rueckgaengig-Verlauf; sonst wuerde ein Strg+Z ein unabhaengiges zweites Objekt
  mitreissen (`DatenaenderungsVerlauf.Oeffne`, `istFremderDatensatz`).
  **Der StaticResource-Theme-Token-Waechter (`DesignAuditKeinStaticResourceThemeTokenTests`)
  nimmt nur noch die drei Theme-DEFINITIONSDATEIEN aus, nicht mehr den ganzen `Theme/`-Ordner:**
  `Theme.xaml`/`ThemeLight.xaml`/`ThemeHighContrast.xaml` legen die Tokens fest (dort ist ein
  `StaticResource`-Verweis auf ein anderes Token derselben Datei unproblematisch), aber
  `Theme/Controls.xaml` baut nur Steuerelement-Stile aus diesen Tokens und muss deshalb wie jede
  andere XAML-Datei zeigen, dass sie Farben ausschliesslich als `{DynamicResource ...}` bezieht —
  die urspruengliche Ausnahme des ganzen Ordners haette einen kuenftigen `StaticResource`-Fehler
  genau dort nicht gefunden. Ausserdem: `DatenVerlaufTasten.MenuText` verdoppelt einen Unterstrich
  in der Beschreibung bewusst (WPF-Menue-Zugriffstaste) — das gilt NUR fuer echte Menuepunkte.
  Ein reiner `TextBlock` ausserhalb eines Menues (die Trefferliste der globalen Suche, Strg+K)
  kennt keine Zugriffstasten und wuerde die Verdopplung sichtbar falsch anzeigen;
  `DatenVerlaufTasten.SuchText` ist die unescapte Fassung dafuer, `ShellViewModel.
  RueckgaengigSuchText`/`WiederholenSuchText` reichen sie an `GlobaleSucheViewModel` weiter. Nie
  `RueckgaengigMenuText`/`WiederholenMenuText` ausserhalb eines echten Menues verwenden.

## Startanimation 5.0: Kugel, deutlich neu (14.09.2026)

- Entscheid Pascal nach einer Browser-Vorschau mit vier Varianten
  (`docs/reviews/2026-09-14-startanimation/`, Kamerafahrt / Leitungsnetz / Kugel straffer /
  Kugel deutlich neu): Die zurueckhaltende Fassung C war «deutlich zu wenig neu», umgesetzt
  ist D. `StartupSplashChoreografie` (Views/Windows, reine Rechnung neben
  `StartupSplashAnimationPolicy`) liefert je Bild: Einflug der Knoten von 2,0-3,3
  Kugelradien aussen (0,05-1,85 s, Spiralreihenfolge, je 0,8 s), Kugelwachstum 72->100 %
  in 2 s, Ringe ab 0,3/0,5/0,7 s als wachsender Bogen, Verbindungen erst nach Ankunft
  beider Enden mit Aufblitzen, Impulse ab 1,9 s, Scanwelle bei 3,0 s und Ringwelle aus dem
  Kern bei 5,0 s (beide alle 5,4 s weiter, solange geladen wird), gruene Bereit-Welle 0,9 s.
- **Kein Storyboard mehr auf Kern, Ringen, Knoten oder Linien.** `RenderFrame` und
  `UpdateBackdrop` setzen Deckkraft und Farbe je Bild aus der Choreografie; ein haltendes
  WPF-Storyboard auf `Opacity` ueberstimmt jeden spaeteren direkten Wert. Nie wieder
  `FadeIn(...)` auf Elemente legen, die je Bild gesetzt werden.
- Ringe zeichnen sich ueber `StrokeDashArray` (ein langer Strich, riesige Luecke) als
  Bogen; waehrend des Bogens ist ihr Bitmap-Cache aus. Je Knoten gibt es eine `Line` als
  Leuchtspur, fuer Ringwelle und Bereit-Welle je einen Kreis (`BuildWellenkreise`).
- Die Ueberzeile «KI-GESTUETZTE KANALINSPEKTION» ist entfernt (Entscheid Pascal). Rechte
  Haelfte, Chips, Versions-Chip und Ueberspringen sind sonst unveraendert; kein Gold, kein
  Dunkel. Der Bereit-Moment kommt fruehestens nach 8 s (`MinimumDisplayTime`).
- Nachweise: `StartupSplashChoreografieTests` (33) und die `StartupSplash*`-Tests; die
  Sichtprobe im Programm macht Pascal. Ein laufendes SewerStudio sperrt `bin\Debug`:
  Testprojekt mit `-o .tmp/testout-splash` bauen, die DLL direkt testen und `bin\Debug`
  erst nach dem Schliessen neu bauen, sonst zeigt der Neustart die alte Fassung.
## Persoenliche Erledigt-Markierung (13.09.2026)

- `HaltungRecord.BearbeitungErledigt` und `SchachtRecord.BearbeitungErledigt` sind
  eigene boolesche Arbeitsmarkierungen mit PropertyChanged. Standard false;
  nur true wird additiv im Projekt-JSON gespeichert. Projektkopie und Inhaltssignatur
  erfassen sie. Sie sind keine Fields und gehen nicht in Fach-/Normexporte.
- **`Offen_abgeschlossen` beschreibt die Sanierung, nicht den Bearbeitungsstand.**
  Die neue Markierung veraendert weder dieses Feld noch KI-/Pruefstatus.
- Beide `*PageViewModel.Erledigt`-Anbindungen schalten nur einen Datensatz des
  aktuellen, bereiten Projekts um, markieren es geaendert und nutzen die bestehende
  automatische Speicherung. Die Schacht-Importsperre gilt auch hier.
- Der Knopf `Erledigt` markiert die Auswahl; erneutes Klicken hebt die Marke auf.
  Aufklapplisten zeigen ein gruenes Haekchen am Namen, Tabellen im Zeilenkopf.
  Gemeinsame Styles liegen in `Theme/Controls.xaml`.
- `SplitterPersistenceBehavior` stellt fuer explizit ausgeblendete Splitter keine
  gespeicherte Groesse wieder her. Sonst reservierte ein Loaded-Ereignis erneut
  270 px fuer die unsichtbare Eingabeschublade unter der Aufklappliste.
- Nachweise: `BearbeitungErledigtSpeicherungTests`, `BearbeitungErledigtUiTests`.
  Anleitung: `docs/BEARBEITUNG-ERLEDIGT.md`.

## Aufklapplisten: Reihenfolge (09.09.2026)

`ListenReihenfolgeController` verbindet beide Aufklapplisten mit dem vorhandenen
`MoveToPosition`-Weg ihrer ViewModels. Ziehgriff ist ausschliesslich die Nr.-Zelle;
`ListenEinfuegelinie` zeigt davor/danach, der Rand scrollt automatisch.
`ListenReihenfolgeLeiste` bietet auch Zielposition, Anfang und Ende an.
Filter, Suche, Sortierung und ein seit Ziehbeginn geaenderter Bestand sperren
den neuen Verschiebeweg. Ein Abbruch schreibt nichts; eigene Listeninstanz und
Datensatzreferenzen binden den Ziehvorgang. Keine neue Registrierung und kein
neues Speicherformat. Bedienung und Pruefgrenzen: `docs/reviews/2026-09-06-nova/aufklapp-liste/REIHENFOLGE.md`.

### Design-Feinschliff 2026-09-03 (Audit `docs/DESIGN-AUDIT-2026-09-03.md`, Q1-Q6)

Waechter `DesignAuditFeinschliffTests` (7 Tests) haelt fest, was nicht zurueckfallen darf:

- Sichtbare Texte (`Content/Header/Text/ToolTip/Title`, auch `StringFormat` in Bindungen)
  schreiben echte Umlaute. Die `ae/oe/ue`-Konvention gilt nur fuer Quellcode und
  Kommentare. Schweizer `ss` bleibt (kein `ß`).
- Menuepunkte mit literalem `Header` tragen ein `MenuItem.Icon` (Fluent-Glyph); nur
  checkbare Punkte, Menueleisten-Koepfe (`_Datei`) und Punkte mit eigenem Header-Inhalt
  sind frei.
- Bedienelemente verwenden `ui:FluentIcon` statt Textsymbolen (`▲▼✕⚠📷↶↷⟲⟳`); gerade
  Pfeile im Fliesstext bleiben erlaubt. Im Code liefert die Erweiterung
  `FluentGlyphKnopf.MitGlyph(...)` Glyph plus zugaenglichen Namen aus dem Tooltip.
  Ausnahmen mit Grund: `DataPageConverters` (Tabellen-Haekchen), `ShellViewModel` (Punkt
  fuer „ungespeichert" im Fenstertitel).
- Jedes Fenster traegt `ui:WindowFx.Entrance="True"`; ausgenommen MainWindow, PlayerWindow,
  LiveFrameWindow, StartupSplashWindow.
- `KeyboardFocusVisual` sitzt auch auf CheckBox, RadioButton, ComboBox, Expander,
  TreeViewItem, TabItem, Slider und GridViewColumnHeader.
- Feste Farbwerte (`#RRGGBB`) gibt es nur in den sechs Video-Dateien (PlayerWindow,
  PlayerCodingSidePanel, LiveFrameWindow, PhotoMeasurementWindow, StartupSplashWindow,
  PipeGraphTimeline). Neue Tokens: `ScrimBrush`, `StatusBadgeTextBrush` (je Theme) und
  `Video*Brush` (theme-unabhaengig in `Controls.xaml`) fuer die Player-Abdunkelungen.

Nachpruefung 2026-09-06: Der `NachschlagKontextmenueTests`-Kindprozess erreicht das
60-s-Limit auch einzeln. Die fruehere Aussage "allein rund 26 s gruen" ist fuer den
aktuellen Stand nicht mehr belastbar. Hängepunkt und Produktursache bleiben offen
(A05, `docs/audits/2026-09-06-programmaudit/`).
Der Paket-1-Gesamtlauf bestand diesen Test einmal in 1,56 s; das ist keine Ursachenbehebung.

**Schriftskala (M1, 2026-09-03, Waechter `DesignAuditSchriftskalaTests`):** Sieben
`sys:Double`-Tokens in `Controls.xaml` — `TextXS` 11, `TextS` 12, `TextM` 13, `TextL` 15,
`TextXL` 18, `TextTitle` 22, `TextDisplay` 28 — plus `IconHero` 36 fuer grosse
Leerzustand-Glyphen. **11 px ist die Untergrenze im ganzen Programm** (Entscheid Pascal).
Seiten, Fenster, Controls und Dialoge setzen `FontSize` nur noch als
`{DynamicResource Text…}`; feste Zahlen sind nur in `Theme/*.xaml` (nie unter 11) und im
`StartupSplashWindow` erlaubt. Im Code duerfen nur gezeichnete Beschriftungen auf Video,
Grafik und PDF-Nachbildung kleiner als 11 sein (Positivliste im Test). Umgestellt wurden
885 Stellen: 8-11 -> XS, 12 -> S, 13 -> M, 14-16 -> L, 17-21 -> XL, 22/24 -> Title,
30-40 -> Display bzw. IconHero.

**Fenster und Rundungen (Waechter `DesignAuditFensterUndRundungenTests`):** Einzelne
`CornerRadius`-Zahlen ausserhalb des Themes gibt es nicht mehr — nur `RadiusS` 4, `RadiusM` 6,
`RadiusL` 8, `RadiusXL` 10, `RadiusXXL` 14, `RadiusPill` 999 (vierteilige Werte wie `8,8,0,0`
und `0` bleiben erlaubt). Jeder Fenstertitel heisst `SewerStudio — <Aufgabe>` oder ist
gebunden; nur MainWindow und Splash sind frei. Jedes veraenderbare Fenster (ohne
`SizeToContent`/`NoResize`) traegt `MinWidth` und `MinHeight`. `Margin`/`Padding` sind
bewusst NICHT auf ein Raster gezwungen worden (216 Werte, jedes Layout gleichzeitig) —
Abstaende bleiben Handarbeit je Fenster.

**Feedback, Bedienbarkeit, Suche (M2-M4, 2026-09-04):** `IToastService.Success(message,
aktionText, aktion)` zeigt einen Link im Toast (Standardmethode; Fakes brauchen sie nicht).
Excel- und XTF-Export toasten mit „Ordner oeffnen", der Importbericht mit „Bericht oeffnen"
(`ImportReportNavigationController`). Fotokarten tragen `ui:HoverFx.Lift`, das Dossier-Cockpit
`ui:EntranceFx.Stagger`. Jeder Icon-Knopf hat `AutomationProperties.Name` UND `ToolTip`
(Waechter in `DesignAuditAccessibilityTests`); Player-Knoepfe nennen ihre Taste im Tooltip
(`DesignAuditPlayerShortcutTests`, Quelle `PlayerKeyboardShortcutPolicy`). Die Einstellungen
haben ein Suchfeld: `SettingsSearchMatcher` (reiner Abgleich, Umlaut-tolerant, UND) und
`SettingsSearchController` (blendet `GroupBox`-Gruppen aus, springt zum ersten Reiter mit
Treffer). Neue Gruppen brauchen nichts weiter — der Controller liest Ueberschrift, Texte,
Haekchen und Tooltips selbst. Der aktuelle Seiteninhalt umfasst 16 Gruppen; die fruehere Zahl
17 zaehlte die `GroupBox`-Style-Definition mit.

Nachgelagerte Grossumbauten vom 2026-08-14:

- **Import-Staging vervollstaendigt.** `ImportFileTransaction` ist der gemeinsame
  Markerablauf fuer manuell und Ein-Knopf. `StageCopyAs`, `ResolveReadPath`,
  `EnumerateReadableFiles` und `StageGeneratedFile` bilden auch umbenannte Ziele,
  Zwischenlesepfade und neu erzeugte PDF-Seiten ab. Die manuelle projektinterne
  Schachtverteilung verwendet denselben Weg. Nur bewusst externe Schacht-Zielordner
  bleiben direkte Exporte, weil ein Projektmarker ausserhalb des Projekts nichts
  loeschen darf.
- **UI-Dateilogik ausgelagert.** `TrainingCenterStore` bleibt als 61-zeilige
  Kompatibilitaetsfassade; `ITrainingCenterDocumentStore` und
  `TrainingCenterDocumentFileStore` bewahren JSON, numerischen Status, `.bak` und
  Quarantaeneformat. Die Wissens-ZIP-Engine, ihr Dateikatalog und ihre Nachbearbeitung
  liegen unter `Infrastructure/Ai/Backup`; `KnowledgeBackupService.BackupResult` und
  die bisherigen Aufrufer bleiben unveraendert.

### Nova-Etappe 1 (2026-09-06, Leiste und Haltungsseite)

Quelle ist der freigegebene Prototyp `docs/reviews/2026-09-06-nova/optimiert/v2/`, Plan
`docs/superpowers/plans/2026-09-06-nova-wpf-etappe-1.md`, Abnahme
`docs/reviews/2026-09-06-nova/wpf-etappe-1/ABNAHME.md`. Umgesetzt und durch Waechter gehalten
(`ZustandsklasseInkPolicyTests`, `ShellNavigationGroupsTests`, `DataPageColumnViewCatalogTests`,
`DataPageWorkspaceLayoutPolicyTests`, `HaltungFelderDrawerFilterTests`, `DesignAuditNovaHaltungenTests`,
erweiterte `DesignAuditSchriftskalaTests`, `DesignAuditContrastTests`, `DesignAuditCommandReachabilityTests`):

- Zustandsklassen-Marken (Chips in Haltungs- und Schachtansicht, Uebersicht) tragen eine Textfarbe je
  Klasse mit mindestens 4,5:1 (`ZustandsklasseInkPolicy`, `ZustandsklasseInkConverter`). Die
  Tabellenzellen behalten ihre schwarze Tinte aus `ZustandsklasseCellStyleFactory`.
- KI-Farbtoken `KiBrush`, `KiSubtleBrush`, `KiTextBrush` in beiden Themes, getrennt vom Akzent;
  `KiTextBrush` erreicht auf `CardBrush` und `KiSubtleBrush` den Normaltext-Kontrast.
- Die Leiste ist in Projekt, Daten, Bewertung, System gruppiert (`ShellNavigationGroups`, `NavItem.Group`,
  `CollectionViewSource` ohne Sortierung); der Systemmonitor ist ein zugeklappter Aufklapper mit
  `NeuralPulseDot` und dem Kopf „Systemleistung", bei gesperrten Sensoren „Sensoren gesperrt"
  (Nachpruefung W02: `IsSensorBlocked` beschreibt Hardwaresensoren, nicht die KI; eine echte
  Bereitschaftsanzeige braucht die KI-Pruefungen und ist noch nicht gebaut). Die Schriftskala
  gilt seit dieser Etappe auch fuer `Setter Property="FontSize"` (22 XAML-Dateien umgestellt).
- Haltungen: eine Hauptaktion (Speichern, `ToolbarButtonAccent`), sichtbar Neu, Loeschen, `Video pruefen`
  und der Knopf `Weitere Aktionen`, dessen Kontextmenue alle bisherigen Aktionen samt Ansicht-Menue
  und Abdocken traegt; Gruppenkoepfe sind dort keine deaktivierten Menuepunkte (XamlActionWiringGuard).
  Spaltenansichten Kompakt, Stammdaten, Bewertung, Sanierung, Kosten, Alle
  (`DataPageColumnViewCatalog`, `DataPageColumnViewController` blendet nur Sichtbarkeit; gespeichert in
  `DataPageLayout.ActiveColumnView`). `PDF_Path` ist keine Haltungsspalte und steht deshalb nicht in Kompakt.
- Standardlayout der Haltungen: Liste | Uebersicht rechts (`HaltungUebersichtPanel`, nur lesend,
  Doppelklick auf einen Schaden = Beobachtungen) | Eingabefelder unten (`HaltungFelderDrawer`, die
  Themen des `DataPageRecordDetailsBuilder` nebeneinander als Expander ueber den unveraenderten
  `RecordDetailsView`, Feldsuche nur ueber die Beschriftung). Beide Trennlinien merken sich ihre Lage
  ueber `SplitterPersistenceBehavior` mit `ViewPersonalization.ViewKey="DataPage"` und den Schluesseln
  `HaltungenUebersicht` / `HaltungenEingabefelder`; `DataPageWorkspaceLayoutPolicy.Berechne` haelt
  mindestens sieben Zeilen sichtbar und klappt die Eingabefelder bei Platzmangel automatisch zu
  (Sichtprobe 06.09.2026: bei 1366 x 768 bleiben nur rund 400 px fuer Liste und Eingabefelder;
  aufgeklappt sind es 5 Zeilen). Die Anbindung liegt in `DataPageNovaWorkspaceController`, die
  Partial-Klasse `DataPage` reicht nur ihre Elemente herein (Waechter: 2000 Zeilen). Der
  Detail-Renderer laeuft dort mit `RecordDetailsView.IsHeaderVisible=false`. Die alte Haltungsansicht bleibt ueber den Toggle
  erreichbar; `AppSettings.ShowHaltungenNovaLayout=false` macht sie wieder zum Standard. Beim Wechsel
  werden Spalten und Zeilen der Arbeitsflaeche auf 0 gesetzt, damit keine Luecke bleibt. Zugeklappte
  Eingabefelder lassen nur die Kopfzeile stehen und geben der Liste die Flaeche zurueck
  (`HaltungFelderDrawer.IsOpen`, Nachpruefung W03, Waechter `DataPageNovaLayoutIsolatedSmokeTests`).
- **Tabelle und Formular teilen sich den Datensatz live (Nachpruefung W01).** `DataPageDetailLiveSync`
  haengt an `HaltungRecord.PropertyChanged` und schreibt jede Feldaenderung ueber
  `RecordDetailItem.UebernehmeAusDatensatz` ohne Rueckschreiben ins Formular. Waehrend ein Editor den
  Tastaturfokus hat (`IsEditing`, gesetzt vom `RecordDetailsView`), wird eine externe Aenderung nur
  gemerkt und erst nach dem Rueckschreiben der Bindung angezeigt. Der Rueckschreibweg der
  `DataPageDetailItemFactory` vergleicht `Ausgangswert` und aktuellen Datensatzwert
  (`IstKonflikt`): Hat sich der Datensatz seit der Anzeige geaendert, bleibt die neuere Korrektur
  stehen, das Formular zeigt sie, und die verworfene Eingabe steht als Hinweis in der Kopfzeile
  der Eingabefelder (kein Service-Locator in der Seite, `UiArchitectureGuardTests`). Nie wieder
  eine Momentaufnahme still ueber einen neueren Wert schreiben. Waechter:
  `DataPageFormularTabelleAbgleichTests` (Ablauf Alt -> Neue Tabellenkorrektur -> Zusatz).
  Seit 02.10.2026 gilt dieselbe Regel ueber `FormularKonfliktschutz` auch fuer Schaechte.
- Nicht umgesetzt (Etappe 2): Uebersichtsseite, Schaechte, Player, Training Studio, Chip „Naechste
  Aufgabe" (braucht einen fachlichen Pruefstatus je Haltung), Palettenwechsel Glas/Cockpit,
  animierte Symbole ueber den bestehenden `MotionSettings`-Rahmen hinaus.

### Nova-Etappe 2 (2026-09-07, ganze Oberflaeche)

Quelle bleibt der Prototyp `docs/reviews/2026-09-06-nova/optimiert/v2/`, Inventar
`docs/reviews/2026-09-06-nova/wpf-etappe-2/PROTOTYP-INVENTAR.md`, Abnahme
`docs/reviews/2026-09-06-nova/wpf-etappe-2/ABNAHME.md` samt zwoelf Bildschirmfotos unter
`bilder/`. Der isolierte Pruefhost liegt in `wpf-etappe-2/werkzeug/` und bleibt bewusst
ausserhalb von `AuswertungPro.sln`.

- Beide Themes tragen die Paletten Hell·Glas und Dunkel·Cockpit als Tokens; neu sind
  `AccentTextBrush`, `FaintBrush` und `GlassBorderBrush`. Der dunkle Akzent bleibt `#2563EB`,
  die Zustandsfarben Z0-Z4 bleiben unveraendert.
- Werkzeugknoepfe, Chips und Umschalter sind Pillen (`ToolbarButton`, `ToolbarButtonAccent`,
  `CompactToggleButton`), der Tabellenkopf steht in Kapitaelchen. **Eine Kapsel traegt den Radius
  ihrer halben ECHTEN Hoehe, nie 999.** WPF teilt den Eckenradius getrennt auf Breite UND Hoehe
  auf; ein zu grosser Wert ergibt auf einer flachen, breiten Flaeche eine Ellipse mit spitzen
  Enden statt einer Kapsel (Befund B1). Tokens in `Theme/Controls.xaml`: `RadiusPill` 15
  (Bedienelemente mit `MinHeight` 30), `RadiusChip` 11 (Abzeichen mit rund 22 px Hoehe),
  `RadiusBar` 5 (Fortschrittsbalken mit 10 px Hoehe), `RadiusCircle` 36 (die 72-px-Kreisflaeche
  des Leerzustands). Waechter: `DesignAuditFensterUndRundungenTests`.
- Der implizite `TextBlock`-Stil beider Themes setzt `Foreground` selbst und schlaegt damit die
  Vererbung. Die vier Knopfvorlagen (Basis-`Button`, `ToolbarButton`, `ToolbarButtonAccent`,
  `CompactToggleButton`) reichen ihre Tinte deshalb ueber eine eigene, engere Fassung in
  `ContentPresenter.Resources` an den Inhalt durch (B7). Dieselben Vorlagen richten ihren Inhalt
  jetzt nach `HorizontalContentAlignment`/`VerticalContentAlignment` aus statt fest mittig —
  ohne das bleibt ein gestreckter Knopfinhalt (Balken, rechtsbuendige Zaehler) unsichtbar.
- Eingabefelder und Knopf-Umrisse tragen den neuen Token `InputBorderBrush` (hell `#828FA4`,
  dunkel `#64769C`): mindestens 3:1 gegen `CardBrush`. Der Kartenrand (`BorderBrush`) bleibt
  bewusst hell (Glas-Look). Waechter: `DesignAuditContrastTests`.
- `HaltungPruefstatus` (offen / KI analysiert / abgeschlossen) und `NaechsteAufgabeRegel`
  liegen WPF-frei in `Application/UseCases/NaechsteAufgabe`. Der Chip der Kopfzeile zeigt die
  erste KI-analysierte Haltung, sonst die erste offene mit Video.
- `ShellNovaKopfzeile` liefert Brotkrume (`Projekt / Seite`) und Speicherstand;
  `ShellNavigationTitles.Anzeige` macht aus den ASCII-Schluesseln die Anzeigenamen mit Umlauten.
- Die globale Suche laeuft ueber `GlobaleSucheRegel` (WPF-frei) und `GlobaleSucheViewModel`;
  Strg+K fokussiert das Feld, Pfeiltasten und Enter waehlen ueber `MarkiertIndex`, nie ueber
  `SelectedItem` (sonst erreichen die Pfeiltasten die Liste nicht).
- `KiBereitschaftRegel` bestimmt den Kopf des Leisten-Aufklappers aus
  `AiRuntimeStatusTracker.Current`. Das ist die KI-Bereitschaft, nicht der Sensorzustand.
- `ProjektUebersichtPage` erscheint nur im Arbeitsbereich mit fertigem Projekt; ohne Projekt
  bleibt die bisherige `OverviewPage` der Startbildschirm. Die Kennzahlen kommen WPF-frei aus
  `ProjektUebersichtKennzahlen`; Schachtfelder liest sie ueber `SchachtFeldnamen`. Der Donut des
  Prototyps ist bewusst durch eine anklickbare Legende ersetzt.
- `AppSettings.ShowUebersichtNovaLayout` (Standard `true`) und der checkbare Menuepunkt
  `Ansicht -> Klassische Uebersicht` (`ShellViewModel.KlassischeUebersicht`) geben bei offenem
  Projekt die klassische `OverviewPage` zurueck — sie traegt Projektliste, Vorschau und die
  Vorschau-PDF. Die neue Seite hat dafuer einen eigenen Knopf `Vorschau-PDF`; beide Wege laufen
  ueber denselben `ProjektVorschauPdfUseCase` (Dateiname, Reihenfolge, Meldungen liegen dort).
  Die Zeilen unter „Haeufigste Schaeden" fuehren wie die Balken der klassischen Uebersicht in
  die gefilterte Haltungsliste.
- `ICodingSuggestionRegistry` merkt je Haltung den letzten Vorabdurchlauf fuer die Karte
  „KI-Vorabdurchlauf diese Sitzung"; es ist die 159. Registrierung im `ServiceProvider`
  (`ServiceProviderRegistrationTests`). Gemerkt wird ein Durchlauf erst NACH der
  Staleness-Pruefung und nur, wenn mindestens ein Teil wirklich gelaufen ist
  (`CodingSuggestionMerkRegel`); ein abgeschalteter Vorabdurchlauf taeuscht sonst Arbeit vor.
  Ohne Fund zeigt die Karte den Grund (`Hinweis`) statt eines leeren Abzeichens.
- Der Umschalter „Alte Haltungsansicht" sitzt im Menue `Weitere Aktionen`, nicht mehr in der
  Werkzeugleiste. Chips und Themen tragen ihre Anzahl als hochgestellten Zaehler.
- `RohrringGeometrie` folgt der erfassten Uhrlage (`Uhr_von`/`Uhr_bis` samt Aliassen); die
  frueher benutzte Indexregel ist nur noch der Rueckfall ohne Uhrlage. 0 Grad ist 12 Uhr.
  Gezeichnet und gelistet werden nur Schaeden: `SchadensgruppenRegel` (Application/Common) ist
  die EINE Quelle dafuer, was ein Schaden ist (BA-/BB-Hauptcodes aus dem VSA-Katalog) — dieselbe
  Regel wie im Cockpit (`DashboardStatisticsBuilder`). Bestandsaufnahme (BCD Rohranfang, BCE
  Rohrende, BCA Anschluss, BCC Bogen) gehoert nicht dazu. Die Reihenfolge ist Stufe absteigend,
  bei Gleichstand der kleinere Meterwert. Nie eine zweite Kopie dieser Regel anlegen.
- `HaltungUebersichtPanel` hoert zusaetzlich auf `HaltungRecord.PropertyChanged` (Pruefung und
  Video haengen an Feldern, nicht am Wechsel des Datensatzes) und marshallt wie
  `SchachtUebersichtPanel` auf den UI-Thread. Der Threadvertrag steht am Setter von
  `SchachtRecord.Protocol`: Die Meldung laeuft auf dem setzenden Thread, UI-Abonnenten
  marshallen selbst. Das Panel liegt in einem Bildlauf; nur die Schadenliste bekommt die
  Reststrecke, und ein leeres Eckdatenfeld zeigt einen Gedankenstrich (`HaltungFaktenText`)
  statt einer nackten Einheit.
- Die Schachtseite hat dasselbe dreiteilige Layout wie die Haltungen
  (`SchaechteNovaWorkspaceController`, `SchachtUebersichtPanel`, Spaltensaetze in
  `SchaechteColumnViewCatalog`). `AppSettings.ShowSchaechteNovaLayout=false` gibt die alte
  Ansicht zurueck; dann verschwinden auch die Spaltenchips, weil es keine Spalten mehr gibt.
  Am Schacht wird die Zustandsklasse weiterhin nie berechnet.
- **Schachtfelder werden ueber `SchachtFeldnamen` gelesen UND verglichen.** Der Datensatz fuehrt
  sie unter der Kopfzeile der Excel-Vorlage („Eigentümer" mit Umlaut), Katalog und Import
  schreiben „Eigentuemer". `DataPageColumnViewController` bekommt deshalb den Faltvergleich
  gereicht (`DataPageColumnView.Enthaelt/Anzahl`), sonst blendet die Ansicht „Sanierung und
  Kosten" die Spalte still aus. Der Chip zaehlt nur wirklich vorhandene Spalten.
- Die Zustandsklasse der Schachtliste ist eine eigene Vorlagenspalte
  (`SchaechteZustandsklasseColumnFactory`): Text zum Anzeigen, Auswahl zum Bearbeiten. Eine
  `DataGridComboBoxColumn` taugt dafuer nicht — ihr Anzeigeelement ist ein internes
  ComboBox-Abkoemmling, ein `ElementStyle` mit `TargetType="TextBlock"` wirft dort sogar, und die
  Ziffer bekommt am Ende die Farbe des impliziten `TextBlock`-Stils (im Dunkeln weiss auf Gelb,
  Befund B6). Die Anzeige bindet ihre Tinte direkt an die `DataGridCell`.
- Der Player hat einen kompakten Kopf und die Bedienleiste des Prototyps; alles Seltene liegt
  im Menue `Weitere ▾`. Die Zeitleiste zeigt die Schadensmarken der Haltung.
- Das Training Studio steht in drei Spalten (Werkzeuge 240 mit `MinWidth` 210 und `MaxWidth` 240,
  dann `*`, dann 330) mit den drei nummerierten Schritten
  KI-Vorschlag, Fachliche Codierung und Freigabe fuer Training. Einen Knopf „Fuer Training
  freigeben" gibt es bewusst nicht; die Freigabe bleibt das Export-Register im Training Center.
- `NetzHintergrund` zeichnet das Leitungsnetz aus einem Formen-Pool (keine Neuanlage je Bild);
  `AppSettings.HintergrundEngine` schaltet ihn, die Einstellungen melden die Aenderung ueber
  `MotionSettings.EngineChanged`. Sichtbar ist er nur in Kopfzeile, Raendern und hinter der Leiste.
- `NovaPageHeader` ist der einheitliche Seitenkopf mit Untertitel auf elf Seiten.
- Auswahlfelder: WPF leitet `ComboBox.SelectionBoxItemTemplate` NICHT aus `DisplayMemberPath` ab
  — die Eigenschaft bleibt null. Die eigene ComboBox-Vorlage bindet sie korrekt, hat damit aber
  nichts in der Hand und faellt auf `ToString()` zurueck (Befund B5, mit
  `ComboBoxAnzeigeIsolatedSmokeTests` als Produktfehler nachgewiesen).
  `ComboBoxAnzeigeTemplateSelector` schliesst die Luecke; ein ausdruecklicher
  `ItemTemplateSelector` hat weiterhin Vorrang.
- `TabControl` uebertraegt die Inhaltsausrichtung des GEWAEHLTEN Reiters auf den Inhaltsbereich.
  Mit `VerticalContentAlignment="Center"` stand der Seiteninhalt der Einstellungen mittig und
  liess ueber der ersten Gruppe rund 200 px leer (Befund B8); der Reiterstil streckt jetzt.
- Ein Aufklapper mit `StaysOpen="False"` schliesst sich beim Klick auf seinen eigenen Knopf
  selbst; ohne Zeitregel oeffnete ihn derselbe Klick sofort wieder. `PopupToggle`/`PopupToggleGate`
  ist der gemeinsame Weg (Player und Training Studio, Knopf „Weitere").
- Waechter: `DesignAuditNovaPaletteTests` (7), `DesignAuditNovaHaltungenTests` (7),
  `DesignAuditNovaSchaechteTests` (5), `DesignAuditNovaPlayerTests` (3),
  `DesignAuditNovaTrainingStudioTests` (4), `DesignAuditNovaUebersichtTests` (2),
  `DesignAuditNovaSeitenkoepfeTests` (4), `NetzHintergrundTests` (3),
  `NovaPageHeaderIsolatedSmokeTests` (1), `SchaechteNovaLayoutIsolatedSmokeTests` (2),
  `ShellNovaKopfzeileTests` (3), `KiBereitschaftRegelTests` (1),
  `SchaechteColumnViewCatalogTests` (6), `ComboBoxAnzeigeIsolatedSmokeTests` (1),
  `SettingsPageLayoutIsolatedSmokeTests` (1), `PopupToggleGateTests` (4),
  `ZustandsklasseInkPolicyTests` (6) sowie WPF-frei
  `GlobaleSucheRegelTests` (2), `NaechsteAufgabeRegelTests` (6),
  `ProjektUebersichtKennzahlenTests` (3), `RohrringGeometrieTests` (7),
  `SchadensgruppenRegelTests` (4), `HaltungFaktenTextTests` (5),
  `ProjektVorschauPdfUseCaseTests` (6), `CodingSuggestionRegistryTests` (3),
  `CodingSuggestionMerkRegelTests` (7).
- Ein Glyph gehoert in ein `ui:FluentIcon`, nicht in `Button.Content`. Der implizite
  `TextBlock`-Style beider Themes setzt `FontFamily` und `Foreground` selbst und schlaegt die
  Vererbung vom Knopf; ein Glyph als reiner Content erscheint deshalb als leeres Kaestchen und
  eine Akzentbeschriftung als dunkler Text. Behoben in `DataPage.xaml`, `SchaechtePage.xaml`
  und `RecordDetailsView.xaml`; die Tinte der Knopfvorlagen und der farbigen Zellen ist seit der
  Fixwelle vom 07.09.2026 zentral geloest (B6/B7 oben).
- Die Befunde B1 bis B8 und die Schlussreview F1 bis F7 sind in der Fixwelle vom 07.09.2026
  abgearbeitet; Stand und Grenzen stehen in der Abnahme. Nicht gemessen wurde Windows-Skalierung
  125 und 150 Prozent; das bleibt eine Sichtpruefung durch Pascal beim Merge. Der Pruefhost malt
  fuer das Bildschirmfoto die Mica-Flaeche des Fensters aus — `Fluent.Backdrop="Mica"` setzt
  `Window.Background` auf Transparent, und ein `RenderTargetBitmap` erfasst die vom
  Windows-Compositor gemalte Flaeche nicht.

### Nova-Etappe 2b (2026-09-07, Tabellen, Suche und Eingabefelder)

Anlass ist Pascals Bild vom 07.09. aus einem echten Projekt: alte Tabelle, Ansicht
„Alle Spalten 52", eine mehrzeilige Schadenzelle, nur sechs sichtbare Zeilen und
`{DependencyProperty.UnsetValue}` bei DN und Profil. Plan
`docs/superpowers/plans/2026-09-07-nova-wpf-etappe-2b.md`, Abnahme mit sechs Bildern
`docs/reviews/2026-09-06-nova/wpf-etappe-2b/ABNAHME.md`.

- Der Tabellenkopf steht in echten Grossbuchstaben (`GrossbuchstabenConverter`, Ressource
  `Grossbuchstaben` in `App.xaml`) statt in Kapitaelchen: `Typography.Capitals` greift mit der
  Programmschrift nicht. Das Kopf-Template gilt programmweit fuer alle `DataGrid`.
- **Vier virtuelle Statusspalten** KI, Pruefung, Video, Protokoll (`NovaStatusSpalten`,
  Schluessel mit Praefix `Nova_`). Sie sind KEINE Felder: nicht im `FieldCatalog`, nie in einem
  Export, nie als Feld im gespeicherten Spaltenlayout. Layout-Speicherung und -Wiederherstellung
  ueberspringen sie ueber `NovaStatusSpalten.IstVirtuell`. Gebaut werden sie von
  `HaltungStatusColumnFactory` (Ampel, Badge, zwei Knoepfe) aus der WPF-freien Regel
  `HaltungZeilenStatus.Bestimme` (Application/UseCases/NaechsteAufgabe); Schaechte haben
  dafuer `SchachtProtokollQuelle` und `SchaechteProtokollColumnFactory`.
- Die Zustandsklasse ist im Nova-Layout eine Marke (`ZustandsklasseChipColumnFactory`,
  gemeinsam fuer Haltungen und Schaechte); `SchaechteZustandsklasseColumnFactory` ist darin
  aufgegangen. Der Chip-Editor schreibt **nur bei einer echten Auswahl**: Beim blossen Oeffnen
  der Auswahlliste darf kein Wert und keine Handmarkierung entstehen, sonst wuerde ein Blick in
  die Liste den Wert der Zeile stempeln. Ohne gueltige Klasse steht ein gestrichelter Strich
  mit dem Hinweis „nicht berechnet" — nie eine erfundene Z4.
- `DataPageHaltungColumnBuilder` baut die Spalten der Haltungstabelle ausserhalb von
  `DataPage` (Zeilengrenze 2000 je Teildatei). `DataPageZeilenhoehePolicy` sagt, welche Ansicht
  einzeilig ist (kompakt, stammdaten, sanierung, kosten); dort gilt die Zeilenhoehe aus dem
  Token `RowHeightCompact` (34, siehe Fixwelle). „Alle Spalten" und „Bewertung" bleiben auf
  `Auto`, und eine Zelle mit Zeilenumbruechen wird dort auf **hoechstens drei Zeilen** begrenzt
  (`DataPageColumnStyleRules.MaximaleZellenhoehe` 54) mit Volltext im Hinweis. Diese Grenze gilt
  nur im Nova-Layout; die alte Haltungsansicht bleibt unveraendert und ihr „Kompakt" behaelt
  den rohen Videopfad `Link`, weil es dort keine Statusspalten gibt.
- „Kompakt" wird **einmalig** zum Standard (`KompaktStartRegel`): Ist
  `DataPageLayoutSettings.NovaKompaktEinmalGesetzt` noch `false`, wird die Ansicht auf
  „kompakt" gesetzt und das Flag gesetzt. Danach zaehlt ausschliesslich die Wahl des Benutzers,
  auch wenn er wieder „Alle Spalten" waehlt. Gilt gleich fuer Haltungen und Schaechte.
- Die Suche ist eine Pille rechts in der Werkzeugleiste (Haltungen mit Tastenmarke F3,
  Schaechte ohne). „Verschieben auf Position" und „Gehe zu Zeile" haben keine eigene Zeile mehr;
  sie sind Menuepunkte unter `Weitere Aktionen -> Reihenfolge` und oeffnen ein `Popup` mit
  Eingabefeld — fuer beide Layouts gleich. `PopupFocusHelper` setzt den Tastaturfokus in das
  Feld und schliesst mit Escape. **Die Filterzeile bleibt** (Entscheid Pascal 07.09.), direkt
  unter den Spaltenchips.
- Die Eingabefelder stehen in den vier Prototyp-Themen; die Prototyp-Feldliste ist eine
  **Mindestliste, keine Ausschlussliste**. Daraus werden Stammdaten 17, Bewertung 9,
  Sanierung 11, Kosten und Bemerkungen 3: `Schacht_oben`/`Schacht_unten` bleiben in den
  Stammdaten (der Haltungsname haengt an den Schaechten), das Gefaelle ebenso,
  `Renovierung_Inliner_Stk` in der Sanierung. Alles Uebrige steht in „Weitere Angaben"
  (zugeklappt) — kein Feld verschwindet. Die Themennamen gelten auch im alten Detailfenster,
  weil beide denselben `DataPageRecordDetailsBuilder` verwenden.
- `HaltungFaktenText.Zusammen` ist eine reine Textregel: Sie sieht nur Zeichenketten und
  wirft `null`, Leeres und alles weg, was mit `{` beginnt. Den WPF-Sonderwert
  `DependencyProperty.UnsetValue` sieht sie nie — die Konverter bilden ihn vorher auf `null`
  ab. Ohne gewaehlte Zeile zeigt `HaltungUebersichtPanel` nur den Leerzustand — kein
  Rohrring, keine Fakten, kein Fehltext.
- Waechter: `DesignAuditNovaTabelleTests`, `DesignAuditNovaHaltungenTests`,
  `DesignAuditNovaSchaechteTests`, `GrossbuchstabenConverterTests`,
  `DataPageColumnStyleRulesTests`, `DataPageColumnViewCatalogTests`,
  `SchaechteColumnViewCatalogTests`, `NovaStatusSpaltenTests`,
  `DataPageColumnViewControllerTests`, `DataPageZeilenhoehePolicyTests`,
  `HaltungStatusColumnFactoryTests`, `ZustandsklasseChipColumnFactoryTests`,
  `SchaechteZustandsklasseColumnFactoryTests`, `SchaechteProtokollColumnFactoryTests`,
  `PopupFocusHelperTests`, `DataPageRecordDetailsBuilderTests`,
  `HaltungFelderDrawerFilterTests`, `HaltungFaktenTextConverterTests`,
  `DataGridColumnHeaderGrossbuchstabenIsolatedSmokeTests` sowie WPF-frei
  `HaltungZeilenStatusTests`, `SchachtProtokollQuelleTests`, `HaltungProtokollQuelleTests`,
  `HaltungFaktenTextTests`, `HaltungRecordProtokollMeldungTests`, `NovaSpaltenbreitenTests`,
  `DataPageZeilenhoehePolicyTests`, `DesignAuditLaufzeittexteTests`,
  `DesignAuditContrastTests`.

### Nova-Fixwelle 2b (07.09.2026, F1-F5 und P1-P6 behoben)

Bericht `.superpowers/sdd/2026-09-07-nova-wpf-etappe-2b/final-fix-report.md`, Abnahme
`docs/reviews/2026-09-06-nova/wpf-etappe-2b/ABNAHME.md` (Abschnitt 0a). Sechs Regeln, die
nicht zurueckfallen duerfen:

- **Die Statusspalten befragen kein Dateisystem.** `HaltungProtokollQuelle` (Muster
  `SchachtProtokollQuelle`) sagt nur, ob ein Pfad HINTERLEGT ist: `PDF_Path`, `PDF_Eigen`,
  `PDF_All` und ein `Link` mit `.pdf`-Endung. Bei tausenden Zeilen waere eine echte
  Dateipruefung je Zelle ein Ordnerlauf je Bild. Der Gedankenstrich nennt deshalb
  ausdruecklich den zweiten Weg („das Kontextmenue sucht im Projekt", „Video pruefen sucht
  im Ordner"); ein Knopf erscheint nur bei hinterlegtem Pfad, und ob die Datei noch da ist,
  meldet erst der Oeffner.
- **Die KI-Ampel spricht ueber die KI, nicht ueber den Arbeitsablauf.** Massgeblich ist, ob
  im Protokoll ueberhaupt KI-Eintraege stehen: keine heisst `KeineAnalyse` — auch an einer
  fachlich abgeschlossenen Haltung. Alles bestaetigt und noch nicht abgeschlossen heisst
  `Bestaetigt` („bestätigt", gruener Punkt); abgeschlossen bleibt `Geprueft` („geprüft")
  beziehungsweise `Kritisch` bei Zustandsklasse 0 oder 1. `HaltungPruefstatus` ist davon
  unberuehrt und bleibt die Quelle fuer Aufgaben-Chip und Uebersicht.
- **Zahlen stehen rechts.** `DataPageColumnSetup.Apply` liefert die rechte Ausrichtung fuer
  jede `DataPageColumnStyleRules.IstZahlenspalte`; die Schachtliste vergleicht dabei
  gefaltet (`SchachtFeldnamen.Falte`). Ohne das schrieb der `DataGridColumnLayoutController`
  seine linke Vorgabe in Zell- und Textstil und schlug damit das `TextAlignment.Right` der
  Spaltenfabrik. Eine gespeicherte Nutzerausrichtung gewinnt weiterhin, weil sie erst mit
  `RestoreLayoutFromSettings` gelesen wird — und genau deshalb braucht es die einmalige
  `ZahlenRechtsMigration` (Flag `DataPageLayoutSettings.ZahlenRechtsEinmalGesetzt`, VOR dem
  Wiederherstellen, Haltungen und Schaechte): In einer bestehenden Installation steht im
  gespeicherten Layout ueberall `Left` und die Kopfbreite, sonst wirkte die neue Regel dort
  nie. Sie hebt die Ausrichtung jeder Zahlenspalte einmal auf `Right` und Breiten NUR an —
  eine in Pixeln gespeicherte Handbreite wird nie verkleinert; eine Breite ohne
  Pixel-Einheit (`SizeToHeader`) ist keine Wahl des Benutzers und wird durch die Startbreite
  ersetzt. Rechtsbuendige Zahlen tragen 6 px rechtes Polster (`NovaTextZellenStil.ZahlenPolster`),
  sonst kleben sie an der Nachbarspalte.
- **`RowHeightCompact` (34) allein reicht nicht.** Die Tabelle traegt zusaetzlich die frei
  einstellbare Mindesthoehe `AppSettings.GridMinRowHeight` (Werkseinstellung 38), gebunden
  an `MinRowHeight`. Sie ist groesser und hat das Token vollstaendig ausgehebelt — eine
  Gegenprobe mit Token 24 ergab weiterhin 38 px je Zeile. In einzeiligen Nova-Ansichten
  gilt jetzt die kleinere der beiden Zahlen (`DataPageZeilenhoehePolicy.Mindesthoehe`,
  angewendet von `DataPageZeilenhoehenAnwender` — bewusst AUSSERHALB der `DataPage`-
  Teildateien, die zusammen unter 2000 Zeilen bleiben muessen). Die Seite wendet das auch bei
  jeder Aenderung von `GridMinRowHeight` erneut an, damit der Regler sofort greift. Gemessen
  bei 1920 x 1080 mit offener Schublade: 12 ganze Zeilen.
- **Lange Werte werden gekuerzt, nicht abgeschnitten.** Standard-Textspalten tragen im
  Nova-Layout `TextTrimming=CharacterEllipsis` (Schaechte ueber `NovaTextZellenStil`), und
  jede Nova-Zelle zeigt den Volltext oben im Hinweis. `NovaSpaltenbreiten` gibt Name 150,
  Strasse 120 und Material 100 als START-, nicht als Mindestbreite; ein gespeichertes
  Spaltenlayout gewinnt.
- **Sichtbare C#-Laufzeittexte tragen Umlaute.** `DesignAuditLaufzeittexteTests` prueft die
  drei Quellen (`LearningReadinessPresenter`, beide RecordDetails-Builder) und sieht nur
  Zeichenketten MIT Leerzeichen — Feldschluessel wie `Gefaelle_Promille` sind
  Datenschluessel, keine Beschriftungen, und duerfen ihre Schreibweise nie aendern.

Kleineres, ebenfalls fest: Der Tabellenkopf erbt Groesse und Tinte per RelativeSource vom
`DataGridColumnHeader` (vorher gewann die String-Vorlage gegen jeden Setter, auch gegen
einen abgeleiteten `ColumnHeaderStyle`); die Grossschreibung bleibt bewusst nur auf den
Nova-Tabellen. Die Kopf-Ziehgriffe zeichnen nur rechts eine 1 px schmale Linie in
`BorderBrush` — der Kontrastwaechter prueft, dass sie sich nicht staerker vom Kopfgrund
abhebt als der Kopftext (im hellen Theme ist heller unauffaellig, im dunklen auffaellig;
„nicht heller als die Tinte" waere deshalb die falsche Regel). Die Suchpille steht rechts
IN der Werkzeugleiste. „Dokumente und Medien" der Schaechte fuehrt `PDF_Path` neben dem
Knopf weiter als bearbeitbare Spalte. „Kompakt" wird nur im Nova-Layout einmalig zum
Standard.

**Eine virtuelle Statusspalte darf nie in `Fields` landen.** Das Praefix `Nova_` liegt als
eine Wahrheit in der Domaene (`VirtuelleSpalte`); `NovaStatusSpalten.IstVirtuell` leitet nur
dorthin weiter. `HaltungRecord` und `SchachtRecord` weisen einen solchen Schluessel auf ALLEN
Schreibwegen mit `ArgumentException` ab — bewusst kein stilles Ignorieren, denn ein
verschluckter Schreibversuch sieht fuer den Aufrufer wie ein Erfolg aus. Der Rechtsklick
beider Seiten laeuft ausserdem durch denselben `DataPageRightClickController`. Anlass:
Die Schachtseite hatte einen zweiten, eigenen Rechtsklickpfad ohne diesen Schutz und schrieb
bei „Spalte leeren" auf der Protokollspalte `Nova_Protokoll` mit Handmarkierung in JEDEN
Schachtdatensatz und damit in die Projektdatei (Re-Review 07.09.2026). Zwei Wege zu
derselben Entscheidung heisst, dass nur einer den Schutz bekommt.

Offen bleibt: Windows-Skalierung 125 und 150 Prozent ist nicht gemessen.

### Nova: Aufklapp-Listen und Grafiken (2026-09-08)

Stand und ehrliche Abnahmegrenzen: `docs/reviews/2026-09-06-nova/aufklapp-liste/ABNAHME.md`.

- `AppSettings.HaltungenAnsicht` und `SchaechteAnsicht` haben den Standard `"liste"`.
  `HaltungenAnsichtRegel` wird von beiden Ansichtsumschaltern verwendet; die Tabelle
  bleibt unter Weitere Aktionen / Ansicht erreichbar. Spaltenchips und Abdocken gehoeren
  zur Tabelle. Genau ein Datensatz ist aufklappbar; blosse Auswahl klappt nicht auf.
- `HaltungAufklappListe` und `SchachtAufklappListe` teilen Themenbildung, Tastenregel,
  Zustandskonverter und `DataPageDetailLiveSync`. `DataPageAufklappListeController` und
  `SchaechteAufklappListeController` besitzen den jeweiligen Formular-Abgleich. Genau
  EIN Formular je Datensatz: Unsichtbare Schubladen werden mit `LeereFelderDrawer`
  geleert und ihr Abgleich entsorgt. Der Tabellen-Rueckweg baut ihn wieder auf.
  Nach erneutem `Verdrahte` wird auch ein bereits offenes Listenformular wieder verbunden.
- `HaltungAufklappTastenregel`: Escape aus einem Editor holt zuerst den Fokus auf die
  Zeile, damit die normale Eingabe zurueckgeschrieben wird. Erst der zweite Escape
  klappt zu. Ein ausdruecklicher Haltungssprung nutzt `DataPageViewModel.ZeigeHaltung`.
- `HaltungsgrafikAnsichtBuilder` liest das aktuelle Protokoll ausschliesslich.
  Niemals `ResolveEntriesForExport` in einer Ansicht verwenden: Dieser Exportweg
  repariert Eintraege. `HaltungsgrafikSvgBuilder` liefert mit `nurRohr` nur die Rohrsaeule.
- Die Uebersicht nutzt `BaueUebersicht` und `HaltungsgrafikKlartextZeichner`: volle
  Haltungslaenge auf der verfuegbaren Hoehe, Meterteilung und kollisionsfreie Klartexte
  mit Bezugslinien. Viele Texte vergroessern die scrollbare Grafik statt die Schrift
  zu verkleinern. Eckdaten und Schadenliste liegen im Expander darunter.
  `Application/Reports/NutzungsartReportColors` ist die gemeinsame Farbregel fuer
  Bildschirm und PDF. `SvgFarbZuordnung` erhaelt diese Nutzungsfarben in beiden Themen.
  `HaltungsgrafikMarke.FotoPaths` bleibt nach Gegenfahrt und Sortierung am Ereignis.
  Seit 10.09.2026 verbindet `HaltungsgrafikFotoAktion` Symbol/Klartext mit
  `PhotoHoverPreviewBehavior`: nach 350 ms erscheint die bestehende Fotokarte,
  das Mausrad blaettert. Klick und Enter/Leertaste zeigen ebenfalls nur die Karte;
  Escape, Verlassen oder Entladen schliessen sie. Der Projektroot-Provider wird vom
  Workspace-Controller an die Grafik vererbt und liest das aktuelle ViewModel.
  Die bisherigen FotoOeffnen-Eigenschaften bleiben kompatibel, werden in dieser
  Grafik aber nicht mehr ausgefuehrt. Keine neue Registrierung oder Datenformate.
- `SchachtgrafikAnsichtBuilder` liest Schachtfelder ueber `SchachtFeldnamen` und
  angeschlossene Haltungen aus der vom ViewModel gereichten Liste. Keine
  Zustandsberechnung am Schacht. `SchachtSchadenKategorieRegel` verwendet Text nur bei
  bekannten Bauteilnamen aus `SchachtBauteilNamen`, mit Wortgrenze und Negationswaechter.
- `SvgTeilmengeZeichner`, `SvgTeilmengeDefinitionen`, `SvgWert` und `SvgFarbZuordnung`
  sind der SVG-Vertrag: Neue Builder-Elemente oder Attribute verlangen eine Erweiterung
  des Zeichners und der Teilmengen-Tests. Unbekanntes wirft; das Control zeigt einen
  ehrlichen Leer-/Fehlerzustand. Beide Grafik-Controls verbinden ihre Datensatzmeldungen
  nach `Loaded` erneut, auch wenn dieselbe Instanz und derselbe Datensatz zurueckkehren.
- Nachpruefungen: `NovaGrafikWiederladenTests`, `NovaListenWiederverbindenTests`,
  `SchaechteNovaLayoutIsolatedSmokeTests` sowie die vorhandenen Listen-, Grafik- und
  Gestaltungswaechter. Der isolierte Pruefhost kennt `haltungenliste` und `schaechteliste`
  und prueft den Menuewechsel auf echten Seiten mit eigenem Profil.

### Projektwechsel-Fixwelle (08.09.2026, R1/R2/R4 aus dem Gesamtaudit)

Bericht `docs/reviews/2026-09-08-redesign-gesamtaudit/PRUEFBERICHT.md`. Drei Befunde mit
derselben Wurzel: Ein Teil der Oberflaeche merkte sich das Projekt EINMAL und erfuhr vom
Wechsel nie.

- **Ein KI-Vorabdurchlauf gehoert zu genau einem Projekt.** `ICodingSuggestionRegistry`
  schluesselt nach Projekt-Id UND Haltung (`Merke(Guid, string, Set)`, `Heute(Guid)`); ein
  reiner Namensschluessel vermischte gleichnamige Haltungen zweier Projekte. `Guid.Empty`
  wird wie eine leere Haltung ignoriert — ein Lauf ohne Projektbezug waere sonst in jedem
  Projekt sichtbar. Der Player bindet die Id VOR dem Durchlauf; ein spaet zurueckkommendes
  Ergebnis landet dadurch beim richtigen Projekt und nicht in der Uebersicht des neuen.
- **Die globale Suche verwirft ihre Treffer beim Projektwechsel.** Ein Treffer traegt einen
  direkten Objektverweis; nach dem Wechsel gehoert er zum alten Bestand.
  `GlobaleSucheViewModel` hoert dafuer auf `ShellViewModel.PropertyChanged` (`Project`,
  `IsProjectReady`) und prueft in `Waehle` zusaetzlich, ob Haltung oder Schacht wirklich im
  offenen Projekt liegen. Die Strasse ist nur ein Filtertext und bleibt erlaubt. Das ViewModel
  ist dadurch `IDisposable`; `ShellViewModel.Dispose` meldet es ab.
- **Die Projektuebersicht haengt ihr Listen-Abo um.** `ProjektUebersichtPageViewModel` band
  `_shell.Project.Data` einmalig im Konstruktor. Beim Wechsel wird das Abo jetzt abgemeldet,
  auf die neue Liste gesetzt und neu gerechnet. Nur `Aktualisiere()` zu rufen reicht NICHT:
  Die Seite bliebe fuer spaetere Aenderungen des neuen Projekts taub.
- Waechter: `CodingSuggestionRegistryTests` (6) und `NovaProjektwechselTests` (6, Wechsel A→B
  und B→C mit echten `ReplaceProject`-Aufrufen).

## Nova-Abschluss: Video-Kopienregel (2026-09-06)

Die Behandlung mehrdeutiger Video-Treffer liegt vollstaendig in `HoldingDistribution/VideoKopienAufloeser.LoeseTreffer`. `HoldingFolderDistributor.FindVideo` delegiert darauf; die grosse Verteilerklasse bleibt unter ihrer bisherigen Groessengrenze. Bestehende Medienkopien- und Importtests pruefen unveraendertes Verhalten.


## Nova-Nachpruefung abgeschlossen (2026-09-06)

Ergaenzung 08.09.2026: Die aufgeklappten Haltungs- und Schachtlisten bieten
`Ansicht anpassen` ueber `AufklappLayoutWindow`. Die Vorschau benutzt losgeloeste,
schreibgeschuetzte Karten aus `AufklappDetailLayout`, ohne Datensatz-Callbacks.
Erst Speichern uebernimmt Reihenfolge und Ausblendung in die vorhandenen,
getrennten `DataPageLayout.DetailLayout` / `SchaechtePageLayout.DetailLayout`.
Die Listencontroller wenden diese beim Aufbau an; der Live-Abgleich bleibt an
den originalen Feldobjekten. Dokumentgruppen werden in diesen Listen als normale
Feldgruppen dargestellt, damit verschobene Felder sichtbar bleiben.
Bedienung und Nachweise: `docs/reviews/2026-09-06-nova/aufklapp-liste/ANSICHT-ANPASSEN.md`.

- Mindest-Bildschirmaufloesung laut Nutzer: Full HD (1920 x 1080). 1366 x 768 ist lediglich eine zusaetzliche Fensterprobe. Windows-Skalierung 125 und 150 Prozent wurde auf Full HD nach einem Neustart des isolierten Pruefhosts gemessen. 150 Prozent ergibt weniger Arbeitsflaeche; F11 schafft mehr Tabellenplatz.
- `DataPageHydraulikReportCalculator` verwendet fuer Einzel-PDF und Dossier das Projektgefaelle in Promille. DN und Gefaelle muessen positiv und endlich sein; keine stillen Ersatzwerte 300 mm / 5 Promille. Die Berichtskonvention bleibt Halbfuellung (DN / 2); Materialzustand und Temperatur kommen weiterhin aus den Panel-Einstellungen. Der Bericht ist keine Kopie der frei veraenderten Panel-Berechnung.
- `FieldCatalog.Definitions` benennt den vorhandenen Schluessel `SlopePromille` als Gefaelle in Promille. `DataPageRecordDetailsBuilder` bietet ihn immer als Stammdaten-Eingabe an, auch ohne bisherigen Projektwert. Die feste `ColumnOrder` fuer Tabellenexporte bleibt erhalten; das gespeicherte Dictionary-Format aendert sich nicht.
- `DataPageProjectBindingController` aktualisiert bei Aenderungen der Haltungsliste auch die Auswahlbefehle. Nach oben/unten reagiert damit ohne erneute Auswahl. Keine zusaetzliche Kartenrueckmeldung.
- Auswahlfarben und die Zellentext-Vererbung sind in beiden Themes vereinheitlicht. Kontextmenues besitzen einen ScrollViewer, damit auf Full HD bei 150 Prozent auch die letzten Eintraege erreichbar sind. Keine neuen Abhaengigkeiten oder Registrierungen.
- Nachweise, Pruefgrenzen und verstaendliche HTML-Uebersicht: `docs/reviews/2026-09-06-nova/wpf-etappe-1/abschluss/`. Die isolierte Bedienprobe startet weder produktiven App-Startup noch Spiegel oder QGIS-Bruecke und belegt keine vollstaendige Programmabnahme.


