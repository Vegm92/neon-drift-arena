const { chromium } = require("C:/Users/victo/AppData/Local/npm-cache/_npx/9833c18b2d85bc59/node_modules/playwright");

const BASE = "http://localhost:8791";
const ROOM = "TESTROOM";
const results = [];
const check = (name, pass, detail) => {
  results.push({ name, pass, detail });
  console.log(`${pass ? "ok  " : "FAIL"} ${name}${detail ? "  — " + detail : ""}`);
};

const spy = (room) => {
  sessionStorage.setItem("nda-room", room);
  window.__spy = { sent: [], recv: [], role: null };
  const Native = window.WebSocket;
  window.WebSocket = function (url, proto) {
    const ws = new Native(url, proto);
    window.__spy.url = String(url);
    ws.addEventListener("message", (e) => {
      try {
        const m = JSON.parse(e.data);
        window.__spy.recv.push({ ev: m.event, t: Date.now() });
        if (m.event === "nda:role") window.__spy.role = m.data.host;
      } catch {}
    });
    const send = ws.send.bind(ws);
    ws.send = (d) => {
      try { window.__spy.sent.push({ ev: JSON.parse(d).event, t: Date.now() }); } catch {}
      return send(d);
    };
    return ws;
  };
  window.WebSocket.prototype = Native.prototype;
  // block the real CrazyGames SDK, stub what Input/Menu touch
  window.CrazyGames = {
    SDK: {
      init: async () => {},
      game: {
        gameplayStart() {}, gameplayStop() {}, loadingStart() {}, loadingStop() {},
        addSettingsChangeListener() {}, addRoomJoinListener() {},
        getInviteLinkParameters: () => ({}), inviteLink: async () => "",
        updateRoom() {}, leftRoom() {}, isInstantMultiplayer: false, showInviteButton() {},
      },
      user: { isUserAccountAvailable: false, getUser: async () => null, addAuthListener() {} },
      ad: { requestAd: async () => {} },
      banner: { requestBanner: async () => {}, clearAllBanners() {} },
      data: { getItem: (k) => localStorage.getItem(k), setItem: (k, v) => localStorage.setItem(k, v) },
    },
  };
};

const counts = (s, ev, since) => s.filter((m) => m.ev === ev && (!since || m.t >= since)).length;

(async () => {
  const browser = await chromium.launch({
    channel: "chrome",
    args: ["--enable-unsafe-swiftshader", "--autoplay-policy=no-user-gesture-required"],
  });
  const open = async () => {
    const ctx = await browser.newContext();
    await ctx.route("**/*crazygames*sdk*", (r) => r.fulfill({ body: "", contentType: "text/javascript" }));
    await ctx.addInitScript(spy, ROOM);
    const page = await ctx.newPage();
    page.on("pageerror", (e) => console.log("  [pageerror]", String(e).slice(0, 160)));
    await page.goto(`${BASE}/play/?desktop=1`, { waitUntil: "load" });
    return { ctx, page };
  };

  const A = await open();
  await A.page.waitForTimeout(2500);
  const B = await open();
  await B.page.waitForTimeout(3500);

  const read = (p) => p.evaluate(() => window.__spy);
  let a = await read(A.page), b = await read(B.page);

  check("A is told it is the host", a.role === true, `role=${a.role}`);
  check("B is told it is a peer", b.role === false, `role=${b.role}`);
  check("A broadcasts nda:state", counts(a.sent, "nda:state") > 5, `${counts(a.sent, "nda:state")} sent`);
  check("B never broadcasts nda:state", counts(b.sent, "nda:state") === 0, `${counts(b.sent, "nda:state")} sent`);
  check("B receives A's nda:state", counts(b.recv, "nda:state") > 5, `${counts(b.recv, "nda:state")} received`);

  // throttle: measure A's send rate over a 2s window
  const t0 = Date.now();
  await A.page.waitForTimeout(2000);
  a = await read(A.page);
  const rate = counts(a.sent, "nda:state", t0) / 2;
  check("A sends at the ~10/s netStateMs tick, not per frame", rate > 5 && rate < 16, `${rate.toFixed(1)}/s`);

  // no flip-flop: B must stay a peer and stay silent
  b = await read(B.page);
  check("B stays a peer (no flip-flop)", b.role === false && counts(b.sent, "nda:state") === 0, `role=${b.role}, sent=${counts(b.sent, "nda:state")}`);

  // host promotion: close A, B should be promoted and start broadcasting
  const tKill = Date.now();
  await A.ctx.close();
  await B.page.waitForTimeout(6000);
  b = await read(B.page);
  check("B is promoted to host when A leaves", b.role === true, `role=${b.role}`);
  check("promoted B starts broadcasting", counts(b.sent, "nda:state", tKill) > 5, `${counts(b.sent, "nda:state", tKill)} sent after promotion`);

  await browser.close();
  const failed = results.filter((r) => !r.pass);
  console.log(`\n${results.length - failed.length}/${results.length} passed`);
  process.exit(failed.length ? 1 : 0);
})().catch((e) => { console.error(e); process.exit(1); });
