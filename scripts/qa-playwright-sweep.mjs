import { chromium, devices } from "playwright-core";
import { existsSync, mkdirSync, rmSync, writeFileSync } from "node:fs";
import { inflateSync } from "node:zlib";

const base = process.env.QA_BASE_URL ?? "http://localhost:4173";
const out = "qa/screenshots";
const chrome = process.env.QA_CHROME ?? `${process.env.LOCALAPPDATA}/ms-playwright/chromium-1228/chrome-win64/chrome.exe`;
rmSync(out, { recursive: true, force: true });
mkdirSync(out, { recursive: true });

const notes = [];
const shots = [];
const note = (screen, text) => notes.push({ screen, text });
const wait = (ms) => new Promise((r) => setTimeout(r, ms));

const pngStats = (buf) => {
  let p = 8, w = 0, h = 0, ct = 0;
  const idat = [];
  while (p < buf.length) {
    const len = buf.readUInt32BE(p), type = buf.toString("ascii", p + 4, p + 8);
    if (type === "IHDR") { w = buf.readUInt32BE(p + 8); h = buf.readUInt32BE(p + 12); ct = buf[p + 17]; }
    if (type === "IDAT") idat.push(buf.subarray(p + 8, p + 8 + len));
    p += 12 + len;
  }
  const bpp = ct === 6 ? 4 : ct === 2 ? 3 : 1;
  const raw = inflateSync(Buffer.concat(idat)), stride = w * bpp, px = Buffer.alloc(h * stride);
  for (let y = 0; y < h; y++) {
    const f = raw[y * (stride + 1)], s = y * (stride + 1) + 1, o = y * stride;
    for (let x = 0; x < stride; x++) {
      const a = x >= bpp ? px[o + x - bpp] : 0, b = y > 0 ? px[o - stride + x] : 0, c = x >= bpp && y > 0 ? px[o - stride + x - bpp] : 0;
      let v = raw[s + x];
      if (f === 1) v += a; else if (f === 2) v += b; else if (f === 3) v += (a + b) >> 1;
      else if (f === 4) { const q = a + b - c, pa = Math.abs(q - a), pb = Math.abs(q - b), pc = Math.abs(q - c); v += pa <= pb && pa <= pc ? a : pb <= pc ? b : c; }
      px[o + x] = v & 255;
    }
  }
  const luma = [];
  for (let y = 0; y < h; y += 4) for (let x = 0; x < w; x += 4) { const i = y * stride + x * bpp; luma.push((px[i] * 54 + px[i + 1] * 183 + px[i + 2] * 19) >> 8); }
  const mean = luma.reduce((a, b) => a + b, 0) / luma.length;
  const bright = luma.filter((v) => v > 180).length / luma.length;
  return { mean: +mean.toFixed(1), bright: +bright.toFixed(3), luma };
};

const flashDelta = (a, b) => {
  let n = 0;
  for (let i = 0; i < a.luma.length; i++) if (Math.abs(a.luma[i] - b.luma[i]) > 25) n++;
  return +(n / a.luma.length).toFixed(3);
};

const browser = await chromium.launch({
  headless: true,
  ...(existsSync(chrome) ? { executablePath: chrome } : { channel: "chrome" }),
  args: ["--enable-gpu", "--ignore-gpu-blocklist", "--use-gl=angle", "--use-angle=d3d11", "--autoplay-policy=no-user-gesture-required"],
});

const errors = [];
const watchConsole = (page, tag) => {
  page.on("console", (m) => { if (m.type() === "error" || m.type() === "warning") errors.push(`[${tag}] ${m.type()}: ${m.text().slice(0, 200)}`); });
  page.on("pageerror", (e) => errors.push(`[${tag}] pageerror: ${String(e).slice(0, 200)}`));
};

let n = 0;
const shot = async (page, name, opts = {}) => {
  const file = `${String(++n).padStart(2, "0")}-${name}.jpg`;
  await page.screenshot({ path: `${out}/${file}`, type: "jpeg", quality: 82, ...opts });
  shots.push(file);
  return file;
};
const shotPng = (page) => page.screenshot({ type: "png" });

const domCheck = (page, screen, sel = "#menu") =>
  page.evaluate(({ sel }) => {
    const root = document.querySelector(sel), issues = [];
    if (!root) return ["missing " + sel];
    if (/\{\{\w+\}\}/.test(document.body.innerText)) issues.push("raw {{key}} placeholder visible");
    for (const el of root.querySelectorAll("*")) {
      if (el.children.length === 0 && el.textContent.trim() && el.scrollWidth > el.clientWidth + 2 && getComputedStyle(el).overflow !== "visible")
        issues.push(`clipped text <${el.tagName.toLowerCase()}.${[...el.classList].join(".")}> "${el.textContent.trim().slice(0, 40)}"`);
    }
    const r = root.getBoundingClientRect();
    if (r.bottom > innerHeight + 1 || r.right > innerWidth + 1) issues.push(`${sel} exceeds viewport ${Math.round(r.right)}x${Math.round(r.bottom)} vs ${innerWidth}x${innerHeight}`);
    const lobby = root.querySelector(".lobby");
    if (lobby && lobby.scrollHeight > lobby.clientHeight + 1) issues.push(`.lobby overflows its box ${lobby.scrollHeight} > ${lobby.clientHeight}`);
    const small = [...root.querySelectorAll("*")].filter((el) => el.children.length === 0 && el.textContent.trim()).map((el) => parseFloat(getComputedStyle(el).fontSize));
    if (small.length) issues.push(`smallest text ${Math.min(...small).toFixed(1)}px at ${innerWidth}x${innerHeight}`);
    return issues;
  }, { sel }).then((r) => r.forEach((t) => note(screen, t)));

