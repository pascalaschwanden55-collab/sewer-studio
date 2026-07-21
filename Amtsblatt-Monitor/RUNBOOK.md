# Amtsblatt-Uri-Monitor — Wöchentliches Runbook

**Zweck:** Jeden Montagmorgen die neueste Ausgabe des Amtsblatts des Kantons Uri
auswerten, Excel ablegen und einen Gmail-Entwurf an
`pascal.aschwanden@abwasser-uri.ch` erstellen.

## Was gesucht wird

1. **ALLE Baugesuche / Bauplanauflagen** (Neubau, Anbau, Umbau, Erweiterung …) —
   weil an jedem Bauvorhaben Kanalisation hängen kann.
   **Ausnahme:** reine Solaranlagen und reine Wärmepumpen fallen raus.
   (Ein Neubau *mit* Wärmepumpe bleibt drin — nur die reine Anlage fliegt raus.)
2. **Alles mit Abwasser-Bezug** in der ganzen Ausgabe, egal in welcher Rubrik:
   Kanalisation, Abwasser, Entwässerung, ARA, Kläranlage, **dezentrale
   Kleinkläranlagen**, Klärgrube usw.

Baugesuche mit Abwasser-Stichwort werden zusätzlich als „Abwasser-Bezug" markiert.

## Dateien

| Datei | Rolle |
|---|---|
| `browser_scan.js` | **Massgebliche Stichwort-/Trefferlogik** (läuft im Browser) |
| `amtsblatt_scan.py` | baut nur noch die Excel aus dem JSON (`--json`-Modus) |
| `tmp/ergebnis_<nr>_<jahr>.json` | Zwischenergebnis aus dem Browser |
| `state.json` | zuletzt verarbeitete Ausgabe |

## Zwei harte technische Fakten

- Die **Ausgabenliste** auf ur.ch wird per JavaScript geladen und die neuen
  **`_web.pdf`** geben ihren Text über `web_fetch` **nicht** her. → Browser nötig
  (fetch same-origin + pdf.js von cdnjs; CSP von ur.ch erlaubt das).
- Der **Rückgabekanal des Browsers kappt bei ca. 1500 Zeichen**. Der 30-kB-Volltext
  kann deshalb NICHT nach Python. → Auswertung passiert im Browser, und die
  Ergebnisse werden **portionsweise** abgeholt.

## Ablauf

1. `list_connected_browsers` → leer? **Fallback F**.
2. Browser: `https://www.ur.ch/amtsblatt`. Oberste Zeile `Amtsblatt Nr. <n> vom <Datum>`
   → `nr`, `datum`, Detail-Link `/_rte/publikation/<pubid>`.
3. `state.json` prüfen: schon verarbeitet → Ende.
4. Browser: `https://www.ur.ch/_rte/publikation/<pubid>` → PDF-Link `/_doc/<docid>`.
5. `browser_scan.js` lesen, `__PDFURL__`/`__NR__`/`__DATUM__` ersetzen, via
   `javascript_tool` ausführen. Rückgabe: `{b,t,x}` (Anzahl Baugesuche / Abwasser-
   Treffer / Ausgeschlossene). Ergebnis liegt in `window.__res`.
6. Ergebnisse **portionsweise** abholen (je ca. 4 Einträge, sonst Abschneiden):
   ```js
   JSON.stringify(window.__res.bau.slice(0,4).map(e=>({g:e.g,s:e.s,k:e.k||'',
     h:(e.h||'').slice(0,48),v:(e.v||'').slice(0,85),
     p:(e.p||'').slice(0,48),b:(e.b||'').slice(0,26)})))
   ```
   dann `.slice(4,8)`, `.slice(8,12)` … ebenso `window.__res.tre` und `window.__res.exc`.
7. Alles zu `tmp/ergebnis_<nr>_<jahr>.json` zusammensetzen
   (`{nr,datum,url,bau:[…],tre:[…],exc:[…]}`) und ausführen:
   ```
   python3 amtsblatt_scan.py tmp/ergebnis_<nr>_<jahr>.json \
     "Amtsblatt_Uri_KW<nr>_<jahr>_Baugesuche_Abwasser.xlsx"
   ```
8. **Gmail-Entwurf** (`create_draft`) an `pascal.aschwanden@abwasser-uri.ch`:
   - Betreff: `Amtsblatt Uri KW<nr>/<jahr> – Baugesuche & Abwasser: <b> Einträge`
   - Body: Zusammenfassung (Baugesuche, davon mit Abwasser-Bezug, sonstige
     Abwasser-Treffer, Ausgeschlossene), dann die Baugesuche als Liste
     (Gemeinde – Bauvorhaben, Bauherrschaft, Bauplatz, Seite), dann die
     ausgeschlossenen, dann PDF-Link und Excel-Pfad.
   - **Anhänge unterstützt der Connector nicht** → Excel-Pfad im Text nennen.
9. **Entwurf versenden.** Der Gmail-Connector hat keine Sende-Funktion (nur
   `create_draft`) — deshalb macht Chrome den Klick:
   `https://mail.google.com/mail/u/0/#drafts` öffnen, Entwurfszeile (`tr.zA`) mit
   dem heutigen Betreff anklicken, dann im Dialog prüfen und **nur bei
   Übereinstimmung** senden:
   ```js
   const dlg=document.querySelector('div[role="dialog"]');
   const subj=(dlg.querySelector('input[name="subjectbox"]')||{}).value||'';
   const em=new Set(); dlg.querySelectorAll('[email]').forEach(e=>em.add(e.getAttribute('email')));
   const ok = subj.includes('Amtsblatt Uri KW<nr>/<jahr>')
           && em.has('pascal.aschwanden@abwasser-uri.ch');
   const btn=[...dlg.querySelectorAll('div[role="button"],button')]
       .find(b=>/^senden$/i.test((b.textContent||'').trim()));
   if (ok && btn) btn.click();
   ```
   Danach auf die Bestätigung „Nachricht gesendet" prüfen. Stimmt Betreff oder
   Empfänger nicht → NICHT senden, Entwurf stehen lassen und melden.
   (Hinweis: Der Empfänger steht als Chip im `email`-Attribut, nicht im Text.)
10. `state.json` schreiben: `{"letzte_nr":<nr>,"jahr":<jahr>,"datum":"…","stand":"<ISO>"}`.

### Fallback F — kein Browser
Gmail-Entwurf „Amtsblatt Uri – Chrome nicht verbunden" mit Link
`https://www.ur.ch/amtsblatt` und Bitte, Chrome zu verbinden und den Task
manuell erneut auszulösen.

### Leere Wochen
Auch bei 0 Einträgen: Excel erzeugen und Entwurf mit „0 Einträge" anlegen,
damit bestätigt ist, dass der Lauf durch ist.

## Stichwörter ändern
Nur in `browser_scan.js` (Listen `ABW`, `FLAG`, Regex `SOLAR` / `OBJ`).

## Zeitplan
Montag ~07:00 (Europe/Zurich). Die Ausgabe ist bereits ab Freitag 16:00 online.
