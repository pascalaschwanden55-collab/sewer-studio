/* Führt die Codex-Gegenproben (gegenproben.cjs, kontrast.cjs, player_abbruch.cjs) unverändert gegen die v2-Datei aus. */
const { chromium } = require('playwright-core'); const path = require('path'); const fs = require('fs');
const exe = path.join(process.env.USERPROFILE, 'AppData', 'Local', 'ms-playwright', 'chromium-1155', 'chrome-win', 'chrome.exe');
const htmlPath = path.resolve(__dirname, '..', 'SewerStudio-Nova-Optimiert-v2.html');
const file = 'file:///' + htmlPath.split(path.sep).join('/');
const codex = process.argv[2]; const outDir = process.argv[3]; fs.mkdirSync(path.join(outDir, 'output', 'playwright'), { recursive: true });
(async () => {
  const b = await chromium.launch({ executablePath: exe }); const out = {};
  process.chdir(outDir);
  for (const name of ['gegenproben', 'kontrast', 'player_abbruch']){
    const ctx = await b.newContext({ viewport: { width: 1440, height: 900 } }); const p = await ctx.newPage();
    await p.goto(file); await p.waitForTimeout(300);
    const fn = eval('(' + fs.readFileSync(path.join(codex, name + '.cjs'), 'utf8') + ')');
    try { out[name] = await fn(p); } catch (e){ out[name] = { fehler: e.message }; }
    await ctx.close();
  }
  await b.close();
  // Kontrast zusammenfassen
  if (Array.isArray(out.kontrast)){ out.kontrast_zusammenfassung = out.kontrast.map(r => ({ theme: r.theme, page: r.page, count: r.count, minSize: r.minSize, lows: r.lows.length, uncertain: r.uncertain.length, lowsDetail: r.lows.slice(0, 5).map(l => l.cls + ' „' + l.text.slice(0, 20) + '" ' + l.ratio) })); }
  fs.writeFileSync(path.join(outDir, 'gegenpruefung-v2.json'), JSON.stringify(out, null, 2));
  console.log('gegenproben-fehler', out.gegenproben && out.gegenproben.fehler, 'kontrast-fehler', out.kontrast && out.kontrast.fehler);
  const g = out.gegenproben || {}, pa = out.player_abbruch, k = out.kontrast_zusammenfassung || [];
  console.log('wechselVorAbbruch', JSON.stringify(g.wechselVorAbbruch)); console.log('wechselNachAbbruch', JSON.stringify(g.wechselNachAbbruch));
  console.log('dialogSuche', JSON.stringify(g.dialogSuche)); console.log('training', JSON.stringify(g.training)); console.log('speicherFehler', JSON.stringify(g.speicherFehler));
  console.log('layout', JSON.stringify(g.layout)); console.log('codierung', JSON.stringify(g.codierung)); console.log('skriptfehler', JSON.stringify(g.skriptfehler));
  console.log('player_abbruch', JSON.stringify(pa));
  console.log('kontrast lows gesamt', k.reduce((a, r) => a + r.lows, 0), 'uncertain', k.reduce((a, r) => a + r.uncertain, 0), 'minSize', Math.min(...k.map(r => r.minSize)));
  k.filter(r => r.lows).forEach(r => console.log('  ', r.theme, r.page, r.lowsDetail.join(' | ')));
})().catch(e => { console.error('FAIL', e); process.exit(1); });