const gameBoot = async (page) => {
  await page.waitForSelector("#loader.done", { state: "attached", timeout: 40000 });
  await page.evaluate(async () => {
    const url = performance.getEntriesByType("resource").map((e) => e.name).find((u) => /\/build\/Main\.js/.test(u)) ?? "/build/Main.js";
    const at = (f) => url.replace(/Main\.js.*$/, f);
    const [M, Menu, Sim, D] = await Promise.all([import(url), import(at("UI/Menu.js")), import(at("Core/Sim.js")), import(at("Core/Domain.js"))]);
    const q = {
      M, Menu, Sim, D, raf: window.requestAnimationFrame.bind(window), t: performance.now(), frozen: false,
      clone: (o, f) => Object.assign(Object.create(Object.getPrototypeOf(o)), o, f),
      freeze() { if (q.frozen) return; q.frozen = true; window.requestAnimationFrame = () => 0; q.t = performance.now(); },
      step(k = 1, ms = 16) { for (let i = 0; i < k; i++) M.frame((q.t += ms)); },
      thaw() { if (!q.frozen) return; q.frozen = false; window.requestAnimationFrame = q.raf; M.frame(performance.now()); },
      world(f) { M.world(q.clone(M.world(), f)); },
      ship(i, f) { const w = M.world(); q.world({ Ships: w.Ships.map((s) => (s.Id === i ? q.clone(s, f) : s)) }); },
      ships(f) { const w = M.world(); q.world({ Ships: w.Ships.map((s) => q.clone(s, f(s))) }); },
      cons(list, head) { return Object.assign(Object.create(Object.getPrototypeOf(list)), { head, tail: list }); },
      arr(l) { const a = []; while (l && l.tail != null) { a.push(l.head); l = l.tail; } return a; },
      cam(x, y, h) { const c = M.view.Camera; c.position.set(x, h, y + h * 0.3); c.lookAt(x, 0, y); M.view.Composer.render(); },
      tags(on) { document.querySelectorAll("#hud .tag").forEach((t) => (t.style.visibility = on ? "" : "hidden")); },
      pos(i) { const s = M.world().Ships[i]; return { x: s.Pos.X, y: s.Pos.Y }; },
      watch(cond, timeout) {
        return new Promise((res) => {
          const t0 = performance.now();
          const tick = () => { if (cond(M.world())) { q.freeze(); res(true); } else if (performance.now() - t0 > timeout) res(false); else setTimeout(tick, 4); };
          tick();
        });
      },
    };
    window.__qa = q;
  });
};

const key = async (page, code, hold = 170) => { await page.keyboard.down(code); await wait(hold); await page.keyboard.up(code); await wait(90); };
const q = (page, fn, arg) => page.evaluate(fn, arg);
const freeze = (page) => q(page, () => __qa.freeze());
const thaw = (page) => q(page, () => __qa.thaw());
const step = (page, k, ms = 16) => q(page, ([k, ms]) => __qa.step(k, ms), [k, ms]);
const closeup = async (page, name, i, h = 520) => {
  await q(page, ([i, h]) => { const p = __qa.pos(i); __qa.tags(false); __qa.cam(p.x, p.y, h); }, [i, h]);
  const f = await shot(page, name);
  await q(page, () => __qa.tags(true));
  return f;
};
const closeupAt = async (page, name, x, y, h = 700) => {
  await q(page, ([x, y, h]) => { __qa.tags(false); __qa.cam(x, y, h); }, [x, y, h]);
  const f = await shot(page, name);
  await q(page, () => __qa.tags(true));
  return f;
};

const flashSeq = async (page, name, frames = 5, ms = 16, aim = null) => {
  const seq = [];
  for (let i = 0; i < frames; i++) {
    if (i > 0) await q(page, ([ms, aim]) => { __qa.step(1, ms); if (aim) { const p = __qa.pos(aim.i === "target" ? __qa.Sim.target() : aim.i); __qa.tags(false); __qa.cam(p.x + aim.dx, p.y, aim.h); } }, [ms, aim]);
    const stats = pngStats(await shotPng(page));
    if (i < 3) await shot(page, `${name}-f${i}`);
    seq.push(stats);
  }
  const deltas = seq.slice(1).map((s, i) => flashDelta(seq[i], s));
  note("flash", `${name}: mean luma ${seq.map((s) => s.mean).join(" → ")} · bright(>180) ${seq.map((s) => s.bright).join(" → ")} · changed-area per ${ms}ms step ${deltas.join(", ")}`);
  return { seq, deltas };
};

