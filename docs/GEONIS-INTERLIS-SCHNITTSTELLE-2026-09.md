# GEONIS-Rueckschrieb ueber INTERLIS 2 / SIA 405 — Konzept und Entscheidbedarf

**Stand:** 2026-09-04
**Grundlage:** Mail Andreas Sidler (Trigonet AG) vom 04.09.2026 als Antwort auf die
Testdatei vom 03.09.2026 (eine Haltung 78998-79002, ein Schacht 78998).
**Status:** Konzept. Es ist noch nichts gebaut. Im HEAD gibt es keinen XTF-Export.

## 1 Ergebnis des ersten Tests

- Die Testdatei ist valid.
- Schacht: Abgleich sollte ohne Anpassung funktionieren.
- Haltung: zwei Punkte sind offen — Breite/Rohrprofil und der Primaerschluessel.
- GEONIS hat heute keine Interlis-2-Importschnittstelle. Trigonet wuerde eine
  FME-Workbench bauen: Start per Drag/Drop im Browser, Schreiben direkt in die
  Datenbank, kein Dialog mit dem Benutzer, auf Wunsch ein Aenderungsprotokoll.
  Aufwand rund ein Arbeitstag inklusive Test und Einfuehrung, Betrieb in der
  bestehenden Wartung, Erweiterungen nach Regietarif.

Fachlich ist das der richtige Weg. Der Aufwand ist tief. Die Risiken liegen nicht
in FME, sondern in den vier Rahmenbedingungen und im Schluessel.

## 2 Was die vorgeschlagenen Rahmenbedingungen fuer uns bedeuten

| Annahme Trigonet | Folge | Was SewerStudio liefern muss |
|---|---|---|
| Attribute sind geprueft, GEONIS validiert nicht mehr | Die gesamte Pruefpflicht liegt bei uns | Harte Exportpruefung vor dem Schreiben der XTF: Pflichtfelder, Codelisten, Wertebereiche, Einheiten. Fehler = kein Export, nicht "trotzdem liefern" |
| Kein Rueckgaengig (kein Ctrl+Z) | Ein Fehler bleibt in der Produktivdatenbank | 1. Trockenlauf mit Aenderungsliste, die ich vor dem Schreiblauf freigebe. 2. Datenbanksicherung durch Trigonet unmittelbar vor dem Lauf. 3. Start mit genau einem Projekt, nicht mit dem ganzen Netz |
| Import an der GEONIS-Funktionalitaet vorbei, direkt in die DB | Abgeleitete Plantexte und Symbolik werden nicht automatisch nachgefuehrt | Muss Trigonet klaeren. Fuer uns ist es ein Abnahmekriterium: nach dem Import stimmen die Plantexte fuer Material und Dimension |
| Attributumfang muss festgelegt werden | Ohne feste Liste schreibt der Import irgendwann Felder, die ich gar nicht beurteile | Verbindliche Liste, siehe Kapitel 5. Wir exportieren nichts ausserhalb dieser Liste |

Zusaetzliche Regel, die in der Workbench stehen muss:

> Ein fehlendes oder leeres Attribut in der XTF bedeutet **nie** "Wert in der
> Datenbank loeschen", sondern "nicht angefasst".

Ohne diese Regel loescht der erste Produktivlauf alle Attribute, die ich nicht
beurteile. Das ist der wahrscheinlichste Weg, wie so eine Schnittstelle Schaden
anrichtet.

## 3 Primaerschluessel — der kritische Punkt

Die Bezeichnung ist **kein** Schluessel. Trigonet nennt selbst ueber 1000
Duplikate. Ein Abgleich allein ueber die Bezeichnung wuerde bei jedem Duplikat
entweder die falsche Haltung ueberschreiben oder mehrere gleichzeitig.

**Vorschlag A (Ziel): OBJ_ID.**
Jedes SIA-405-Objekt traegt eine OBJ_ID. Sie stammt aus dem GEONIS-Bestand
selbst, kommt ueber den Katasterexport zu uns und wird von uns unveraendert
zurueckgeschrieben. Wichtig: der Operateur muss die OBJ_ID nicht kennen und nicht
mitfuehren. SewerStudio haelt sie aus dem Kataster-XTF und haengt sie beim Export
wieder an. Die TID im Transferfile ist dafuer ungeeignet, sie wird je Export neu
vergeben — das deckt sich mit Andreas' Beobachtung, dass die TID nicht zur
Datenbank passt.

