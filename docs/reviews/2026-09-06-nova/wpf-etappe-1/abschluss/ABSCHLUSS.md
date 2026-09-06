# Nova-Etappe 1 – Abschluss der Nachprüfung

Stand: 6. September 2026. Nutzeranforderung: **Full HD (1920 × 1080) ist das Minimum.**

Die Nachprüfung des zusammengeführten Nova-Stands ist abgeschlossen. Sechs dabei bestätigte Bedien-/Darstellungsprobleme sind korrigiert und mit Gegenproben geprüft. Dieser Bericht belegt die beschriebene Etappe, keine vollständige 9-von-10-Bewertung des Gesamtprogramms.

## Sicherung und Zusammenführung

Der offene Hauptbaum wurde vor dem Merge in einer SHA-256-geprüften ZIP gesichert (186 konkrete Dateien, darunter rekursiv aufgelöste neue Ordner) und als `eb62604d7` committet. Die früher genannten 83 Einträge sind daher keine verlässliche Dateianzahl dieses Sicherungslaufs. ZIP: `.tmp/nova-abschluss/sicherung-20260906-182750.zip`, SHA-256 siehe `sicherung.json`.

`feature/nova-etappe-1` bei `07477255b` wurde konfliktfrei in `feature/eval-pruefsatz-review` integriert. Ausgangsbasis war `69fd0a671`. CLAUDE.md enthält sowohl Audit-Paket 1 als auch Nova. Der abschliessende Merge-Commit enthält diese Korrekturen und Nachweise. Kein Push. Der externe Nova-Worktree bleibt erhalten.

Der Sicherungscommit lagerte die Mehrdeutigkeitsentscheidung von `HoldingFolderDistributor.FindVideo` in den bestehenden kleinen `VideoKopienAufloeser.LoeseTreffer` aus. Grund war die bestehende Grössengrenze der grossen Partial-Klasse. Vor dem Sicherungscommit bestand der vollständige Releaseweg separat.

## Nachgewiesene Korrekturen

1. **Auswahl klar erkennen:** Aktiver Chip und Tabellenzeile hatten im dunklen Thema zu wenig Kontrast. Beide Themes besitzen jetzt gemeinsame Auswahlfarben und eine sichtbare Kontur.
2. **Zustandsklasse lesbar:** Direkt erzeugte Textspalten verloren die vorgesehene Textfarbe beim Laden der Ausrichtung. Alle drei Spaltentypen übernehmen jetzt die Farbe ihrer Zelle.
3. **Menü vollständig erreichbar:** Bei 150 Prozent verschwanden die unteren Aktionen. Lange Kontextmenüs sind jetzt scrollbar; die letzte Aktion wurde erreicht.
4. **Bericht mit belegten Werten:** Das Hydraulik-PDF benutzte Panelwerte oder Ersatzwerte. Es prüft nun DN und Projektgefälle und nutzt das tatsächlich gespeicherte Gefälle.
5. **Fehlende Eingabe ergänzt:** Das bereits vorgesehene Projektgefälle fehlte als normales Eingabefeld. „Gefälle ‰“ steht jetzt immer in den Stammdaten bereit.
6. **Zeilen in beide Richtungen bewegen:** Nach einer Bewegung blieb der Gegenbefehl ausgegraut. Die Befehle werden nun bei einer Änderung der Liste sofort aktualisiert.

Die roten Gegenproben stehen in `testnachweise.zip`: `fachfehler-rot.trx` (acht Fehlschläge), `gefaellefeld-rot.trx` (zwei) und `standardfarbe-layout-rot.trx`. Der finale fokussierte Lauf hat 69 bestandene Tests und einen ausgelagerten Kindtest. Der isolierte Elterntest prüft per Receipt, dass der Kindprozess tatsächlich lief.

Fachliche Regel: Der Hydraulik-Bericht berechnet weiterhin Halbfüllung, nicht die letzte freie Panel-Eingabe. DN und Gefälle kommen aus der Haltung; Zustand/Materialersatz und Temperatur weiterhin aus Einstellungen. Keine unabhängige hydraulische Norm- oder Formelabnahme. Der bestehende Feldschlüssel und das JSON-Dictionary bleiben kompatibel; die feste CSV-/Excel-Spaltenfolge wird nicht erweitert. Keine neuen NuGet-Pakete, keine neue Dienstregistrierung.

