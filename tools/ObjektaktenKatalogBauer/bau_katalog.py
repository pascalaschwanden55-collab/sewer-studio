"""Baut Objektakten.Katalog.json aus den erfassten WebGIS-Maskendateien.

Wiederholbar: Quelle sind die JSON-Dateien aus der WebGIS-Maskenerfassung, Ziel ist die
eingebettete Katalogressource. Der Bestand (Objektarten haltung, schacht, deckel,
sanierung samt deren Auswahlkatalogen) wird unveraendert uebernommen - die Erfassung
bestaetigt ihn, ersetzt ihn nicht.

Aufruf:
    python tools/ObjektaktenKatalogBauer/bau_katalog.py [--masken <Ordner>] [--pruefen]

--pruefen schreibt nichts und meldet nur, was entstehen wuerde.
"""

from __future__ import annotations

import argparse
import io
import json
import os
import re
import sys
from collections import OrderedDict

BESTANDSARTEN = ("haltung", "schacht", "deckel", "sanierung")

# Eine Objektart je WebGIS-Maske. Mehrere Quelldateien bedeuten: dieselbe Maske haengt an
# mehreren Objekten; die Gleichheit wird vor dem Zusammenfuehren geprueft.
NEUE_ARTEN = [
    ("bauwerksteil", "Bauwerksteil", ["haltung-bauwerksteile.json", "schacht-bauwerksteile.json"]),
    ("unterhalt", "Unterhaltsmassnahme", ["haltung-unterhaltsmassnahmen.json", "schacht-unterhaltsmassnahmen.json"]),
    ("dichtheitspruefung", "Dichtheitspruefung", ["haltung-dichtheitsprufungen.json", "schacht-dichtheitsprufungen.json"]),
    ("massnahme", "GEP-Massnahme", ["haltung-gep-massnahmen.json", "schacht-gep-massnahmen.json"]),
    ("inspektion_haltung", "Inspektion Haltung", ["haltung-inspektionen.json"]),
    ("inspektion_schacht", "Inspektion Schacht", ["schacht-inspektionen.json"]),
    ("einzugsgebiet", "Einzugsgebiet", ["schacht-einzugsgebiete-sw.json", "schacht-einzugsgebiete-rw.json", "schacht-einzugsgebiete-mw.json"],
     "schacht-einzugsgebiete-beschriftungen.json"),
    ("absperr_drossel", "Absperr-/Drosselorgan", ["schacht-absperr-drosselorgane.json"]),
    ("pumpe", "Pumpe", ["schacht-pumpen.json"]),
    ("ueberlauf", "Ueberlauf", ["schacht-uberlaufe.json"]),
    ("mech_vorreinigung", "Mechanische Vorreinigung", ["schacht-mechanische-vorreinigung.json"]),
]

# Untertypen desselben Abwasserknotens: die gemeinsamen Felder gehoeren dem Schacht und
# duerfen nicht ein zweites Mal gepflegt werden. Sie bleiben sichtbar, aber nur lesend.
ERBT_VOM_SCHACHT = {"absperr_drossel", "pumpe", "ueberlauf"}

# Listen, die kein eigenes Objekt fuehren, sondern auf einen vorhandenen Datensatz zeigen.
ZEIGT_AUF = {
    ("haltung", "Einlaeufe"): "haltung",
    ("schacht", "Einlaeufe"): "haltung",
    ("schacht", "Auslaeufe"): "haltung",
    ("schacht", "Hauptdeckel"): "deckel",
    ("schacht", "Deckel"): "deckel",
    ("haltung", "Sanierungsmassnahmen"): "sanierung",
    ("schacht", "Sanierungsmassnahmen"): "sanierung",
}

# Welche Liste zeigt auf welche neue Objektart (Listentitel -> Art).
LISTE_ZU_ART = {
    "Bauwerksteile": "bauwerksteil",
    "Unterhaltsmassnahmen": "unterhalt",
    "Dichtheitspruefungen": "dichtheitspruefung",
    "GEP Massnahmen": "massnahme",
    "Mechanische Vorreinigung": "mech_vorreinigung",
    "Absperr-/Drosselorgane": "absperr_drossel",
    "Pumpen": "pumpe",
    "Ueberlaeufe": "ueberlauf",
    "Einzugsgebiete SW": "einzugsgebiet",
    "Einzugsgebiete RW": "einzugsgebiet",
    "Einzugsgebiete MW": "einzugsgebiet",
}
LISTE_ZU_ART_JE_OBJEKT = {
    ("haltung", "Inspektionen"): "inspektion_haltung",
    ("schacht", "Inspektionen"): "inspektion_schacht",
}

UMLAUTE = str.maketrans({"ä": "ae", "ö": "oe", "ü": "ue", "Ä": "Ae", "Ö": "Oe", "Ü": "Ue", "ß": "ss"})

