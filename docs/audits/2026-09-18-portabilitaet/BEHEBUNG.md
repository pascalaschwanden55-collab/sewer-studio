# Portabilitaet: Behebung des Auditbefunds 04

Stand 18.09.2026. Drittes Reparaturpaket nach `2026-09-18-dateischutz` und
`2026-09-18-videoauswertung`.

## Befund

`Projekt portabel machen` bog mehrere verschiedene PDF-Verweise derselben Haltung auf
EINE lokale Datei um. Die Originaldokumente blieben erhalten, waren danach aber falsch
zugeordnet — drei Umstellungen, null ungeklaerte Verweise, keine Meldung.

## Ursache — zwei Luecken, nicht eine

1. **Der Inhaltsvergleich galt nur fuer Fotos.** Die Bedingung lautete
   `copyExternalInto != null && ... && !SameFileContent(...)`. Video (`Link`), `PDF_Path`
   und `PDF_All` rufen `ResolvePortable` aber mit `copyExternalInto: null` auf; bei ihnen
   entfiel der Vergleich vollstaendig.
2. **Dieselbe Kandidatin durfte beliebig oft vergeben werden.** Findet
   `PickHoldingMatch` keinen Namenstreffer, faellt `PickPreferred` auf die Datei mit dem
   kuerzesten Namen zurueck. Zwei verschiedene Verweise landeten so auf derselben Datei.

## Korrektur

- Der Inhaltsvergleich gilt jetzt fuer **alle** Medientypen, sobald die Quelle noch
  existiert. Weicht der Inhalt ab, wird nicht umgebogen: Fotos werden wie bisher
  kollisionssicher ins Projekt kopiert, die uebrigen Verweise bleiben unveraendert und
  erscheinen als `nicht aufgeloest`.
- Neu `Infrastructure/Import/PortableTargetAssignments.cs`: Je Haltung darf eine
  Projektdatei nur EINEM Quellverweis gehoeren. Derselbe Verweis darf sie mehrfach
  beanspruchen — `PDF_Path` und ein Eintrag in `PDF_All` nennen oft dasselbe
  Hauptprotokoll.
- Nebenbefund: `RelinkFieldList` liest `PDF_All` ueber den gemeinsamen
  `StoredFileListParser` (Semikolonliste ODER gespeicherte JSON-Liste). Das gespeicherte
  Format bleibt erhalten — eine JSON-Liste geht als JSON zurueck, damit ein Semikolon im
  Dateinamen nichts zerreisst.

## Bewusst NICHT geaendert

Der legitime Normalfall bleibt: Ein einzelner Verweis auf eine nicht mehr vorhandene
Quelle zeigt weiterhin auf die umbenannte Projektkopie. Genau das macht die Verteilung
mit ihrem Datumspraefix (`H_22149-3.01.mpg` -> `20260616_22149-3.01.mpg`). Eine Regel
"nur bei Namensgleichheit" haette diesen Weg zerstoert; der Bestandstest
`MakePortable_AbsoluteExternalVideoLink_RelinksToHoldingCopyRelative` haelt ihn fest.

**Offene Fachentscheidung:** Fehlen zwei Quellen und existiert nur eine Kandidatin, erhaelt
sie der in der gespeicherten Reihenfolge erste Verweis; der zweite bleibt extern und wird
gemeldet. Gar nichts zuzuordnen waere strenger, liesse den haeufigen Normalfall aber
unportabel. Diese Wahl ist bewusst und kann verschaerft werden.

## Nachweis

- Neue Tests `ProjectPortabilityPdfIdentityTests` (4): zwei vorhandene fremde PDFs, zwei
  fehlende fremde PDFs, Bestandsschutz des Einzelverweises, JSON-Liste.
- Rot-Lauf vor der Korrektur: 3 von 4 rot (der Bestandsschutz-Test war von Anfang an gruen).
- **Sabotageprobe**: Inhaltsvergleich wieder an `copyExternalInto` gebunden und
  `TryClaim` entfernt -> beide Zuordnungstests rot, die uebrigen gruen. Danach bytegleich
  zurueckgenommen.
- Testlauf je Projekt mit eigenem Ausgabeordner:

  | Projekt | Ergebnis |
  |---|---|
  | Infrastructure | 6783 gesamt, 0 Fehler, 6 uebersprungen |
  | UI | 7354 gesamt, 0 Fehler, 29 uebersprungen |
  | Pipeline | 2691 gesamt, 0 Fehler, 3 uebersprungen |
  | ProjectModernizer | 62 gesamt, 0 Fehler |

## Grenzen

- Der Inhaltsvergleich braucht eine erreichbare Quelle. Fehlt sie, schuetzt nur noch die
  Eindeutigkeit der Zuordnung.
- Eine PDF-Aufteilung (Sammelbericht -> Einzelprotokoll) ist inhaltlich nie identisch und
  kann durch diesen Weg nicht belegt werden; solche Verweise bleiben extern und werden
  gemeldet. Ein Herkunftsnachweis ueber die gespeicherte Importzuordnung waere der
  naechste Ausbauschritt.
- Keine Sichtprobe im laufenden Programm.
