import { preview } from "vite";
import { chromium } from "playwright-core";
import { readdir, stat } from "node:fs/promises";
import { existsSync } from "node:fs";

const dir = "dist-game";
const caps = { initialMB: 50, mobileMB: 20, totalMB: 250, files: 1500, startS: 20 };

const names = (await readdir(dir, { recursive: true, withFileTypes: true })).filter((e) => e.isFile());
const sizes = await Promise.all(names.map((e) => stat(`${e.parentPath}/${e.name}`).then((s) => s.size)));
const totalMB = sizes.reduce((a, b) => a + b, 0) / 1e6;

const chrome = process.env.QA_CHROME ?? `${process.env.LOCALAPPDATA}/ms-playwright/chromium-1228/chrome-win64/chrome.exe`;
const server = await preview({ build: { outDir: dir }, preview: { port: 4175 } });
const browser = await chromium.launch({
  headless: true,
  ...(existsSync(chrome) ? { executablePath: chrome } : { channel: "chrome" }),
  args: ["--enable-gpu", "--ignore-gpu-blocklist", "--use-gl=angle", "--use-angle=d3d11", "--autoplay-policy=no-user-gesture-required"],
});
const ctx = await browser.newContext();
await ctx.route("**/crazygames-sdk-v3.js", (r) => r.fulfill({ body: "", contentType: "application/javascript" }));
await ctx.addInitScript(() => {
  const noop = () => {};
  window.__cg = { start: null };
  window.CrazyGames = { SDK: {
    init: async () => {},
    game: { gameplayStart: () => (window.__cg.start ??= performance.now()), gameplayStop: noop, addSettingsChangeListener: noop, settings: {}, getInviteParam: () => null, updateRoom: noop, addJoinRoomListener: noop, leftRoom: noop },
    user: { isUserAccountAvailable: false },
    ad: { requestAd: (t, c) => c.adError("no-ad") },
    banner: { requestBanner: noop, clearBanner: noop },
  } };
});

const page = await ctx.newPage();
await page.goto("http://localhost:4175/", { waitUntil: "commit" });
await page.waitForSelector("#loader.done", { state: "attached", timeout: 60000 });
const loaded = await page.evaluate(() => performance.now());
const initialMB = await page.evaluate(() => performance.getEntriesByType("resource").reduce((a, e) => a + (e.transferSize || e.encodedBodySize || 0), 0) / 1e6);

const key = async (k) => { await page.keyboard.down(k); await new Promise((r) => setTimeout(r, 170)); await page.keyboard.up(k); await new Promise((r) => setTimeout(r, 400)); };
for (const k of ["Space", "Space", "Enter"]) await key(k);
await page.waitForFunction(() => window.__cg.start !== null, null, { timeout: 60000 });
const start = await page.evaluate(() => window.__cg.start);

await browser.close();
await server.close();

const ok = (v, cap) => (v <= cap ? "OK" : "OVER");
const rows = [
  [`${dir} files`, names.length, caps.files],
  [`${dir} total MB`, +totalMB.toFixed(2), caps.totalMB],
  ["initial download MB (to playable menu)", +initialMB.toFixed(2), caps.initialMB],
  ["menu interactive s", +(loaded / 1000).toFixed(2), caps.startS],
  ["s to gameplayStart", +(start / 1000).toFixed(2), caps.startS],
];
for (const [label, v, cap] of rows) console.log(`${ok(v, cap).padEnd(4)} ${label}: ${v} (cap ${cap})`);
console.log(`${ok(+initialMB.toFixed(2), caps.mobileMB).padEnd(4)} initial download MB vs mobile-homepage bonus cap: ${initialMB.toFixed(2)} (cap ${caps.mobileMB}, not required)`);
if (rows.some(([, v, cap]) => v > cap)) process.exitCode = 1;
