import fs from 'fs';
import {decodeBinarySchema, compileSchema} from 'kiwi-schema';
import * as pako from 'pako';
import {decompress} from 'fzstd';
const buf = fs.readFileSync(process.argv[2]);
let off = 12; const chunks=[];
while (off < buf.length) { const len = buf.readUInt32LE(off); off+=4; chunks.push(buf.subarray(off, off+len)); off+=len; }
const un = c => (c[0]===0x28&&c[1]===0xb5) ? decompress(c) : pako.inflateRaw(c);
const schema = decodeBinarySchema(un(chunks[0]));
const s = compileSchema(schema);
const msg = s.decodeMessage(un(chunks[1]));
fs.writeFileSync(process.argv[3], JSON.stringify(msg, (k,v)=> (v instanceof Uint8Array)? {bytes:Buffer.from(v).toString('hex')} : v, 1));
console.log(chunks.length, msg.nodeChanges.length);
