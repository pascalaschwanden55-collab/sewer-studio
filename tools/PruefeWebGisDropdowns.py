"""Vergleicht Code, Text und Elterngruppe der dokumentierten WebGIS-Dropdowns.

Liest nur; der JSON-Bericht wird als neue Datei geschrieben. Verglichen werden
Kataloginhalte, nicht die Feldbindungen der Oberfläche. Kombinationen mit mehreren
Eingabefeldern werden über ihre Auswahlpaare zugeordnet. Unklare oder fehlende
Zuordnungen bleiben ausdrücklich im Bericht.
"""
import collections
import hashlib
import json
import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parents[1]
KATALOG = ROOT / 'src/AuswertungPro.Next.Domain/Models/Objektakten.Katalog.json'
VORLAGE = ROOT / 'docs/reviews/2026-09-11-objektakten/WEBGIS-LAYOUT-KOMPLETT.md'


def paare(text):
    return [(m[1], m[2].strip()) for m in re.finditer(r'`([^`]*)`\s+([^`]+?)(?=\s+·\s+`|<br>|$)', text)]


def pruefe():
    daten = json.loads(KATALOG.read_text(encoding='utf-8-sig'))
    kataloge = {k['id']: k for k in daten['kataloge']}
    arten = {1: 'haltung', 2: 'schacht', 3: 'bauwerksteil', 4: 'haltungspunkt',
             5: 'strang', 6: 'deckel', 11: 'dichtheitspruefung', 22: 'inspektion_haltung',
             23: 'inspektion_schacht', 24: 'massnahme', 25: 'einzugsgebiet',
             26: 'hydr_geometrie', 27: 'mech_vorreinigung', 28: 'pumpe',
             29: 'ueberlauf', 30: 'absperr_drossel'}
    for n in [8, 9, 10]: arten[n] = 'sanierung'
    for n in [7, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21]: arten[n] = 'unterhalt'
    katalog_art = collections.defaultdict(set)
    for f in daten['felder']:
        for key in ['katalogId', 'katalogIdJeEltern']:
            if f.get(key): katalog_art[f['art']].add(f[key])
    belege = []
    maske = ''; art = ''; abschnitt = ''
    for nr, zeile in enumerate(VORLAGE.read_text(encoding='utf-8-sig').splitlines(), 1):
        m = re.match(r'## (\d+)\. (.*)', zeile)
        if m: art = arten[int(m[1])]; maske = m[2]; abschnitt = ''
        if zeile.startswith('### '): abschnitt = zeile[4:]
        if not re.match(r'\| \d+ \|', zeile): continue
        spalten = zeile.split('|')
        if len(spalten) < 9 or spalten[3].strip() != 'auswahl': continue
        text = spalten[7].strip()
        gruppen = re.findall(r'Elternwert `([^`]+)`: (.*?)(?=<br>\s*-\s*Elternwert|$)', text)
        erwartet = [(eltern, code, label) for eltern, liste in gruppen for code, label in paare(liste)] if gruppen else [(None, c, l) for c, l in paare(text)]
        if not erwartet:
            belege.append({'maske': maske, 'art': art, 'feld': spalten[2].strip(), 'zeile': nr,
                           'status': 'ohne_ausgezaehlte_werte', 'quelle': text})
            continue
        kandidaten = []
        for kid in katalog_art[art]:
            k = kataloge[kid]
            aktuell = [(e.get('eltern') if gruppen else None, e.get('originalCode'), e['label']) for e in k['eintraege']]
            # Ältere Erhebungen enthalten nur Texte; fehlende Codes werden separat gemeldet.
            key = (lambda x: (x[0], x[2])) if not k.get('codesBestaetigt') and not gruppen else (lambda x: x)
            soll = collections.Counter(map(key, erwartet))
            ist = collections.Counter(map(key, aktuell))
            gemeinsam = sum((soll & ist).values())
            if gemeinsam:
                kandidaten.append((gemeinsam / sum((soll | ist).values()), kid, aktuell))
        kandidaten.sort(reverse=True)
        if not kandidaten or kandidaten[0][0] < 0.5:
            belege.append({'maske': maske, 'art': art, 'abschnitt': abschnitt, 'feld': spalten[2].strip(),
                           'zeile': nr, 'status': 'kein_passender_katalog', 'erwartet': erwartet})
            continue
        beste = kandidaten[0][0]
        treffer = [x for x in kandidaten if x[0] == beste]
        # Identische Kataloginhalte mit mehreren Kennungen sind keine inhaltliche Mehrdeutigkeit.
        inhalte = {tuple(x[2]) for x in treffer}
        aktuell = treffer[0][2]
        fehlt = list((collections.Counter(erwartet) - collections.Counter(aktuell)).elements())
        extra = list((collections.Counter(aktuell) - collections.Counter(erwartet)).elements())
        nur_leerwahl = not fehlt and extra and all(code in (None, '') and label == '' for parent, code, label in extra)
        status = 'mehrdeutig' if len(inhalte) > 1 else 'vollstaendig_mit_leerwahl' if nur_leerwahl else 'abweichung' if fehlt or extra else 'vollstaendig'
        belege.append({'maske': maske, 'art': art, 'abschnitt': abschnitt, 'feld': spalten[2].strip(), 'zeile': nr,
                       'kataloge': [x[1] for x in treffer],
                       'felder_im_katalog': [f['id'] for f in daten['felder'] if f['art'] == art
                                             and any(f.get(key) in [x[1] for x in treffer] for key in ('katalogId', 'katalogIdJeEltern'))],
                       'status': status,
                       'anzahl_quelle': len(erwartet), 'anzahl_katalog': len(aktuell),
                       'fehlend': fehlt, 'zusaetzlich': extra,
                       'fehlende_texte': [e for e in erwartet if e[2] not in [a[2] for a in aktuell]],
                       'reihenfolge_gleich': erwartet == aktuell,
                       'reihenfolge_ohne_leerwahl_gleich': erwartet == [e for e in aktuell if e[2]]})
    return {'pruefumfang': 'Code, Text, Gruppe, Häufigkeit und Reihenfolge der dokumentierten Kataloginhalte. Keine Vollabnahme der Feldbindungen, dynamischen Datenlisten oder Maskenfunktionen.',
            'vorlage': str(VORLAGE.relative_to(ROOT)), 'vorlage_sha256': hashlib.sha256(VORLAGE.read_bytes()).hexdigest(),
            'katalog_sha256': hashlib.sha256(KATALOG.read_bytes()).hexdigest(),
            'kataloge': len(kataloge), 'katalogeintraege': sum(len(k['eintraege']) for k in kataloge.values()),
            'dropdownfelder': sum(bool(f.get('katalogId')) for f in daten['felder']),
            'status': dict(collections.Counter(b['status'] for b in belege)), 'belege': belege}


if __name__ == '__main__':
    ziel = pathlib.Path(sys.argv[1]).resolve()
    if ziel in (KATALOG.resolve(), VORLAGE.resolve()): raise ValueError('Keine Quelle überschreiben.')
    bericht = pruefe()
    ziel.parent.mkdir(parents=True, exist_ok=True)
    with ziel.open('x', encoding='utf-8') as f: json.dump(bericht, f, ensure_ascii=False, indent=2)
    print(json.dumps({k: v for k, v in bericht.items() if k != 'belege'}, ensure_ascii=True))
