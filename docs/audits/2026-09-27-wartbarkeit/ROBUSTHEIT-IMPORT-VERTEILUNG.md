# Robustheit Projektimport und Verteilung (28.09.2026)

Anlass: Prüfung von Projektimport, Integration und Verteilung nach AP07. Sieben Befunde, alle
freigegeben («Alles umsetzen»). Je Befund zuerst ein Test, der am alten Code rot war, dann die
Korrektur, dann eine Gegenprobe (Korrektur kurz abgeschaltet → Test wieder rot, danach bytegleich
zurückgesetzt).

| # | Befund | Korrektur | Tests |
|---|---|---|---|
| 1 | Schacht- und Dichtheitsverteilung legte bei jedem erneuten Lauf `_01`-Kopien an; Begleit-PDF und Quelldateien ebenso | `DistributionTargetReuse`: eine inhaltsgleiche Datei im Zielordner wird wiederverwendet (Schacht-Einzelprotokoll, DP einzeln, Behälterprüfung, Seitenauszug eines Sammelberichts, Haltungs-PDF, Begleit-PDF, Quelldateien). `PdfInhaltsKennung` macht die Dokumentkennung (`/ID`) herausgeschnittener Seiten aus dem Inhalt statt zufällig — sonst wäre ein Seitenauszug nie bytegleich. | `DistributeShaftFiles_Zweimal_…`, `DistributeDichtheitFiles_Zweimal_…`, `…_SammelberichtZweimal_…`, `PdfInhaltsKennungTests` |
| 2 | Im Ein-Knopf-Import verschwanden gescannte Schachtprotokolle (ohne Textebene) und solche ohne lesbares Datum ohne jede Zeile | `ImportMediaPhase.EinordnenSchachtFehler`: ohne Textebene → Hinweis «bitte prüfen»; Schachtnummer erkannt, Datum fehlt → Fehler in der Bilanz; weder Nummer noch Datum → kein Schachtprotokoll, bleibt still | `Import_MediaPhase_ScannedOrDatelessShaftProtocolsDoNotDisappear` |
| 3 | Dichtheit: gescheiterte Ablage und unlesbare Datei zählten nicht als Fehler | `DichtheitImportDistributor.Result.Fehler` (additiv, `FehlerListe`); Ablagefehler, SHA-Lesefehler und Lesefehler bei der Suche landen dort. Die Medienphase meldet sie als Schritt «Dichtheitsverteilung» in der Fehlerbilanz; die Abschlusszeile nennt «n Fehler». | `Gescheiterte_Ablage_zaehlt_als_Fehler`, `Import_MediaPhase_DichtheitFehler_stehen_in_der_Fehlerbilanz` |
| 4 | Dichtheit: ein stilles `catch` um die ganze Kandidatensuche — eine gesperrte PDF liess alle Begleitprotokolle wegfallen | Schutz je Datei; eine gesperrte Datei wird vor der Textlesung erkannt und als Fehler gemeldet, die übrigen werden verteilt. Auch die KI-Zweitmeinung schützt jede Datei einzeln. | `Gesperrte_PDF_reisst_die_anderen_Protokolle_nicht_mit` |
| 5 | Schacht-Sammel-PDF: Schachtseiten ohne Datum vor dem ersten erkannten Protokoll fielen still weg | `SplitPdfIntoShafts` sammelt verwaiste Seiten (Schachtnummer ohne Datum; Seite ohne Textebene nur, solange keine Haltungsseite vorkam — danach sind es Fotos des Haltungsberichts). Je zusammenhängendem Bereich ein Ergebnis «Parse failed: … (Seite n, Schacht x nicht verteilt)». Deckblätter und Verzeichnisse bleiben still. | `DistributeShaftFiles_Sammel_PDF_meldet_…`, `…_Deckblatt_bleibt_still` |
| 6 | Ausweichnamen mit Zeitstempel (`foto_20260717_123000.jpg`) erzeugten bei jedem Lauf eine weitere, inhaltsgleiche Kopie (Staging und Medienverteilung) | `Ausweichkopien.FindeGleiche`: sucht im Zielordner genau dieses Namensmuster und verwendet eine inhaltsgleiche frühere Ausweichkopie | `Zweiter_Lauf_verwendet_die_fruehere_Kollisionskopie_wieder`, `DistributeImportedMedia_Namenskollision_FruehereAusweichkopie_…` |
| 7 | Nach dem Ein-Knopf-Import zeigte «Bericht öffnen» den Bericht eines früheren manuellen Laufs | `IOneClickImportReportWriter.TryWrite` gibt den Pfad zurück; der Controller meldet ihn nach erfolgreichem Speichern über denselben Weg wie der manuelle Import (`SetLastReportPath`, Toast mit «Bericht öffnen») | `Ein_Knopf_Import_merkt_seinen_Bericht_…`, `OneClickImportReportWriterTests` |