# Einzige Stelle, an der eine Beschriftung nicht aus der Maske selbst hervorgeht.
# Sie wird bei jedem Lauf gemeldet und muss im WebGIS bestaetigt werden.
ABGELEITETE_BESCHRIFTUNGEN = {
    ("schacht-pumpen.json", 7): (
        "Bauwerksart",
        "In den beiden anderen Untertypmasken desselben Abwasserknotens (Ueberlaeufe #9, "
        "Absperr-/Drosselorgane #7) traegt genau dieses Feld die Beschriftung 'Bauwerksart'; "
        "in der Pumpenmaske fehlt der Text in der Maskendefinition.",
    ),
    ("schacht-pumpen.json", 18): (
        "Tiefe [m]",
        "Gleiche Tabelle AWK_ABWASSERKNOTEN, gleicher Abschnitt 'Daten I', gleiche Nachbarn: "
        "in der Schacht-Hauptmaske steht zwischen 'Sohlenhoehe' und 'Ebene' das Feld 'Tiefe [m]'.",
    ),
    ("schacht-einzugsgebiete-sw.json", 41): (
        "Befestigungsgrad [%] SW",
        "Erste Zelle der Matrixzeile. Die Zellen rechts davon heissen laut Beschriftungsdatei "
        "'... RW', '... MW', '... Geplant SW' usw.; die erste Spalte ist SW.",
    ),
    ("schacht-einzugsgebiete-sw.json", 47): (
        "Abflussbeiwert [%] SW",
        "Erste Zelle der Matrixzeile, gleiche Begruendung wie Befestigungsgrad.",
    ),
    ("schacht-uberlaufe.json", 23): (
        "Tiefe [m]",
        "Gleiche Tabelle AWK_ABWASSERKNOTEN, gleicher Abschnitt 'Daten I', gleiche Nachbarn: "
        "in der Schacht-Hauptmaske steht zwischen 'Sohlenhoehe' und 'Ebene' das Feld 'Tiefe [m]'.",
    ),
}

# Abhaengige Bestandsfelder: Der Plan vom 10.09. kannte nur die Unterliste des damals
# belegten Elternwerts (Beton bzw. Unbekannt). Aus der Hauptmaske kommt jetzt ZUSAETZLICH
# ein Katalog je Elternwert; der bisherige Katalog bleibt unveraendert.
BESTAND_JE_ELTERN = {
    "haltung.material": ("haltung-hauptmaske.json", "haltung.pipegroup"),
    "schacht.materialdetail": ("schacht-hauptmaske.json", "schacht.materialgruppe"),
}

# Reine Masseinheiten. Ein Text daraus ist keine Feldbeschriftung.
EINHEITEN = {"mm", "cm", "m", "m2", "m3", "m²", "m³", "%", "l/s", "l", "kn/m²", "kn/m2",
             "chf", "ha", "°", "°c", "t", "d", "a", "e/ha", "km", "s", "h", "min"}
TRENNER = {"/", "->", "-", "|", "¦", ":", "…", "...", "+", "="}


def ist_einheit(text: str | None) -> bool:
    roh = (text or "").strip().strip("[]()").strip().lower()
    return roh in EINHEITEN


def ist_nur_einheit_als_label(text: str) -> bool:
    """'[mm]' ist keine Beschriftung, sondern die Einheit der noch offenen Teilbeschriftungen."""
    roh = text.strip()
    return len(roh) > 1 and roh.startswith("[") and roh.endswith("]")


def teile_beschriftung(roh: str) -> list[str]:
    """Zerlegt eine zusammengefasste Beschriftung in die Namen ihrer Einzelfelder.

    'Rechtswert/Hochwert'        -> ['Rechtswert', 'Hochwert']
    'Bez. Schacht (von/bis)'     -> ['Bez. Schacht (von)', 'Bez. Schacht (bis)']
    'Zahl vorl./endg.'           -> ['Zahl vorl.', 'Zahl endg.']
    'Abwasserknoten SW/geplant'  -> ['Abwasserknoten SW', 'Abwasserknoten geplant']
    'Entfernung [m]/Fixierung'   -> ['Entfernung [m]', 'Fixierung']
    'Qan ist [l/s]'              -> ['Qan ist [l/s]']   (Schraegstrich in der Einheit trennt nicht)
    """
    teile = teile_auf_oberster_ebene(roh)
    if len(teile) == 1:
        klammer = re.match(r"^(.*?)\(([^()]*/[^()]*)\)\s*$", roh)
        if klammer:
            stamm = klammer.group(1).strip()
            return [f"{stamm} ({teil.strip()})".strip() for teil in klammer.group(2).split("/")]
        return [roh]
    # 'Lagebest./-genauigkeit': der Bindestrich haengt am ersten Teil, nicht daneben.
    if all(teil.startswith("-") for teil in teile[1:]):
        return [teile[0]] + [f"{teile[0]}{teil}" for teil in teile[1:]]
    # Stammregel: 'Zahl vorl./endg.' -> 'Zahl endg.'. Sie gilt nur, wenn der zweite Teil
    # eine Abwandlung ist (klein geschrieben oder abgekuerzt), nicht ein eigener Begriff
    # wie 'Fixierung' oder 'Hochwert'.
    woerter = teile[0].split()
    if woerter and ist_einheit(woerter[-1]):
        woerter = woerter[:-1]
    if len(woerter) >= 2 and all(ist_abwandlung(teil) for teil in teile[1:]):
        stamm = " ".join(woerter[:-1])
        return [teile[0]] + [f"{stamm} {teil}" for teil in teile[1:]]
    return teile


def teile_auf_oberster_ebene(roh: str) -> list[str]:
    """Trennt an '/', aber nie innerhalb von [..] oder (..) - '[l/s]' bleibt ganz."""
    teile, tiefe, aktuell = [], 0, []
    for zeichen in roh:
        if zeichen in "[(":
            tiefe += 1
        elif zeichen in "])":
            tiefe = max(0, tiefe - 1)
        if zeichen == "/" and tiefe == 0:
            teile.append("".join(aktuell).strip())
            aktuell = []
        else:
            aktuell.append(zeichen)
    teile.append("".join(aktuell).strip())
    return [teil for teil in teile if teil]


