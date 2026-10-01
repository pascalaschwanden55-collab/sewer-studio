# Status und Projektprüfung

Stand: 29.09.2026

## Status richtig lesen

Der Sanierungsstand sagt nichts darüber aus, ob die Bearbeitung erledigt ist.
Die persönliche Erledigt-Markierung bleibt unabhängig davon gespeichert.

- **KI-Befunde zu prüfen:** Mindestens ein aktueller KI-Vorschlag ist noch nicht bestätigt.
  Das bleibt sichtbar, auch wenn die Haltung als erledigt markiert wurde.
- **Bearbeitung erledigt:** Persönlich als erledigt markiert und keine offenen KI-Befunde.
- **Bearbeitung offen:** Nicht als erledigt markiert und keine offenen KI-Befunde.

Die KI-Anzeige sagt bei ausschliesslich bestätigten Vorschlägen **bestätigt**.
Das ist keine vollständige fachliche Freigabe. Die Zustandsklasse bleibt eine eigene Angabe.
Die Übersicht zählt nach denselben Regeln. Erledigt-Markierungen aktualisieren die Anzeige sofort.

## Projekt prüfen

In der Projektübersicht **Projekt prüfen** anklicken. Die Prüfung liest eine Kopie
des aktuellen, auch ungespeicherten Projektstands. Sie verändert keine Projektdaten oder Originaldateien.
Die Kopie entsteht in kurzen Abschnitten; **Abbrechen** beendet den Lauf ohne
vollständiges Ergebnis. Die Befundliste bleibt auch bei vielen Hinweisen bedienbar.

| Bereich | Was wird geprüft? |
|---|---|
| Dateiverweise | Hinterlegte Video- und PDF-Pfade der Haltungen und Schächte: vorhanden, lesbar, nicht leer. Auch PDF-Listen werden aufgelöst. |
| Offene KI-Befunde | Nicht gelöschte, noch nicht bestätigte KI-Vorschläge im aktuellen Haltungsprotokoll. |
| Meterangaben | Negative/ungültige Meterwerte, umgekehrte Bereiche, fehlende gültige Haltungslänge bei Meterbefunden und Befunde hinter dem Haltungsende. Es gilt die bestehende Toleranz von 1 m. |
| Schachthöhen | Die vorhandenen Regeln zu Deckelhöhe, Sohlenhöhe und Tiefe, einschliesslich widersprüchlicher oder mehrdeutiger Angaben. |
| Eingabefelder | Im Feldkatalog belegte Pflichtangaben, Textlängen sowie Datums- und Zahlenformate der bearbeitbaren Objektfelder. Verbundene Unterakten werden einbezogen. |

Jeder Hinweis enthält das betroffene Objekt und **Zur Stelle**.
Der Knopf öffnet das betreffende Feld in der Objektakte mit Fokus auf dem Editor
oder markiert den Befund im Protokollfenster. Nach dem Protokollfenster führt der
Weg zurück zur Prüfung und wählt den bisherigen Hinweis. **Enter** öffnet den
gewählten Hinweis, **Alt+P** startet die Prüfung und **Escape** bricht sie ab. Ein
schreibgeschützter Dateipfad wird über den vorhandenen Medien-/PDF-Zuordnungsweg
korrigiert.

Nach einer Korrektur erneut **Projekt prüfen** anklicken. Änderungen oder ein
Projektwechsel verwerfen die alten Ergebnisse. Vor dem Öffnen eines Hinweises wird
der Projektstand nochmals verglichen. Ein abgebrochener oder fehlgeschlagener Lauf
wird nicht als vollständiges Ergebnis angezeigt.

## Grenzen dieser ersten Version

- Fehlende Dateiverweise werden nicht verlangt. Geprüft werden die tatsächlich hinterlegten Pfade.
- Die Dateiprüfung erkennt fehlende, nicht lesbare und leere Dateien; sie prüft nicht den gesamten Video-/PDF-Inhalt.
- Netzwerkpfade und Dateiverknüpfungen werden mit einem Hinweis als nicht prüfbar ausgewiesen.
  Relative Pfade benötigen einen gültigen Projektordner.