*Frage an Trigonet:* Fuehrt GEONIS je Objekt eine `obj_id`, ist sie stabil und
eindeutig? Dann ist der Abgleich sauber und die Duplikatfrage faellt weg.

**Vorschlag B (Rueckfall, falls GEONIS keine OBJ_ID fuehrt):** zusammengesetzter
Schluessel.

- Haltung: Bezeichnung + Bezeichnung Startschacht + Bezeichnung Endschacht
  (aus Haltungspunkt/Abwasserknoten). Diese Kombination ist im Netz praktisch
  eindeutig, auch wenn die Bezeichnung allein es nicht ist.
- Schacht: Bezeichnung + Lage mit enger Toleranz (Vorschlag 0.50 m).

**Harte Regeln in beiden Varianten:**

1. Nur Update bestehender Objekte. Kein Insert, kein Delete in Phase 1.
2. Kein eindeutiger Treffer (0 oder mehr als 1) → kein Schreibvorgang, Zeile ins
   Protokoll. Nie ein "bester Treffer".
3. Das Protokoll ist Pflicht, nicht optional. Ohne Protokoll ist ein Lauf ohne
   Rueckgaengig nicht verantwortbar.

## 4 Rohrprofil, Hoehe und Breite

Trigonet braucht in der Klasse `Rohrprofil` das Attribut
`HoehenBreitenverhaeltnis`, weil GEONIS die Breite fuehrt und sie aus der
lichten Hoehe ableitet. Beim Kreisprofil ist der Wert 1.

Unser Vorschlag:

- Wir exportieren je vorkommendem Profil genau ein `Rohrprofil`-Objekt
  (Kreisprofil → `Profiltyp` Kreisprofil, `HoehenBreitenverhaeltnis` = 1) und
  referenzieren es aus der Haltung ueber `rohrprofilRef`.
- Zusaetzlich liefern wir `Lichte_Breite` direkt an der Haltung mit (beim Kreis
  gleich `Lichte_Hoehe`). Das ist bewusst redundant, damit GEONIS nicht rechnen
  muss. Die beiden Angaben muessen zueinander passen; ein Widerspruch ist ein
  Fehler bei uns und muss den Export stoppen.

*Frage an Trigonet:* Erwartet GEONIS feste Rohrprofil-Bezeichnungen aus dem
eigenen Katalog? Sonst entstehen bei jedem Import neue Profil-Dubletten in der
Profiltabelle.

## 5 Attributumfang Phase 1 (Vorschlag)

Grundsatz: Wir liefern nur Attribute, die ich fachlich aus der Kanalfernsehaufnahme
beurteile. Alles andere bleibt GEONIS-Hoheit und wird nicht angefasst.

**Haltung / Kanal**

| Attribut | Herkunft SewerStudio | Bemerkung |
|---|---|---|
| `Lichte_Hoehe` | DN aus Aufnahme/Beurteilung | Millimeter |
| `Lichte_Breite` + `Rohrprofil.HoehenBreitenverhaeltnis` | abgeleitet | Kapitel 4 |
| `Material` | Rohrmaterial | Wertebereich muss mit GEONIS abgeglichen werden |
| `Baulicher_Zustand` | Zustandsklasse | Mapping siehe unten, muss schriftlich bestaetigt werden |
| `Bemerkung` | Bemerkungen | Freitext |
| `Letzte_Aenderung` | Datum der Beurteilung | Herkunftsnachweis |

**Normschacht**

| Attribut | Herkunft SewerStudio | Bemerkung |
|---|---|---|
| `Dimension1` | groesstes Innenmass | Millimeter |
| `Dimension2` | kleinstes Innenmass | Millimeter |
| `Material` | Schachtmaterial | nur wenn beurteilt |
| `Baulicher_Zustand` | Zustandsklasse Schacht | wie oben |
| `Bemerkung` | Bemerkungen | Freitext |
| `Letzte_Aenderung` | Datum der Beurteilung | Herkunftsnachweis |