const lobbyState = (page) => q(page, () => ({
  slots: [...document.querySelectorAll("#menu .slot")].map((s) => s.className + " | " + s.innerText.replace(/\s+/g, " ").trim()),
  row: [...document.querySelectorAll("#menu .item")].map((i) => i.className.replace("item", "").trim() + ":" + i.querySelector("b").textContent),
  sel: document.querySelector("#menu .item.sel")?.dataset.pick ?? null,
  go: !!document.querySelector("#menu .item.go"),
  away: !!document.querySelector('#menu .slot[data-slot="0"].away'),
  note: document.querySelector("#menu .note")?.textContent ?? "",
}));

const onRow = async (page) => { if (!(await lobbyState(page)).away) await key(page, "KeyS"); };
const offRow = async (page) => { if ((await lobbyState(page)).away) await key(page, "KeyW"); };
const rowTo = async (page, pick) => {
  await onRow(page);
  for (let i = 0; i < 8; i++) {
    const s = await lobbyState(page);
    if (+s.sel === pick) return;
    const cur = +s.sel, d = (pick - cur + 6) % 6;
    await key(page, d <= 3 ? "KeyD" : "KeyA");
  }
};
const modeIs = (page) => q(page, () => document.querySelector('#menu .item[data-pick="0"] b').textContent);
const setMode = async (page, want) => {
  await rowTo(page, 0);
  for (let i = 0; i < 4 && (await modeIs(page)) !== want; i++) await key(page, "Space");
};
const joinedCount = (page) => q(page, () => __qa.Menu.joined.size);
const clearBots = (page) => q(page, () => { for (const s of [...__qa.Menu.joined]) if (__qa.Menu.isBot(s)) __qa.Menu.joined.delete(s); });
const addBots = (page, k) => q(page, (k) => { for (let i = 0; i < k; i++) __qa.Menu.addTarget(); }, k);
const ensureGo = async (page, screen) => {
  await offRow(page);
  await key(page, "Space");
  let s = await lobbyState(page);
  if (!s.go) {
    note(screen, `START not lit after MODE change with bots in the lobby (note reads "${s.note}"); cards: ${s.slots.filter((x) => x.includes("BOT")).join(" / ")}`);
    const k = (await joinedCount(page)) - 1;
    await clearBots(page);
    await addBots(page, k);
    await wait(200);
    s = await lobbyState(page);
    note(screen, `after re-adding bots START lit = ${s.go}`);
  }
};
const menuPick = async (page, idx) => { for (let i = 0; i < idx; i++) await key(page, "KeyS"); await key(page, "Space"); };
const quitToLobby = async (page, viaPause) => {
  if (viaPause) { await q(page, () => __qa.Menu.pause()); await wait(300); await menuPick(page, 3); }
  else await menuPick(page, 1);
  await page.waitForFunction(() => document.querySelector("#menu .lobby .slots"), null, { timeout: 5000 });
  await wait(300);
};

const ctx = await browser.newContext({ viewport: { width: 1920, height: 1080 }, deviceScaleFactor: 1 });
const page = await ctx.newPage();
watchConsole(page, "desktop");

// 1 landing
await page.goto(base + "/", { waitUntil: "networkidle" });
await wait(1500);
await shot(page, "landing-hero");
await shot(page, "landing-full", { fullPage: true });
note("landing", JSON.stringify(await q(page, () => {
  const v = document.getElementById("trailer");
  const m = (k) => document.querySelector(`meta[property="${k}"],meta[name="${k}"]`)?.content ?? null;
  return {
    video: { paused: v.paused, muted: v.muted, readyState: v.readyState, currentTime: +v.currentTime.toFixed(1) },
    cta: document.querySelector(".hero .cta").innerText.replace(/\s+/g, " "),
    nodesktop: document.body.classList.contains("nodesktop"),
    stores: [...document.querySelectorAll(".store")].map((s) => s.className + ":" + s.innerText.replace(/\s+/g, " ")),
    og: { image: m("og:image"), title: m("og:title"), desc: m("description"), twitter: m("twitter:card") },
    placeholders: (document.body.innerHTML.match(/\{\{\w+\}\}/g) ?? []).slice(0, 5),
    h1: getComputedStyle(document.querySelector("h1")).fontFamily,
  };
})));
await domCheck(page, "landing", "body");