def ist_abwandlung(teil: str) -> bool:
    erstes = teil.split()[0] if teil.split() else ""
    return bool(erstes) and (erstes[0].islower() or erstes.endswith("."))


def beschriftungen(felder: list[dict]) -> list[str]:
    """Loest die Beschriftung jedes Feldes auf.

    Das WebGIS fasst mehrere Eingaben unter einer Beschriftung zusammen ('Art/Subart',
    'Profiltyp/Breite/Hoehe'); nur die erste Eingabe traegt den Text. Ausserdem rutscht
    die Beschriftung des naechsten Feldes gelegentlich in das Einheitenfeld des
    vorherigen. Beide Faelle werden aufgeloest; erfunden wird nichts.
    """
    offen: list[str] = []
    namen: list[str] = []
    einheit_anhang = ""
    for stelle, feld in enumerate(felder):
        roh = (feld.get("label") or "").strip()
        if roh and ist_nur_einheit_als_label(roh) and offen:
            # 'Profiltyp/Breite/Hoehe' + '[mm]': die Einheit gehoert zu Breite UND Hoehe.
            einheit_anhang = roh
            namen.append(f"{offen.pop(0)} {roh}")
            continue
        if roh:
            zerlegt = teile_beschriftung(roh)
            namen.append(zerlegt[0])
            offen = zerlegt[1:]
            einheit_anhang = ""
            feld["_alternative"] = None
            continue
        if offen:
            name = offen.pop(0)
            if einheit_anhang and not name.endswith("]"):
                name = f"{name} {einheit_anhang}"
            namen.append(name)
            # Gehoert zu einer geteilten Beschriftung: bei Namensgleichheit innerhalb einer
            # Objektart wird daraus spaeter der eindeutige lange Name gebildet.
            vorgaenger = next((f for f in reversed(felder[:stelle]) if (f.get("label") or "").strip()), None)
            feld["_alternative"] = (name, (vorgaenger or {}).get("label") or "")
            continue
        vorher = (felder[stelle - 1].get("einheit") or "").strip() if stelle else ""
        if vorher and vorher not in TRENNER and not ist_einheit(vorher):
            namen.append(vorher)
            continue
        namen.append("")
    return namen


def schluessel(text: str) -> str:
    """Beschriftung -> stabiler Feldschluessel (nur ASCII, keine Sonderzeichen)."""
    roh = (text or "").translate(UMLAUTE).lower()
    aus = []
    for zeichen in roh:
        aus.append(zeichen if zeichen.isalnum() else "_")
    zusammen = "".join(aus)
    while "__" in zusammen:
        zusammen = zusammen.replace("__", "_")
    return zusammen.strip("_") or "feld"


def lies(pfad: str) -> dict:
    with io.open(pfad, encoding="utf-8") as strom:
        return json.load(strom)


def felder_der_maske(dokument: dict) -> list[dict]:
    """Felder einer Maske. Varianten werden ueber die Beschriftung zusammengefuehrt.

    Die technische Kennung refId ist je Layout verschieden (in der Unterhaltsmaske 43
    refIds fuer 18 Beschriftungen) und taugt deshalb nicht als Feldidentitaet.
    """
    if dokument.get("felder"):
        satz = list(dokument["felder"])
        for feld, name in zip(satz, beschriftungen(satz)):
            feld["_name"] = name
        return satz

    zusammen: "OrderedDict[str, dict]" = OrderedDict()
    for variante in dokument.get("varianten", []):
        arten = [str(teil.get("value")) for teil in (variante.get("subtype") or [])]
        satz = variante.get("felder", [])
        for feld, aufgeloest in zip(satz, beschriftungen(satz)):
            feld["_name"] = aufgeloest
        for feld in satz:
            name = feld["_name"]
            vorhanden = zusammen.get(name)
            if vorhanden is None:
                kopie = dict(feld)
                kopie["_nurBeiArt"] = list(arten)
                zusammen[name] = kopie
                continue
            for art in arten:
                if art not in vorhanden["_nurBeiArt"]:
                    vorhanden["_nurBeiArt"].append(art)
            # Eine Auswahlliste, die in einer Variante gefuellt ist, gewinnt gegen eine leere.
            if vorhanden.get("auswahl") is None and feld.get("auswahl") is not None:
                vorhanden["auswahl"] = feld["auswahl"]
    anzahl = len(dokument.get("varianten", []))
    for feld in zusammen.values():
        if len(feld["_nurBeiArt"]) >= anzahl or not feld["_nurBeiArt"]:
            feld.pop("_nurBeiArt", None)
    return list(zusammen.values())


def feldkennzeichen(feld: dict) -> tuple:
    """Vergleichsschluessel, um zwei Masken auf Gleichheit zu pruefen."""
    return (
        feld.get("_name"), feld.get("feldart"), feld.get("pflicht"), feld.get("nurLesen"),
        feld.get("maxLaenge"), feld.get("einheit"), json.dumps(feld.get("auswahl"), sort_keys=True, ensure_ascii=False),
    )


