# Gemeinsame Player-Integration – 03.10.2026

## Umfang

Gemeinsame Basis: master 72d47f298. Enthalten: PR 84 (dc58f627, Codierkontexte),
PR 86 (3b9b2a462, Tastatur), PR 87 (09228856a, E2 samt Pflicht-Zeitparameter).
Zweig codex/player-integration baut auf dem geprüften PR-87-Commit auf. Die beiden
übrigen Pakete werden unverändert als gezielte Patches übernommen. Die ursprünglichen
PR- und Sicherungsnachweise bleiben erhalten. Die Einzelpakete gelten nach erfolgreicher
Integration dieses gemeinsamen PRs als geliefert. Training Center PR 85 ist getrennt.

## Konflikt und Messung

Beide Übernahmen kollidierten nur an der PlayerWindow-Grössenkonstante. Zentral am
wirklichen kombinierten Code gemessen: 73 Teildateien, zusammen 4.207 Zeilen inklusive
Leerzeilen/Kommentaren. Hauptdatei 573 Zeilen. Ausgangsmaster: 4.246/568.
Die neue Grenze 4.207 schützt die gesamte Klasse. Das Ziel unter 1.000 bleibt offen.
Die beiden neuen Helfer PlayerWindowCodingContextFactory und PlayerKeyboardPresenter
haben je 68 Zeilen. Eine überzählige EOF-Leerzeile im übernommenen Factory-Test entfernt;
keine Test-Erwartung verändert. Sonstige Quellen entsprechen den Einzelpaketen.

## Verhalten

Kontextaufbau weiterhin vor InitializeComponent, dieselben Besitzer und späte Quellen.
Tastaturaufbau weiterhin nach Wiedergabecontroller und vor den gebundenen Ereignissen.
F1/Textfokus/Handled und ursprüngliche Aktionen bleiben erhalten. Der Handeintrag reicht
LastTimestampSeconds durch; der Zeitparameter bleibt verpflichtend. Die bestehende
Application-Regel für maximal 1,5 Sekunden und der gemeinsame KI-Start (#47) bleiben
unverändert. Kein neuer Workflow unter UI/Ai, kein Paket oder gespeichertes Format.

## Prüfung und Lieferung

Zwei getrennte Sol-Agenten prüfen Anschlüsse (high) und Testnetz/Messung (medium)
nur lesend. Builds, vollständige Release-Suiten und Gitaktionen laufen zentral.
Die Ergebnisse werden nach ihrem tatsächlichen Abschluss ergänzt.
Kein echter Video-/OCR-/KI-Lauf. Hauptarbeitsordner, Kundenoriginale und laufendes
SewerStudio werden nicht verändert. Eine Übernahme nach master erfolgt erst nach
unabhängiger Prüfung, grüner CI und einer frischen GitHub-Codeprüfung des gemeinsamen Stands.

## Zentraler Releaseabschluss

Beide unabhängigen Prüfungen ohne Befund freigegeben. Vollständiger Release-Build:
0 Warnungen, 0 Fehler. Endfokus:132 bestanden, 0 übersprungen, 0 Fehler.

| Testbereich | Bestanden | Übersprungen | Fehler |
| --- | ---: | ---: | ---: |
| Infrastruktur | 8.256 | 6 | 0 |
| Pipeline | 3.048 | 3 | 0 |
| Oberfläche | 7.947 | 49 | 0 |
| ProjectModernizer | 62 | 0 | 0 |
| Gesamt | 19.313 | 58 | 0 |

Alle 18 eingefrorenen Quell-/Testhashes nach den Gesamtläufen identisch.
Architekturkarte mit dem tatsächlichen Code abgeglichen und validiert.
Ausgangsmaster vor Lieferung nochmals geprüft: unverändert72d47f298.
Push-, GitHub- und Übernahmenachweise werden getrennt auf G festgehalten.
