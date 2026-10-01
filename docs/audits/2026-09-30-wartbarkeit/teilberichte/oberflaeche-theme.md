# UI-Schicht, Theme und XAML (Wartbarkeitsaudit 30.09.2026)

Stand HEAD 3211155cd. Pfade relativ zu `src/AuswertungPro.Next.UI`, wenn nicht anders angegeben.
Seit 0c2d5c17b: 453 Dateien, +11'950 / -5'399 Zeilen in der UI-Schicht.

## 1) Kurzfazit
Die neuen Querschnittsbausteine (Rueckgaengig-Verlauf, Nova-Dialog, UserError) sind klein, gut kommentiert und an einer Stelle verdrahtet. Das Hauptproblem ist das Theme: Hell und Dunkel pflegen die Stile doppelt (17 von 21 Stilen/Vorlagen mit Schluessel sind nach Abzug der Kommentare bytegleich), jede Stiländerung muss zweimal gemacht werden. Die Zeilengrenze wird bei einzelnen Dateien nur durch Auslagern in Huellen-Dateien eingehalten (SchaechtePage.xaml.cs steht seit 3 Commits exakt auf 1000). Bei den grossen partial-Klassen ist die Aufteilung ueberwiegend fachlich (DossierPreviewFieldPanel, ShellViewModel). Das Handbuch und das Tastenkuerzel-Fenster sind von Hand gepflegte Textkopien und brechen still, wenn sich Seiten oder Tasten aendern.

## 2) Befundtabelle
| ID | Prio | Titel | Nutzen |
|---|---|---|---|
| U01 | P1 | Stile in Theme.xaml und ThemeLight.xaml doppelt gepflegt | Eine Stiländerung an einer Stelle statt zwei |
| U02 | P2 | Tastenkuerzel-Fenster kopiert die globalen Tasten von Hand | Neue Taste kann nicht mehr vergessen werden |
| U03 | P2 | Handbuchtext haengt per Namen an NavItems und ist fest im Code | Seitenaenderung und Handbuch driften nicht |
| U04 | P2 | Verlauf: jede externe Uebernahme braucht einen manuellen Sperr-Aufruf | Neuer Schreibweg ohne Sperre wird erkannt |
| U05 | P2 | «...MitVerlauf»-Huellen doppelt in DataPage und SchaechtePage | Halbierter Pflegeaufwand, weniger Fehlverdrahtung |
| U06 | P3 | Dateigrenze 1000 wird per Auslagern «erreicht», SchaechtePage.xaml.cs bei 1000 | Ehrliche Restschuld sichtbar |
| U07 | P3 | Lokale Doppel-Stile ausserhalb des Themes (NovaDialogDangerButton u. a.) | Weniger Abweichung von DangerButton |
| U08 | P3 | Neuer Dienst: 3-4 Stellen plus Zaehlertest | Nur Bestandsaufnahme |

## 3) Befunde im Detail

