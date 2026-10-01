from pathlib import Path
import shutil

root = Path(r'C:\Sewer-Studio_KI_4.5')
skill = Path(r'C:\Users\Besitzer\.codex\skills\sewer-architektur\SKILL.md')
marker = '## Nova-Nachpruefung abgeschlossen (2026-09-06)'
checks = {
    'src/AuswertungPro.Next.Application/DataPage/DataPageHydraulikReportCalculator.cs': ['ReadAvailability(record)', 'availability.GefaellePromille!.Value', 'double.IsFinite'],
    'src/AuswertungPro.Next.Domain/Models/FieldCatalog.cs': ['[FieldKeys.SlopePromille]'],
    'src/AuswertungPro.Next.UI/DataPage/DataPageRecordDetailsBuilder.cs': ['Append(FieldKeys.SlopePromille)'],
    'src/AuswertungPro.Next.UI/DataPage/DataPageProjectBindingController.cs': ['command?.NotifyCanExecuteChanged()'],
}
for name, fragments in checks.items():
    source = (root/name).read_text(encoding='utf-8-sig')
    for fragment in fragments:
        assert fragment in source, (name, fragment)
text = '''

## Nova-Nachpruefung abgeschlossen (2026-09-06)

- Mindest-Bildschirmaufloesung laut Nutzer: Full HD (1920 x 1080). 1366 x 768 ist lediglich eine zusaetzliche Fensterprobe. Windows-Skalierung 125 und 150 Prozent wurde auf Full HD nach einem Neustart des isolierten Pruefhosts gemessen. 150 Prozent ergibt weniger Arbeitsflaeche; F11 schafft mehr Tabellenplatz.
- `DataPageHydraulikReportCalculator` verwendet fuer Einzel-PDF und Dossier das Projektgefaelle in Promille. DN und Gefaelle muessen positiv und endlich sein; keine stillen Ersatzwerte 300 mm / 5 Promille. Die Berichtskonvention bleibt Halbfuellung (DN / 2); Materialzustand und Temperatur kommen weiterhin aus den Panel-Einstellungen. Der Bericht ist keine Kopie der frei veraenderten Panel-Berechnung.
- `FieldCatalog.Definitions` benennt den vorhandenen Schluessel `SlopePromille` als Gefaelle in Promille. `DataPageRecordDetailsBuilder` bietet ihn immer als Stammdaten-Eingabe an, auch ohne bisherigen Projektwert. Die feste `ColumnOrder` fuer Tabellenexporte bleibt erhalten; das gespeicherte Dictionary-Format aendert sich nicht.
- `DataPageProjectBindingController` aktualisiert bei Aenderungen der Haltungsliste auch die Auswahlbefehle. Nach oben/unten reagiert damit ohne erneute Auswahl. Keine zusaetzliche Kartenrueckmeldung.
- Auswahlfarben und die Zellentext-Vererbung sind in beiden Themes vereinheitlicht. Kontextmenues besitzen einen ScrollViewer, damit auf Full HD bei 150 Prozent auch die letzten Eintraege erreichbar sind. Keine neuen Abhaengigkeiten oder Registrierungen.
- Nachweise, Pruefgrenzen und verstaendliche HTML-Uebersicht: `docs/reviews/2026-09-06-nova/wpf-etappe-1/abschluss/`. Die isolierte Bedienprobe startet weder produktiven App-Startup noch Spiegel oder QGIS-Bruecke und belegt keine vollstaendige Programmabnahme.
'''
for path in (root/'CLAUDE.md', skill):
    if marker not in path.read_text(encoding='utf-8-sig'):
        if path == skill:
            shutil.copy2(path, root/'.tmp/nova-abschluss/architektur-vor-abschluss.md')
        with path.open('a', encoding='utf-8') as stream:
            stream.write(text)
print('Vier beteiligte Komponenten am Code abgeglichen; Architekturkarte und CLAUDE aktualisiert.')
