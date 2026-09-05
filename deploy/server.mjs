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

const rooms = new Map();
const send = (ws, msg) => ws?.readyState === 1 && ws.send(msg);

new WebSocketServer({ server, path: "/relay" }).on("connection", (ws, req) => {
  const query = new URL(req.url, "http://x").searchParams;
  const code = (query.get("room") ?? "").slice(0, 8);
  const isHost = query.get("role") === "host";
  if (!code) return ws.close();

  let room = rooms.get(code);
  if (!room) rooms.set(code, (room = { host: null, pads: new Set() }));
  if (isHost) { room.host?.close(); room.host = ws; } else room.pads.add(ws);

  ws.on("message", (buf) => {
    const msg = buf.toString().slice(0, 4096);
    if (isHost) for (const pad of room.pads) send(pad, msg);
    else send(room.host, msg);
  });
  ws.on("close", () => {
    if (isHost) { if (room.host === ws) room.host = null; } else room.pads.delete(ws);
    if (!room.host && room.pads.size === 0) rooms.delete(code);
  });
});

server.listen(process.env.PORT ?? 3000);
