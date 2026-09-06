# Projektimport — Fortschritt zum Übergabeplan vom 05.09.2026

## Echter Abschlusslauf Göschenen — 06.09.2026, 00:02:28

Pascal meldet den abgeschlossenen Import. Bericht und gespeicherte Projektdatei unter
`D:\Projekte\Gep_Aufnahmen_Göschenen_2026` wurden danach rein lesend geprüft.
Vom Restore-Punkt um 23:50:34 bis zum Abschlussbericht: rund zwölf Minuten.
Gespeichert sind **272 Haltungen, 447 Schächte und 1'772 Haltungsbefunde**.
250 Haltungen tragen ein Video, 241 ein Originalprotokoll, 262 Schächte ein Protokoll.
Alle **753 geprüften eindeutigen Video-/PDF-Verweise** in den Hauptfeldern zeigen auf
vorhandene Dateien. Das ist eine Existenzprüfung, keine vollständige Inhaltsabnahme.

Zwei Quellenprobleme bleiben sichtbar:

- Die DB referenziert für Haltung `9956-9115` eine MP4-Datei. Unter diesem
  Haltungsnamen ist in der Quelle nur ein JPG vorhanden; der Videolink bleibt leer.
  Der Importbericht meldet dafür einen Fehler. Eine möglicherweise anders benannte
  Ersatzaufnahme ist mit dieser Namenssuche nicht ausgeschlossen.
- Datei und Projekt nennen Schacht `07.1093673`; im echten PDF-Kopf steht
  `07.10893673`. Die namensbasierte PDF-Verknüpfung existiert, der abweichende
  inhaltsbasierte Schachtauszug bleibt ohne Gegenstück und wird gemeldet.
  Die richtige Nummer wurde nicht geraten oder im Kundenoriginal geändert.

Die Kopfzahl „Haltungen: 2926“ im Bericht ist eine aufsummierte Importstatistik,
keine Zahl eindeutiger Haltungsdatensätze. Für diesen Nachweis wurden deshalb
die tatsächlichen Collections der gespeicherten Projektdatei gezählt.

## Zweite Laufzeitkorrektur — Haltungsbericht im Schachtverteiler

Nach dem Neustart waren 146 Schachtordner vorbereitet. Danach verarbeitete der
Schachtverteiler das 71-MB-Haltungs-Sammelprotokoll mit **1'003 Seiten**. Jede nicht
als Schacht erkannte Seite konnte die Formularprüfung und OCR auslösen. Der laufende
Import zeigte genau dieses Eingabedokument; ein Tesseract-Prozess war aktiv.

`ShaftPdfRelevance` prüft jetzt vor diesem Weg **alle Seiten ohne OCR**. Das vermeidet
auch den Fehler, nur die ersten sechs Seiten eines gemischten Berichts zu betrachten.
Nur durchgehend klar fremde Seiten erlauben das Überspringen. Unbekannte, leere,
gescannte, formularartige und erkennbare Schachtseiten behalten den bisherigen Weg.
Die Regel gilt für direkte und vorbereitete Importe. Im gemischten PDF werden
eindeutige Haltungsseiten ausserdem nicht an den vorherigen Schacht angehängt und
lösen keine Schacht-OCR aus.

**Lesender Nachweis am echten Göschenen-Bericht:** 1 Projektdeckblatt und 1'002
TV-Seiten, keine leere Seite. Die neue Vorprüfung überspringt ihn nach **1,81 Sekunden**.
Der danach zusätzlich aufgerufene echte `ShaftDistributionService` lieferte nach
**0,44 Sekunden** null Schachtergebnisse und legte keinen Ausgabeordner an.
Der zweite Aufruf profitiert vom bereits gelesenen Dateibestand. Diese Zahlen sind
keine neue Gesamtimportmessung und sagen nichts über die verbleibende Kopierzeit aus.

Die beiden neuen Schutzfälle (direkt/vorbereitet) schlugen vor dem Fix fehl. Nach
dem Fix bestanden alle **25 gezielten Tests**. Gegenfälle prüfen Schachtseiten erst
nach acht TV-Seiten, unklare/Bildseiten und ein gemischtes PDF mit zwei Schachtteilen;
beide Schachtausgaben enthalten genau die zugehörige Seite.

Release-Entwicklungsbuild und nach Pascals „app zu“ auch der normale Debug-Build:
jeweils erfolgreich mit 0 Warnungen und 0 Fehlern. Der Architektur-Skill wurde
aktualisiert und validiert. Kein Commit; keine Kundenoriginale verändert und keine
laufenden Prozesse automatisch beendet.

Abschliessend vollständige Infrastructure-Testsammlung: **6'148 bestanden,
6 übersprungen, 0 fehlgeschlagen**. Die Gesamtimportdauer bleibt nach dem nächsten
kompletten Import zu messen; die Einzelprüfung belegt nur den beseitigten OCR-Fehlweg.

## Laufzeitkorrektur Göschenen 2026 — 05.09.2026

Pascal meldete rund 15 Minuten Importdauer. Rein lesende Bestandsaufnahme der Quelle:
254 MP4-Dateien mit 51,49 GiB, 271 PDFs mit 5,78 GiB.

**Bestätigter Fehler:** Die PDF-Typ-Erkennung kannte keinen eigenen Schachttyp.
261 Schachtprotokolle galten deshalb als unbekannte Haltungs-Kandidaten und wurden
zusätzlich vollständig im Haltungsverteiler verarbeitet. Im aktiven Arbeitsordner
entstand beispielsweise `0.80-0.90/20260817_0.80-0.90.pdf`. Das ist nachweislich das
Schachtprotokoll von Schacht 9283: 0.80/0.90 sind seine rechteckigen Schachtmasse.
Dieser Fehler verursacht unnötige PDF-/OCR-Arbeit und kann falsche Haltungen erzeugen.

`PdfDokumentTypErkennung` erkennt nun Schachtprotokolle, Schachtinspektionen,
Schachtberichte und SCHACHTPRO-Köpfe. Der Haltungsverteiler schliesst diesen Typ aus;
die vorhandene Schachtverteilung bleibt zuständig. Positive TV-Merkmale haben für
gemischte Sammelberichte weiterhin Vorrang. Keine Kopierprüfung wurde entfernt.

Lesender Kontrolllauf über alle 271 echten PDFs mit der korrigierten Erkennung:
**1 TV-Sammelprotokoll, 261 Schachtprotokolle, 9 Pläne**, Dauer **6,57 Sekunden**.
Das ist die Dauer der Dokumentzuordnung, **keine gemessene Gesamtimportdauer**.
Die Videos müssen weiterhin kopiert und geprüft werden.

Der neue Schutztest war vor dem Fix rot; anschliessend bestanden alle 21 gezielten
Tests für Dokumenterkennung, Sammelprotokolle und Schachtverteilung. Der Release-
Entwicklungsbuild bestand mit 0 Warnungen und 0 Fehlern. Ein bereits laufendes
Debug-Programm verwendet seinen alten Stand weiter; der laufende Import wird durch
den Build nicht repariert. Kundenoriginale und aktive Importdateien wurden nur
gelesen, keine Prozesse beendet. Nach Pascals Rückmeldung „zu“ wurde das beendete
Programm geprüft und auch der gewohnte Debug-Build erfolgreich aktualisiert
(0 Warnungen, 0 Fehler). Die vollständige Infrastructure-Testsammlung bestand:
6'137 erfolgreich, 6 übersprungen, 0 fehlgeschlagen. Architektur-Skill aktualisiert
und validiert. Kein Commit erstellt.

## Nachprüfung und Nachbesserung — 05.09.2026

**Die acht im unabhängigen Review gefundenen Fehler sind behoben.** Dieser Abschnitt
ist der aktuelle Stand. Die weiter unten stehenden AP-Berichte dokumentieren die
vorherige Umsetzung; ihre damaligen Testergebnisse und Messungen sind keine neue
Abnahme des nachgebesserten Codes.

| Fehler im Review | Nachbesserung | Nachweis |
|---|---|---|
| Gleich viele Schäden galten als gleiche XTF | Modell und vollständige XML-Objekte samt Werten, TID und REF werden als Inhaltsbelege verglichen. Reine Anzahlen beweisen keine Teilmenge. | `ProjektimportReviewRegressionTests`: BAB/BBC trotz gleicher Anzahl; identische Datei als Gegenprobe |
| Direkt auf die Bezeichnung folgendes Datum wurde übersprungen | Untersuchung wird mit einem eigenen XML-Unterleser vollständig gelesen. | Unterschiedliche Aufnahmetage bleiben getrennt |
| Schacht-PDFs im vorbereiteten Importarchiv waren unsichtbar | Der Schachtverteiler liest die vorbereiteten Dateien über dieselbe Transaktion; der PDF-Leser erhält eine private Kopie mit richtiger Dateiendung. Gleichnamige Quellen bleiben getrennt. | `ShaftDistributionServiceTests.ArchivImStaging_WirdVorPublishGelesen` |
| Aktuelles Protokoll konnte das Video einer älteren Untersuchung erhalten | Hauptvideo wird an die aktive Datenbank-Untersuchung gebunden. Veraltete automatische Haupt-/Gegenlinks werden entfernt; Handverweise bleiben geschützt. | `WinCanMehrereBefahrungenTests` mit neuerer Rückfahrt und unterschiedlichen Metern |
| Kamerarichtung wurde mit Haupt-/Gegenfahrt verwechselt | Eine Hauptfahrt darf upstream sein. Die Richtung allein und zwei Dateien allein beweisen kein Gegenpaar. Widersprüchliche oder mehrdeutige Gegenkandidaten bleiben offen. | `BefahrungsrollenTests`, einschliesslich gleichem Richtungswert und ähnlichen Haltungsnummern |
| Beliebige XTF-TID galt als ersetzbare Dateikennung | `Xtf`/`Xtf405`/`Ili` beweist keine fremde Herkunft. Eine abweichende Kennung bleibt als Prüffall geschützt. Nur eigene `chSST`-Kennungen sind nachweislich lokal. | `GeonisTidAusXtf405_WiderspruchBleibtGeschuetzt` und Lookup-Tests |
| Ein nicht leerer Link zählte auch ohne Datei; Schachtfehler fehlten in der Summe | Abschluss prüft sichere Projektpfade und echte Lesbarkeit, auch vor Veröffentlichung. Fehlende WinCan-Medien und Schacht-Verteilfehler werden gezählt. Ohne Dateiprüfung darf der Abschluss keine vollständige Prüfung melden. | Dateibilanz-, Fehlerbilanz- und fehlende-Quellvideo-Tests |
| Erneuter Schacht-XTF-Import erzeugte zusätzliche Begehungen | Ein stabiler Quellfingerabdruck verhindert identische Revisionen. Geänderte Begehungen bleiben erhalten. | Wiederholung, Handkorrektur und geändertes Datum in `ProjektimportReviewRegressionTests` |

Zusätzliche Absicherungen:

- WinCan hält weitere Videos an ihrer Untersuchung in `ProtocolRevision.ImportVideoPaths`
  fest. Der Kanalverteiler kopiert auch diese Videos ins Projekt und speichert relative
  Pfade. Eine unklare Aufnahme erhält deshalb keinen erfundenen `Link_G`.
