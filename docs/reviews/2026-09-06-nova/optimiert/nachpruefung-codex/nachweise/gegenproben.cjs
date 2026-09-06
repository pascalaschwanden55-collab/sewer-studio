async (page) => {
  const r={}; const errors=[]; page.on('pageerror',e=>errors.push(e.message));
  const fresh=async()=>{await page.evaluate(()=>localStorage.clear());await page.reload();await page.setViewportSize({width:1440,height:900});await page.locator('#thLight').click();await page.locator('#moOff').click();};
  await fresh();
  await page.locator('.nav[data-nav=haltungen]').click();
  await page.locator('#tblH tr[data-id=h02]').click();
  r.auswahl=await page.evaluate(()=>({name:document.querySelector('#f_h_name').value,material:document.querySelector('#f_h_material').value,dn:document.querySelector('#f_h_dn').value,laenge:document.querySelector('#f_h_laenge').value,detail:document.querySelector('#sideHName').textContent}));
  const labels=()=>page.locator('#secsS label:not(.hide)').count();
  r.feldsuche={vorher:await labels()};await page.locator('#fsH').fill('Baujahr');r.feldsuche.nachher=await labels();await page.locator('#fsH').fill('');
  await page.locator('#f_h_baujahr').fill('2001');
  await page.locator('#tblH tr[data-id=h01]').click();
  await page.locator('#cfCancel').click();
  r.abbrechen=await page.locator('#f_h_name').inputValue();
  await page.locator('#tblH tr[data-id=h01]').click();await page.locator('#cfSave').click();
  await page.reload();
  r.gespeichert=await page.evaluate(()=>window.__nova.DATA.haltungen.find(h=>h.id==='h02').baujahr);
  await fresh();
  r.kennzahlen=await page.evaluate(()=>{const h=window.__nova.DATA.haltungen;return {anzahl:h.length,geprueft:h.filter(x=>x.pruefung==='geprueft').length,dringend:h.filter(x=>x.zk===0||x.zk===1).length,text:document.querySelector('#ovText').textContent,anzeige:document.querySelector('#kpiDring').textContent};});
  r.layout=[];
  for(const [width,height] of [[1366,768],[1440,900],[1920,1080]]){
    await page.setViewportSize({width,height});await page.locator('.nav[data-nav=haltungen]').click();
    r.layout.push(await page.evaluate(()=>{const wrap=document.querySelector('#page-haltungen .tablewrap').getBoundingClientRect(),head=document.querySelector('#tblH thead').getBoundingClientRect();return{breite:innerWidth,hoehe:innerHeight,zeilen:[...document.querySelectorAll('#tblH tbody tr')].filter(e=>{const x=e.getBoundingClientRect();return x.top>=head.bottom&&x.bottom<=wrap.bottom;}).length};}));
  }
  await page.setViewportSize({width:1366,height:768});
  await page.screenshot({path:'output/playwright/nova-opt-haltungen-1366.png'});
  await page.locator('#btnPlayerH').click();
  await page.locator('[data-pl=fwd5]').click();
  r.codierung={vorher:await page.evaluate(()=>({id:window.__nova.state.player.id,position:window.__nova.state.player.pos,befunde:window.__nova.DATA.haltungen[0].findings.length}))};
  await page.locator('#plNewEvent').click();await page.locator('#vsaApply').click();
  r.codierung.nachher=await page.evaluate(()=>({id:window.__nova.state.player.id,position:window.__nova.state.player.pos,befunde:window.__nova.DATA.haltungen[0].findings.length,player:document.querySelector('#winPlayer').classList.contains('show'),vsa:document.querySelector('#winVsa').classList.contains('show')}));
  await page.keyboard.press('Escape');
  await page.locator('#moOn').click();await page.waitForTimeout(200);await page.locator('#moOff').click();
  const frame=await page.locator('#engine').evaluate(e=>e.toDataURL());await page.waitForTimeout(350);
  r.ruhig={bildUnveraendert:frame===await page.locator('#engine').evaluate(e=>e.toDataURL()),schleife:await page.evaluate(()=>window.__nova.engine.isRunning())};

  // Kombination: ungespeicherte Haltung und nächste Aufgabe.
  await fresh();await page.locator('.nav[data-nav=haltungen]').click();await page.locator('#tblH tr[data-id=h02]').click();await page.locator('#f_h_baujahr').fill('2001');
  await page.locator('.nav[data-nav=overview]').click();await page.locator('#btnNext').click();
  r.wechselVorAbbruch=await page.evaluate(()=>({auswahl:window.__nova.state.ent.h.sel,player:window.__nova.state.player.id,dialoge:window.__nova.state.dialogs.map(d=>d.id),fokus:document.activeElement.id}));
  await page.screenshot({path:'output/playwright/nova-opt-wechsel-konflikt.png'});
  await page.locator('#cfCancel').click();
  r.wechselNachAbbruch=await page.evaluate(()=>({auswahl:window.__nova.state.ent.h.sel,player:window.__nova.state.player.id,dialoge:window.__nova.state.dialogs.map(d=>d.id),form:document.querySelector('#f_h_name').value,playerName:document.querySelector('#plName').textContent}));
  await page.keyboard.press('Control+k');
  r.dialogSuche=await page.evaluate(()=>({fokus:document.activeElement.id,fokusImOberstenDialog:!!document.getElementById(window.__nova.state.dialogs.at(-1).id).contains(document.activeElement)}));

  // Bestätigte Trainingsdaten nachträglich ungültig machen.
  await fresh();await page.locator('[data-open=winStudio]').click();
  r.training={vorherFreigabeAktiv:await page.locator('#stRelease').isEnabled()};
  await page.locator('#stAccept').click();r.training.nachBestaetigungAktiv=await page.locator('#stRelease').isEnabled();
  await page.locator('#stCode').fill('');await page.locator('#stDesc').fill('');
  r.training.nachLeerenAktiv=await page.locator('#stRelease').isEnabled();
  await page.locator('#stRelease').click();r.training.meldung=await page.locator('#toasts').innerText();
  await page.screenshot({path:'output/playwright/nova-opt-training-leer-freigegeben.png'});

  // Fehler des Browserspeichers: kontrolliert nur in diesem Prüf-Browser erzeugt.
  await fresh();await page.locator('.nav[data-nav=haltungen]').click();await page.locator('#f_h_baujahr').fill('2002');
  await page.evaluate(()=>{Storage.prototype.setItem=function(){throw new DOMException('Prüffehler: Speicher voll','QuotaExceededError');};});
  await page.locator('#btnSaveH').click();
  r.speicherFehler=await page.evaluate(()=>({meldung:document.querySelector('#toasts').textContent,dirty:!document.querySelector('#dirtyH').hidden,wert:window.__nova.DATA.haltungen[0].baujahr}));
  await page.reload();r.speicherFehler.nachNeuladen=await page.evaluate(()=>window.__nova.DATA.haltungen[0].baujahr);
  r.skriptfehler=errors;
  return r;
}
