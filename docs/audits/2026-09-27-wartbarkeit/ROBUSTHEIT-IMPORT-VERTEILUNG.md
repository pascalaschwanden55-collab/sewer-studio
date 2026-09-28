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
