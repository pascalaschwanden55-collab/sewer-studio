# Eingabefelder selbst anordnen – 08.09.2026

In der aufgeklappten Haltungs- und Schachtzeile gibt es jetzt **Ansicht anpassen**.
Im Anpassungsfenster:

1. Ein Feld an seiner Beschriftung an die gewuenschte Stelle ziehen, auch in eine andere Spalte.
2. Ganze Spalten an ihrer Ueberschrift verschieben.
3. Unwichtige Felder mit dem Kreuz ausblenden. Unten bei **Ausgeblendete Felder** wieder einblenden.
4. **Speichern** uebernimmt die Ansicht. **Abbrechen** oder Schliessen verwirft die Vorschau.
5. **Standard wiederherstellen** setzt die Vorschau zurueck; erst Speichern uebernimmt das.

Haltungen und Schaechte verwenden getrennt die vorhandenen Einstellungen
`DataPageLayout.DetailLayout` und `SchaechtePageLayout.DetailLayout`. Die Auswahl gilt
fuer alle Datensaetze der jeweiligen Liste und bleibt nach einem Neustart erhalten.
Leere, komplett ausgeblendete Themen belegen keine Spalte. Ausgeblendete Werte
werden weder geloescht noch aus Exporten entfernt.

`AufklappLayoutWindow` verwendet die vorhandene Verschiebe-/Ausblendefunktion von
`RecordDetailsView` mit losgeloesten, schreibgeschuetzten Vorschaukarten.
`AufklappDetailLayout` kopiert nur Beschriftung, Feldschluessel und Anzeigewert;
keine Schreib- oder Nachschlagebefehle werden in die Vorschau uebernommen.
Die Listencontroller wenden das gespeicherte Layout beim Formularaufbau an.
Der bestehende Live-Abgleich bleibt an den originalen Feldobjekten angeschlossen.
Die neuen Ereignisabos werden beim Entladen entfernt und beim Wiederladen erneuert.

Dokumentfelder werden in diesen Listen als normale Felder angezeigt, damit eine
dorthin verschobene Karte nicht vom Dokumentfilter der alten Detailansicht
verschluckt wird. Fachliche Sichtbarkeitsregeln, etwa bei Sanierungsfolgefeldern,
gelten weiterhin; in der Gestaltungsvorschau sind auch solche Felder anordenbar.

Nachweise: `AufklappDetailLayoutTests`, `AufklappLayoutBedienTests`, vorhandene
RecordDetail-/Aufklapp-/Architekturtests. Die isolierte Bedienprobe auf echten
Seiten prueft Ausblenden, Verschieben, Speichern und erneutes Oeffnen ohne
Aenderungen an Projektdaten. Lokale Bilder und Ergebnisse stehen unter
`.tmp/kompakte-details/profil/` und `.tmp/layout-anpassen/`.

Abschluss: Debug-Build erfolgreich, 0 Warnungen und 0 Fehler. Vollstaendiger
UI-Testlauf des Abschlussstands: **6876 bestanden, 18 uebersprungen, 0 Fehler**.
Die uebersprungenen Eintraege sind die separaten WPF-Kindprozess-Einstiegspunkte;
ihre Eltern-Tests laufen regulaer. Der vorherige Gesamtlauf fand vier Punkte:
fehlendes Fenstertitel-Muster, fehlende Eintrittsanimation und zwei Strukturtests,
die noch den alten direkten Gruppenaufruf beziehungsweise die alten Feldabstaende
erwarteten. Fenster vereinheitlicht; Tests auf die neue gemeinsame Layoutregel
und die ausdruecklich gewuenschten kompakteren Abstaende angepasst. Keine Tests entfernt.

Die echten Seitenproben liefen fuer Haltungen (hell) und Schaechte (dunkel)
erfolgreich durch. Gespeicherte Anordnung beim erneuten Oeffnen bestaetigt;
Schacht-Anordnung zusaetzlich in der isolierten Einstellungsdatei nachgelesen.
Das lokale Debug-Programm ist neu gebaut und mit F5 startbar. Kein Commit/Push
und keine Aenderung am echten Benutzerprofil durch die Prueflaeufe.
