# Unabhängige Nachprüfung der optimierten Nova-Version

Stand: 6. September 2026

Die optimierte Version ist deutlich besser. Der unverändert wiederholte Prüflauf besteht alle 62 Punkte. Sechs zusätzliche Gegenbeispiele zeigen jedoch offene Fehler und eine zu weit gefasste Kontrastaussage. Vor dem WPF-Einbau sollten diese Punkte geschlossen werden.

Unabhängig bestätigt: passende Werte nach normalem Datensatzwechsel, getrennte Feldsuchen, Speichern im Normalfall samt Neuladen, richtige Summen, Rückkehr aus der Codierung mit gleicher Videoposition und eine stillstehende Hintergrundgrafik im ruhigen Modus. Die Liste zeigt bei den drei vorgegebenen Fenstergrössen 7, 9 und 12 vollständige Zeilen.

## Sechs offene Punkte

### R01 · Priorität 1 · Player startet trotz abgebrochenem Wechsel

**Gegenprobe:** Haltung 07.6588-6587 wählen, Baujahr ändern, zur Übersicht gehen und „Nächste Haltung prüfen“ anklicken. In der Rückfrage „Abbrechen, hier bleiben“ wählen.

**Tatsächlich:** Die Auswahl bleibt auf 07.6588-6587, aber der Player für 78998-79002 ist bereits geöffnet. Vor dem Abbruch sind Rückfrage und Player gleichzeitig offen; der Fokus liegt im Player hinter der Rückfrage.

**Korrektur:** Den vollständigen Wechsel samt Player erst nach Speichern oder Verwerfen ausführen. Abbrechen beendet die ganze angeforderte Aktion. Dasselbe Muster auf die KI-Aufgabenliste anwenden.

**Fertig, wenn:** Bei Abbrechen bleiben Auswahl und Entwurf erhalten, der neue Player bleibt geschlossen. Bei Zustimmung gehören Auswahl, Formular und Player zur gleichen Haltung.

**Code und Nachweis:** SewerStudio-Nova-Optimiert.html:1413, 1419–1421, 1432–1444. JSON: wechselVorAbbruch / wechselNachAbbruch.

### R02 · Priorität 1 · Freigabe bleibt nach Änderungen gültig

**Gegenprobe:** Training Studio öffnen und die vorhandenen Angaben akzeptieren. Danach VSA-Code und Beschreibung leeren. „Für Training freigeben“ anklicken.

**Tatsächlich:** Die Freigabe bleibt aktiv und meldet Erfolg trotz leerer Pflichtfelder. Die Bestätigung bezieht sich damit nicht mehr auf die aktuellen Angaben. Es wurde dabei kein echtes Trainingssample geschrieben.

**Korrektur:** Bestätigung an einen bestimmten Stand der Felder binden. Jede fachliche Änderung hebt sie auf. Auch die Freigabe selbst muss aktuelle Pflichtfelder und Bestätigung erneut prüfen.

**Fertig, wenn:** Änderungen an Code, Beschreibung, Uhrlage, Stufe oder der zugehörigen Bildauswahl sperren die Freigabe bis zur erneuten Bestätigung.

**Code und Nachweis:** SewerStudio-Nova-Optimiert.html:1702, 1718–1720. JSON: training.

### R03 · Priorität 1 · Speicherfehler wird als Erfolg gemeldet

**Gegenprobe:** Baujahr ändern. Im isolierten Prüf-Browser beim Schreiben einen QuotaExceededError auslösen und „Speichern“ anklicken. Die Seite neu laden.

**Tatsächlich:** Die Meldung behauptet, der Wert sei im Browserspeicher abgelegt. Die Änderungsmarke verschwindet. Nach Neuladen steht wieder das alte Baujahr 1978 statt 2002. Dies war eine kontrollierte Fehlereinspeisung; der echte Browserspeicher des Anwenders wurde nicht gefüllt.

**Korrektur:** Speichern muss Erfolg oder Fehler zurückmelden. Bei Fehler den Entwurf und die Änderungsmarke erhalten. „Speichern und wechseln“ darf dann nicht wechseln.

**Fertig, wenn:** Ein erzwungener Speicherfehler zeigt eine verständliche Meldung. Die Eingabe bleibt verfügbar und ein späterer erfolgreicher Versuch funktioniert.

**Code und Nachweis:** SewerStudio-Nova-Optimiert.html:1062–1064, 1280–1284, 1442–1450. JSON: speicherFehler.

### R04 · Priorität 2 · Tastenkürzel verlässt den offenen Dialog

**Gegenprobe:** Player öffnen und Strg+K drücken.

**Tatsächlich:** Der Fokus springt in die globale Suche hinter dem Player. Die Tab-Begrenzung schützt nur die erste und letzte Position innerhalb des Dialogs; dieses Tastenkürzel umgeht sie.

**Korrektur:** Globale Aktionen bei einem offenen modalen Fenster passend sperren oder innerhalb des Fensters anbieten. Nur die oberste Dialogebene darf den Fokus erhalten.

**Fertig, wenn:** Strg+K und Tab verlassen weder Player noch Codierfenster. Nach Schliessen kehrt der Fokus an eine sichtbare, passende Stelle zurück.

**Code und Nachweis:** SewerStudio-Nova-Optimiert.html:1293–1334. JSON: dialogSuche.

### R05 · Priorität 2 · Kontrastziel ist noch nicht überall erreicht

