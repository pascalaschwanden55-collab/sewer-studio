# Vorhandene Bildhinweise beim Codieren erhalten

Erkennt der Bildklassifikator eine Schadensgruppe, bleibt diese jetzt im abschliessenden KI-Status sichtbar:

> Bildhinweis: Einragendes Dichtungsmaterial – bitte prüfen

Der Klartext stammt aus dem aktiven VSA-Katalog. Die Anzeige ist gelb. Der Hinweis steht am Textanfang, weil der vorhandene Statusbalken lange Texte kürzt. Der vollständige Text erscheint beim Darüberfahren mit der Maus. Die Details erklären, dass Position, Uhrlage, Ausmass und Untercode damit noch nicht bestimmt sind. Der Prozentwert ist als Modellwert bezeichnet; er ist keine gemessene Trefferwahrscheinlichkeit.

Ohne lokalisierte Detektion erscheint bei einem solchen Hinweis kein grünes «Kein Schaden erkannt». Technische Fehler haben weiterhin Vorrang. Der Hinweis legt keine Protokollzeile an und verändert keine DINO-/SAM-Maske. Die bisherigen Regeln für sichtbare Segmentbefunde, Abstand und Detektorqualifikation bleiben bestehen.

## Belegter Anlass

Im historischen CodingReplay-Lauf `lauf-20260920-133809-24a1dffd` waren sieben von zwölf Bildern auswertbar. Der Klassifikator traf bei fünf dieser sieben Bilder bereits die Referenz-Hauptgruppe. Trotzdem entstand kein passender Ereigniscode. Seine Schadensgruppen gelangten nicht in die Struktur-/Grenzcode-Wege; die weitere Ereignisbildung verwendete ausschliesslich lokalisierte DINO-/SAM-Befunde.

Das fehlende automatische Ereignis ist ohne passende Lokalisation fachlich begründet. Der Bildhinweis soll jedoch für die manuelle Prüfung erhalten bleiben. Daher werden weder Klassifikatorcodes an beliebige Masken gebunden noch neue Untercodes aus Bildvermutungen erzeugt.

Der begrenzte Claude-Opus-Prüfauftrag bestätigte den Datenfluss. Zwei Aussagen seiner Antwort werden nicht übernommen: Die Qualität der Masken wurde nicht bildweise bewiesen; eine sichere Ursache allein im Detektor wurde nicht nachgewiesen. Sein vorgeschlagener früher Anzeigeort würde ausserdem durch den späteren Ergebnisstatus überschrieben. Die Umsetzung erfolgt deshalb am abschliessenden Ergebnisstatus.

## Umsetzung und Prüfung

Die reine Hinweiserzeugung liegt in `Application/UseCases/CodingClassifierHint`. Der bestehende UI-Workflow ergänzt die Anzeige. Gespeicherte Formate und öffentliche Workflow-Verträge bleiben gleich. Keine zusätzlichen KI-Anfragen und kein neues Training.

Ein vorher fehlschlagender Verhaltenstest zeigte die grüne Leeranzeige trotz aufgelöster Schadensgruppe. Neue Tests schützen die gelbe Prüfaufforderung, technische Fehler, fehlende Masken, vorausliegende Befunde, unveränderte Segmentobjekte und Ereignisaktionen sowie ungültige Modellwerte. Bestehende Struktur- und Grenzereigniswege bleiben separat.

Die Änderung erhöht die Sichtbarkeit bereits vorhandener Hinweise. Sie belegt keine höhere automatische Ereigniserkennung. Dafür braucht es anschliessend einen Vergleich vollständiger Videoabschnitte mit ihren geprüften Ereignissen.

Prüfstand: Pipeline 2 785 bestanden/3 übersprungen, UI 7 359 bestanden/30 übersprungen,
jeweils keine Fehler. Nach der abschliessenden Textanordnung 27 betroffene Tests
erneut bestanden. Schneller Release-Build erfolgreich; die bestehende CS8604-Warnung
in `VsaFotoAblageTests.cs:81` bleibt. Architektur-Skill validiert. Keine direkte
Fensterprüfung, da die Windows-Fenstersteuerung nicht verbunden war.

## Trainingsbestand separat geprüft

Neben dem älteren Export mit 852 Bildern existiert bereits ein neuerer Export `61370615b1c1…` mit 1 359 Bildern: 1 101 zum Lernen, 258 zur Validierung. Darunter liegen 286 Bilder ohne Markierungen; der Export bindet die geprüften Negativquellen. Der vorhandene Gold-Trainer bestätigte heute die technische Verwendbarkeit mit 1 206 Boxen in 13 belegten Klassen. Es wurde kein Training gestartet.

Zehn vorhandene Detect-Exporte, 18 Kandidatenmanifeste und neun Prüfquellen wurden abgeglichen. Der Entwicklungsbenchmark und seine Erweiterung teilen die Haltung `34738–34741` mit zwei neueren Trainingsexporten; die geprüften Bilddateien selbst sind verschieden. Dieser Benchmark ist bereits als Entwicklung markiert und darf nicht als frische unabhängige Abnahme ausgegeben werden. Historische Klassifikator-, Qwen- und OSD-Trainingsbestände sind mit diesem Detect-Abgleich noch nicht vollständig untersucht.

Maschinenlesbare Belege, Quellprüfsummen und Claudes Antwort liegen unter
`C:\Users\Besitzer\Documents\SewerStudio-KI-Strategie-20260920\Arbeit-20260920-Erkennung`.
