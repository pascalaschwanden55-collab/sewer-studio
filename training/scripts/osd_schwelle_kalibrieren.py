"""Bestimmt die Sicherheitsschwelle des OSD-Zeichenlesers.

WOZU
Die Regel des Projekts lautet: null falsch ist wichtiger als Abdeckung. Also
braucht das Modell eine Schwelle, unter der es lieber nichts sagt. Diese Schwelle
wird an einem GETRENNTEN Reservebestand bestimmt - dem Testteil der 897 schwach
beschrifteten Protokollbilder - und danach eingefroren.

WARUM NICHT AN GOLD
Wer die Schwelle so lange dreht, bis auf Gold null Fehler stehen, hat Gold zum
Anpassen benutzt. Die anschliessende Goldmessung waere dann keine unabhaengige
Messung mehr, sondern eine Selbstbestaetigung.

WARUM DIE SCHWACHEN ETIKETTEN HIER TAUGEN
Sie stimmen nur auf wenige Zentimeter genau (Sichtprobe: 25 von 30 auf 1 cm).
Fuer die Frage "liegt diese Lesung GROB daneben" reicht das voellig - und nur
diese Frage wird hier gestellt. Als Zeichenwahrheit fuers Training bleiben sie
gesperrt.

ZWEI MODI
"faelle" liest den TESTteil des Reservebestands (Train/Validation haben das
Modell trainiert und duerfen die Schwelle nicht mitbestimmen) ueber
osd_modell_leser.baue_modell_leser mit schwelle=0.0 - ungefiltert, damit die
tatsaechliche Sicherheit jedes Bildes sichtbar bleibt, nicht nur die Bilder, die
mit irgendeiner willkuerlichen Schwelle bereits durchgekommen waeren - und
schreibt je Bild Sicherheit, Lesung, Sollwert und Abweichung in einen
Faelle-Rohbeleg. "kalibrieren" liest genau diesen Beleg, waehlt die kleinste
Schwelle, die jeden groben Fehler aussperrt, und friert sie EINMALIG ins
Kandidatenmanifest ein - ein Kandidat mit bereits gesetzter Schwelle wird
abgewiesen, weil ein zweites Einstellen keine unabhaengige Messung mehr waere.
"""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

# Standard-Reservebestand: Testteil der 897 schwach beschrifteten
# Protokollbilder (siehe osd_wahrheit_aus_protokoll.py). 674 Train, 135
# Validation, 88 Test - nur die 88 Test-Eintraege sind hier zulaessig.
RESERVEBESTAND_STANDARD = Path(
    r"C:\KI_BRAIN\training\diagnostics\osd_wahrheit_protokoll_v1\wahrheit.json")

# Ab dieser Abweichung gilt eine Lesung als grob falsch. Deutlich ueber dem
# Zentimeter-Rauschen der schwachen Etiketten, deutlich unter einem echten
# Lesefehler (der verschiebt den Wert meist um Meter).
GROB_FALSCH_AB_M = 0.5

# Ohne jeden groben Fehler bleibt es bei diesem Wert. Nicht 0.0: Eine Lesung
# ohne jede Sicherheit soll auch dann nicht durchgehen.
GRUNDSCHWELLE = 0.25


def vergleichbare_faelle(faelle: list[dict]) -> list[dict]:
    """Faelle MIT Sollwert - nur diese sagen etwas ueber richtig/falsch aus.

    Ein Fall ohne Lesung oder ohne Sollwert (abweichung_m is None) ist weder
    ein Beleg fuer noch gegen eine Schwelle; er zaehlt bewusst nicht mit
    (Fix-Runde 1 zu Aufgabe 7: sichtbar machen, WORAUF sich die Kalibrierung
    stuetzt - "0 grobe Fehler" aus 3 vergleichbaren Faellen sieht sonst aus
    wie "0 grobe Fehler" aus 80)."""
    return [fall for fall in faelle if fall.get("abweichung_m") is not None]


def grobe_fehler(faelle: list[dict]) -> list[dict]:
    """Teilmenge von vergleichbare_faelle mit Abweichung >= GROB_FALSCH_AB_M."""
    return [
        fall for fall in vergleichbare_faelle(faelle)
        if abs(float(fall["abweichung_m"])) >= GROB_FALSCH_AB_M
    ]