Schnittstellenänderungen: `IOneClickImportReportWriter.TryWrite` liefert jetzt `string?`
(einziger Implementierer und ein Test-Fake angepasst). `DichtheitImportDistributor.Result` und
`ImportOneClickProjectActions` sind additiv erweitert. Die Abschlusszeile der Dichtheitsprüfung
lautet jetzt «… bereits vorhanden, n Fehler.».

## Grenzen

- Ein Ablagefehler am genauen Weg (Haltungsbezeichnung) lässt die Dichtheitsprüfung weiterhin in
  den Rückfall über das Schachtpaar laufen; der Fehler bleibt trotzdem gemeldet.
- Eine Schachtseite ohne Textebene nach einer Haltungsseite wird bewusst nicht gemeldet.
- `ShaftDistributionService` filtert Dateien weiterhin still über `ShouldProcess`; die
  Archivsuche meldet übersprungene Ordner nicht; `ProjectPortabilityService` verliert beim
  Kopierfehler den Grund. Diese drei Nebenbefunde sind nicht Teil dieses Pakets.

## Bedienung, erster Teil (Rückmeldung und Klartext)

- **Jede Verteilung hinterlässt einen Bericht.** `IVerteilberichtAblage` / `VerteilberichtAblage`
  (Registrierung 171 → 172) schreibt nach Haltungs-, Schacht- und Dichtheitsverteilung sowie nach
  «Protokolle verteilen» `<Projekt>\__IMPORT_REPORTS\verteilung_<Art>_<Zeit>.txt` — neben die
  Importberichte, damit «Berichte» auf der Importseite alles zeigt. Nie überschrieben. Die
  Export-Seite zeigt danach einen Toast mit «Bericht öffnen» (grün ohne Fehler, gelb mit Fehlern;
  `IToastService.Warning(…, aktionText, aktion)` neu). «Protokolle verteilen» schreibt alle
  nicht zugeordneten Dateien und alle Fehlergründe in den Bericht (vorher nur Tageslog) und merkt
  ihn für «Letzter Bericht». Tests `VerteilberichtAblageTests`,
  `Jede_Schachtverteilung_hinterlaesst_einen_Bericht_im_Projekt`,
  `Protokollverteilung_schreibt_einen_Bericht_und_merkt_ihn`.
- **Umlaute in sichtbaren Texten** der Import-/Export-/Schachtprotokoll-Abläufe (Dialogtitel wie
  «…wählen», Meldungen wie «übernommen», «prüfen»). Log-Texte, Ordner- und Dateinamen
  (`Schaechte.xlsx` im Excel-Ziel, `Schächte_Verteilt`) und Bezeichner bleiben unverändert.
- **Klartext:** «Ziel-Wurzel» heisst «Hauptordner», der Tooltip sagt jetzt richtig «Leer = der
  Projektordner»; «Dry-Run» heisst «Probelauf». Ordnernamen wie `__UNMATCHED` und `__IMPORT_REPORTS`
  bleiben, weil bestehende Projekte sie tragen.

## Bedienung, zweiter Teil (Verteilen-Fenster)

- **Ein Fenster statt Dialogkette.** Die fünf Menüpunkte unter «Verteilen ▾» (Haltungen und
  Schächte je Normal/Sanierung, Dichtheitsprüfung) öffnen jetzt `VerteilenWindow` mit
  vorgewählter Art und Ablage; «Abgleichen» bleibt unverändert. Links die Schritte 1 WAS,
  2 ABLAGE (nicht bei Dichtheit, Hinweis auf den Unterordner «…_Saniert JJJJ»), 3 QUELLE
  (PDF-Ordner, «Stattdessen einzelne PDFs wählen», bei Haltungen auch der TXT-Import
  kiDVDaten.txt), 4 FILME (nur Haltungen), 5 ZIEL (wirksamer Hauptordner, Link zu den
  Ordnerbausteinen der Export-Seite). Rechts die Vorschau: Protokoll | Wird abgelegt in | Film |
  Zustand | Hinweis mit den Chips «gefunden», «wird abgelegt», «fehlt», «mehrdeutig»,
  «schon vorhanden», «nicht zugeordnet», «wird geprüft» und der Kopfzeile «n Dateien · m werden
  abgelegt · k brauchen Aufmerksamkeit». Hauptknopf «Jetzt verteilen (m Dateien)».
