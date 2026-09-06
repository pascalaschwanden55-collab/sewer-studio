/* Prüflauf für SewerStudio-Nova-Optimiert.html. Jeder Punkt meldet bestanden / fehlgeschlagen. */
const { chromium } = require('playwright-core'); const path = require('path'); const fs = require('fs');
const exe = path.join(process.env.USERPROFILE, 'AppData', 'Local', 'ms-playwright', 'chromium-1155', 'chrome-win', 'chrome.exe');
const htmlPath = path.resolve(__dirname, '..', 'SewerStudio-Nova-Optimiert.html');
const file = 'file:///' + htmlPath.split(path.sep).join('/');
const outDir = process.argv[2] || path.resolve(__dirname, '..', 'nachweise-opt'); fs.mkdirSync(outDir, { recursive: true });
const results = []; const errs = [];
function rec(id, name, ok, detail){ results.push({ id, name, ergebnis: ok === null ? 'nicht geprüft' : ok ? 'bestanden' : 'fehlgeschlagen', detail: String(detail ?? '') }); console.log((ok === null ? '·' : ok ? '✓' : '✗') + ' ' + id + ' ' + name + (detail ? ' — ' + detail : '')); }
async function fresh(b, w, h, opts){ const ctx = await b.newContext({ viewport: { width: w, height: h }, reducedMotion: (opts && opts.reduce) ? 'reduce' : 'no-preference', colorScheme: (opts && opts.dark) ? 'dark' : 'light' }); const p = await ctx.newPage(); p.on('pageerror', e => errs.push(e.message)); p.on('console', m => { if (m.type() === 'error') errs.push(m.text()); }); await p.goto(file); await p.waitForTimeout(300); return { ctx, p }; }
async function visibleRows(p){ return p.evaluate(() => { const wrap = document.querySelector('#page-haltungen .tablewrap'); const r = wrap.getBoundingClientRect(); const head = document.querySelector('#tblH thead').getBoundingClientRect(); return Array.from(document.querySelectorAll('#tblH tbody tr')).filter(tr => { const b = tr.getBoundingClientRect(); return b.top >= head.bottom - 1 && b.bottom <= r.bottom + 1; }).length; }); }

