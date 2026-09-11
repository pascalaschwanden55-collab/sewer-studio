# ?bernahme der Nova-Listen und Grafiken am 08.09.2026

Der gepr?fte Nova-Stand ist im Hauptarbeitsordner `C:\Sewer-Studio_KI_4.5` angekommen.
Merge: `0d3ea8d89`, Branch `feature/eval-pruefsatz-review`, Quelle `559473200`.
SewerStudio war vor dem Merge geschlossen; ein produktiver Start wurde nicht ausgef?hrt.

## Bisherige Arbeiten erhalten

Vor dem Merge wurden alle 176 ungesicherten Dateien (33 461 821 Bytes) in einer ZIP-Datei
und zus?tzlich in einem Git-Stash gesichert. Die ZIP-Inhalte wurden mit SHA-256 gepr?ft.
Nach dem Wiedereinspielen sind 172 Dateien wieder bytegleich zum Ausgangsstand;
bei vier Dateien wurden Nova und die bisherigen ?nderungen kombiniert. Ihre ?nderungszeilen
gegen?ber dem jeweiligen Ausgangsstand sind gleich geblieben. Dazu geh?ren CLAUDE.md,
ShellViewModel.cs, SchaechtePageViewModel.cs und SchaechtePage.xaml.cs.

Nur in CLAUDE.md entstand eine Text?berschneidung: Der Nova-Abschnitt und die Dokumentation
der Projektwechsel-Korrektur wollten an dieselbe Stelle. Beide Abschnitte bleiben erhalten.
Die bestehenden XTF-, Projektwechsel- und Auditdateien wurden nicht mitcommittet.
Der Stash bleibt als zus?tzliche Sicherung bestehen und ist bereits wieder eingespielt.

Lokale Sicherung: `.tmp/nova-merge-20260908-154344/arbeitsstand.zip`;
Pr?fnachweis daneben: `manifest.json` und `wiederherstellung.json`.

## Pr?fung nach dem Merge

- Vollst?ndiger Debug-Build: **0 Warnungen, 0 Fehler** ?
  [Protokoll](nachweise/merge-debug-build.txt).
- Oberfl?chen-, Listen-, Architektur- und Projektwechseltests: **295 bestanden**,
  7 Kindprozess-Einstiege ?bersprungen, 0 Fehler ? [Protokoll](nachweise/merge-ui-tests.txt).
- XTF-?nderungslieferung, Bauwerksarten und Projektzuordnung: **28 bestanden**, 0 Fehler ?
  [Protokoll](nachweise/merge-infrastruktur-tests.txt).
- [Maschinenlesbare Zahlen](nachweise/merge-tests.json).

Die Abweichungen und Abnahmegrenzen im [Pr?fbericht](ABNAHME.md) bleiben bestehen.
Die Pr?fung des kombinierten lokalen Standes umfasst auch die uncommitteten Vorarbeiten;
auf GitHub werden ausschliesslich die committeten Nova-?nderungen und diese Dokumentation ver?ffentlicht.

## Ver?ffentlichung

Ziel ist der vorhandene GitHub-Branch `origin/feature/eval-pruefsatz-review`.
Der Programmstand wurde erfolgreich hochgeladen. `git ls-remote` best?tigte auf diesem Branch
`0d3ea8d896ebeef4165460ca969e025edb136962`.

Der unver?nderte projektweite Vor-Push-Hook f?hrte alle vier Testgruppen nochmals aus:

| Testgruppe | Bestanden | Fehler | ?bersprungen |
|---|---:|---:|---:|
| Infrastructure | 6316 | 0 | 6 |
| Pipeline | 2644 | 0 | 3 |
| UI | 6872 | 0 | 17 |
| ProjectModernizer | 62 | 0 | 0 |
| **Summe** | **15894** | **0** | **26** |

[Protokoll des Pushs und aller vier Testgruppen](nachweise/merge-push-pruefung.txt).
Auch der zuvor zeitweise rote Nachschlag-Men?test bestand in diesem Debug-Lauf.
Das ist keine gezielte Reparatur dieses Tests und widerlegt seine fr?heren Zeit?berschreitungen
nicht. Kein Testfilter und keine Umgehung des Vor-Push-Hooks wurden verwendet.

Dieser zus?tzliche ?bernahmebericht und seine neuen Protokolle liegen nur lokal und sind nicht
committet oder ver?ffentlicht. Die automatische Freigabepr?fung lehnte ihren gesonderten Upload
ab, weil sie keine ausdr?ckliche Freigabe f?r diese zus?tzlichen Daten im ?ffentlichen Repository
erkannte. Der vorher bereits best?tigte Push des Programmstands ist davon nicht betroffen.
