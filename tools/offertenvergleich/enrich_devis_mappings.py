"""
Reichert devis_mappings.json mit Marktpreisen aus marktpreise_burglen_2026.json an.

Strategie:
- Pro Mapping-Eintrag wird die passende Markt-Position via Bezeichnungs-Heuristik gefunden
- Median-Preis ueberschreibt referenzpreis (= Marktrealitaet)
- Min/Max werden als Marktpreis-Bandbreite ergaenzt
- SubmissionPos wird gesetzt (Pos-Code aus Submissions-Struktur)

Input:
  Knowledge/sanierung/marktpreise_burglen_2026.json
  src/AuswertungPro.Next.UI/Config/devis_mappings.json
Output:
  src/AuswertungPro.Next.UI/Config/devis_mappings.json (in-place)
  src/AuswertungPro.Next.UI/Config/devis_mappings.bak_<timestamp>.json (Backup)
"""
import json
import re
import shutil
import sys
from datetime import datetime
from pathlib import Path

ROOT = Path(r"C:/Sewer-Studio_KI_4.2")
PRICES = ROOT / "Knowledge/sanierung/marktpreise_burglen_2026.json"
MAPPINGS = ROOT / "src/AuswertungPro.Next.UI/Config/devis_mappings.json"

# Bezeichnungs-Schluesselwort -> Submissions-Marktposition (Pos-Code, Synonyme)
# Heuristik: matcht devis_mappings.bezeichnung (Lowercase, Substring) gegen Markt-Bezeichnung.
HEURISTICS = [
    # Schlauchlining (Renovierung / Pos. 600)
    (r"schlauchlin|relining|inliner", "schlauchliner einbauen", "600"),
    # Kurzliner / Partliner (Reparatur / Pos. 500)
    (r"kurzliner|partliner|kurzlin", "partliner / kurzliner", "500"),
    # Roboter / Wurzelfraesen (Reparatur / Pos. 500)
    (r"roboter|wurzel|fraes", "roboter-reparaturen", "500"),
    # Reinigung HD (Pos. 2.1.1 / 200)
    (r"hd-spulung|hd-spülung|reinigung vor|hochdruck", "reinigung vor sanierung", "2.1.1"),
    # TV-Inspektion (Pos. 2.1.2 / 200)
    (r"tv-inspektion vor|referenzaufnahme", "tv-inspektion vor sanierung", "2.1.2"),
    # Dichtheit / Schacht (Pos. 800/700)
    (r"dichtheit|sia 190", "dichtheitsprüfung luft", "800"),
    # Anschluss (auffraesen, Liner-Anschluesse)
    (r"anschluss erneuern|anschluss dn", "seitliche anschlüsse", "600"),
    # Schachtsanierung (Pos. 700)
    (r"schacht|kontrollschacht", "schacht", "700"),
]


def normalize(s: str) -> str:
    return re.sub(r"\s+", " ", (s or "").lower().strip())


# Plausibilitaets-Mindestschwellen pro Einheit (CHF) - filtert Anzahlen/Faktoren raus
MIN_EP_BY_UNIT = {
    "m": 5.0, "m1": 5.0, "m2": 10.0, "m3": 20.0,
    "st": 50.0, "stk": 50.0, "stck": 50.0,
    "h": 30.0,
    "gl": 100.0, "pau": 100.0, "gl/pau": 100.0,
    "kg": 1.0, "lt": 1.0, "tag": 200.0,
}


def is_plausible_ep(ep: float, einheit: str) -> bool:
    """True wenn EP ueber Mindest-Schwelle fuer die Einheit liegt (sonst vermutlich Anzahl/Faktor)."""
    key = normalize(einheit).replace("/", "/")
    return ep >= MIN_EP_BY_UNIT.get(key, 5.0)


