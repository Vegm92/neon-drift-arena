# Neon Drift Arena

Fable 5 (F# → JS) + Vite + Three.js couch brawler. Read `README.md` for rules, controls and layout.

## Commands

- `npm run dev` — Fable watch + Vite dev server.
- `npm run check` — headless simulation assertions (`dotnet run --project test`). Run after touching `Sim.fs` or `Domain.fs`.
- `dotnet fable src -o build` — one-shot compile; output is `build/*.js` (no `.fs.js` suffix).

## Constraints

- `Sim.fs`, `Domain.fs`, `Vec.fs` stay free of Fable/browser dependencies so `test/Check.fsproj` can compile them on plain .NET.
- All tunables live in `Domain.Cfg`. Keep `2 * arenaHalf / maxSpeed` inside 8–10 s.
- No networking, no inventories. UI is the lobby overlay (`Menu.fs`), the SETTINGS panel, the four corner panels and one banner.
- User-facing strings go through `Strings.fs`.
- Three.js bindings in `Three.fs` are hand-written and minimal; add a member only when the renderer uses it.