// 4 tutorial + lobby
await page.goto(base + "/play/", { waitUntil: "load" });
await gameBoot(page);
await wait(600);
await shot(page, "tutorial-first-run");
await domCheck(page, "tutorial", "#tut");
await key(page, "KeyX");
await wait(300);
await shot(page, "lobby-initial");
note("lobby-initial", JSON.stringify(await lobbyState(page)));
await key(page, "Space");
await wait(200);
await shot(page, "lobby-kb-ready");
await domCheck(page, "lobby");
await key(page, "KeyD");
await wait(150);
note("lobby", "◀▶ while READY: " + JSON.stringify((await lobbyState(page)).slots[0]));

// 5 full lobby via ADD BOT
await rowTo(page, 3);
await shot(page, "lobby-row-addbot");
await key(page, "Space");
await key(page, "Space");
await wait(200);
await shot(page, "lobby-full-away");
note("lobby-full", JSON.stringify(await lobbyState(page)));
await domCheck(page, "lobby-full");
await page.setViewportSize({ width: 1366, height: 768 });
await wait(300);
await shot(page, "lobby-full-1366");
await domCheck(page, "lobby-full-1366");
await page.setViewportSize({ width: 1920, height: 1080 });
await wait(200);

// 6 mode / arena / mutator cycling
for (const m of ["TEAMS", "PRACTICE", "RACE", "FREE FOR ALL"]) {
  await setMode(page, m);
  await wait(200);
  await shot(page, `lobby-mode-${m.replace(/ /g, "").toLowerCase()}`);
  note("mode-" + m, JSON.stringify(await lobbyState(page)));
}
await rowTo(page, 1);
const arenas = [];
for (let i = 0; i < 7; i++) { arenas.push(await q(page, () => document.querySelector('#menu .item[data-pick="1"] b').textContent)); await key(page, "Space"); }
note("arena-row", "ARENA cycle (FFA): " + arenas.join(" → "));
await shot(page, "lobby-row-arena");
await rowTo(page, 2);
const muts = [];
for (let i = 0; i < 4; i++) { muts.push(await q(page, () => document.querySelector('#menu .item[data-pick="2"] b').textContent)); await key(page, "Space"); }
note("mutator-row", "MUTATOR cycle: " + muts.join(" → "));
await shot(page, "lobby-row-mutator");

// 22 QR in lobby
const qr = await q(page, () => { const d = document.querySelector("#menu details.qr summary"); if (!d) return null; const r = d.getBoundingClientRect(); return { x: r.x + r.width / 2, y: r.y + r.height / 2, w: r.width, h: r.height }; });
if (qr) {
  await shot(page, "lobby-qr-closed", { clip: { x: qr.x - 120, y: qr.y - 60, width: 240, height: 120 } });
  await page.mouse.click(qr.x, qr.y);
  await wait(300);
  await shot(page, "lobby-qr-open");
  note("qr", JSON.stringify(await q(page, () => { const s = document.querySelector("#menu details.qr svg"); const r = s?.getBoundingClientRect(); return { url: document.querySelector("#menu .qr .url")?.textContent, svgPx: r && Math.round(r.width), open: !!document.querySelector("#menu details.qr[open]") }; })));
  await page.mouse.click(960, 540);
  await wait(300);
} else note("qr", "no QR rendered in the lobby (padUrl empty)");

// 7 settings
await rowTo(page, 4);
await key(page, "Space");
await wait(300);
await shot(page, "settings-top");
await domCheck(page, "settings");
note("settings", JSON.stringify(await q(page, () => ({ rows: document.querySelectorAll("#menu .row").length, hint: document.querySelector("#menu .hint")?.innerText, sel: document.querySelector("#menu .row.sel")?.innerText }))));
await key(page, "KeyS", 700);
await shot(page, "settings-arena-audio");
await key(page, "KeyS", 1800);
await shot(page, "settings-tuning-mid");
await key(page, "KeyS", 4500);
await shot(page, "settings-bottom");
note("settings-bottom", JSON.stringify(await q(page, () => [...document.querySelectorAll("#menu .row")].slice(-4).map((r) => r.innerText.replace(/\s+/g, " ")))));
await key(page, "KeyD");
await key(page, "KeyD");
note("settings-adjust", "after ▶▶ on last selected row: " + (await q(page, () => document.querySelector("#menu .row.sel")?.innerText.replace(/\s+/g, " "))));
await page.setViewportSize({ width: 1366, height: 768 });
await wait(200);
await shot(page, "settings-1366");
await domCheck(page, "settings-1366");
await page.setViewportSize({ width: 1920, height: 1080 });
await key(page, "Escape");
await wait(300);
note("settings-back", "after ESC: " + JSON.stringify(await lobbyState(page)));

// 8 launch FFA with 3 bots
await setMode(page, "FREE FOR ALL");
await ensureGo(page, "ffa-launch");
await q(page, () => { __qa.Menu.wins[0] = 1; __qa.Menu.wins[3] = 2; });
await key(page, "Enter");
await wait(400);
await shot(page, "intro-flyby-early");
await wait(2200);
await shot(page, "intro-flyby-late");
await page.waitForFunction(() => __qa.M.countdown() <= 0, null, { timeout: 12000 });
await wait(60);
await shot(page, "go-shout");
await wait(1500);
await shot(page, "hud-early");
await domCheck(page, "hud", "#hud");
note("hud", JSON.stringify(await q(page, () => ({ clock: document.getElementById("clock").textContent, panels: [...document.querySelectorAll("#hud .panel")].map((p) => p.className + " | " + p.innerText.replace(/\s+/g, " ")) }))));