def eintraege_flach(auswahl) -> list[dict] | None:
    """Auswahlwerte als flache Liste. Abhaengige Listen werden vereinigt, nichts verworfen."""
    if auswahl is None:
        return None
    if isinstance(auswahl, list):
        roh = auswahl
    else:
        roh = [eintrag for block in auswahl.get("jeElternwert", []) for eintrag in block.get("eintraege", [])]
    gesehen: "OrderedDict[tuple, dict]" = OrderedDict()
    for eintrag in roh:
        code = eintrag.get("originalCode")
        code = None if code is None else str(code)  # C# erwartet Text, die Quelle liefert Zahlen.
        paar = (code, eintrag.get("label") or "")
        if paar not in gesehen:
            gesehen[paar] = {"index": len(gesehen), "originalCode": code, "label": paar[1]}
    return list(gesehen.values())


def je_eltern_katalog(kennung: str, auswahl: dict) -> dict:
    """Alle Eintraege aller Elternwerte in einem Katalog, jeder mit seinem Elterncode."""
    eintraege = []
    for block in auswahl.get("jeElternwert", []):
        eltern = str(block.get("elternCode"))
        for e in block.get("eintraege", []):
            code = e.get("originalCode")
            eintraege.append({
                "index": len(eintraege), "originalCode": None if code is None else str(code),
                "label": e.get("label") or "", "eltern": eltern,
            })
    return {"id": kennung, "codesBestaetigt": True, "eintraege": eintraege}


def abhaengiges_maskenfeld(maske: dict) -> dict:
    treffer = [f for f in maske.get("felder", []) if isinstance(f.get("auswahl"), dict)]
    if len(treffer) != 1:
        raise SystemExit(f"ABBRUCH: {maske.get('maske')} hat {len(treffer)} abhaengige Felder, erwartet 1.")
    return treffer[0]


class Katalogsammler:
    """Vergibt Katalogkennungen und verhindert zwei Kataloge mit gleichem Inhalt."""

    def __init__(self, bestand: list[dict]) -> None:
        self.ausgabe = list(bestand)
        self.nach_inhalt = {}
        self.zaehler = {}
        for katalog in bestand:
            self.nach_inhalt.setdefault(self._inhalt(katalog["eintraege"]), katalog["id"])

    @staticmethod
    def _inhalt(eintraege) -> tuple:
        # Der Elterncode gehoert zum Inhalt: ein Katalog je Elternwert ist etwas anderes
        # als die flache Vereinigung derselben Eintraege.
        return tuple((e.get("originalCode"), e.get("label"), e.get("eltern")) for e in eintraege)

    def kennung(self, art: str, eintraege: list[dict]) -> str:
        inhalt = self._inhalt(eintraege)
        vorhanden = self.nach_inhalt.get(inhalt)
        if vorhanden is not None:
            return vorhanden
        nummer = self.zaehler.get(art, 0)
        self.zaehler[art] = nummer + 1
        neu = f"{art}-K{nummer:02d}"
        self.nach_inhalt[inhalt] = neu
        self.ausgabe.append({"id": neu, "codesBestaetigt": True, "eintraege": eintraege})
        return neu


