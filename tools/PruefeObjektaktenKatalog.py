"""Vergleicht den eingebauten Katalog lesend mit dem unabhängigen WebGIS-Planpaket."""
import argparse
import hashlib
import json
from pathlib import Path


def read(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def verify(package):
    root = Path(__file__).resolve().parent.parent
    fields = read(package / "feldkatalog.json")
    catalogs = read(package / "auswahlkataloge.json")
    actual = read(root / "src/AuswertungPro.Next.Domain/Models/Objektakten.Katalog.json")
    definitions = {f["id"]: f for f in actual["felder"]}
    choices = {c["id"]: c for c in actual["kataloge"]}
    assert len(definitions) == len(actual["felder"]), "Doppelte Fachdefinition"
    assert len(fields["fields"]) == 200
    for field in fields["fields"]:
        got = definitions[field["id"]]
        assert got["art"] == field["scope"], field["id"]
        assert got["katalogId"] == field["catalogId"], field["id"]
        assert got["quellanzeigen"] == len(field["sourceOccurrences"]), field["id"]
        assert got["label"] and got["thema"] and got["gruppe"], field["id"]
    for catalog in catalogs["catalogs"]:
        got = choices[catalog["id"]]
        assert got["codesBestaetigt"] == catalog["codesVerified"], catalog["id"]
        def entries(values):
            return [(e["index"], e["originalCode"], e["label"]) for e in values]
        assert entries(got["eintraege"]) == entries(catalog["entries"]), catalog["id"]
    manifest = read(package / "QUELLENMANIFEST.json")
    for source in manifest["sourceCopies"]:
        assert hashlib.sha256((package / source["file"]).read_bytes()).hexdigest() == source["sha256"], source["file"]
    assert len(actual["unterlisten"]) == len(fields["linkedTables"]) == 24
    return {
        "Quellfelder": 200, "Quellanzeigen": sum(f["quellanzeigen"] for f in actual["felder"]),
        "Dropdownstellen": sum(bool(f["catalogId"]) for f in fields["fields"]),
        "Kataloge": len(choices), "Katalogeintraege": sum(len(c["eintraege"]) for c in choices.values()),
        "Quellbelege": len(manifest["sourceCopies"]), "AlleEintraegeUnveraendert": True,
        "sha256": {name: hashlib.sha256((package / name).read_bytes()).hexdigest()
                   for name in ("feldkatalog.json", "auswahlkataloge.json")}
    }


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("paket", type=Path)
    arguments = parser.parse_args()
    print(json.dumps(verify(arguments.paket), indent=2, ensure_ascii=False))
