import * as D from "../build/Core/Determinism.js";

let fails = 0;
const check = (name, cond) => { console.log((cond ? "ok  " : "FAIL ") + name); if (!cond) fails++; };

const hex = (n) => "0x" + n.toString(16).toUpperCase().padStart(8, "0");

const w = D.goldenRun();
const actual = D.worldHash(w);
const moved = w.Ships.filter((s) => Math.hypot(s.Pos.X, s.Pos.Y) > 1).length;

if (actual !== D.golden) {
  console.log(`  js sim hash=${hex(actual)} expected=${hex(D.golden)} time=${w.Time.toFixed(3)} moved=${moved}`);
}

check("the golden run is a live match in JS too", moved === 4);
check(`${D.steps} scripted steps hash to the golden value in JS`, actual === D.golden);
check("the same script replays to the same hash in JS", D.worldHash(D.goldenRun()) === D.golden);

console.log(fails === 0 ? "\nsim determinism ok" : `\n${fails} failed`);
process.exit(fails === 0 ? 0 : 1);
