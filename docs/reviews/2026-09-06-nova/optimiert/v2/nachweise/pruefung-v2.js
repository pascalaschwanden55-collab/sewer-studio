/* Prüflauf für SewerStudio-Nova-Optimiert.html. Jeder Punkt meldet bestanden / fehlgeschlagen. */
const { chromium } = require('playwright-core'); const path = require('path'); const fs = require('fs');
const exe = path.join(process.env.USERPROFILE, 'AppData', 'Local', 'ms-playwright', 'chromium-1155', 'chrome-win', 'chrome.exe');
const htmlPath = path.resolve(__dirname, '..', 'SewerStudio-Nova-Optimiert-v2.html');
const file = 'file:///' + htmlPath.split(path.sep).join('/');
const outDir = process.argv[2] || path.resolve(__dirname, '..', 'nachweise-v2'); fs.mkdirSync(outDir, { recursive: true });
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
    await p.click('[data-pl="fwd5"]'); await p.click('[data-pl="fwd5"]'); const pos0 = await p.evaluate(() => window.__nova.state.player.pos); const n0 = await p.evaluate(() => window.__nova.state.player.draft.findings.length); const d0 = await p.evaluate(() => window.__nova.DATA.haltungen.find(h => h.id === window.__nova.state.player.id).findings.length);
    await p.click('#plNewEvent'); await p.waitForTimeout(100); const vsaOpen = await p.isVisible('#winVsa'); const vsaFocus = await p.evaluate(() => document.querySelector('#winVsa').contains(document.activeElement));
    await p.click('[data-close]', { strict: false }).catch(() => {}); // erster data-close ist im Player-Titel, deshalb gezielt:
    await p.click('#winVsa .wintitle [data-close]'); await p.waitForTimeout(100);
    const afterCancel = await p.evaluate(() => ({ vsa: document.querySelector('#winVsa').classList.contains('show'), player: document.querySelector('#winPlayer').classList.contains('show'), pos: window.__nova.state.player.pos, n: window.__nova.state.player.draft.findings.length, focus: document.activeElement.id }));
    rec('E3', 'Ereignis erfassen → Abbrechen: kehrt zum Player zurück, nichts übernommen, Position gleich', vsaOpen && vsaFocus && !afterCancel.vsa && afterCancel.player && afterCancel.pos === pos0 && afterCancel.n === n0 && afterCancel.focus === 'plNewEvent', JSON.stringify(afterCancel));
    await p.click('#plNewEvent'); await p.waitForTimeout(100); await p.click('#vsaFreq button:nth-child(2)'); await p.fill('#vsaM2', '1'); await p.click('#vsaApply'); await p.waitForTimeout(50); const errShown = await p.isVisible('#vsaErr'); await p.fill('#vsaM2', ''); await p.click('#vsaApply'); await p.waitForTimeout(100);
    const afterApply = await p.evaluate(() => ({ vsa: document.querySelector('#winVsa').classList.contains('show'), player: document.querySelector('#winPlayer').classList.contains('show'), pos: window.__nova.state.player.pos, n: window.__nova.state.player.draft.findings.length, bestand: window.__nova.DATA.haltungen.find(h => h.id === window.__nova.state.player.id).findings.length, id: window.__nova.state.player.id, sel: document.querySelector('#tblH tbody tr[aria-selected="true"]').dataset.id }));
    rec('E4', 'Ereignis erfassen → Übernehmen: Befund im Player-Entwurf, Bestand noch unverändert, Player offen, Haltung und Position erhalten', errShown && !afterApply.vsa && afterApply.player && afterApply.pos === pos0 && afterApply.n === n0 + 1 && afterApply.bestand === d0 && afterApply.id === afterApply.sel, JSON.stringify(afterApply) + ' Fehlerhinweis bei Ende<Start: ' + errShown);
    await p.keyboard.press('Escape'); await p.waitForTimeout(80); const askedFirst = await p.isVisible('#winConfirm'); await p.click('#cfDiscard'); await p.waitForTimeout(80); const playerClosed = !(await p.evaluate(() => document.querySelector('#winPlayer').classList.contains('show'))) && askedFirst; const focusBack = await p.evaluate(() => document.activeElement.id || (document.activeElement.dataset && document.activeElement.dataset.id) || document.activeElement.tagName);
    rec('E5', 'Esc bei Entwurf fragt nach; Verwerfen schliesst den Player; Fokus kehrt zum Auslöser oder zur gewählten Zeile', playerClosed && (focusBack === 'btnNext' || focusBack === 'h01'), `focus ${focusBack}`);
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

  // ---------- I: Schriftgrössen und Kontrast (alle Seiten, drei Fenster, Eingaben, Platzhalter, beide Themen) ----------
  const scan = (p) => p.evaluate(() => {
    const parse = c => { const v = (c.match(/[\d.]+/g) || ['0','0','0','0']).map(Number); return [v[0]||0, v[1]||0, v[2]||0, v.length > 3 ? v[3] : 1]; };
    const compose = (a, b) => [0,1,2].map(i => a[i] * a[3] + b[i] * (1 - a[3])).concat(1);
    const lum = c => c.slice(0,3).map(x => { x /= 255; return x <= 0.04045 ? x / 12.92 : Math.pow((x + .055) / 1.055, 2.4); }).reduce((s, x, i) => s + x * [.2126,.7152,.0722][i], 0);
    const ratio = (a, b) => (Math.max(lum(a), lum(b)) + .05) / (Math.min(lum(a), lum(b)) + .05);
    let count = 0, minSize = 99, minSizeEl = ''; const lows = [], uncertain = [], large = [];
    for (const el of document.querySelectorAll('body *')){
      if (['SCRIPT','STYLE','OPTION','TITLE'].includes(el.tagName)) continue;
      const isInput = el.matches('input:not([type=checkbox]):not([type=radio]),textarea');
      const own = Array.from(el.childNodes).filter(n => n.nodeType === 3).map(n => n.textContent.trim()).join('');
      const text = isInput ? (el.value || el.getAttribute('placeholder') || '') : own; if (!text) continue;
      const r = el.getBoundingClientRect(); if (!r.width || !r.height || r.bottom <= 0 || r.top >= innerHeight || r.right <= 0 || r.left >= innerWidth) continue;
      let visible = true, opacity = 1; for (let q = el; q; q = q.parentElement){ const s = getComputedStyle(q); if (s.display === 'none' || s.visibility === 'hidden') visible = false; opacity *= Number(s.opacity); }
      if (!visible || !opacity) continue;
      const hit = document.elementFromPoint(Math.min(innerWidth - 1, Math.max(0, r.left + r.width / 2)), Math.min(innerHeight - 1, Math.max(0, r.top + r.height / 2)));
      if (!hit || !(el.contains(hit) || hit === el)) continue;
      const style = getComputedStyle(el); const fs = parseFloat(style.fontSize); if (fs < minSize){ minSize = fs; minSizeEl = (el.getAttribute('class') || el.tagName) + ' „' + text.slice(0, 20) + '"'; }
      if (el.matches(':disabled')) continue;
      const stack = []; let complex = false;
      for (let q = el; q; q = q.parentElement){ const s = getComputedStyle(q); if (s.backgroundImage !== 'none') complex = true; const bg = parse(s.backgroundColor); stack.push(bg); if (bg[3] === 1) break; }
      let bg = [255,255,255,1]; while (stack.length) bg = compose(stack.pop(), bg);
      let fg = parse(el instanceof SVGElement ? style.fill : style.color);
      if (isInput && !el.value) fg = parse(getComputedStyle(el, '::placeholder').color);
      fg[3] *= opacity; fg = compose(fg, bg); const q = ratio(fg, bg); count++;
      const bold = parseInt(style.fontWeight, 10) >= 700; const isLarge = fs >= 24 || (bold && fs >= 18.66);
      const detail = (el.getAttribute('class') || el.tagName) + ' „' + text.slice(0, 24) + '" ' + q.toFixed(2) + ':1 ' + fs + 'px';
      if (q < 4.5){ if (complex) uncertain.push(detail); else if (isLarge) large.push(detail); else lows.push(detail); }
    }
    return { count, minSize, minSizeEl, lows, uncertain, large };
  });
  for (const [dark, name] of [[false, 'hell'], [true, 'dunkel']]){
    const { ctx, p } = await fresh(b, 1440, 900, { dark }); await p.click(dark ? '#thDark' : '#thLight'); await p.click('#moOff'); await p.waitForTimeout(100);
    let tot = 0, minSize = 99, minSizeEl = ''; const lows = [], uncertain = [], large = []; const scanned = [];
    const take = async (label) => { const r = await scan(p); tot += r.count; if (r.minSize < minSize){ minSize = r.minSize; minSizeEl = label + ': ' + r.minSizeEl; } r.lows.forEach(x => lows.push(label + ': ' + x)); r.uncertain.forEach(x => uncertain.push(label + ': ' + x)); r.large.forEach(x => large.push(label + ': ' + x)); scanned.push(label); };
    for (const pg of ['overview','projekt','haltungen','schaechte','import','export','medien','druck','dossiers','matrix','smatrix','schatten','vsa','diagnose','einstellungen']){ await p.click(`.nav[data-nav="${pg}"]`); await p.waitForTimeout(60); await take(pg); }
    await p.click('.nav[data-nav="haltungen"]'); await p.click('#btnPlayerH'); await p.waitForTimeout(100); await take('player');
    await p.click('#plNewEvent'); await p.waitForTimeout(100); await take('codierung'); await p.click('#winVsa .wintitle [data-close]'); await p.waitForTimeout(50); await p.click('#plDiscard'); await p.waitForTimeout(50);
    await p.click('[data-open="winStudio"]'); await p.waitForTimeout(100); await take('training'); await p.keyboard.press('Escape');
    rec('I1-' + name, `Schriftgrösse ${name}: kleinster sichtbarer Text ≥ 11 px auf 15 Seiten und 3 Fenstern`, minSize >= 11, `kleinste ${minSize} px (${minSizeEl}), ${scanned.length} Ansichten`);
    rec('I2-' + name, `Kontrast ${name}: normaler Text ≥ 4,5:1 auf 15 Seiten, 3 Fenstern, Eingaben und Platzhaltern (deckende Hintergründe)`, lows.length === 0, `${tot} Textelemente, ${lows.length} unter 4,5:1${lows.length ? ': ' + lows.slice(0, 5).join('; ') : ''}`);
    rec('I3-' + name, `Kontrast ${name}: Projektziel 4,5:1 auch für grossen Text (WCAG erlaubt dort 3:1)`, large.length === 0, large.length ? large.slice(0, 5).join('; ') : 'kein grosser Text unter 4,5:1');
    rec('I4-' + name, `Kontrast ${name}: Texte auf Verläufen gesondert gelistet (nicht als Verstoss gewertet)`, null, uncertain.length ? uncertain.length + ' Stellen: ' + uncertain.slice(0, 4).join('; ') : 'keine');
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

  // ---------- K: Nachbesserung R01 bis R06 ----------
  { const { ctx, p } = await fresh(b, 1440, 900);
    await p.click('.nav[data-nav="haltungen"]'); await p.click('#tblH tbody tr[data-id="h02"]'); await p.fill('#f_h_baujahr', '2001'); await p.click('.nav[data-nav="overview"]'); await p.click('#btnNext'); await p.waitForTimeout(100);
    const before = await p.evaluate(() => ({ sel: window.__nova.state.ent.h.sel, player: document.querySelector('#winPlayer').classList.contains('show'), confirm: document.querySelector('#winConfirm').classList.contains('show'), dialoge: window.__nova.state.dialogs.map(d => d.id) }));
    await p.click('#cfCancel'); await p.waitForTimeout(80);
    const after = await p.evaluate(() => ({ sel: window.__nova.state.ent.h.sel, player: document.querySelector('#winPlayer').classList.contains('show'), form: document.querySelector('#f_h_name').value, dirty: !document.querySelector('#dirtyH').hidden, page: document.querySelector('.page.show').id }));
    rec('K1', 'R01 Abbrechen: Rückfrage ohne Player, Auswahl und Entwurf bleiben auf 07.6588-6587', before.confirm && !before.player && before.dialoge.length === 1 && !after.player && after.sel === 'h02' && after.form === '07.6588-6587' && after.dirty, JSON.stringify({ before, after }));
    await p.click('#btnNext'); await p.waitForTimeout(80); await p.click('#cfDiscard'); await p.waitForTimeout(120);
    const disc = await p.evaluate(() => ({ sel: window.__nova.state.ent.h.sel, player: document.querySelector('#winPlayer').classList.contains('show'), playerId: window.__nova.state.player.id, form: document.querySelector('#f_h_name').value, page: document.querySelector('.page.show').id, h02: window.__nova.DATA.haltungen.find(h => h.id === 'h02').baujahr }));
    rec('K2', 'R01 Verwerfen und wechseln: Auswahl, Formular und Player gemeinsam auf 78998-79002, Ursprung unverändert', disc.sel === 'h01' && disc.player && disc.playerId === 'h01' && disc.form === '78998-79002' && disc.page === 'page-haltungen' && disc.h02 === 1994, JSON.stringify(disc));
    await p.click('#plDiscard'); await p.waitForTimeout(50);
    await p.click('#tblH tbody tr[data-id="h02"]'); await p.fill('#f_h_baujahr', '2003'); await p.click('.nav[data-nav="overview"]'); await p.click('#kiRuns [data-go-h="h01"]'); await p.waitForTimeout(80); const kiAsk = await p.isVisible('#winConfirm') && !(await p.isVisible('#winPlayer')); await p.click('#cfSave'); await p.waitForTimeout(120);
    const sv = await p.evaluate(() => ({ sel: window.__nova.state.ent.h.sel, player: document.querySelector('#winPlayer').classList.contains('show'), playerId: window.__nova.state.player.id, h02: window.__nova.DATA.haltungen.find(h => h.id === 'h02').baujahr, dirty: !document.querySelector('#dirtyH').hidden }));
    rec('K3', 'R01 KI-Aufgabenliste: Rückfrage vor dem Player; Speichern und wechseln übernimmt Wert und öffnet den richtigen Player', kiAsk && sv.sel === 'h01' && sv.player && sv.playerId === 'h01' && String(sv.h02) === '2003' && !sv.dirty, JSON.stringify(sv));
    await p.click('#plDiscard'); await p.evaluate(() => localStorage.clear()); await ctx.close(); }
  { const { ctx, p } = await fresh(b, 1440, 900);
    await p.evaluate(() => { window.__nova.showPage('haltungen'); window.__nova.select('h', 'h05'); window.__nova.openPlayer('h05', document.querySelector('#btnPlayerH')); }); await p.waitForTimeout(100);
    const b0 = await p.evaluate(() => { const h = window.__nova.DATA.haltungen.find(x => x.id === 'h05'); return JSON.stringify(h.findings) + '|' + h.pruefung + '|' + h.ki.length; });
    await p.click('#plNewEvent'); await p.fill('#vsaNote', 'Nachprüfung: verwerfen'); await p.click('#vsaApply'); await p.waitForTimeout(60);
    await p.click('#plSide [data-fconfirm="1"]'); await p.waitForTimeout(40); await p.click('#plSide [data-freject="2"]'); await p.waitForTimeout(40); await p.click('#plSide [data-ki-confirm="0"]'); await p.waitForTimeout(40);
    const draft = await p.evaluate(() => ({ changes: window.__nova.playerDraftChanges(), badge: !document.querySelector('#plDraft').hidden, bestand: (() => { const h = window.__nova.DATA.haltungen.find(x => x.id === 'h05'); return JSON.stringify(h.findings) + '|' + h.pruefung + '|' + h.ki.length; })() }));
    await p.click('#plDiscard'); await p.waitForTimeout(80);
    const afterDiscard = await p.evaluate(() => ({ player: document.querySelector('#winPlayer').classList.contains('show'), bestand: (() => { const h = window.__nova.DATA.haltungen.find(x => x.id === 'h05'); return JSON.stringify(h.findings) + '|' + h.pruefung + '|' + h.ki.length; })() }));
    await p.click('#btnPlayerH'); await p.waitForTimeout(80); const reopened = await p.evaluate(() => document.querySelector('#plSide').innerText.includes('Nachprüfung: verwerfen') || document.querySelector('#plDraft').hidden === false);
    rec('K4', 'R06 Beenden ohne Übernahme verwirft neuen, bestätigten, gelöschten Befund und Prüfstatus; Bestand unverändert; beim Wiederöffnen weg', draft.changes >= 3 && draft.badge && draft.bestand === b0 && !afterDiscard.player && afterDiscard.bestand === b0 && !reopened, JSON.stringify({ changes: draft.changes, bestandGleich: afterDiscard.bestand === b0, reopened }));
    await p.click('#plNewEvent'); await p.fill('#vsaNote', 'Nachprüfung: übernehmen'); await p.click('#vsaApply'); await p.waitForTimeout(60); await p.click('#plApply'); await p.waitForTimeout(80);
    const applied = await p.evaluate(() => ({ player: document.querySelector('#winPlayer').classList.contains('show'), hat: window.__nova.DATA.haltungen.find(x => x.id === 'h05').findings.some(f => f.text === 'Nachprüfung: übernehmen'), side: document.querySelector('#sideHBody').textContent.includes('Nachprüfung: übernehmen') }));
    rec('K5', 'R06 Codierung übernehmen schreibt den Entwurf in den Bestand und die Übersicht', !applied.player && applied.hat && applied.side, JSON.stringify(applied));
    await p.click('#btnPlayerH'); await p.click('#plNewEvent'); await p.click('#vsaApply'); await p.waitForTimeout(60); await p.keyboard.press('Escape'); await p.waitForTimeout(60);
    const esc = await p.evaluate(() => ({ confirm: document.querySelector('#winConfirm').classList.contains('show'), title: document.querySelector('#cfTitle').textContent }));
    await p.click('#cfCancel'); await p.waitForTimeout(40); const stillOpen = await p.isVisible('#winPlayer'); await p.click('#plDiscard');
    rec('K6', 'R06 Esc oder Schliessen bei Entwurf fragt nach; Zurück zum Player behält Entwurf und Fenster', esc.confirm && esc.title.includes('nicht übernommen') && stillOpen, JSON.stringify(esc));
    await ctx.close(); }
  { const { ctx, p } = await fresh(b, 1440, 900);
    await p.click('[data-open="winStudio"]'); await p.waitForTimeout(80);
    const r0 = await p.evaluate(() => document.querySelector('#stRelease').disabled);
    await p.click('#stAccept'); const r1 = await p.evaluate(() => !document.querySelector('#stRelease').disabled);
    await p.fill('#stCode', ''); await p.fill('#stDesc', ''); const r2 = await p.evaluate(() => document.querySelector('#stRelease').disabled);
    const forced = await p.evaluate(() => { const b = document.querySelector('#stRelease'); b.disabled = false; b.click(); return { disabledAgain: b.disabled, note: document.querySelector('#stNote').textContent, toast: document.querySelector('#toasts').textContent }; });
    rec('K7', 'R02 Akzeptieren, dann Felder leeren: Freigabe gesperrt; erzwungener Klick wird abgelehnt', r0 && r1 && r2 && forced.disabledAgain && forced.note.includes('abgelehnt') && !forced.toast.includes('Für Training freigegeben'), JSON.stringify({ r0, r1, r2, note: forced.note.slice(0, 60) }));
    await p.fill('#stCode', 'BABBA'); await p.fill('#stDesc', 'Querriss im Scheitel, 2 mm breit.'); await p.click('#stAccept'); const r3 = await p.evaluate(() => !document.querySelector('#stRelease').disabled);
    await p.fill('#stStufe', '4'); const r4 = await p.evaluate(() => document.querySelector('#stRelease').disabled);
    await p.click('#stAccept'); await p.click('#stThumbs .thumb:nth-child(2)'); const r5 = await p.evaluate(() => document.querySelector('#stRelease').disabled);
    await p.click('#stAccept'); await p.click('#stRelease'); const r6 = await p.evaluate(() => document.querySelector('#toasts').textContent.includes('Für Training freigegeben') && document.querySelector('#stRelease').disabled);
    rec('K8', 'R02 Jede Änderung (Stufe, Bildauswahl) hebt die Bestätigung auf; nach erneutem Akzeptieren ist die Freigabe möglich und wird danach wieder gesperrt', r3 && r4 && r5 && r6, JSON.stringify({ r3, r4, r5, r6 }));
    await ctx.close(); }
  { const { ctx, p } = await fresh(b, 1440, 900);
    await p.click('.nav[data-nav="haltungen"]'); await p.fill('#f_h_baujahr', '2002');
    await p.evaluate(() => { window.__origSet = Storage.prototype.setItem; Storage.prototype.setItem = function(){ throw new DOMException('Prüffehler: Speicher voll', 'QuotaExceededError'); }; document.querySelector('#toasts').innerHTML = ''; });
    await p.click('#btnSaveH'); await p.waitForTimeout(60);
    const f1 = await p.evaluate(() => ({ toast: document.querySelector('#toasts').textContent, dirty: !document.querySelector('#dirtyH').hidden, bestand: window.__nova.DATA.haltungen[0].baujahr, feld: document.querySelector('#f_h_baujahr').value, saveEnabled: !document.querySelector('#btnSaveH').disabled }));
    rec('K9', 'R03 Speicherfehler: Fehlermeldung statt Erfolg, Entwurf und Marke bleiben, Bestand unverändert', f1.toast.includes('Nicht gespeichert') && !f1.toast.includes('Gespeichert (Vorschau)') && f1.dirty && f1.bestand === 1978 && f1.feld === '2002' && f1.saveEnabled, JSON.stringify(f1));
    await p.click('#tblH tbody tr[data-id="h02"]'); await p.waitForTimeout(60); await p.click('#cfSave'); await p.waitForTimeout(80);
    const f2 = await p.evaluate(() => ({ confirmOpen: document.querySelector('#winConfirm').classList.contains('show'), note: document.querySelector('#cfNote').textContent, sel: window.__nova.state.ent.h.sel, dirty: !document.querySelector('#dirtyH').hidden }));
    rec('K10', 'R03 „Speichern und wechseln" wechselt bei Speicherfehler nicht; Hinweis im Dialog', f2.confirmOpen && f2.note.includes('fehlgeschlagen') && f2.sel === 'h01' && f2.dirty, JSON.stringify(f2));
    await p.click('#cfCancel'); await p.evaluate(() => { Storage.prototype.setItem = window.__origSet; document.querySelector('#toasts').innerHTML = ''; });
    await p.click('#btnSaveH'); await p.waitForTimeout(60); const f3 = await p.evaluate(() => ({ toast: document.querySelector('#toasts').textContent, dirty: !document.querySelector('#dirtyH').hidden, bestand: window.__nova.DATA.haltungen[0].baujahr }));
    await p.reload(); await p.waitForTimeout(300); const f4 = await p.evaluate(() => window.__nova.DATA.haltungen[0].baujahr);
    rec('K11', 'R03 Wiederholung nach Fehler speichert erfolgreich und überlebt das Neuladen', f3.toast.includes('Gespeichert (Vorschau)') && !f3.dirty && String(f3.bestand) === '2002' && String(f4) === '2002', JSON.stringify({ f3, f4 }));
    await p.evaluate(() => localStorage.clear()); await ctx.close(); }
  { const { ctx, p } = await fresh(b, 1440, 900);
    await p.click('.nav[data-nav="haltungen"]'); await p.click('#btnPlayerH'); await p.waitForTimeout(80);
    await p.keyboard.press('Control+k'); const c1 = await p.evaluate(() => document.querySelector('#winPlayer').contains(document.activeElement));
    await p.keyboard.press('F3'); const c2 = await p.evaluate(() => document.querySelector('#winPlayer').contains(document.activeElement));
    const first = await p.evaluate(() => { const f = Array.from(document.querySelectorAll('#winPlayer button:not([disabled]),#winPlayer input:not([disabled]),#winPlayer select:not([disabled]),#winPlayer [tabindex]:not([tabindex="-1"])')).filter(x => x.offsetParent !== null); f[0].focus(); return f[0].id || f[0].textContent.trim().slice(0, 20); });
    await p.keyboard.press('Shift+Tab'); const c3 = await p.evaluate(() => document.querySelector('#winPlayer').contains(document.activeElement));
    await p.click('#plNewEvent'); await p.waitForTimeout(80); await p.keyboard.press('Control+k'); const c4 = await p.evaluate(() => document.querySelector('#winVsa').contains(document.activeElement));
    let inside = true; for (let i = 0; i < 60; i++){ await p.keyboard.press('Tab'); if (!(await p.evaluate(() => document.querySelector('#winVsa').contains(document.activeElement)))){ inside = false; break; } }
    await p.keyboard.press('F1'); await p.waitForTimeout(60); const c5 = await p.evaluate(() => window.__nova.state.dialogs.map(d => d.id).join(',') + '|' + document.querySelector('#winHelp').contains(document.activeElement));
    await p.keyboard.press('Escape'); await p.waitForTimeout(40); const c6 = await p.evaluate(() => window.__nova.state.dialogs.map(d => d.id).join(',') + '|' + document.querySelector('#winVsa').contains(document.activeElement));
    await p.keyboard.press('Escape'); await p.waitForTimeout(40); const c7 = await p.evaluate(() => window.__nova.state.dialogs.map(d => d.id).join(',') + '|' + document.querySelector('#winPlayer').contains(document.activeElement));
    await p.keyboard.press('Escape'); await p.waitForTimeout(40); const c8 = await p.evaluate(() => window.__nova.state.dialogs.length + '|' + (document.activeElement.dataset.id || document.activeElement.id));
    rec('K12', 'R04 Ctrl+K und F3 verlassen weder Player noch Codierfenster; Shift+Tab und Tab bleiben im obersten Dialog', c1 && c2 && c3 && c4 && inside, JSON.stringify({ c1, c2, c3, c4, tabBleibt: inside, erstes: first }));
    rec('K13', 'R04 Drei Dialogebenen: Esc schliesst je die oberste, Fokus kehrt Ebene für Ebene zurück', c5 === 'winPlayer,winVsa,winHelp|true' && c6 === 'winPlayer,winVsa|true' && c7 === 'winPlayer|true' && (c8 === '0|btnPlayerH' || c8 === '0|h01'), [c5, c6, c7, c8].join(' → '));
    await ctx.close(); }
  for (const [w, h, soll] of [[1366, 768, 7], [1440, 900, 9], [1920, 1080, 12]]){ const { ctx, p } = await fresh(b, w, h); await p.click('.nav[data-nav="haltungen"]'); await p.waitForTimeout(150); const rows = await visibleRows(p); rec('K14-' + w, `${w}×${h}: weiterhin ${soll} vollständige Zeilen sichtbar`, rows === soll, rows + ' Zeilen'); await ctx.close(); }

  await b.close();
  fs.writeFileSync(path.join(outDir, 'pruefergebnis.json'), JSON.stringify({ datei: htmlPath, zeit: new Date().toISOString(), ergebnisse: results, skriptfehler: errs }, null, 2));
  const bestanden = results.filter(r => r.ergebnis === 'bestanden').length, fehl = results.filter(r => r.ergebnis === 'fehlgeschlagen').length;
  console.log(`\n${bestanden} bestanden, ${fehl} fehlgeschlagen, ${results.length - bestanden - fehl} nicht geprüft`);
})().catch(e => { console.error('FAIL', e); process.exit(1); });