## Vollständiger Releaseweg am Endstand

Solution-Build: **0 Fehler, 0 Warnungen**.

| Testprojekt | Bestanden | Übersprungen | Fehler |
|---|---:|---:|---:|
| AuswertungPro.Next.Infrastructure.Tests | 6170 | 6 | 0 |
| AuswertungPro.Next.Pipeline.Tests | 2562 | 3 | 0 |
| AuswertungPro.Next.UI.Tests | 6414 | 4 | 0 |
| ProjectModernizer.Tests | 62 | 0 | 0 |
| Gesamt | 15208 | 13 | 0 |

Alle vier Testprozesse Exitcode 0. Ausführung: `werkzeug/release.ps1 -Stand abschluss`. Wegen einer vom laufenden MCP-Dienst belegten DLL wurde dieselbe Solution mit `--artifacts-path` in einer eigenen Ausgabe gebaut. Kein laufender SewerStudio-/MCP-Prozess wurde dafür beendet. Pakete wurden zuvor aus den gesperrten Abhängigkeitsdateien wiederhergestellt; dieser Lauf ist keine neue Schwachstellenprüfung. Sidecar- und QGIS-Python-Code wurden in diesem Abschluss nicht verändert, deren separate CPU-Suiten hier nicht erneut ausgeführt.

## Bildschirm und Skalierung

Ein physischer Bildschirm mit 1920 × 1080. Die Prozentwerte wurden in Windows geändert und jeweils mit einem neu gestarteten Prüfhost kontrolliert. WPF misst logische Einheiten, einschliesslich unsichtbarer maximierter Fensterrahmen; daher sind die Roh-Fensterbreiten nicht identisch mit den physischen 1920 Pixeln.

| Probe | Thema / DPI | Volle Zeilen | Eingabefelder |
|---|---|---:|---|
| Full HD, maximiert, 100 % | dunkel / 96 | 14 von 14 | zu |
| Full HD, maximiert, 100 % | dunkel / 96 | 9 | offen |
| Full HD, 125 %, Fenster 1366,4 × 768 logisch | hell / 120 | 7 | zu |
| Full HD, maximiert, 150 % | dunkel / 144 | 5 | zu |
| Full HD, 150 %, Fokusmodus F11 | dunkel | 7 (Sichtprobe) | zu |

Die frühere 1366-×-768-Probe ist ein kleineres Fenster auf Full HD, keine zugesagte Mindest-Bildschirmauflösung. Bei 150 Prozent bietet F11 mehr Platz. Sieben Zeilen werden bei 150 Prozent im normalen Modus ausdrücklich nicht behauptet. Messungen gelten ohne vorübergehende Statusleiste nach Speichern, mit Standard-Zeilenhöhe und Standard-Tabellenzoom. Windows steht wieder auf **100 Prozent**, siehe `bilder/windows-100-zurueck-0.png`.

Auswahltext und Kontur wurden in beiden Themes per Kontrasttest geprüft (Text mindestens 4,5:1, Kontur mindestens 3:1 gegen Auswahlfläche). Das ist keine vollständige WCAG-Prüfung aller Dialoge. Helle und dunkle Zustandsziffern wurden nach Korrektur angesehen. Der zusätzliche letzte Standardspalten-Fix wurde danach bei 100 Prozent dunkel und durch isolierte Render-Tests beider Themes bestätigt.

## Alle Einträge unter „Weitere Aktionen“

Alle aufgeführten Aktionen wurden ausgelöst. „Leerfall“ oder „Ersatzfall“ ist bewusst keine Behauptung eines vollständigen Fachablaufs. Nachweisnamen bezeichnen PNG/JSON in `bediennachweise.zip`.

