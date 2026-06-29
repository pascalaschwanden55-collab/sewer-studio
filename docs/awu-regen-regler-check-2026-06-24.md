# AWU Regen-Regler Check 2026-06-24

QGIS-Projekt: `D:\QGIS_V4\Abwasser_Uri.qgs`

Aktiver Layer: `Regen-Auslastung akkumuliert (DEM/Sohle)`

## Kurzbefund

Der Regler war bisher nur ein Live-Farbregler auf `i_grenz`. Die eigentliche Hydraulik und Flächenlogik kommt aus dem Ergebnislayer, nicht aus dem Regler selbst.

Aktueller Layer:

| Kennzahl | Wert |
|---|---:|
| Leitungsabschnitte total | 72'425 |
| hydraulisch beurteilbar (`q_voll_ls` + `a_acc_ha`) | 6'537 |
| nicht beurteilbar | 65'888 |
| `dir_quelle=sohle` | 9'858 |
| `dir_quelle=dig` | 62'559 |
| `dir_quelle=self` | 8 |

Die vielen nicht beurteilbaren Abschnitte sind der Hauptfehlerpunkt. Ohne `q_voll_ls` und akkumulierte Fläche kann der Regler keine ehrliche Aussage zur Überlastung machen.

## Rechenlogik

Verwendete Basis:

```text
Q_regen_acc [l/s] = psi * i [l/s*ha] * A_acc [ha]
Auslastung [-]    = (Q_regen_acc + Q_basis) / Q_voll
i_grenz [l/s*ha]  = (Q_voll - Q_basis) / (psi * A_acc)
```

Aktuell ist `psi = 0.90`, solange kein Feld `psi` oder `abflussbeiwert` im Layer vorhanden ist.

Wichtig: `A_acc` ist akkumuliert entlang des Netzes. Werte aus `Q_regen_acc` dürfen deshalb nicht über mehrere Leitungen aufsummiert werden. Für den lokalen Zufluss ist `a_local_ha` maßgebend.

## Szenarien

| Regenintensität | überlastet | hoch | ok | beurteilbar | nicht beurteilbar | lokaler Regenzufluss |
|---:|---:|---:|---:|---:|---:|---:|
| 50 l/s*ha | 1'408 | 297 | 4'832 | 6'537 | 65'888 | 130'122 l/s |
| 100 l/s*ha | 2'037 | 356 | 4'144 | 6'537 | 65'888 | 260'243 l/s |
| 150 l/s*ha | 2'450 | 429 | 3'658 | 6'537 | 65'888 | 390'364 l/s |
| 300 l/s*ha | 3'268 | 411 | 2'858 | 6'537 | 65'888 | 780'729 l/s |
| 500 l/s*ha | 3'828 | 388 | 2'321 | 6'537 | 65'888 | 1'301'215 l/s |

In QGIS wurde ein temporärer Layer `AWU Regen-Check 300 l/s ha - ueberlastet` mit 3'268 überlasteten Abschnitten erzeugt. Darin sind pro Leitung `q_regen_local_ls`, `q_regen_acc_ls`, `q_voll_ls`, `auslast_300` und `i_grenz` abfragbar.

## Geändertes Plugin

Geändert:

- `D:\QGIS_V4\AWU_Plugins\awu_regen_regler\dock.py`
- `D:\QGIS_V4\AWU_Plugins\awu_regen_regler\metadata.txt`
- aktive QGIS-Profilkopie unter `C:\Users\Besitzer\AppData\Roaming\QGIS\QGIS4\profiles\default\python\plugins\awu_regen_regler`

Sicherungen:

- `D:\QGIS_V4\Plugin_Backups\awu_regen_regler_codex_20260624_optimierung`
- `C:\Users\Besitzer\AppData\Roaming\QGIS\QGIS4\profiles\default\python\plugins\awu_regen_regler_backup_codex_20260624_optimierung`

Neue Funktionen:

- Layer werden akzeptiert, wenn `i_grenz` vorhanden ist oder `q_voll_ls` + `a_acc_ha`.
- Nicht beurteilbare Abschnitte werden offen gezählt.
- `a_local_ha` wird als lokaler Regenzufluss angezeigt.
- Optionale Basislastfelder wie `q_schmutz_ls`, `q_sw_acc_ls`, `q_fremd_ls`, `q_fremdwasser_ls` werden in der Auslastung berücksichtigt, sobald sie im Layer vorhanden sind.
- Sliderbereich erweitert auf 0 bis 600 l/s*ha.

## Nächste fachliche Optimierung

1. Fehlende `q_voll_ls` systematisch aus DN, Gefälle, Rauheit und Sohlenkoten nachrechnen. Das vorhandene `awu_hydrodim`-Plugin kann die Prandtl-Colebrook-Basis bereits.
2. Bevölkerung aus `Einwohner pro Knoten` in `q_schmutz_ls` bzw. `q_sw_acc_ls` übersetzen und im Netz akkumulieren.
3. Flächen fachlich trennen: versiegelt, teilversiegelt, Dach, Straße, Grünfläche. Dafür sind AV-Bodenbedeckung, Liegenschaften, SWISSIMAGE/Orthofoto und Zonenfelder relevant.
4. Fließrichtung je Leitung bevorzugt aus Sohlenkoten ableiten, dann aus Gefälle, zuletzt aus DEM. `dir_quelle` sollte als Qualitätsflag im Layer bleiben.
5. Regenintensität nicht nur als freier Slider, sondern als IDF-Szenario führen: Dauer, Wiederkehrperiode, Ort/Gemeinde.

