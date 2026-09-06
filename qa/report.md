# UX/UI sweep — 2026-09-06 UTC

Scripted run of `scripts/qa-playwright-sweep.mjs` against a clean build of `main` (8987a12) served by Vite on port 4173. Headless Chromium 1228, 1920×1080 plus 1366×768 passes, Pixel 7 emulation for the phone screens. 96 screenshots in `qa/screenshots/`, raw machine notes in `qa/sweep.md`. Console: zero errors or warnings on every page.

Rerun: `npx vite --port 4173 --host` (from a built tree) then `QA_BASE_URL=http://localhost:4173 node scripts/qa-playwright-sweep.mjs`.

## Blockers

| # | Finding | Evidence | Where | Fix |
|---|---------|----------|-------|-----|
| B1 | **Changing MODE with bots in the lobby makes START unreachable.** Every bot flips to unready and nothing can ready a bot again, so the note reads ALL PLAYERS MUST READY UP forever. Only CLEAR BOTS + ADD BOT recovers. Reproduced on FFA→TEAMS→…→FFA and on RACE. | `09-lobby-mode-teams.jpg`, `11-lobby-mode-race.jpg`, `12-lobby-mode-freeforall.jpg` (bot cards show ◀ colour ▶ instead of READY, START dim); sweep notes `ffa-launch`, `race-launch` | `src/UI/Menu.fs:165` `applyMode` → `Array.fill ready 0 4 false` | `for s in joined do ready.[s] <- isBot s` |
| B2 | **Tutorial TURN row shows two empty key caps.** First-run overlay lists thrust as W/S but the turn line is two blank boxes. | `03-tutorial-first-run.jpg` | `src/Main.fs:55` `keys "" "" "SPACE" …` | Pass `"A" "D"` (or `"Q" "D"` for Azerty, same branch as `movement`) |

## UX friction

| # | Finding | Evidence | Where | Fix |
|---|---------|----------|-------|-----|
| U1 | **Mode bar lies in RACE and PRACTICE.** Header strip still says STOCK BATTLE · 3 LIVES · LAST SHIP FLYING WINS in every mode. | `10-lobby-mode-practice.jpg`, `11-lobby-mode-race.jpg` | `src/UI/Menu.fs:334` `mode = Strings.t.Mode` | Pick the list per mode (add `RaceMode`/`PracticeMode` lists to `Strings.fs`) |
| U2 | **Wormhole pairs are indistinguishable.** Two pairs were live at once (natural timer + forced one) and four identical violet rings sat in the arena, two of them adjacent; nothing tells you which ring exits where. | `35-wormhole-close.jpg` | `src/UI/Render/RenderEnt.fs` portal draw | Colour or number each pair, or forbid a second pair while one is open |
| U3 | **Kill flash washes the whole screen for ~0.5 s.** Every takedown sets a full-viewport vignette in the victim's colour (0.75 opacity, decays at 1.6/s) plus a bloom spike; the arena reads as a solid magenta/amber wash and other ships vanish for the duration. Also the main photosensitivity item (F1). | `28-kill-fx-f0.jpg`, `29-kill-fx-f1.jpg`, `79-target-kill-fx-f0.jpg`, `80-target-kill-fx-f1.jpg` | `src/UI/Render/Render.fs:151` `vw.Tint <- 0.75`; `src/UI/Render/RenderHud.fs:100` inset box-shadow 220px | Drop Tint to ~0.35 and shrink the shadow spread, or only tint when *your* ship dies |
| U4 | **Rename uses `window.prompt`.** Blocks the render loop and is unusable from a pad. Already being replaced by an inline input in the uncommitted `Menu.fs` diff in the working tree. | code only | `src/UI/Menu.fs:270` | Land the in-progress inline rename |
| U5 | **SETTINGS › TUNING shows raw code identifiers** (`turnRate`, `holeG 9.00e+6`, `padAimOn`). 50+ rows a player has no context for, between AUDIO and SIGN IN. | `19-settings-tuning-mid.jpg`, `20-settings-bottom.jpg` | `src/UI/Settings.fs:99` `Tune k` rows | Hide TUNING behind a dev flag (`?dev=1` / localStorage) and keep CONTROLLERS · ARENA · AUDIO for players |
| U6 | **Settings scroll has no "above" cue and a fixed 11-row window.** ▼ MORE BELOW appears but nothing says there are rows above once you scroll; the list is a 540 px column at 12 px on a 1080p screen. | `18-settings-arena-audio.jpg`, `20-settings-bottom.jpg`, `21-settings-1366.jpg` | `src/UI/Menu.fs:407` `renderOptions`, `play/index.html` `#menu .rows{width:540px;font-size:12px}` | Add a ▲ hint when `top > 0`; scale the column with `clamp()` like the lobby |
| U7 | **Railgun fires by itself at full charge**, README says "release fires". Either behaviour or doc is wrong; the sweep caught the target already dead before F was released. | `62-weapon-1-release-f0.jpg` shows FIRST BLOOD before key-up | `src/Core/Sim.fs:542` `if c >= railCharge then` | Decide: fire-on-release (charge is a commitment) or update README/Strings |
| U8 | **Ships are ~15–20 px tall at gameplay distance.** The camera frames the whole 1350-unit arena, so on 1080p a ship is smaller than its own name tag. Readable, but every FX finding below is judged from close-ups the player never gets. | `25-hud-early.jpg`, `92-hud-1366.jpg` | `src/UI/Render/RenderCam.fs` `maxCamH`/`minCamH` | Known design choice; consider a tighter `minCamH` when ≤2 ships are alive |

