import { readFile, readdir, access } from "node:fs/promises";

const at = (p) => new URL(`../dist-game/${p}`, import.meta.url);
const gone = async (p) => access(at(p)).then(() => false, () => true);

const fail = [];
const ok = (cond, what) => (cond ? console.log("ok  " + what) : fail.push(what));

const index = await readFile(at("index.html"), "utf8");
const bundles = await Promise.all(
  (await readdir(at("assets"))).filter((f) => f.endsWith(".js")).map((f) => readFile(at(`assets/${f}`), "utf8")),
);
const js = bundles.join("");

ok(index.includes("window.NDA_CG=true"), "the package stamps the lean flag");
ok(await gone("pad.html"), "the phone pad is not in the package");
ok(await gone("play"), "the game sits at the package root");
ok(!(await gone("privacy.html")), "the privacy page ships");
ok(js.includes("./privacy.html") && !js.includes("../privacy.html"), "the privacy link stays inside the root");
ok(!/(src|href)="\//.test(index) && !/url\(\//.test(index), "every path in the page is relative");

if (fail.length) {
  for (const f of fail) console.error("FAIL " + f);
  process.exit(1);
}
console.log("\npackage ok");
