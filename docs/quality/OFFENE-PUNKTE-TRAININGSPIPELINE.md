# Offene Punkte der Trainingspipeline (lebende Liste)

Stand: 2026-08-05. Reihenfolge = empfohlene Abarbeitung.

1. **Quarantäne-Ordner `D:\Haltungen\06.691078-691070`** — 57 Goldsamples in
   14 Gruppen warten auf Pascals Entscheid, welche Seite (Ordnername vs.
   PDF-Dateiname) recht hat. Vorlage: `artifacts\quarantaene-entscheid-20260804\index.html`.
   Zielhaltungen sind vorab schutzfrei geprüft.
2. **43 Samples mit Platzhalter-Knoten oder foto_*-Kennung** (z. B.
   `999006-10591`, `999001-07.999003`), davon 43 im Trainingsregister.
   Nicht pauschal falsch: Es gibt echte Haltungen mit unnummeriertem Knoten.
   Vor der Release-Abnahme einzeln auflösen (Byte-Beweis wie bei den bisherigen
   Reparaturen); für Entwicklungsvergleiche tolerierbar. Beleg: Bildschutz
   byteweise gegen eval_set/eval_review geprüft, 0 Treffer (2026-08-05).
3. **Frischer Release-Holdout** — Der bisherige 400er-Holdout
   (`detect_release_holdout_45b66da2c778`) ist durch Diagnose-Nutzung und die
   belegte Kontamination (Nachtrag in `DETECT-RELEASE-DIAGNOSTIC-2026-08-03.md`)
   verbraucht. Vor jeder Modellfreigabe einen neuen, unberührten Bestand aus
   anderen Haltungen aufbauen.
4. **OSD-Entscheidung** — Eingebrannter Text (Meter, Haltung, Befundtext) kann
   vom Modell mitgelernt werden. Pilot-Protokoll auswerten (Spalten
   `osd_verdeckt_schaden` / `osd_nahe_schaden`) und dann entscheiden, ob der
   OSD-Bereich beim Training überdeckt wird. Muss vor der Massensammlung fallen.
5. **Zwei defekte Trainings-Testmodule** — `test_prepare_detect_gold.py` und
   `test_prepare_bcc_pilot.py` brechen beim Sammeln (ImportError), weil ein
   site-packages-Paket `training` das Repo-Verzeichnis schattet. Auf
   `importlib`-Dateiimport wie bei den anderen Modulen umstellen.

Erledigt 2026-08-04/05 (zur Nachverfolgung):
- PDF-Gold-Haltungs-IDs repariert (169) + 13 dekontaminiert
- gold_inbox-Pseudo-IDs repariert (75, Byte-Beweis über Kandidatenmessung)
- 9 ManualCoding-Samples mit Alt-CaseIds repariert (Byte-Beweis über
  Provenienz-PDF), darunter die beiden `1-1`-Degenerationen
- Provenance-Suffix-Regel beidseitig präzisiert

Erledigt 2026-08-06:
- Benchmark-Erweiterung `detect_benchmark_extension_v1` (17 Bilder:
  14 BAH-Testrolle + 3 BAJ-Reservierung), blind reviewt, 16/17 positiv.
  Neue Sollabdeckung: BAH 21 Boxen/≥11 Haltungen, BAJ 20, BAI 26, BCA 53 —
  alle Kandidatenklassen über der 20er-Regel.
- BAH-Verfügbarkeit belegt: 151 Haltungen (65 Gold, 47 Benchmark, 39 frei)
  — Ziel 50–70 erreichbar, Quelle nur PDF-Kanal.
