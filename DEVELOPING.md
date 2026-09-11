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

`npm run build` emits the page, the game and the pad into `dist/`, then `scripts/prune-dist.mjs` drops `models/` (47 MB of `.glb` used only by the `models.html` sandbox, never by the game). `combat.html`, `models.html` and `mapedit.html` are dev pages and are not build inputs. The root `Dockerfile` builds it on Railway (a .NET 10 + Node stage runs Fable and Vite, a Node image serves `dist/`): the `site` service of the `neon-drift-arena` project deploys it from GitHub on every push (https://site-production-a98b.up.railway.app), and `npm run deploy` uploads the working tree for a one-off build. `deploy/server.mjs` serves those files gzipped and carries the phone-pad relay on `/relay`.

## CrazyGames

The SDK v3 script is loaded from `sdk.crazygames.com` in `play/index.html` only — the landing page does not load it. Every call into it goes through `src/Platform/CrazyGames.fs`, which imports `src/Platform/CrazyGames.js` — gameplay start/stop, loading start/stop, the platform mute listener, invite rooms, the user module, video ads and the data module. There are no banner ads and no leaderboard call. Nothing else in the codebase touches `window.CrazyGames`.

`base: "./"` in `vite.config.js` keeps every emitted path relative so the build runs from a CrazyGames subpath; links in the HTML must stay relative for the same reason.

- **Boot order.** `Main.fs` awaits `CrazyGames.init` (which hands `Sfx.setCrazyGamesMuted` the platform mute) before calling `Input.initNetwork ()` and `Menu.initUser ()`, so a room arriving from an invite is known before the socket opens.
- **Rooms.** `Input.initNetwork ()` takes the room from `CrazyGames.getInviteRoom ()`, else `sessionStorage`, else a fresh code, then opens a plain `/relay` WebSocket itself (`Input.fs`, unrelated to the SDK wrapper) and calls `updateRoom`. A join event stores the room and reloads. `CrazyGames.isInstantMultiplayer ()` skips the first-run tutorial overlay so an instant-multiplayer launch drops straight into the already-joinable lobby with nothing to click through. `CrazyGames.leftRoom ()` fires on `pagehide` so `isJoinable` stops claiming a closed tab is still open.
- **Audio.** `Sfx.isMuted ()` is the OR of three flags: the platform mute, the ad mute and the local `M` toggle. The platform mute can never be overridden from in-game.
- **Ads.** The midgame ad runs between the match ending and the result screen; `adPlaying` freezes the sim to a render-only frame and mutes, and `gameplayStop`/`gameplayStart` bracket it. There are no banner ads.
- **Progress and identity.** `Menu.loadProgress` / `saveProgress` keep `nda-high-score` and `nda-matches-played` in the data module; `Menu.initUser` sets the player tag from `getUser` and updates it live through `addAuthListener`. SETTINGS shows a SIGN IN row only while `Settings.isGuest`.

`npm run pack` builds and then writes `dist-game/` through `scripts/pack-crazygames.mjs`: it promotes `dist/play/index.html` to the root (rewriting `../` to `./`, which `assetRoot` in `Sfx.fs` and `RenderMeshes.fs` already expects outside `/play/`) and drops the landing page and its media (`og.jpg`, `trailer.*`, `still-*.jpg`). Zip that folder for the CrazyGames upload.

`npm run measure` then checks that build against the CrazyGames submission caps: `dist-game/` file count and total size, the bytes fetched before the menu is interactive, and the seconds from navigation to the `gameplayStart` call. It serves `dist-game/` with `vite preview`, blocks the real SDK script and injects a stub that timestamps `gameplayStart`, then plays the lobby with keys (tap-to-start, READY, START). It exits non-zero on any cap breach. Measured on 2026-09-11: 24 files, 10.65 MB packed, 4.30 MB initial download, menu interactive at 0.70 s, `gameplayStart` at 2.01 s.

`docs/CrazyGames-v1-plan.md` and `docs/CrazyGames-v2-plan.md` hold the publication plans and their accept/reject criteria.

## Phone pads and LAN mirrors

Under the dev server, pad messages travel over Vite's HMR WebSocket and the QR code points at `http://<lan-ip>:5173/pad.html` (`npm run dev` serves on the LAN with `--host`). On the deployed build there is no HMR, so the host page mints a four-character room code (kept in `sessionStorage`), the QR points at `https://<site>/pad.html#<code>`, and both ends meet on `/relay` in `deploy/server.mjs`: the first connection in a room becomes its host, and every later connection — phone pad or a second desktop — is routed as a peer of that host, never evicting it.

LAN mirror clients (`nda:state`, host-authoritative) ride the Vite dev socket in dev. On the deployed build a second desktop in the same room mirrors the same way over `/relay`: it renders the host's `nda:state` and its own input rides back over the pad channel, seated with its CrazyGames username instead of "Phone". The menu HTML was already deduped by content plus a 1s heartbeat before the WAN work; that work added the `Cfg.netStateMs` (100 ms) send tick on top, capping the rest of the payload that used to go out every render frame. A full-combat `nda:state` payload is estimated at ~7.5-9.3 KB, which puts the sustained stream at an estimated ~75-93 KB/s over the relay - a synthetic estimate from payload size times send rate, not a captured measurement. Losing the host mid-match hands the host seat to the oldest remaining desktop peer, which restarts from the lobby; the match itself is not carried over - see `docs/adr/0001-online-multiplayer-host-loss-and-lobby-size.md`.

The lobby carries a COPY INVITE LINK panel next to the phone QR: it copies `<this page>?room=<code>`, which `CrazyGames.getInviteRoom ()` also reads as a plain query parameter when the SDK has no invite of its own, so a friend on any URL joins the same room. It is there for direct links on `neondriftarena.com`; CrazyGames supplies its own invite button, so set `Cfg.inviteButton` in `src/Core/Domain.fs` to `false` before packing a CrazyGames build.

## Tuning

SETTINGS > TUNING is a developer tool and only appears with `?dev=1` in the URL, mirroring `?desktop=1`; without it players see CONTROLLERS, ARENA and AUDIO only. Behind the flag every tunable in `Cfg` is a row, adjusted with left/right in steps of a twentieth of the code default and clamped to three times it. Changes save to `localStorage` (`nda-tweaks`) immediately; RESET clears it and reloads with the code defaults. Reverse thrust is `reverseFactor` × thrust.

## Map editor

`npm run dev`, then open `/mapedit.html`. It reads the compiled `build/Core/Maps.js` and `build/Core/Strings.js`, so what you see and the names you edit are exactly what the game loads.

Every map is either **FFA** or **RACE** — the same split `Settings.pool ()` uses, and it is simply whether `Layout.Track` is `Some`. The picker shows `index  MODE - NAME`, the name box renames the entry in `Strings.Arenas`, and `+ ffa` / `+ race` append a new map that already satisfies every rule, so you can save it straight away. On an FFA map the track panel and the track-node tool are hidden and the track rules are skipped; on a RACE map they appear.

Drag a box on empty canvas to select everything inside it, ctrl/shift-click to add or remove one, ctrl+A for all. Dragging any selected item moves the whole selection and keeps its relative spacing, the wheel resizes every selected rock, Del removes the lot, and Shift while dragging snaps to 50. Ctrl+C, Ctrl+X and Ctrl+V copy, cut and paste the selection the way any Windows app does: the paste lands centred on the mouse pointer with the group's relative spacing intact, is selected so you can drag it straight away, and passes through the symmetry mode, so pasting one rock with `quad` on drops four. The clipboard survives switching maps and even a discard, which is how you move geometry between maps; track nodes are skipped when the target map is FFA, and pasted nodes are inserted next to the nearest node rather than appended, so the road keeps its order. Ctrl+Z undoes and Ctrl+Y (or Ctrl+Shift+Z) redoes, across sixty steps and every kind of edit - placing, moving, resizing, deleting, transforms, adding or removing whole maps; selection-only clicks are dropped from the history so one undo always reverses one real change. A legend in the top-right corner of the canvas names every marker colour and a shortcut card sits in the bottom-right.

A map can hold one BLACK HOLE. Paint it like anything else, then set its core radius and gravity in the inspector; the canvas draws the event horizon at the core size and a dashed ring where the pull still bites. A map that has one is a `Layout.Hole` of `Some(pos, core, gravity)`, which `Sim.build` seeds into the world with `Life = infinity` - and because `stepHole` only spawns while no hole is live, a map with a fixed hole never gets a wandering one. `State.holeCoreNow ()` and `holeGNow ()` feed the pull, the kill radius and the renderer, falling back to the `Cfg` tunables when the map has none, so the map's own numbers only apply to the map's own hole. There is at most one per map: painting again moves it rather than adding a second, symmetry never mirrors it, and a fold that drops its side removes it.

Spawn points are not map data - `Spawn.spawnPos` computes them, from `diagLimit ()` on an arena and from `road.[0]` on a race map, so the four crosshairs are a readout, not something to drag. What is editable is which node a race starts from: select one track node and MAKE THIS THE START LINE rotates the node list so it becomes `road.[0]`, moving the grid and the starting direction with it. The current start node is ringed in white and labelled `S`. Arena spawns can only move by changing `Spawn.fs`. The paint buttons place rocks, pads, crates and track nodes (a node is inserted after the nearest one). The road is drawn through the same Catmull-Rom `smoothLoop` the sim uses, so the ribbon is the real driveable surface, and the crosshairs are the four spawn points `Sim.spawnPos` will produce.

SYMMETRY is a mode, not a one-shot: pick `⇄`, `⇅`, `quad` or `spin` and its axes stay drawn on the canvas in the toggle's own colour while every edit mirrors as you make it. Placing one rock places all its twins, dragging one drags them, the wheel resizes them together and Del removes the set. Partners are found by position at the moment you grab something, so it works on the maps already in the repo without any per-item bookkeeping - grab one of CORE RING's four corner rocks with `quad` on and the other three follow. An item sitting on an axis has no twin and stays single. `off` returns to editing one item at a time.

TRANSFORM applies to the whole map, track nodes included. Hovering any transform button previews it on the canvas before you commit: its axis is drawn as a dashed line, the ghost outlines show where every item will land, and `fold` shades the half it is about to throw away in red. Each button is tinted with its own axis colour so the button and the line always match - magenta for the vertical axis (`mirror` and `fold` left/right), amber for the horizontal one (`mirror` and `fold` up/down), blue for the `rot 90` centre pivot, green for the two axes and centre that `spin x4` folds around. `mirror` and `rot` move everything, `fold` keeps the positive half and mirrors it back to make the map symmetric, `spin x4` keeps the first quadrant and repeats it four times (the `quad`/`spin` helpers in `Maps.fs`, applied by hand), and `jitter` nudges everything by a random amount up to the box value.

RULES mirrors the per-arena invariants in `test/Check.fs` and re-checks on every edit: rock/pad/crate/spawn clearances at the sim's own margins, rocks overlapping or reaching past `arenaHalf` or `diagLimit ()`, and then per mode — FFA wants exactly one shield core, four heal pads, eight crate spots, an open lane out of the centre and an 8-10 s crossing; RACE wants at least three nodes, `raceHalf` size, a road that stays inside the arena and never doubles back within `trackWidth * 0.9`. Red breaks the sim, yellow is a smell. Saving with red findings asks first. `npm run check` stays the real gate.

NEW MAP at the top of the panel appends a genuinely empty map - no rocks, pads, crates or road - and the rules panel immediately lists everything it still needs. The mode dropdown under the name switches a map between FFA and RACE at any time; switching to RACE resizes it to `raceHalf` and drops in a twelve-node ring if it has no road yet, switching to FFA resizes it back and stops emitting `Track`. DISCARD CHANGES throws away everything unsaved and reloads from the last build, asking first only when there is something to lose. Switching maps while anything is unsaved is blocked by an UNSAVED MAP dialog offering the same two actions, so a new map can never quietly sit in the list without being written; leaving the page while dirty warns too.

SAVE MAP posts to the dev-server-only `/__maps` hook, which replaces the `custom` binding in `src/Core/Maps.fs` with plain `Layout` literals and rewrites `Arenas` in `src/Core/Strings.fs` so names stay in i18n. `layouts` uses `custom` whenever it is non-empty, otherwise it falls back to the procedural `arenas`/`tracks`; pad amounts are emitted as `padRefill`/`healAmount`/`shieldAmount` so tuning still reaches them. Fable's watch picks the files up and recompiles. Clearing `custom` back to `[||]` restores the generated maps.

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
| `src/Platform/CrazyGames.js` | SDK v3 wrapper: gameplay events, mute, rooms, user, video ads, data |
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
