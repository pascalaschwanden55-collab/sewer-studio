/* Amtsblatt-Uri — Browser-Schritt (Extraktion + Auswertung)
 * ----------------------------------------------------------
 * Laeuft via javascript_tool auf einer ur.ch-Seite (same-origin noetig!).
 * Vor dem Ausfuehren __PDFURL__ durch den PDF-Link der Ausgabe ersetzen,
 * z.B. '/_doc/453490'  (leitet automatisch auf die _web.pdf weiter).
 *
 * WARUM hier und nicht in Python: Der Rueckgabekanal des Browsers kappt bei
 * ca. 1500 Zeichen — der 30-kB-Volltext kann also NICHT nach Python. Deshalb
 * ist dies die massgebliche Stichwort-/Trefferlogik. amtsblatt_scan.py baut
 * daraus nur noch die Excel.
 *
 * Ergebnis liegt danach in window.__res = {nr,datum,url,bau,tre,exc}
 * und wird in kleinen Portionen abgeholt (siehe RUNBOOK.md).
 */
const pdfjs = window.__pdfjs || await import('https://cdnjs.cloudflare.com/ajax/libs/pdf.js/4.2.67/pdf.min.mjs');
window.__pdfjs = pdfjs;
pdfjs.GlobalWorkerOptions.workerSrc = 'https://cdnjs.cloudflare.com/ajax/libs/pdf.js/4.2.67/pdf.worker.min.mjs';
const ab = await fetch('__PDFURL__').then(r => r.arrayBuffer());
const doc = await pdfjs.getDocument({ data: ab }).promise;
let text = '';
for (let p = 1; p <= doc.numPages; p++) {
  const pg = await doc.getPage(p);
  const c = await pg.getTextContent();
  let s = '';
  for (const it of c.items) { s += it.str; s += it.hasEOL ? '\n' : ' '; }
  text += s + '\n';
}
window.__pdftext = text;

/* --- Stichwoerter -------------------------------------------------------- */
const ABW = ["kanalisation","abwasser","schmutzwasser","mischwasser","meteorwasser",
 "entwässer","entwaesser","sammelkanal","kläranlage","kleinkläranlage",
 "abwasserreinigung","hauskläranlage","klärgrube","faulgrube","klärschlamm",
 "sickerleitung","versickerung","einleitbewilligung","gewässerschutz"];
/* breiter, nur zum Markieren innerhalb der Baugesuche */
const FLAG = ABW.concat(["sauberwasser","einleitung","schacht","schächte","leitung",
 "drainage","werkleitung","hausanschluss","grube","meteor","rohr"]);
/* Ausschluss: reine Solaranlagen / Waermepumpen */
const SOLAR = /(solaranlage|solarpanel|photovoltaik|pv-anlage|\bpv\b|\bsolar\b|wärmepumpe|waermepumpe|erdsonde|erdwärmesonde)/i;
/* ... aber nur, wenn sonst kein echtes Bauobjekt genannt ist */
const OBJ = /(wohnhaus|einfamilienhaus|mehrfamilienhaus|\befh\b|\bmfh\b|gebäude|\bhaus\b|halle|stall|scheune|garage|carport|überdachung|anbau|umbau|umnutzung|ersatzneubau|erweiterung|abbruch|terrasse|pool|mauer|zaun|reklame|antenne|strasse|leitung|kanal|schacht|parkplatz|unterstand|tunnel|brücke|deponie|silo|remise|werkstatt|laden|restaurant)/i;

const MUNI = new Set(["Altdorf","Andermatt","Attinghausen","Bürglen","Bauen","Erstfeld",
 "Flüelen","Göschenen","Gurtnellen","Hospental","Isenthal","Realp","Schattdorf",
 "Seedorf","Seelisberg","Silenen","Sisikon","Spiringen","Unterschächen","Wassen"]);
