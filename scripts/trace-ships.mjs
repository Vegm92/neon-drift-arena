import { PNG } from "pngjs";
import { readFileSync, writeFileSync } from "node:fs";

const CELL = [530, 450];
const CELLS = [[217, 40], [790, 46], [12, 452], [502, 481], [984, 474]];
const SCALE = 54 / 450;
const EPS = 3;
const SMOOTH = 4;

const png = PNG.sync.read(readFileSync("public/ships.png"));

function cellPixels([cx, cy]) {
  const [w, h] = CELL;
  const at = (x, y) => {
    const i = ((cy + y) * png.width + cx + x) * 4;
    return [png.data[i], png.data[i + 1], png.data[i + 2], png.data[i + 3]];
  };
  const hull = new Uint8Array(w * h);
  const glow = new Uint8Array(w * h);
  for (let y = 0; y < h; y++)
    for (let x = 0; x < w; x++) {
      const [r, g, b, a] = at(x, y);
      if (a < 232) continue;
      hull[y * w + x] = 1;
      const mx = Math.max(r, g, b), mn = Math.min(r, g, b);
      if (mx > 120 && mx - mn > 50) glow[y * w + x] = 1;
    }
  return { w, h, hull, glow };
}

function components(mask, w, h) {
  const seen = new Uint8Array(w * h);
  const out = [];
  for (let s = 0; s < w * h; s++) {
    if (!mask[s] || seen[s]) continue;
    const px = [];
    const st = [s];
    seen[s] = 1;
    while (st.length) {
      const p = st.pop();
      px.push(p);
      const x = p % w, y = (p / w) | 0;
      for (const [dx, dy] of [[1, 0], [-1, 0], [0, 1], [0, -1]]) {
        const nx = x + dx, ny = y + dy;
        if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
        const q = ny * w + nx;
        if (mask[q] && !seen[q]) { seen[q] = 1; st.push(q); }
      }
    }
    out.push(px);
  }
  return out;
}

function toMask(px, w, h) {
  const m = new Uint8Array(w * h);
  for (const p of px) m[p] = 1;
  return m;
}

function trace(mask, w, h) {
  const inside = (x, y) => x >= 0 && y >= 0 && x < w && y < h && !!mask[y * w + x];
  let start = -1;
  for (let i = 0; i < w * h; i++) if (mask[i]) { start = i; break; }
  let x = start % w, y = (start / w) | 0;
  let fx = 1, fy = 0;
  const pts = [];
  for (let guard = 0; guard < 400000; guard++) {
    pts.push([x, y]);
    const rx = -fy, ry = fx;
    const ar = inside(Math.floor(x + fx / 2 + rx / 2), Math.floor(y + fy / 2 + ry / 2));
    const al = inside(Math.floor(x + fx / 2 - rx / 2), Math.floor(y + fy / 2 - ry / 2));
    if (!ar) { [fx, fy] = [rx, ry]; }
    else if (al) { [fx, fy] = [-rx, -ry]; }
    x += fx; y += fy;
    if (x === pts[0][0] && y === pts[0][1]) break;
  }
  return pts;
}

function rdp(pts, eps) {
  if (pts.length < 3) return pts;
  const [a, b] = [pts[0], pts[pts.length - 1]];
  let idx = 0, dmax = 0;
  for (let i = 1; i < pts.length - 1; i++) {
    const d = pointLine(pts[i], a, b);
    if (d > dmax) { dmax = d; idx = i; }
  }
  if (dmax <= eps) return [a, b];
  return rdp(pts.slice(0, idx + 1), eps).slice(0, -1).concat(rdp(pts.slice(idx), eps));
}

function pointLine([px, py], [ax, ay], [bx, by]) {
  const dx = bx - ax, dy = by - ay;
  const l2 = dx * dx + dy * dy;
  if (l2 === 0) return Math.hypot(px - ax, py - ay);
  const t = Math.max(0, Math.min(1, ((px - ax) * dx + (py - ay) * dy) / l2));
  return Math.hypot(px - ax - t * dx, py - ay - t * dy);
}

function smooth(pts, k) {
  const n = pts.length;
  return pts.map((_, i) => {
    let x = 0, y = 0;
    for (let j = -k; j <= k; j++) { const q = pts[(i + j + n) % n]; x += q[0]; y += q[1]; }
    return [x / (2 * k + 1), y / (2 * k + 1)];
  });
}

function simplifyClosed(raw, eps) {
  const pts = smooth(raw, SMOOTH);
  const half = pts.length >> 1;
  const a = rdp(pts.slice(0, half + 1), eps);
  const b = rdp(pts.slice(half).concat([pts[0]]), eps);
  return a.slice(0, -1).concat(b.slice(0, -1));
}

const ships = CELLS.map((c, i) => {
  const { w, h, hull, glow } = cellPixels(c);
  const body = components(hull, w, h).sort((a, b) => b.length - a.length)[0];
  const bodyMask = toMask(body, w, h);
  let minX = w, maxX = 0, minY = h, maxY = 0;
  for (const p of body) {
    const x = p % w, y = (p / w) | 0;
    minX = Math.min(minX, x); maxX = Math.max(maxX, x);
    minY = Math.min(minY, y); maxY = Math.max(maxY, y);
  }
  const s = SCALE;
  const cx = (minX + maxX) / 2, cy = (minY + maxY) / 2;
  const flip = i === 3 ? -1 : 1;
  const map = ([x, y]) => [+(-(y - cy) * s * flip).toFixed(2), +((x - cx) * s * flip).toFixed(2)];
  const poly = (m) => simplifyClosed(trace(m, w, h), EPS).map(map);
  const glowIn = new Uint8Array(w * h);
  for (let p = 0; p < w * h; p++) if (glow[p] && bodyMask[p]) glowIn[p] = 1;
  const decals = components(glowIn, w, h)
    .filter((px) => {
      if (px.length < 60) return false;
      let a = w, b = 0, c2 = h, d = 0;
      for (const p of px) { const x = p % w, y = (p / w) | 0; a = Math.min(a, x); b = Math.max(b, x); c2 = Math.min(c2, y); d = Math.max(d, y); }
      const fill = px.length / ((b - a + 1) * (d - c2 + 1));
      return fill > 0.3 && Math.min(b - a, d - c2) > 6;
    })
    .map((px) => poly(toMask(px, w, h)));
  console.log(`ship ${i}: hull ${poly(bodyMask).length} pts, ${decals.length} decals`);
  return { hull: poly(bodyMask), decals };
});

writeFileSync("src/UI/Render/ShipHulls.json", JSON.stringify(ships));
