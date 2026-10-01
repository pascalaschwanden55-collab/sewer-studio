# Paket 1: Originale und Projektdateien schützen

Stand: 06.09.2026. **A01, A02 und A03 sind umgesetzt und durch feste Tests geprüft.**
Die übrigen Pakete bleiben offen. Zielniveau 9/10 ist weiterhin nicht nachgewiesen.

## Was jetzt anders läuft

- Schachtumbenennung weist fremde Dateipfade ab. Ohne Projektordner werden Dateipfade nicht verändert.
- Verknüpfungen und Junctions werden geprüft. Importarchive, Projektdateien und Wiederherstellungspunkte bleiben geschützt.
- Hauptordner, Unterordner, Dateien und Fotoordner folgen einem gemeinsamen Änderungsplan.
- PDF-Felder und Medienverweise aller Protokollstände folgen den tatsächlich geplanten Änderungen.
- Alle Ziele werden vorab auf Konflikte geprüft. Schreibfehler lösen die Rücknahme bereits ausgeführter Schritte aus.
- Die PDF-Textkorrektur erhält nur erneut geprüfte Arbeitsdateien im Projekt.
- JSON-`null` wird als ungültiges Projekt abgewiesen. Die Wiederherstellung überspringt solche Sicherungen und sucht die nächste gültige.
- Gültige leere Projekte und alte Version-1-Projekte bleiben lesbar.

## Was die Tests belegen

Vor der Reparatur: 14 von 16 neuen Datei-/Wiederherstellungsfällen rot; zwei gültige Altprojektfälle grün.
Dazu zwei neue rote PDF-Schutzfälle in der Oberfläche. Anschliessend bestätigte ein zusätzlicher Test veraltete Fotoverweise.
Die festen Prüfungen decken fremde Pfade in allen vier Pfadfeldern, fehlenden Projektpfad, Archive,
Junctions, verschachtelte absolute und relative Pfade, Namenskonflikte, gesperrte Dateien und alle Protokollstände ab.
Sie verwenden ausschliesslich künstliche Dateien.

Gezielter Abschlusslauf: **25 Datei-/Wiederherstellungstests bestanden**.
Die **24 zugehörigen UI-/Architekturtests** bestanden; sie sind zusätzlich in der vollständigen UI-Suite enthalten.
Insgesamt wurden **24 Testfälle neu ergänzt**, vorhandene Umbenennungsfälle blieben erhalten.
Der Bestandszähler der Junction-Schutztests wurde von 84 auf 85 ergänzt; keine Schutzgrenze wurde gelockert.

Vollständiger Release-Build der 52 Projekte: **0 Fehler, 0 Warnungen**.
Isolierter Bauordner: `.tmp/paket1-2026-09-06/artifacts`. Abhängigkeiten gesperrt wiederhergestellt; keine Paketänderung.

| Testsuite | Bestanden | Fehler | Übersprungen |
|---|---:|---:|---:|
| AuswertungPro.Next.Infrastructure.Tests | 6170 | 0 | 6 |
| AuswertungPro.Next.Pipeline.Tests | 2562 | 0 | 3 |
| AuswertungPro.Next.UI.Tests | 6326 | 1 | 3 |
| ProjectModernizer.Tests | 62 | 0 | 0 |

**Gesamt: 15.120 bestanden, 1 Fehler, 12 übersprungen.**
Die Dateisuite wurde nach Ergänzung des Bestandszählers vollständig wiederholt. Wiederholungen werden nicht doppelt gezählt.
Nachweise: [Testergebnisse](nachweise/paket1/testergebnisse.json) und TRX-Dateien im Unterordner `nachweise/paket1/regression`.

Der verbleibende Fehler ist **A04**, das bereits vorhandene Wachstum von `HoldingFolderDistributor` auf 3.071 Zeilen.
**A05 bestand diesmal in 1,56 Sekunden.** Das hebt die zuvor belegten 60-Sekunden-Hänger nicht auf.
Es gab keine gezielte Reparatur des Nachschlagtests; die Ursache bleibt Paket 2.

## Präzisierungen nach der Nachprüfung

- **A01:** Strikte Abweisung ausserhalb der Projektgrenze; kein automatisches Übernehmen fremder Dateien während der Umbenennung.
- **A04:** Die sieben zusätzlichen Zeilen stammen aus dem schon vorhandenen, nicht committeten Import-Arbeitsbaum.
- **A05:** Auch einzeln wurde das Limit erreicht. Historischer namentlicher Nachweis: [Auszug](nachweise/paket1/nachschlag-nachweis.md).
- **A09:** Sechs aktuelle Sicherheitsausnahmen. Die frühere Zahl fünf bezeichnet den historischen Stand vom 14.08.
- **A10:** GitHub-Lauf [33967264002](https://github.com/pascalaschwanden55-collab/sewer-studio/actions/runs/33967264002) direkt nachgeprüft:
  358.420 / 768.970 Zeilen = **46,61 %**, Grenze **45,35 %**. Die vier Testschritte bestanden, das Abdeckungstor scheiterte.
  Der lokale Auditwert bleibt **46,58 %**. Die vorhandene Grenze kann somit bereits aus einem echten CI-Wert nachgezogen werden.
  Änderung der Grenzdatei und eine getrennte Produktcode-Messung bleiben Paket 2.

## Architektur und Grenzen

Die öffentliche Fassade, `IShaftRenameService`, Registrierung und gespeicherten Datenformate bleiben erhalten.
Die neuen Planungshelfer liegen in `Application/UseCases/Schaechte` und führen selbst keine Dateioperationen aus.
Die bestehende Dateiimplementierung in `Application/Common` bleibt eine dokumentierte Altlast.

Die Rücknahme ist kein dauerhaftes Journal gegen Stromausfall. Scheitert die Rücknahme selbst, wird dies ausdrücklich gemeldet.
Die PDF-Inhaltskorrektur bleibt ein nachgelagerter Schritt mit eigener Fehleranzeige.
Die Pfadprüfung wird vor dem Schreiben wiederholt; ein gleichzeitig von anderen Prozessen ausgetauschter Pfad
ist durch diese verwalteten Dateioperationen nicht atomar ausgeschlossen.
Unbewegte Fotoquellen und historische Änderungsprotokolltexte bleiben unverändert.

`CLAUDE.md` und der globale Architektur-Skill sind aktualisiert.
Nach ausdrücklicher Nutzerfreigabe wurde die zuvor blockierte
[Skill-Ergänzung](nachweise/paket1/sewer-architektur.patch) unverändert übernommen.
Die globale Datei wurde danach erfolgreich validiert; der vorherige Stand ist als Nachweis gesichert.

Produktcode und Tests sind lokal geändert; kein Commit, keine Veröffentlichung. Die laufende SewerStudio-Anwendung wurde nicht beendet.
Die übrigen Importänderungen blieben erhalten. Der GPU-Versuch des ursprünglichen Audits bleibt ein gesonderter offener Nachweis.

[Einfacher HTML-Überblick](paket-1.html) · [Gesamter Audit](ueberblick.html) · [Umsetzungsplan](UMSETZUNGSPLAN.md)
