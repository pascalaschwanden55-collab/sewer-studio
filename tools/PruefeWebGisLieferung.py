"""Liest eine Original-XTF ohne Änderungen und zählt sämtliche Objektklassen/Felder.

Kein INTERLIS-Validator. Der Bericht enthält keine Namen, Adressen oder Einzel-TIDs.
Aufruf: python tools/PruefeWebGisLieferung.py ORIGINAL.xtf BERICHT.json
"""
import collections
import hashlib
import json
import pathlib
import sys
import xml.etree.ElementTree as ET


def inventar(quelle):
    vorher = quelle.stat()
    klassen = collections.Counter()
    felder = collections.defaultdict(collections.Counter)
    refs = collections.defaultdict(collections.Counter)
    tids = set()
    doppelt = 0
    ohne_tid = collections.Counter()
    modelle = []
    stack = []
    with quelle.open('rb') as stream:
        for event, elem in ET.iterparse(stream, events=('start', 'end')):
            if event == 'start':
                stack.append(elem)
                if elem.tag.split('}')[-1] == 'MODEL':
                    modelle.append(dict(elem.attrib))
            else:
                if len(stack) == 4 and stack[1].tag.split('}')[-1] == 'DATASECTION':
                    cls = elem.tag.split('}')[-1]
                    klassen[cls] += 1
                    tid = elem.get('TID')
                    if tid:
                        if tid in tids:
                            doppelt += 1
                        tids.add(tid)
                    else:
                        ohne_tid[cls] += 1
                    for child in elem:
                        feld = child.tag.split('}')[-1]
                        felder[cls][feld] += 1
                        if child.get('REF'):
                            refs[cls][feld] += 1
                    stack[-2].remove(elem)
                    elem.clear()
                stack.pop()
    with quelle.open('rb') as stream:
        sha = hashlib.file_digest(stream, 'sha256').hexdigest()
    nachher = quelle.stat()
    if (vorher.st_size, vorher.st_mtime_ns) != (nachher.st_size, nachher.st_mtime_ns):
        raise ValueError('Die Quelle wurde während der Prüfung geändert. Bericht verworfen.')
    return {'quelle': str(quelle), 'bytes': vorher.st_size, 'sha256': sha, 'modelle': modelle,
            'objekte': sum(klassen.values()), 'eindeutige_tids': len(tids), 'doppelte_tids': doppelt,
            'klassen': [{'klasse': cls, 'anzahl': count, 'ohne_tid': ohne_tid[cls],
                         'felder': dict(sorted(felder[cls].items())), 'referenzen': dict(sorted(refs[cls].items()))}
                        for cls, count in sorted(klassen.items())]}


if __name__ == '__main__':
    quelle, ziel = (pathlib.Path(s).resolve() for s in sys.argv[1:])
    if quelle == ziel:
        raise ValueError('Quelle und Bericht müssen verschieden sein.')
    bericht = inventar(quelle)
    ziel.parent.mkdir(parents=True, exist_ok=True)
    with ziel.open('x', encoding='utf-8') as f:
        json.dump(bericht, f, ensure_ascii=False, indent=2)
        f.write('\n')
    print(f'{bericht["objekte"]} Objekte; {len(bericht["klassen"])} Klassen; {bericht["doppelte_tids"]} doppelte TIDs')