- Die Prüfung überwacht Dateien nicht dauerhaft. Nach externen Dateiänderungen erneut prüfen.
- Stille Änderungen ohne Datensatzmeldung werden beim Abschluss eines Prüflaufs und
  vor einem Sprung durch einen vollständigen Vergleich der tatsächlich geprüften
  Daten erkannt. Zwischen diesen Zeitpunkten kann eine bereits angezeigte Liste
  vorübergehend veraltet sein. Nicht synchronisierte Hintergrundschreiber sind
  durch den UI-Thread-Vergleich nicht atomar geschützt.
- Null Hinweise sind keine fachliche Freigabe. Die vollständige XTF-Normprüfung bleibt beim Export.
- Das Ergebnis gilt für den angezeigten Stand und wird nicht als Prüfprotokoll gespeichert.

## Technischer Nachweis

Die Regeln liegen in `Application/UseCases/ProjektPruefung`. Der registrierte
`IProjektPruefung`-Dienst prüft Dateien lesend in Infrastructure. Die neue
`ProjektPruefdatenKopie` kopiert in abbrechbaren UI-Abschnitten nur Werte, die
die fünf Regeln verwenden: aktuelle Haltungsprotokolle, Haltungs- und Schachtfelder,
Schacht-Handmarkierungen und GEONIS-Knoten sowie Objektaktenwerte, Bezüge und
Quellbelege. Ein anschliessender durchgehender Vergleich auf dem UI-Thread
verwirft gemischte Kopien; derselbe Vergleich läuft nach der Hintergrundprüfung
und vor **Zur Stelle**. Unbenutzte Metadaten und Protokollhistorien werden nicht
verglichen. Der neue Öffnungsweg löst Video-Pfade und persönliche Objektaktenlisten
vor dem Fenster im Hintergrund auf und bestätigt die Aktualität danach erneut.
Der bisherige synchrone `Open`-Aufruf und das Speicherverhalten bleiben erhalten.
Die gespeicherten Datenformate bleiben unverändert.

Ein synthetischer fokussierter Lauf mit 100 / 1'000 / 10'000 Schachtdatensätzen
ergab für die Kopie 0,1 / 0,5 / 8,2 ms Gesamtzeit. Der längste gemessene
Kopierabschnitt dauerte 0,0 / 0,1 / 5,3 ms, der durchgehende Vergleich
0,0 / 0,1 / 3,3 ms. Das ist ein Testlauf mit einfacher Pausenfunktion, keine
WPF-Frame- oder Referenzgeräte-Abnahme. Ein weiterer Test prüft 10'000 Befunde
in einer Haltung. Die 100-ms-Zielgrenze ist damit für die echte Oberfläche
noch nicht belegt.

Die isolierte Sichtprobe der Ergebnisliste zeigte Hell/Dunkel bei 550 und
1'100 DIP mit vollständig erreichbaren Bedienelementen. Das gebaute EXE-Manifest
enthält `asInvoker` und PerMonitorV2 mit PerMonitor-Rückfall. Auf dem Testgerät
waren zwei Monitore nur bei 96 DPI verfügbar; ein echter DPI-Monitorwechsel,
Narrator und Windows-Kontrastumschaltung sind nicht geprüft.
Persönliche Einstellungen liegen standardmässig unter `LocalAppData`; der
bestehende ausdrückliche Override `SEWERSTUDIO_APPDATA_DIR` bleibt möglich.

Verhaltenstests: `ProjektPruefungTests`, `ProjektPruefdatenKopieTests`,
`ProjektPruefstatusTests`, `ProjektPruefungViewModelTests`, `ProjektPruefungUiTests`,
`DataPageProtocolWindowControllerTests` und die bestehenden Status-/Übersichtstests.
