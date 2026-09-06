async (page) => {
  await page.evaluate(()=>localStorage.clear());await page.reload();await page.locator('#moOff').click();
  await page.locator('#btnNext').click();
  const result={vorher:await page.evaluate(()=>window.__nova.DATA.haltungen[0].findings.length)};
  await page.locator('#plNewEvent').click();await page.locator('#vsaNote').fill('Nachprüfung: Dieser Befund soll verworfen werden.');await page.locator('#vsaApply').click();
  result.nachCodierung=await page.evaluate(()=>window.__nova.DATA.haltungen[0].findings.length);
  await page.getByRole('button',{name:'Beenden ohne Übernahme',exact:true}).click();
  result.nachBeenden=await page.evaluate(()=>({playerOffen:document.querySelector('#winPlayer').classList.contains('show'),befunde:window.__nova.DATA.haltungen[0].findings.length,neuerBefundVorhanden:window.__nova.DATA.haltungen[0].findings.some(f=>f.text==='Nachprüfung: Dieser Befund soll verworfen werden.')}));
  await page.locator('#btnPlayerH').click();
  result.beimWiederoeffnenImPlayer= (await page.locator('#plSide').innerText()).includes('Nachprüfung: Dieser Befund soll verworfen werden.');
  return result;
}