// 11 danger arc (natural, else injected)
let hit = await q(page, () => __qa.watch((w) => {
  const me = w.Ships[0]; if (!me.Alive) return false;
  return __qa.arr(w.Bullets).some((b) => { if (b.Owner === 0) return false; const rx = me.Pos.X - b.Pos.X, ry = me.Pos.Y - b.Pos.Y, l = Math.hypot(b.Vel.X, b.Vel.Y) || 1, dx = b.Vel.X / l, dy = b.Vel.Y / l, along = rx * dx + ry * dy; return along > 40 && along < 500 && Math.hypot(rx - dx * along, ry - dy * along) < 50; });
}, 20000));
if (!hit) {
  note("danger-arc", "no natural bullet threat within 20 s, injecting one");
  await freeze(page);
  await q(page, () => { const w = __qa.M.world(); const me = w.Ships[0]; const b = { Owner: 3, Pos: __qa.clone(me.Pos, { X: me.Pos.X + 300, Y: me.Pos.Y }), Vel: __qa.clone(me.Vel, { X: -400, Y: 0 }), Life: 3, Kind: 0, Damage: 20 }; __qa.ship(0, { Hp: 100, Invuln: 4 }); __qa.world({ Bullets: __qa.cons(__qa.M.world().Bullets, b) }); __qa.step(2); });
}
await shot(page, "danger-arc-wide");
await closeup(page, "danger-arc-close", 0, 480);
await thaw(page);

// 9 kill feed + kill FX frames
await q(page, () => { window.__fed = false; new MutationObserver(() => { __fed = true; }).observe(document.getElementById("feed"), { childList: true }); });
const fed = await q(page, () => __qa.watch(() => window.__fed, 45000));
if (fed) {
  await flashSeq(page, "kill-fx", 6, 16);
  await step(page, 6);
  await shot(page, "kill-feed-banner");
  note("kill-feed", JSON.stringify(await q(page, () => ({ feed: document.getElementById("feed").innerText.replace(/\s+/g, " "), banner: document.getElementById("banner").className + " " + document.getElementById("banner").textContent }))));
} else note("kill-feed", "no kill within 45 s of play");
await thaw(page);

// 12 low HP smoke
await freeze(page);
await q(page, () => { __qa.ship(0, { Hp: 18, Alive: true, Invuln: 5 }); __qa.step(40); });
await shot(page, "low-hp-wide");
await closeup(page, "low-hp-close", 0, 420);
note("low-hp", JSON.stringify(await q(page, () => ({ panel0: document.querySelector("#hud .panel.p0").className, hp: __qa.M.world().Ships[0].Hp }))));

// 13 wormhole
await q(page, () => { __qa.ship(0, { Hp: 100 }); __qa.world({ PortalIn: 0.001 }); __qa.step(3); __qa.step(30); });
const portal = await q(page, () => { const h = __qa.arr(__qa.M.world().Portals)[0]; return h ? { ax: h.A.X, ay: h.A.Y, bx: h.B.X, by: h.B.Y, life: h.Life } : null; });
note("wormhole", JSON.stringify(portal));
await shot(page, "wormhole-wide");
if (portal) {
  await closeupAt(page, "wormhole-close", portal.ax, portal.ay, 600);
  await q(page, (p) => { const me = __qa.M.world().Ships[0]; __qa.ship(0, { Pos: __qa.clone(me.Pos, { X: p.ax - 40, Y: p.ay }), Vel: __qa.clone(me.Vel, { X: 250, Y: 0 }), Invuln: 5, WarpCd: 0 }); __qa.step(12); }, portal);
  const warped = await q(page, (p) => { const s = __qa.M.world().Ships[0]; return Math.hypot(s.Pos.X - p.bx, s.Pos.Y - p.by) < 200; }, portal);
  note("wormhole", "ship warped A→B: " + warped);
  await closeupAt(page, "wormhole-warp-exit", portal.bx, portal.by, 600);
}