| Aktion | Ergebnis | Tatsächlich geprüft | Nachweis |
|---|---|---|---|
| Medien suchen | Bestanden | Ein echtes künstliches MP4 gefunden und verknüpft; 13 Haltungen ohne Treffer. | `medien-verknuepfung-gespeichert` |
| Leere Felder aus QGIS | Bestanden | Ein leeres Materialfeld zu Steinzeug ergänzt. DN 300 und 13 vorhandene Beton-Werte erhalten. | `qgis-uebernommen` |
| Katasterkennungen | Bestanden | 14 Kennungen übernommen; 14 Haltungen nur für Neu-Export geeignet, kein vollständiger GEONIS-Verbund. | `kataster-uebernommen` |
| Strassen | Leerfall geprüft | Ohne zugehörige Schacht-Strassen verständliche Meldung; Übernehmen gesperrt. Kein positiver Übernahmelauf. | `strassen-ohne-schachtdaten` |
| Sanierungsmassnahme bearbeiten | Bestanden | Echte Massnahmenansicht der gewählten Haltung geöffnet und zur Liste zurückgekehrt. | `sanierung-bearbeiten` |
| Direkt zur KI-Optimierung | Ersatzfall geprüft | Dialog gestartet; KI nicht verfügbar ehrlich angezeigt, regelbasierter Ersatzvorschlag. Ohne Übernahme geschlossen. | `ki-direkt-ergebnis` |
| Vorschlag für diese Haltung | Leerfall geprüft | Ohne bewertete Vergleichsfälle erklärt das Programm, warum noch kein Vorschlag vorliegt. | `vorschlag-ergebnis` |
| Hydraulik berechnen | Bestanden | Wasserstand 90 auf 120 mm verändert: angezeigter Durchfluss 14,40 auf 24,71 l/s. Keine unabhängige Fachvalidierung der Formel. | `hydraulik-wasserstand-120` |
| Hydraulik PDF | Bestanden nach Korrektur | Ohne Gefälle gesperrt; nach Eingabe von 2,5 ‰ PDF erstellt und Wert im Bericht bestätigt. Halbfüllung bleibt 150 mm bei DN 300. | `hydraulik-final-erstellt` |
| Dossier | Bestanden | Zwei Seiten mit Deckblatt und Haltungsprotokoll erzeugt und visuell geprüft. Fehlende Abschnitte korrekt nicht verfügbar. | `dossier-erstellt` |
| Abdocken / Andocken | Bestanden | Tabelle in eigenes Fenster und zurück, Auswahl erhalten. Wechsel zwischen unterschiedlichen Monitoren nicht geprüft. | `tabelle-wieder-angedockt` |
| Nach oben / Nach unten | Bestanden nach Korrektur | Reihenfolge geändert; Gegenrichtung ohne erneute Auswahl sofort verfügbar. Oberste Grenze korrekt gesperrt. | `nach-oben-jetzt-aktiv` |
| Spalten anordnen | Bestanden | Aktiviert und Strasse per Ziehen hinter DN verschoben. | `spalten-umgeordnet` |
| Spalte leeren | Leerfall geprüft | Modus aktiviert, Rechtsklick auf Profilform: bereits leer korrekt erkannt. Meldung mit Escape geschlossen; keine gefüllte Spalte gelöscht. | `spalte-leeren-bestaetigung` |
| Zeilenhöhe | Bestanden | 38 auf 91 Pixel gestellt, sichtbare Höhenänderung, danach auf 38 zurück. | `zeilenhoehe-veraendert` |
| Tabellenzoom | Bestanden | 100 auf 118 Prozent gestellt, Tabelle vergrössert sich sichtbar. | `tabellenzoom-veraendert` |
| Ausrichtung (sechs Knöpfe) | Bestanden | Links, Mitte, rechts sowie oben, mittig, unten an der NR-Spalte ausgelöst und sichtbare Textlage geprüft. | `ausrichtung-vertikal-mitte` |
| Fokusmodus F11 | Bestanden | Bei 150 Prozent ein- und ausgeschaltet. Sieben statt fünf volle Zeilen sichtbar. | `fokusmodus-ein` |