- `ImportFingerprint` verhindert auch bei WinCan doppelte historische Untersuchungen.
  Ein unveränderter Quellstand ersetzt keine zwischenzeitliche Protokollkorrektur.
  Beide neuen Metadaten werden beim Klonen und Speichern erhalten; alte Projektdateien
  bleiben lesbar. Die vorhandene Historienoberfläche erhielt keinen neuen Videoauswahldialog.
- Ein Abbruch nach dem letzten Importschritt wird vor der Veröffentlichung erkannt.
  Der UI-Test prüft: keine Veröffentlichung, keine Projektübernahme, kein Speichern.

### Grenzen der Aussage

- Die alten **59 Richtungs-Konflikte** in Göschenen waren eine falsche Regel, keine
  notwendige Rückfrage an den Unternehmer. Upstream ohne Gegenmarke ist für sich zulässig.
- Die frühere Behauptung, drei umnummerierte XTF-Exporte seien durch gleiche Anzahlen
  vollständig als Teilmengen bewiesen, gilt **nicht**. Bei geänderten TIDs/Verweisen
  bleibt eine Quelle vorsichtshalber enthalten und wird zur Prüfung gemeldet. Eine
  automatische Gleichsetzung ganzer umnummerierter Objektgraphen ist nicht implementiert.
- Geprüft wird, ob verknüpfte Projektdateien sicher erreichbar, lesbar und nicht leer
  sind. Das ist keine vollständige Prüfung jedes Videocodecs, jeder PDF-Seite oder
  sämtlicher inhaltlich erwarteter Dateien. Fehlende Sollangaben bleiben ausdrücklich offen.
- Die automatisierten Gegenproben verwenden künstliche Quellen. Kundenoriginale wurden
  bei der Nachbesserung nicht verändert. Eine neue vollständige Abnahme aller grossen
  Kundenprojekte und die Prüfung am Bildschirm sind damit nicht ersetzt.
- Der tatsächliche Import in GEONIS und der Abgleich mit zwischenzeitlich geändertem
  GEONIS-Bestand bleiben externe Abnahmepunkte. Die fertige XTF-Ausgabe wurde hier nicht umgebaut.

### Prüfung nach der Nachbesserung

Die ersten gezielten Gegenproben waren vor der Reparatur **8-mal rot**; die positive
Kontrolle mit zwei identischen XTF-Dateien war bereits grün. Nach der Reparatur sind
die Gegenproben und die erweiterten Verhaltenstests grün.

| Prüfung | Ergebnis |
|---|---|
| `dotnet build AuswertungPro.Dev.slnf -c Release --no-restore` | erfolgreich, 0 Warnungen, 0 Fehler |
| Infrastructure.Tests | 6'132 bestanden, 6 übersprungen |
| Pipeline.Tests | 2'562 bestanden, 3 übersprungen |
| UI.Tests | 6'325 bestanden, 3 übersprungen |
| ProjectModernizer.Tests | 62 bestanden |
| Gesamt | **15'081 bestanden, 12 übersprungen, 0 fehlgeschlagen** |
| Vollständiger Standard-Build | nur beim Kopieren der vom laufenden MCP-Dienst gesperrten `SewerStudio.McpServer.dll` gescheitert (MSB3027/MSB3021) |
| MCP-Projekt separat in temporären Ausgabeordner gebaut | erfolgreich, 0 Warnungen, 0 Fehler; keine laufenden Prozesse beendet |
| Architektur-Skill | mit Code abgeglichen, aktualisiert; `quick_validate.py`: gültig |

Die vier Testsammlungen wurden mit `-c Release --no-build --no-restore` nach dem
erfolgreichen Entwicklungs-Build ausgeführt. Der unveränderte Standardweg des
Gesamt-Builds ist wegen der Dateisperre weiterhin nicht vollständig grün; der
separate MCP-Build ersetzt diese Aussage nicht.

Kein Commit erstellt; die vorher vorhandenen Änderungen sind erhalten.

---

Plan: `UMSETZUNGSPLAN-5.6-TERRA-PROJEKTIMPORT-2026-09-05.md` (Audit-Ordner unter `%TEMP%`).
Ausgangsstand: `c1021e76e`. Diese Datei wird nach jedem Arbeitspaket fortgeschrieben.

Legende: **offen** · **in Arbeit** · **geprüft** · **blockiert**

## Auftragsumfang eingegrenzt (Entscheid Pascal, 05.09.2026)

**Nur Projekte neueren Datums. IBAK und WinCan sind die häufigsten Systeme.**
Aufnahmen mit älteren Systemen sind aus dem Auftrag genommen.

Was das konkret bedeutet — gemessen an allen 52 geprüften Ordnern:

| Jahr | erkannt | unerkannt |
|---|---|---|
| 2023–2026 | 33 (WinCan 20, IKAS 9, IBAK 3, KINS 1) | 3 |
| 2018–2021 | 1 | 15 |

Die 15 unerkannten Ordner von 2018–2021 (Erstfeld Zone 6.00–6.21, Andermatt 2.11/2.12,
Hospental 10.1 — durchweg WinCan VX mit `.sdf`) sind **nicht mehr Teil der Abnahme**.
Der Import meldet weiterhin ehrlich, was in ihnen liegt; freigegeben werden sie nicht.
Damit entfällt die Plan-Forderung, für jeden der 17 Ordner einen Importweg zu
finden (AP11).

**Die Arbeit aus AP2 und AP3 bleibt trotzdem drin.** Sie betrifft nicht nur alte
Projekte: Das IBAK-Projekt *Göschenen Unterdorfstrasse* von 2026 schreibt seine
Inspektions-XTF weiterhin im Modell `VSA_KEK` Fassung **15.07.2008**. „Altes Modell"
heisst also nicht „altes Projekt". Zum Vergleich: Göschenen GEP 2026 (WinCan) liefert
`.db3` und `VSA_KEK_2020_LV95` — den modernen, direkt lesbaren Weg.

| AP | Thema | Stand |
|---|---|---|
| AP0 | Ausgangsstand und kleine Prüffälle | **geprüft** |
| AP1 | Fehler vollständig bis zum Abschluss melden | **geprüft** |
| AP2 | Quellen vollständig erfassen und begründet auswählen | **geprüft** |
| AP3 | Alte VSA-XTF nach Haltungen und Schächten einlesen | **geprüft** |
| AP4 | Videokopien und gültige Dateireferenzen erkennen | **geprüft** |
| AP5 | Haupt-, Gegen- und Wiederholungsbefahrung unterscheiden | **geprüft** |
| AP6 | Beide Videos in allen Importwegen übernehmen | **geprüft** |
| AP7 | Haltungsprotokolle vollständig verteilen | **geprüft** |
| AP8 | Schacht-Sammelprotokolle automatisch anschliessen | **geprüft** |
| AP9 | Quellenwege aktivieren und Gesamtabschluss absichern | **teilweise** (Quellen geprüft, Abschluss offen) |
| AP10 | GEONIS-Kennungsherkunft und Exportbereitschaft | **teilweise** (Herkunft geprüft, Rückweg extern offen) |
| AP11 | Gesamtprüfung und Übergabe | offen |

---

## AP0 — Ausgangsstand und kleine Prüffälle (geprüft, 05.09.2026)

### Was geprüft wurde

Der Ausgangsstand ist sauber (`git status` leer, HEAD `c1021e76e`). Der schnelle
Release-Build läuft mit **0 Warnungen und 0 Fehlern** durch. SewerStudio selbst lief
während der Arbeit nicht; nur `SewerStudio.McpServer.exe` war offen, der sperrt keine
Build-Ausgaben.

Jeder Befund des Audits wurde gegen den echten Code und — rein lesend — gegen
`D:\Videoprojekte` nachgeprüft. Ergebnis: **alle acht P1-Befunde sind bestätigt**, keiner
ist zwischenzeitlich behoben worden.

