import {test, expect} from '@playwright/test';
import {readFileSync} from 'node:fs';
import {parse} from 'yaml';

const resources = new URL('../../apps/CryptidCompanion/wwwroot/Resources/', import.meta.url);
const load = name => parse(readFileSync(new URL(name, resources), 'utf8'));
const tiles = load('tiles.yml');
const board = load('companion-setups.yml').find(p => p.type === 'board');
const clues = load('clues.yml');
const cells = board.tiles.flatMap(p => tiles.find(t => t.id === p.tile).cells.map(c => ({
  row: p.origin.row + (p.flipped ? 2 - c.position.row : c.position.row),
  col: p.origin.column + (p.flipped ? 5 - c.position.column : c.position.column),
  terrain: c.terrain, animal: c.animal || 'None'
})));
for (const c of cells) {c.structure = board.structures.find(s => s.position.row === c.row && s.position.column === c.col); c.label = String.fromCharCode(65+c.row)+(c.col+1)}
const distance = (a,b) => {const dq=a.col-b.col;const dr=(a.row-Math.trunc(a.col/2))-(b.row-Math.trunc(b.col/2));return Math.max(Math.abs(dq),Math.abs(dr),Math.abs(dq+dr))};
function matches(id,cell) {
  const clue=clues.find(c=>c.id===id);
  if(clue.rule==='TerrainPair')return clue.terrains.includes(cell.terrain);
  return cells.some(s=>distance(cell,s)<=clue.distance && (
    clue.rule==='NearTerrain' ? clue.terrains.includes(s.terrain) :
    clue.rule==='NearAnimal' ? s.animal!=='None' && (clue.animal==='None'||clue.animal===s.animal) :
    clue.rule==='NearStructure' ? s.structure?.kind===clue.structure : s.structure?.color===clue.color));
}
const same=(p,c)=>p.Row===c.row&&p.Column===c.col;
const cubed=(save,c)=>save.Markers.some(m=>m.Piece===0&&same(m.Position,c));
const own=(save,c)=>save.Markers.some(m=>m.Book===save.Human&&same(m.Position,c));
const saved=page=>page.evaluate(()=>JSON.parse(localStorage.getItem('cryptid-session-v1')));
async function start(page, players=3) {
  await page.goto('./');
  await expect(page.getByRole('heading',{name:/One explorer/})).toBeVisible({timeout:60000});
  await page.getByLabel('Clue-holders, including you').selectOption(String(players));
  await page.getByRole('button',{name:/Prepare the board/}).click();
  await expect(page.locator('.hex-cell')).toHaveCount(108);
  await page.getByRole('link',{name:/Board ready/}).click();
  await expect(page.getByLabel('Your color',{exact:true})).toBeVisible();
  await page.getByText('Reproducible opening cubes',{exact:true}).click();
  await expect(page.locator('select#clue')).toHaveCount(0);
  await page.getByLabel('Optional random seed').fill('42');
  await page.getByRole('button',{name:/Take my assigned clue/}).click();
  await expect(page.getByRole('heading',{name:'Mark what cannot be.'})).toBeVisible();
  for(let i=0;i<2;i++) {
    const save=await saved(page);const cell=cells.find(c=>!matches(save.Assignments[save.Human],c)&&!own(save,c));
    await page.getByLabel('Your next cube').selectOption(cell.label);
    await page.getByRole('button',{name:'Place my cube',exact:true}).click();
    await expect.poll(async()=>(await saved(page)).PendingHumanCubes).toBe(1-i);
  }
  await page.getByRole('button',{name:/All cubes placed/}).click();
  await expect(page.getByRole('heading',{name:'Follow your own hunch.'})).toBeVisible();
}
async function penalty(page) {
  const save=await saved(page);const c=cells.find(c=>!matches(save.Assignments[save.Human],c)&&!own(save,c));
  await page.getByLabel('Selected space').selectOption(c.label);
  await page.getByRole('button',{name:'Record my cube',exact:true}).click();
  await expect.poll(async()=>(await saved(page)).PendingHumanCubes).toBe(0);
}
for(const count of [3,4,5])test(`${count} clue-holders: opening, question, penalties, search, reload, and win`,async({page})=>{
  const errors=[]; page.on('pageerror',e=>errors.push(e.message));
  await start(page,count);
  let save=await saved(page);
  expect(save.Markers).toHaveLength(count*2);
  const pieceToggle = page.getByRole('checkbox',{name:'Show recorded cubes and discs'});
  await expect(page.locator('.board-piece')).toHaveCount(0);
  await pieceToggle.check();
  await expect(page.locator('.board-piece')).toHaveCount(save.Markers.length);
  await expect(page.locator('.board-piece.book-red')).toHaveCount(2);
  await expect(page.locator('.board-piece.book-green')).toHaveCount(2);
  await expect(page.locator('.board-piece.book-blue')).toHaveCount(2);
  if(count>=4)await expect(page.locator('.board-piece.book-orange')).toHaveCount(2);
  if(count===5)await expect(page.locator('.board-piece.book-black')).toHaveCount(2);
  for(const coordinate of ['A1','E6','I12']) {
    await page.getByLabel('Selected space').selectOption(coordinate);
    await expect(page.locator('.selection-overlay')).toBeVisible();
    expect(await page.locator('.board').evaluate(svg=>svg.lastElementChild.classList.contains('selection-overlay'))).toBe(true);
    expect((await page.locator('.selection-outline').getAttribute('points')).split(' ')).toHaveLength(6);
  }
  await page.getByRole('button',{name:/^E6,/}).hover();
  await expect(page.locator('.interaction-overlay')).toBeVisible();
  await pieceToggle.uncheck();
  await expect(page.locator('.board-piece')).toHaveCount(0);
  for(const [name,expected] of [
    ['Highlight Forest terrain',cells.filter(c=>c.terrain==='Forest').map(c=>c.label)],
    ['Highlight Bear territory',cells.filter(c=>c.animal==='Bear').map(c=>c.label)],
    ['Highlight Blue structures',cells.filter(c=>c.structure?.color==='Blue').map(c=>c.label)],
    ['Highlight Alpha pieces',save.Markers.filter(m=>m.Book==='Alpha').map(m=>String.fromCharCode(65+m.Position.Row)+(m.Position.Column+1))]
  ]) {
    await page.getByRole('button',{name,exact:true}).hover();
    await expect.poll(()=>page.locator('.hex-cell.legend-match').evaluateAll(nodes=>nodes.map(n=>n.getAttribute('aria-label').split(',')[0]).sort())).toEqual([...new Set(expected)].sort());
  }
  await expect(page.getByRole('button',{name:'Highlight Alpha pieces'})).toHaveText('Alpha');
  await page.getByRole('button',{name:'Highlight Alpha pieces'}).click();
  await page.getByRole('heading',{name:'Follow your own hunch.'}).hover();
  await expect(page.getByRole('button',{name:'Highlight Alpha pieces'})).toHaveAttribute('aria-pressed','true');
  await page.getByRole('button',{name:'Clear highlighting'}).click();
  await expect(page.locator('.legend-match')).toHaveCount(0);
  await page.getByRole('button',{name:'Highlight Beta pieces'}).focus();
  await expect(page.locator('.legend-match')).toHaveCount(2);
  await page.getByRole('button',{name:'Clear highlighting'}).click();
  await expect(page.locator('.animal-bear').first()).toHaveCSS('stroke-dasharray','none');
  await expect(page.locator('.animal-cougar').first()).not.toHaveCSS('stroke-dasharray','none');
  const opponents=Object.keys(save.Assignments).filter(b=>b!==save.Human);
  const target=opponents[0];
  const negative=cells.find(c=>!cubed(save,c)&&!matches(save.Assignments[target],c));
  expect(negative).toBeDefined();
  await page.getByLabel('Ask a clue-holder').selectOption(target);
  await page.getByLabel('Selected space').selectOption(negative.label);
  await page.getByRole('button',{name:`Ask about ${negative.label}`,exact:true}).click();
  await expect(page.getByText('You owe a cube.',{exact:true})).toBeVisible();
  await expect.poll(async()=>(await saved(page)).History.length).toBe(1);
  await expect(page.getByRole('status',{name:'Latest answer'})).toContainText('NO — place a cube');
  await expect(page.getByRole('status',{name:'Latest answer'})).toContainText(negative.label);
  await penalty(page);
  save=await saved(page);
  const failure=cells.find(c=>!cubed(save,c)&&matches(save.Assignments[save.Human],c)&&opponents.some(b=>!matches(save.Assignments[b],c)));
  expect(failure).toBeDefined();
  await page.getByLabel('Selected space').selectOption(failure.label);
  await page.getByRole('button',{name:`Search ${failure.label}`,exact:true}).click();
  await expect(page.getByText('You owe a cube.',{exact:true})).toBeVisible();
  await penalty(page);
  save=await saved(page);
  for(const book of opponents)await expect(page.locator('main')).not.toContainText(clues.find(c=>c.id===save.Assignments[book]).text);
  await page.reload();
  await expect(page.getByRole('heading',{name:'Follow your own hunch.'})).toBeVisible({timeout:60000});
  expect(await saved(page)).toEqual(save);
  const solution=cells.filter(c=>Object.values(save.Assignments).every(id=>matches(id,c)));
  expect(solution).toHaveLength(1);
  await page.getByLabel('Selected space').selectOption(solution[0].label);
  await page.getByRole('button',{name:`Search ${solution[0].label}`,exact:true}).click();
  await expect(page.getByRole('heading',{name:'You found the habitat.'})).toBeVisible();
  await expect(page.getByText('The other clues remain hidden.')).toBeVisible();
  await pieceToggle.check();
  const habitat = page.getByRole('button',{name:new RegExp('^'+solution[0].label+',')});
  await expect(habitat.locator('.board-piece circle')).toHaveCount(count);
  const cubeWidth=Number(await page.locator('.board-piece rect').first().getAttribute('width'));
  const discDiameter=2*Number(await habitat.locator('.board-piece circle').first().getAttribute('r'));
  expect(discDiameter).toBeGreaterThan(cubeWidth*1.5);
  expect(await habitat.evaluate(cell=>{
    const label=cell.querySelector('.coordinate').getBBox();
    return [...cell.querySelectorAll('.board-piece')].every(piece=>{const b=piece.getBBox();return b.y+b.height<label.y;});
  })).toBe(true);
  expect(errors).toEqual([]);
});