def waehle_schwelle(faelle: list[dict], sicherheitsabstand: float = 0.05) -> float:
    """Kleinste Schwelle, die JEDEN groben Fehler aussperrt, plus Abstand."""
    grob = [fall["sicherheit"] for fall in grobe_fehler(faelle)]
    if not grob:
        return GRUNDSCHWELLE

    # Knapp ueber der staerksten falschen Lesung.
    schwelle = max(grob) + 1e-6
    return round(min(schwelle + sicherheitsabstand, 1.0 + sicherheitsabstand), 6)


def _atomar_schreiben(ziel: Path, text: str) -> None:
    """Schreibt text nach ziel atomar: Temp-Datei im selben Ordner, dann Replace.

    Verhindert eine halb geschriebene Datei bei einem Absturz mitten im
    Schreiben. Fix-Runde 1 zu Aufgabe 7: das Kandidatenmanifest beim
    Einfrieren ist der sicherheitskritische letzte Schritt dieses Werkzeugs
    und darf nicht halb geschrieben liegenbleiben.
    """
    ziel.parent.mkdir(parents=True, exist_ok=True)
    tmp = ziel.with_name(f".{ziel.name}.tmp")
    tmp.write_text(text, encoding="utf-8")
    tmp.replace(ziel)


# ---------------------------------------------------------------------------
# Modus "faelle": rohe Modell-Lesungen auf dem TESTteil des Reservebestands.
# Reine Logik unten (nur_testteil/abweichung_m/baue_fall/baue_faelle_dokument)
# ist ohne Modell testbar; die Inferenz selbst kommt aus osd_modell_leser.
# ---------------------------------------------------------------------------

def nur_testteil(wahrheit: dict) -> list[dict]:
    """Nur split == 'test': Train und Validation haben das Modell gesehen
    und duerfen die Schwelle nicht mitbestimmen."""
    return [e for e in (wahrheit.get("eintraege") or []) if e.get("split") == "test"]


def abweichung_m(gelesen_m: float | None, soll_m: float | None) -> float | None:
    """None wenn nichts gelesen wurde ODER kein Sollwert vorliegt, sonst der
    Absolutbetrag der Differenz."""
    if gelesen_m is None or soll_m is None:
        return None
    return abs(float(gelesen_m) - float(soll_m))


def baue_fall(eintrag_id: str, sicherheit: float, gelesen_m: float | None,
              soll_m: float | None) -> dict:
    """Ein Fall-Datensatz - genau die Eingabeform von waehle_schwelle."""
    return {
        "id": eintrag_id,
        "sicherheit": sicherheit,
        "gelesen_m": gelesen_m,
        "soll_m": soll_m,
        "abweichung_m": abweichung_m(gelesen_m, soll_m),
    }


def baue_faelle_dokument(kandidat_id: str, gewicht_sha256: str,
                          faelle: list[dict]) -> dict:
    """Reine Funktion: baut nur die Dokumentform, kein I/O."""
    return {
        "schema": "osd_schwelle_faelle_v1",
        "kandidat_id": kandidat_id,
        "gewicht_sha256": gewicht_sha256,
        "faelle": faelle,
    }


def _lade_manifest(kandidat: Path) -> dict:
    return json.loads((kandidat / "manifest.json").read_text(encoding="utf-8-sig"))