// 14 black hole
await q(page, () => { __qa.world({ Hole: undefined, HoleIn: 0.001 }); __qa.step(3); });
const hole = await q(page, () => { const h = __qa.M.world().Hole; return h ? { x: h.Pos.X, y: h.Pos.Y } : null; });
note("black-hole", JSON.stringify(hole));
if (hole) {
  await q(page, () => __qa.step(40));
  await shot(page, "black-hole-wide");
  await q(page, (h) => { __qa.tags(false); __qa.cam(h.x, h.y, 520); }, hole);
  await shot(page, "black-hole-close");
  const seq = [];
  for (let i = 0; i < 6; i++) { await q(page, (h) => { __qa.step(1, 16); __qa.cam(h.x, h.y, 520); }, hole); seq.push(pngStats(await shotPng(page))); }
  note("flash", `black-hole horizon (close-up, 16ms steps): mean ${seq.map((s) => s.mean).join(" → ")} · bright ${seq.map((s) => s.bright).join(" → ")} · changed-area ${seq.slice(1).map((s, i) => flashDelta(seq[i], s)).join(", ")}`);
  await q(page, (h) => { const me = __qa.M.world().Ships[0]; __qa.ship(0, { Pos: __qa.clone(me.Pos, { X: h.x + 220, Y: h.y }), Vel: __qa.clone(me.Vel, { X: 0, Y: 0 }), Hp: 100, Invuln: 5 }); __qa.step(20); __qa.tags(false); __qa.cam(h.x, h.y, 520); }, hole);
  await shot(page, "black-hole-pull-arc");
  await q(page, () => __qa.tags(true));
}

// 17 pause
await thaw(page);
await q(page, () => __qa.Menu.pause());
await wait(400);
await shot(page, "pause-menu");
await domCheck(page, "pause");
note("pause", JSON.stringify(await q(page, () => ({ items: [...document.querySelectorAll("#menu .item")].map((i) => i.className + ":" + i.textContent), qr: !!document.querySelector("#menu details.qr"), hints: document.querySelector("#menu .hints")?.innerText.replace(/\s+/g, " ") }))));
const pqr = await q(page, () => { const d = document.querySelector("#menu details.qr summary"); if (!d) return null; const r = d.getBoundingClientRect(); return { x: r.x + r.width / 2, y: r.y + r.height / 2 }; });
if (pqr) { await page.mouse.click(pqr.x, pqr.y); await wait(300); await shot(page, "pause-qr-open"); await page.mouse.click(960, 540); await wait(200); }
await key(page, "Escape");
await wait(500);
await shot(page, "resume-countdown");
await page.waitForFunction(() => __qa.M.countdown() <= 0, null, { timeout: 8000 });

// 15 sudden death
await freeze(page);
await q(page, () => { __qa.world({ Time: __qa.D.Cfg_matchTime() - 0.05 }); __qa.ships((s) => ({ Hp: 100, Invuln: 6 })); __qa.step(8); });
await shot(page, "sudden-death-shout");
await q(page, () => { __qa.world({ Time: __qa.D.Cfg_matchTime() + 22 }); __qa.step(10); });
await shot(page, "sudden-death-border");
note("sudden-death", JSON.stringify(await q(page, () => ({ clock: document.getElementById("clock").className + " " + document.getElementById("clock").textContent, banner: document.getElementById("banner").textContent, bounds: __qa.Sim.bounds?.(__qa.M.world().Time) }))));

// 16 launcher
await q(page, () => { __qa.ship(0, { Alive: false, Stocks: 0, Active: true, RespawnIn: 0 }); __qa.step(60); });
await shot(page, "launcher-wide");
await closeup(page, "launcher-chevron", 0, 700);
await page.keyboard.down("Space");
await q(page, () => { __qa.step(3); });
await page.keyboard.up("Space");
await q(page, () => __qa.step(25));
note("launcher", JSON.stringify(await q(page, () => ({ rocks: __qa.arr(__qa.M.world().Rocks).length, wep: document.querySelector("#hud .panel.p0 .wep")?.innerHTML.slice(0, 80), launchCd: __qa.M.world().Ships[0].LaunchCd }))));
await closeup(page, "launcher-rock", 0, 900);
await shot(page, "launcher-rock-wide");

// 18 end match → victory cam → result
await q(page, () => { __qa.ships((s) => (s.Id === 1 || s.Id === 2 ? { Alive: false, Stocks: 0 } : {})); __qa.step(5); });
await thaw(page);
await wait(1200);
await shot(page, "victory-cam");
await page.waitForFunction(() => document.body.innerText.includes("REMATCH"), null, { timeout: 20000 });
await wait(400);
await shot(page, "result-screen");
await domCheck(page, "result");
note("result", JSON.stringify(await q(page, () => ({ title: document.querySelector("#menu h1")?.textContent, tally: document.querySelector("#menu .tally")?.innerText.replace(/\s+/g, " "), hints: document.querySelector("#menu .hints")?.innerText.replace(/\s+/g, " "), awards: [...document.querySelectorAll("#menu .award")].map((a) => a.innerText.replace(/\s+/g, " ")) }))));
await page.setViewportSize({ width: 1366, height: 768 });
await wait(300);
await shot(page, "result-screen-1366");
await domCheck(page, "result-1366");
await page.setViewportSize({ width: 1920, height: 1080 });
await quitToLobby(page, false);

