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

`index.html` at the root is the marketing page: a muted autoplay loop of `public/trailer.mp4` (a 27 s cut of real bots matches, poster `public/trailer.jpg`), one PLAY FREE button pointing at `play/`, four feature rows, a hub block with WATCH TRAILER and the store links, and the OG card `public/og.jpg`.

Every string on the page lives in `strings.json`; the `site-strings` hook in `vite.config.js` substitutes `{{key}}` placeholders in dev and build. Store links come from the `stores` array in the same file — an entry with an empty `url` renders as a dimmed SOON chip, so a store goes live by filling in its url and nothing else.

The game is desktop only for now. Both the landing page and `/play/` test `(any-hover: hover) and (any-pointer: fine)` plus `navigator.userAgentData.mobile`: on a phone or tablet the CTA reads DESKTOP ONLY and `/play/` shows a gate screen instead of importing `build/Main.js`, so the game bundle is never downloaded there. `?desktop=1` overrides the gate. `pad.html` is never gated — it is the phone controller.

## Deploy

`npm run build` emits the page, the game and the pad into `dist/`. The root `Dockerfile` builds it on Railway (a .NET 10 + Node stage runs Fable and Vite, a Node image serves `dist/`): the `site` service of the `neon-drift-arena` project deploys it from GitHub on every push (https://site-production-a98b.up.railway.app), and `npm run deploy` uploads the working tree for a one-off build. `deploy/server.mjs` serves those files gzipped and carries the phone-pad relay on `/relay`.

## CrazyGames

The SDK v2 script is loaded from `sdk.crazygames.com` in both `index.html` and `play/index.html`. Every call into it goes through `src/Platform/CrazyGames.fs`, which imports `src/Platform/CrazyGames.js` — gameplay start/stop, the platform mute listener, invite rooms, the user module, video ads, banners, the data module and the leaderboard. Nothing else in the codebase touches `window.CrazyGames`.

`base: "./"` in `vite.config.js` keeps every emitted path relative so the build runs from a CrazyGames subpath; links in the HTML must stay relative for the same reason.

- **Boot order.** `Main.fs` awaits `CrazyGames.init` (which hands `Sfx.setCrazyGamesMuted` the platform mute) before calling `Input.initNetwork ()` and `Menu.initUser ()`, so a room arriving from an invite is known before the socket opens.
- **Rooms.** `Input.initNetwork ()` takes the room from `getInviteParams`, else `sessionStorage`, else a fresh code, then connects through `CrazyGames.createRelaySocket` (WebRTC where available, the `/relay` WebSocket otherwise) and calls `updateRoom`. A join event stores the room and reloads.
- **Audio.** `Sfx.isMuted ()` is the OR of three flags: the platform mute, the ad mute and the local `M` toggle. The platform mute can never be overridden from in-game.
- **Ads.** The midgame ad runs between the match ending and the result screen; `adPlaying` freezes the sim to a render-only frame and mutes, and `gameplayStop`/`gameplayStart` bracket it. Banners live in `#cg-banner-1` and `#cg-banner-2` in `play/index.html`, are requested on every menu screen at most once per 31 s, and are cleared when the menu hides.
- **Progress and identity.** `Menu.loadProgress` / `saveProgress` keep `nda-high-score` and `nda-matches-played` in the data module; `Menu.initUser` sets the player tag from `getUser` and updates it live through `addAuthListener`. SETTINGS shows a SIGN IN row only while `Settings.isGuest`.

`docs/CrazyGames-v1-plan.md` and `docs/CrazyGames-v2-plan.md` hold the publication plans and their accept/reject criteria.

## Phone pads and LAN mirrors

Under the dev server, pad messages travel over Vite's HMR WebSocket and the QR code points at `http://<lan-ip>:5173/pad.html` (`npm run dev` serves on the LAN with `--host`). On the deployed build there is no HMR, so the host page mints a four-character room code (kept in `sessionStorage`), the QR points at `https://<site>/pad.html#<code>`, and both ends meet on `/relay` in `deploy/server.mjs`, which only ever forwards a room's phones to that room's host. The relay carries pad input, nothing else — the match still runs entirely in the host browser.

LAN mirror clients (`nda:state`, host-authoritative) ride the Vite dev socket only.

## Tuning

SETTINGS > TUNING is a developer tool and only appears with `?dev=1` in the URL, mirroring `?desktop=1`; without it players see CONTROLLERS, ARENA and AUDIO only. Behind the flag every tunable in `Cfg` is a row, adjusted with left/right in steps of a twentieth of the code default and clamped to three times it. Changes save to `localStorage` (`nda-tweaks`) immediately; RESET clears it and reloads with the code defaults. Reverse thrust is `reverseFactor` × thrust.

## Map editor

`npm run dev`, then open `/mapedit.html`. It reads the compiled `build/Core/Maps.js` and `build/Core/Strings.js`, so what you see and the names you edit are exactly what the game loads.

Every map is either **FFA** or **RACE** — the same split `Settings.pool ()` uses, and it is simply whether `Layout.Track` is `Some`. The picker shows `index  MODE - NAME`, the name box renames the entry in `Strings.Arenas`, and `+ ffa` / `+ race` append a new map that already satisfies every rule, so you can save it straight away. On an FFA map the track panel and the track-node tool are hidden and the track rules are skipped; on a RACE map they appear.

Drag anything to move it, wheel over a rock to resize, Del to delete, Shift-drag snaps to 50. The paint buttons place rocks, pads, crates and track nodes (a node is inserted after the nearest one). The road is drawn through the same Catmull-Rom `smoothLoop` the sim uses, so the ribbon is the real driveable surface, and the crosshairs are the four spawn points `Sim.spawnPos` will produce.

SYMMETRY is a mode, not a one-shot: pick `⇄`, `⇅`, `quad` or `spin` and its axes stay drawn on the canvas in the toggle's own colour while every edit mirrors as you make it. Placing one rock places all its twins, dragging one drags them, the wheel resizes them together and Del removes the set. Partners are found by position at the moment you grab something, so it works on the maps already in the repo without any per-item bookkeeping - grab one of CORE RING's four corner rocks with `quad` on and the other three follow. An item sitting on an axis has no twin and stays single. `off` returns to editing one item at a time.

TRANSFORM applies to the whole map, track nodes included. Hovering any transform button previews it on the canvas before you commit: its axis is drawn as a dashed line, the ghost outlines show where every item will land, and `fold` shades the half it is about to throw away in red. Each button is tinted with its own axis colour so the button and the line always match - magenta for the vertical axis (`mirror` and `fold` left/right), amber for the horizontal one (`mirror` and `fold` up/down), blue for the `rot 90` centre pivot, green for the two axes and centre that `spin x4` folds around. `mirror` and `rot` move everything, `fold` keeps the positive half and mirrors it back to make the map symmetric, `spin x4` keeps the first quadrant and repeats it four times (the `quad`/`spin` helpers in `Maps.fs`, applied by hand), and `jitter` nudges everything by a random amount up to the box value.

RULES mirrors the per-arena invariants in `test/Check.fs` and re-checks on every edit: rock/pad/crate/spawn clearances at the sim's own margins, rocks overlapping or reaching past `arenaHalf` or `diagLimit ()`, and then per mode — FFA wants exactly one shield core, four heal pads, eight crate spots, an open lane out of the centre and an 8-10 s crossing; RACE wants at least three nodes, `raceHalf` size, a road that stays inside the arena and never doubles back within `trackWidth * 0.9`. Red breaks the sim, yellow is a smell. Saving with red findings asks first. `npm run check` stays the real gate.

WRITE TO MAPS.FS posts to the dev-server-only `/__maps` hook, which replaces the `custom` binding in `src/Core/Maps.fs` with plain `Layout` literals and rewrites `Arenas` in `src/Core/Strings.fs` so names stay in i18n. `layouts` uses `custom` whenever it is non-empty, otherwise it falls back to the procedural `arenas`/`tracks`; pad amounts are emitted as `padRefill`/`healAmount`/`shieldAmount` so tuning still reaches them. Fable's watch picks the files up and recompiles. Clearing `custom` back to `[||]` restores the generated maps.

## Layout

| File | Responsibility |
|------|----------------|
| **Core** (pure .NET, no browser deps) | |
| `src/Core/Vec.fs` | 2D vector struct and helpers |
| `src/Core/Domain.fs` | Tunables (`Cfg`), the keyboard action -> key-code map (`Binds`), input, ship, bullet, pad, world records |
| `src/Core/Strings.fs` | All user-facing strings and locale |
| `src/Core/Sim.fs` | Pure fixed-step simulation: movement, firing, bullets, rams, pads, deaths, match phase |
| **Platform** (browser bindings) | |
| `src/Platform/Input.fs` | Keyboard (through `Domain.Binds`) + Gamepad API → `Input[]` for the four slots |
| `src/Platform/Three.fs` | Minimal Three.js bindings used by the renderer |
| `src/Platform/CrazyGames.fs` | Typed bindings over `CrazyGames.js` — the only door to the SDK |
| `src/Platform/CrazyGames.js` | SDK v2 wrapper: gameplay events, mute, rooms, user, ads, banners, data, leaderboard, relay socket |
| `src/Platform/Sfx.fs` | WebAudio synth: arcade square/noise voices, shimmer delay bus, stereo pan, boost engine drone; music player (`public/music/menu.mp3` loops in menus, `battle1..3.mp3` shuffle in play) |
| **UI** (browser-dependent screens) | |
| `src/UI/Render/RenderTypes.fs` | Render types, constants, shared material helpers |
| `src/UI/Render/RenderMeshes.fs` | Mesh factories (`mkShip`, `mkPad`, `mkArena`...) and `syncArena` |
| `src/UI/Render/RenderFx.fs` | Particle effects: bursts, rings, bolts, beams, flashes |
| `src/UI/Render/RenderEnt.fs` | Per-entity draw functions (ships, pads, crates, bullets, portals, holes) |
| `src/UI/Render/RenderHud.fs` | DOM HUD: panels, kill feed, weapon icons, tags, tint, bloom |
| `src/UI/Render/RenderCam.fs` | Camera framing, intro flyby, resize |
| `src/UI/Render/Render.fs` | `create()`, `draw()` orchestrator, event → effect dispatch |
| `src/UI/Settings.fs` | Settings model: rows for controller slots, swap sticks (`nda-pads`), keyboard bindings (`nda-binds`, including the key-capture state) and `Cfg.tunables` (`nda-tweaks`), plus their persistence |
| `src/UI/Menu.fs` | Lobby, settings, pause and result overlays: join/leave/launch by device, owns the `joined` slot set, renders the phone QR (`src/qr.js`) |
| `src/UI/Pad.fs` | Phone controller page (`pad.html`): lobby card, floating stick, fire/boost zone, menu buttons; talks to the host over the HMR socket |
| **Entry point** | |
| `src/Main.fs` | requestAnimationFrame loop with a 120 Hz accumulator |
| **Test** | |
| `test/Check.fs` | Headless .NET run of the simulation with assertions |