### U01 (P1) Theme doppelt gepflegt
Belege:
- Dateigroessen: `Theme/Theme.xaml` 1252, `Theme/ThemeLight.xaml` 1207, `Theme/Controls.xaml` 1924, `Theme/ThemeHighContrast.xaml` 135 Zeilen.
- Beide Dateien enthalten je 43 `<Style` und 24 `<ControlTemplate`. Gleiche Schluessel an fast gleicher Stelle: `PrimaryButton` Theme.xaml:509 / ThemeLight.xaml:479, `SecondaryButton` :560 / :523, `FilledButtonTemplate` :428 / :398, `ToolbarButtonAccent` :635 / :598, `CompactButton` :794 / :754.
- Messung (kleine Python-Auswertung, Kommentare entfernt, Leerraum normalisiert) ueber alle 21 Bloecke mit `x:Key` in beiden Dateien: 17 identisch, nur 4 weichen ab (`IconButton`, `Card`, `NavItemStyle`, `ToolbarButton`). `PrimaryButton` unterscheidet sich nur in den Kommentaren (Theme.xaml:513-520 gegen ThemeLight.xaml:483-486).
- Die drei Nachtragskommentare in ThemeLight verweisen ausdruecklich auf Theme.xaml («siehe Kommentar in Theme.xaml»): Man hat die Aenderung zweimal gemacht.
- Seit 27.09.: je 7 Commits an Theme.xaml, ThemeLight.xaml und Controls.xaml; Fix-Runden 2-4 zu Aufgabe 13 (CLAUDE.md, «PrimaryButton ... DynamicResource», «FilledButtonTemplate») betrafen jeweils beide Dateien.
Warum es bremst: Die Farbtokens sind je Theme verschieden (das ist gewollt), die Formen nicht. Wer `FilledButtonTemplate` oder `CompactButton` aendert, muss beide Dateien treffen. Vergisst man eine, sieht man den Fehler nur im anderen Theme.
Empfehlung: Stile und Vorlagen, die nur `DynamicResource`-Tokens nutzen, einmal nach `Theme/Controls.xaml` (dort steht schon DangerButton/WarningButton, Zeile 1870/1884) verschieben. Theme.xaml/ThemeLight.xaml behalten Farbtokens und die vier wirklich abweichenden Stile. Zuerst nur die 17 bytegleichen Stile.
Abnahme: Fertig, wenn die 17 Stile nur noch einmal im Repo stehen, ein Test die Schluesselmengen von Theme.xaml und ThemeLight.xaml vergleicht (gleiche Farbtokens in beiden), und die bestehenden Design-Audit-Tests (Kontrast, HighContrast-Farbpaare, `FuellknopfFarbenIsolatedSmokeTests`) unveraendert gruen sind. Vermutung (nicht geprueft): Die MergedDictionaries-Reihenfolge in App.xaml muss beim Verschieben beachtet werden, weil ThemeHighContrast nach dem Theme kommen muss.

### U02 (P2) Tastenkuerzel von Hand kopiert
Belege: `Views/Windows/TastenkuerzelWindow.xaml.cs:59-76` schreibt F11, Strg+N/O/S/K, Strg+F1, F3, Strg+Z/Y/Umschalt+Z als feste Texte. Die echten Tasten stehen in `MainWindow.xaml:15-30` (`KeyBinding`). Nur die Player-Tasten kommen aus `PlayerKeyboardShortcutPolicy.Beschreibungen` und sind per Test abgedeckt (CLAUDE.md, Aufgabe 6).
Warum es bremst: Aendert oder ergaenzt jemand ein `KeyBinding`, bleibt die Uebersicht falsch, ohne dass ein Test rot wird.
Empfehlung: Ein Test, der die `KeyBinding` aus `MainWindow.xaml` per XDocument liest und verlangt, dass jede Taste in der Liste des Fensters vorkommt.
Abnahme: Fertig, wenn das Hinzufuegen eines KeyBinding ohne Listeneintrag den Test rot macht (Sabotageprobe).

### U03 (P2) Handbuch fest im Code, an Namen gekoppelt
Belege: `Services/HandbuchInhalt.cs` 205 Zeilen / 15'971 Byte, Text als Literale; `Schluessel` muss wortgleich `NavItem.Title` sein (HandbuchInhalt.cs:8-12). 10 Commits seit 27.09. beruehren die Datei, meist als Nebenwirkung anderer Aufgaben (Aufgabe 7, 8, 9, 16 mussten hier nachziehen, siehe `git log -- Services/HandbuchInhalt.cs`).
Warum es bremst: Jede sichtbare Seitenaenderung erfordert einen weiteren Blick in diese Datei. Der Abdeckungstest liest `ShellViewModel.cs` per Regex (CLAUDE.md, Aufgabe 6); er prueft Vorhandensein, nicht Richtigkeit.
Empfehlung: Kein Umbau. Nur (a) am Kopf jeder neuen Aufgabenvorlage vermerken «Handbuch mitpruefen», (b) optional Text in eine eingebettete Markdown-/Textressource je Seite auslagern, damit Texte ohne C#-Build aenderbar sind. Beides klein.
Abnahme: Fertig, wenn ein Seitentext ohne Aenderung an C#-Logik anpassbar ist und der Abdeckungstest weiter gilt.

