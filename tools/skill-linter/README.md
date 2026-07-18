# Skill-Linter

Prueft SewerStudio-Skills gegen bekannte Fehlermuster (alte Pfade, tote Klassen,
falsche Sidecar-Routen, nicht existente Modelle, kaputte Zeichencodierung) und
validiert das Frontmatter.

## Nutzung

```bash
python tools/skill-linter/skill_lint.py "C:\Users\Besitzer\.claude\skills"
python tools/skill-linter/skill_lint.py "C:\Users\Besitzer\.codex\skills"
```

## Exit-Codes

| Code | Bedeutung |
|------|-----------|
| 0 | sauber |
| 1 | Altbegriffe/Funde vorhanden |
| 2 | Pruefung nicht moeglich (kaputtes/unbekanntes Format) — **Vorrang** vor 1 |

## Regeln

- Muster stehen in `forbidden.json` (abgeleitet aus `docs/SYSTEM-FAKTEN.md`, Abschnitt 8).
- Ein Treffer ist **kein** Fund, wenn die Zeile eine Negation/Meta enthaelt
  (`niemals`, `nicht`, `veraltet`, `entfernt`, `existiert nicht`) oder den Marker
  `<!-- lint-ok: grund -->`.
- Ordner mit `-archiv`, `_archiv`, `.system` werden ignoriert.
- Fehlendes/kaputtes Frontmatter (`name`/`description`) => Exit 2.

## Grenzen

Ein Text-Linter findet nur **bekannte** Altfehler. Pruefungen der zentralen Fakten
(Routen/Klassen) gegen den echten Code sind ein Folgeschritt, hier noch nicht enthalten.

## Tests

```bash
python -m pytest tools/skill-linter -q
```
