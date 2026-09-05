#!/bin/bash
# Baut die Kennungstabelle fuer "Katasterkennungen ergaenzen" aus einer Kopie der
# GEONIS-Datenbank (File-Geodatabase). Nur Kennungen und Namen, keine Fachwerte.
#
# Voraussetzungen: QGIS (ogr2ogr, GDAL mit OpenFileGDB), Python der Sidecar-Umgebung,
# Git Bash. Aufruf aus einem leeren Arbeitsordner:
#   export GDAL_DATA="/c/Program Files/QGIS 4.2.0/share/gdal"
#   export TEMP="$(cygpath -w "$PWD")" TMP="$(cygpath -w "$PWD")"
#   ./bau_kennungen.sh
# Die Zieldatei wird ersetzt; bei neuer Kopie zuerst G und die Stand-Angabe unten anpassen.
# Belegt 2026-09-04: 102'317 Haltungen, 123'096 Schaechte aus Stand_Dezember_2024.
set -e
OGR2="/c/Program Files/QGIS 4.2.0/bin/ogr2ogr.exe"
PY="/c/Sewer-Studio_KI_4.5/sidecar/.venv/Scripts/python.exe"
G="D:/Fachwissen/ArcGis/Stand_Dezember_2024_uri_abwasser.gdb/uri_abwasser.gdb"
TMP="$(cygpath -w "$PWD")\roh_2024.gpkg"; ZIEL="D:/QGIS_V4.2/Layer/Kataster_Kennungen_GEONIS_2024-12.gpkg"
rm -f "$TMP" "$ZIEL"
echo "[1] Rohtabellen kopieren"
"$OGR2" -f GPKG "$TMP" "$G" -sql "SELECT OBJECTID AS OBJECTID, GlobalId, SIA405_ID, BEZEICHNUNG, BEZEICHNUNGALTERNATIV, BEZEICHNUNGHISTORISCH, U_GEMEINDE, STATUS, KANAL_REF, VP_REF, NP_REF, ROHRPROFIL_REF, EIGENTUEMER_REF, GN_LAST_EDITED_DATE FROM AWK_HALTUNG" -nln haltung -nlt MULTILINESTRING
"$OGR2" -f GPKG "$TMP" "$G" -sql "SELECT OBJECTID AS OBJECTID, GlobalId, SIA405_ID, BEZEICHNUNG, U_GEMEINDE, STATUS, ART_BAUWERK, BAUWERK_REF, EIGENTUEMER_REF, GN_LAST_EDITED_DATE FROM AWK_ABWASSERKNOTEN" -nln knoten -nlt POINT -update
"$OGR2" -f GPKG "$TMP" "$G" -sql "SELECT GlobalId, SIA405_ID, ART_BAUWERK FROM AWK_ABWASSERBAUWERK" -nln bauwerk -nlt NONE -update
"$OGR2" -f GPKG "$TMP" "$G" -sql "SELECT GlobalId, SIA405_ID FROM AWK_KANAL" -nln kanal -nlt NONE -update
"$OGR2" -f GPKG "$TMP" "$G" -sql "SELECT GlobalId, SIA405_ID, BEZEICHNUNG FROM AWK_HALTUNGSPUNKT" -nln punkt -nlt NONE -update
"$OGR2" -f GPKG "$TMP" "$G" -sql "SELECT GlobalId, SIA405_ID, PROFILTYP, HOEHENBREITENVERHAELTNIS FROM AWK_ROHRPROFIL" -nln profil -nlt NONE -update
"$OGR2" -f GPKG "$TMP" "$G" -sql "SELECT GlobalID, BEZEICHNUNG, U_VSA_TID, VSA_OBJ_ID, ART FROM AWO_ORGANISATION" -nln organisation -nlt NONE -update
echo "[2] Indizes"
"$PY" - "$TMP" <<'PYEOF'
import sqlite3, sys
c = sqlite3.connect(sys.argv[1]); c.execute("PRAGMA temp_store=MEMORY")
for t, col in [("haltung","GlobalId"),("knoten","GlobalId"),("bauwerk","GlobalId"),("kanal","GlobalId"),("punkt","GlobalId"),("profil","GlobalId"),("organisation","GlobalID")]:
    c.execute(f'CREATE INDEX IF NOT EXISTS ix_{t}_gid ON {t}("{col}")')
for t in ("haltung","knoten"):
    c.execute(f'CREATE INDEX IF NOT EXISTS ix_{t}_bez ON {t}("BEZEICHNUNG")')
