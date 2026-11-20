import fs from 'fs'; import {Resvg} from '@resvg/resvg-js';
const [dir,out]=process.argv.slice(2);
for (const f of fs.readdirSync(dir)) { if(!f.endsWith('.svg')) continue;
  const r=new Resvg(fs.readFileSync(`${dir}/${f}`,'utf8'),{fitTo:{mode:'original'}, font:{loadSystemFonts:false}});
  fs.writeFileSync(`${out}/${f.replace('.svg','.png')}`, r.render().asPng()); }
