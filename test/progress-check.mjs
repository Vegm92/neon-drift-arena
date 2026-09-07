import * as P from "../build/Core/Progress.js";

let fails = 0;
const check = (name, cond) => { console.log((cond ? "ok  " : "FAIL ") + name); if (!cond) fails++; };
const guard = (name, fn) => {
  const t = Date.now();
  try { const r = fn(); check(`${name} (${Date.now() - t}ms)`, true); return r; }
  catch (e) { check(`${name} threw: ${e}`, false); }
};

const dates = Array.from({ length: 60 }, (_, d) => `2026-${String(1 + (d % 12)).padStart(2, "0")}-${String(1 + (d % 28)).padStart(2, "0")}`);
const sets = guard("daily() returns for 60 dates without spinning", () => dates.map((d) => [...P.daily(d)]));

check("every day yields exactly 3 tasks", sets.every((s) => s.length === 3));
check("every id is a real pool entry", sets.every((s) => s.every((i) => Number.isInteger(i) && i >= 0 && i < P.pool.length)));
check("the 3 tasks of a day are distinct in kind", sets.every((s) => new Set(s.map((i) => String(P.task(i)))).size === 3));
check("the same date is stable across calls", dates.every((d, k) => String(P.daily(d)) === String(sets[k])));

const distinct = new Set(sets.map((s) => s.join(","))).size;
check(`the set rotates across dates (${distinct} distinct in 60 days)`, distinct >= 6);

check("level math survives the JS number model", P.level(0) === 1 && P.level(50) === 2 && P.level(200) === 3 && P.toNext(50) === 150);
check("a daily round-trips through encode/decode in JS", (() => {
  const [d, ids, prog] = P.decode("2026-09-07", P.encode("2026-09-07", P.daily("2026-09-07"), [1, 2, 3]));
  return d === "2026-09-07" && String(ids) === String(P.daily("2026-09-07")) && String(prog) === "1,2,3";
})());
check("a corrupt record falls back instead of throwing", String(P.decode("2026-09-07", "garbage")[2]) === "0,0,0");

console.log(fails ? `\n${fails} failed` : "\nall good");
process.exit(fails ? 1 : 0);
