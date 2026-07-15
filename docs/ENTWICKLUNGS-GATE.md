# Entwicklungs-Gate (Test-Absicherung)

Damit rote Tests keinen Push erreichen, prüft ein **pre-push-Hook** vor jedem
`git push` die Infrastructure-, Pipeline- und UI-Tests.

## Reproduzierbar auf jedem Klon

Der Hook liegt **versioniert** unter [`.githooks/pre-push`](../.githooks/pre-push)
(nicht nur lokal in `.git/hooks/`, das nicht getrackt wird). Nach einem frischen
Klon einmalig aktivieren:

```bash
git config core.hooksPath .githooks
```

Ab dann läuft das Gate bei jedem `git push`. Ohne diese Zeile greift der Hook
nicht — deshalb steht sie hier dokumentiert.

## Was das Gate prüft

- `AuswertungPro.Next.Infrastructure.Tests`
- `AuswertungPro.Next.Pipeline.Tests`
- `AuswertungPro.Next.UI.Tests`

Ist eine der drei Test-Sammlungen rot, bricht der Push ab.

## Grenzen (bewusst)

- Der Hook ist mit `git push --no-verify` umgehbar — er ist eine Bequemlichkeits-
  Absicherung für den Solo-Betrieb, kein serverseitiges Schloss. Wer echte
  Bypass-Sicherheit braucht, ergänzt später eine CI (z. B. GitHub Actions auf
  einem Windows-Runner).
- Die **KI-Pipeline gegen ein echtes Video** (GPU + Sidecar) ist hier bewusst
  **nicht** enthalten — dieser Golden-Lauf ist ein eigenes Release-Gate, siehe
  [`docs/KI-RELEASE-GATE.md`](KI-RELEASE-GATE.md).