def baue(maskenordner: str, katalogpfad: str) -> tuple[dict, list[str], list[str]]:
    hinweise: list[str] = []
    uebersprungen: list[str] = []
    alt = lies(katalogpfad)
    bestandsfelder = [f for f in alt["felder"] if f["art"] in BESTANDSARTEN]
    bestandskataloge_ids = {f["katalogId"] for f in bestandsfelder if f.get("katalogId")}
    bestandskataloge = [k for k in alt["kataloge"] if k["id"] in bestandskataloge_ids]

    sammler = Katalogsammler(bestandskataloge)
    neue_felder: list[dict] = []
    schachtbeschriftungen = {
        f.get("label") for f in felder_der_maske(lies(os.path.join(maskenordner, "schacht-hauptmaske.json")))
    }

    for eintrag_art in NEUE_ARTEN:
        art, thema, dateien = eintrag_art[0], eintrag_art[1], eintrag_art[2]
        beschriftungsdatei = eintrag_art[3] if len(eintrag_art) > 3 else None
        masken = [lies(os.path.join(maskenordner, name)) for name in dateien]
        feldsaetze = [felder_der_maske(m) for m in masken]
        # Eine getrennt erfasste Beschriftungsdatei (refId -> abgeleitetes Label) fuellt
        # Felder, deren Name aus der Maske allein nicht hervorgeht - etwa Matrixzellen.
        # Sie gilt fuer alle Quelldateien der Objektart, die Zuordnung laeuft ueber refId.
        if beschriftungsdatei:
            pfad = os.path.join(maskenordner, beschriftungsdatei)
            if not os.path.isfile(pfad):
                raise SystemExit(f"ABBRUCH: Beschriftungsdatei fehlt: {beschriftungsdatei}")
            zusatz = {e["refId"]: e["abgeleitetesLabel"] for e in lies(pfad)["felder"]}
            for satz in feldsaetze:
                for feld in satz:
                    name = zusatz.get(feld.get("refId"))
                    if name and name != feld.get("_name"):
                        if satz is feldsaetze[0]:
                            hinweise.append(f"{art}: Feld {feld.get('reihenfolge')} heisst laut "
                                            f"{beschriftungsdatei} '{name}' (vorher '{feld.get('_name')}').")
                        feld["_name"] = name
        erste = [feldkennzeichen(f) for f in feldsaetze[0]]
        for name, satz in zip(dateien[1:], feldsaetze[1:]):
            if [feldkennzeichen(f) for f in satz] != erste:
                raise SystemExit(
                    f"ABBRUCH: {dateien[0]} und {name} sind nicht identisch und duerfen nicht "
                    f"zu einer Objektart zusammengefuehrt werden."
                )
        # Zwei Felder derselben Maske duerfen nicht gleich heissen. Wo eine geteilte
        # Beschriftung denselben Namen erzeugt ('Abwasserknoten SW/geplant' und
        # 'Abwasserknoten RW/geplant'), wird der eindeutige lange Name gebildet.
        haeufig: dict[str, int] = {}
        for feld in feldsaetze[0]:
            haeufig[feld.get("_name") or ""] = haeufig.get(feld.get("_name") or "", 0) + 1
        for feld in feldsaetze[0]:
            name = feld.get("_name") or ""
            alternative = feld.get("_alternative")
            if haeufig.get(name, 0) > 1 and alternative and "/" in (alternative[1] or ""):
                erster = alternative[1].split("/")[0].strip()
                eigen = name.split()[-1] if name.split() else name
                feld["_name"] = f"{erster} {eigen}"
                hinweise.append(f"{art}: '{name}' kam mehrfach vor und heisst jetzt "
                                f"'{feld['_name']}' (aus '{alternative[1]}').")

        vergeben: set[str] = set()
        for feld in feldsaetze[0]:
            beschriftung = (feld.get("_name") or "").strip()
            ausnahme = ABGELEITETE_BESCHRIFTUNGEN.get((dateien[0], feld.get("reihenfolge")))
            if ausnahme:
                beschriftung, grund = ausnahme
                hinweise.append(f"ZU BESTAETIGEN - {art}: Feld {feld.get('reihenfolge')} in "
                                f"{dateien[0]} als '{beschriftung}' uebernommen. {grund}")
            if not beschriftung:
                # Kein Name aus der Maske ableitbar. Das Feld wird ausgelassen und gemeldet;
                # ein erfundener Name waere schlimmer als ein fehlendes Feld.
                uebersprungen.append(f"{art}: Feld {feld.get('reihenfolge')} in {dateien[0]} "
                                     f"(Abschnitt {feld.get('abschnitt')!r}, {feld.get('feldart')}) "
                                     f"hat keine ableitbare Beschriftung und fehlt im Katalog.")
                continue
            if beschriftung != (feld.get("label") or "").strip():
                hinweise.append(f"{art}: Beschriftung '{beschriftung}' aus der Maske abgeleitet "
                                f"(Feld {feld.get('reihenfolge')} traegt selbst keine).")

            basis = f"{art}.{schluessel(beschriftung)}"
            kennung = basis
            nummer = 2
            while kennung in vergeben:
                kennung = f"{basis}_{nummer}"
                nummer += 1
            vergeben.add(kennung)

            eintraege = eintraege_flach(feld.get("auswahl"))
            geerbt = art in ERBT_VOM_SCHACHT and beschriftung in schachtbeschriftungen
            eintrag = {
                "id": kennung,
                "art": art,
                "label": beschriftung,
                "thema": thema,
                "gruppe": (feld.get("abschnitt") or "Daten").strip(),
                "speicherfeld": None,
                "katalogId": sammler.kennung(art, eintraege) if eintraege else None,
                "exportziel": None,
                "belegstatus": "webgis",
                "nurLesen": bool(feld.get("nurLesen")) or geerbt,
                "quellanzeigen": 1,
                "elternfeld": None,
                "belegterElterntext": None,
                "webgisKennung": feld.get("refId"),
                "webgisReihenfolge": feld.get("reihenfolge"),
                "webgisTabelle": masken[0].get("tabelle"),
                "webgisPflicht": bool(feld.get("pflicht")),
                "webgisFeldart": feld.get("feldart"),
                "webgisEinheit": feld.get("einheit") if ist_einheit(feld.get("einheit")) else None,
                "webgisMaxLaenge": feld.get("maxLaenge"),
            }
            if geerbt:
                eintrag["erbtVon"] = "schacht"
            if feld.get("_nurBeiArt"):
                eintrag["nurBeiArt"] = feld["_nurBeiArt"]
            if isinstance(feld.get("auswahl"), dict):
                eltern = (feld["auswahl"].get("abhaengigVon") or {}).get("label") or ""
                eintrag["belegterElterntext"] = None
                eintrag["webgisElternbeschriftung"] = eltern
                # Elternfeld: das Feld derselben Objektart mit der Beschriftung des ersten
                # Teils ('Art/Subart' -> 'Art'). Ohne eindeutigen Treffer wird abgebrochen.
                elternname = teile_auf_oberster_ebene(eltern)[0] if eltern else ""
                kandidaten = [f for f in neue_felder if f["art"] == art and f["label"] == elternname]
                if len(kandidaten) != 1:
                    raise SystemExit(f"ABBRUCH: Elternfeld '{elternname}' fuer {kennung} nicht eindeutig "
                                     f"({len(kandidaten)} Treffer).")
                eintrag["elternfeld"] = kandidaten[0]["id"]
                voll = je_eltern_katalog(f"{kennung}-je-eltern", feld["auswahl"])
                sammler.ausgabe.append(voll)
                eintrag["katalogIdJeEltern"] = voll["id"]
            neue_felder.append(eintrag)

    # Bestand: Katalog je Elternwert additiv anhaengen. Der vorhandene Katalog (nur der
    # damals belegte Elternwert) bleibt Byte fuer Byte; das Feld erhaelt eine ZUSAETZLICHE
    # Kennung. Vorher wird geprueft, dass jeder Elterncode im Eltern-Katalog existiert und
    # dass die Gruppe des belegten Elternwerts dem alten Katalog exakt entspricht.
    kat_nach_id = {k["id"]: k for k in sammler.ausgabe}
    felder_nach_id = {f["id"]: f for f in bestandsfelder}
    for feld_id, (datei, eltern_id) in BESTAND_JE_ELTERN.items():
        feld = felder_nach_id[feld_id]
        maske = lies(os.path.join(maskenordner, datei))
        auswahl = abhaengiges_maskenfeld(maske)["auswahl"]
        elterncodes = {e["originalCode"]: e["label"]
                       for e in kat_nach_id[felder_nach_id[eltern_id]["katalogId"]]["eintraege"]}
        gruppen = {str(b.get("elternCode")): b.get("eintraege", []) for b in auswahl.get("jeElternwert", [])}
        fehlend = [c for c in gruppen if c not in elterncodes]
        if fehlend:
            raise SystemExit(f"ABBRUCH: Elterncodes {fehlend} von {feld_id} fehlen im Eltern-Katalog {eltern_id}.")
        belegt = next((c for c, l in elterncodes.items() if l == feld.get("belegterElterntext")), None)
        alt_eintraege = [(e["originalCode"], e["label"]) for e in kat_nach_id[feld["katalogId"]]["eintraege"]]
        neu_eintraege = [(None if e.get("originalCode") is None else str(e["originalCode"]), e.get("label") or "")
                         for e in gruppen.get(belegt or "", [])]
        if alt_eintraege != neu_eintraege:
            raise SystemExit(f"ABBRUCH: Die Gruppe '{feld.get('belegterElterntext')}' der Maske entspricht nicht "
                             f"dem Bestandskatalog {feld['katalogId']} von {feld_id}.")
        voll = je_eltern_katalog(f"{feld_id}-je-eltern", auswahl)
        sammler.ausgabe.append(voll)
        felder_nach_id[feld_id] = dict(feld, katalogIdJeEltern=voll["id"])
        hinweise.append(f"{feld_id}: Katalog je Elternwert mit {len(voll['eintraege'])} Eintraegen in "
                        f"{len(gruppen)} Gruppen ergaenzt; bisheriger Katalog {feld['katalogId']} unveraendert.")
    bestandsfelder = [felder_nach_id[f["id"]] for f in bestandsfelder]

    unterlisten = baue_unterlisten(maskenordner, hinweise)
    katalog = {
        "version": 1,
        "stand": f"WebGIS 2026-09-11 / DSS_2020_1_LV95 - Hauptmasken 2026-09-10, Detailmasken 2026-09-11",
        "felder": bestandsfelder + neue_felder,
        "kataloge": sammler.ausgabe,
        "unterlisten": unterlisten,
    }
    return katalog, hinweise, uebersprungen


