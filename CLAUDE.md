# Neon Drift Arena

Fable 5 (F# → JS) + Vite + Three.js couch brawler. Read `README.md` for rules, controls and layout.

## Commands

- `npm run dev` — Fable watch + Vite dev server.
- `npm run check` — headless simulation assertions (`dotnet run --project test`). Run after touching `Sim.fs` or `Domain.fs`.
- `dotnet fable src -o build` — one-shot compile; output is `build/*.js` (no `.fs.js` suffix).

## Constraints

- `Core/Vec.fs`, `Core/Domain.fs`, `Core/Sim.fs` stay free of Fable/browser dependencies so `test/Check.fsproj` can compile them on plain .NET.
- All tunables live in `Domain.Cfg`. Keep `2 * arenaHalf / maxSpeed` inside 8–10 s.
- No internet networking between hosts, no inventories; the match always runs in one browser. Phone pads ride the Vite dev socket in dev and the `/relay` room socket in `deploy/server.mjs` on the deployed build (`pad.html`); LAN mirror clients (`nda:state`, host-authoritative) ride the Vite dev socket only. UI is the lobby overlay (`Menu.fs`), the SETTINGS panel, the four corner panels and one banner.
- User-facing strings go through `Strings.fs`; landing-page copy goes through `strings.json` (substituted by the `site-strings` Vite hook).
- Landing page is `index.html` at the root, the game lives at `play/index.html` (served at `/play/`).
- The site is the hub: `index.html` sends traffic to `/play/` first and to the store links (`stores` in `strings.json`) second. A store with an empty `url` renders as a dimmed SOON chip.
- Domain is `neondriftarena.com` (`siteUrl` in `strings.json`).
- Desktop only for now. `index.html` and `play/index.html` test `(any-hover: hover) and (any-pointer: fine)` plus `navigator.userAgentData.mobile`; on a phone or tablet the landing CTA turns into DESKTOP ONLY and `/play/` shows the `#gate` screen instead of importing `build/Main.js`. `?desktop=1` overrides the gate. `pad.html` is never gated — it is the phone controller.
- Three.js bindings in `Three.fs` are hand-written and minimal; add a member only when the renderer uses it.
