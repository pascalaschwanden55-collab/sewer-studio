# Kompaktere aufgeklappte Felder – 08.09.2026

Auf Nutzerwunsch nutzt `RecordDetailsView` im vorhandenen kompakten Modus
weniger Platz. Das gilt fuer die aufgeklappten Haltungs- und Schachtformulare
und andere Ansichten, die denselben kompakten Modus verwenden.

- Einzeilige Eingaben und Auswahllisten: Mindesthoehe 30 statt 36, Innenabstand 6/4.
- Mehrzeilige Eingaben: Mindesthoehe 60 statt 72; Scrollen und Zeilenumbruch bleiben aktiv.
- Feldkarten: Innenabstand 4 statt 8, Abstand darunter 4 statt 6.
- Normale Feldkarten ohne zusaetzlichen Rahmen und Hintergrund; fachliche Farbmarkierungen bleiben erhalten.
- Abstand zwischen Beschriftung und Eingabe: 2 statt 5. Schriftgroessen bleiben gleich.
- Gruppen-Innenabstand: 4 statt 8. Eingabebindungen und Datenregeln sind unveraendert.

Pruefung: `dotnet build AuswertungPro.Dev.slnf -c Release --no-restore`
erfolgreich, 0 Warnungen und 0 Fehler. Die betroffenen bestehenden UI-Tests
(`AufklappListe`, `RecordDetail`, `NovaLayout`, `NovaListenWiederverbinden`)
lieferten 164 bestandene Tests, 8 uebersprungene Kindprozess-Einstiegspunkte,
0 Fehler. Die zugehoerigen Eltern-Tests starten die Kindprozess-Pruefungen.

Zusaetzlich wurden Haltungen im hellen und Schaechte im dunklen Thema bei
1920 x 1080 mit dem isolierten Pruefhost fotografiert und visuell kontrolliert.
Beide Laeufe endeten mit Code 0, einschliesslich Listen-/Tabellenwechsel.
Bilder und TRX liegen lokal unter `.tmp/kompakte-details/`.
Der temporaere Pruefhost meldete beim Wiederherstellen zwei NU1900-Warnungen
wegen nicht erreichbarer NuGet-Sicherheitsabfrage; der Produkt-Build war warnungsfrei.

Das laufende Debug-Programm wurde nicht beendet oder ersetzt. Die Aenderung
wird beim naechsten Neubauen und Starten sichtbar. Keine Veroeffentlichung
oder vollstaendige neue Redesign-Abnahme mit dieser kleinen Darstellungsaenderung.
