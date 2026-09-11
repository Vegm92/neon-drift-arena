import { execSync } from "node:child_process";
import { readFileSync, writeFileSync } from "node:fs";

const hex = (n) => "0x" + n.toString(16).toUpperCase().padStart(8, "0");
const sh = (cmd) => execSync(cmd, { encoding: "utf8" }).trim();

const accept = process.argv.includes("--accept");
const file = "src/Core/Determinism.js";

execSync("dotnet fable src -o build", { stdio: ["ignore", "ignore", "inherit"] });
const D = await import(`../build/Core/${file.split("/").pop()}`);

const actual = D.worldHash(D.goldenRun());

if (actual === D.golden && !accept) process.exit(0);

if (accept) {
  const src = "src/Core/Determinism.fs";
  const patched = readFileSync(src, "utf8").replace(/let golden = 0x[0-9A-Fa-f]{8}L/, `let golden = ${hex(actual)}L`);
  writeFileSync(src, patched);
  execSync(`git add ${src}`);
  console.log(`golden sim hash updated to ${hex(actual)}`);
  process.exit(0);
}

const tweaks = sh("git diff --cached -U0 -- src/Core/Domain.fs")
  .split("\n")
  .filter((l) => /^[-+]\s*let mutable/.test(l));

console.log("\n  The simulation changed.\n");
if (tweaks.length) {
  console.log("  Tuning values in this commit:");
  for (const t of tweaks) console.log("    " + t.trim());
  console.log("");
}
console.log(`  golden hash ${hex(D.golden)} -> ${hex(actual)}\n`);
process.exit(2);
