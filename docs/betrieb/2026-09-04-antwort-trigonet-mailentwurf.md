# Antwortentwurf an Trigonet (Andreas Sidler), 2026-09-04

Entwurf zum Anpassen und Versenden. Fachliche Begruendung dahinter:
`docs/GEONIS-INTERLIS-SCHNITTSTELLE-2026-09.md`.

**Betreff:** AW: Testdatei — Rückschrieb SIA 405 nach GEONIS

---

Sali Andreas

Besten Dank für die schnelle und konkrete Rückmeldung. Der Weg über eine
FME-Workbench passt aus meiner Sicht gut, und der Aufwand ist überschaubar.
Bevor wir starten, drei Antworten und drei Bedingungen von meiner Seite.

**1. Primärschlüssel**

Über die Bezeichnung möchte ich nicht abgleichen — bei über 1000 Duplikaten
riskieren wir, dass die falsche Haltung überschrieben wird.

Mein Vorschlag: Wir gleichen über die OBJ_ID ab. Die stammt aus eurem eigenen
Bestand, kommt über den Katasterexport zu mir und wird von mir unverändert
zurückgeliefert. Der Operateur muss sie nicht kennen, meine Anwendung führt sie
mit. Die TID ist dafür tatsächlich ungeeignet, sie wird pro Export neu vergeben —
das deckt sich mit deiner Beobachtung.

*Meine Frage:* Führt GEONIS je Objekt eine stabile, eindeutige obj_id?

Falls nein, wäre mein Rückfall ein zusammengesetzter Schlüssel: bei der Haltung
Bezeichnung plus Start- und Endschacht, beim Schacht Bezeichnung plus Lage mit
enger Toleranz (z. B. 0.50 m). Und in beiden Varianten die harte Regel: kein
eindeutiger Treffer, also null oder mehrere — dann wird nicht geschrieben,
sondern nur protokolliert. Nie ein "bester Treffer".

**2. Rohrprofil und Breite**

Ja, das ergänze ich. Ich gebe beim Export je Profil ein Rohrprofil-Objekt mit
HoehenBreitenverhaeltnis mit (Kreisprofil = 1) und liefere zusätzlich die
Lichte_Breite direkt an der Haltung, damit ihr nicht rechnen müsst.

*Meine Frage:* Erwartet GEONIS feste Rohrprofil-Bezeichnungen aus eurem Katalog?
Sonst entstehen bei jedem Import neue Profil-Einträge.

**3. Attribute, die ich liefern möchte**

Haltung/Kanal: Lichte_Hoehe, Lichte_Breite bzw. Rohrprofil, Material,
Baulicher_Zustand, Bemerkung, Letzte_Aenderung.

Schacht: Dimension1, Dimension2, Material, Baulicher_Zustand, Bemerkung,
Letzte_Aenderung.

Bewusst nicht: Geometrie und Verlauf, Höhenlagen, Topologie und Referenzen,
Nutzungsart, Eigentümer, Funktion, Baujahr. Ebenfalls nicht die effektive Länge —
die im Video gemessene Länge ist nicht die Katasterlänge aus der Geometrie, das
würde eure Daten gegenüber dem Plan inkonsistent machen.

*Meine Frage:* Kannst du mir die zulässigen Werte für Material und
Baulicher_Zustand geben, inklusive Richtung der Skala Z0–Z4? Bei mir ist nach
VSA-Richtlinie die Klasse 0 der schlechteste Zustand und 4 der beste. Wenn das
bei euch gleich ist, bilde ich 1:1 ab.

**Meine drei Bedingungen zum Punkt "kein Rückgängig"**

1. Ein leeres oder fehlendes Attribut in der Datei heisst "nicht anfassen", nie
   "Wert löschen". Sonst räumt der erste Produktivlauf alle Attribute ab, die ich
   gar nicht beurteile.
2. Trockenlauf mit Änderungsprotokoll, das ich vor dem Schreiblauf anschaue und
   freigebe. Das Protokoll wäre für mich Teil des Grundumfangs, nicht optional.
3. In der ersten Phase nur Aktualisieren, kein Anlegen und kein Löschen. Objekte
   aus meiner Datei, die in GEONIS nicht existieren, bitte nur protokollieren.

Dazu: Wer macht die Datensicherung unmittelbar vor einem Lauf, und wie schnell
kämen wir im Notfall zurück?

**Zu den Texten bei Material- und Dimensionsänderung**

Für mich wäre das ein Abnahmekriterium im Test: Nach dem Import müssen die
Plantexte stimmen. Gib mir bitte Bescheid, was du dazu herausfindest.

**Nächster Schritt**

Wenn das für dich passt, erstelle ich eine zweite Testdatei: dieselbe Haltung wie
bisher, plus bewusst einen Fall aus deinen Duplikaten. Dann sehen wir die
Schlüsselregel gleich am echten Bestand.

Ich melde mich diese Woche telefonisch für die Details.

Freundliche Grüsse
Pascal

---

## Notiz fuer mich

Der Rueckschrieb ist im Programm gebaut (`tools/GeonisXtfExport`, siehe
`docs/GEONIS-INTERLIS-SCHNITTSTELLE-2026-09.md`, Kapitel 6). Die zweite Testdatei
kommt also aus dem Programm und nicht mehr von Hand.

Vor dem Versand an Trigonet:

1. `dotnet build AuswertungPro.sln` und `dotnet test AuswertungPro.sln`.
2. Trockenlauf gegen die echte Kataster-XTF, Protokoll lesen.
3. Erzeugte Datei mit ilivalidator pruefen.
