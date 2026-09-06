"""Isolierter Einstellungsordner fuer die Sichtpruefung: eigenes settings.json, Fenster 1366x768,
kuenstliches Projekt als letztes Projekt, Wissensordner = echter KI_BRAIN (sonst saehe der
KI-Spiegel eine leere Quelle). Nichts davon beruehrt die echten Einstellungen unter LOCALAPPDATA."""
import io, json, os, sys
basis = os.path.dirname(os.path.abspath(__file__))
profil = os.path.join(basis, 'profil')
projekt = os.path.join(basis, 'Projekt', 'Projektdateien', 'projekt.json')
theme = sys.argv[1] if len(sys.argv) > 1 else 'Light'
os.makedirs(profil, exist_ok=True)
p = os.path.join(profil, 'settings.json')
s = json.load(io.open(p, encoding='utf-8')) if os.path.exists(p) else {}
s['UiTheme'] = theme
s['KnowledgeRootPath'] = r'C:\KI_BRAIN'
s['AiStartOnProgramStart'] = False
s['EnableRestorePoints'] = False
s['ReduceMotion'] = True
s['LastProjectPath'] = projekt
s['RecentProjectPaths'] = [projekt]
ws = s.setdefault('WindowStates', {})
ws['MainWindow'] = {'Left': 100, 'Top': 60, 'Width': 1366, 'Height': 768, 'IsMaximized': False}
io.open(p, 'w', encoding='utf-8').write(json.dumps(s, indent=2, ensure_ascii=False))
print('profil ok', p, 'theme', theme)
