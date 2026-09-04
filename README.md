# Neon Drift Arena

Local couch brawler for up to 4 ships. Asteroids-style Newtonian drift, recoil-driven blaster, boost pads, last ship standing. F# compiled to JavaScript with Fable 5, rendered with Three.js + bloom.

## Run

```bash
npm install
npm run dev
```

Opens Vite on http://localhost:5173 with Fable in watch mode.

Requires the .NET 10 SDK (Fable 5.15 ships as a net10.0 tool) and Node 18+.

## Controls

| Slot | Source | Turn | Strafe | Thrust | Reverse | Boost | Fire | Special | Rematch |
|------|--------|------|--------|--------|---------|-------|------|---------|---------|
| P1 | Keyboard | A / D or arrows | Q / E | W / Up | S / Down | Shift | Space | F | Enter |
| Any | Gamepad (slot from lobby or SETTINGS) | Right stick X | Left stick X | Left stick up | Left stick down | A / LT | RT | RB | Start |
| Any | Phone (`pad.html`) | Left thumb direction | Strafe slider | Left thumb past half deflection | - | BOOST button | FIRE hex | SPECIAL button | ☰ |

SWAP STICKS in SETTINGS > CONTROLLERS flips the stick roles per pad. `M` mutes everything, music included; SETTINGS > AUDIO sets MUSIC and SOUNDS levels (left/right steps 10%, Fire toggles OFF/100%).

SETTINGS lives in the lobby: a READY player moves down to the SETTINGS entry and confirms. The panel is a single controller-driven list - up/down moves, left/right adjusts, A/Space toggles, B/Esc goes back - covering controller slots, SWAP STICKS, every tunable in `Cfg`, and RESET.

The lobby opens on load. Each device presses Fire or A to claim one of the four slots (explicit slot from SETTINGS, else the first free one); the keyboard holds no slot until it joins, so four gamepads can fill the lobby without it. On your ship card, left/right picks your colour - CYAN / MAGENTA / LIME / AMBER, never two players on the same one - or your side in TEAMS mode, Fire confirms you as READY, and Back (Esc / B) un-confirms, then leaves. Down moves you from your card onto the shared menu row below the slots, where left/right moves the cursor and Up (or Back) returns to your card; the cursor pulses and shows the tags of the players sitting on it, and a card whose player went down dims with a ▼. MODE toggles FREE FOR ALL and TEAMS (which resets everyone to unconfirmed), START launches, SETTINGS opens the panel. A device whose saved slot is already taken joins the first free slot instead. START only lights up with 2+ players, everyone READY, and - in TEAMS - both sides filled; Start (Enter) launches from anywhere once that holds. Teammates share a colour, do not damage each other, and win together. Slots not in the lobby are ignored during the match. Every entry into play starts with a 3-2-1 countdown. Start during play opens the pause menu (RESUME / RESTART MATCH / QUIT TO LOBBY); the result screen offers REMATCH / QUIT TO LOBBY. Menus navigate with stick, d-pad or W/S, confirm with A / Fire / Start, back with B / Esc.

## Play from your phone

`npm run dev` serves the game on the LAN (`--host`). The lobby shows a QR code with the pad URL (`http://<lan-ip>:5173/pad.html`); the pause menu shows it again. A phone that opens it becomes a controller: JOIN, then ◀ ▶ picks the colour (or side in TEAMS), the big button readies up, BACK and START match the gamepad buttons. In play the left half is a floating stick — the ship turns toward the thumb at the keyboard turn rate and thrusts once the thumb is near the rim (`padAimOn` / `padThrustOn` in TUNING) — the middle column shows that player's hull, boost, stocks and kills, and the right panel holds the SPECIAL, BOOST and FIRE hexes (hold them) with a strafe slider underneath; ☰ in the header pauses. Pause and result menus turn the phone into ▲ ▼ OK BACK. A phone silent for 2 s drops out of the lobby. Messages travel over Vite's HMR WebSocket, so the pad only works under the dev server, not a static build.

## Rules

