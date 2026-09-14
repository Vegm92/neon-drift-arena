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
  window.__cg = { inviteCalls: [], settingsListeners: [], rooms: [], left: 0 };
  // block the real CrazyGames SDK, stub what Input/Menu touch
  window.CrazyGames = {
    SDK: {
      init: async () => {},
      game: {
        gameplayStart() {}, gameplayStop() {}, loadingStart() {}, loadingStop() {},
        addSettingsChangeListener(f) { window.__cg.settingsListeners.push(f); }, addRoomJoinListener() {},
        addJoinRoomListener() {},
        getInviteLinkParameters: () => ({}),
        inviteLink: async (p) => { window.__cg.inviteCalls.push(p); return "https://www.crazygames.com/i/SDKLINK"; },
        updateRoom(r) { window.__cg.rooms.push(r); }, leftRoom() { window.__cg.left++; },
        isInstantMultiplayer: false, showInviteButton() {},
        hideInviteButton() {},
        settings: { muteAudio: false, disableChat: false },
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

  // the SDK is the source of the invite link, and the lobby offers it
  const inviteCalls = await A.page.evaluate(() => window.__cg.inviteCalls.length);
  check("A asks the SDK for the room's invite link", inviteCalls > 0, `${inviteCalls} calls`);
  const panel = await A.page.evaluate(() => {
    const el = document.querySelector("#menu .legend.invite");
    return el ? el.textContent.trim() : null;
  });
  check("the lobby shows the invite panel", !!panel, panel ?? "absent");

  // chat: host types, the peer must see the line; then the other way round
  const say = (p, text) =>
    p.evaluate((t) => {
      const inp = document.querySelector("#chat input");
      if (!inp) return false;
      inp.focus();
      inp.value = t;
      inp.dispatchEvent(new KeyboardEvent("keydown", { key: "Enter", bubbles: true }));
      return true;
    }, text);
  const log = (p) => p.evaluate(() => [...document.querySelectorAll("#chat .log > div")].map((d) => d.textContent));

  check("the chat panel is up in the lobby", await say(A.page, "hello from the host"), "");
  await A.page.waitForTimeout(400);
  check("the host's line lands in its own log", (await log(A.page)).some((l) => l.includes("hello from the host")), JSON.stringify(await log(A.page)));
  await B.page.waitForTimeout(600);
  check("the peer sees the host's line", (await log(B.page)).some((l) => l.includes("hello from the host")), JSON.stringify(await log(B.page)));

  await say(B.page, "and back from the peer");
  await A.page.waitForTimeout(600);
  check("the host sees the peer's line", (await log(A.page)).some((l) => l.includes("and back from the peer")), JSON.stringify(await log(A.page)));
  await B.page.waitForTimeout(600);
  check("the peer sees its own line echoed by the host", (await log(B.page)).some((l) => l.includes("and back from the peer")), JSON.stringify(await log(B.page)));

  // chat text is escaped, never injected
  await say(A.page, "<img src=x onerror=alert(1)>");
  await A.page.waitForTimeout(400);
  const injected = await A.page.evaluate(() => document.querySelectorAll("#chat img").length);
  check("a message cannot inject markup", injected === 0, `${injected} elements`);

  // disableChat: the SDK setting hides the panel on that machine
  await A.page.evaluate(() => {
    window.CrazyGames.SDK.game.settings.disableChat = true;
    window.__cg.settingsListeners.forEach((f) => f({ disableChat: true, muteAudio: false }));
  });
  await A.page.waitForTimeout(400);
  const hidden = await A.page.evaluate(() => document.getElementById("chat").className.includes("hidden"));
  check("disableChat hides the chat panel", hidden, `class=${await A.page.evaluate(() => document.getElementById("chat").className)}`);
  await A.page.evaluate(() => {
    window.CrazyGames.SDK.game.settings.disableChat = false;
    window.__cg.settingsListeners.forEach((f) => f({ disableChat: false, muteAudio: false }));
  });

  // between rounds the room must go back to joinable, so an invited friend can
  // still walk in; and the peer must never be dropped on the way through
  const joinable = () => A.page.evaluate(() => window.__cg.rooms.map((r) => r.isJoinable));
  const bRecv = () => B.page.evaluate(() => window.__spy.recv.filter((m) => m.ev === "nda:state").length);
  // the key has to be held across at least one frame for `rising` to see it
  const pressA = async (code, key) => {
    const fire = (type) =>
      A.page.evaluate(([t, c, k]) => window.dispatchEvent(new KeyboardEvent(t, { code: c, key: k, bubbles: true })), [type, code, key]);
    await fire("keydown");
    await A.page.waitForTimeout(150);
    await fire("keyup");
    await A.page.waitForTimeout(150);
  };
  await A.page.evaluate(() => document.activeElement?.blur?.());
  await pressA("Space", " ");
  await A.page.waitForTimeout(300);
  await pressA("Enter", "Enter");
  await A.page.waitForTimeout(7000);
  const inMatch = await A.page.evaluate(() => document.getElementById("menu").className.includes("hidden"));
  check("the lobby launches a round", inMatch, `menu=${await A.page.evaluate(() => document.getElementById("menu").className)}`);
  check("the room closes while the round runs", (await joinable()).at(-1) === false, JSON.stringify(await joinable()));
  const before = await bRecv();
  // PAUSE > QUIT TO LOBBY, then YES: the shortest way back to the lobby
  await pressA("Enter", "Enter");
  for (let i = 0; i < 3; i++) await pressA("ArrowDown", "ArrowDown");
  await pressA("Enter", "Enter");
  await pressA("ArrowDown", "ArrowDown");
  await pressA("Enter", "Enter");
  await A.page.waitForTimeout(1500);
  check("the room reopens for friends back in the lobby", (await joinable()).at(-1) === true, JSON.stringify(await joinable()));
  check("the peer is still in the room after the round", (await bRecv()) > before, `${(await bRecv()) - before} more snapshots`);
  check("the host never announced it left the room", (await A.page.evaluate(() => window.__cg.left)) === 0, "");

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
