from pathlib import Path
import json, hashlib, shutil, zipfile, xml.etree.ElementTree as ET
from html import escape

root = Path(r'C:\Sewer-Studio_KI_4.5')
tmp = root/'.tmp/nova-abschluss'
probe = tmp/'bedienung'
out = root/'docs/reviews/2026-09-06-nova/wpf-etappe-1/abschluss'
out.mkdir(parents=True, exist_ok=True)
(out/'bilder').mkdir(exist_ok=True)
(out/'werkzeug').mkdir(exist_ok=True)
sha = lambda p: hashlib.sha256(p.read_bytes()).hexdigest()

before = json.loads((probe/'quellen/original-hashes.json').read_text())
after = {name: sha(probe/'quellen'/name) for name in before}
assert before == after, 'Eine Testquelle wurde veraendert!'
project = json.loads((probe/'projekt/Projektdateien/projekt.json').read_text(encoding='utf-8-sig'))
records = project['Data']
assert len(records) == 14
fields = [r['Fields'] for r in records]
chosen = next(f for f in fields if f['Haltungsname'] == '10001-10002')
assert chosen['Rohrmaterial'] == 'Steinzeug'
assert chosen['Gefaelle_Promille'] == '2,5'
assert all(f['DN_mm'] == '300' for f in fields)
assert sum(f['Rohrmaterial'] == 'Beton' for f in fields) == 13
validation = {'sourceSha256Before': before, 'sourceSha256After': after,
    'sourceFilesUnchanged': True, 'recordCount': len(records), 'dn300Count': 14,
    'betonCount': 13, 'steinzeugCount': 1, 'selectedSlopePromille': chosen['Gefaelle_Promille'],
    'projectSha256': sha(probe/'projekt/Projektdateien/projekt.json')}
(out/'datenpruefung.json').write_text(json.dumps(validation, ensure_ascii=False, indent=2), encoding='utf-8')
shutil.copy2(tmp/'sicherung.json', out/'sicherung.json')