def _main_faelle(args: argparse.Namespace) -> int:
    if not args.reservebestand.is_file():
        print(f"ABBRUCH: Reservebestand fehlt: {args.reservebestand}", file=sys.stderr)
        return 2
    wahrheit = json.loads(args.reservebestand.read_text(encoding="utf-8-sig"))
    eintraege = nur_testteil(wahrheit)
    if not eintraege:
        print("ABBRUCH: Keine Testeintraege im Reservebestand.", file=sys.stderr)
        return 2

    manifest = _lade_manifest(args.kandidat)

    # Lazy: reine Logiktests dieses Skripts brauchen kein Ultralytics/Torch.
    from osd_modell_leser import baue_modell_leser

    # schwelle=0.0: keine Filterung. Wir brauchen die rohe Sicherheit JEDES
    # Bildes, nicht nur die, die eine willkuerliche Schwelle schon bestehen.
    lese = baue_modell_leser(args.kandidat, schwelle=0.0)

    basis = args.reservebestand.parent
    faelle: list[dict] = []
    gelesen = 0
    for eintrag in eintraege:
        bild_pfad = basis / eintrag["bild"]
        ergebnis = lese(bild_pfad)
        konfidenz = ergebnis.get("konfidenz_min")
        # Ohne jede erkannte Ziffer gibt es keine Zeichensicherheit zu
        # melden - das zaehlt hier als 0.0 (die niedrigste moegliche
        # Sicherheit), nicht als fehlender Fall: Der Fall bleibt Teil der
        # Kalibrierung, er kann nur nie eine Schwelle unterlaufen.
        sicherheit = float(konfidenz) if konfidenz is not None else 0.0
        gelesen_m = ergebnis.get("meter")
        if gelesen_m is not None:
            gelesen += 1
        faelle.append(baue_fall(
            eintrag["id"], sicherheit, gelesen_m, eintrag.get("soll_meter")))

    dokument = baue_faelle_dokument(
        str(manifest.get("kandidat_id") or ""),
        str(manifest.get("gewicht_sha256") or ""),
        faelle,
    )

    _atomar_schreiben(
        args.faelle_ziel, json.dumps(dokument, indent=2, ensure_ascii=False))

    print(f"Testfaelle gesehen: {len(eintraege)}")
    print(f"Gelesen: {gelesen}")
    print(f"Nicht gelesen: {len(eintraege) - gelesen}")
    print(f"Geschrieben: {args.faelle_ziel}")
    return 0


def _main_kalibrieren(args: argparse.Namespace) -> int:
    daten = json.loads(args.faelle.read_text(encoding="utf-8-sig"))
    faelle = daten.get("faelle") or []
    if not faelle:
        print("ABBRUCH: Keine Faelle im Reservebestand.", file=sys.stderr)
        return 2

    vergleichbar = vergleichbare_faelle(faelle)
    grob = grobe_fehler(faelle)
    schwelle = waehle_schwelle(faelle, args.sicherheitsabstand)

    manifest_pfad = args.kandidat / "manifest.json"
    manifest = json.loads(manifest_pfad.read_text(encoding="utf-8-sig"))
    if manifest.get("schwelle") is not None:
        print(f"ABBRUCH: Schwelle ist bereits eingefroren "
              f"({manifest['schwelle']}). Ein zweites Einstellen waere keine "
              f"unabhaengige Messung mehr.", file=sys.stderr)
        return 2

    manifest["schwelle"] = schwelle
    manifest["schwelle_quelle"] = str(args.faelle)
    manifest["schwelle_faelle"] = len(faelle)
    manifest["schwelle_faelle_vergleichbar"] = len(vergleichbar)
    manifest["schwelle_faelle_grob_falsch"] = len(grob)
    _atomar_schreiben(
        manifest_pfad, json.dumps(manifest, indent=2, ensure_ascii=False))

    print(f"Faelle gesamt: {len(faelle)}")
    print(f"Davon vergleichbar (Sollwert vorhanden): {len(vergleichbar)}")
    print(f"Davon grob falsch (>= {GROB_FALSCH_AB_M} m): {len(grob)}")
    print(f"Schwelle: {schwelle}  (aus {len(faelle)} Faellen)")
    print(f"Eingefroren in: {manifest_pfad}")
    return 0


def main(argv=None) -> int:
    p = argparse.ArgumentParser(description=__doc__)
    sub = p.add_subparsers(dest="modus", required=True)

    p_faelle = sub.add_parser(
        "faelle",
        help="Rohe Modell-Lesungen auf dem Testteil des Reservebestands erzeugen.")
    p_faelle.add_argument("--kandidat", type=Path, required=True)
    p_faelle.add_argument("--reservebestand", type=Path, default=RESERVEBESTAND_STANDARD)
    p_faelle.add_argument("--faelle-ziel", type=Path, required=True)

    p_kal = sub.add_parser(
        "kalibrieren",
        help="Schwelle aus einer Faelle-Datei bestimmen und ins Manifest einfrieren.")
    p_kal.add_argument("--faelle", type=Path, required=True)
    p_kal.add_argument("--kandidat", type=Path, required=True)
    p_kal.add_argument("--sicherheitsabstand", type=float, default=0.05)

    args = p.parse_args(argv)
    if args.modus == "faelle":
        return _main_faelle(args)
    return _main_kalibrieren(args)


if __name__ == "__main__":
    sys.exit(main())
