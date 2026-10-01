async (page) => {
  const result = {};
  await page.setViewportSize({width:1440,height:1000});
  await page.goto('http://127.0.0.1:8804/SewerStudio-Nova-Komplett-UTF8.html');
  await page.getByRole('button',{name:'ruhig',exact:true}).click();
  await page.locator('.nav[data-nav=haltungen]').click();
  await page.locator('#rows tr').nth(1).click();
  await page.waitForTimeout(650);
  await page.screenshot({path:'output/playwright/nova-haltungen-falsche-details.png'});
  result.record = await page.evaluate(() => ({row:document.querySelector('#rows tr.sel')?.innerText, heading:document.querySelector('#detName').innerText, facts:document.querySelector('#page-haltungen .facts').innerText}));
  await page.getByRole('button',{name:'Training Studio',exact:true}).click();
  await page.waitForTimeout(650);
  result.training = await page.evaluate(() => {
    const r=document.querySelector('#win-studio .winbody').lastElementChild.getBoundingClientRect();
    return {viewport:innerWidth,panelLeft:r.left,panelRight:r.right,panelWidth:r.width,visibleWidth:Math.max(0,Math.min(innerWidth,r.right)-Math.max(0,r.left))};
  });
  await page.screenshot({path:'output/playwright/nova-training.png'});
  await page.keyboard.press('Escape');
  await page.setViewportSize({width:1366,height:768});
  await page.waitForTimeout(650);
  result.rows = await page.evaluate(() => {
    const table=document.querySelector('#rows');
    const ancestors=[];
    let p=table.parentElement;
    while(p) { const s=getComputedStyle(p); if(['auto','scroll','hidden'].includes(s.overflowY)){const r=p.getBoundingClientRect();ancestors.push({cls:p.className,top:r.top,bottom:r.bottom,overflow:s.overflowY});} p=p.parentElement; }
    return {viewportHeight:innerHeight,ancestors,rows:[...table.children].map(e=>{const r=e.getBoundingClientRect();return {top:r.top,bottom:r.bottom};})};
  });
  await page.screenshot({path:'output/playwright/nova-haltungen-1366.png'});
  await page.setViewportSize({width:1440,height:1000});
  await page.locator('.nav[data-nav=overview]').click();
  await page.locator('.nav[data-nav=export]').focus();
  await page.keyboard.press('Enter');
  result.keyboardAfterExportEnter=await page.locator('.page.show').getAttribute('id');
  await page.goto('http://127.0.0.1:8804/SewerStudio-Nova-Vorschau-UTF8.html');
  await page.waitForTimeout(800);
  result.preview=await page.evaluate(()=>({title:document.title,pages:document.querySelectorAll('.page').length,mode:document.compatMode,doctype:!!document.doctype,lang:document.documentElement.lang}));
  await page.screenshot({path:'output/playwright/nova-vorschau.png'});
  return result;
}
