# Developing Neon Drift Arena

F# compiled to JavaScript with [Fable 5](https://fable.io/), rendered with Three.js + bloom. The simulation is pure .NET and runs headless in tests. Game rules and controls live in [GAMEPLAY.md](GAMEPLAY.md).

## Run

```bash
npm install
npm run dev
```

Opens Vite on http://localhost:5173 with Fable in watch mode. The landing page is served at `/`, the game at `/play/` and the phone pad at `/pad.html`.

Requires the .NET 10 SDK (Fable 5.15 ships as a net10.0 tool) and Node 18+.

```bash
npm run check      # headless simulation assertions on .NET
dotnet fable src -o build   # one-shot compile, output is build/*.js
```

`npm run check` runs `Sim` on .NET (no browser) and asserts traverse time, recoil, hits, pads, out-of-bounds, respawn, rams and win condition. Run it after touching `Sim.fs` or `Domain.fs`.

## Site

`index.html` at the root is the marketing page: a muted autoplay loop of `public/trailer.mp4` (a 27 s cut of real bots matches, poster `public/trailer.jpg`), one PLAY FREE button pointing at `/play/`, four feature rows, a hub block with WATCH TRAILER and the store links, and the OG card `public/og.jpg`.

Every string on the page lives in `strings.json`; the `site-strings` hook in `vite.config.js` substitutes `{{key}}` placeholders in dev and build. Store links come from the `stores` array in the same file — an entry with an empty `url` renders as a dimmed SOON chip, so a store goes live by filling in its url and nothing else.

The game is desktop only for now. Both the landing page and `/play/` test `(any-hover: hover) and (any-pointer: fine)` plus `navigator.userAgentData.mobile`: on a phone or tablet the CTA reads DESKTOP ONLY and `/play/` shows a gate screen instead of importing `build/Main.js`, so the game bundle is never downloaded there. `?desktop=1` overrides the gate. `pad.html` is never gated — it is the phone controller.

## Deploy

`npm run build` emits the page, the game and the pad into `dist/`. The root `Dockerfile` builds it on Railway (a .NET 10 + Node stage runs Fable and Vite, a Node image serves `dist/`): the `site` service of the `neon-drift-arena` project deploys it from GitHub on every push (https://site-production-a98b.up.railway.app), and `npm run deploy` uploads the working tree for a one-off build. `deploy/server.mjs` serves those files gzipped and carries the phone-pad relay on `/relay`.

## Phone pads and LAN mirrors

Under the dev server, pad messages travel over Vite's HMR WebSocket and the QR code points at `http://<lan-ip>:5173/pad.html` (`npm run dev` serves on the LAN with `--host`). On the deployed build there is no HMR, so the host page mints a four-character room code (kept in `sessionStorage`), the QR points at `https://<site>/pad.html#<code>`, and both ends meet on `/relay` in `deploy/server.mjs`, which only ever forwards a room's phones to that room's host. The relay carries pad input, nothing else — the match still runs entirely in the host browser.

LAN mirror clients (`nda:state`, host-authoritative) ride the Vite dev socket only.

## Tuning

Every tunable in `Cfg` is a row in SETTINGS > TUNING, adjusted with left/right in steps of a twentieth of the code default and clamped to three times it. Changes save to `localStorage` (`nda-tweaks`) immediately; RESET clears it and reloads with the code defaults. Reverse thrust is `reverseFactor` × thrust.

## Layout

| File | Responsibility |
|------|----------------|
| **Core** (pure .NET, no browser deps) | |
| `src/Core/Vec.fs` | 2D vector struct and helpers |
| `src/Core/Domain.fs` | Tunables (`Cfg`), input, ship, bullet, pad, world records |
| `src/Core/Strings.fs` | All user-facing strings and locale |
| `src/Core/Sim.fs` | Pure fixed-step simulation: movement, firing, bullets, rams, pads, deaths, match phase |
| **Platform** (browser bindings) | |
| `src/Platform/Input.fs` | Keyboard + Gamepad API → `Input[]` for the four slots |
| `src/Platform/Three.fs` | Minimal Three.js bindings used by the renderer |
| `src/Platform/Sfx.fs` | WebAudio synth: arcade square/noise voices, shimmer delay bus, stereo pan, boost engine drone; music player (`public/music/menu.mp3` loops in menus, `battle1..3.mp3` shuffle in play) |
| **UI** (browser-dependent screens) | |
| `src/UI/Render/RenderTypes.fs` | Render types, constants, shared material helpers |
| `src/UI/Render/RenderMeshes.fs` | Mesh factories (`mkShip`, `mkPad`, `mkArena`...) and `syncArena` |
| `src/UI/Render/RenderFx.fs` | Particle effects: bursts, rings, bolts, beams, flashes |
| `src/UI/Render/RenderEnt.fs` | Per-entity draw functions (ships, pads, crates, bullets, portals, holes) |
| `src/UI/Render/RenderHud.fs` | DOM HUD: panels, kill feed, weapon icons, tags, tint, bloom |
| `src/UI/Render/RenderCam.fs` | Camera framing, intro flyby, resize |
| `src/UI/Render/Render.fs` | `create()`, `draw()` orchestrator, event → effect dispatch |
| `src/UI/Settings.fs` | Settings model: rows for controller slots, swap sticks (`nda-pads`) and `Cfg.tunables` (`nda-tweaks`), plus their persistence |
| `src/UI/Menu.fs` | Lobby, settings, pause and result overlays: join/leave/launch by device, owns the `joined` slot set, renders the phone QR (`src/qr.js`) |
| `src/UI/Pad.fs` | Phone controller page (`pad.html`): lobby card, floating stick, fire/boost zone, menu buttons; talks to the host over the HMR socket |
| **Entry point** | |
| `src/Main.fs` | requestAnimationFrame loop with a 120 Hz accumulator |
| **Test** | |
| `test/Check.fs` | Headless .NET run of the simulation with assertions |
