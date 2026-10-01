# Entwicklungs-Gate (Test-Absicherung)

Damit rote Tests keinen Push erreichen, prüft ein **pre-push-Hook** vor jedem
`git push` die vier normalen .NET-Testprojekte.

## Reproduzierbar auf jedem Klon

Der Hook liegt **versioniert** unter [`.githooks/pre-push`](../.githooks/pre-push)
(nicht nur lokal in `.git/hooks/`, das nicht getrackt wird). Nach einem frischen
Klon einmalig aktivieren:

```powershell
./tools/git-hooks/Install-PrePushHook.ps1
```

Das Skript setzt `core.hooksPath` auf `.githooks`. Es kopiert den Hook nicht;
dadurch gibt es nur eine versionierte, aktuelle Fassung. Ab dann läuft das Gate
bei jedem `git push`.

## Was das Gate prüft

- `AuswertungPro.Next.Infrastructure.Tests`
- `AuswertungPro.Next.Pipeline.Tests`
- `AuswertungPro.Next.UI.Tests`
- `ProjectModernizer.Tests`

Ist eine der vier Test-Sammlungen rot, bricht der Push ab.

## Dateisperre ist kein Codefehler

Bricht der Build mit MSB3021/MSB3027, sperrt ein laufendes Werkzeug (z. B. der
MCP-Server oder eine Dateivorschau) seine eigene exe und blockiert den Build.
Das Log sieht dann aus wie ein Compilefehler — ist aber ein Dateikonflikt.
Der Hook meldet das seit 2026-08-08 ausdrücklich: mit dem gesperrten Pfad und
dem Vorschlag, das Programm zu beenden und erneut zu pushen. Erst wenn keine
Sperrmeldung im Log steht, gilt: Tests sind rot.

## Grenzen (bewusst)

- Der Hook ist mit `git push --no-verify` umgehbar — er ist eine Bequemlichkeits-
  Absicherung für den Solo-Betrieb, kein serverseitiges Schloss. Wer echte
  Bypass-Sicherheit braucht, ergänzt später eine CI (z. B. GitHub Actions auf
  einem Windows-Runner).
- Die **KI-Pipeline gegen ein echtes Video** (GPU + Sidecar) ist hier bewusst
  **nicht** enthalten — dieser Golden-Lauf ist ein eigenes Release-Gate, siehe
  [`docs/KI-RELEASE-GATE.md`](KI-RELEASE-GATE.md).
- Die Python-Tests des **Sidecars** und die **QGIS**-Tests laufen in der CI sowie
  bei passenden Änderungen. Sie sind bewusst nicht Teil des schnellen
  Pre-Push-Hooks.
