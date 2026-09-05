import { spawn } from "node:child_process";
import { fileURLToPath } from "node:url";
import assert from "node:assert/strict";

const port = 8787;
const server = spawn(process.execPath, [fileURLToPath(new URL("../deploy/server.mjs", import.meta.url))], {
  env: { ...process.env, PORT: String(port) },
  stdio: "inherit",
});

const open = (room, role) =>
  new Promise((ok, fail) => {
    const attempt = (left) => {
      const ws = new WebSocket(`ws://127.0.0.1:${port}/relay?room=${room}&role=${role}`);
      ws.inbox = [];
      ws.addEventListener("message", (e) => ws.inbox.push(e.data));
      ws.addEventListener("open", () => ok(ws));
      ws.addEventListener("error", () =>
        left > 0 ? setTimeout(() => attempt(left - 1), 200) : fail(new Error("relay never came up")));
    };
    attempt(25);
  });
const settle = () => new Promise((ok) => setTimeout(ok, 150));

let code = 0;
setTimeout(() => { console.error("relay check timed out"); server.kill(); process.exit(1); }, 30000).unref();

try {
  const [host, pad, other, otherHost] = await Promise.all([
    open("AAAA", "host"), open("AAAA", "pad"), open("BBBB", "pad"), open("BBBB", "host"),
  ]);

  pad.send("from-pad");
  host.send("from-host");
  other.send("from-other-room");
  await settle();

  assert.deepEqual(host.inbox, ["from-pad"], "host hears its own room's pad");
  assert.deepEqual(pad.inbox, ["from-host"], "pad hears its own host");
  assert.deepEqual(other.inbox, [], "a pad in another room hears nothing");
  assert.deepEqual(otherHost.inbox, ["from-other-room"], "rooms do not cross");

  pad.close();
  await settle();
  host.send("after-pad-left");
  await settle();
  assert.deepEqual(pad.inbox, ["from-host"], "a closed pad receives nothing further");

  console.log("relay ok");
} catch (err) {
  console.error(err.message);
  code = 1;
} finally {
  server.kill();
  process.exit(code);
}