### U04 (P2) Verlauf: Sperren sind verstreut
Belege: Der Baustein selbst ist klein und klar: `Application/UseCases/Datenaenderungen/DatenaenderungsVerlauf.cs` 479, `DatenaenderungsZustand.cs` 174, `IDatenaenderungsVerlauf.cs` 80 Zeilen; UI-Anbindung `DataPage.Verlauf.cs` 61, `SchaechtePage.Verlauf.cs` 64, `ShellViewModel.DatenVerlauf.cs` 153, `DatenVerlaufZellErfassung.cs` 49, zwei `*PageViewModel.Verlauf.cs` je ~48. Registrierung nur in `ServiceProvider.DatenVerlauf.cs` (1 Property) und `ServiceProviderRegistrationMap.cs:97`.
Verstreut sind die Sperren: 19 Fundstellen in 11 Dateien, z. B. `DataPageViewModel.cs:41` (`MeldeFelderExternErgaenzt`), `DataPageViewModel.KatasterKennungen.cs:32`, `.QgisNachfuellen.cs:35`, `.WebGisHolen.cs:22`, `SchaechtePageViewModel.Verlauf.cs:43-45` (`MeldeUebernahme`), `Services/WebGisHolenAblauf.cs:86`, `Services/ObjektaktenDialog.cs:34-71,119-127` (`MitSperre`), `ShellViewModel.DatenVerlauf.cs:136`.
Warum es bremst: Jeder neue Weg, der Felder von aussen fuellt (naechste Nachfuellfunktion), braucht von Hand einen `Leere`-Aufruf. Fehlt er, faengt nur die Vorbedingung (Wert muss noch dem eigenen Stand entsprechen) den Schaden ab; das Ergebnis waere ein verworfener Eintrag statt einer klaren Sperre. Das ist gut abgesichert, aber leicht zu vergessen.
Empfehlung: Ein Test, der alle `*ViewModel*.cs` nach den Namen der bekannten Schreibwege durchsucht (Muster `NachschlagenUndSchreiben`, `Nachfuellen`, `Uebernimm`) ist unzuverlaessig; besser eine kurze Checkliste im Kopfkommentar von `IDatenaenderungsVerlauf` mit der Liste der 11 Stellen. Kleinstschritt.
Abnahme: Fertig, wenn `IDatenaenderungsVerlauf.cs` die Stellen und die Regel «neuer externer Schreibweg -> Sperre» benennt und ein Test einen Beispielweg (Nachfuellen) mit Verlauf prueft (existiert teilweise: `DatenVerlaufFixRundeTests`).

### U05 (P2) MitVerlauf-Huellen doppelt
Belege: `Views/Pages/DataPage.Verlauf.cs:22-60` und `Views/Pages/SchaechtePage.Verlauf.cs:22-64` sind fast Zeile fuer Zeile gleich (`ErfasseVerlauf`, `Grid_PreparingCellForEditMitVerlauf`, `Grid_CellEditEndingMitVerlauf`, `ComboBox_*MitVerlauf`, `ClearColumnMitVerlauf`). Der Commit `e1c622203` hat die alten Handler bewusst unveraendert gelassen und nur den Verweis im Konstruktor/XAML geaendert (SchaechtePage.xaml.cs: `CommitSchachtDetailKonsolidiert` -> `CommitSchachtDetailMitVerlauf`, 4 Verweise in XAML).
Warum es bremst: Zu jedem Handler gibt es zwei Namen; bindet jemand spaeter den alten Namen wieder ein, laeuft die Eingabe ohne Verlauf. Beide Seiten muessen synchron gehalten werden.
Empfehlung: Gemeinsame Hilfsklasse `TabellenVerlaufAnbindung` (Zelle beginnen/beenden, Bereich fuer Combo), die beide Seiten aufrufen. Kein Umbau der Seiten.
Abnahme: Fertig, wenn `DataPage.Verlauf.cs` und `SchaechtePage.Verlauf.cs` je unter 30 Zeilen liegen und ein Test in beiden XAMLs verlangt, dass keine der nicht-Verlauf-Handler direkt gebunden sind.