## Visual polish

| # | Finding | Evidence | Where | Fix |
|---|---------|----------|-------|-----|
| V1 | **Black-hole horizon tumbles edge-on instead of spinning in place.** `flat` bakes a −90° X rotation into the geometry, then `rotation.z` is applied to the mesh, so the ring tilts through the floor; in one frame it is a thin vertical crescent, in the next a dull disc. The README's "bright violet horizon" is never on screen (close-up mean luma ≈5/255). | `38-black-hole-close.jpg` vs `39-black-hole-pull-arc.jpg` | `src/UI/Render/RenderEnt.fs:232` `vw.Horizon.rotation.z`, `:234` `vw.Halo.rotation.z`; geometry via `RenderTypes.fs:33` | Rotate about `y` (the geometry's normal after `flat`), raise horizon opacity floor |
| V2 | **Lobby card name collides with the stock-pip accent.** `User1` already touches the five boxes; a 16-char rename will sit under them. | `04-lobby-initial.jpg`, `07-lobby-full-away.jpg` | `play/index.html` `#menu .lobby .slot::after{right:10px;width:118px}` and `.slot b` | Pad the name (`max-width:calc(100% - 140px)` + ellipsis) or move the accent above the name |
| V3 | **Name tags draw over the corner HUD panels during the intro fly-by.** BOT tag sits on top of the bottom-left panel bars. | `22-intro-flyby-early.jpg`, `53-race-intro-gates.jpg` (all four tags stacked on the grid) | `src/UI/Render/RenderHud.fs:106` `drawTags` | Skip tags whose projected point falls inside a `.panel` rect, or hide tags while `Intro > 0` |
| V4 | **Launcher chevron is a ~15 px cyan arc at gameplay distance**; the thrown rock reads as an orange spark trail, no solid body visible at 700-unit close-up either. | `46-launcher-chevron.jpg`, `47-launcher-rock.jpg`, `48-launcher-rock-wide.jpg` | `src/UI/Render/RenderEnt.fs` launcher/rock draw | Bigger chevron (scale with camera height), give the rock a filled core |
| V5 | **Low-HP "smoke" is a spray of bright white squares** — reads as sparks, not smoke, and is brighter than the ship. | `33-low-hp-close.jpg` | `src/UI/Render/RenderFx.fs` `spawnSmoke` | Grey, larger, lower-alpha puffs |
| V6 | **Result-screen column headers are 9 px** (KILLS · ACCURACY · CRATES · RINGS · STOCKS) on 1080p and 768p alike. Landing SOON chips are 8 px. | `50-result-screen.jpg`, `51-result-screen-1366.jpg`, `02-landing-full.jpg` | `play/index.html` `#menu .stats .hdr{font-size:9px}`; `index.html` `.store i{font-size:8px}` | 11 px minimum |
| V7 | **Danger arc is fine; guide arc in RACE not verified** — the violet "next portal" arc never showed in the race frames because a real threat (grid neighbours) always won. | `54-race-hud.jpg` | `src/UI/Render/RenderEnt.fs:130` | No action; note for a manual race lap |

## Flash-safety pass (photosensitivity)

