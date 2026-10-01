# Erster Bildvergleich des Codiermodus – 20.09.2026

## Was umgesetzt ist

`tools/CodingReplay` bereitet geprüfte Bilder vor und führt den bestehenden
Mehrmodell-Einzelbildweg mit nachvollziehbaren Zwischenschritten aus. Die
Anleitung steht in [README](../../tools/CodingReplay/README.md). Produktionsregeln,
aktive Gewichte, Projektdateien und Kundenoriginale wurden nicht geändert.
Training bleibt gestoppt.

32 historische, von Hand geprüfte Bilder wurden samt SHA-256 übernommen.
Alle 32 konnten mit eindeutiger Nennweite und Haltungslänge verbunden werden.
Die Referenzcodes werden erst nach der Analyse verglichen.

## Durchgeführte Läufe

Alle Ausgaben liegen unter
`C:\Users\Besitzer\Documents\SewerStudio-KI-Strategie-20260920\Messungen`.

| Laufordner | Umfang | Bis zur weiteren Analyse | Ohne Meterwert |
|---|---:|---:|---:|
| `lauf-20260920-132819-89e33941` | erste 6 Bilder | 4 | 2 |
| `lauf-20260920-133513-35e37886` | erste 12 Bilder | 7 | 5 |
| `lauf-20260920-133809-24a1dffd` | dieselben 12 wiederholt | 7 | 5 |

Die letzten beiden Läufe stimmen im Meter-/Code-Ergebnis überein. Der letzte
enthält zusätzlich die tatsächlich an Qwen übergebenen OSD-Suchbilder. Die
Erweiterungen betrafen die Messspur, nicht Erkennungsregeln oder Parameter.

Modell für Meterlesen: `qwen3-vl:8b-q8`, Kontext 8192, produktives 8-Sekunden-Limit.
Ollama-Digest vor der Wiederholung:
`e7ede54bc4ba32796beb2f4466ffc9b9a667af096ae6ac1b215c310fd84a3e36`.
Klassifikator: `vsa_cls_v5_nocrop`, letterbox 1024,
SHA-256 `121134583eeb7b175047ee98b0cf9493cb93cc231971ad1c44a518def676bd80`.
DINO/SAM liefen lokal. Der alte YOLO-Detektor blieb gemäß bestehender
Qualifikationssperre ausgeschaltet. Kein Einsatz des großen 27B-Modells.

## Was die kleine Stichprobe zeigt

1. Beim ersten Lauf fehlten zwei Meterantworten nach etwa acht Sekunden. In den
   beiden Zwölferläufen antwortete Qwen auf fünf Bildern ausdrücklich `null`.
   Dort lag kein Zeitablauf vor. Zwei dieser Suchbilder wurden angesehen: die
   Werte 11.30 und 30.94 sind in ihnen sichtbar. Der Meterleser ist deshalb ein
   konkreter Prüfpunkt. Die vollständige App kann zusätzlich Timeline-/Verlaufswerte
   verwenden; deren Wirkung ist hier nicht geprüft.
2. Bei den sieben ausgewerteten Bildern entstand kein Vorschlag mit dem
   Referenzcode oder dessen Hauptgruppe. Zwei Bilder erhielten stattdessen `BCC`
   aus dem gemischten DINO-Label `pipe defect connection pipe bend`; ihre Referenzen
   sind `BAIZ` und `BAHC`. Andere Labels wie `joint defect` oder
   `water ingress hole side opening` wurden keinem Code zugeordnet.
3. Das belegt eine Lücke zwischen Bildantwort und passendem Vorschlag. Es beweist
   noch nicht, dass bloß zusätzliche Wortzuordnungen den richtigen Schaden liefern.
   Mehrdeutige Labels dürfen nicht blind zu Schadenscodes werden. Klassifikator,
   DINO-Prompts und Codeauflösung müssen gemeinsam anhand der Bilder geprüft werden.

Die zwölf Bilder sind die ersten Fälle in der festgeschriebenen Reihenfolge und
alle positiv beurteilt. Keine repräsentative Stichprobe, keine Negativfallquote,
keine unabhängige Freigabe und keine Ereignis-Trefferquote für Videos. Zwei
technische OSD-Ausfälle aus dem ersten Lauf wurden nicht als schadensfrei gewertet.
Auch späteres `null` ist ein nicht auswertbarer Fall, kein negativer Schadensbefund.

## Nächster gezielter Versuch

Zuerst den Meterleser mit den vorhandenen OSD-Beurteilungen und gebundenen
Eingabebildern messen. Originalablauf gegen genau eine Änderung an Suchbild oder
Prompt vergleichen; Lesefehler, Enthaltungen, falsche Zahlen und Zeit getrennt
zählen. Historische Entwicklungsfälle und frische Abnahme getrennt halten.
Kein Übernehmen der Sollwerte in die Schadensanalyse.

Danach die mehrdeutigen DINO-Antworten und verlorenen Codezuordnungen auf denselben
Bildern untersuchen. Den tatsächlichen Vorschlag und seine Belege vergleichen;
keine erfundene Vervollständigung fehlender Schadensinformation.

Erst nach einer belegten Verbesserung folgt der vollständige Videoablauf mit
Bildauswahl, Vorabdurchlauf und zeitlicher Ereigniszusammenführung. Neues Training
ist noch nicht begründet oder begonnen.

## Prüfung und offene Grenzen

Neue Tests schützen Bildhash, Pflichtkontext, Zeitlimit, Abbruch, Einzelfehler,
Schreibfehler, Codevergleich, reale Grenzereignisregeln und Nicht-Überschreiben.
Pipeline- und UI-Tests sowie der schnelle Release-Build wurden ausgeführt.
Detailzahlen stehen im Arbeitsbericht im Strategieordner. Architekturkarte und
`CLAUDE.md` wurden abgeglichen; Skill-Validator besteht.

Der lokale Vorschau-Server wurde von der automatischen Freigabe blockiert
(ausgegebener Grund: `blocked by policy`). Deshalb keine vollständige Browser-
Sichtprüfung. HTML-Datei, Bildverweise und OSD-Eingabebilder werden lokal geprüft.
Sidecar-Gewichte sind noch nicht vollständig gebunden. Es gibt noch keine
Jobwarteschlange, Wiederaufnahme oder automatische Modellfreigabe.
