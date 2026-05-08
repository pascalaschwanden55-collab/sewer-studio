# Architecture Decision Records (ADRs)

Dieses Verzeichnis enthaelt die wichtigen Architektur-Entscheidungen
fuer SewerStudio. Jeder ADR dokumentiert:

- **Was** entschieden wurde
- **Warum** (Kontext, Alternativen)
- **Konsequenzen** (positive + negative)

## Format

Jeder ADR ist eine Markdown-Datei `NNNN-titel.md`. Status-Wert:
- `proposed` — vorgeschlagen, noch nicht entschieden
- `accepted` — entschieden, im Code umgesetzt
- `deprecated` — durch spaeteren ADR ersetzt
- `superseded` — explizit von ADR-XXXX abgeloest

## Index

| Nr | Titel | Status | Datum |
|---|---|---|---|
| [0001](0001-partial-class-refactor.md) | HoldingFolderDistributor in 16 Partials zerlegt | accepted | 2026-05-08 |
| [0002](0002-characterization-tests-vor-refactor.md) | Charakterisierungs-Tests als Sicherungsnetz vor grossen Refactors | accepted | 2026-05-07 |
| [0003](0003-sidecar-contract-tests-stub-handler.md) | Sidecar-Contract-Tests via StubHandler ohne externe Deps | accepted | 2026-05-07 |
| [0004](0004-domain-inotify-tech-debt.md) | INotifyPropertyChanged in Domain-Modellen — Tech-Debt-Akzeptanz mit Migrationspfad | accepted | 2026-05-08 |
| [0005](0005-thin-ai-c-sharp-orchestrator.md) | Thin-AI-Architektur — C# orchestriert, LLM nur fuer Text | accepted | 2026-04 |

## Workflow

1. Neuer architektonischer Punkt → ADR-Entwurf als `proposed`
2. Diskussion (oder einsame Entscheidung wenn Solo) → `accepted` oder `proposed` → verworfen
3. Wenn ein neuer ADR einen alten ersetzt: alter wird auf `superseded` gesetzt mit Verweis auf den neuen