**Gegenprobe:** Helle Diagnose sowie helles und dunkles Training Studio öffnen. Die dunkle Codierung mit leerem Feld „Meter Ende“ prüfen.

**Tatsächlich:** Gemessen: „INFO“ hell 3,31:1, „WARN“ hell 4,05:1; „SAM-Maske“ hell 3,52:1 und dunkel 1,81:1; „Hand-Box“ dunkel 3,05:1. Der Platzhalter „nur Streckenschaden“ liegt dunkel bei 3,44:1. Diese Texte sind 11–13 Pixel gross.

**Korrektur:** Textfarben auf den farbigen Marken je Thema abstimmen. Diagnosefarben und Platzhalter anpassen. Den Test auf alle Seiten, Arbeitsfenster und Eingabefelder ausweiten.

**Fertig, wenn:** Die genannten normalen Texte erreichen mindestens 4,5:1. Messungen berücksichtigen tatsächliche Hintergründe und Platzhalter. Verläufe werden gesondert geprüft.

**Code und Nachweis:** SewerStudio-Nova-Optimiert.html:209, 1000–1001; pruefung.js:176–193. JSON: kontrast.

### R06 · Priorität 1 · „Beenden ohne Übernahme“ behält den Befund

**Gegenprobe:** Player öffnen, ein neues Ereignis erfassen und im Codierfenster übernehmen. Danach im Player „Beenden ohne Übernahme“ wählen und den Player erneut öffnen.

**Tatsächlich:** Die Befundzahl bleibt bei vier statt ursprünglich drei. Der neue Befund steht weiter im gemeinsamen Datenbestand und erscheint beim erneuten Öffnen. Der Abbruch im Codierfenster selbst funktioniert; dieser Fehler betrifft den anschliessenden Player-Abbruch.

**Korrektur:** Player-Änderungen in einem eigenen Entwurf halten. Erst „Codierung übernehmen“ schreibt sie in den gemeinsamen Beispielbestand. „Beenden ohne Übernahme“ verwirft diesen Entwurf.

**Fertig, wenn:** Neue, bearbeitete und gelöschte Befunde sowie Prüfstatus werden beim Player-Abbruch zurückgesetzt. Bei Übernahme bleiben sie erhalten.

**Code und Nachweis:** SewerStudio-Nova-Optimiert.html:941, 1303–1310, 1705–1707. JSON: player_abbruch.

## Reihenfolge

1. Datensatzwechsel und Player-Entwurf absichern (R01, R06).
2. Speicherfehler und Trainingsbestätigung korrekt behandeln (R03, R02).
3. Dialogkürzel und Kontrast korrigieren; bisherigen Lauf und Gegenproben ausführen (R04, R05).
4. Danach die Haltungsseite als ersten begrenzten WPF-Einbau vorbereiten.

## Aussagekraft der Nachweise

Geprüft: Desktop-Datei und bytegleiche Projektkopie, Änderungsliste, Funktionsliste, Prüftabelle, Prüfskript und JSON. Der mitgelieferte Lauf wurde unverändert mit Chromium erneut ausgeführt. Eigene Gegenproben liefen auf einer bytegleichen lokalen Kopie in einem getrennten Browser. Zusätzlich wurden die anfangs sichtbaren Texte aller 15 Seiten und drei Arbeitsfenster in Hell und Dunkel gemessen. Nicht alle Scrollpositionen, Zustände oder Schaltflächen wurden vollständig geprüft. Dies ist keine neue Vollprüfung von SewerStudio.

Der vorhandene Kontrasttest betrachtet nur sechs Seiten. Arbeitsfenster und Eingabewerte fehlen im Auswahlmuster. Der eigene ergänzende Test nimmt auch Eingaben und Platzhalter auf. Hintergrundverläufe werden als unsicher markiert und nicht als nachgewiesener Kontrastfehler gewertet. Die oben genannten Befunde verwenden einfache, deckende Farbpaare. Grosse Kennzahlen auf Import- und Dossierseite liegen zwar unter dem pauschalen Projektziel 4,5:1, aber über der WCAG-Grenze 3:1 für grossen Text; sie sind deshalb hier kein eigener WCAG-Verstoss.

Für normalen Text gilt als Massstab [W3C: mindestens 4,5:1](https://www.w3.org/WAI/WCAG22/Understanding/contrast-minimum.html). Für modale Fenster beschreibt das [W3C-Dialogmuster](https://www.w3.org/WAI/ARIA/apg/patterns/dialog-modal/) die Begrenzung und Rückkehr des Tastaturfokus.

Die zwei ursprünglichen Desktop-Dateien sind weiterhin unverändert; die optimierte Datei wurde durch diese Nachprüfung ebenfalls nicht geändert. Desktop- und Projektkopie der optimierten Version sind identisch. Im gelieferten Nachweisordner liegen 14 PNG-Bilder, nicht 16. Das entspricht zwölf hellen Bildern und zwei dunklen Bildern. Die dokumentierten Grenzen zu Windows-Skalierung, Screenreader und echten Daten sind angemessen.

Die optimierte Datei hat SHA-256 `658352c260dc21fd1e716068d8a76d00b27853244c566d03573c33d75885c1fe`.

[Rohergebnisse der Gegenprüfung](nachweise/gegenpruefung.json) · [Wiederholter Originaltest](nachweise/wiederholung-originalpruefung.json) · [Dateiprüfung](nachweise/dateipruefung.json)

[HTML-Überblick](ueberblick.html) · [Fertiger Nachbesserungsprompt](CLAUDE-NACHBESSERUNG.md)
