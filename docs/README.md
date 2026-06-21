# SewerStudio Dokumentation

Einstieg in die projektinterne Dokumentation. Der produktive Code liegt in `src/`,
Tests in `tests/`, der Python-Sidecar in `sidecar/`.

- `VSA-Regelwerk-KI-Pipeline.md` — aktueller Architektur- und Fachstand (massgeblich).
- `kostenberechnung.md` — Kostenberechnungs-/Offerten-Modul der .NET/.Next-App.
- `superpowers/plans/` — Umsetzungsplaene (task-basiert, Checkbox-Tracking).
- `superpowers/specs/` — Design-Spezifikationen zu den Plaenen.
- `cleanup/` — Projektordner-Aufraeum-Inventar (2026-06-21).
- `audits/` — historische Audits; nur behalten, solange nicht in aktuelle Plaene ueberfuehrt.

Alte PowerShell-Prototyp-Doku (README_v2/START/ARCHITECTURE/DATEIEN_MANIFEST/
LIEFERUEBERSICHT/RELEASE_NOTES/CODE_AUDIT_REPORT) wurde 2026-06-21 entfernt — sie
beschrieb die abgeloeste `.ps1`-Anwendung.

## Lokale Daten

Grosse lokale Daten, Trainingslaeufe und Rohdaten liegen nicht im Repo-Root.
Standard-Ablage fuer lokale Archive: `C:\KI_BRAIN\SewerStudio_LocalArchive_YYYYMMDD`.
Aufraeum-Quarantaene (reversibles Backup): `C:\tmp\SewerStudioCleanupQuarantine\<stamp>`.