- **Die Vorschau schreibt nichts.** `IVerteilVorschau` (Application/UseCases/Verteilung) /
  `VerteilVorschauService` mit `VerteilVorschauPlanung` und `DichtheitVorschau`
  (Infrastructure/HoldingDistribution, bewusst ausserhalb des `HoldingFolderDistributor`, der
  nicht wachsen darf; dort wurden nur Zugriffsrechte geöffnet) planen mit denselben Bausteinen
  wie das Verteilen: Seitenlesen, Aufteilen von Sammelberichten (`SplitPdfIntoHoldings` /
  `SplitPdfIntoShafts` bekommen dafür eine Lesung ohne Texterkennung hereingereicht),
  `HoldingVideoSearch` samt Gegeninspektion, Verzeichnisbaum mit Sanierungsebene,
  `FindExistingIdenticalFile` bzw. für Auszüge aus Sammelberichten ein Inhaltsvergleich im
  Speicher (`BuildPdfPagesBytes`, derselbe Inhalt, den `WritePdfPages` schreibt). Kein Ordner,
  keine Datei, keine Projektänderung (Test `Vorschau_schreibt_nichts` vergleicht Grösse und
  Zeitstempel aller Dateien). Läuft im Hintergrund mit Abbruch; jede Änderung von Art, Ablage,
  Quelle oder Filmen rechnet neu und bricht die vorige Rechnung ab.
- **Sicher vorhergesagt:** Haltung, Datum und Zielordner aus der Textebene; Film eindeutig /
  mehrdeutig / fehlt (derselbe Suchweg); «schon vorhanden» für ganze PDFs, Auszüge aus
  Sammelberichten und Filme; Schachtprotokolle je Schacht inklusive Anhängen an denselben Schacht;
  Begleit-PDFs per Dateiname; mitkopierte Quelldateien (XTF/M150/MDB/XML); abgelehnte Einzeldateien
  und fremde PDFs im Schachtweg.
- **Ehrlich offen («wird beim Verteilen geprüft»):** reine Scans ohne Textebene und Bildseiten in
  gemischten PDFs (Texterkennung läuft erst beim Verteilen), der Katasterabgleich der
  Dichtheitsprüfung (Ablage unter «keine_Zuordnung»), «schon vorhanden», wenn der Haltungs- oder
  Schachtname im PDF korrigiert wird, und alles, solange kein gespeichertes Projekt den Hauptordner
  festlegt. Die TXT-Verteilung legt die TXT immer neu ab; sie zeigt nie «schon vorhanden».
- **«Jetzt verteilen» ruft die bisherigen Wege** mit genau der gewählten Quelle
  (`VerteilAuftrag`): `HoldingFolderDistributor` (PDF/TXT, Ordner/Einzeldateien),
  `IShaftDistributionService` (inkl. Staging und internem Speichern), Dichtheitsweg mit
  Katasterabgleich — mit Projekt-Operationssperre, `VerteilzielPruefung`, Bericht und Toast wie
  bisher. Die Schachtverteilung erhält ihre Speicherfreigabe erst, wenn der Auftrag wirklich ein
  Schachtauftrag ist.
- Registrierung 172 → 174 (`IVerteilVorschau`, `IVerteilenDialog`). Tests
  `VerteilVorschauServiceTests` (11, echte PDFs und Filme), `VerteilenViewModelTests` (9),
  `VerteilenWindowIsolatedSmokeTests` (echtes WPF), neue Fälle in
  `ExportPageDistributionProjectGuardTests` (Vorwahl, dieselbe Quelle an den Verteilweg,
  Abbrechen, Ordnerbausteine). Angepasst: die Harness beider Export-Testklassen liefert statt der
  Ja/Nein-Dialoge ein Test-Fenster (`IVerteilenDialog`), weil es die Dialogkette nicht mehr gibt.
