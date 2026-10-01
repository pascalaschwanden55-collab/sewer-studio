"""Gemeinsame Regel "dieselbe physische Haltung" fuer die Eval-Trennung.

Dieselbe Haltung in Gegenrichtung (``100-200`` == ``200-100``) darf nie
gleichzeitig in Trainings- und Pruefdaten stehen. Diese Regel steckte bisher in
fuenf eigenen Kopien; sie liegt jetzt nur hier. Massgeblich auf C#-Seite ist
``EvalContaminationGuard.IsEvalHaltung``; beide lesen dieselben Faelle aus
``tests/Fixtures/Haltungsidentitaet/beispiele.json``.

Bewusst NICHT hier: die Vorpruefung, ob ein Text ueberhaupt eine belastbare
Haltung ist (streng/lenient, Normalisierung). Sie ist bei den Aufrufern fachlich
verschieden und wuerde gebundene Manifeste veraendern. Diese Funktion nimmt nur
einen bereits geprueften Schluessel der Form ``links-rechts``.
"""

from __future__ import annotations


def physischer_schluessel(normalisiert: str) -> str:
    """Richtungsunabhaengiger Schluessel ``klein|gross`` aus ``links-rechts``.

    Beide Seiten werden ``casefold``-vergleichbar sortiert; ``100-200`` und
    ``200-100`` ergeben ``100|200``.
    """
    teile = normalisiert.split("-", maxsplit=1)
    if len(teile) != 2 or not all(teile):
        raise ValueError(f"Keine belastbare Haltungsidentitaet: {normalisiert}")
    return "|".join(sorted((teile[0].casefold(), teile[1].casefold())))