### U06 (P3) Zeilengrenze und Restschuld
Belege: 7 UI-Dateien stehen bei 985-1000 Zeilen: `Views/Pages/SchaechtePage.xaml.cs` 1000, `ViewModels/TrainingStudioViewModel.cs` 1000, `ViewModels/ShellViewModel.cs` 999, `Services/AnnotationWorkbenchService.cs` 993 (weitere 985-997, siehe Messwerte).
`SchaechtePage.xaml.cs` war in `git log` bei den letzten Commits 1000 (`57be6e772`, `d1e21e66e`, `e1c622203`); davor wuchs es Schritt fuer Schritt von 919 auf 994. Beim Verlauf-Commit wurde die Zeilenzahl nicht erhoeht (nur 8 geaenderte Zeilen), die neue Logik ging in `SchaechtePage.Verlauf.cs`. Die Aufteilung der Klasse ist also teils fachlich (Strasse, Nachschlag, ColumnViews, AufklappListe, Protokollspalte, QgisSelection, Verlauf - je 27-144 Zeilen), teils Folge der Grenze.
Gut getrennt: `DossierPreviewFieldPanel` (7 Dateien, 1999 Zeilen; Fields 701, Topics 438, Toc 263, Rows 207, FixedTexts 159) folgt echten Bereichen. `ShellViewModel` (5 Dateien, 1783; ProjectSaving 390, Nova 124, DatenVerlauf 153) ebenso.
Schwaecher: `DataPage` 11 Dateien 1978 Zeilen (`DataPage.xaml.cs` 876), `SchaechtePage` 9 Dateien 1722 (`SchaechtePage.xaml.cs` 1000): weitere Erweiterungen werden wieder in neue Teildateien gehen.
Empfehlung: Vorhandenen Waechter (`MaintainabilityFitnessTests.cs:79-93`) behalten. Bei der naechsten Arbeit an SchaechtePage zuerst `RebuildColumns` (SchaechtePage.xaml.cs:170, 122 Zeilen, Entscheidungsproxy 13) auf einen Spaltenaufbau-Helfer auslagern.
Abnahme: Fertig, wenn SchaechtePage.xaml.cs unter 900 Zeilen liegt.

### U07 (P3) Lokale Stile neben dem Theme
Belege: `Views/Windows/NovaDialogWindow.xaml:20` (`NovaDialogDangerButton`) ist laut `Theme/Controls.xaml:1867` «optisch identisch» zu `DangerButton` (Controls.xaml:1870); die Doppelung ist in CLAUDE.md bewusst festgehalten. Weitere lokale Button-Stile: `PhotoMeasurementWindow.xaml:76,132`, `PlayerWindow.xaml:28,34,40`, `ObjektakteView.xaml:75` (Video-Fenster und geschuetzte Ausnahmen des Waechters `DesignAuditKnopfleistenTests`).
Empfehlung: Bei der naechsten Aenderung an NovaDialogWindow `NovaDialogDangerButton` durch `DangerButton` ersetzen (dazu Ausnahme fuer Regel (c) in `DesignAuditKnopfleistenTests` streichen). `ObjektakteView.ListenZeileKnopf` pruefen, ob ein Theme-Stil reicht.
Abnahme: Fertig, wenn die Ausnahme fuer NovaDialogWindow aus dem Waechter entfernt ist und `NovaDialogWindowIsolatedSmokeTests` gruen bleibt.