c.commit(); print("Indizes ok")
PYEOF
echo "[3] Zielschicht Haltungen"
"$OGR2" -f GPKG "$ZIEL" "$TMP" -dialect SQLITE -sql "SELECT h.BEZEICHNUNG AS bezeichnung, h.BEZEICHNUNGALTERNATIV AS bezeichnung_alternativ, h.BEZEICHNUNGHISTORISCH AS bezeichnung_historisch, h.U_GEMEINDE AS gemeinde, h.STATUS AS status_code, h.OBJECTID AS objectid, h.GlobalId AS globalid, h.SIA405_ID AS haltung_id, k.SIA405_ID AS kanal_id, vp.SIA405_ID AS vonpunkt_id, vp.BEZEICHNUNG AS vonpunkt_bezeichnung, np.SIA405_ID AS nachpunkt_id, np.BEZEICHNUNG AS nachpunkt_bezeichnung, p.SIA405_ID AS rohrprofil_id, p.PROFILTYP AS profiltyp_code, p.HOEHENBREITENVERHAELTNIS AS hoehenbreitenverhaeltnis, o.BEZEICHNUNG AS eigentuemer, o.U_VSA_TID AS eigentuemer_id, h.GN_LAST_EDITED_DATE AS geonis_geaendert, h.shape FROM haltung h LEFT JOIN kanal k ON k.GlobalId = h.KANAL_REF LEFT JOIN punkt vp ON vp.GlobalId = h.VP_REF LEFT JOIN punkt np ON np.GlobalId = h.NP_REF LEFT JOIN profil p ON p.GlobalId = h.ROHRPROFIL_REF LEFT JOIN organisation o ON o.GlobalID = h.EIGENTUEMER_REF" -nln haltungen -nlt MULTILINESTRING -a_srs EPSG:2056
echo "[4] Zielschicht Schaechte"
"$OGR2" -f GPKG "$ZIEL" "$TMP" -dialect SQLITE -sql "SELECT n.BEZEICHNUNG AS bezeichnung, n.U_GEMEINDE AS gemeinde, n.STATUS AS status_code, n.ART_BAUWERK AS art_bauwerk_code, n.OBJECTID AS objectid, n.GlobalId AS globalid, n.SIA405_ID AS knoten_id, b.SIA405_ID AS bauwerk_id, o.BEZEICHNUNG AS eigentuemer, o.U_VSA_TID AS eigentuemer_id, n.GN_LAST_EDITED_DATE AS geonis_geaendert, n.shape FROM knoten n LEFT JOIN bauwerk b ON b.GlobalId = n.BAUWERK_REF LEFT JOIN organisation o ON o.GlobalID = n.EIGENTUEMER_REF" -nln schaechte -nlt POINT -a_srs EPSG:2056 -update
echo "[5] Herkunft"
"$PY" - "$ZIEL" <<'PYEOF'
import sqlite3, sys
c = sqlite3.connect(sys.argv[1]); c.execute("PRAGMA temp_store=MEMORY")
c.execute("CREATE TABLE IF NOT EXISTS herkunft (schluessel TEXT PRIMARY KEY, wert TEXT)")
c.executemany("INSERT OR REPLACE INTO herkunft VALUES (?,?)", [
 ("quelle","GEONIS-Kopie Stand_Dezember_2024_uri_abwasser.gdb (Abwasser Uri)"),
 ("stand","2024-12"), ("erzeugt","2026-09-04"), ("kennung_praefix_geonis","ch23h1a4"),
 ("zweck","Nur Kennungen fuer den XTF-Austausch mit GEONIS; keine Fachwerte uebernehmen"),
 ("schema_version","1")])
c.execute('CREATE INDEX IF NOT EXISTS ix_haltungen_bez ON haltungen("bezeichnung")')
c.execute('CREATE INDEX IF NOT EXISTS ix_schaechte_bez ON schaechte("bezeichnung")')
c.commit()
for t in ("haltungen","schaechte"):
    print(t, c.execute(f"SELECT COUNT(*), COUNT({'haltung_id' if t=='haltungen' else 'knoten_id'}) FROM {t}").fetchone())
print(c.execute("SELECT bezeichnung, haltung_id, kanal_id, vonpunkt_id, vonpunkt_bezeichnung, nachpunkt_id, rohrprofil_id, eigentuemer, eigentuemer_id FROM haltungen WHERE bezeichnung='78998-79002'").fetchall())
print(c.execute("SELECT bezeichnung, knoten_id, bauwerk_id, art_bauwerk_code, eigentuemer_id FROM schaechte WHERE bezeichnung='78998'").fetchall())
PYEOF
ls -la "$ZIEL"; echo FERTIG
