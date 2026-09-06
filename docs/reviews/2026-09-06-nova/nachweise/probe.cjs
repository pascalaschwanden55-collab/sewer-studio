async (page) => {
  const result = {};
  await page.setViewportSize({width:1440,height:1000});
  await page.reload();
  await page.getByRole('button', {name:'Hell · Glas', exact:true}).click();
  result.base = await page.evaluate(() => ({
    title:document.title, charset:document.characterSet, mode:document.compatMode,
    doctype:!!document.doctype, lang:document.documentElement.lang,
    viewportMeta:!!document.querySelector('meta[name=viewport]'),
    pages:document.querySelectorAll('.page').length, windows:document.querySelectorAll('.win').length,
    buttons:document.querySelectorAll('button').length,
    inlineButtonHandlers:document.querySelectorAll('button[onclick]').length,
    nav:[...document.querySelectorAll('.nav')].map(e=>({name:e.textContent.trim(),tabIndex:e.tabIndex,role:e.getAttribute('role')})),
    minTextSize:Math.min(...[...document.querySelectorAll('body *')].filter(e=>e.getClientRects().length&&[...e.childNodes].some(n=>n.nodeType===3&&n.textContent.trim())).map(e=>parseFloat(getComputedStyle(e).fontSize))),
    fonts:document.fonts.status
  }));
  await page.getByRole('button', {name:'Nächste Haltung prüfen',exact:false}).click();
  result.nextButtonPage = await page.locator('.page.show').getAttribute('id');
  result.pages=[];
  for(const key of ['overview','projekt','haltungen','schaechte','import','export','medien','druck','dossiers','matrix','smatrix','schatten','vsa','diagnose','einstellungen']) {
    await page.locator('.nav[data-nav='+key+']').click();
    result.pages.push(await page.evaluate(()=>{
      const e=document.querySelector('.page.show'),r=e.getBoundingClientRect();
      return {page:e.id,right:Math.round(r.right),viewport:innerWidth,contentHeight:Math.round(r.height),buttons:e.querySelectorAll('button').length};
    }));
  }
  await page.locator('.nav[data-nav=haltungen]').click();
  result.rowBefore = await page.locator('#page-haltungen .facts').innerText();
  await page.locator('#rows tr').nth(1).click();
  result.rowAfter = {heading:await page.locator('#detName').innerText(),facts:await page.locator('#page-haltungen .facts').innerText()};
  await page.screenshot({path:'output/playwright/nova-haltungen-falsche-details.png'});
  result.chipsBefore = await page.locator('#page-schaechte .vchip[aria-pressed=true]').count();
  await page.locator('#page-haltungen .vchip').nth(1).click();
  result.chipsAfter = await page.locator('#page-schaechte .vchip[aria-pressed=true]').count();
  result.shaftLabelsBefore = await page.locator('#page-schaechte label:not(.hide)').count();
  await page.locator('#fieldSearch').fill('Baujahr');
  await page.locator('.nav[data-nav=schaechte]').click();
  result.shaftLabelsAfter = await page.locator('#page-schaechte label:not(.hide)').count();
  await page.screenshot({path:'output/playwright/nova-schaechte-fremder-filter.png'});
  await page.locator('.nav[data-nav=haltungen]').click();
  await page.locator('#fieldSearch').fill('');
  await page.getByRole('button',{name:'ruhig',exact:true}).click();
  const frame=await page.locator('#engine').evaluate(e=>e.toDataURL());
  await page.waitForTimeout(350);
  result.canvasStillChangesWhenQuiet = frame!==await page.locator('#engine').evaluate(e=>e.toDataURL());
  await page.getByRole('button',{name:'Player',exact:true}).click();
  result.dialog = await page.evaluate(()=>({roleCount:document.querySelectorAll('[role=dialog]').length,focusInside:!!document.activeElement.closest('.win'),activeTag:document.activeElement.tagName}));
  await page.screenshot({path:'output/playwright/nova-player.png'});
  await page.locator('#win-player').getByRole('button',{name:'Neues Ereignis erfassen'}).click();
  await page.locator('#win-vsa').getByRole('button',{name:'Übernehmen',exact:true}).click();
  result.windowsAfterCoding = await page.locator('.win.show').count();
  await page.getByRole('button',{name:'Training Studio',exact:true}).click();
  await page.screenshot({path:'output/playwright/nova-training.png'});
  await page.keyboard.press('Escape');
  await page.locator('.nav[data-nav=overview]').click();
  await page.getByRole('button',{name:'Dunkel · Cockpit',exact:true}).click();
  await page.screenshot({path:'output/playwright/nova-uebersicht-dunkel.png'});
  await page.getByRole('button',{name:'Hell · Glas',exact:true}).click();
  await page.setViewportSize({width:1366,height:768});
  await page.locator('.nav[data-nav=haltungen]').click();
  await page.screenshot({path:'output/playwright/nova-haltungen-1366.png'});
  result.width1366=await page.evaluate(()=>({viewport:innerWidth,doc:document.documentElement.scrollWidth,right:document.querySelector('.main').getBoundingClientRect().right}));
  await page.setViewportSize({width:1000,height:768});
  result.width1000=await page.evaluate(()=>({viewport:innerWidth,doc:document.documentElement.scrollWidth,
    visibleNavChildren:[...document.querySelectorAll('.nav span')].filter(e=>e.getClientRects().length).length,
    mainRight:document.querySelector('.main').getBoundingClientRect().right}));
  await page.screenshot({path:'output/playwright/nova-1000-navigation.png'});
  return result;
}