def baue_unterlisten(maskenordner: str, hinweise: list[str]) -> list[dict]:
    listen: list[dict] = []
    for objektart, datei in (("haltung", "haltung-hauptmaske.json"), ("schacht", "schacht-hauptmaske.json")):
        maske = lies(os.path.join(maskenordner, datei))
        for eintrag in maske.get("listen", []):
            titel = eintrag.get("titel") or ""
            kennschluessel = schluessel(titel)
            ziel = ZEIGT_AUF.get((objektart, kennschluessel.capitalize()))
            if ziel is None:
                ziel = ZEIGT_AUF.get((objektart, titel))
            art = LISTE_ZU_ART_JE_OBJEKT.get((objektart, titel)) or LISTE_ZU_ART.get(titel)
            if art is None and ziel is None:
                # Ueber den ASCII-Schluessel nochmals versuchen (Umlaute in Titeln).
                for name, wert in LISTE_ZU_ART.items():
                    if schluessel(name) == kennschluessel:
                        art = wert
                        break
            if art is None and ziel is None:
                for (objekt, name), wert in ZEIGT_AUF.items():
                    if objekt == objektart and schluessel(name) == kennschluessel:
                        ziel = wert
                        break
            if art is None and ziel is None:
                raise SystemExit(f"ABBRUCH: Liste '{titel}' an {objektart} hat keine Zuordnung.")
            zeile = {
                "art": objektart,
                "id": eintrag.get("refId") or kennschluessel,
                "label": titel,
                # In der Quelle ist jede Spalte ein Objekt; sichtbar ist nur ihr Titel.
                "spalten": [spalte.get("titel") if isinstance(spalte, dict) else str(spalte)
                            for spalte in (eintrag.get("spalten") or [])
                            if (spalte.get("titel") if isinstance(spalte, dict) else spalte)],
                "webgisSpalten": eintrag.get("spalten") or [],
                "zeigtAufObjektart": ziel or art,
                # Eigene Akte heisst: die Zeile ist ein eigenes Objekt (Deckel, Unterhalt, ...).
                # Falsch nur dort, wo die Liste auf einen vorhandenen Projektdatensatz zeigt -
                # ein Einlauf ist die anschliessende Haltung, kein zweites Objekt daneben.
                "eigeneObjektart": (ziel or art) not in ("haltung", "schacht"),
                "mehrerePositionen": True,
                "nurLesen": bool(eintrag.get("nurLesen")),
                "webgisAbschnitt": eintrag.get("abschnitt"),
                "webgisTabelle": eintrag.get("tabelle"),
                "webgisRelation": eintrag.get("relation"),
            }
            listen.append(zeile)
    hinweise.append(f"{len(listen)} Listen aufgenommen ({sum(1 for l in listen if l['nurLesen'])} davon nur lesend).")
    return listen