Eine automatische Freigabeprüfung lehnte Enter in der Informationsmeldung zur bereits leeren Spalte vorsorglich als mögliche Löschbestätigung ab. Die Meldung wurde anschliessend mit Escape geschlossen. Kein Löschvorgang ist blockiert offen; ein Löschlauf auf gefüllten Spalten war für die ausgeführte Leerfallprobe nicht nötig und wird nicht behauptet.

## Isolation und Grenzen

Der Prüfhost lädt echte Produktdienste, MainWindow, Views und Bedienlogik, aber unterdrückt den produktiven `App.OnStartup`. Eigenes Projekt, Profil, Wissen, Katasterdateien und ein künstlicher Videoclip liegen vollständig unter `.tmp/nova-abschluss/bedienung`. Spiegel, QGIS-Brücke und KI-Start werden nicht gestartet. Die drei SQLite-Testquellen sind nach allen Aktionen bytegleich, SHA-256 siehe `datenpruefung.json`. Im Testprojekt bleiben 14 Haltungen, alle DN 300, 13 Beton und eine ergänzte Steinzeug-Haltung. Diese Quellen sind künstliche Tabellen, kein vollständiger QGIS-Geometriebestand.

Nicht neu belegt: produktiver Gesamtstart, vollständiger GEONIS-Rückabgleich, echte KI-Qualität, positive Strassenübernahme mit verknüpften Schächten, Löschen gefüllter Spalten, unterschiedliche Monitor-DPI, Screenreader. Die Neustart-Persistenz der Trennlinien stammt aus Claudes früherer Sichtprobe; der eigene Host initialisiert Einstellungen pro Start neu und wird dafür nicht als weiterer Nachweis ausgegeben. Die Originaldateien auf dem Desktop und der Nova-Worktree wurden in diesem Abschluss nicht bearbeitet.

Die Test-Projektübersicht scannt auch andere JSON-Dateien im künstlichen Prüfverzeichnis. Ihre Projektzahl ist deshalb keine fachliche Messung. Frühere Fehler im Aufbau des Prüfhosts sind nicht als Produktfehler gewertet. Historische Screenshots zeigen den damaligen Korrekturstand; massgeblich für den Code ist der vollständige finale Releaseweg.

## Weiterer Umsetzungsplan

1. **Etappe 2: Lesbarkeit in allen Fachdialogen.** Verbliebene helle Hervorhebungen, Checkboxen und PDF-Farben vereinheitlichen. Abnahme: benannte Dialogliste, beide Themes, 100/125/150 Prozent auf Full HD; Eingaben und Meldungen vollständig erreichbar.
2. **Hydraulik-Ablauf verständlicher machen.** Panel-Szenario und Halbfüllungsbericht mit eindeutigem Berechnungszweck und Quellenhinweisen darstellen. Vor einer Umstellung fachlich festlegen, welche Berechnung gedruckt werden soll; Referenzfälle von einer fachkundigen Person prüfen lassen.
3. **Vollständiges künstliches Netz bereitstellen.** Haltungen mit Schächten, Strassen, Geometrie und vollständigen Katasterverbünden aufbauen. Abnahme: positiver Strassenlauf, vollständiger Export und Rückimport in eine isolierte QGIS-/GEONIS-Testumgebung, Originalhashes unverändert.
4. **Gesamtfreigabe getrennt bewerten.** Produktiven Start in separatem Windows-Testprofil, zwei Monitore mit verschiedener Skalierung und Screenreader prüfen. KI mit dem freigegebenen Bewertungsdatensatz messen. Erst dann lässt sich ein belastbares Gesamturteil Richtung 9 von 10 abgeben.

Diese vier Punkte sind Vorschläge für Folgeetappen, nicht als bereits umgesetzt markiert.


## Prüfung des HTML-Berichts

Playwright/Chromium: 1920 × 1080 und 1280 × 720 ohne horizontales Seitenüberlaufen. Prüfliste aufgeklappt, 18 Aktionsgruppen vorhanden; keine fehlenden Bilder. Zehn lokale Dateilinks liefern HTTP 200. Startansicht und Gesamtseite visuell kontrolliert, Bilder in `html-pruefbilder.zip`. Die Datenarchive wurden vollständig zur Integritätsprüfung zurückgelesen. Die Seite benötigt keinen Internetzugriff.