test('phone layout, invalid YAML, and a corrupted saved game fail usefully',async({page})=>{
  await page.setViewportSize({width:390,height:844});
  await page.goto('./');
  await expect(page.getByRole('heading',{name:/One explorer/})).toBeVisible({timeout:60000});
  expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBe(true);
  await page.getByText('Content packs & printed cards',{exact:true}).click();
  await page.getByLabel('Import YAML content pack').setInputFiles({name:'invalid.yml',mimeType:'text/yaml',buffer:Buffer.from('- type: clue\n  id: invalid\n  rule: NearAnimal\n')});
  await expect(page.locator('pre')).toContainText('invalid.yml');
  await page.evaluate(()=>localStorage.setItem('cryptid-session-v1','{broken'));
  await page.reload();
  await expect(page.getByText(/Saved game\/content could not be restored/)).toBeVisible({timeout:60000});
  await expect(page.getByRole('button',{name:/Prepare the board/})).toBeVisible();
});

test('published PWA resumes a game with the network offline',async({page,context})=>{
  test.skip(process.env.CRYPTID_PUBLISHED!=='1','Run against published static assets with CRYPTID_PUBLISHED=1.');
  await start(page);
  const save=await saved(page);
  await page.evaluate(async()=>{await navigator.serviceWorker.ready;if(!navigator.serviceWorker.controller)await new Promise(resolve=>navigator.serviceWorker.addEventListener('controllerchange',resolve,{once:true}));});
  await context.setOffline(true);
  await page.reload();
  await expect(page.getByRole('heading',{name:'Follow your own hunch.'})).toBeVisible({timeout:60000});
  expect(await saved(page)).toEqual(save);
  await context.setOffline(false);
});