def pruefe(katalog: dict, alt: dict) -> list[str]:
    """Dieselben Regeln, die auch ObjektFeldKatalog.Pruefe in C# anwendet, plus eigene."""
    fehler: list[str] = []
    felder, kataloge, listen = katalog["felder"], katalog["kataloge"], katalog["unterlisten"]
    kennungen = [f["id"] for f in felder]
    if len(set(kennungen)) != len(kennungen):
        fehler.append("Doppelte Feldkennungen.")
    katalogids = [k["id"] for k in kataloge]
    if len(set(katalogids)) != len(katalogids):
        fehler.append("Doppelte Katalogkennungen.")
    bekannt = set(katalogids)
    for feld in felder:
        if not (feld.get("label") or "").strip():
            fehler.append(f"Feld ohne Beschriftung: {feld['id']}")
        if feld.get("katalogId") and feld["katalogId"] not in bekannt:
            fehler.append(f"Toter Katalogverweis: {feld['id']} -> {feld['katalogId']}")
    for eintrag in kataloge:
        if not eintrag.get("eintraege"):
            fehler.append(f"Katalog ohne Eintraege: {eintrag['id']}")
        for wert in eintrag["eintraege"]:
            if wert.get("originalCode") is not None and not isinstance(wert["originalCode"], str):
                fehler.append(f"originalCode ist keine Zeichenkette: {eintrag['id']}")
                break
    # Inhaltsgleiche Kataloge sind nur unter den neuen verboten. Im Bestand gibt es sie
    # bereits (haltung-C00 und schacht-C01 fuehren dieselbe Liste); der Bestand bleibt
    # unangetastet, und ein neuer Katalog verwendet immer den vorhandenen mit.
    altids = {f.get("katalogId") for f in alt["felder"] if f["art"] in BESTANDSARTEN}
    inhalte = {}
    for eintrag in kataloge:
        schluesselwert = tuple((w.get("originalCode"), w.get("label"), w.get("eltern")) for w in eintrag["eintraege"])
        vorher = inhalte.get(schluesselwert)
        if vorher is not None and not (vorher in altids and eintrag["id"] in altids):
            fehler.append(f"Zwei Kataloge mit gleichem Inhalt: {vorher} und {eintrag['id']}")
        if vorher is None:
            inhalte[schluesselwert] = eintrag["id"]
    verwendet = {f.get("katalogId") for f in felder} | {f.get("katalogIdJeEltern") for f in felder}
    for eintrag in kataloge:
        if eintrag["id"] not in verwendet:
            fehler.append(f"Katalog ohne Feld: {eintrag['id']}")
    # Katalog je Elternwert: Elternfeld vorhanden, jeder Elterncode im Eltern-Katalog.
    nach_id = {f["id"]: f for f in felder}
    kat_ids = {k["id"]: k for k in kataloge}
    for feld in felder:
        voll_id = feld.get("katalogIdJeEltern")
        if not voll_id:
            continue
        eltern = nach_id.get(feld.get("elternfeld") or "")
        if eltern is None or not eltern.get("katalogId"):
            fehler.append(f"{feld['id']}: Katalog je Elternwert ohne Elternfeld mit Katalog.")
            continue
        codes = {e.get("originalCode") for e in kat_ids[eltern["katalogId"]]["eintraege"]}
        for e in kat_ids[voll_id]["eintraege"]:
            if not e.get("eltern") or e["eltern"] not in codes:
                fehler.append(f"{feld['id']}: Elterncode {e.get('eltern')!r} nicht im Eltern-Katalog {eltern['katalogId']}.")
                break
    if len(listen) != 24:
        fehler.append(f"{len(listen)} Listen statt 24.")

    # Datentypen, die das C#-Modell erwartet. Ein Objekt statt eines Textes bricht sonst
    # erst beim Programmstart - der Python-Lauf saehe davon nichts.
    for feld in felder:
        # Eine Beschriftung ohne Buchstaben ist keine. So etwas entsteht, wenn ein
        # Trenn- oder Einheitenzeichen faelschlich als Feldname uebernommen wird.
        if not any(zeichen.isalpha() for zeichen in (feld.get("label") or "")):
            fehler.append(f"{feld.get('id')}: Beschriftung {feld.get('label')!r} enthaelt keinen Buchstaben.")
        for name in ("id", "art", "label", "thema", "gruppe", "belegstatus"):
            if not isinstance(feld.get(name), str):
                fehler.append(f"{feld.get('id')}: '{name}' ist kein Text.")
        for name in ("speicherfeld", "katalogId", "exportziel", "elternfeld", "belegterElterntext"):
            if feld.get(name) is not None and not isinstance(feld[name], str):
                fehler.append(f"{feld.get('id')}: '{name}' ist weder Text noch leer.")
        # Fehlende Angaben sind erlaubt - C# setzt dann seinen Standardwert. Nur ein
        # vorhandener Wert mit falschem Typ bricht das Einlesen.
        if feld.get("nurLesen") is not None and not isinstance(feld["nurLesen"], bool):
            fehler.append(f"{feld.get('id')}: 'nurLesen' ist kein Ja/Nein-Wert.")
        if feld.get("quellanzeigen") is not None and not isinstance(feld["quellanzeigen"], int):
            fehler.append(f"{feld.get('id')}: 'quellanzeigen' ist keine Zahl.")
    for liste in listen:
        for name in ("art", "id", "label"):
            if not isinstance(liste.get(name), str):
                fehler.append(f"Liste {liste.get('label')}: '{name}' ist kein Text.")
        if not all(isinstance(spalte, str) for spalte in liste.get("spalten", [])):
            fehler.append(f"Liste {liste.get('label')}: 'spalten' enthaelt etwas anderes als Text.")
    for eintrag in kataloge:
        for wert in eintrag["eintraege"]:
            if not isinstance(wert.get("label"), str) or not isinstance(wert.get("index"), int):
                fehler.append(f"Katalog {eintrag['id']}: Eintrag mit falschem Typ.")
                break
    arten = {f["art"] for f in felder}
    for liste in listen:
        if liste["zeigtAufObjektart"] not in arten:
            fehler.append(f"Liste '{liste['label']}' zeigt auf unbekannte Objektart {liste['zeigtAufObjektart']}.")

    # Der Bestand muss Feld fuer Feld unveraendert bleiben.
    # Der Bestand darf nur additiv erweitert werden: jeder vorhandene Schluessel behaelt
    # seinen Wert, einzig 'katalogIdJeEltern' darf dazukommen.
    altbestand = [f for f in alt["felder"] if f["art"] in BESTANDSARTEN]
    neubestand = [f for f in felder if f["art"] in BESTANDSARTEN]
    if len(altbestand) != len(neubestand):
        fehler.append("Bestandsfelder wurden entfernt oder hinzugefuegt.")
    for a, n in zip(altbestand, neubestand):
        for key, wert in a.items():
            if key != "katalogIdJeEltern" and n.get(key) != wert:
                fehler.append(f"Bestandsfeld {a['id']}: '{key}' veraendert.")
        for key in n:
            if key not in a and key != "katalogIdJeEltern":
                fehler.append(f"Bestandsfeld {a['id']}: unerwarteter neuer Schluessel '{key}'.")
    altids = {f["katalogId"] for f in altbestand if f.get("katalogId")}
    altkat = {k["id"]: k for k in alt["kataloge"] if k["id"] in altids}
    neukat = {k["id"]: k for k in kataloge if k["id"] in altids}
    if altkat != neukat:
        fehler.append("Bestandskataloge wurden veraendert.")
    return fehler