### U08 (P3) Neuer Dienst: Aufwand (Bestandsaufnahme)
Belege: 19 Dateien `ServiceProvider*.cs`, zusammen 1819 Zeilen (`ServiceProvider.cs` 939, `ServiceProviderRegistrationMap.cs` 262). Seit 27.09. 8 Commits daran. Fuer `IDatenaenderungsVerlauf` waren noetig: Property in `ServiceProvider.DatenVerlauf.cs`, Eintrag `ServiceProviderRegistrationMap.cs:97`, Zaehler im Test `ServiceProviderRegistrationTests.cs:143-147` (176 -> 177, mit fortlaufender Kommentarhistorie). Also 3 Stellen plus Nutzung, bei einem Dienst mit Konstruktorabhaengigkeiten kommt `ServiceProvider.cs` dazu (z. B. `Taskbar` :110, `BerichtsMarke` :273).
Neu gegenueber W07: harter Zaehlertest (177) verlangt bei jedem neuen Dienst einen Merge-Konflikt-anfaelligen Eingriff in einer Zahl, an der parallele Zweige regelmaessig kollidieren.
Empfehlung: Test von «Anzahl == N» auf «jede oeffentliche Property mit Schnittstellentyp ist in der Map» umstellen.
Abnahme: Fertig, wenn ein neuer Dienst ohne Zahlenanpassung im Test hinzugefuegt werden kann und ein vergessener Mapeintrag den Test rot macht.

## 4) Was gut ist
- `DatenaenderungsVerlauf` ist klein, WPF-frei, in Application; die UI-Anbindung besteht aus dünnen Huellen (je ~50-150 Zeilen). Klare Regeln in Kommentaren, Sperren mit Grund-Konstanten.
- `NovaDialog`/`DialogService`: 62 + 156 + 115 Zeilen, ein Marshalling-Punkt, Waechter verbietet `MessageBox.Show` ausserhalb (`DesignAuditDialogeTests`; Suche nach `MessageBox.Show` ausserhalb DialogService/NovaDialog fand keinen Treffer).
- `UserError.cs` 299 Zeilen, zentral und mit Sprachwaechter.
- Viele XAML-Waechter (Kontrast, Knopfleisten, Farbpaare, Tokens): Design-Regeln sind mechanisch abgesichert.
- Code-behind ist nicht zu gross: SchaechtePage.xaml.cs hat 54 Methoden (grösste `RebuildColumns` 122 Zeilen), DataPage.xaml.cs 43 (grösste `DataPage`-Konstruktor 90); beide legen Logik in Controller (`new DataPageBeobachtungenController` DataPage.xaml.cs:67, `SchachtMassnahmenDialogController` SchaechtePage.xaml.cs:156). Kein `File.*`/`Process.Start` im Code-behind gefunden (nur `File.Exists` als Delegate DataPage.xaml.cs:701).

## 5) Grenzen der Pruefung
- Kein Build, keine Tests gelaufen; Theme-Vergleich per Text (Bloecke mit `x:Key`, Kommentare entfernt); implizite Stile ohne Schluessel und Farbtoken wurden nicht einzeln verglichen (je 43 Style, nur 21 mit Schluessel erfasst).
- Bei den 4 abweichenden Stilen (`IconButton`, `Card`, `NavItemStyle`, `ToolbarButton`) wurde die Art der Abweichung nicht ausgewertet (vermutlich Schatten/Farbe).
- Code-behind-Logik nur nach Groesse und Stichprobe beurteilt, nicht Zeile fuer Zeile.
- Ob die Tastenliste in `TastenkuerzelWindow` heute vollstaendig ist, wurde nicht geprueft (nur die Kopplung).