- 3 stocks each, 100 HP. Bullets deal 20 and knock the target back. Ramming exchanges momentum and deals damage proportional to closing speed.
- The blaster runs on heat, not a magazine: every shot adds heat, sustained fire hits 100 and locks the gun for 1.5 s while it vents.
- Below 25 HP a ship smokes and runs 25% slower in every axis - turn, thrust, boost, strafe and top speed.
- Four heal pads sit at the far ends of the two lanes: +40 HP, 30 s respawn, ignored by a ship at full health.
- The blaster is always on RT. Crate weapons are a **special** on RB, fired independently, so you never lose your gun.
- Four weapon crates sit in the arena at all times, one per quadrant. Grabbing one hands out a random loadout - MAG MINES and SEEKERS common (3/9 each), REPULSOR uncommon (2/9), RAILGUN rare (1/9) - and that crate reappears 10 s later on a free spot. Spend the ammo and the special is gone.

| Weapon | Behaviour |
|--------|-----------|
| RAILGUN | Hold RB to charge 1 s, release fires a full-arena beam that pierces ships and asteroids. **One hit kills.** Heavy recoil, and letting go early loses the charge. |
| MAG MINES | Drops a dormant orb behind you. An enemy inside 130 units arms it; it then chases them and detonates 1.5 s later, hurting anyone in the blast - you included. |
| SEEKERS | One homing missile per RB press, not a volley. |
| REPULSOR | No damage: a forward cone of pure knockback. Ring-outs count as your kill. |
- Boost drains at 21/s and only refills from pads: eight ring pads give 26, the centre pad refills fully. Pads respawn 7 s after pickup. All pads sit inside two narrow lanes (a cross through the centre) walled by asteroids.
- Asteroids block bullets and bounce ships; a bump stuns the ship for 0.6 s and sends it spinning.
- Leaving the arena border by more than 60 units destroys the ship.
- Firing recoils the shooter, so the blaster doubles as a reverse thruster.
- Every sim event drives a synthesised voice — no audio files. Browsers keep audio suspended until a key or click, so a gamepad-only session stays silent until someone touches the keyboard or the window.

Every tunable in `Cfg` is a row in SETTINGS > TUNING, adjusted with left/right in steps of a twentieth of the code default and clamped to three times it. Changes save to `localStorage` (`nda-tweaks`) immediately; RESET clears it and reloads with the code defaults. Reverse thrust is `reverseFactor` × thrust.

## Layout

| File | Responsibility |
|------|----------------|
| `src/Vec.fs` | 2D vector struct and helpers |
| `src/Domain.fs` | Tunables (`Cfg`), input, ship, bullet, pad, world records |
| `src/Sim.fs` | Pure fixed-step simulation: movement, firing, bullets, rams, pads, deaths, match phase |
| `src/Input.fs` | Keyboard + Gamepad API → `Input[]` for the four slots |
| `src/Three.fs` | Minimal Three.js bindings used by the renderer |
| `src/Sfx.fs` | WebAudio synth: arcade square/noise voices, shimmer delay bus, stereo pan, boost engine drone; music player (`public/music/menu.mp3` loops in menus, `battle1..3.mp3` shuffle in play) |
| `src/Render.fs` | Scene, ship meshes, asteroids, trails, bursts, ring/beam flashes, bloom spike, HUD shake, dynamic camera, DOM HUD |
| `src/Settings.fs` | Settings model: rows for controller slots, swap sticks (`nda-pads`) and `Cfg.tunables` (`nda-tweaks`), plus their persistence |
| `src/Menu.fs` | Lobby, settings, pause and result overlays: join/leave/launch by device, owns the `joined` slot set, renders the phone QR (`src/qr.js`) |
| `src/Pad.fs` | Phone controller page (`pad.html`): lobby card, floating stick, fire/boost zone, menu buttons; talks to the host over the HMR socket |
| `src/Main.fs` | requestAnimationFrame loop with a 120 Hz accumulator |
| `test/Check.fs` | Headless .NET run of the simulation with assertions |

## Check the physics

```bash
npm run check
```

Runs `Sim` on .NET (no browser) and asserts traverse time, recoil, hits, pads, out-of-bounds, respawn, rams and win condition.