ns = {'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
tests = []
for p in sorted((tmp/'results-abschluss').glob('*.trx')):
    tree = ET.parse(p)
    c = tree.find('.//t:Counters', ns).attrib
    assert int(c['failed']) == 0 and int(c['passed']) > 0
    tests.append({'project':p.stem, **{k:int(v) for k,v in c.items()}})
assert len(tests) == 4
(out/'testergebnis.json').write_text(json.dumps(tests, indent=2), encoding='utf-8')
total = sum(t['passed'] for t in tests)
skips = sum(t['total']-t['executed'] for t in tests)
assert total == 15208 and skips == 13

labels = '''fullhd-100-maximiert fullhd-100-auswahl haltungen-125-nach-neustart zustandsfarben-125-hell haltungen-150-korrigiert kompakt-150-korrigiert zeilenauswahl-150-korrigiert menue-150-letzter-eintrag fokusmodus-ein fokusmodus-aus windows-100-zurueck qgis-vorschau qgis-uebernommen ergaenzungen-gespeichert kataster-vorschau kataster-uebernommen medien-suchergebnis medien-verknuepft medien-verknuepfung-gespeichert strassen-ohne-schachtdaten sanierung-bearbeiten ki-direkt-ergebnis vorschlag-ergebnis hydraulik-ergebnis hydraulik-wasserstand-120 hydraulik-pdf-gespeichert dossier-verfuegbarkeit dossier-erstellt tabelle-abgedockt-fenster tabelle-wieder-angedockt haltung-nach-unten haltung-nach-oben-versuch nach-oben-korrigiert nach-oben-jetzt-aktiv nach-unten-korrigiert verschieben-oben-gesperrt spalten-umgeordnet spalte-leeren-bestaetigung spalte-leeren-meldung-geschlossen zeilenhoehe-veraendert zeilenhoehe-zurueck tabellenzoom-veraendert ausrichtung-links ausrichtung-mitte ausrichtung-rechts ausrichtung-oben ausrichtung-unten ausrichtung-vertikal-mitte hydraulik-fehlende-werte-gesperrt gefaellefeld-jetzt-sichtbar gefaelle-gespeichert hydraulik-final-erstellt'''.split()
selected = []
for label in labels:
    files = [probe/'nachweise'/f'{label}.json', *sorted((probe/'nachweise').glob(f'{label}-*.png'))]
    assert all(p.exists() for p in files) and len(files)>1, label
    selected.extend(files)
selected.extend(sorted((probe/'nachweise').glob('messung-*.json')))
selected += [probe/'isolation.json', probe/'startup-unterdrueckt.txt',
             probe/'projekt/Projektdateien/projekt.json', *[probe/'quellen'/name for name in before],
             probe/'medien/10001-10002.mp4', probe/'hydraulik-test.pdf',
             probe/'hydraulik-final.pdf', probe/'dossier-test.pdf']
with zipfile.ZipFile(out/'bediennachweise.zip', 'w', zipfile.ZIP_DEFLATED) as z:
    for p in selected:
        z.write(p, p.relative_to(probe))
with zipfile.ZipFile(out/'testnachweise.zip', 'w', zipfile.ZIP_DEFLATED) as z:
    for p in [tmp/'build-abschluss.log',tmp/'release-abschluss.json',
              *sorted(tmp.glob('*Tests-abschluss.log')), *sorted((tmp/'results-abschluss').glob('*.trx')),
              *[tmp/'theme-tests'/f'{n}.trx' for n in ('fachfehler-rot','gefaellefeld-rot','standardfarbe-layout-rot','abschluss-fokussiert-final')]]:
        assert p.exists(), p
        z.write(p, p.relative_to(tmp))
for name in ('fullhd-100-auswahl-0.png','zustandsfarben-125-hell-0.png','kompakt-150-korrigiert-0.png',
             'menue-150-letzter-eintrag-0.png','gefaelle-gespeichert-0.png','hydraulik-final-1.png',
             'dossier-pdf-1.png','dossier-pdf-2.png','windows-100-zurueck-0.png'):
    shutil.copy2(probe/'nachweise'/name, out/'bilder'/name)
for name in ('hydraulik-final.pdf','dossier-test.pdf'):
    shutil.copy2(probe/name, out/name)
for name in ('release.ps1','quellen.py','architektur_abschluss.py','abschluss_erstellen.py'):
    shutil.copy2(tmp/name, out/'werkzeug'/name)
for name in ('Program.cs','Pruefhost.csproj'):
    shutil.copy2(tmp/'pruefhost'/name, out/'werkzeug'/name)

actions = [
('Medien suchen','Bestanden','Ein echtes künstliches MP4 gefunden und verknüpft; 13 Haltungen ohne Treffer.','medien-verknuepfung-gespeichert'),
('Leere Felder aus QGIS','Bestanden','Ein leeres Materialfeld zu Steinzeug ergänzt. DN 300 und 13 vorhandene Beton-Werte erhalten.','qgis-uebernommen'),
('Katasterkennungen','Bestanden','14 Kennungen übernommen; 14 Haltungen nur für Neu-Export geeignet, kein vollständiger GEONIS-Verbund.','kataster-uebernommen'),
('Strassen','Leerfall geprüft','Ohne zugehörige Schacht-Strassen verständliche Meldung; Übernehmen gesperrt. Kein positiver Übernahmelauf.','strassen-ohne-schachtdaten'),
('Sanierungsmassnahme bearbeiten','Bestanden','Echte Massnahmenansicht der gewählten Haltung geöffnet und zur Liste zurückgekehrt.','sanierung-bearbeiten'),
('Direkt zur KI-Optimierung','Ersatzfall geprüft','Dialog gestartet; KI nicht verfügbar ehrlich angezeigt, regelbasierter Ersatzvorschlag. Ohne Übernahme geschlossen.','ki-direkt-ergebnis'),
('Vorschlag für diese Haltung','Leerfall geprüft','Ohne bewertete Vergleichsfälle erklärt das Programm, warum noch kein Vorschlag vorliegt.','vorschlag-ergebnis'),
('Hydraulik berechnen','Bestanden','Wasserstand 90 auf 120 mm verändert: angezeigter Durchfluss 14,40 auf 24,71 l/s. Keine unabhängige Fachvalidierung der Formel.','hydraulik-wasserstand-120'),
('Hydraulik PDF','Bestanden nach Korrektur','Ohne Gefälle gesperrt; nach Eingabe von 2,5 ‰ PDF erstellt und Wert im Bericht bestätigt. Halbfüllung bleibt 150 mm bei DN 300.','hydraulik-final-erstellt'),
('Dossier','Bestanden','Zwei Seiten mit Deckblatt und Haltungsprotokoll erzeugt und visuell geprüft. Fehlende Abschnitte korrekt nicht verfügbar.','dossier-erstellt'),
('Abdocken / Andocken','Bestanden','Tabelle in eigenes Fenster und zurück, Auswahl erhalten. Wechsel zwischen unterschiedlichen Monitoren nicht geprüft.','tabelle-wieder-angedockt'),
('Nach oben / Nach unten','Bestanden nach Korrektur','Reihenfolge geändert; Gegenrichtung ohne erneute Auswahl sofort verfügbar. Oberste Grenze korrekt gesperrt.','nach-oben-jetzt-aktiv'),
('Spalten anordnen','Bestanden','Aktiviert und Strasse per Ziehen hinter DN verschoben.','spalten-umgeordnet'),
('Spalte leeren','Leerfall geprüft','Modus aktiviert, Rechtsklick auf Profilform: bereits leer korrekt erkannt. Meldung mit Escape geschlossen; keine gefüllte Spalte gelöscht.','spalte-leeren-bestaetigung'),
('Zeilenhöhe','Bestanden','38 auf 91 Pixel gestellt, sichtbare Höhenänderung, danach auf 38 zurück.','zeilenhoehe-veraendert'),
('Tabellenzoom','Bestanden','100 auf 118 Prozent gestellt, Tabelle vergrössert sich sichtbar.','tabellenzoom-veraendert'),
('Ausrichtung (sechs Knöpfe)','Bestanden','Links, Mitte, rechts sowie oben, mittig, unten an der NR-Spalte ausgelöst und sichtbare Textlage geprüft.','ausrichtung-vertikal-mitte'),
('Fokusmodus F11','Bestanden','Bei 150 Prozent ein- und ausgeschaltet. Sieben statt fünf volle Zeilen sichtbar.','fokusmodus-ein'),
]
fixes = [
('Auswahl klar erkennen','Aktiver Chip und Tabellenzeile hatten im dunklen Thema zu wenig Kontrast. Beide Themes besitzen jetzt gemeinsame Auswahlfarben und eine sichtbare Kontur.'),
('Zustandsklasse lesbar','Direkt erzeugte Textspalten verloren die vorgesehene Textfarbe beim Laden der Ausrichtung. Alle drei Spaltentypen übernehmen jetzt die Farbe ihrer Zelle.'),
('Menü vollständig erreichbar','Bei 150 Prozent verschwanden die unteren Aktionen. Lange Kontextmenüs sind jetzt scrollbar; die letzte Aktion wurde erreicht.'),
('Bericht mit belegten Werten','Das Hydraulik-PDF benutzte Panelwerte oder Ersatzwerte. Es prüft nun DN und Projektgefälle und nutzt das tatsächlich gespeicherte Gefälle.'),
('Fehlende Eingabe ergänzt','Das bereits vorgesehene Projektgefälle fehlte als normales Eingabefeld. „Gefälle ‰“ steht jetzt immer in den Stammdaten bereit.'),
('Zeilen in beide Richtungen bewegen','Nach einer Bewegung blieb der Gegenbefehl ausgegraut. Die Befehle werden nun bei einer Änderung der Liste sofort aktualisiert.'),
]
testrows = '\n'.join(f"| {t['project']} | {t['passed']} | {t['total']-t['executed']} | 0 |" for t in tests)
actionrows = '\n'.join(f'| {a} | {s} | {d} | `{n}` |' for a,s,d,n in actions)
fixtext = '\n'.join(f'{i}. **{a}:** {b}' for i,(a,b) in enumerate(fixes,1))
md = f'''# Nova-Etappe 1 – Abschluss der Nachprüfung

Stand: 6. September 2026. Nutzeranforderung: **Full HD (1920 × 1080) ist das Minimum.**

Die Nachprüfung des zusammengeführten Nova-Stands ist abgeschlossen. Sechs dabei bestätigte Bedien-/Darstellungsprobleme sind korrigiert und mit Gegenproben geprüft. Dieser Bericht belegt die beschriebene Etappe, keine vollständige 9-von-10-Bewertung des Gesamtprogramms.

## Sicherung und Zusammenführung

Der offene Hauptbaum wurde vor dem Merge in einer SHA-256-geprüften ZIP gesichert (186 konkrete Dateien, darunter rekursiv aufgelöste neue Ordner) und als `eb62604d7` committet. Die früher genannten 83 Einträge sind daher keine verlässliche Dateianzahl dieses Sicherungslaufs. ZIP: `.tmp/nova-abschluss/sicherung-20260906-182750.zip`, SHA-256 siehe `sicherung.json`.

`feature/nova-etappe-1` bei `07477255b` wurde konfliktfrei in `feature/eval-pruefsatz-review` integriert. Ausgangsbasis war `69fd0a671`. CLAUDE.md enthält sowohl Audit-Paket 1 als auch Nova. Der abschliessende Merge-Commit enthält diese Korrekturen und Nachweise. Kein Push. Der externe Nova-Worktree bleibt erhalten.

Der Sicherungscommit lagerte die Mehrdeutigkeitsentscheidung von `HoldingFolderDistributor.FindVideo` in den bestehenden kleinen `VideoKopienAufloeser.LoeseTreffer` aus. Grund war die bestehende Grössengrenze der grossen Partial-Klasse. Vor dem Sicherungscommit bestand der vollständige Releaseweg separat.

## Nachgewiesene Korrekturen

{fixtext}

Die roten Gegenproben stehen in `testnachweise.zip`: `fachfehler-rot.trx` (acht Fehlschläge), `gefaellefeld-rot.trx` (zwei) und `standardfarbe-layout-rot.trx`. Der finale fokussierte Lauf hat 69 bestandene Tests und einen ausgelagerten Kindtest. Der isolierte Elterntest prüft per Receipt, dass der Kindprozess tatsächlich lief.

Fachliche Regel: Der Hydraulik-Bericht berechnet weiterhin Halbfüllung, nicht die letzte freie Panel-Eingabe. DN und Gefälle kommen aus der Haltung; Zustand/Materialersatz und Temperatur weiterhin aus Einstellungen. Keine unabhängige hydraulische Norm- oder Formelabnahme. Der bestehende Feldschlüssel und das JSON-Dictionary bleiben kompatibel; die feste CSV-/Excel-Spaltenfolge wird nicht erweitert. Keine neuen NuGet-Pakete, keine neue Dienstregistrierung.

## Vollständiger Releaseweg am Endstand

Solution-Build: **0 Fehler, 0 Warnungen**.

| Testprojekt | Bestanden | Übersprungen | Fehler |
|---|---:|---:|---:|
{testrows}
| Gesamt | {total} | {skips} | 0 |

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
{actionrows}

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
'''
(out/'ABSCHLUSS.md').write_text(md, encoding='utf-8')

cards = ''.join(f'<article><span class="number">{i:02}</span><h3>{escape(a)}</h3><p>{escape(b)}</p></article>' for i,(a,b) in enumerate(fixes,1))
rows = ''.join(f'<tr><th scope="row">{escape(a)}</th><td><span class="badge {"ok" if s.startswith("Bestanden") else "partial"}">{escape(s)}</span></td><td>{escape(d)}</td></tr>' for a,s,d,n in actions)
html = '''<!doctype html><html lang="de"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>SewerStudio · Nova Abschluss</title>
<style>
:root{color-scheme:light;--ink:#172a3b;--muted:#486071;--line:#cddae2;--blue:#185b9d;--paper:#f2f6f8}*{box-sizing:border-box}body{margin:0;font:17px/1.6 system-ui,Segoe UI,sans-serif;color:var(--ink);background:var(--paper)}a{color:#104e8b;text-underline-offset:4px}a:focus-visible,summary:focus-visible{outline:3px solid #c14f00;outline-offset:5px}.wrap{width:min(1280px,calc(100% - 56px));margin:auto}header{background:#132d42;color:white;padding:58px 0 46px}.eyebrow{text-transform:uppercase;letter-spacing:.14em;font-size:13px;font-weight:700;color:#b8d6ec}h1{font-size:clamp(32px,4vw,58px);line-height:1.1;max-width:900px;margin:20px 0}header p{max-width:790px;color:#d2e2ed;font-size:20px}nav{display:flex;gap:26px;flex-wrap:wrap;margin-top:27px}nav a{color:#dcefff}h2{font-size:28px;line-height:1.25;margin:0 0 22px}h3{font-size:20px;line-height:1.3;margin:10px 0}p{margin:10px 0}section{padding:42px 0;border-bottom:1px solid var(--line)}.metrics{display:grid;grid-template-columns:repeat(4,1fr);gap:15px;margin:28px 0}.metric,article{background:white;border:1px solid var(--line);border-radius:12px;padding:23px}.metric strong{display:block;font-size:30px;line-height:1.25;color:#134e78}.metric span{color:var(--muted);font-size:15px}.note{border-left:4px solid #b66b00;background:#fff5df;padding:16px 22px;border-radius:0 8px 8px 0}.grid{display:grid;grid-template-columns:repeat(3,1fr);gap:18px}.number{color:#1b5c93;font-size:14px;font-weight:700}article p{font-size:16px;color:var(--muted)}figure{margin:24px 0}img{display:block;width:100%;height:auto;border:1px solid #9aaeba;border-radius:10px}figcaption{color:var(--muted);font-size:15px;margin-top:8px}.columns{display:grid;grid-template-columns:1fr 1fr;gap:30px}.tablewrap{overflow-x:auto}table{border-collapse:collapse;width:100%;background:white;font-size:15px;text-align:left}th,td{padding:14px 16px;border-bottom:1px solid var(--line);vertical-align:top}thead{background:#dfebf3}tbody th{min-width:200px;font-weight:650}.badge{display:inline-block;white-space:nowrap;border-radius:20px;padding:3px 9px;font-size:13px;font-weight:650}.ok{background:#dcefe6;color:#13543c}.partial{background:#fff0cd;color:#714300}summary{cursor:pointer;font-size:20px;font-weight:700;padding:20px;background:white;border:1px solid var(--line);border-radius:10px}details[open] summary{margin-bottom:16px}.plan{counter-reset:steps;list-style:none;padding:0}.plan li{counter-increment:steps;position:relative;padding:0 0 23px 55px}.plan li:before{content:counter(steps);position:absolute;left:0;top:0;background:#185b9d;color:white;width:34px;height:34px;border-radius:50%;text-align:center;line-height:34px}.plan strong{display:block}.links{display:flex;gap:12px;flex-wrap:wrap}.links a{padding:10px 16px;background:white;border:1px solid var(--line);border-radius:8px}footer{padding:32px 0;color:var(--muted);font-size:14px}code{overflow-wrap:anywhere}.small{font-size:15px;color:var(--muted)}@media(max-width:850px){.grid{grid-template-columns:1fr 1fr}.columns{grid-template-columns:1fr}.metrics{grid-template-columns:1fr 1fr}}@media(max-width:520px){.wrap{width:calc(100% - 30px)}.grid{grid-template-columns:1fr}.metric{padding:16px}.metric strong{font-size:23px}header{padding-top:32px}}@media print{header{background:white;color:#172a3b;padding:10px 0}header p,.eyebrow{color:#172a3b}nav{display:none}.wrap{width:100%}section{padding:20px 0}article,figure,tr{break-inside:avoid}body{font-size:11pt}.metrics{margin:12px 0}}
</style>
<header><div class="wrap"><div class="eyebrow">SewerStudio / Nova-Etappe 1 / 6. September 2026</div><h1>Nachgeprüft.<br>Verbessert. Zusammengeführt.</h1><p>Die offenen Nova-Punkte wurden am laufenden Programm mit künstlichen Daten geprüft. Gefundene Fehler sind behoben; der gesamte Release-Test ist grün.</p><nav aria-label="Seitenbereiche"><a href="#ergebnis">Ergebnis</a><a href="#verbessert">Was besser ist</a><a href="#bedienung">Bedienprüfung</a><a href="#plan">Nächste Schritte</a><a href="#nachweise">Nachweise</a></nav></div></header>
<main class="wrap"><section id="ergebnis"><div class="metrics"><div class="metric"><strong>15.208</strong><span>Tests bestanden · 13 übersprungen</span></div><div class="metric"><strong>0 Fehler</strong><span>Build · auch 0 Warnungen</span></div><div class="metric"><strong>6 Korrekturen</strong><span>am Code und Verhalten geprüft</span></div><div class="metric"><strong>Full HD</strong><span>1920 × 1080 als Minimum</span></div></div><div class="note"><strong>Was dieses Ergebnis bedeutet:</strong> Die Nova-Nachprüfung ist abgeschlossen. Eine Bewertung des gesamten SewerStudio mit 9 von 10 ist damit noch nicht belegt. Die Grenzen stehen unten offen dabei.</div><figure><a href="bilder/fullhd-100-auswahl-0.png"><img src="bilder/fullhd-100-auswahl-0.png" alt="Echte SewerStudio-Haltungsliste im dunklen Thema auf Full HD mit hervorgehobener Zeile"></a><figcaption>Echte Bedienprobe auf Full HD, dunkles Thema. Ausschliesslich künstliches Projekt.</figcaption></figure></section>
<section id="verbessert"><h2>Was jetzt besser funktioniert</h2><div class="grid">''' + cards + '''</div></section>
<section><h2>Mehr Schrift braucht mehr Platz</h2><p>Full HD bleibt die Mindestauflösung. Windows-Skalierung vergrössert die Darstellung und verkleinert dabei den nutzbaren Arbeitsbereich.</p><div class="tablewrap"><table><thead><tr><th>Full-HD-Probe</th><th>Volle Zeilen</th><th>Hinweis</th></tr></thead><tbody><tr><th>100 %, maximiert</th><td>14 von 14</td><td>Eingabefelder zu; mit offenen Feldern 9 Zeilen.</td></tr><tr><th>125 %, hell</th><td>7</td><td>Fenster 1366,4 × 768 in logischen Einheiten; nach Neustart gemessen.</td></tr><tr><th>150 %, maximiert, dunkel</th><td>5 / mit F11: 7</td><td>Unterste Menüeinträge jetzt durch Scrollen erreichbar.</td></tr></tbody></table></div><p class="small">Standard-Zeilenhöhe und Tabellenzoom, keine vorübergehende Speichermeldung. Die 1366er-Fensterprobe ist keine Freigabe kleinerer Bildschirme. Windows steht wieder auf 100 %.</p></section>
<section id="bedienung"><h2>Jede Aktion wurde angeklickt</h2><p>Viele Abläufe wurden bis zum Ergebnis ausgeführt. Wo Testdaten fehlen oder die KI nicht verfügbar war, wurde der passende Hinweis oder Ersatzfall geprüft.</p><details><summary>Prüfliste aller 18 Aktionsgruppen öffnen</summary><div class="tablewrap"><table><thead><tr><th>Aktion</th><th>Ergebnis</th><th>Was wirklich geprüft wurde</th></tr></thead><tbody>''' + rows + '''</tbody></table></div></details><div class="columns"><div><h3>PDF mit dem richtigen Gefälle</h3><p>Nach Eingabe von 2,5 ‰ zeigt der Bericht genau diesen Wert. Ohne gültiges Gefälle stoppt der Export mit einem Hinweis.</p><p>Die Berechnung im Bericht bleibt bei Halbfüllung. Sie ist ein eigener Berechnungsfall und übernimmt nicht den zuletzt veränderten Wasserstand im Rechenfenster.</p><p><a href="hydraulik-final.pdf">Geprüften Hydraulik-Bericht öffnen</a><br><a href="dossier-test.pdf">Geprüftes Dossier öffnen</a></p></div><figure><a href="bilder/hydraulik-final-1.png"><img src="bilder/hydraulik-final-1.png" alt="Hydraulik-Bericht für DN 300 und Gefälle 2,5 Promille, Wasserstand 150 Millimeter" loading="lazy"></a><figcaption>Der erzeugte PDF-Bericht wurde auch als Bild geprüft.</figcaption></figure></div></section>
<section><h2>Deine Arbeit ist gesichert</h2><div class="columns"><div><h3>Hauptbaum und Nova zusammengeführt</h3><p>Die offenen Änderungen wurden vorab als geprüfte Sicherung und eigener Commit gespeichert. Danach wurde Nova konfliktfrei übernommen. Beide Dokumentationsstände sind enthalten.</p><p class="small">Sicherung: 186 konkrete Dateien. Sicherungscommit <code>eb62604d7</code>. Nova-Ausgangsstand <code>07477255b</code>. Lokal integriert; kein Push. Der zusätzliche Nova-Arbeitsordner bleibt erhalten.</p></div><div><h3>Prüfdaten getrennt gehalten</h3><p>Eigenes Projekt, eigene Einstellungen und künstliche Kataster- und Mediendateien. Der Prüfhost startet weder den Datenspiegel noch die QGIS-Brücke.</p><p>Die drei Test-Quelldateien sind per SHA-256 nachweislich unverändert. Die Originaldateien auf dem Desktop wurden nicht bearbeitet.</p></div></div></section>
<section id="plan"><h2>Was ich als Nächstes umsetzen würde</h2><p>Diese Folgeetappen sind Vorschläge. Sie sind noch nicht als erledigt gezählt.</p><ol class="plan"><li><strong>Die restlichen Fachdialoge vereinheitlichen</strong>Lesbare Eingaben, Hervorhebungen und Checkboxen in beiden Themen. Abnahme auf Full HD bei 100, 125 und 150 Prozent.</li><li><strong>Hydraulik-Rechenfälle klar benennen</strong>Sichtbar machen, ob eine freie Berechnung oder ein Halbfüllungsbericht entsteht. Wertequellen erklären und fachliche Referenzfälle gegenprüfen.</li><li><strong>Ein vollständiges künstliches Kanalnetz prüfen</strong>Schächte, Strassen, Geometrie und Katasterverbünde zusammenbringen. Übernahme, Export und Rückimport vollständig durchspielen.</li><li><strong>Die Gesamtfreigabe separat prüfen</strong>Produktiven Start in einem eigenen Windows-Profil, unterschiedliche Monitore, Screenreader und KI-Bewertungsdatensatz prüfen. Daraus ein belastbares Gesamturteil ableiten.</li></ol><details><summary>Wo die Nachprüfung bewusst Grenzen hat</summary><p>Kein neuer Nachweis für den produktiven Gesamtstart, vollständigen GEONIS-Rückabgleich, reale KI-Qualität, positiven Strassenlauf mit zugehörigen Schächten, Löschen gefüllter Spalten, mehrere Monitore mit verschiedener Skalierung oder Screenreader.</p><p>Die Trennlinien-Persistenz wurde in Claudes früherer Sichtprobe belegt. Der eigene Prüfhost setzt Einstellungen pro Start neu und ist deshalb kein zusätzlicher Nachweis dafür. Die Kontrasttests prüfen die geänderten Auswahlfarben; sie ersetzen keine vollständige WCAG-Prüfung.</p></details></section>
<section id="nachweise"><h2>Alles nachvollziehbar abgelegt</h2><p>Der ausführliche Bericht erklärt die Befunde, Testgrenzen und Wiederholungsschritte. Die ZIP-Dateien enthalten die ausgewählten Originalnachweise.</p><div class="links"><a href="ABSCHLUSS.md">Ausführlicher Bericht</a><a href="testnachweise.zip">Build &amp; Tests</a><a href="bediennachweise.zip">Bediennachweise</a><a href="datenpruefung.json">Datenprüfung</a><a href="testergebnis.json">Testzahlen</a><a href="bilder/windows-100-zurueck-0.png">Windows wieder 100 %</a></div></section></main><footer class="wrap">SewerStudio · Nova-Nachprüfung · Alle Aussagen beziehen sich auf die dokumentierten Prüfungen vom 6. September 2026. Diese Seite funktioniert ohne Internet.</footer></html>'''
html = html.replace('<title>', '<link rel="icon" href="data:,"><title>', 1)
(out/'ueberblick.html').write_text(html, encoding='utf-8')
manifest = {str(p.relative_to(out)):sha(p) for p in sorted(out.rglob('*')) if p.is_file() and p.name != 'SHA256.json'}
(out/'SHA256.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding='utf-8')
print(json.dumps({'out':str(out),'passed':total,'skipped':skips,'evidenceFiles':len(selected), 'sourceFilesUnchanged':True,'actions':len(actions)},ensure_ascii=False))
