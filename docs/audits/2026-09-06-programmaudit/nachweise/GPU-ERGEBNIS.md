# GPU-Versuch am 06.09.2026

**Ergebnis: keine erfolgreiche GPU-Abnahme. Ursache des Hängers offen.**

Gestartet um 10:18:21 Uhr, Europe/Zurich, mit der vorhandenen Python-3.12-Umgebung und RTX 5090. Aufruf: `pytest -m gpu -q` mit eigenem Testordner. Hugging-Face-/Transformers-Netzzugriffe waren ausdrücklich abgeschaltet; keine Modelle wurden heruntergeladen.

Die beiden vorhandenen GPU-Schematests rufen Grounding DINO und SAM mit einem selbst erzeugten grauen Bild auf. Nach mehr als elf Minuten lag noch kein Testergebnis vor; die Logdatei war leer. Der eigene Python-Testprozess verbrauchte weiter CPU-Zeit. Daraus lässt sich kein bestandener Modellstart und auch keine bestimmte Fehlerursache ableiten.

Die automatische Freigabeprüfung lehnte das erzwungene Beenden ab. Begründung: Die Projektregel erlaube nur das Beenden eines hängengebliebenen `testhost`. Deshalb wurden die Python-Prozesse **nicht beendet**. Eine ausdrückliche Zustimmung des Nutzers wurde angefragt.

Zu diesem Audit gehören die anhand ihrer Startzeile geprüften Prozesse **45000** und **47204**. Die Startzeilen enthalten `-m pytest -m gpu` und `programmaudit-2026-09-06/pytest-gpu`. Vor einem späteren Beenden erneut prüfen, dass diese IDs noch genau dieselben Prozesse bezeichnen. Die laufende SewerStudio-Instanz und andere Python-Prozesse sind nicht betroffen.

Nächster Prüfschritt: Nach Freigabe den eigenen Test beenden, den Hängepunkt mit Zeitlimit und Thread-Aufzeichnung eingrenzen und den Modellstart getrennt von der fachlichen Erkennungsqualität prüfen.