(async () => {
  const b = await chromium.launch({ executablePath: exe });
  // ---------- A: Grundlagen ----------
  { const { ctx, p } = await fresh(b, 1366, 768);
    rec('A1', 'Dokument: DOCTYPE, UTF-8, lang=de, viewport', await p.evaluate(() => document.compatMode === 'CSS1Compat' && document.characterSet === 'UTF-8' && document.documentElement.lang === 'de' && !!document.querySelector('meta[name=viewport]')), await p.evaluate(() => document.compatMode + ' ' + document.characterSet));
    rec('A2', 'Keine externen Schriften oder Skripte', await p.evaluate(() => !document.querySelector('link[rel=stylesheet],script[src]')), 'Systemschriften Segoe UI / Cascadia Mono');
    const ov = await p.textContent('#ovText'); rec('A3', 'Umlaute im lokalen Aufruf lesbar', /geprüft/.test(ov), ov.slice(0, 60));
    // Seiten und Fenster
    const pages = ['overview','projekt','haltungen','schaechte','import','export','medien','druck','dossiers','matrix','smatrix','schatten','vsa','diagnose','einstellungen'];
    let okPages = 0; for (const pg of pages){ await p.click(`.nav[data-nav="${pg}"]`); await p.waitForTimeout(60); if (await p.isVisible(`#page-${pg}`)) okPages++; }
    rec('A4', 'Alle 15 Seiten über die Navigation erreichbar', okPages === 15, okPages + ' von 15');
    await p.click('.nav[data-nav="haltungen"]');
    await p.click('#btnPlayerH'); await p.waitForTimeout(100); const w1 = await p.isVisible('#winPlayer'); await p.keyboard.press('Escape');
    await p.click('[data-open="winStudio"]'); await p.waitForTimeout(100); const w2 = await p.isVisible('#winStudio'); await p.click('#stCatalog'); await p.waitForTimeout(100); const w3 = await p.isVisible('#winVsa'); await p.keyboard.press('Escape'); await p.keyboard.press('Escape');
    rec('A5', 'Drei Fenster öffnen: Player, Training Studio, VSA-Codierung', w1 && w2 && w3, [w1, w2, w3].join('/'));
    rec('A6', 'Keine Skriptfehler beim Durchklicken', errs.length === 0, errs.slice(0, 3).join(' | ') || 'keine');
    await ctx.close(); }

  // ---------- B: Datensatzwechsel, Speichern, Verwerfen, Abbrechen ----------
  { const { ctx, p } = await fresh(b, 1440, 900);
    await p.click('.nav[data-nav="haltungen"]');
    const readForm = async () => p.evaluate(() => ({ name: document.querySelector('#f_h_name').value, material: document.querySelector('#f_h_material').value, dn: document.querySelector('#f_h_dn').value, laenge: document.querySelector('#f_h_laenge').value, side: document.querySelector('#sideHName').textContent, sideFacts: document.querySelector('#sideHBody .facts').textContent, drawer: document.querySelector('#drawerHName').textContent, video: document.querySelector('#sideHBody').textContent.includes('20251006_07.6588-6587.mp4') }));
    await p.click('#tblH tbody tr[data-id="h02"]'); await p.waitForTimeout(100); const f2 = await readForm();
    rec('B1', 'Wechsel auf 07.6588-6587: Formular, Übersicht und Medien folgen', f2.name === '07.6588-6587' && f2.material === 'PVC' && f2.dn === '250' && f2.laenge === '28.1' && f2.side === '07.6588-6587' && f2.drawer === '07.6588-6587' && f2.sideFacts.includes('PVC') && f2.video, JSON.stringify({ material: f2.material, dn: f2.dn, laenge: f2.laenge, video: f2.video }));
    await p.click('#tblH tbody tr[data-id="h01"]'); await p.waitForTimeout(100); const f1 = await readForm();
    rec('B2', 'Zurück auf 78998-79002: Beton, 300, 42.30', f1.material === 'Beton' && f1.dn === '300' && f1.laenge === '42.3', JSON.stringify({ material: f1.material, dn: f1.dn, laenge: f1.laenge }));
    // Änderung -> ungespeichert sichtbar
    await p.fill('#f_h_baujahr', '1979'); await p.waitForTimeout(50);
    const dirtyVisible = await p.isVisible('#dirtyH'); const saveEnabled = await p.evaluate(() => !document.querySelector('#btnSaveH').disabled); const rowMark = await p.evaluate(() => document.querySelector('#tblH tbody tr[data-id="h01"] .dirtymark').textContent === '●');
    rec('B3', 'Ungespeicherte Änderung sichtbar (Badge, Zeilenmarke, Speichern aktiv)', dirtyVisible && saveEnabled && rowMark, `badge ${dirtyVisible} save ${saveEnabled} mark ${rowMark}`);
    // Wechsel mit Abbrechen
    await p.click('#tblH tbody tr[data-id="h02"]'); await p.waitForTimeout(100); const cfOpen = await p.isVisible('#winConfirm'); const cfRole = await p.getAttribute('#winConfirm', 'role');
    await p.click('#cfCancel'); await p.waitForTimeout(50); const still1 = (await readForm()).name === '78998-79002' && await p.isVisible('#dirtyH');
    rec('B4', 'Wechsel bei Änderungen fragt nach; Abbrechen bleibt auf der Haltung', cfOpen && cfRole === 'alertdialog' && still1, `dialog ${cfOpen} role ${cfRole} bleibt ${still1}`);
    // Verwerfen und wechseln
    await p.click('#tblH tbody tr[data-id="h02"]'); await p.waitForTimeout(100); await p.click('#cfDiscard'); await p.waitForTimeout(100);
    const now2 = (await readForm()).name === '07.6588-6587'; const orig = await p.evaluate(() => window.__nova.DATA.haltungen.find(h => h.id === 'h01').baujahr);
    rec('B5', 'Verwerfen und wechseln: Ziel gewählt, Ursprung unverändert', now2 && orig === 1978, `jetzt 07.6588-6587 ${now2}, Baujahr h01 = ${orig}`);
    // Speichern und wechseln
    await p.fill('#f_h_baujahr', '1995'); await p.click('#tblH tbody tr[data-id="h01"]'); await p.waitForTimeout(100); await p.click('#cfSave'); await p.waitForTimeout(100);
    const saved = await p.evaluate(() => window.__nova.DATA.haltungen.find(h => h.id === 'h02').baujahr); const now1 = (await readForm()).name === '78998-79002';
    rec('B6', 'Speichern und wechseln: Wert im Bestand, Ziel gewählt', String(saved) === '1995' && now1, `Baujahr h02 = ${saved}`);
    await p.reload(); await p.waitForTimeout(300); const persisted = await p.evaluate(() => window.__nova.DATA.haltungen.find(h => h.id === 'h02').baujahr);
    rec('B7', 'Gespeicherter Wert überlebt Neuladen (Browserspeicher, kein Projektschreiben)', String(persisted) === '1995', `nach Neuladen ${persisted}`);
    // Direktes Speichern / Verwerfen
    await p.click('.nav[data-nav="haltungen"]'); await p.fill('#f_h_strasse', 'Testgasse'); await p.click('#btnDiscardH'); await p.waitForTimeout(50);
    const disc = await p.evaluate(() => document.querySelector('#f_h_strasse').value === 'Seilergasse' && document.querySelector('#dirtyH').hidden);
    rec('B8', 'Verwerfen ohne Wechsel setzt Feld zurück', disc);
    await ctx.clearCookies(); await p.evaluate(() => localStorage.clear()); await ctx.close(); }

  // ---------- C: unabhängige Suchen ----------
  { const { ctx, p } = await fresh(b, 1440, 900);
    await p.click('.nav[data-nav="haltungen"]');
    const countS = async () => p.evaluate(() => Array.from(document.querySelectorAll('#secsS label')).filter(l => !l.classList.contains('hide')).length);
    const before = await countS(); await p.fill('#fsH', 'Baujahr'); await p.waitForTimeout(50); const after = await countS();
    const hVisible = await p.evaluate(() => Array.from(document.querySelectorAll('#secsH label')).filter(l => !l.classList.contains('hide')).map(l => l.querySelector('span').textContent));
    rec('C1', 'Feldsuche „Baujahr" bei Haltungen lässt Schachtfelder unberührt', before === after && hVisible.length === 1 && hVisible[0] === 'Baujahr', `Schachtfelder ${before} → ${after}, Haltungsfelder sichtbar: ${hVisible.join(',')}`);
    await p.fill('#fsH', '');
    const selSBefore = await p.evaluate(() => document.querySelector('#tblS tbody tr[aria-selected="true"]').dataset.id);
    await p.click('#viewsH .vchip:nth-of-type(3)'); await p.waitForTimeout(50);
    const selSAfter = await p.evaluate(() => document.querySelector('#tblS tbody tr[aria-selected="true"]').dataset.id); const viewS = await p.evaluate(() => document.querySelector('#viewsS .vchip[aria-pressed="true"]').textContent);
    rec('C2', 'Ansichtswechsel bei Haltungen ändert Schachtansicht und Schachtauswahl nicht', selSBefore === selSAfter && viewS.startsWith('Kompakt'), `Schacht ${selSBefore}→${selSAfter}, Ansicht S: ${viewS.trim()}`);
    await p.click('[data-secs="secsH"][data-open-all="0"]'); const sOpen = await p.evaluate(() => Array.from(document.querySelectorAll('#secsS details')).every(d => d.open)); const hOpen = await p.evaluate(() => Array.from(document.querySelectorAll('#secsH details')).some(d => d.open));
    rec('C3', '„Alle zu" wirkt nur auf die eigene Seite', sOpen && !hOpen, `S offen ${sOpen}, H offen ${hOpen}`);
    await p.click('.nav[data-nav="schaechte"]'); await p.fill('#searchS', '774'); await p.waitForTimeout(50); const rowsS = await p.evaluate(() => document.querySelectorAll('#tblS tbody tr[data-id]').length); const rowsH = await p.evaluate(() => document.querySelectorAll('#tblH tbody tr[data-id]').length);
    rec('C4', 'Schachtsuche arbeitet eigenständig', rowsS === 1 && rowsH === 14, `Schächte ${rowsS}, Haltungen ${rowsH}`);
    await p.fill('#fsS', 'Dichtheit'); const sLab = await p.evaluate(() => Array.from(document.querySelectorAll('#secsS label')).filter(l => !l.classList.contains('hide')).length); const hLab = await p.evaluate(() => Array.from(document.querySelectorAll('#secsH label')).filter(l => !l.classList.contains('hide')).length);
    rec('C5', 'Feldsuche bei Schächten lässt Haltungsfelder unberührt', sLab === 1 && hLab === 36, `S ${sLab}, H ${hLab}`);
    await ctx.close(); }

  // ---------- D: Summen ----------
  { const { ctx, p } = await fresh(b, 1440, 900);
    const d = await p.evaluate(() => { const s = window.__nova.stats(); const leg = Array.from(document.querySelectorAll('#legend b')).map(b => Number(b.textContent)); return { n: s.n, dring: s.dring, z0: s.zk[0], z1: s.zk[1], legSum: leg.reduce((a, b) => a + b, 0), kpiDring: document.querySelector('#kpiDring').textContent, kpiDringT: document.querySelector('#kpiDringT').textContent, ov: document.querySelector('#ovText').textContent, gepr: s.gepr, pct: (100 * s.gepr / s.n).toFixed(1).replace('.', ','), kosten: document.querySelector('#kpiKosten').textContent, sum: s.kostH + s.kostS }; });
    rec('D1', 'Dringend = Z0 + Z1', d.dring === d.z0 + d.z1 && d.kpiDring.startsWith(String(d.dring)) && d.kpiDringT.includes(`Z0 ${d.z0} + Z1 ${d.z1}`), `${d.dring} = ${d.z0} + ${d.z1}`);
    rec('D2', 'Legende summiert auf die Gesamtzahl', d.legSum === d.n, `${d.legSum} = ${d.n}`);
    rec('D3', 'Prozent mit Zähler und Nenner, korrekt gerundet', d.ov.includes(`${d.gepr} von ${d.n}`) && d.ov.includes(d.pct + ' %'), d.ov.slice(0, 50));
    rec('D4', 'Kosten-KPI = Summe Haltungen + Schächte', d.kosten.replace(/[^0-9]/g, '') === String(d.sum), d.kosten);
    rec('D5', 'Text unterscheidet fachlich geprüft, KI analysiert und ohne Analyse', /fachlich geprüft/.test(d.ov) && /KI analysiert/.test(d.ov) && /ohne Analyse/.test(d.ov));
    await ctx.close(); }

  // ---------- E: Ablauf Nächste Haltung, Player, Codierung ----------
  { const { ctx, p } = await fresh(b, 1440, 900);
    await p.click('#btnNext'); await p.waitForTimeout(150);
    const e1 = await p.evaluate(() => ({ page: document.querySelector('.page.show').id, sel: document.querySelector('#tblH tbody tr[aria-selected="true"]').dataset.id, player: document.querySelector('#winPlayer').classList.contains('show'), name: document.querySelector('#plName').textContent, pruef: window.__nova.DATA.haltungen.find(h => h.id === document.querySelector('#tblH tbody tr[aria-selected="true"]').dataset.id).pruefung }));
    rec('E1', '„Nächste Haltung prüfen" wählt eine offene Haltung und öffnet den Player', e1.page === 'page-haltungen' && e1.player && e1.pruef === 'analysiert', `${e1.sel} ${e1.name} (${e1.pruef})`);
    const focusIn = await p.evaluate(() => document.querySelector('#winPlayer').contains(document.activeElement)); const role = await p.getAttribute('#winPlayer', 'role'); const modal = await p.getAttribute('#winPlayer', 'aria-modal'); const labelled = await p.getAttribute('#winPlayer', 'aria-labelledby');
    rec('E2', 'Player: Dialogrolle, aria-modal, Name, Fokus im Dialog', focusIn && role === 'dialog' && modal === 'true' && !!labelled, `focus ${focusIn} role ${role}`);
    await p.click('[data-pl="fwd5"]'); await p.click('[data-pl="fwd5"]'); const pos0 = await p.evaluate(() => window.__nova.state.player.pos); const n0 = await p.evaluate(() => window.__nova.DATA.haltungen.find(h => h.id === window.__nova.state.player.id).findings.length);
    await p.click('#plNewEvent'); await p.waitForTimeout(100); const vsaOpen = await p.isVisible('#winVsa'); const vsaFocus = await p.evaluate(() => document.querySelector('#winVsa').contains(document.activeElement));
    await p.click('[data-close]', { strict: false }).catch(() => {}); // erster data-close ist im Player-Titel, deshalb gezielt:
    await p.click('#winVsa .wintitle [data-close]'); await p.waitForTimeout(100);
    const afterCancel = await p.evaluate(() => ({ vsa: document.querySelector('#winVsa').classList.contains('show'), player: document.querySelector('#winPlayer').classList.contains('show'), pos: window.__nova.state.player.pos, n: window.__nova.DATA.haltungen.find(h => h.id === window.__nova.state.player.id).findings.length, focus: document.activeElement.id }));
    rec('E3', 'Ereignis erfassen → Abbrechen: kehrt zum Player zurück, nichts übernommen, Position gleich', vsaOpen && vsaFocus && !afterCancel.vsa && afterCancel.player && afterCancel.pos === pos0 && afterCancel.n === n0 && afterCancel.focus === 'plNewEvent', JSON.stringify(afterCancel));
    await p.click('#plNewEvent'); await p.waitForTimeout(100); await p.click('#vsaFreq button:nth-child(2)'); await p.fill('#vsaM2', '1'); await p.click('#vsaApply'); await p.waitForTimeout(50); const errShown = await p.isVisible('#vsaErr'); await p.fill('#vsaM2', ''); await p.click('#vsaApply'); await p.waitForTimeout(100);
    const afterApply = await p.evaluate(() => ({ vsa: document.querySelector('#winVsa').classList.contains('show'), player: document.querySelector('#winPlayer').classList.contains('show'), pos: window.__nova.state.player.pos, n: window.__nova.DATA.haltungen.find(h => h.id === window.__nova.state.player.id).findings.length, id: window.__nova.state.player.id, sel: document.querySelector('#tblH tbody tr[aria-selected="true"]').dataset.id }));
    rec('E4', 'Ereignis erfassen → Übernehmen: Befund gespeichert, Player offen, Haltung und Position erhalten', errShown && !afterApply.vsa && afterApply.player && afterApply.pos === pos0 && afterApply.n === n0 + 1 && afterApply.id === afterApply.sel, JSON.stringify(afterApply) + ' Fehlerhinweis bei Ende<Start: ' + errShown);
    await p.keyboard.press('Escape'); await p.waitForTimeout(50); const playerClosed = !(await p.evaluate(() => document.querySelector('#winPlayer').classList.contains('show'))); const focusBack = await p.evaluate(() => document.activeElement.id || (document.activeElement.dataset && document.activeElement.dataset.id) || document.activeElement.tagName);
    rec('E5', 'Esc schliesst den Player; Fokus kehrt zum Auslöser oder, wenn der unsichtbar ist, zur gewählten Zeile', playerClosed && (focusBack === 'btnNext' || focusBack === 'h01'), `focus ${focusBack}`);
    // Studio -> Codierung -> zurück
    await p.click('[data-open="winStudio"]'); await p.waitForTimeout(100); await p.click('#stCatalog'); await p.waitForTimeout(100); await p.click('#vsaFreq button:nth-child(3)'); await p.click('#vsaApply'); await p.waitForTimeout(100);
    const st = await p.evaluate(() => ({ studio: document.querySelector('#winStudio').classList.contains('show'), vsa: document.querySelector('#winVsa').classList.contains('show'), code: document.querySelector('#stCode').value, focus: document.activeElement.id }));
    rec('E6', 'Training Studio → Katalog → Übernehmen: Code im Studio, Studio bleibt offen', st.studio && !st.vsa && st.code.startsWith('BBC') && st.focus === 'stCatalog', JSON.stringify(st));
    await p.fill('#stDesc', 'kurz'); await p.click('#stAccept'); const short = await p.textContent('#stNote'); await p.fill('#stDesc', 'Querriss im Scheitel, 2 mm breit.'); await p.click('#stAccept'); const rel = await p.evaluate(() => !document.querySelector('#stRelease').disabled);
    rec('E7', 'Studio: Vorschlag, Bestätigung und Trainingsfreigabe sind getrennte Schritte', short.includes('mindestens 10') && rel, 'Freigabe erst nach Akzeptieren aktiv');
    await ctx.close(); }

  // ---------- F: ruhiger Modus ----------
  { const { ctx, p } = await fresh(b, 1440, 900);
    const runOn = await p.evaluate(() => window.__nova.engine.isRunning());
    await p.click('#moOff'); await p.waitForTimeout(100);
    const snap = async () => p.evaluate(() => document.querySelector('#engine').toDataURL().length + ':' + document.querySelector('#engine').toDataURL().slice(-64));
    const a = await snap(); await p.waitForTimeout(450); const bb = await snap();
    const runOff = await p.evaluate(() => window.__nova.engine.isRunning()); const attr = await p.getAttribute('html', 'data-motion');
    rec('F1', '„Ruhig" stoppt die Hintergrund-Engine (Canvas unverändert, Schleife aus)', runOn && !runOff && a === bb && attr === 'off', `vorher läuft ${runOn}, nachher ${runOff}, Canvas gleich ${a === bb}`);
    await p.reload(); await p.waitForTimeout(300); const persisted = await p.getAttribute('html', 'data-motion'); const pressed = await p.getAttribute('#moOff', 'aria-pressed');
    rec('F2', 'Wahl „ruhig" bleibt nach Neuladen erhalten', persisted === 'off' && pressed === 'true');
    await p.click('#moOn'); await p.waitForTimeout(50); const runAgain = await p.evaluate(() => window.__nova.engine.isRunning());
    await p.click('.nav[data-nav="haltungen"]'); await p.click('#btnPlayerH'); await p.waitForTimeout(100); const runDialog = await p.evaluate(() => window.__nova.engine.isRunning()); await p.keyboard.press('Escape');
    rec('F3', 'Engine pausiert, solange ein Fenster die Ansicht verdeckt', runAgain && !runDialog, `sichtbar ${runAgain}, bei Dialog ${runDialog}`);
    await ctx.close();
    const r2 = await fresh(b, 1440, 900, { reduce: true }); const runReduced = await r2.p.evaluate(() => window.__nova.engine.isRunning()); const anim = await r2.p.evaluate(() => getComputedStyle(document.querySelector('.pulse')).animationName);
    rec('F4', 'Systemeinstellung „Bewegung reduzieren" stoppt Engine und Pulsanimation auch bei „bewegt"', !runReduced && anim === 'none', `engine ${runReduced}, animation ${anim}`);
    await r2.ctx.close(); }

  // ---------- G: Tastatur ----------
  { const { ctx, p } = await fresh(b, 1440, 900);
    await p.focus('.nav[data-nav="export"]'); await p.keyboard.press('Enter'); await p.waitForTimeout(50);
    rec('G1', 'Enter auf Navigationseintrag Export öffnet die Exportseite', await p.isVisible('#page-export'));
    await p.keyboard.press('Control+k'); const f = await p.evaluate(() => document.activeElement.id); await p.keyboard.type('82007'); await p.waitForTimeout(80); const n = await p.evaluate(() => document.querySelectorAll('#gresults button').length); await p.keyboard.press('Enter'); await p.waitForTimeout(100);
    const g2 = await p.evaluate(() => ({ page: document.querySelector('.page.show').id, sel: document.querySelector('#tblH tbody tr[aria-selected="true"]').dataset.id }));
    rec('G2', 'Ctrl+K, Suchtext, Enter: springt zur gefundenen Haltung', f === 'gsearch' && n >= 1 && g2.page === 'page-haltungen' && g2.sel === 'h10', JSON.stringify(g2) + ' Treffer ' + n);
    await p.keyboard.press('F3'); const f3 = await p.evaluate(() => document.activeElement.id); rec('G3', 'F3 fokussiert die Haltungssuche', f3 === 'searchH');
    await p.focus('#tblH tbody tr[data-id="h01"]'); await p.keyboard.press('ArrowDown'); await p.keyboard.press('Enter'); await p.waitForTimeout(50); const selK = await p.evaluate(() => document.querySelector('#tblH tbody tr[aria-selected="true"]').dataset.id);
    rec('G4', 'Pfeiltasten und Enter wählen Zeilen in der Liste', selK === 'h02', selK);
    await p.keyboard.press('F1'); await p.waitForTimeout(50); const help = await p.isVisible('#winHelp'); const helpFocus = await p.evaluate(() => document.querySelector('#winHelp').contains(document.activeElement));
    await p.keyboard.press('Tab'); await p.keyboard.press('Tab'); await p.keyboard.press('Tab'); const trapped = await p.evaluate(() => document.querySelector('#winHelp').contains(document.activeElement));
    await p.keyboard.press('Escape'); await p.waitForTimeout(50); const helpClosed = !(await p.isVisible('#winHelp'));
    rec('G5', 'F1 öffnet Tastenkürzel, Fokus bleibt im Dialog, Esc schliesst', help && helpFocus && trapped && helpClosed);
    const unlabeled = await p.evaluate(() => Array.from(document.querySelectorAll('button')).filter(b => !(b.textContent.trim() || b.getAttribute('aria-label') || b.getAttribute('title'))).length);
    rec('G6', 'Jede Schaltfläche hat Text, aria-label oder Titel', unlabeled === 0, unlabeled + ' ohne Bezeichnung');
    const focusVisible = await p.evaluate(() => { const s = Array.from(document.styleSheets[0].cssRules).some(r => r.selectorText && r.selectorText.includes(':focus-visible') && r.style.outline); return s; });
    rec('G7', 'Sichtbarer Tastaturfokus definiert', focusVisible);
    await ctx.close(); }

  // ---------- H: Layout und Lesbarkeit ----------
  for (const [w, h] of [[1366, 768], [1440, 900], [1920, 1080]]){
    const { ctx, p } = await fresh(b, w, h); await p.click('.nav[data-nav="overview"]'); await p.waitForTimeout(150);
    await p.screenshot({ path: path.join(outDir, `uebersicht-${w}x${h}.png`) });
    await p.click('.nav[data-nav="haltungen"]'); await p.waitForTimeout(200); const rows = await visibleRows(p); const drawerVisible = await p.evaluate(() => { const d = document.querySelector('#drawerH'); const r = d.getBoundingClientRect(); return r.bottom <= innerHeight + 1 && !d.classList.contains('closed'); }); const sideVisible = await p.evaluate(() => document.querySelector('#sideHBody .findings').getBoundingClientRect().top < innerHeight);
    await p.screenshot({ path: path.join(outDir, `haltungen-${w}x${h}.png`) });
    rec('H-' + w, `${w}×${h}: mindestens sechs Haltungen sichtbar, Eingabefelder und Übersicht im Bild`, rows >= 6 && drawerVisible && sideVisible, `${rows} Zeilen sichtbar, Eingabefelder ${drawerVisible}, Übersicht ${sideVisible}`);
    await p.click('#btnPlayerH'); await p.waitForTimeout(150);
    const pl = await p.evaluate(() => { const inV = el => { const r = el.getBoundingClientRect(); return r.top >= 0 && r.bottom <= innerHeight && r.left >= 0 && r.right <= innerWidth; }; return { play: inV(document.querySelector('#plPlay')), time: inV(document.querySelector('#plTime')), apply: inV(document.querySelector('#plApply')), side: inV(document.querySelector('#plNewEvent')), sideW: document.querySelector('#plSide').getBoundingClientRect().width }; });
    await p.screenshot({ path: path.join(outDir, `player-${w}x${h}.png`) });
    rec('HP-' + w, `${w}×${h}: Player zeigt Wiedergabe, Zeit, Übernehmen und Seitenspalte ohne Scrollen`, pl.play && pl.time && pl.apply && pl.side && pl.sideW > 300, JSON.stringify(pl));
    await p.keyboard.press('Escape'); await p.click('[data-open="winStudio"]'); await p.waitForTimeout(150);
    const st = await p.evaluate(() => { const col = document.querySelector('#stRelease').closest('.stackv'); const r = col.getBoundingClientRect(); const rel = document.querySelector('#stRelease'); col.scrollTop = col.scrollHeight; const rr = rel.getBoundingClientRect(); return { colInView: r.left >= 0 && r.right <= innerWidth, colW: Math.round(r.width), releaseReachable: rr.top >= 0 && rr.bottom <= innerHeight, acceptVisibleAtTop: (col.scrollTop = 0, document.querySelector('#stAccept').getBoundingClientRect().bottom <= innerHeight) }; });
    await p.screenshot({ path: path.join(outDir, `training-${w}x${h}.png`) });
    rec('HS-' + w, `${w}×${h}: Training Studio: Codier- und Freigabespalte vollständig erreichbar`, st.colInView && st.releaseReachable && st.colW >= 300, JSON.stringify(st));
    await ctx.close();
  }
  { const { ctx, p } = await fresh(b, 1440, 900, { dark: true }); await p.click('#thDark'); await p.waitForTimeout(150); await p.screenshot({ path: path.join(outDir, 'uebersicht-dunkel-1440x900.png') }); await p.click('.nav[data-nav="haltungen"]'); await p.waitForTimeout(150); await p.screenshot({ path: path.join(outDir, 'haltungen-dunkel-1440x900.png') }); await ctx.close(); }

  // ---------- I: Schriftgrössen und Kontrast ----------
  const contrastCheck = async (p, label) => p.evaluate(() => {
    const lum = c => { const m = c.match(/\d+(\.\d+)?/g).map(Number); const f = v => { v /= 255; return v <= .03928 ? v / 12.92 : Math.pow((v + .055) / 1.055, 2.4); }; return .2126 * f(m[0]) + .7152 * f(m[1]) + .0722 * f(m[2]); };
    const parse = c => { const m = c.match(/\d+(\.\d+)?/g).map(Number); return { r: m[0], g: m[1], b: m[2], a: m.length > 3 ? m[3] : 1 }; };
    const bgOf = el => { let e = el; const stack = []; while (e && e !== document.documentElement){ const c = parse(getComputedStyle(e).backgroundColor); if (c.a > 0) stack.push(c); if (c.a >= 1) break; e = e.parentElement; } if (!stack.length || stack[stack.length - 1].a < 1) stack.push(parse(getComputedStyle(document.body).backgroundColor)); let out = stack.pop(); while (stack.length){ const t = stack.pop(); out = { r: t.r * t.a + out.r * (1 - t.a), g: t.g * t.a + out.g * (1 - t.a), b: t.b * t.a + out.b * (1 - t.a), a: 1 }; } return `rgb(${out.r},${out.g},${out.b})`; };
    const ratio = (a, b) => { const l1 = lum(a), l2 = lum(b); return (Math.max(l1, l2) + .05) / (Math.min(l1, l2) + .05); };
    const els = Array.from(document.querySelectorAll('.page.show *, .rail *, .topbar *, .preview *')).filter(el => el.offsetParent !== null && Array.from(el.childNodes).some(n => n.nodeType === 3 && n.textContent.trim()));
    let minRatio = 99, minEl = '', minSize = 99, minSizeEl = '', below45 = 0, total = 0; const fails = [];
    for (const el of els){ const cs = getComputedStyle(el); const fs = parseFloat(cs.fontSize); if (fs < minSize){ minSize = fs; minSizeEl = (el.getAttribute('class') || el.tagName); } const fg = cs.color; if (parse(fg).a < 1) continue; const r = ratio(fg, bgOf(el)); total++; if (r < minRatio){ minRatio = r; minEl = (el.getAttribute('class') || el.tagName) + ' „' + el.textContent.trim().slice(0, 25) + '"'; } if (r < 4.5){ below45++; if (fails.length < 6) fails.push((el.getAttribute('class') || el.tagName) + ' ' + r.toFixed(2)); } }
    return { minRatio: +minRatio.toFixed(2), minEl, minSize, minSizeEl, below45, total, fails };
  });
  for (const [dark, name] of [[false, 'hell'], [true, 'dunkel']]){
    const { ctx, p } = await fresh(b, 1440, 900, { dark }); await p.click(dark ? '#thDark' : '#thLight'); await p.waitForTimeout(100);
    let worstSize = 99, worstSizeEl = '', worstRatio = 99, worstRatioEl = '', below = 0, tot = 0; const allFails = [];
    for (const pg of ['overview', 'haltungen', 'schaechte', 'export', 'druck', 'einstellungen']){ await p.click(`.nav[data-nav="${pg}"]`); await p.waitForTimeout(80); const r = await contrastCheck(p, pg); if (r.minSize < worstSize){ worstSize = r.minSize; worstSizeEl = pg + ': ' + r.minSizeEl; } if (r.minRatio < worstRatio){ worstRatio = r.minRatio; worstRatioEl = pg + ': ' + r.minEl; } below += r.below45; tot += r.total; r.fails.forEach(f => allFails.push(pg + ' ' + f)); }
    rec('I1-' + name, `Schriftgrösse ${name}: kleinster sichtbarer Text ≥ 11 px (Beschriftungen 12 bis 13, Daten 13 bis 15)`, worstSize >= 11, `kleinste ${worstSize} px (${worstSizeEl})`);
    rec('I2-' + name, `Kontrast ${name}: sichtbarer Text mindestens 4,5:1`, below === 0, `${tot} Textelemente geprüft, schwächster ${worstRatio}:1 (${worstRatioEl})${allFails.length ? ' · unter 4,5: ' + allFails.slice(0, 4).join('; ') : ''}`);
    await ctx.close();
  }

  // ---------- J: Ehrlichkeit ----------
  { const { ctx, p } = await fresh(b, 1440, 900);
    const demoCount = await p.evaluate(() => document.querySelectorAll('[data-demo]').length);
    await p.click('.nav[data-nav="export"]'); await p.click('[data-export="xlsx-h"]'); await p.waitForTimeout(80); const exp = await p.textContent('#exportResult');
    rec('J1', 'Export nennt Ziel und Ergebnis und sagt, dass keine Datei geschrieben wurde', exp.includes('keine Datei geschrieben') && exp.includes('Haltungen.xlsx') && exp.includes('D:\\Projekte'), exp.slice(0, 80));
    await p.click('[data-export="xtf-rev"]'); await p.waitForTimeout(80); const xtf = await p.textContent('#exportResult');
    rec('J2', 'Revidierte XTF wird vom GEONIS-Rückabgleich unterschieden', xtf.includes('kein Rückabgleich') && xtf.includes('GEONIS-Import'));
    await p.click('.nav[data-nav="import"]'); await p.click('#page-import .drop .btn'); await p.waitForTimeout(80); const t = await p.textContent('#toasts');
    rec('J3', 'Nicht umgesetzte Aktionen sind gekennzeichnet und erklären sich', demoCount > 50 && t.includes('Nur Vorschau') && t.includes('keine Datei gelesen'), demoCount + ' Aktionen mit Kennzeichnung ◌');
    await p.click('.nav[data-nav="medien"]'); await p.click('#mkFilter button[data-mk="Gelernt"]'); const rowsG = await p.evaluate(() => document.querySelectorAll('#tblMK tbody tr').length); await p.evaluate(() => { document.querySelector('#mkFilter button[data-mk="Gelernt"]').click(); });
    const emptyShown = await p.evaluate(() => { window.__nova.DATA.konflikte.forEach(k => { if (k.typ === 'Gelernt') k.typ = 'Mehrdeutig'; }); document.querySelector('#mkFilter button[data-mk="Gelernt"]').click(); return !document.querySelector('#mkEmpty').hidden; });
    rec('J4', 'Leerzustand erscheint, wenn ein Filter keine Fälle hat', rowsG === 1 && emptyShown);
    await p.click('.nav[data-nav="schatten"]'); await p.click('#btnShadow'); await p.waitForTimeout(400); const loading = await p.evaluate(() => document.querySelector('#btnShadow').disabled && !document.querySelector('#btnShadowCancel').disabled); await p.waitForTimeout(1200); const done = await p.evaluate(() => document.querySelectorAll('#tblShadow tbody tr[data-id]').length > 0 && document.querySelector('#shadowText').textContent.includes('Vorschau-Simulation'));
    rec('J5', 'Lade- und Erfolgszustand (Schattenlauf) sichtbar und als Simulation benannt', loading && done);
    const kiText = await p.evaluate(() => { window.__nova.showPage('haltungen'); window.__nova.select('h', 'h01'); return document.querySelector('#sideHBody').textContent; });
    rec('J6', 'KI-Prozentwerte werden als Modellsicherheit erklärt; Bestätigung getrennt', kiText.includes('Modellsicherheit') && kiText.includes('fachliche Bestätigung'));
    rec('J7', 'Skriptfehler im gesamten Lauf', errs.length === 0, errs.slice(0, 5).join(' | ') || 'keine');
    await ctx.close(); }

  await b.close();
  fs.writeFileSync(path.join(outDir, 'pruefergebnis.json'), JSON.stringify({ datei: htmlPath, zeit: new Date().toISOString(), ergebnisse: results, skriptfehler: errs }, null, 2));
  const bestanden = results.filter(r => r.ergebnis === 'bestanden').length, fehl = results.filter(r => r.ergebnis === 'fehlgeschlagen').length;
  console.log(`\n${bestanden} bestanden, ${fehl} fehlgeschlagen, ${results.length - bestanden - fehl} nicht geprüft`);
})().catch(e => { console.error('FAIL', e); process.exit(1); });