// 19 race
await setMode(page, "RACE");
await rowTo(page, 1);
const tracks = [];
for (let i = 0; i < 5; i++) { tracks.push(await q(page, () => document.querySelector('#menu .item[data-pick="1"] b').textContent)); await key(page, "Space"); }
note("race-lobby", "ARENA cycle (RACE): " + tracks.join(" → "));
await shot(page, "race-lobby");
await ensureGo(page, "race-launch");
await key(page, "Enter");
await wait(1200);
await shot(page, "race-intro-gates");
await page.waitForFunction(() => __qa.M.countdown() <= 0, null, { timeout: 12000 });
await wait(2500);
await shot(page, "race-hud");
note("race-hud", JSON.stringify(await q(page, () => ({ clock: document.getElementById("clock").textContent, panels: [...document.querySelectorAll("#hud .panel .stocks")].map((s) => s.textContent) }))));
await freeze(page);
const gates = await q(page, () => __qa.Sim.gates().map((g) => ({ x: g.X, y: g.Y })));
note("race", `gates: ${gates.length}`);
if (gates.length) { await closeupAt(page, "race-finish-line", gates[0].x, gates[0].y, 800); await closeupAt(page, "race-next-portal", gates[1 % gates.length].x, gates[1 % gates.length].y, 800); }
await thaw(page);
await quitToLobby(page, true);

// 20 practice + 10 weapons
await clearBots(page);
await setMode(page, "PRACTICE");
await offRow(page);
await wait(200);
await shot(page, "practice-lobby");
note("practice-lobby", JSON.stringify(await lobbyState(page)));
await key(page, "Enter");
await page.waitForFunction(() => __qa.M.countdown() <= 0 && !__qa.Menu.visible(), null, { timeout: 12000 });
await wait(800);
await shot(page, "practice-range");
await closeup(page, "practice-range-close", 0, 700);
await thaw(page);
const weapons = ["RAILGUN", "MAG MINES", "SEEKERS", "REPULSOR", "SCATTER GUN", "TRACTOR"];
for (let k = 1; k <= 6; k++) {
  const name = weapons[k - 1];
  await key(page, `Digit${k}`);
  await wait(150);
  await shot(page, `weapon-${k}-armed-hud`, { clip: { x: 0, y: 0, width: 320, height: 140 } });
  if (k === 1 || k === 6) {
    await page.keyboard.down("KeyF");
    await wait(700);
    await freeze(page);
    await closeup(page, `weapon-${k}-charging`, 0, 520);
    await q(page, (k) => __qa.step(k === 1 ? 49 : 75), k);
    await q(page, () => { const p = __qa.pos(0); __qa.tags(false); __qa.cam(p.x + 250, p.y, 620); });
    await flashSeq(page, `weapon-${k}-release`, 6, 16, { i: 0, dx: 250, h: 620 });
    await page.keyboard.up("KeyF");
    await q(page, () => __qa.tags(true));
  } else {
    await freeze(page);
    await page.keyboard.down("KeyF");
    await q(page, () => __qa.step(3));
    await page.keyboard.up("KeyF");
    await q(page, (k) => __qa.step(k === 4 ? 4 : 18), k);
    await q(page, () => { const p = __qa.pos(0); __qa.tags(false); __qa.cam(p.x + 200, p.y, 620); });
    await shot(page, `weapon-${k}-fired`);
    await q(page, () => __qa.tags(true));
  }
  note("weapon-" + k, name + " · " + JSON.stringify(await q(page, () => ({ wep: document.querySelector("#hud .panel.p0 .wep")?.textContent, ammo: __qa.M.world().Ships[0].Ammo, bullets: __qa.arr(__qa.M.world().Bullets).length, mines: __qa.arr(__qa.M.world().Mines).length }))));
  await thaw(page);
  await key(page, "KeyR");
  await wait(300);
}
await shot(page, "practice-crate-wide");
await freeze(page);
await page.keyboard.down("KeyK");
await q(page, () => __qa.step(1));
await page.keyboard.up("KeyK");
await q(page, () => { const p = __qa.pos(__qa.Sim.target()); __qa.tags(false); __qa.cam(p.x, p.y, 520); });
await flashSeq(page, "target-kill-fx", 6, 16, { i: "target", dx: 0, h: 520 });
await q(page, () => __qa.tags(true));
await q(page, () => __qa.step(10));
await shot(page, "practice-kill-feed");
await thaw(page);
await wait(400);
await key(page, "KeyT");
await wait(400);
await shot(page, "practice-sudden-death");
await key(page, "KeyG");
await wait(800);
await freeze(page);
await closeup(page, "practice-launcher", 0, 700);
await thaw(page);
await key(page, "KeyR");
await quitToLobby(page, true);

