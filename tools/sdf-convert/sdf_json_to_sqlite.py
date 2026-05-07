"""
Liest den SSCE-JSON-Export aus convert_sdf_to_db3.ps1 und schreibt eine
SQLite .db3 mit identischem Schema. Binaries (BLOB) werden aus Base64 dekodiert.
"""
import json, sqlite3, sys, os, base64
from pathlib import Path

# SSCE → SQLite Typ-Mapping (vereinfacht, lax)
TYPE_MAP = {
    'nvarchar': 'TEXT', 'nchar': 'TEXT', 'ntext': 'TEXT',
    'varchar': 'TEXT',  'char':  'TEXT', 'text':  'TEXT',
    'int': 'INTEGER', 'bigint': 'INTEGER', 'smallint': 'INTEGER', 'tinyint': 'INTEGER',
    'bit': 'INTEGER',
    'real': 'REAL', 'float': 'REAL', 'numeric': 'REAL', 'decimal': 'REAL', 'money': 'REAL',
    'datetime': 'TEXT', 'rowversion': 'BLOB',
    'uniqueidentifier': 'TEXT',
    'image': 'BLOB', 'binary': 'BLOB', 'varbinary': 'BLOB',
}

def to_sqlite_type(sdf_type: str) -> str:
    return TYPE_MAP.get((sdf_type or '').lower(), 'TEXT')

def convert(json_path: str, sqlite_path: str):
    with open(json_path, encoding='utf-8-sig') as f:
        data = json.load(f)

    if os.path.exists(sqlite_path):
        os.remove(sqlite_path)
    con = sqlite3.connect(sqlite_path)
    con.execute("PRAGMA journal_mode=WAL;")
    cur = con.cursor()

    for table, payload in data.items():
        cols = payload.get('columns', [])
        rows = payload.get('rows', [])
        if not cols:
            continue

        col_defs = ", ".join(f'"{c["name"]}" {to_sqlite_type(c["type"])}' for c in cols)
        cur.execute(f'CREATE TABLE "{table}" ({col_defs})')

        if not rows:
            continue

        placeholders = ", ".join("?" for _ in cols)
        col_list = ", ".join(f'"{c["name"]}"' for c in cols)
        insert_sql = f'INSERT INTO "{table}" ({col_list}) VALUES ({placeholders})'

        batch = []
        for row in rows:
            vals = []
            for c in cols:
                v = row.get(c['name'])
                if isinstance(v, str) and v.endswith("::BLOB"):
                    try:
                        v = base64.b64decode(v[:-6])
                    except Exception:
                        v = None
                vals.append(v)
            batch.append(vals)

        cur.executemany(insert_sql, batch)
        print(f"  {table}: {len(batch)} rows inserted")

    con.commit()
    con.close()
    print(f"\nSQLite: {sqlite_path}  ({os.path.getsize(sqlite_path)/1024:.0f} KB)")

if __name__ == '__main__':
    if len(sys.argv) < 3:
        print("Usage: sdf_json_to_sqlite.py <json-export> <out-sqlite.db3>")
        sys.exit(1)
    convert(sys.argv[1], sys.argv[2])