const SEC = {"bauplanauflagen":"Bauplanauflagen","baugesuche":"Baugesuche",
 "bau- und planungsrecht":"Bau- und Planungsrecht",
 "auflage- und einspracheverfahren":"Auflage- und Einspracheverfahren",
 "konzession; gesuch":"Konzession; Gesuch","öffentliche auflage":"Öffentliche Auflage",
 "verkehrsbeschränkungen":"Verkehrsbeschränkungen","signalisation":"Signalisation",
 "submissionen":"Submissionen","handelsregister":"Handelsregister",
 "eigentumsübertragungen":"Eigentumsübertragungen","regierungsrat":"Regierungsrat",
 "direktionen":"Direktionen","sicherheitsdirektion":"Sicherheitsdirektion",
 "gerichte":"Gerichte","schuldbetreibung und konkurs":"Schuldbetreibung und Konkurs",
 "rechtsauskunft":"Rechtsauskunft","veranstaltungen":"Veranstaltungen"};
const BAUS = new Set(["Bauplanauflagen","Baugesuche"]);
/* WICHTIG: Label NICHT am Zeilenanfang verankern — die PDF-Aufzaehlungszeichen
   kommen als Buchstaben ("n n") davor an. Sonst verschmelzen die Eintraege. */
const LAB = /(Bauherrschaft|Bauvorhaben|Bauplatz|Bemerkungen)\s*:\s*(.*)$/;
const MAP = { Bauherrschaft:'h', Bauvorhaben:'v', Bauplatz:'p', Bemerkungen:'b' };

const cl = s => (s || '').replace(/\s+/g, ' ').trim();
const fl = t => { const l = (t || '').toLowerCase(); for (const k of FLAG) if (l.includes(k)) return k; return null; };
const aw = t => { const l = (t || '').toLowerCase(); for (const k of ABW) if (l.includes(k)) return k; return /\bARA\b/.test(t || '') ? 'ARA' : null; };

const lines = text.split('\n');
let page = '', sec = '', gem = '', ent = null, f = null;
const bau = [], tre = [], exc = [];
const flush = () => {
  if (ent && (ent.v || ent.h)) {
    ['h','v','p','b'].forEach(k => ent[k] = cl(ent[k]));
    ent.k = fl([ent.h, ent.v, ent.p, ent.b].join(' '));
    if (ent.v && SOLAR.test(ent.v) && !OBJ.test(ent.v)) exc.push(ent); else bau.push(ent);
  }
  ent = null; f = null;
};
for (let i = 0; i < lines.length; i++) {
  const ln = lines[i].replace(/\s+$/, ''), s = ln.trim();
  const m = ln.match(/^\s*(\d{2,4})\s*(?:Administrativer|Gerichtlicher)/) || ln.match(/^\s*(\d{2,4})\s*$/);
  if (m) page = m[1];
  const key = s.toLowerCase().replace(/:+$/, '');
  if (SEC[key]) { flush(); sec = SEC[key]; gem = ''; continue; }
  if (MUNI.has(s)) { flush(); gem = s; continue; }
  const mm = s.match(/^(?:Betroffene\s+)?Gemeinde\s+([A-Za-zÄÖÜäöüß]+)$/);
  if (mm && MUNI.has(mm[1])) { flush(); gem = mm[1]; continue; }
  if (BAUS.has(sec)) {
    const lm = ln.match(LAB);
    if (lm) {
      const lab = lm[1];
      if (lab === 'Bauherrschaft') { flush(); ent = { g: gem, s: page, r: sec }; }
      if (!ent) ent = { g: gem, s: page, r: sec };
      f = MAP[lab]; ent[f] = ((ent[f] || '') + ' ' + lm[2]).trim();
      continue;
    }
    if (ent && f && s) { ent[f] = ((ent[f] || '') + ' ' + s).trim(); continue; }
  }
  const k = aw(ln);
  if (k && !BAUS.has(sec)) {
    tre.push({ r: sec || '-', g: gem, s: page, k: k,
               a: cl(lines.slice(Math.max(0, i - 1), i + 3).join(' ')).slice(0, 300) });
  }
}
flush();
window.__res = { nr: '__NR__', datum: '__DATUM__', url: location.origin + '__PDFURL__',
                 bau: bau, tre: tre, exc: exc };
JSON.stringify({ b: bau.length, t: tre.length, x: exc.length });
