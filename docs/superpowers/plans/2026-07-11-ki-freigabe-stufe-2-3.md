# KI-Freigabe: Umsetzung Stufe 2 und 3

Datum: 2026-07-11

## Ergebnis

Stufe 2 ist umgesetzt:

- Manuelle Befunde besitzen keinen erfundenen KI-Kontext mehr.
- Beide Session-Abschlusswege verwenden dieselbe Uebernahme-Regel.
- Die Statistik trennt KI-Kriterien, menschliche Bestaetigung, Korrektur,
  Ablehnung und offene Befunde.
- Die positive Wissensdatenbank nimmt nur `Approved` plus
  `HumanConfirmed=true` auf. Schreiben, Rebuild und Lesen pruefen das erneut.

Stufe 3 ist technisch bis auf die fachliche Schwellen-Kalibrierung umgesetzt:

- Single-Pass-Pseudo-Unsicherheit wird nicht mehr erzeugt und darf auch bei
  alten Aufrufern nicht als Freigabe-Signal zaehlen.
- Jede neue zentrale Entscheidung speichert Outcome, festen ReasonCode,
  Policy-Version, Signal-Snapshot und verwendete Schwellen.
- Vollanalyse und Coding-Session speichern zusaetzlich vorhandene Modellnamen,
  QualityGate-Version, Gate-Gewichte und Gate-Erklaerung.
- Der Begriff `verlaesslich` wurde durch `KI-Kriterien erfuellt` ersetzt, solange
  die Schwellen nicht fachlich kalibriert sind.

## Warum die Schwellen noch unveraendert bleiben

Der eingefrorene Bestand unter `C:\KI_BRAIN\eval_set` enthaelt 120 Bilder. Der
bereits sichtgepruefte Teilbestand `eval_visible_clean_eval_set` hat 57 Bilder:

| Gruppe | Anzahl |
|---|---:|
| Leerbild | 16 |
| Rohranfang/-ende (`BCD`, `BCE`) | 17 |
| Zustandsbeobachtung (`BDA`, `BDDC`) | 9 |
| BA/BB-Schadensbild | 15 |

15 sichtgepruefte Schadensbilder reichen nicht, um eine sichere
Auto-Freigabe-Schwelle zu belegen. Eine Aenderung von 0.92, 0.60 oder 0.15 waere
derzeit geraten und wuerde genau das Problem wieder einfuehren, das der Audit
beanstandet hat.

## Benoetigte fachliche Entscheidung

Empfehlung fuer die Revision des Eval-Sets:

1. `BCD` und `BCE` in einen eigenen Struktur-Test verschieben.
2. `BDA` und `BDDC` in einen eigenen Zustands-/Betriebs-Test verschieben.
3. Die Freigabe-Schwellen nur auf einem eingefrorenen, sichtgeprueften
   Schaden-Test mit BA/BB-Codes bewerten.
4. Leerbilder getrennt als Falsch-Positiv-Test behalten.

Es werden keine bestehenden Eval-Dateien geloescht oder als Training benutzt.
Die Trennung erfolgt ueber neue, versionierte Teilmengen.

## Daten fuer den Kalibrierungslauf

Jeder Fall muss mindestens enthalten:

- Fall-/Haltungs-ID und Bild-Hash
- erwarteter, menschlich bestaetigter Code oder `LEER`
- vorgeschlagener Code
- zentrale Confidence, QualityGate-Ampel und blinder KB-Abgleich
- echte Mehrfachlauf-Unsicherheit oder `null`
- Outcome, ReasonCode, Policy-Version und verwendete Schwellen
- Vision-, Text- und QualityGate-Version

Abnahme vor einer Schwellen-Aenderung:

- keine Ueberschneidung mit Trainingsdaten
- mehrere voneinander getrennte Haltungen
- Precision, Recall und Konfidenzintervall getrennt nach Schadensgruppe
- eigene Auswertung fuer Leerbilder
- Schwellen-Aenderung als separater Commit mit Vorher-/Nachher-Bericht

Bis dahin bleiben die bisherigen Schwellen bewusst als **unkalibriert** markiert.

## Verifikation

Ausgefuehrt am 2026-07-11:

- `dotnet build AuswertungPro.sln --nologo --verbosity minimal`
  - 0 Warnungen, 0 Fehler
- `dotnet test AuswertungPro.sln --no-build --nologo --verbosity minimal`
  - 8.256 bestanden
  - 1 bewusst uebersprungen
  - 0 fehlgeschlagen
