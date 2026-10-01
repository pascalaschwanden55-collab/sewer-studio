async (page) => {
  const errors=[];
  page.on('pageerror',e=>errors.push(e.message));
  await page.setViewportSize({width:1440,height:1000});
  await page.goto('http://127.0.0.1:8804/bericht/ueberblick.html');
  await page.screenshot({path:'output/playwright/nova-bericht-desktop.png'});
  const base=await page.evaluate(()=>({title:document.title,charset:document.characterSet,mode:document.compatMode,lang:document.documentElement.lang,cards:document.querySelectorAll('.finding').length,hasOverflow:document.documentElement.scrollWidth>innerWidth,promptLength:document.querySelector('#prompt-text').value.length,localLinks:[...document.querySelectorAll('a[href]')].map(e=>e.getAttribute('href')).filter(h=>!h.startsWith('https:')&&!h.startsWith('#'))}));
  await page.getByRole('button',{name:'Zuerst beheben · 5',exact:true}).click();
  const firstCount=await page.locator('.finding:visible').count();
  await page.getByRole('button',{name:'Danach verbessern · 5',exact:true}).click();
  const secondCount=await page.locator('.finding:visible').count();
  await page.getByRole('button',{name:'Alle 10 Punkte',exact:true}).click();
  await page.getByRole('button',{name:'Prompt kopieren',exact:true}).click();
  await page.waitForTimeout(250);
  const copyStatus=await page.locator('#copy-status').innerText();
  await page.setViewportSize({width:390,height:844});
  await page.evaluate(()=>scrollTo(0,0));
  await page.screenshot({path:'output/playwright/nova-bericht-schmal.png'});
  const narrow=await page.evaluate(()=>({viewport:innerWidth,documentWidth:document.documentElement.scrollWidth}));
  return {base,firstCount,secondCount,copyStatus,narrow,errors};
}