test('display settings persist across pages and reloads',async({page})=>{
  await start(page,5);
  const before=await saved(page);
  await page.getByRole('link',{name:'Settings',exact:true}).click();
  await page.getByRole('checkbox',{name:'Show coordinate labels'}).uncheck();
  await page.getByRole('checkbox',{name:'Show recorded cubes and discs'}).check();
  await page.getByRole('link',{name:'Return to game',exact:true}).click();
  await expect(page.locator('.coordinate')).toHaveCount(0);
  await expect(page.locator('.board-piece')).toHaveCount(10);
  await page.reload();
  await expect(page.getByRole('heading',{name:'Follow your own hunch.'})).toBeVisible({timeout:60000});
  await expect(page.locator('.coordinate')).toHaveCount(0);
  await expect(page.locator('.board-piece')).toHaveCount(10);
  expect(await saved(page)).toEqual(before);
});

test('completed games move to review history without disturbing a new expedition',async({page})=>{
  await start(page);
  const save=await saved(page);
  const solution=cells.find(c=>Object.values(save.Assignments).every(id=>matches(id,c)));
  await page.getByLabel('Selected space').selectOption(solution.label);
  await page.getByRole('button',{name:`Search ${solution.label}`,exact:true}).click();
  await expect(page.getByRole('heading',{name:'You found the habitat.'})).toBeVisible();
  // Migrate a winning save from the previous release, which had no session ID or archive.
  await page.evaluate(()=>{
    const legacy=JSON.parse(localStorage.getItem('cryptid-session-v1'));
    delete legacy.SessionId;
    localStorage.setItem('cryptid-session-v1',JSON.stringify(legacy));
    localStorage.removeItem('cryptid-history-v1');
  });
  await page.goto('./');
  await expect(page.getByRole('heading',{name:'Past games',exact:true})).toBeVisible({timeout:60000});
  await expect(page.getByRole('link',{name:/Resume expedition/})).toHaveCount(0);
  const review=await page.getByRole('link',{name:'Review',exact:true}).getAttribute('href');
  await page.reload();
  await expect(page.getByRole('link',{name:'Review',exact:true})).toHaveCount(1,{timeout:60000});
  await start(page);
  const active=await saved(page);
  expect(active.SessionId).not.toBe(save.SessionId);
  await page.goto(review);
  await expect(page.getByRole('heading',{name:'Back through the field notes.'})).toBeVisible({timeout:60000});
  await expect(page.getByRole('button',{name:/^Search /})).toHaveCount(0);
  expect(await saved(page)).toEqual(active);
  await page.getByRole('link',{name:'Back to camp',exact:true}).click();
  await expect(page.getByRole('link',{name:/Resume expedition/})).toBeVisible();
  await expect(page.getByRole('link',{name:'Review',exact:true})).toHaveCount(1);
});

test('GitHub Pages direct routes boot from the 404 shell under the repository path',async({page})=>{
  test.skip(process.env.CRYPTID_PAGES!=='1','Run against a prepared Pages artifact.');
  const response=await page.goto('settings');
  expect(response.status()).toBe(404);
  await expect(page.getByRole('heading',{name:'Display settings',exact:true})).toBeVisible({timeout:60000});
  const base=new URL(process.env.CRYPTID_BASE_URL);
  await expect(page.locator('base')).toHaveAttribute('href',base.pathname);
  const manifest=await page.request.get('manifest.webmanifest');
  expect(manifest.ok()).toBe(true);
  const data=await manifest.json();
  expect(new URL(data.start_url,manifest.url()).pathname).toBe(base.pathname);
});
