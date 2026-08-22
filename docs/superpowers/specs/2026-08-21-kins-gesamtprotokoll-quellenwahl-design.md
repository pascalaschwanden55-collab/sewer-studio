# KINS-Gesamtprotokoll: Quellenwahl

Datum: 2026-08-21
Status: **Entwurf — nicht umgesetzt.** Bewusst getrennt von
`2026-08-21-wincan-quellenwahl-stopptor-design.md`.

## Warum getrennt

Beim ersten Durchgang wurde KINS mit WinCan in einen Topf geworfen („beide raten die
groesste Datei"). Das war falsch. Der Blick in den Code zeigt:

`KinsGesamtprotokollFileLocator` raet **nicht** blind. Er filtert bereits fachlich:

```csharp
name.Contains("Protokoll") && !name.Contains("Deckblatt")
```

und nimmt erst danach die groesste. Der Kommentar darueber erklaert auch warum — die
reine Auto-Wahl „groesste PDF im Archiv" wuerde bei KINS Plaene und
Dichtheitsprotokolle treffen.

Zweitens ist dieser Locator **nicht** die allgemeine KINS-Quellenwahl. Sein einziger
produktiver Aufrufer ist `ProjectImportOrchestrator` (Schritt 7b) fuer den
PDF-Seiten-Split des Ein-Knopf-Imports. Der eigentliche KINS-Datenimport laeuft ueber
`KinsImportService` und `kiDVDaten.txt`.

Drittens — und das ist der eigentliche Grund fuer die Trennung — ist die Pruefung hier
fachlich schwer. Bei WinCan lautet die Frage „hat die Datei eine Tabelle SECTION?" und
kostet Millisekunden. Bei einem PDF gibt es keine solche Tabelle.

## Was „wirklich ein Gesamtprotokoll" heissen muss

Vier Bedingungen, alle noetig:

1. **Sicher lesbar.** Kein beschaedigtes PDF, keine Verschluesselung, Text extrahierbar.
2. **Ein TV-Protokoll.** Nicht Plan, nicht Deckblatt, nicht Dichtheitspruefung. Dafuer
   existiert bereits `PdfDokumentTypErkennung` — sie ist die naheliegende Grundlage.
3. **Mindestens eine erkennbare Haltung.** Ein Protokoll ohne jede Haltungsangabe ist
   fuer den Seiten-Split wertlos.
4. **Passend zum eingelesenen Bestand.** Mindestens eine erkannte Haltung muss zu einer
   bereits importierten KINS-Haltung gehoeren. Sonst wird ein Protokoll aus einem
   fremden Auftrag zerschnitten.

Bedingung 4 erzeugt eine Reihenfolge-Abhaengigkeit: Die Haltungen muessen vor der
Quellenwahl eingelesen sein. Im Orchestrator ist das gegeben (Schritt 7b liegt nach dem
Parsen), aber es muss ausdruecklich gelten und getestet werden.

## Kosten, die WinCan nicht hat

Die Pruefung liest PDF-Text. Bei einem Archiv mit vielen PDFs kostet das spuerbar Zeit —
anders als der Blick ins SQLite-Inhaltsverzeichnis.

Konsequenz fuer den Entwurf: Die Namensregel (`Protokoll`, nicht `Deckblatt`) bleibt als
**billige Vorsortierung** erhalten. Erst die verbleibenden Kandidaten werden geoeffnet,
und die Reihenfolge sollte den wahrscheinlichsten zuerst pruefen, damit im Normalfall
nur eine Datei gelesen wird. `PdfTextPrefixReaderService` liest bereits nur einen
Anfangsteil — das ist der richtige Ansatz.

## Anschluss an Spec 1

Derselbe Baustein: `Quellenwahl.Waehle(kandidaten, pruefe)` aus
`Application/UseCases/Import/Quellen/`. Neu ist nur ein `KinsProtokollPruefer` in
`Infrastructure/Import/Kins/`, der `QuellenBefund` liefert:

| Befund | Bedeutung |
|---|---|
| `Tauglich(n)` | TV-Protokoll mit `n` erkannten, zum Bestand passenden Haltungen |
| `Leer` | lesbares TV-Protokoll ohne erkennbare Haltung |
| `Untauglich` | Plan, Deckblatt, Dichtheitspruefung — falsche Dokumentart |
| `NichtLesbar` | beschaedigt, verschluesselt, kein Text |

Die Hardware-Regel aus Spec 1 gilt unveraendert: kein KI-, Ollama- oder Sidecar-Bezug.
Der bestehende `PdfKiSchiedsrichter` bleibt davon unberuehrt — er ist ein getrennter,
optionaler Weg fuer unklare Einzel-PDFs und darf nicht in die Quellenwahl wandern.

## Offene Fragen vor der Umsetzung

Diese muessen vor dem Bauen entschieden werden — sie sind der Grund, warum dieser
Entwurf noch nicht spec-reif ist:

1. **Wie viele PDFs duerfen im schlechtesten Fall geoeffnet werden?** Ohne Obergrenze
   kann ein grosses Kundenarchiv den Import merklich verlangsamen. Eine Obergrenze muss
   im Protokoll sichtbar gemeldet werden — eine stille Kuerzung liest sich wie
   „alles geprueft".
2. **Was passiert bei mehreren tauglichen Protokollen?** Heute gewinnt die groesste.
   Nach neuer Regel gewaenne die mit den meisten passenden Haltungen. Ist das fachlich
   richtig, oder gibt es bei KINS Faelle mit mehreren gueltigen Teilprotokollen?
3. **Zaehlt ein Gesamtprotokoll ins Plausibilitaetstor?** Es liefert keine Haltungen,
   sondern verteilt PDFs. Vermutlich gehoert es NICHT in dieselbe Mengenpruefung,
   sondern hoechstens als eigene Meldung in den Bericht.
4. **Gibt es echte Messdaten?** Fuer WinCan gab es den Andermatt-Ordner als Beleg. Fuer
   KINS fehlt bisher ein Kundenordner, an dem sich ein Fehlgriff nachweisen liesse. Ohne
   solchen Beleg besteht das Risiko, eine funktionierende Regel gegen eine ungetestete
   zu tauschen.

Punkt 4 ist der wichtigste. Bei WinCan war der Fehler gemessen. Hier ist er es nicht.
