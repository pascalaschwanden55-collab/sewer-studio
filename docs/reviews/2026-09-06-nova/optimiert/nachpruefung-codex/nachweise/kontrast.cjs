async (page) => {
  await page.evaluate(()=>localStorage.clear());await page.reload();await page.setViewportSize({width:1440,height:900});await page.locator('#moOff').click();
  const results=[];
  const scan=()=>page.evaluate(()=>{
    const parse=c=>{const v=c.match(/[\d.]+/g)?.map(Number)||[0,0,0,0];return [...v.slice(0,3),v[3]??1];};
    const compose=(a,b)=>[0,1,2].map(i=>a[i]*a[3]+b[i]*(1-a[3])).concat(1);
    const lum=c=>c.slice(0,3).map(x=>{x/=255;return x<=0.04045?x/12.92:((x+.055)/1.055)**2.4;}).reduce((s,x,i)=>s+x*[.2126,.7152,.0722][i],0);
    const ratio=(a,b)=>(Math.max(lum(a),lum(b))+.05)/(Math.min(lum(a),lum(b))+.05);
    let count=0,minSize=99;const lows=[],uncertain=[];
    for(const el of document.querySelectorAll('body *')){
      if(['SCRIPT','STYLE','OPTION'].includes(el.tagName))continue;
      const isInput=el.matches('input:not([type=checkbox]):not([type=radio]),textarea');
      const own=[...el.childNodes].filter(n=>n.nodeType===3).map(n=>n.textContent.trim()).join('');
      const text=isInput?(el.value||el.getAttribute('placeholder')||''):own;if(!text)continue;
      const r=el.getBoundingClientRect();if(!r.width||!r.height||r.bottom<=0||r.top>=innerHeight||r.right<=0||r.left>=innerWidth)continue;
      let visible=true,disabled=el.matches(':disabled'),opacity=1;
      for(let p=el;p;p=p.parentElement){const s=getComputedStyle(p);if(s.display==='none'||s.visibility==='hidden')visible=false;opacity*=Number(s.opacity);}
      if(!visible||!opacity)continue;
      const hit=document.elementFromPoint(Math.min(innerWidth-1,Math.max(0,r.left+r.width/2)),Math.min(innerHeight-1,Math.max(0,r.top+r.height/2)));
      if(!hit||!(el.contains(hit)||hit===el))continue;
      const style=getComputedStyle(el);const fs=parseFloat(style.fontSize);minSize=Math.min(minSize,fs);
      if(disabled)continue;
      const stack=[];let complex=false;
      for(let p=el;p;p=p.parentElement){const s=getComputedStyle(p);if(s.backgroundImage!=='none')complex=true;const bg=parse(s.backgroundColor);stack.push(bg);if(bg[3]===1)break;}
      let bg=[255,255,255,1];while(stack.length)bg=compose(stack.pop(),bg);
      let fg=parse(el instanceof SVGElement?style.fill:style.color);
      if(isInput&&!el.value)fg=parse(getComputedStyle(el,'::placeholder').color);
      fg[3]*=opacity;fg=compose(fg,bg);const q=ratio(fg,bg);count++;
      const detail={id:el.id,tag:el.tagName,cls:typeof el.className==='string'?el.className:'SVG',text:text.slice(0,80),ratio:+q.toFixed(3),font:fs,foreground:fg,background:bg};
      if(q<4.5){if(complex)uncertain.push(detail);else lows.push(detail);}
    }
    return {count,minSize,lows,uncertain};
  });
  for(const theme of ['Light','Dark']){
    await page.locator('#th'+theme).click();
    for(const name of ['overview','projekt','haltungen','schaechte','import','export','medien','druck','dossiers','matrix','smatrix','schatten','vsa','diagnose','einstellungen']){
      await page.locator('.nav[data-nav='+name+']').click();results.push({theme,page:name,...await scan()});
    }
    await page.locator('.nav[data-nav=haltungen]').click();await page.locator('#btnPlayerH').click();results.push({theme,page:'player',...await scan()});
    await page.locator('#plNewEvent').click();results.push({theme,page:'codierung',...await scan()});await page.keyboard.press('Escape');await page.keyboard.press('Escape');
    await page.locator('[data-open=winStudio]').click();results.push({theme,page:'training',...await scan()});
    await page.screenshot({path:'output/playwright/nova-opt-training-'+theme.toLowerCase()+'.png'});await page.keyboard.press('Escape');
  }
  return results;
}
