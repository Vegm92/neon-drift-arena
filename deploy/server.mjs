import { createServer } from "node:http";
import { createReadStream } from "node:fs";
import { stat } from "node:fs/promises";
import { extname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { createGzip } from "node:zlib";
import { WebSocketServer } from "ws";

const root = fileURLToPath(new URL("site", import.meta.url));
const types = {
  ".html": "text/html", ".js": "text/javascript", ".css": "text/css", ".json": "application/json",
  ".svg": "image/svg+xml", ".png": "image/png", ".jpg": "image/jpeg", ".ico": "image/x-icon",
  ".mp4": "video/mp4", ".webm": "video/webm", ".mp3": "audio/mpeg", ".woff2": "font/woff2",
};
const zippable = /\.(html|js|css|json|svg)$/;

const server = createServer(async (req, res) => {
  let path = new URL(req.url, "http://x").pathname;
  try { path = decodeURIComponent(path); } catch {}
  let file = join(root, path);
  if (file !== root && !file.startsWith(root + "/") && !file.startsWith(root + "\\")) return res.writeHead(403).end();
  try { if ((await stat(file)).isDirectory()) file = join(file, "index.html"); await stat(file); }
  catch { return res.writeHead(404).end(); }
  const gzip = zippable.test(file) && /\bgzip\b/.test(req.headers["accept-encoding"] ?? "");
  res.writeHead(200, {
    "content-type": types[extname(file)] ?? "application/octet-stream",
    vary: "accept-encoding",
    ...(gzip ? { "content-encoding": "gzip" } : {}),
  });
  const body = createReadStream(file);
  gzip ? body.pipe(createGzip()).pipe(res) : body.pipe(res);
});

const maxMsg = 262144;
const rooms = new Map();
const send = (ws, msg) => ws?.readyState === 1 && ws.send(msg);

const log = (level, message, fields) =>
  console.log(JSON.stringify({ level, message, ...fields }));

const seen = new Map();
const every = (ms, key) => {
  const now = Date.now();
  if (now - (seen.get(key) ?? 0) < ms) return false;
  seen.set(key, now);
  return true;
};

new WebSocketServer({ server, path: "/relay", perMessageDeflate: { threshold: 1024 } }).on("connection", (ws, req) => {
  const query = new URL(req.url, "http://x").searchParams;
  const code = (query.get("room") ?? "").slice(0, 8);
  const wantsHost = query.get("role") === "host";
  if (!code) return ws.close();

  let room = rooms.get(code);
  if (!room) rooms.set(code, (room = { host: null, pads: new Set() }));
  const isHost = wantsHost && !room.host;
  ws.wantsHost = wantsHost;
  if (isHost) room.host = ws; else room.pads.add(ws);
  send(ws, JSON.stringify({ event: "nda:role", data: { host: isHost } }));

  const role = isHost ? "host" : wantsHost ? "peer" : "pad";
  const at = (level, message, fields) =>
    log(level, message, { room: code, role, peers: room.pads.size + (room.host ? 1 : 0), ...fields });
  ws.joinedAt = Date.now();
  at("info", "joined");

  ws.on("message", (buf) => {
    const msg = buf.toString();
    if (msg.length > maxMsg)
      return every(5000, code + "big") && at("error", "dropped an oversize payload", { bytes: msg.length, limit: maxMsg });

    if (msg.length < 8192 && msg.includes('"nda:log"')) {
      try {
        const { data } = JSON.parse(msg);
        return at(data.level ?? "info", data.message, { source: "browser", ...data.fields });
      } catch {}
    }

    const t0 = performance.now();
    if (room.host === ws) for (const pad of room.pads) send(pad, msg);
    else send(room.host, msg);
    const ms = performance.now() - t0;
    if (ms > 5 && every(5000, code + "slow"))
      at("warn", "relaying one message took longer than a frame", { relayMs: +ms.toFixed(1), bytes: msg.length });

    room.msgs = (room.msgs ?? 0) + 1;
    room.bytes = (room.bytes ?? 0) + msg.length;
    if (every(30000, code + "rate"))
      at("debug", "relay throughput", { msgs: room.msgs, kb: Math.round(room.bytes / 1024) });
  });
  ws.on("close", () => {
    if (room.host === ws) {
      room.host = null;
      for (const peer of room.pads) {
        if (!peer.wantsHost) continue;
        room.pads.delete(peer);
        room.host = peer;
        send(peer, JSON.stringify({ event: "nda:role", data: { host: true } }));
        break;
      }
    } else room.pads.delete(ws);
    if (!room.host && room.pads.size === 0) rooms.delete(code);
    at("info", "left", { heldForSec: Math.round((Date.now() - ws.joinedAt) / 1000) });
  });
});

process.on("uncaughtException", (err) => log("error", "uncaught exception", { stack: err.stack }));
process.on("unhandledRejection", (err) => log("error", "unhandled rejection", { stack: String(err?.stack ?? err) }));

const port = process.env.PORT ?? 3000;
server.listen(port, () => log("info", "relay listening", { port: Number(port) }));