// 21 phone pad
await clearBots(page);
await setMode(page, "FREE FOR ALL");
await offRow(page);
const padCtx = await browser.newContext({ ...devices["Pixel 7"] });
const pad = await padCtx.newPage();
watchConsole(pad, "pad");
await pad.goto(base + "/pad.html", { waitUntil: "load" });
await wait(1500);
await shot(pad, "pad-portrait-join");
const tap = async (sel, hold = 260) => {
  const r = await pad.evaluate((sel) => { const e = document.querySelector(sel); if (!e) return null; const b = e.getBoundingClientRect(); return { x: b.x + b.width / 2, y: b.y + b.height / 2 }; }, sel);
  if (!r) { note("pad", "missing " + sel); return; }
  await pad.mouse.move(r.x, r.y); await pad.mouse.down(); await wait(hold); await pad.mouse.up(); await wait(200);
};
note("pad", "phase text: " + (await pad.evaluate(() => document.body.innerText.replace(/\s+/g, " ").slice(0, 200))));
await tap('[data-k="fire"]');
await wait(500);
await shot(pad, "pad-card");
note("pad-card", await pad.evaluate(() => document.body.innerText.replace(/\s+/g, " ").slice(0, 200)));
await tap('[data-k="right"]');
await wait(300);
await shot(pad, "pad-card-colour");
await tap('[data-k="fire"]');
await wait(300);
await shot(pad, "pad-card-ready");
note("pad-lobby-host", JSON.stringify(await lobbyState(page)));
await shot(page, "lobby-with-phone");
await key(page, "Space");
await key(page, "Enter");
await page.waitForFunction(() => __qa.M.countdown() <= 0 && !__qa.Menu.visible(), null, { timeout: 15000 });
await wait(800);
await shot(pad, "pad-play-portrait");
await padCtx.close();
const landCtx = await browser.newContext({ ...devices["Pixel 7 landscape"] });
const padL = await landCtx.newPage();
watchConsole(padL, "pad-landscape");
await padL.goto(base + "/pad.html", { waitUntil: "load" });
await wait(1500);
await shot(padL, "pad-play-landscape-fresh");
note("pad-landscape", JSON.stringify(await padL.evaluate(() => ({ text: document.body.innerText.replace(/\s+/g, " ").slice(0, 160), stick: !!document.getElementById("stick"), vw: innerWidth, vh: innerHeight }))));
await page.setViewportSize({ width: 1366, height: 768 });
await wait(400);
await shot(page, "hud-1366");
await domCheck(page, "hud-1366", "#hud");
await page.setViewportSize({ width: 1920, height: 1080 });
await q(page, () => __qa.Menu.pause());
await wait(500);
await shot(padL, "pad-pause-menu");
note("pad-pause", await padL.evaluate(() => document.body.innerText.replace(/\s+/g, " ").slice(0, 160)));
await landCtx.close();

// 2 + 3 mobile landing + gate
const mob = await browser.newContext({ ...devices["Pixel 7"] });
const mp = await mob.newPage();
watchConsole(mp, "mobile");
await mp.goto(base + "/", { waitUntil: "networkidle" });
let nodesktop = await mp.evaluate(() => document.body.classList.contains("nodesktop"));
note("landing-mobile", `Pixel 7 emulation → nodesktop=${nodesktop}; any-hover:${await mp.evaluate(() => matchMedia("(any-hover: hover)").matches)} any-pointer-fine:${await mp.evaluate(() => matchMedia("(any-pointer: fine)").matches)} uaData.mobile:${await mp.evaluate(() => navigator.userAgentData?.mobile)}`);
if (!nodesktop) {
  await mob.addInitScript(() => Object.defineProperty(navigator, "userAgentData", { value: { mobile: true, brands: [], platform: "Android" } }));
  await mp.reload({ waitUntil: "networkidle" });
  nodesktop = await mp.evaluate(() => document.body.classList.contains("nodesktop"));
  note("landing-mobile", "after forcing userAgentData.mobile=true → nodesktop=" + nodesktop);
}
await wait(800);
await shot(mp, "landing-mobile-top");
await shot(mp, "landing-mobile-full", { fullPage: true });
note("landing-mobile", "CTA: " + (await mp.evaluate(() => document.querySelector(".hero .cta").innerText.replace(/\s+/g, " "))) + " · body scrollWidth " + (await mp.evaluate(() => document.documentElement.scrollWidth + " vs " + innerWidth)));
await mp.goto(base + "/play/", { waitUntil: "load" });
await wait(1200);
await shot(mp, "play-gate-mobile");
note("gate", JSON.stringify(await mp.evaluate(() => ({ gateShown: !document.getElementById("gate").hidden, text: document.getElementById("gate").innerText.replace(/\s+/g, " ").slice(0, 300), loader: document.getElementById("loader").className }))));
await mob.close();

await browser.close();

const md = [`# QA sweep raw notes (${new Date().toISOString()})`, "", `Base: ${base}`, "", "## Notes", ...notes.map((x) => `- **${x.screen}**: ${x.text}`), "", "## Console errors / warnings", ...(errors.length ? [...new Set(errors)].map((e) => `- ${e}`) : ["- none"]), "", "## Screenshots", ...shots.map((s) => `- ${s}`)].join("\n");
writeFileSync("qa/sweep.md", md);
writeFileSync("qa/sweep.json", JSON.stringify({ notes, errors, shots }, null, 2));
console.log(md);
