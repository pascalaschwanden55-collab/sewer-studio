"""Fokussierte Tests fuer collect_class_candidates.py.

Schuetzt den XTF-Parser gegen den Modellvarianten-Fehler (VSA_KEK_2020_LV95
und aelteres VSA_KEK) und die REF-Attribut-Aufloesung, plus den db3-Join.
"""
from __future__ import annotations

import importlib.util
import sqlite3
import sys
import tempfile
import unittest
from pathlib import Path

SCRIPTS_DIR = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(SCRIPTS_DIR))

MODULE_PATH = SCRIPTS_DIR / "collect_class_candidates.py"
SPEC = importlib.util.spec_from_file_location("collect_class_candidates", MODULE_PATH)
assert SPEC is not None and SPEC.loader is not None
MODULE = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = MODULE
SPEC.loader.exec_module(MODULE)


def _xtf(model: str, code: str, foto: str) -> str:
    return f"""<?xml version="1.0" encoding="UTF-8"?>
<TRANSFER xmlns="http://www.interlis.ch/INTERLIS2.3">
  <DATASECTION>
    <{model}.KEK.Untersuchung TID="u1">
      <vonPunktBezeichnung>10009</vonPunktBezeichnung>
      <bisPunktBezeichnung>10010</bisPunktBezeichnung>
    </{model}.KEK.Untersuchung>
    <{model}.KEK.Kanalschaden TID="s1">
      <UntersuchungRef REF="u1"></UntersuchungRef>
      <Distanz>12.5</Distanz>
      <KanalSchadencode>{code}</KanalSchadencode>
      <SchadenlageAnfang>1</SchadenlageAnfang>
      <SchadenlageEnde>3</SchadenlageEnde>
      <Videozaehlerstand>00:01:02:03</Videozaehlerstand>
    </{model}.KEK.Kanalschaden>
    <{model}.KEK.Datei TID="d1">
      <Bezeichnung>{foto}</Bezeichnung>
      <Klasse>Kanalschaden</Klasse>
      <Objekt REF="s1"></Objekt>
    </{model}.KEK.Datei>
  </DATASECTION>
</TRANSFER>
"""


class CollectClassCandidatesTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.root = Path(self.tmp.name)

    def tearDown(self):
        self.tmp.cleanup()

    def _write_xtf(self, name: str, content: str) -> Path:
        path = self.root / name
        path.write_text(content, encoding="utf-8")
        return path

    def test_neue_modellvariante_wird_gelesen(self):
        path = self._write_xtf("neu.xtf", _xtf("VSA_KEK_2020_LV95", "BAHCA", "foto1.jpg"))
        befunde, totals = MODULE.parse_xtf(path)
        self.assertEqual(totals["BAH"], 1)
        self.assertEqual(len(befunde), 1)
        self.assertEqual(befunde[0]["code"], "BAHCA")
        self.assertEqual(befunde[0]["haltung_von"], "10009")
        self.assertEqual(befunde[0]["haltung_bis"], "10010")
        self.assertEqual(befunde[0]["meter"], "12.5")
        self.assertEqual(befunde[0]["uhrlage"], "1-3")
        self.assertEqual(befunde[0]["datei_name"], "foto1.jpg")

    def test_alte_modellvariante_wird_gelesen(self):
        path = self._write_xtf("alt.xtf", _xtf("VSA_KEK", "BCCAB", "foto2.jpg"))
        befunde, totals = MODULE.parse_xtf(path)
        self.assertEqual(totals["BCC"], 1)
        self.assertEqual(len(befunde), 1)
        self.assertEqual(befunde[0]["code"], "BCCAB")
        self.assertEqual(befunde[0]["haltung_von"], "10009")

    def test_db3_join_liefert_foto_und_haltung(self):
        db = self.root / "projekt.db3"
        connection = sqlite3.connect(str(db))
        try:
            connection.execute("CREATE TABLE SECTION (OBJ_PK INTEGER PRIMARY KEY, OBJ_Key TEXT)")
            connection.execute("CREATE TABLE SECINSP (INS_PK INTEGER PRIMARY KEY, INS_Section_FK INTEGER)")
            connection.execute("CREATE TABLE SECOBS (OBS_PK INTEGER PRIMARY KEY, OBS_Inspection_FK INTEGER, OBS_OpCode TEXT, OBS_Distance REAL, OBS_ClockPos1 INTEGER, OBS_ClockPos2 INTEGER, OBS_VideoCtr TEXT, OBS_Observation TEXT)")
            connection.execute("CREATE TABLE SECOBSMM (OMM_PK INTEGER PRIMARY KEY, OMM_Observation_FK INTEGER, OMM_FilePath TEXT, OMM_FileName TEXT)")
            connection.execute("INSERT INTO SECTION VALUES (1, '716915-690666')")
            connection.execute("INSERT INTO SECINSP VALUES (1, 1)")
            connection.execute("INSERT INTO SECOBS VALUES (1, 1, 'BAHCA', 4.2, 12, 2, '00:00:10', 'schadhafter Anschluss')")
            connection.execute("INSERT INTO SECOBSMM VALUES (1, 1, 'X:/fotos', 'img1.jpg')")
            connection.commit()
        finally:
            connection.close()
        befunde, totals = MODULE.parse_db3(db)
        self.assertEqual(totals["BAH"], 1)
        self.assertEqual(len(befunde), 1)
        eintrag = befunde[0]
        self.assertEqual(eintrag["haltung_von"], "716915-690666")
        self.assertEqual(eintrag["code"], "BAHCA")
        self.assertEqual(eintrag["meter"], "4.2")
        self.assertEqual(eintrag["uhrlage"], "12-2")
        self.assertEqual(eintrag["datei_name"], "img1.jpg")

    def test_db3_ohne_kerntabellen_wird_ignoriert(self):
        db = self.root / "fremd.db3"
        connection = sqlite3.connect(str(db))
        try:
            connection.execute("CREATE TABLE Irgendwas (Id INTEGER)")
            connection.commit()
        finally:
            connection.close()
        befunde, totals = MODULE.parse_db3(db)
        self.assertEqual(befunde, [])
        self.assertEqual(dict(totals), {})


if __name__ == "__main__":
    unittest.main()