**Bewusst nicht uebernommen (Phase 1):** Geometrie und Verlauf, Hoehenlagen
(Deckel-/Sohlenkoten), Topologie und Referenzen, Nutzungsart, Eigentuemer,
Funktion, Zugaenglichkeit, Baujahr. Und: `LaengeEffektiv` bewusst nicht — die im
Video gemessene Laenge ist nicht die Katasterlaenge aus der Geometrie. Wenn wir
sie zurueckschreiben, wird die Datenbank gegenueber dem Plan inkonsistent.

**Offen: Mapping Zustandsklasse → `Baulicher_Zustand`.**
Unsere Skala folgt der VSA-Richtlinie 2023 (siehe
`docs/vsa-zustandsklassifizierung-2023-schwellen.md`): Klasse 0 ist der
schlechteste Zustand, Klasse 4 der beste. Wenn GEONIS `Z0`-`Z4` gleich meint,
ist die Abbildung 1:1 (`0` → `Z0`). Das muss vor dem ersten Produktivlauf
schriftlich bestaetigt sein — eine gedrehte Skala wuerde jede Prioritaetenliste
im WebGIS auf den Kopf stellen.

## 6 Luecken in SewerStudio (Ist-Zustand HEAD, ehrlich)

1. **Es gibt keinen XTF-Export.** Gelesen wird SIA 405 in
   `src/AuswertungPro.Next.Infrastructure/Import/Xtf/LegacyXtfImportService.cs`
   (`ParseSia405`); einen Schreiber gibt es nirgends im Code und auch nicht in der
   Historie. Die Testdatei vom 03.09. stammt nicht aus dem Programm.
2. **Die OBJ_ID wird beim SIA-405-Import weggeworfen.** Nur der VSA-KEK-Zweig
   liest `OBJ_ID` (fuer Kanalschaeden). Fuer Haltung, Kanal, Abwasserknoten und
   Normschacht muessen `OBJ_ID` und Herkunft additiv mitgefuehrt werden, sonst
   ist Vorschlag A gar nicht moeglich.
3. **Kein Rohrprofil.** Der Import kennt nur `Lichte_Hoehe`/`Lichte_Breite` und
   nutzt die Breite bloss als Ersatzwert fuer DN.
4. **Schacht-Datenhaltung passt noch nicht.** `SchachtRecord` fuehrt ein
   Textfeld `Dimension` aus dem PDF-Protokoll, nicht `Dimension1`/`Dimension2`,
   und es gibt keinen Normschacht-Import aus dem XTF.
5. **Kein Exportprotokoll und keine Exportpruefung**, die Kapitel 2 verlangt.

Reihenfolge fuer den Bau: erst 2 (klein, additiv, empty-only), dann 3/4, dann der
Exporter als eigener Service mit Interface samt Trockenlauf und Protokoll.

## 7 Offene Fragen an Trigonet (fuer das Telefon)

1. Fuehrt GEONIS je Objekt eine stabile `obj_id`? Wenn ja, ist sie der Schluessel.
2. Wenn nein: ist der zusammengesetzte Schluessel aus Kapitel 3 akzeptiert?
3. Bestaetigung: leeres/fehlendes Attribut heisst "nicht anfassen", nie loeschen.
4. Trockenlauf mit Aenderungsprotokoll vor jedem Schreiblauf — im Grundaufwand
   enthalten?
5. Datenbanksicherung vor dem Lauf: wer macht sie, wie schnell ist ein
   Ruecksetzen im Notfall?
6. Erwartete Wertelisten fuer `Material` und `Baulicher_Zustand` (inklusive
   Richtung der Skala Z0-Z4).
7. Rohrprofil-Katalog: feste Bezeichnungen aus GEONIS oder frei?
8. Was passiert mit Objekten in der XTF, die in GEONIS nicht existieren —
   ignorieren und protokollieren (unser Vorschlag) oder anlegen?
9. Kann Kilian den Lauf selbst ausloesen, und wer darf ihn ausloesen?

## 8 Naechste Schritte

1. Antwortmail und Telefon mit Trigonet (Entwurf:
   `docs/betrieb/2026-09-04-antwort-trigonet-mailentwurf.md`).
2. Nach der Klaerung des Schluessels: `OBJ_ID` im SIA-405-Import mitfuehren.
3. XTF-Export als eigener Service mit Interface, Trockenlauf und Protokoll bauen.
4. Zweiter Testlauf mit derselben Haltung plus einem bewusst gewaehlten
   Duplikatfall, damit die Schluesselregel im echten Bestand geprueft ist.