| Befund | Bestätigt an | Was der Code heute tut |
|---|---|---|
| Alte XTF-Projekte werden abgewiesen | `KanalExportDetector.cs` | Erkennt nur `VSA_KEK_2020_LV95`, liest dafür nur die ersten 64 KiB, kennt `.db3`/`.mdb` unter `DB\`, aber **kein `.sdf`** |
| Schachtbegehungen werden Haltungen | `LegacyXtfImportService.VsaKek.cs` | Jede `Untersuchung` wird zu einem `HaltungRecord`; `Normschachtschaden` wird gar nicht gelesen |
| Videokopien blockieren die Zuordnung | `WinCanDbImportService.cs` (`LinkMediaFromFileIndex`) | `Take(2)` → zwei bytegleiche Kopien = „mehrere Kandidaten" = **kein** Link; ein nicht leerer, aber toter Link verhindert jede Neusuche |
| Gegenvideo bleibt aussen | `KanalImportDistributionService.cs` | Der Rückfall-Kopierlauf kennt nur `Link`, nie `Link_G` |
| Ein Einzelprotokoll schaltet den Split ab | `ProjectImportOrchestrator.cs` | `splitPdf: nameBasedHaltungHits == 0` — ein einziger Treffer deaktiviert alle Sammelprotokolle |
| Schacht-Sammelprotokolle fehlen im Ablauf | `ProjectImportOrchestrator.cs` | `IncludeSchacht: false`, ausdrücklich dem manuellen Befehl überlassen |
| „0 Fehler" beweist nichts | `ProjectImportOrchestrator.cs`, `NameBasedProtocolDistributor.cs`, `ImportPlausibilitaetsTor.cs` | `ProtocolDistributionReport.Meldungen` (die Kopierfehler je Datei!) wird nie gelesen; `mediaResult.Errors` fliesst nicht in `errors`; ein leeres Quellenprotokoll ergibt **Grün** |
| Eigene Kennung blockiert GEONIS | `KatasterKennungPlanBuilder.cs` | Gültige SIA-Form in `Objekt_ID` ⇒ `Abweichend`, obwohl `record.Geonis` leer und die Herkunft unbekannt ist |

### Was die echte Quelle zeigt (Andermatt Zone 2.11, nur gelesen)

Der Ordner ist ein WinCan-VX-Projekt. Seine Daten liegen als **`.sdf`** unter
`Projects\...\db\` — deshalb greift die WinCan-Erkennung nicht. Die drei XTF-Exporte unter
`Misc\Exchange\` tragen im Kopf das **alte** Modell `VSA_KEK` Version `15.07.2008`
zusammen mit `SIA405_Abwasser` — deshalb greift auch die XTF-Erkennung nicht.

Die grösste dieser XTF enthält 48 Untersuchungen. Die Trennung ist darin eindeutig:

| Art | Anzahl | Erfassungsart | von-/bisPunkt | Schadenselemente |
|---|---|---|---|---|
| Haltung | 23 | `Kanalfernsehen` | vorhanden | nur `Kanalschaden` |
| Schacht | 24 | `Begehung` | fehlt | nur `Normschachtschaden` |
| unklar | 1 (`2204`) | fehlt | fehlt | keine |

Schacht `3133` hat acht `Normschachtschaden`-Einträge — genau der Fall aus der
Abnahmematrix. Der unklare Fall `2204` trägt zusätzlich das Platzhalterdatum `00010101`
und keinen Operateur; er darf in AP3 **weder** als Haltung **noch** als Schacht geraten
werden, sondern muss als offener Befund erscheinen.

Der Schachtname `2200` kommt zweimal vor (zwei Begehungen desselben Schachts). Mehrere
Untersuchungen desselben Bauwerks sind hier also real und kein Sonderfall.

### Was angelegt wurde

- `tests/.../Import/AlteVsaKekTestquelle.cs` — Generator für kleine künstliche XTF-Dateien
  im alten VSA_KEK-Modell (Haltung, Schacht mit Schachtschäden, unklare Untersuchung).
  Bildet den echten WinCan-VX-Kopf nach. **Kein Kundenoriginal im Repository.**
- `tests/.../Import/AlteVsaKekQuelleSchutzTests.cs` — zwei Schutztests für das, was heute
  schon richtig ist: eine Haltung aus einer alten VSA_KEK-Datei wird mit Name, beiden
  Schächten und ihren Kanalschäden gelesen, und mehrere Haltungen bleiben getrennt.
  Beide **bestanden** (121 ms).

Der Soll-Test für die falsche Schachtzuordnung entsteht bewusst erst in AP3 — dort wird
sein vorheriges Scheitern dokumentiert.

### Grenzen dieses Arbeitspakets

- Die 973 GB unter `D:\` wurden nicht kopiert und keine Videos abgespielt.
- Die 110 XTF-Dateien sind als XML lesbar; das ist **keine** INTERLIS-Modellprüfung.
- „Erkannt" heisst nicht „vollständig importiert". Auch die 34 heute erkannten Ordner
  sind damit nicht abgenommen.

---

## AP1 — Fehler vollständig bis zum Abschluss melden (geprüft, 05.09.2026)

### Verhaltensänderung

Vorher konnte der Ein-Knopf-Import „0 Fehler" melden, obwohl Teilschritte sehr wohl
gescheitert waren. Drei Löcher sind zu:

1. **Kopierfehler der Namensverteilung.** `ProtocolDistributionReport.Meldungen` enthält
   pro Datei den Grund, warum ein Protokoll nicht kopiert werden konnte. Diese Liste
   wurde befüllt und **nie gelesen**. Jetzt steht jede Meldung mit Dateiname im Bericht
   und zählt als Fehler.
2. **Fotoverteilung.** `mediaResult.Errors` stand nur im Zusammenfassungstext, floss aber
   nicht in `errors`. Jetzt zählt sie mit.
3. **„Geprüft" ohne Sollzahl.** Das Plausibilitätstor liefert bei fehlendem
   Quellenprotokoll `Grün`. Das ist als Ablaufentscheidung richtig — es gibt nichts zu
   beanstanden —, war aber als *Aussage* falsch: Es wurde nie etwas geprüft.

Statt einer blossen Gesamtzahl führt der Lauf jetzt eine **Fehlerbilanz nach Schritten**.
Der Bericht in `__IMPORT_REPORTS\` und die Abschlussmeldung nennen jeden fehlerhaften
Schritt beim Namen.

### Geänderte Dateien

| Datei | Änderung |
|---|---|
| `Application/Import/ImportFehlerbilanz.cs` | **neu** — `ImportSchrittFehler`, `ImportFehlerbilanz`, `ImportFehlerbilanzSammler` (reines Datenobjekt) |
| `Application/Import/OneClickImportVollstaendigkeit.cs` | **neu** — ein ehrlicher Satz: geprüft / nicht vollständig / nicht geprüft |
| `Application/Import/IOneClickProjectImportService.cs` | additiv `Fehlerbilanz` |
| `Application/UseCases/Import/Quellen/ImportPlausibilitaetsTor.cs` | additiv `PlausibilitaetsUrteil.Geprueft` |
| `Infrastructure/Import/ProjectImportOrchestrator.cs` | Fehler schrittweise zählen; `Meldungen` und Fotofehler angeschlossen |
| `Infrastructure/Import/OneClickImportReportWriter.cs` | Vollständigkeitszeile + Fehler nach Schritten |
| `UI/Services/ImportOneClickProjectController.cs` | Abschlussmeldung nennt Vollständigkeit und fehlerhafte Schritte |

**Bewusst nicht geändert:** `PlausibilitaetsStufe` bekam keinen neuen Wert. Sonst hätte
der manuelle Import plötzlich einen zusätzlichen Rückfragedialog bekommen — genau das
„blinde Umdeuten für alle Aufrufer", das der Plan verbietet. `Geprueft` ist eine reine
Zusatzangabe; der Ablauf bleibt für jeden bisherigen Aufrufer identisch.

### Tests

Neu: `tests/.../Import/ImportFehlerbilanzTests.cs` — 7 Tests.

**Vorheriges Scheitern belegt:** Mit abgeklemmten Anschlüssen (also dem alten Verhalten)
scheitern **3 von 7** Tests; mit dem Fix bestehen alle 7. Die Prüfung wurde in beide
Richtungen gefahren.

Vollständiger Release-Weg danach, alle vier Suiten grün:

| Suite | Ergebnis |
|---|---|
| Infrastructure | 6017 bestanden, 6 übersprungen |
| UI | 6320 bestanden, 3 übersprungen |
| Pipeline | 2545 bestanden, 3 übersprungen |
| ProjectModernizer | 62 bestanden |

### Prüfblockade (nicht kritisch)

`dotnet build AuswertungPro.sln` meldet **2 Fehler**, beide dieselbe Ursache: Der laufende
`SewerStudio.McpServer.exe` (PID 32172) sperrt seine eigene DLL. Er gehört zu dieser
Sitzung und wurde absichtlich nicht beendet. Alle produktiven Projekte und alle vier
Testprojekte bauen fehlerfrei; das Werkzeugprojekt ist von der Änderung nicht betroffen.

### Offene Grenze

Die Fehlerbilanz macht Fehler sichtbar. Sie sagt noch **nicht**, ob jede erwartete Datei
angekommen ist — dafür braucht es die Sollzahlen aus AP2 und AP9.

### Nächstes

AP2 (Quellen vollständig erfassen und begründet auswählen).

---

## AP2 — Quellen vollständig erfassen und begründet auswählen (geprüft, 05.09.2026)

### Verhaltensänderung

Die Erkennung las bisher die ersten **64 KiB** einer XTF als Text und suchte darin die
Zeichenfolge `VSA_KEK_2020_LV95`. Drei Fehler in einem Satz:

1. **Die Version stand im Suchwort.** Alle älteren WinCan-Exporte führen `VSA_KEK` in
   der Fassung 15.07.2008 und fielen durch.
2. **Die SIA-Abhängigkeit versteckte die Untersuchungen.** Im selben Kopf steht
   `SIA405_Abwasser` — die Datei galt damit zusätzlich als Katasterdatei.
3. **Ein langer Kopf schob den Namen aus dem Fenster.** Und der Kopf sagt ohnehin
   nichts darüber, ob überhaupt Untersuchungen drin sind.

Neu entscheidet **Modellfamilie UND Fachinhalt**, gelesen mit einem strömenden
XML-Leser ohne Grössengrenze. Und der Ordner meldet jetzt, *was* er enthält.

Vorher für Andermatt Zone 2.11:

> Kein WinCan (.db3/.mdb in DB/) und kein IKAS/IBAK-Signal gefunden

Nachher (echter Lauf, nur lesend):

> Nicht importierbar, aber Fachdaten vorhanden: WinCan-VX-Datenbank
> `Gep_Andermatt_Zone_2.11_….sdf` gefunden — SQL Server Compact, unter .NET nicht direkt
> lesbar; 1 XTF-Datei mit zusammen 48 Untersuchungen (Modell VSA_KEK, SIA405_Abwasser,
> SIA405_Base, Base) — dieses Modell ist für den Import noch nicht freigegeben

### Die Zahl 144 war dreimal dieselbe Zone

Unter `Misc\Exchange\` liegen drei Ordner mit je einer `Zone_2.11.xtf`. Alle drei
enthalten dieselben 48 Untersuchungen; nur eine führt zusätzlich die 220 Foto- und
Videoverweise. Der Import las alle drei — daher „144 gefunden".

`XtfExportAuswahl` nimmt jetzt je Untersuchungsmenge **einen** Export, und zwar den
inhaltsreichsten. Zwei Sicherungen dagegen, dass dabei etwas verschwindet:

- Weggeworfen wird nur, was der Sieger **in jeder gezählten Klasse** enthält. Sonst
  wird die zweite Datei zusätzlich gelesen und der Widerspruch benannt.
- Verschiedene Untersuchungsmengen sind verschiedene Daten und werden alle gelesen.

**Wichtige Messung nebenbei:** WinCan vergibt bei **jedem** Export neue TIDs. Der erste
Entwurf gruppierte über die TIDs und sah deshalb drei verschiedene Zonen. Der
Fingerabdruck läuft jetzt über `Bezeichnung` + `Zeitpunkt` — das bleibt stabil und
trennt trotzdem eine Wiederholungsbefahrung am anderen Tag. Ein Test hält das fest.

### Geänderte Dateien

| Datei | Änderung |
|---|---|
| `Application/UseCases/Import/Quellen/XtfQuellenklassifikation.cs` | **neu** — Modellfamilie und Quellenart als reine Regel |
| `Application/UseCases/Import/Quellen/XtfExportAuswahl.cs` | **neu** — welcher Export gelesen wird und warum |
| `Application/Import/IXtfQuellenPruefer.cs` | **neu** — Vertrag |
| `Infrastructure/Import/Xtf/XtfQuellenPruefer.cs` | **neu** — strömender XmlReader, DTD gesperrt |
| `Infrastructure/Import/KanalExportDetector.cs` | Kandidatenliste, `SdfPath`, ehrlicher Unknown-Grund; `ReadXtfHeader` entfernt |
| `Infrastructure/Import/WinCan/WinCanDbImportService*.cs` | SDF-Ersatzweg wählt Exporte begründet aus |

**Bewusst NICHT gemacht:** Der alte VSA_KEK-Weg bleibt gesperrt (`Format = Unknown`).
Ihn jetzt freizugeben hiesse, die 24 Schachtbegehungen als Haltungen ins Projekt zu
lassen — genau die „vorübergehende Freigabe des falschen Schachtimports", die der Plan
verbietet. Die Freigabe gehört an das Ende von AP3.

Ebenfalls bewusst: `XtfQuellenPruefer` hat **keine** statische `Current`-Fassade. Der
Wächter `ArchitectureDriftRatchetTests` hat die erste Fassung zu Recht rot gemacht —
die Zahl dieser Fassaden darf nur sinken.

### Zwei angepasste Bestandstests

`KanalExportDetectorTests` legte zweimal eine **Textdatei** mit dem Inhalt
„Irgendwas VSA_KEK_2020_LV95 noch mehr Text" an und erwartete IKAS. Eine echte XTF ist
INTERLIS-XML; diese Fixture war ein Abdruck der alten Textsuche, kein realer Fall. Beide
tragen jetzt ein minimales echtes XTF — dieselbe Form, die ein dritter Test in derselben
Datei schon immer verwendet hat. Das ist keine Lockerung: Ein defektes XTF verschwindet
nicht mehr still, sondern wird im Erkennungsgrund **namentlich** genannt (eigener Test).

### Tests

Neu: `tests/.../Import/XtfQuellenerkennungTests.cs` — 13 Tests (Modellfamilie, Kataster
gegen Inspektion, langer Kopf, defektes XML, TID-Instabilität, zwei Begehungen desselben
Schachts, Exportauswahl, Ordnererkennung).

Alle vier Suiten grün: Infrastructure 6030, UI 6320, Pipeline 2545, ProjectModernizer 62;
12 übersprungen, 0 Fehler.

### Prüflauf gegen echte Ordner (nur lesend, nichts verändert)

| Ordner | Grund neu |
|---|---|
| Andermatt Zone 2.11 | SDF + 1 XTF mit 48 Untersuchungen |
| Erstfeld Zone 6.00 | SDF + 1 XTF mit 54 Untersuchungen |
| Hospental Zone 10.1 | SDF + 1 XTF mit 227 Untersuchungen |
| Erstfeld EIZ_…_202751 | SDF + 1 XTF mit 54 Untersuchungen |
| Erstfeld 2023 | unverändert „kein Signal" — der Ordner enthält nur eine `Thumbs.db` |

Das bestätigt die Audit-Zahl: 18 unbekannte Ordner, davon einer ohne Fachdaten,
**17 datenhaltige**.

### Offene Grenze

Erkannt ist nicht importiert. Diese Ordner bleiben gesperrt, bis AP3 Haltungen und
Schächte sauber trennt.

### Nächstes

AP3 — alte VSA-XTF nach Haltungen und Schächten einlesen.

---

## AP3 — Alte VSA-XTF nach Haltungen und Schächten einlesen (geprüft, 05.09.2026)

### Verhaltensänderung

Der Leser machte aus **jeder** `Untersuchung` einen Haltungsdatensatz und las
`Normschachtschaden` überhaupt nicht. Neu entscheidet `VsaKekUntersuchungsart` aus
vier Belegen, in dieser Reihenfolge:

1. **Bauwerksverweis** — liegt das Objekt in derselben Datei, ist es die Wahrheit.
2. **Schadensart** — `Kanalschaden` gehört an eine Haltung, `Normschachtschaden` an
   einen Schacht.
3. **Form der Untersuchung** — von-/bisPunkt und `Erfassungsart`
   (`Kanalfernsehen` gegen `Begehung`).
4. **Name als letzter Rückfall** — nur wenn sonst gar nichts spricht.

Widersprechen sich die Belege, bleibt der Fall **offen** statt geraten. Ein falsch
angelegtes Bauwerk zieht Ordner, Protokolle und später einen Katastereintrag hinter
sich her.

Wichtig: „Keine Schäden" ist kein Beleg gegen ein Bauwerk. Ein schadenfreier Schacht
bleibt ein Schacht — dafür sorgt Beleg 3.

### Der Namens-Rückfall — und warum er nötig wurde

Die volle Testsuite deckte eine Regression auf, die ich sonst übersehen hätte: Eine
IKAS-Untersuchung kann **nur** Bezeichnung, Zeitpunkt und eine Videodatei führen —
ohne Schäden, ohne Punkte, ohne Erfassungsart. Nach den Belegen 1–3 wäre sie „unklar"
gewesen und weggefallen. Bis heute wurde daraus immer eine Haltung.

Deshalb Beleg 4: Ein Haltungsname nennt zwei Schächte (`327015-2414`), ein Schachtname
ist eine einzelne Nummer (`3133`). Die Regel gilt **nur in eine Richtung** — eine
einzelne Nummer ist kein Beleg für einen Schacht. Damit bleibt der reale Fall `2204`
offen, und die Regel kann nur weniger raten als vorher, nie mehr.

### Zwei echte Funde am Kundenbestand

**1. Die Schreibweise.** Mein Testgenerator schrieb `NormschachtSchadencode`, der echte
WinCan-VX-Export schreibt `SchachtSchadencode`. Der Test war grün gebaut, aber am
echten Format vorbei. Der Leser akzeptiert jetzt beide Schreibweisen, der Generator
bildet die echte nach.

**2. Elf verlorene Schachtschäden.** Der erste Lauf gegen die echte Datei ergab 121 von
132 Schachtschäden. Ursache: Schacht `2200` wurde **zweimal** begangen, und mein
Schutz „ein vorhandenes Protokoll nie ersetzen" liess die zweite Begehung still
verschwinden. Beide Auswege wären falsch gewesen — auslassen verliert 11 Schäden,
ersetzen verliert die 11 der ersten Begehung. Jetzt wird die weitere Begehung als
**Protokollrevision** abgelegt und namentlich gemeldet; welche gilt, entscheidet die
Fachperson.

### Prüflauf gegen die echte Andermatt-XTF (nur lesend)

| Grösse | Vorher | Nachher |
|---|---|---|
| Haltungen | 47 (inkl. Schächte) | **23** |
| Schächte | 0 | **23** (24 Begehungen, `2200` zweimal) |
| Schachtschäden | 0 | **132 von 132** |
| Kanalschäden | 209 | 209 |
| Ungeklärt | — | 1 (`2204`), namentlich gemeldet |
| Schacht 3133 | stand als Haltung | 8 Schäden, Datum 03.07.2020 |

Meldung des Laufs: `48 Untersuchungen gelesen — 23 Haltung(en), 24 Schaecht(e), 1 ungeklaert.`
Bauwerke, Untersuchungen und offene Fälle sind damit getrennt gezählt.

### Geänderte Dateien

| Datei | Änderung |
|---|---|
| `Application/UseCases/Import/Quellen/VsaKekUntersuchungsart.cs` | **neu** — die reine Regel |
| `Infrastructure/Import/Xtf/LegacyXtfImportService.VsaKek.cs` | `Normschachtschaden` lesen, Operateur, Bauwerksverweis, Klassifikation, Schacht-Protokolleinträge |
| `Infrastructure/Import/Xtf/LegacyXtfImportService.Schaechte.cs` | Begehungen zusammenführen, weitere Begehung als Revision |
| `Infrastructure/Import/Xtf/LegacyXtfImportService.cs` | getrennte Zählung, offene Fälle als Warnung |

**Bewusst NICHT gemacht:** Die Erkennung bleibt weiter gesperrt (`Format = Unknown`).
Der Plan verlangt die Freigabe erst „nach AP3 **und** passender Medienverarbeitung" —
die Medien sind AP4. Die Aktivierung gehört damit in AP9.

### Tests

Neu: `tests/.../Import/AlteVsaKekSchachtImportTests.cs` — 13 Tests.

**Vorheriges Scheitern belegt:** Vor der Umsetzung scheiterten **7 von 8** der zuerst
geschriebenen Soll-Tests; danach bestehen alle.

Alle vier Suiten grün: Infrastructure 6043, UI 6320, Pipeline 2545,
ProjectModernizer 62; 12 übersprungen, 0 Fehler.

### Entscheid: keine Zustandsklasse am Schacht (Pascal, 05.09.2026)

Die Schachtbegehung landet als Protokoll am Schacht. Eine **Zustandsklasse wird daraus
NICHT berechnet** — das macht die Fachperson von Hand.

Der Unterschied zur Haltung ist gewollt: Dort rechnet der `VsaEvaluationService` aus den
Befunden. Am Schacht darf ein Import allenfalls einen Wert übernehmen, den die Quelle
ausdrücklich nennt (SIA405 `BaulicherZustand`, WinCan `Condition`) — aber niemals selbst
einen errechnen. Gesichert durch `SchachtbegehungBerechnetKeineZustandsklasse`.

### Offene Grenze

Videos und Fotos der Schachtbegehungen sind noch nicht angeschlossen (AP4).

### Nächstes

AP4 — Videokopien und gültige Dateireferenzen erkennen.

---

## AP4 — Videokopien und gültige Dateireferenzen erkennen (geprüft, 05.09.2026)

### Verhaltensänderung

Zwei Fehler, beide mit demselben Ergebnis — ein vorhandenes Video wurde nicht verlinkt:

1. **Zwei bytegleiche Kopien galten als „mehrere Kandidaten".** In Andermatt liegt das
   Video der Haltung `327015-2414` zweimal (unter `Video\Sec` und im XTF-Exportordner),
   byteidentisch. Der Import zählte zwei Kandidaten und verlinkte **gar nichts**. Zwei
   Kopien sind aber eine Aufnahme.
2. **Ein toter Link verhinderte jede Ersatzsuche.** Die Bedingung lautete „Link nicht
   leer → überspringen", ganz gleich ob die Datei noch existiert.

### Wie jetzt entschieden wird

`MedienInhaltsIndex` bestimmt, welche Dateien denselben Inhalt haben — sparsam:

- **Grösse zuerst.** Verschiedene Grösse heisst verschiedener Inhalt, ohne zu lesen. In
  einem Ordner mit 46 Videos sind das in der Regel null Lesevorgänge.
- **Einmal je Lauf.** Ein Zwischenspeicher hält Grösse, Zeitstempel und Prüfsumme fest;
  sonst würde bei 239 Haltungen derselbe Film hunderte Male gehasht.
- **Änderung während des Laufs ist ein Befund.** Weicht Grösse oder Zeitstempel vom
  gemerkten Stand ab, gilt die Datei als unklar — nicht als Kopie.

`MedienKandidatenAuswahl` entscheidet daraus: ein Inhalt → Treffer mit allen
Herkunftspfaden; mehrere Inhalte → offen; eine unlesbare Datei neben lesbaren → offen,
denn Gleichheit ist dann **nicht prüfbar**.

Der tote Link wird neu aufgelöst — aber nur, wenn er **absolut** ist. Ein relativer Link
zeigt ins Zielprojekt und wird hier nie angefasst; ihn durch einen Quellpfad zu ersetzen
würde eine fertige Verteilung zerstören. Findet sich kein Ersatz, bleibt der alte Wert
stehen und der Bericht sagt es.

### Nebenbefund: die Videos kommen jetzt an

Der direkte XTF-Lauf gegen die echte Andermatt-Datei setzt **23 von 23 Videolinks** und
108 Fotolinks. Der Audit-Befund „0 Videolinks" betraf den WinCan-Ersatzlauf, der drei
XTF-Exporte mischte — davon zwei ohne jede Medienreferenz. Seit AP2 wird der
inhaltsreichste Export gewählt, und der trägt die 220 Datei-Elemente.

### Geänderte Dateien

| Datei | Änderung |
|---|---|
| `Application/Import/MedienIdentitaet.cs` | **neu** — Vertrag `IMedienInhaltsIndex` und die reine Auswahlregel |
| `Infrastructure/Import/Common/MedienInhaltsIndex.cs` | **neu** — Grösse zuerst, Prüfsumme nur bei Kollision, Zwischenspeicher je Lauf |
| `Infrastructure/HoldingDistribution/VideoKopienAufloeser.cs` | **neu** — Kopien-Auflösung für die Verteilung |
| `Infrastructure/Import/WinCan/WinCanDbImportService*.cs` | Kopien-Erkennung, tote Links neu auflösen |
| `Infrastructure/HoldingFolderDistributor.VideoMatching.cs` | ruft den Auflöser in drei Zeilen |

**Wächter respektiert:** Die erste Fassung liess `HoldingFolderDistributor` auf 3084
Zeilen wachsen — `MaintainabilityFitnessTests` hat das zu Recht rot gemacht (Grenze
3064). Statt die Grenze anzuheben ist die Logik in eine eigene Klasse gewandert; die
God-Class steht jetzt bei 3063 Zeilen.

### Tests

Neu: `MedienKopienTests` (10) und `WinCanMedienVerknuepfungTests` (6).

**Vorheriges Scheitern belegt:** Mit abgeklemmter Kopien-Auflösung und abgeklemmter
Link-Erneuerung scheitern **5 von 16**; mit dem Fix bestehen alle.

Alle vier Suiten grün: Infrastructure 6059, UI 6320, Pipeline 2545,
ProjectModernizer 62; 12 übersprungen, 0 Fehler.

### Offene Grenze

Die Kopien-Erkennung sagt, ob zwei Dateien **gleich** sind. Sie sagt nicht, ob zwei
**verschiedene** Videos Haupt- und Gegenbefahrung sind — das ist AP5.

### Nächstes

AP5 — Haupt-, Gegen- und Wiederholungsbefahrung unterscheiden.

---

## AP5 — Haupt-, Gegen- und Wiederholungsbefahrung unterscheiden (geprüft, 05.09.2026)

Wie im Plan vorgesehen in zwei Schritten: erst die reine Regel, dann der Anschluss.

### Schritt 1 — die Regel

`Befahrungsrollen` gibt jeder Videodatei einer Haltung eine Rolle **mit Beleg**:
Hauptbefahrung, Gegenbefahrung, weitere Aufnahme oder ungeklärt.

Belege in dieser Reihenfolge:

1. **Kamerarichtung** aus Datenbank, XTF oder Protokoll — sie sagt es ausdrücklich.
2. **Dateinamenskonvention** — `~G`, `_G` oder `-g` unmittelbar hinter dem Haltungsnamen.

Was ausdrücklich **kein** Beleg ist: Dateireihenfolge, Dateigrösse, Änderungszeit, der
umgedrehte Haltungsname (`200-100` für `100-200` — es gibt parallele Leitungen) und
derselbe Aufnahmetag.

Bytegleiche Dateien (aus AP4) sind **eine** Aufnahme und erben dieselbe Rolle. Zwei
Kandidaten für dieselbe Rolle bleiben ungeklärt statt geraten. Ein Widerspruch zwischen
Richtung und Dateiname ebenfalls.

Der Kernsatz aus dem Plan ist damit umgesetzt: **nie „zweite Datei = Gegeninspektion".**

### Schritt 2 — der Anschluss an WinCan

Der eigentliche Datenverlust: Bei mehreren Befahrungen einer Haltung nahm der Import
**nur die neueste**. Die übrigen wurden gemeldet, ihre Befunde, Fotos und Videosekunden
gingen verloren — in Seilergasse 12 Befunde, 9 Fotos und 1 Video bei „0 Fehler".

Jetzt:

- Die übernommene Befahrung bleibt Arbeitskopie; **jede weitere kommt als eigene
  Protokollrevision** dazu. Dieselbe Mechanik wie bei den Schachtbegehungen in AP3.
- Die Videorollen vergibt die Regel aus `INS_InspectionDir` — nicht aus der Reihenfolge.
  Nur eine **belegte** Gegenbefahrung landet in `Link_G`.
- Ohne belegte Hauptbefahrung bleibt es beim bisherigen Verhalten (Video der übernommenen
  Befahrung). Nichts wird schlechter als vorher.

### Zwei Funde beim Bauen

- **Reihenfolge zählt.** Meine erste Fassung hängte die Revision an, *bevor*
  `ApplyProtocol` lief — und das baut das Protokolldokument neu auf. Die Revision war
  sofort wieder weg. Der Test hat es gefangen.
- **`OMM_FileType = 'V'` gibt es nicht.** WinCan schreibt `MPG` oder lässt das Feld leer
  (dann greift der Endungs-Rückfall). Mein Fixture war am echten Format vorbei.

### Geänderte Dateien

| Datei | Änderung |
|---|---|
| `Application/UseCases/Import/Quellen/Befahrungsrollen.cs` | **neu** — die reine Regel |
| `Infrastructure/Import/WinCan/WinCanDbImportService.Befahrungen.cs` | **neu** — Protokollzeilen je Befahrung, weitere Befahrungen als Revision, Videorollen |
| `Infrastructure/Import/WinCan/WinCanDbImportService.cs` | ruft beides auf; die Schleife ist herausgelöst, damit die grosse Datei nicht wächst |

### Tests

Neu: `BefahrungsrollenTests` (13 — alle zehn Pflichtfälle des Plans plus drei
Zusatzfälle) und `WinCanMehrereBefahrungenTests` (3).

Alle vier Suiten grün: Infrastructure 6062, UI 6320, Pipeline 2558,
ProjectModernizer 62; 12 übersprungen, 0 Fehler.

### Offene Grenze

Die Regel ist angeschlossen an **WinCan**. Die PDF-gestützte Verteilung sucht die
Gegeninspektion weiterhin über den Namen `<Haltung>g` und meldet eine unklare Gegenseite
noch nicht sichtbar — das ist ausdrücklich AP6 („Beide Videos in allen Importwegen
übernehmen", „Fehlende/unklare Gegenseite im Abschluss sichtbar").

Die Stationierung je Aufnahme (`CounterInspectionStationingNormalizer`) ist unverändert;
hydraulische Schächte werden von dieser Änderung nicht berührt.

### Nächstes

AP6 — beide Videos in allen Importwegen übernehmen.

---

## AP6 — Beide Videos in allen Importwegen übernehmen (geprüft, 05.09.2026)

### Verhaltensänderung

Der Rückfall-Kopierweg in `KanalImportDistributionService` kannte nur das Feld `Link`.
Das Gegeninspektionsvideo blieb als **absoluter Pfad auf die Kundenquelle** stehen — und
der Bericht meldete trotzdem „1 Video, 0 Fehler". In einer Kopie des fertigen Projekts
war das Gegenvideo damit nicht mehr abspielbar.

Jetzt laufen **beide Videofelder über denselben abgesicherten Kopierweg**: gleiche
Pfadprüfung, gleiche Medientyp-Erlaubnisliste, gleiche Staging-Transaktion. Die
bestehende Zielnamenkonvention bleibt (`-g` kennzeichnet die Gegenfahrt).

Drei Regeln dabei:

- **Ein Fehler beim einen Feld reisst das andere nicht mit.** Kommt das Hauptvideo an und
  scheitert die Gegenkopie, bleibt das Hauptvideo verteilt und der Fehler steht im Bericht.
- **Der Link wird erst nach erfolgreicher Kopie gesetzt.** Ein Verweis auf eine Datei, die
  nie ankam, wäre schlimmer als der alte Quellpfad.
- **Eine fehlende Quelldatei wird gemeldet.** Bisher lieferte genau dieser Fall keinen
  Fehlertext und verschwand still — der Verweis stand im Projekt, die Datei war nicht da,
  und niemand erfuhr davon.

### Geänderte Dateien

| Datei | Änderung |
|---|---|
| `Infrastructure/Import/KanalImportDistributionService.cs` | beide Videofelder über einen gemeinsamen `VerteileQuellvideo`; fehlende Quelle wird gemeldet |

### Tests

Neu: `GegenvideoVerteilungTests` (6) — Haupt- und Gegenvideo ohne PDF, nur Gegenvideo,
Hauptvideo erfolgreich bei fehlerhafter Gegenkopie, relative Links unangetastet,
zweiter identischer Lauf ohne Zweitkopie, kopiertes Projekt mit beiden gültigen Links.

**Vorheriges Scheitern belegt:** Mit auf `Link` beschränktem Kopierweg scheitern
**4 von 6**; mit dem Fix bestehen alle.

Alle vier Suiten grün: Infrastructure 6068, UI 6320, Pipeline 2558,
ProjectModernizer 62; 12 übersprungen, 0 Fehler.

### Offene Grenze

Der Kopierweg nimmt jetzt beide Rollen. **Woher** die Rolle kommt, ist nur bei WinCan
belegt (AP5). Die PDF-gestützte Verteilung setzt `Link_G` weiterhin über die Namenssuche
`<Haltung>g`; eine unklare Gegenseite erscheint im Bericht, aber der Abschluss zählt sie
noch nicht als eigene Sollgrösse. Das gehört zu AP9.

### Nächstes

AP7 — Haltungsprotokolle vollständig verteilen.

---

## AP7 — Haltungsprotokolle vollständig verteilen (geprüft, 05.09.2026)

### Verhaltensänderung

Zwei Einschränkungen fielen zusammen und liessen Protokolle liegen:

1. **Ein einziger Namenstreffer schaltete den Sammel-Split global ab.**
   `splitPdf: nameBasedHaltungHits == 0` — ein Ordner mit einem Einzelprotokoll für
   Haltung A und einem Sammelprotokoll für B und C liess B und C leer.
2. **Aus dem Archiv wurde genau EIN Protokoll gesplittet.** Mehrere ergänzende
   Sammelprotokolle waren damit nie abgedeckt.

Jetzt läuft der Split immer, wenn es überhaupt ein Protokoll gibt, und er betrachtet
**alle** erkannten TV-Protokolle in begründeter Reihenfolge. Pläne, Deckblätter und
Dichtheitsprotokolle bleiben über ihre negative Bewertung in `ScoreProtocolCandidate`
ausgeschlossen — neu ausdrücklich mit `Score >= 0`, statt nur den Sieger zu nehmen.

Der Schutz gegen doppelte Verknüpfungen liegt jetzt dort, wo er hingehört: **Eine schon
versorgte Haltung behält ihren Verweis aus dem Einzelprotokoll.** Eine Seite aus dem
Sammelprotokoll darf ihn nicht überholen — sonst entschiede die Reihenfolge, welche
Datei gilt. Die zusätzliche Seite bleibt als Version im Ordner und wird benannt.

### Ein Fund an der eigenen Fixture

Mein erster Test scheiterte mit „Parse failed, kein passender Haltungsordner gefunden" —
an **dreistelligen** Haltungsnummern. `100-200` erkennt der Titelparser nicht, `1000-2000`
schon. Nachvollziehbar: dreistellige Zahlen sind im Protokolltext zu leicht mit anderen
Zahlen zu verwechseln. Der Test verwendet jetzt vierstellige Nummern wie die bestehenden
Verteilungstests. **Das ist eine Grenze des Parsers, kein Fehler meiner Änderung** — sie
bestand vorher genauso.

### Geänderte Dateien

| Datei | Änderung |
|---|---|
| `Infrastructure/Import/KanalImportDistributionService.cs` | `SelectProtocolPdfsReadable` liefert alle TV-Protokolle; vorhandene Verknüpfung wird nicht überholt |
| `Infrastructure/Import/ProjectImportOrchestrator.cs` | Split wird nicht mehr durch einen Namenstreffer global abgeschaltet |

### Tests

Neu: `SammelprotokollVerteilungTests` (4) — Einzelprotokoll plus zwei Sammelprotokolle
versorgen alle vier Haltungen; Plan und Dichtheitsprüfung werden nicht als Protokoll
verteilt; eine vorhandene Verknüpfung wird nicht überholt; die Dateireihenfolge ändert
das Ergebnis nicht.

**Vorheriges Scheitern belegt:** Mit auf ein Protokoll begrenztem Split scheitert der
Kernfall; mit dem Fix bestehen alle vier.

Alle vier Suiten grün: Infrastructure 6072, UI 6320, Pipeline 2558,
ProjectModernizer 62; 12 übersprungen, 0 Fehler.

### Offene Grenze

Eine Haltung, die in **beiden** Quellen vorkommt (Einzel- und Sammelprotokoll), behält
zwei Dateien im Ordner. Die Verknüpfung ist begründet, die zweite Version bleibt zur
Kontrolle liegen und steht im Bericht — sie wird nicht gelöscht.

Der Titelparser braucht mindestens vierstellige Schachtnummern. Ob im Bestand
dreistellige Haltungsnummern vorkommen, ist nicht geprüft.

### Nächstes

AP8 — Schacht-Sammelprotokolle automatisch anschliessen.

---

## AP8 — Schacht-Sammelprotokolle automatisch anschliessen (geprüft, 05.09.2026)

### Verhaltensänderung

Der Import übersprang die Schächte ausdrücklich (`IncludeSchacht: false`) und überliess
sie dem manuellen Befehl „Schacht Verteilen". Ein vollständiger Projektimport liess sie
damit leer.

Jetzt ruft der Import **denselben `IShaftDistributionService`** wie der manuelle Weg,
mit **derselben Staging-Sitzung** und **derselben Verknüpfungsregel**. Ein zweiter
Splitter entsteht nicht.

Die Sorge hinter der alten Sperre — „damit kein falsches/ganzes PDF automatisch an die
Schächte gehängt wird" — ist berechtigt und steht jetzt als Regel im Code statt als
Verzicht:

- **Es wird kein Schacht angelegt.** Ein Protokollteil, dessen Nummer das Projekt nicht
  kennt, wird gemeldet: die Datei liegt im Ordner, der Verweis fehlt. Einen Schacht
  allein aus einer PDF-Seite zu erfinden, wäre fachlich nicht belegt.
- **Ein vorhandener Verweis wird nicht ersetzt.** Er kann von Hand gesetzt oder aus einem
  eindeutigen Einzelprotokoll gekommen sein.
- **Ein Haltungsprotokoll fällt beim Schacht-Parser durch** und wird als „Parse failed"
  übersprungen — es wird nicht an beide Endschächte gehängt. Nur echte Ablagefehler
  erscheinen im Bericht.
- **Ein Fehler bei den Schächten bricht den Import nicht ab.** Die Haltungen sind zu
  diesem Zeitpunkt bereits versorgt.

### Eine Regel, zwei Wege

`ApplyPdfPathsToSchachtRecords` und `FindShaftRecord` lagen im Export-ViewModel. Statt
sie im Orchestrator ein zweites Mal zu schreiben, sind sie als
`SchachtProtokollVerknuepfung` nach Application gewandert — **beide Wege verwenden jetzt
dieselbe Regel**. Eine zweite Umsetzung wäre genau die Stelle, an der sie später
auseinanderlaufen.

Nebeneffekt: Das ViewModel ist um die Dateilogik leichter geworden, und die offenen
Fälle erscheinen jetzt auch im manuellen Weg in der Zusammenfassung.

### Geänderte Dateien

| Datei | Änderung |
|---|---|
| `Application/Import/SchachtProtokollVerknuepfung.cs` | **neu** — die gemeinsame Regel |
| `Infrastructure/Import/ProjectImportOrchestrator.cs` | ruft die Schachtverteilung; `IShaftDistributionService` injizierbar |
| `UI/ViewModels/Pages/ExportPageViewModel.ShaftDistribution.cs` | nutzt den gemeinsamen Baustein statt eigener Zuordnung |

### Tests

Neu: `SchachtProtokollVerknuepfungTests` (5 — Verknüpfung, unbekannte Nummer, vorhandener
Verweis, Zielbaum mit Gemeinde und Jahr, zweiter identischer Lauf) und
`SchachtprotokollImportAnschlussTests` (4 — Aufruf mit Archivordner und ohne eigene
Dateiliste, Verknüpfung und Zählung, unbekannter Schacht im Bericht, Fehler bricht den
Import nicht ab).

Alle vier Suiten grün: Infrastructure 6081, UI 6320, Pipeline 2558,
ProjectModernizer 62; 12 übersprungen, 0 Fehler.

### Offene Grenze

Der Anschluss ist mit einem Testdouble geprüft, nicht mit einem echten
Schacht-Sammelprotokoll aus dem Kundenbestand. Ob der Schacht-Parser die realen
Protokolle von IBAK und WinCan zuverlässig in Seiten je Schacht zerlegt, gehört in die
Gesamtprüfung (AP11).

### Nächstes

AP9 — Quellenwege aktivieren und Gesamtabschluss absichern.

---

## AP9 — Quellenwege aktivieren (Teil 1 geprüft, 05.09.2026)

### Die Messung zuerst

Die Frage war: Soll die alte VSA_KEK-XTF freigegeben werden? Statt zu raten habe ich
**alle drei IBAK-Projekte** des Bestands gemessen (nur lesend):

| IBAK-Projekt | IBAK-Weg heute | XTF im Ordner |
|---|---|---|
| Göschenen Unterdorfstrasse | 86 Haltungen, **0 Schächte**, 2 Videolinks | Inspektion + Kataster |
| Erstfeld Jagdmatt | 71 Haltungen, 889 Befunde, 50 Videolinks | **keine** |
| Bürglen Gosmergasse | 2 Haltungen, 32 Befunde | nur Organisationsliste (2206 Einträge) |

**Meine erste Schlussfolgerung war falsch.** Nach dem ersten Projekt sah es so aus, als
wäre der XTF-Weg durchweg besser. Die anderen beiden zeigen: Bei **2 von 3** ist der
IBAK-Weg die einzige Quelle. Ihn zu ersetzen hätte 73 Haltungen und 921 Befunde gekostet.

Also: **ergänzen, nicht ersetzen.** Die MergeEngine entscheidet je Feld (Xtf schlägt
Legacy, Handeingaben bleiben geschützt).

### Ergebnis am echten Projekt

Göschenen Unterdorfstrasse, voller Ein-Knopf-Import in ein Wegwerf-Projekt:

| | vorher | jetzt |
|---|---|---|
| Haltungen | 86 | **93** |
| **Schächte** | **0** | **71** |
| Befunde | 708 | **727** |
| Videolinks | **2** | **86** |
| Fotos verteilt | 0 | **425** |
| Protokolle | 86 | **90** |
| Schacht-PDFs verknüpft | 0 | **50** (von 104 verteilten Teilen) |
| Fehler | 0 | 0 |

Die 50 verknüpften Schachtprotokolle sind AP8, das erst durch die 71 Schächte aus AP9
überhaupt greifen konnte.

### Gegenprobe: nichts wird schlechter

| Ordner | Format | fachliche XTF | Wirkung |
|---|---|---|---|
| Erstfeld Jagdmatt | Ibak | 0 | keine Änderung |
| Bürglen Gosmergasse | Ibak | 0 (Organisationsliste abgelehnt) | keine Änderung |
| Göschenen GEP 2026 | WinCan | 1 | keine Änderung — WinCan wird bewusst nicht ergänzt |
| Göschenen Unterdorfstrasse | Ibak | 2 | die Verbesserung oben |

**Bewusst nur für IBAK:** Bei WinCan ist kein solcher Bedarf gemessen, und ein
zusätzlicher XTF-Lauf könnte dort geprüfte Werte verschieben.

### Drei Fehler beim Bauen — alle vom echten Lauf gefunden

1. **Falscher Filter.** Ich nahm die Kandidaten mit `Uebernommen` — das heisst aber „von
   diesem Importweg verwendet", und der IBAK-Weg verwendet gar keine XTF. Ergebnis: null
   Ergänzungen. Neu gibt es `FachlicheXtfQuellen`, unabhängig vom Importweg.
2. **Katasterdateien ausgeschlossen.** `XtfExportAuswahl` war für die Inspektionswahl
   gebaut und lehnte reine Katasterdateien ab — genau die tragen die 71 Normschächte.
   Jetzt gibt es dafür einen ausdrücklichen Schalter.
3. **Falscher Ausschluss.** Die Erkennung *findet* eine SIA405-Datei auch im IBAK-Ordner;
   ich hatte sie als „schon gelesen" ausgeschlossen. Gelesen wird sie dort aber nie —
   das kostete 592 Bauwerke.

Dazu ein Zählfehler in AP8: „70 verknüpft" bei 50 tatsächlich verlinkten Schächten.
Mehrere Teile desselben Schachts mit demselben Ziel sind **eine** Verknüpfung.

### Geänderte Dateien

| Datei | Änderung |
|---|---|
| `Infrastructure/Import/ProjectImportOrchestrator.cs` | Schritt 5a: ergänzende XTF-Quellen bei IBAK |
| `Infrastructure/Import/KanalExportDetector.cs` | `FachlicheXtfQuellen`; bytegleiche XTF-Kopien zusammengefasst |
| `Application/UseCases/Import/Quellen/XtfExportAuswahl.cs` | Schalter `auchKataster` |
| `Application/Import/SchachtProtokollVerknuepfung.cs` | zählt nur echte Änderungen |

### Tests

Neu: `ErgaenzendeXtfQuellenTests` (4) — IBAK liest Inspektion und Kataster zusätzlich;
eine reine Organisationsliste wird nicht gelesen; WinCan wird nicht ergänzt; IKAS liest
seine Hauptquelle nicht zweimal.

Alle vier Suiten grün: Infrastructure 6085, UI 6320, Pipeline 2558,
ProjectModernizer 62; 12 übersprungen, 0 Fehler.

### Was von AP9 offen bleibt

- **Abbruch.** `ImportOneClickProjectController` übergibt weiterhin
  `CancellationToken.None`. Ein Token allein bringt nichts, solange es keine
  Abbruch-Schaltfläche gibt — das ist ein eigener kleiner UI-Schritt.
- **Sollzahlen im Abschluss.** Erwartete Videos und Protokolle werden noch nicht getrennt
  gezählt; die Fehlerbilanz aus AP1 sagt, *was* schiefging, aber nicht, *wie viel* fehlt.
- **Aufräumhinweis:** Ein voller Prüflauf über Erstfeld Jagdmatt kopiert rund 13 GB in den
  Temp-Ordner. Für Gegenproben reicht der rein lesende Weg über die Erkennung.

### Nächstes

AP10 — GEONIS-Kennungsherkunft.

---

## AP10 — GEONIS-Kennungsherkunft (Teil 1 geprüft, 05.09.2026)

### Verhaltensänderung

Der Planbauer schloss aus der **Form** auf die Quelle: „sechzehn Zeichen, beginnt mit
einem Buchstaben" galt als Beleg für eine neuere Katasterquelle — und blockierte damit
die Übernahme.

Das ist nicht belegt. Eine XTF-TID von WinCan sieht genauso aus
(`ch2585eaef000001`: sechzehn Zeichen, beginnt mit einem Buchstaben) und ist doch nur
die Zeilennummer in einer Exportdatei. Sie blockierte einen eindeutigen, geprüften
Katastertreffer.

Neu entscheidet `KatasterKennungHerkunft` aus **vorhandenen Feldern**:

| Herkunft | Beleg | Blockiert? |
|---|---|---|
| bestätigtes GEONIS | Feldquelle `Kataster` oder Handeingabe | **ja** — kann aus einem neueren Export stammen |
| unbekannt | gültige Form, aber keine belegte Quelle | **ja**, als Prüffall (`HerkunftUnklar`) |
| lokale Exportkennung | Präfix `chSST` — von SewerStudio selbst vergeben | nein |
| Dateikennung | Feldquelle `Xtf`, `Xtf405` oder `Ili` | nein |
| keine | keine SIA405-Form (z.B. die Lisag-Nummer `866789`) | nein |

Der Schutz ist damit **nicht schwächer**, sondern an einen Beleg gebunden. Und die
unbekannte Herkunft wird ausdrücklich zum Prüffall statt still zu gewinnen — genau das
verlangt der Plan.

### Ein Bestandstest musste umgeschrieben werden

`Eine_importierte_TID_in_Objekt_ID_sperrt_eine_abweichende_Kopie` hielt genau die alte
Annahme fest — sein eigener Kommentar sagte: *„Widerspricht sie der Kopie, stammt sie aus
einer neueren Quelle und gewinnt."* Er heisst jetzt
`Nur_eine_belegte_Kennung_sperrt_eine_abweichende_Kopie` und prüft vier Fälle statt drei:
Dateikennung (übernimmt), gleiche Kennung (übernimmt), Lisag-Nummer (übernimmt),
bestätigte Katasterkennung (**bleibt geschützt**).

Das ist keine Lockerung: Der geschützte Fall bleibt geschützt, er braucht nur einen Beleg.

### Geänderte Dateien

| Datei | Änderung |
|---|---|
| `Application/Lookup/KatasterKennungHerkunft.cs` | **neu** — die Herkunftsregel |
| `Application/UseCases/KatasterKennungPlanBuilder.cs` | entscheidet nach Herkunft; neuer Grund `HerkunftUnklar` |

### Tests

Neu: `KatasterKennungHerkunftTests` (20 — Herkunftsregel als Tabelle, Blockadeverhalten,
Wirkung im Plan für Haltungen und Schächte).

**Vorheriges Scheitern belegt:** Mit der alten Formregel scheitern **4 von 20**.

Alle vier Suiten grün: Infrastructure 6105, UI 6320, Pipeline 2558,
ProjectModernizer 62; 12 übersprungen, 0 Fehler.

### Was von AP10 offen bleibt

- **Der Bericht trennt noch nicht** zwischen „Neu-Export möglich", „Objektverbund für den
  GEONIS-Abgleich vollständig" und „Zuordnung ungeklärt".
- **Extern offen und unverändert:** Andreas bestätigt eine valide Datei, aber es gibt
  **keine** INTERLIS2→GEONIS-Abgleichsschnittstelle. FME ist ein Vorschlag, keine
  vorhandene Lösung. Ohne gemeinsamen Test mit der Gegenseite bleibt der Rückweg offen —
  daran ändert diese Arbeit nichts.
- Keine Schreiboperation in einer echten GEONIS-Datenbank; daran wurde nichts angefasst.

### Nächstes

AP11 — Gesamtprüfung und Übergabe. Davor sinnvollerweise die offenen Punkte aus AP9
(Abbruch, Sollzahlen) und AP10 (Berichtstrennung).

---

## Restpunkte aus AP9 und AP10 (erledigt, 05.09.2026)

Drei Punkte standen offen, bevor AP11 sinnvoll ist: der Abbruch, die Bestandszahlen im
Abschluss und die Trennung im GEONIS-Bericht.

### 1. Abbruch — der Knopf war wirkungslos

`ImportOneClickProjectController` übergab fest `CancellationToken.None`. Der Ein-Knopf-Weg
kopiert ganze Projekte und liess sich trotzdem nicht anhalten; der manuelle Importweg
hatte diesen Anschluss längst.

Beim Nachmessen zeigte sich, dass ein durchgereichtes Signal allein nichts nützt: Der
Orchestrator prüfte den Abbruch an **genau einer** Stelle, tief in der Schleife über die
Katasterdateien. Ein Quellordner ohne Katasterdatei lief nach dem Abbruch vollständig zu
Ende — samt Archivierung und Medienverteilung.

| Stelle | Vorher | Jetzt |
|---|---|---|
| Laufbeginn | keine Prüfung | Prüfung |
| Schritt 4 Archivieren | keine Prüfung | Prüfung |
| Schritt 5 Parsen | keine Prüfung | Prüfung |
| Schritt 7 Medien verteilen | keine Prüfung | Prüfung |
| Katasterabgleich | Prüfung je Datei | unverändert |

Dazu die zweite Hälfte: Ein Abbruch darf nicht als Fehler enden. Die Sammel-`catch` der
teuren Schritte lassen `OperationCanceledException` jetzt durch (6 Stellen), und der
Controller zeigt einen Hinweis „Import abgebrochen" statt eines roten Fehlerdialogs. Die
angelegten Dateien werden auf demselben Weg zurückgenommen wie bei einem Fehlschlag.

### 2. Bestandsbilanz — „0 Fehler" war keine Aussage

Der Abschluss nannte eine Zahl Haltungen und daneben „0 Fehler". Zusammen las sich das
wie eine Vollständigkeitszusage, sagte aber nichts darüber, ob Videos und Protokolle
wirklich angekommen sind — genau der Fall Göschenen (239 Ordner, 0 Protokolle,
„0 Fehler").

`ImportBestandszaehler` zählt jetzt am Ende jedes Laufs: Haltungen, davon mit Video,
Gegenvideo, Protokoll und Befunden; Befunde insgesamt; Schächte und davon mit Protokoll.
Die Zahlen stehen in den Meldungen, im Importbericht unter „Bestand nach dem Import" und
additiv als `Bestand` am Ergebnis.

**Bewusst keine erfundene Sollzahl.** Der Satz „Ob das fehlt oder so richtig ist, sagt
diese Zahl NICHT" steht im Bericht: Eine Haltung ohne Video kann sauber sein (nicht
befahren) oder ein Verlust. Das entscheidet die Fachperson.

### 3. GEONIS-Bericht — Neu-Export ist kein Abgleich

Der Kennungsbericht sagte, wie viele Bauteile ihre Kennungen bekämen, aber nicht, was das
für den GEONIS-Weg bedeutet. Der Unterschied wird in der Praxis verwechselt: Ein
Neu-Export gelingt **immer**, notfalls mit eigenen `chSST`-Kennungen — beim Import in
einen gefüllten Kataster entstehen daraus aber Duplikate, keine Aktualisierung.

`GeonisVerbund` trennt drei Zustände:

| Zustand | Bedingung | Bedeutung |
|---|---|---|
| Abgleich möglich | Haltung + Kanal + beide Haltungspunkte (Schacht: Knoten + Bauwerk) | GEONIS erkennt die vorhandenen Objekte wieder |
| nur Neu-Export | Verbund unvollständig | legt neue Objekte an statt zu aktualisieren |
| ungeklärt | mehrdeutig, abweichend oder Herkunft unklar | von Hand zu klären |

Das Rohrprofil bleibt bewusst aussen vor: Es wird in GEONIS von vielen Haltungen geteilt
(56 Profile für 102'317 Haltungen), ein fehlendes Profil verhindert das Wiedererkennen
der Haltung nicht.

### Geänderte Dateien

| Datei | Änderung |
|---|---|
| `Application/Import/ImportBestandsbilanz.cs` | **neu** — Bilanz und Zähler |
| `Application/Lookup/GeonisVerbund.cs` | **neu** — die drei Zustände |
| `Application/UseCases/KatasterKennungPlanBuilder.cs` | `AbgleichMoeglich`, `NurNeuExport`, `Ungeklaert` |
| `Application/UseCases/KatasterKennungAnwender.cs` | Berichtsblock „Was das für den GEONIS-Weg bedeutet" |
| `Application/Import/IOneClickProjectImportService.cs` | additiv `Bestand` |
| `Infrastructure/Import/ProjectImportOrchestrator.cs` | Abbruch an vier Schrittgrenzen, Bilanz am Schluss |
| `Infrastructure/Import/OneClickImportReportWriter.cs` | Block „Bestand nach dem Import" |
| `UI/Services/ImportOneClickProjectController.cs` | Abbruchsignal durchgereicht, eigener Abbruchzweig |
| `UI/ViewModels/Pages/ImportPageViewModel.cs` | eigene `CancellationTokenSource`, Knopf scharf |

### Tests

Neu: `ImportRestpunkteTests` (9, Infrastructure) und `ImportAbbruchArchitectureTests`
(4, UI).

**Vorheriges Scheitern belegt** — jeder Fix einzeln abgeklemmt:

| Abgeklemmt | Rote Tests |
|---|---|
| die fünf Abbruchprüfungen im Orchestrator | 1 von 9 |
| `Bestand` am Ergebnis | 1 von 9 |
| der GEONIS-Block im Bericht | 1 von 9 |

Alle vier Suiten grün: Infrastructure 6114, UI 6324, Pipeline 2558,
ProjectModernizer 62; 12 übersprungen, 0 Fehler.

### Was offen bleibt

- Der Abbruch wirkt an Schrittgrenzen, nicht **innerhalb** eines laufenden Kopiervorgangs.
  Eine sehr grosse Einzeldatei wird noch fertig kopiert. Das ist bewusst so: Ein Abbruch
  mitten im Schreiben hinterliesse eine halbe Datei.
- Die Bestandsbilanz vergleicht nicht mit einer Sollzahl — es gibt keine belegte Quelle
  dafür, wie viele Videos ein Projekt haben *muss*.

### Nächstes

AP11 — Gesamtprüfung und Übergabe.

---

## AP11 — Gesamtprüfung und Übergabe (05.09.2026)

### Release-Weg

| Schritt | Ergebnis |
|---|---|
| `dotnet build AuswertungPro.Dev.slnf -c Release` | 0 Warnungen, 0 Fehler |
| `dotnet build AuswertungPro.sln -c Release` | alle Projekte übersetzen; **eines** konnte seine DLL nicht schreiben |
| Infrastructure.Tests | 6116 grün, 6 übersprungen |
| Pipeline.Tests | 2558 grün, 3 übersprungen |
| UI.Tests | 6324 grün, 3 übersprungen |
| ProjectModernizer.Tests | 62 grün |

Der eine Build-Fehler ist kein Übersetzungsfehler: `SewerStudio.McpServer.dll` ist vom
laufenden MCP-Server dieser Sitzung gesperrt (MSB3021/MSB3027, „.NET Host (32172),
(16156)"). Der Prozess wurde nicht beendet. Gegenprobe: dasselbe Projekt in einen
Temp-Ordner gebaut — **0 Warnungen, 0 Fehler**. Kein anderes Projekt meldet einen Fehler.

### Die 52 Ausgangsordner, erneut rein lesend geprüft

Gelaufen ist der echte `KanalExportDetectionService` über jeden Ordner; nichts wurde
kopiert oder verändert.

| Erkennung | Ordner |
|---|---|
| WinCan | 21 |
| IKAS | 9 |
| IBAK | 3 |
| KINS | 1 |
| **nicht erkannt** | **18** |

Für jeden der 18 nennt die Erkennung jetzt einen konkreten Grund — das war der Auftrag:

| Grund | Ordner |
|---|---|
| WinCan-VX-`.sdf` (SQL Server Compact, unter .NET nicht lesbar) **und** XTF im alten Modell `VSA_KEK` | 15 |
| nur XTF im alten Modell `VSA_KEK` | 2 |
| gar kein Signal | 1 |

Der eine ohne Signal ist `Erstfeld\2023`: Er enthält **genau eine Datei**, eine
`Thumbs.db` (Windows-Bildvorschau) tief in einer leeren WinCan-Ordnerstruktur. Damit
bleiben exakt die im Plan genannten **17 datenhaltigen unerkannten Ordner** — und keiner
davon ist mehr ein „kein Signal gefunden".

**Wichtig für die Übergabe:** Diese 17 sind nicht technisch unlesbar. Alle 17 tragen eine
XTF im alten Modell `VSA_KEK` mit echten Inspektionsdaten (Zone 6.17 zum Beispiel
68 Untersuchungen, 341 Kanal- und 131 Schachtschäden), und der in AP3 gebaute Leser liest
sie. Sie sind ausgeschlossen, weil das alte Modell auf dem Ein-Knopf-Weg bewusst nicht
freigegeben ist — Entscheid Pascal vom 05.09.2026, alte Systeme sind aus dem Auftrag.
Wird die Entscheidung später umgedreht, ist der Weg vorhanden.

### Alte Zonen, lesend gemessen (formal ausserhalb des Auftrags)

Gelaufen sind nur die Leser, nicht der Orchestrator: Der würde die Quelle ins Projekt
archivieren und Gigabyte kopieren.

| | Andermatt Zone 2.11 | Erstfeld Zone 6.17 |
|---|---|---|
| XTF-Dateien im Ordner | 3 | 2 |
| davon gelesen | **1** | **1** |
| Haltungen | 23 | 32 |
| Schächte | 23 | 31 |
| Befunde | 209 | 341 |
| Schachtschäden | 121 | 131 |
| Fehler | 0 | 0 |

Die Abnahmematrix verlangt für Zone 2.11 drei Dinge, alle drei erfüllt:

- **Schacht 3133: genau ein Datensatz mit genau acht Schäden**
  (`DACB, DABBC, DAFCE, DABBC, DBCC, DAFCE, DABBC, DBCC`).
- **Mehrere Quellen zählen nicht als zusätzliche Bauwerke.** Von drei Exporten derselben
  Zone wird einer gelesen; die zwei anderen sind als vollständige Teilmenge belegt und
  werden übersprungen. Die früheren falschen 47 Haltungseinträge treten nicht auf.
- **Doppelt begangene Schächte werden nicht stillschweigend verrechnet.** Schacht 2200
  wurde zweimal begangen; die zweite Begehung liegt als Revision ab, mit dem Satz
  „Welche Begehung gilt, muss von Hand entschieden werden."

Nicht zugeordnete Untersuchungen werden namentlich gemeldet statt geraten: Zone 2.11
eine (`2204`), Zone 6.17 vier — jeweils mit dem Grund „weder Bauwerksverweis noch
Schäden, Punkte oder bekannte Erfassungsart".

### Aktuelles WinCan-DB3-Projekt: Göschenen GEP 2026

| Grösse | Wert |
|---|---|
| Haltungen | 260 |
| Schächte | 443 |
| Befunde | 1772 |
| Haltungen mit Video | 239 |
| Gegeninspektionsvideos | 2 |
| Fehler | 0 |

**339 Untersuchungen ergeben 260 Haltungen.** Die Differenz sind 69 Haltungen mit
mehreren Untersuchungen; jede übersprungene steht namentlich im Bericht mit Datum und
Befundzahl („Uebernommen: 01.09.2026; uebersprungen: 21.08.2026 mit 0 Befunden").

### Fachliche Unklarheit — bitte entscheiden

In Göschenen 2026 melden **59 Videos** dieselbe Widersprüchlichkeit:

> Rolle ungeklaert — Widerspruch: Kamerarichtung `gegen_Fliessrichtung`, Dateiname ohne
> Gegenmarke

Die Regel aus AP5 rät hier bewusst nicht. Nachgemessen: **kein einziges dieser Videos ist
verloren gegangen** — 58 der 59 tragen nachweislich ihr Hauptvideo (die 59. unter ihrem
umbenannten Haltungsnamen `10448-10407`), offen bleibt nur die Gegenrolle. Genau das
verlangt die Abnahmematrix mit „Teilzuordnung mit sichtbarem offenen Gegenbefund".

Die offene Frage ist fachlich, nicht technisch: Nimmt dieser Unternehmer standardmässig
gegen die Fliessrichtung auf — dann sind das 59 normale Hauptbefahrungen und die
Kamerarichtung ist kein Gegenbeleg. Oder sind es echte Gegenbefahrungen, deren Dateien
nur nicht gekennzeichnet sind. Ohne diese Auskunft wird nichts entschieden.

### Ein Fehler gefunden und behoben: der zweite Import legte jedes Video doppelt ab

Beim vollständigen Ablauf am künstlichen Projekt fiel auf, was in keinem Teiltest
sichtbar war. Zweiter identischer Import desselben Ordners:

```text
Lauf 1: Link = Haltungen_Verteilt/1000-2000/20260304_1000-2000.mpg
Lauf 2: Link = Haltungen_Verteilt/1000-2000/20260304_1000-2000_1.mpg
```

Ursache: `VerteileQuellvideo` in `KanalImportDistributionService` suchte über `UniquePath`
immer einen freien Namen, statt eine **inhaltlich identische** Zieldatei
wiederzuverwenden. Damit verstösst genau diese Stelle gegen die Hausregel, die überall
sonst gilt (Fotozuordnung, Portabilität, Importarchiv, Plan-PDF, Dichtheitsverteilung und
der Staging-Weg machen es seit je richtig). Behoben mit demselben `FileContentComparer`
wie die Dichtheitsverteilung: gleicher Inhalt heisst wiederverwenden, abweichender Inhalt
bekommt weiterhin einen freien Namen. Eine Wiederverwendung zählt nicht als „verteiltes
Video" — der Link wird trotzdem auf den Projektpfad repariert.

### Vollständiger Ablauf am kleinen künstlichen Projekt

Neuer Test `ProjektortwechselTests` (2), ausschliesslich künstliche Daten:

1. Ein-Knopf-Import einer kleinen VSA-KEK-XTF mit zwei Haltungen, drei Befunden und
   Videoverweisen → 0 Fehler, beide Videos im Projekt.
2. Die Kundenquelle bleibt bei 4 Dateien — sie wird nur gelesen.
3. Speichern, neu laden → alle Links relativ und auflösbar.
4. Den ganzen Projektordner an einen anderen Ort kopieren, dort laden → alle Links
   weiterhin auflösbar, Bestandsbilanz identisch.
5. Zweiter identischer Import → keine zusätzliche Datei, kein zusätzliches Bauwerk.

**Vorheriges Scheitern belegt:** Ohne die Wiederverwendungsregel ist Punkt 5 rot.

### Gegeninspektion und GEONIS-Herkunft gegen die Negativfälle

| Negativfall | Wo geprüft |
|---|---|
| zwei identische Videokopien → eine Aufnahme | `MedienKopienTests` (10) |
| zwei Aufnahmen gleicher Richtung → keine automatische Gegenbefahrung | `BefahrungsrollenTests` (13) |
| Hauptvideo klar, Gegenkandidaten widersprüchlich → offener Gegenbefund | `BefahrungsrollenTests`, real belegt in Göschenen (59 Fälle) |
| Haupt-/Gegenvideo ohne PDF → beide Links gültig | `GegenvideoVerteilungTests` (6) |
| lokale Kennung + eindeutiger Katastertreffer → keine Blockade | `KatasterKennungHerkunftTests` (20) |
| bestätigte fremde GEONIS-Kennung → geschützt und gemeldet | `KatasterKennungHerkunftTests` |
| unklare Herkunft + Widerspruch → Prüffall statt stiller Übernahme | `KatasterKennungHerkunftTests` |
| Neu-Export ist kein Abgleich | `ImportRestpunkteTests` |

### Externe Abnahme: Haltung 78998–79002 und Schacht 78998

Vorbereitet und durch Tests belegt sind:

- **Gezielte Werteänderungen an den Original-TIDs** — `XtfRevisionPlanBuilderTests` (14),
  `XtfRevisionWriterTests` (26).
- **Keine unbeabsichtigten Neuanlagen** — stabile `chSST`-Kennungen aus Projekt-Id,
  Klasse und fachlichem Schlüssel; `XtfNeuPlanBuilderGeonisTests` (6) prüft, dass
  vorhandene GEONIS-Kennungen als TID hinausgehen.
- **Unbeteiligte Objekte unverändert** — der Revisionsschreiber wendet nur den geprüften
  Plan an; ein von mehreren Haltungen geteiltes Rohrprofil bleibt unangetastet.
- **Rundreise** — `XtfRundreiseTests` schreibt Haltung 78998–79002 samt Schacht 78998 neu
  und liest sie zurück.

**Offen und ausdrücklich nicht behauptet:**

- **Konflikt bei neuerem GEONIS-Stand gibt es noch nicht.**
  `GeonisKennungen.GeonisGeaendert` trägt zwar das GEONIS-Änderungsdatum als
  Ausgangsstand, aber ein Änderungsmanifest mit Ausgangswerten je Objekt existiert nicht.
  Der Neu-Export ist ein Voll-Export. Wer währenddessen in GEONIS arbeitet, wird nicht
  erkannt.
- **Es gibt keine INTERLIS2→GEONIS-Abgleichsschnittstelle.** Andreas bestätigt eine valide
  Datei; FME im UPDATE-Modus ist ein Vorschlag, keine vorhandene Lösung. Ohne gemeinsamen
  Test mit der Gegenseite bleibt der Rückweg **offen**.
- In keiner echten GEONIS-Datenbank wurde geschrieben.

### Grenzen dieser Prüfung

- Die Kundenprüfungen liefen **nur mit den Lesern**, nicht über den Orchestrator: Der
  archiviert die Quelle ins Projekt und hätte Gigabyte kopiert. Verteilung und
  Protokoll-Split sind an diesen Ordnern also nicht gemessen, sondern nur an künstlichen.
- Erkannt heisst nicht vollständig importiert. Die 34 erkannten Ordner sind hier nur auf
  Erkennung geprüft; vollständig durchgelaufen sind Göschenen 2026 (lesend) und die
  künstlichen Beispiele.
- Der Abbruch wirkt an Schrittgrenzen, nicht innerhalb eines laufenden Kopiervorgangs.
- Kein Ordner unter `D:\Videoprojekte` wurde verändert, verschoben oder umbenannt.

### Testnachweis

Alle vier Suiten grün: Infrastructure **6116**, UI **6324**, Pipeline **2558**,
ProjectModernizer **62** — zusammen 15'060 Tests, 12 übersprungen, **0 Fehler**.

Neu in AP11: `ProjektortwechselTests` (2).