def main() -> int:
    wurzel = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
    zerleger = argparse.ArgumentParser(description=__doc__)
    zerleger.add_argument("--masken", default=r"D:\QGIS_V4.2\GeoShop\webgis-masken")
    zerleger.add_argument("--ziel", default=os.path.join(
        wurzel, "src", "AuswertungPro.Next.Domain", "Models", "Objektakten.Katalog.json"))
    zerleger.add_argument("--pruefen", action="store_true", help="nichts schreiben, nur melden")
    argumente = zerleger.parse_args()

    if not os.path.isdir(argumente.masken):
        print(f"Maskenordner nicht gefunden: {argumente.masken}")
        return 2

    alt = lies(argumente.ziel)
    katalog, hinweise, uebersprungen = baue(argumente.masken, argumente.ziel)
    fehler = pruefe(katalog, alt)

    neue = [f for f in katalog["felder"] if f["art"] not in BESTANDSARTEN]
    arten = sorted({f["art"] for f in neue})
    print(f"Bestand   : {len(katalog['felder']) - len(neue)} Felder, unveraendert")
    print(f"Neu       : {len(neue)} Felder in {len(arten)} Objektarten")
    for art in arten:
        anzahl = sum(1 for f in neue if f["art"] == art)
        geerbt = sum(1 for f in neue if f["art"] == art and f.get("erbtVon"))
        print(f"            {art:<20} {anzahl:>3} Felder" + (f" (davon {geerbt} geerbt)" if geerbt else ""))
    print(f"Kataloge  : {len(katalog['kataloge'])} gesamt")
    print(f"Listen    : {len(katalog['unterlisten'])}")
    for hinweis in hinweise:
        print(f"Hinweis   : {hinweis}")
    if uebersprungen:
        print()
        print(f"AUSGELASSEN ({len(uebersprungen)} Felder ohne ableitbare Beschriftung):")
        for eintrag in uebersprungen:
            print(f"  - {eintrag}")
        print("  Diese Masken muessen im WebGIS nachgelesen werden.")
    if fehler:
        print("\nFEHLER:")
        for eintrag in fehler:
            print(f"  - {eintrag}")
        return 1

    if argumente.pruefen:
        print("\nPruefung bestanden. Es wurde nichts geschrieben.")
        return 0

    with io.open(argumente.ziel, "w", encoding="utf-8", newline="\n") as strom:
        json.dump(katalog, strom, ensure_ascii=False, indent=1)
        strom.write("\n")
    groesse = os.path.getsize(argumente.ziel)
    print(f"\nGeschrieben: {argumente.ziel} ({groesse} Bytes)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
