# Status und Projektprüfung

Stand: 16.09.2026

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

| Bereich | Was wird geprüft? |
|---|---|
| Dateiverweise | Hinterlegte Video- und PDF-Pfade der Haltungen und Schächte: vorhanden, lesbar, nicht leer. Auch PDF-Listen werden aufgelöst. |
| Offene KI-Befunde | Nicht gelöschte, noch nicht bestätigte KI-Vorschläge im aktuellen Haltungsprotokoll. |
| Meterangaben | Negative/ungültige Meterwerte, umgekehrte Bereiche, fehlende gültige Haltungslänge bei Meterbefunden und Befunde hinter dem Haltungsende. Es gilt die bestehende Toleranz von 1 m. |
| Schachthöhen | Die vorhandenen Regeln zu Deckelhöhe, Sohlenhöhe und Tiefe, einschliesslich widersprüchlicher oder mehrdeutiger Angaben. |
| Eingabefelder | Im Feldkatalog belegte Pflichtangaben, Textlängen sowie Datums- und Zahlenformate der bearbeitbaren Objektfelder. Verbundene Unterakten werden einbezogen. |

Jeder Hinweis enthält das betroffene Objekt und **Zur Stelle**.
Der Knopf öffnet das betreffende Feld in der Objektakte oder markiert den Befund
im Protokollfenster. Ein schreibgeschützter Dateipfad wird über den vorhandenen
Medien-/PDF-Zuordnungsweg korrigiert.

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
- Null Hinweise sind keine fachliche Freigabe. Die vollständige XTF-Normprüfung bleibt beim Export.
- Das Ergebnis gilt für den angezeigten Stand und wird nicht als Prüfprotokoll gespeichert.

## Technischer Nachweis

Die Regeln liegen in `Application/UseCases/ProjektPruefung`. Der registrierte
`IProjektPruefung`-Dienst prüft Dateien lesend in Infrastructure. Das ViewModel
verwendet die bestehende Projektkopie und Inhalts-Signatur für den Hintergrundlauf.
Die gespeicherten Datenformate bleiben unverändert.

Verhaltenstests: `ProjektPruefungTests`, `ProjektPruefstatusTests`,
`ProjektPruefungViewModelTests`, `ProjektPruefungUiTests`,
`DataPageProtocolWindowControllerTests` und die bestehenden Status-/Übersichtstests.