def find_market_match(bezeichnung: str, einheit: str, market_prices: list) -> dict | None:
    """Findet den besten Markt-Eintrag fuer eine Mapping-Position via Heuristik + Einheit-Match."""
    bez_norm = normalize(bezeichnung)
    einheit_norm = normalize(einheit).replace("m1", "m")

    for pattern, market_keyword, pos_hint in HEURISTICS:
        if not re.search(pattern, bez_norm):
            continue
        # Plausibel = Median > Mindest-Schwelle (filtert "Anzahl als EP"-Faelle).
        candidates = [
            mp for mp in market_prices
            if market_keyword in normalize(mp["bezeichnung"])
            and normalize(mp["einheit"]).replace("m1", "m") == einheit_norm
            and is_plausible_ep(mp["ep_median"], mp["einheit"])
        ]
        if not candidates:
            candidates = [
                mp for mp in market_prices
                if market_keyword in normalize(mp["bezeichnung"])
                and is_plausible_ep(mp["ep_median"], mp["einheit"])
            ]
        if candidates:
            # Hoechster Median bei n>=2 ist verlaesslichster (mehr Anbieter)
            best = max(candidates, key=lambda x: (x["ep_anzahl"], x["ep_median"]))
            best["_matched_pos_hint"] = pos_hint
            return best
    return None


def main():
    sys.stdout.reconfigure(encoding="utf-8")

    if not PRICES.exists():
        print(f"FEHLER: {PRICES} fehlt - zuerst parse_offertenvergleich.py ausfuehren.", file=sys.stderr)
        sys.exit(1)

    market_data = json.loads(PRICES.read_text(encoding="utf-8"))
    market_prices = market_data["marktpreise"]
    print(f"Geladen: {len(market_prices)} Marktpreise")

    mappings_data = json.loads(MAPPINGS.read_text(encoding="utf-8-sig"))
    mappings = mappings_data["mappings"]

    # Backup
    timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
    backup = MAPPINGS.with_name(f"devis_mappings.bak_{timestamp}.json")
    shutil.copy2(MAPPINGS, backup)
    print(f"Backup: {backup.name}")

    # Anreichern
    enriched_count = 0
    for mapping in mappings:
        for pos_list_name in ("baumeisterPositionen", "rohrleitungsbauPositionen"):
            for pos in mapping.get(pos_list_name, []):
                bez = pos.get("bezeichnung", "")
                einheit = pos.get("einheit", "")
                match = find_market_match(bez, einheit, market_prices)
                if match:
                    pos["submissionPos"] = match["_matched_pos_hint"]
                    pos["marktpreis"] = {
                        "min": match["ep_min"],
                        "median": match["ep_median"],
                        "max": match["ep_max"],
                        "anzahl": match["ep_anzahl"],
                        "quelle": "Submission Bürglen 2026 (n=3 Anbieter)",
                    }
                    # Referenzpreis auf Median anheben (= aktueller Marktpreis)
                    old = pos.get("referenzpreis")
                    pos["referenzpreis"] = match["ep_median"]
                    enriched_count += 1
                    print(f"  [{mapping['id']}] {bez[:45]:45} ({einheit}): "
                          f"alt={old} -> Median {match['ep_median']:.2f} CHF "
                          f"(n={match['ep_anzahl']}) [Pos. {match['_matched_pos_hint']}]")

    mappings_data["_meta"] = {
        "marktpreis_quelle": "Submission Bürglen 2026 - Abwasser Uri",
        "stand": "2026-04-28",
        "anzahl_marktpreise": enriched_count,
        "hinweis": "Referenzpreise basieren auf Median der 3 Anbieter (Fretz, GKS, iTS).",
    }

    MAPPINGS.write_text(
        json.dumps(mappings_data, ensure_ascii=False, indent=2),
        encoding="utf-8",
    )
    print(f"\n{enriched_count} Positionen mit Marktpreisen angereichert.")
    print(f"Aktualisiert: {MAPPINGS}")


if __name__ == "__main__":
    main()
