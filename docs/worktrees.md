# Worktrees pro Agent / Session (Parallelbetrieb)

**Warum:** Am 2026-06-21 liefen zwei Claude-Sessions gleichzeitig auf `feature/gis-karte` im
**selben** Arbeitsverzeichnis. Folge: Session A hielt die (autorisierten) Cleanup-Commits von
Session B fuer "rogue" und warf sie zweimal per `git reset` weg; B holte sie per `cherry-pick`
zurueck. Niemand hatte Schuld am Code — die Ursache war **geteilte Working-Copy + geteilter
Branch**. Ein Worktree pro Agent macht das strukturell unmoeglich.

## Regel

> **Nie zwei Agenten/Sessions gleichzeitig im selben Arbeitsverzeichnis oder auf demselben Branch.**

- **Parallele Sessions (Claude, Codex, ...):** jede in einem eigenen git-Worktree unter
  `.worktrees/<name>` auf eigenem Branch `agent/<name>`. `.worktrees/` ist gitignored.
- **Subagenten innerhalb EINER Session:** die native Isolation nutzen
  (Agent-Tool `isolation: "worktree"` bzw. `EnterWorktree`) — kein manueller Worktree noetig.

## Anlegen (Helfer)

```powershell
powershell -ExecutionPolicy Bypass -File tools/new-agent-worktree.ps1 <name> [<base-branch>]
```

Beispiel:

```powershell
powershell -ExecutionPolicy Bypass -File tools/new-agent-worktree.ps1 audit-fixes
# -> Ordner .worktrees/audit-fixes, Branch agent/audit-fixes (vom aktuellen Branch)
```

Roh (Git Bash / ohne Helfer):

```bash
git worktree add -b agent/<name> .worktrees/<name> <base-branch>
```

Danach die jeweilige Session/den Agenten **in diesem Ordner** starten.

## Mergen & Aufraeumen

```bash
# im Haupt-Checkout, wenn die Arbeit fertig & gemerged ist:
git worktree remove .worktrees/<name>
git branch -d agent/<name>
git worktree prune          # entfernt verwaiste Eintraege
git worktree list           # Kontrolle
```

## Hinweise

- Jeder Worktree teilt sich `.git` (Objekte/Refs) mit dem Haupt-Checkout, hat aber eine eigene
  Arbeitskopie und einen eigenen ausgecheckten Branch — Commits stoeren sich nicht mehr.
- Build-/Test-Artefakte (`bin/`, `obj/`) und ggf. `node_modules`/venv werden pro Worktree neu
  erzeugt; das kostet Plattenplatz, ist aber der Preis der Isolation.
- Der Sidecar bindet Port 8100 global: laufen zwei Worktrees gleichzeitig mit eigener Pipeline,
  brauchen sie unterschiedliche `SEWER_SIDECAR_PORT` (sonst Port-Konflikt wie bei den doppelten
  uvicorn-Instanzen am 2026-06-21).
