"""
XTF (Interlis) parser for extracting damage events.
Converts XTF damage records to training-compatible event format.
"""

import xml.etree.ElementTree as ET
import json
import re
from pathlib import Path
from typing import Dict, List, Any, Optional
from dataclasses import dataclass, asdict


@dataclass
class XtfDamageEvent:
    xtf_id: str
    holding_id: str
    type_code: str
    severity: Optional[int]
    start_m: Optional[float]
    end_m: Optional[float]
    station_m: Optional[float]
    quantification1: Optional[str]
    quantification2: Optional[str]
    raw_text: Optional[str]
    source: str = "xtf_auto"


# Mapping from VSA/SIA codes to training labels
CODE_TO_LABEL = {
    # Wurzeln
    "BAA": "WURZELN",
    "BAB": "WURZELN",
    "BAC": "WURZELN",
    "BAD": "WURZELN",
    "BAE": "WURZELN",
    
    # Risse
    "BAF": "RISS",
    "BAG": "RISS",
    "BAH": "RISS",
    "BAI": "RISS",
    "BAJ": "RISS",
    "BAK": "RISS",
    
    # Anschlüsse
    "BCA": "ANSCHLUSS",
    "BCB": "ANSCHLUSS",
    "BCC": "ANSCHLUSS",
    "BCD": "ANSCHLUSS",
    
    # Ablagerungen
    "BDA": "ABLAGERUNG",
    "BDB": "ABLAGERUNG",
    "BDC": "ABLAGERUNG",
    "BDD": "ABLAGERUNG",
    
    # Versatz
    "BBA": "VERSATZ",
    "BBB": "VERSATZ",
    "BBC": "VERSATZ",
    "BBD": "VERSATZ",
    
    # Deformation
    "BEA": "DEFORMATION",
    "BEB": "DEFORMATION",
    "BEC": "DEFORMATION",
    
    # Korrosion
    "BFA": "KORROSION",
    "BFB": "KORROSION",
}


def parse_xtf_to_events(
    xtf_path: str,
    output_path: Optional[str] = None,
    holding_filter: Optional[str] = None
) -> Dict[str, Any]:
    """
    Parse XTF file and extract damage events.
    
    Args:
        xtf_path: Path to XTF file
        output_path: Optional output JSONL file
        holding_filter: Optional regex to filter holdings
    
    Returns:
        dict with parsing results and events
    """
    xtf_path = Path(xtf_path)
    
    if not xtf_path.exists():
        return {"success": False, "error": f"XTF file not found: {xtf_path}"}
    
    try:
        tree = ET.parse(xtf_path)
        root = tree.getroot()
    except ET.ParseError as e:
        return {"success": False, "error": f"Failed to parse XTF: {e}"}
    
    # Handle namespaces
    namespaces = {
        "ili": "http://www.interlis.ch/INTERLIS2.3"
    }
    
    # Find all damage/finding elements
    events: List[XtfDamageEvent] = []
    holdings_found = set()
    
    # Try to find damage elements (various XTF structures)
    for elem in root.iter():
        tag_local = elem.tag.split("}")[-1] if "}" in elem.tag else elem.tag
        
        # Look for damage/Schaden/Feststellung elements
        if tag_local in ("Schaden", "Feststellung", "Damage", "Finding", "VSA_DSS_2015_Schaden", "Kanal_Schaden"):
            event = _parse_damage_element(elem)
            if event:
                if holding_filter:
                    if not re.search(holding_filter, event.holding_id):
                        continue
                events.append(event)
                holdings_found.add(event.holding_id)
    
    # Convert to labels
    for event in events:
        event.type_code = _normalize_code(event.type_code)
    
    result = {
        "success": True,
        "xtf_path": str(xtf_path),
        "event_count": len(events),
        "holding_count": len(holdings_found),
        "holdings": list(holdings_found),
        "events": [asdict(e) for e in events]
    }
    
    # Write output if requested
    if output_path:
        output_path = Path(output_path)
        output_path.parent.mkdir(parents=True, exist_ok=True)
        
        with open(output_path, "w", encoding="utf-8") as f:
            for event in events:
                f.write(json.dumps(asdict(event), ensure_ascii=False) + "\n")
        
        result["output_path"] = str(output_path)
    
    return result


def _parse_damage_element(elem: ET.Element) -> Optional[XtfDamageEvent]:
    """Parse a single damage element from XTF."""
    
    def get_text(tag_name: str) -> Optional[str]:
        for child in elem.iter():
            tag_local = child.tag.split("}")[-1] if "}" in child.tag else child.tag
            if tag_local == tag_name:
                return child.text
        return None
    
    def get_float(tag_name: str) -> Optional[float]:
        text = get_text(tag_name)
        if text:
            try:
                return float(text.replace(",", "."))
            except ValueError:
                pass
        return None
    
    def get_int(tag_name: str) -> Optional[int]:
        text = get_text(tag_name)
        if text:
            try:
                return int(text)
            except ValueError:
                pass
        return None
    
    # Get damage code
    code = get_text("KanalSchadencode") or get_text("Schadencode") or get_text("Code") or ""
    if not code:
        return None
    
    # Get holding reference
    holding_id = get_text("Haltung_Ref") or get_text("HaltungRef") or get_text("Haltung") or ""
    
    # Get XTF ID
    xtf_id = elem.get("{http://www.interlis.ch/INTERLIS2.3}TID") or elem.get("TID") or ""
    
    return XtfDamageEvent(
        xtf_id=xtf_id,
        holding_id=holding_id,
        type_code=code,
        severity=get_int("EZS") or get_int("Schweregrad"),
        start_m=get_float("SchadenlageAnfang") or get_float("StationVon"),
        end_m=get_float("SchadenlageEnde") or get_float("StationBis"),
        station_m=get_float("LL") or get_float("Station"),
        quantification1=get_text("Quantifizierung1"),
        quantification2=get_text("Quantifizierung2"),
        raw_text=get_text("Bemerkung") or get_text("Text")
    )


def _normalize_code(code: str) -> str:
    """Normalize damage code to training label."""
    code_upper = code.upper().strip()
    
    # Direct mapping
    if code_upper in CODE_TO_LABEL:
        return CODE_TO_LABEL[code_upper]
    
    # Try prefix matching
    for prefix_len in [3, 2]:
        if len(code_upper) >= prefix_len:
            prefix = code_upper[:prefix_len]
            if prefix in CODE_TO_LABEL:
                return CODE_TO_LABEL[prefix]
    
    # Fallback
    return "UNKNOWN"


def get_label_for_code(code: str) -> str:
    """Public function to get label for a VSA code."""
    return _normalize_code(code)


def main():
    """CLI entry point."""
    import argparse
    
    parser = argparse.ArgumentParser(description="Parse XTF file to extract damage events")
    parser.add_argument("xtf", help="Path to XTF file")
    parser.add_argument("-o", "--output", help="Output JSONL file")
    parser.add_argument("--filter", help="Regex filter for holdings")
    parser.add_argument("--summary", action="store_true", help="Only show summary")
    
    args = parser.parse_args()
    
    result = parse_xtf_to_events(
        xtf_path=args.xtf,
        output_path=args.output,
        holding_filter=args.filter
    )
    
    if args.summary:
        summary = {k: v for k, v in result.items() if k != "events"}
        print(json.dumps(summary, indent=2))
    else:
        print(json.dumps(result, indent=2))
    
    return 0 if result.get("success") else 1


if __name__ == "__main__":
    exit(main())