Method: freeze the loop, step the sim 16 ms at a time, decode each PNG, sample every 4th pixel. "changed" = share of samples whose luma moved >25/255 (≈10 %) between consecutive frames; WCAG 2.3.1 flags >3 such flashes per second covering >25 % of the viewport.

| Trigger | Frames | Mean luma (0–255) | Changed area per step | Verdict |
|---------|--------|-------------------|-----------------------|---------|
| Takedown (wide camera, FFA) | `28`–`30` | 15→35 (final run; 11→21 in a run that caught it a frame late) | **19–38 %** on the first step, then ~3 % | One flash per kill, ~0.5 s decay. Under the 3 Hz limit unless two kills land within a second (DOUBLE KILL does exactly that). Full-viewport tint is the driver — see U3. |
| Takedown (close-up) | `79`–`81` | 7→40 | **47 %** first step | Same event, magnified by proximity; white flash disc + two rings + tint |
| GO shout | `24` | white vignette 0.9 + bloom spike | not stepped | Single flash at match start, followed 5 s later by nothing. Acceptable. |
| Black-hole horizon | `38` | 4.7→3.5 | 1.6–2 % | No flashing; opacity pulses 0.7–1.0 at 7 rad/s (1.1 Hz) on a dim ring. Fine. |
| Railgun beam (close-up) | `62`–`64` | 11→41→49 | **27 % then 21 %** over two steps, then ~2 % | Beam plus the one-hit-kill tint land in back-to-back frames; same single-flash profile as a takedown because the rail kill *is* a takedown. Not re-examined further on request. |
| Tractor charge/latch | `74`–`77` | 10.3 flat | 0.1 % | No flash. |
| Kill-feed line | `31` | CSS `slam` brightness(3) for 0.22 s | ~40 px strip | Negligible area. |
| Continuous blinkers | CSS | `.panel.hurt .name` 1.4 Hz, `.wep .ready` 2 Hz, `#clock.sudden` 1.4 Hz | text-sized | Under 3 Hz, tiny area. Fine. |

Risk items, in order: (F1) kill tint area — reduce or gate behind a "reduce flashes" toggle; (F2) DOUBLE KILL = two full-screen flashes inside a second; nothing else approached the threshold.

## Verified OK (no action)

Landing hero video autoplays muted with poster; PLAY FREE → `/play/`; all three store chips render dimmed SOON; OG/description/twitter meta present; no `{{key}}` leaks anywhere. Mobile emulation flips the CTA to DESKTOP ONLY and `/play/` shows the gate with BACK TO THE SITE / LET ME IN ANYWAY (`94`–`96`). Lobby join → ready → ADD BOT ×2 → away ▼ card all behave (`04`–`08`); TEAMS pairs sides correctly (`09`); ARENA cycles five arenas + RANDOM in FFA and GRAND PRIX/HAIRPIN/INFINITY/RANDOM in RACE; MUTATOR cycles NONE/RAILS ONLY/TURBO/ICE. Lobby and pause QR overlays open full-screen with the LAN URL (`16`, `41`). Countdown digits, GO, kill feed with weapon icon, danger arc (`26`, `27`), wormhole warp A→B with exit ring (`36`), sudden-death clock/border/shout (`43`, `44`), pause menu layout (`40`), resume countdown (`42`), victory cam (`49`), result tally ★ and awards (`50`), RACE HUD `4TH · LAP 1/3` + portal glow + yellow finish (`54`–`56`), practice range + all six specials arm and fire (`58`–`77`), phone pad JOIN → colour card → READY → play layout → pause pad (`85`–`93`), 1366×768 lobby/settings/result/HUD (`08`, `21`, `51`, `92`).

## Harness notes / false positives

- `domCheck` "body exceeds viewport" on the landing page is just page scroll, not a bug.
- The result screen shows a 0-stock BOT as winner because the sweep forces ship states to end the match; ignore the empty STOCKS column there.
- `User1` as P1's name comes from the CrazyGames SDK running in `local` env on localhost; production off-CrazyGames shows `P1`.
- Runs 1–2 measured railgun/tractor frames with the wide camera after frame 0 (fixed in the script); run 3 missed the beam because of U7, so the script now samples mid-charge (49 frames in) instead of on release.
- The rail beam and tractor latch were never screenshotted at gameplay distance; at that scale they are a few pixels wide.

Not covered, by design: Firefox/Safari, LAN mirror second-PC flow, CrazyGames ad/invite states.
