# Neon Drift Arena — CrazyGames v1 Publication Plan (Corrected & Completed)

## Vision

Take Neon Drift Arena from a local couch brawler (4 ships on one machine) to a **publishable CrazyGames web game** where players on different machines can fight in the same 4-ship arena, invite friends through CrazyGames' own UI, and a lone player can land directly into a bot-filled match. The core 120 Hz deterministic simulation remains the primary source of truth, enhanced by a resilient state-reconciliation layer to combat floating-point divergence and network jitter across varying browser engines.

The target is **Basic Launch** (sole player + bots works out of the box, online friends-battles work, CrazyGames QA can play it end-to-end), with the architecture leaving a clean path to **Full Launch** (monetization, account/progress integration, WebRTC).

## Goals

1. **A single player is immediately playable.** Landing on the page must lead to a bot-match with zero setup friction — ADD BOT becomes the on-ramp, not an obscure option.
2. **Online friends can play together with robust, host-authoritative reconciliation.** A room creator steps the sim; joined players send inputs with sub-frame tick markers. Because pure client-side lockstep suffers from floating-point non-determinism across different browser engines, the host periodically broadcasts state-snapshots to keep clients hard-synchronized.
3. **The CrazyGames SDK is integrated enough for Basic Launch and unblocks Full Launch:** `gameplayStart/stop`, `muteAudio` settings listener, `updateRoom`/`inviteLink`/`addJoinRoomListener` (CG friend invites), and `user` module for CG username/avatar as the online player tag.
4. **Production static build is verified** against CrazyGames' tech requirements (bundle size, relative paths, sitelock whitelist, mobile/DPR readability, touch/gesture CSS).
5. **The sim stays a pure, deterministic core.** Networked and local play share the same `Sim.step`. Nothing about the network touches the physics directly.

## Correctness — what "done" means

Each goal has an explicit test:

| Goal | Correct (accept) | Incorrect (reject) |
|---|---|---|
| Solo play | A fresh load goes from first frame → lobby → bot match in ≤1 visible action; ✓ `npm run check` still passes; single player can reach gameplay without a second human | Player stalls in lobby with no clear path to a match; bots required to fill more slots than asked |
| Online sync | 2–4 players on **different browsers/machines** (e.g., Safari on iOS and Chrome on Windows) share a room; identical sim state is maintained; any minor desync (due to float math or packet loss) is smoothly corrected via host state snapshots; a slowpoke client does not freeze the match or permanently diverge | Any permanent divergence of sim state between clients (positions, HP, spawns) that remains uncorrected; a client that drops and rejoins cannot reclaim their slot or breaks the room |
| SDK Basic | `gameplayStart` fires at match launch, `gameplayStop` on pause/end; `muteAudio` setting actually silences the game (managed via strict tiered priority in `Sfx.fs`) and cannot be overridden by the in-game mute; CG invite link routes a joiner into the host's room with correct `inviteParams`/`roomId` | Events misfire/never fire; audio plays when CG says mute; invite link lands a player in the wrong room, an empty room due to SDK load race conditions, or the lobby |
| CG username/avatar | Online players are tagged with their CG profile (fallback to guest name); works when user is logged in and when guest | In-game rename overrides CG identity for online rooms; login becomes a blocking CTA |
| Static prod build | `dist/` loads over HTTP (no dev server); all paths relative; ≤50 MB total / ≤20 MB initial; no console errors; pad/second-player paths either work or are cleanly gated | Any absolute path, ≥limit bundle, crash in a static host, phone-pad path silently broken without explanation |

## Safe rails (what to do)

- **Treat the sim as sacred but implement a State-Reconciliation Backup.** Keep `Sim.step(dt, inputs, world)` browser-free. While lockstep uses inputs and seeds, implement a lossy state-sync backup: the host broadcasts compressed `World` state-snapshots (using existing `sendState`/`worldOf` mechanics) twice a second. If a client's local state-hash differs, hard-snap the client's local state to the host's snapshot to solve browser floating-point discrepancies.
- **Implement Input Buffering & Tick Cadence mapping.** Clients must buffer inputs locally (2–3 frames, ~20ms) to absorb WebSocket jitter/TCP Head-of-Line blocking. Input packets sent over WS must be serialized with sub-frame tick indexes to feed the 120 Hz physics simulation cleanly, preventing input clobbering on high/low refresh rate screens.
- **Defer WebSocket connection for CrazyGames SDK initialization.** Avoid race conditions where the game connects to a fresh socket room before the asynchronous CG SDK has fetched deep-linked room/invite parameters. Defer `connect()` until CG SDK initialization completes and explicitly checks `getInviteParameters()`.
- **Use Persistent Client Identities.** Replace the random temporary `remoteId` with a persistent ID stored in `localStorage` (or retrieved from the CG User SDK). This allows players who experience disconnections or reloads to reconnect and automatically reclaim their original slot and ship.
- **Implement Tiered Sound Muting in `Sfx.fs`.** Create a clear hierarchy of muting:
  ```fsharp
  let mutable private crazyGamesMuted = false
  let mutable private localMuted = false
  let isMuted () = crazyGamesMuted || localMuted
  ```
  Ensure the global CrazyGames mute state cannot be overridden by user interactions with the in-game mute button.
- **Gate Room Capacity.** The host must actively reject incoming `"nda:pad"` associations once the maximum room limit (4 players) is reached, and prevent extraneous devices from taking over slots.
- **Reuse the phone-pad as the template.** The existing `nda:pad`→`Input` path already proves remote `Input` delivery. Generalize it; don't reinvent the input contract.
- **Verify the production build on a real static host** (e.g. Railway static deploy or a local `dist` served over HTTP) before submitting — this catches sitelock, relative-path and bundle-size failures the dev server hides.
- **Use your Railway MCP** to stand up the realtime service and set env vars; keep credentials out of the repo.

## Guardrails (what to avoid)

- **Do not use "Strict Input-Only Lockstep" without State Snaps.** Do not assume that JS engines are deterministic across devices. Pure input-only synchronization *will* lead to game-breaking desynchronization between browsers (e.g., Safari vs. Chrome).
- **Do not connect WebSocket on module load.** Do not initiate the loop or room creation before checking CrazyGames invite parameters asynchronously, or deep links will silently fail.
- **Do not put gameplay logic in the server.** The relay must not compute the sim; that's a server-authoritative side-road we explicitly set aside. Keep the server dumb to keep it reliable and cheap.
- **Do not rewrite the renderer or Domain/Cfg.** They already consume the host's world correctly. Networking is additive, not invasive.
- **Do not hand-roll WebRTC now.** NAT/TURN/ICE is a rabbit hole that stalls v1. Keep the relay transport; WebRTC can be a post-v1 drop-in.
- **Do not remove or replace the local couch experience.** Local multiplayer, phone pads, and the existing lobby stay fully functional — the game is a local brawler first, online as an extra layer. Regression here = rejection.
- **Do not let CG-specific code leak into `Core/`.** User-facing strings, locale, and the sim all stay CG-agnostic; CG awareness lives only in Platform/UI layers, matching the existing layering.
- **Do not gate the whole game behind CG login or SDK presence.** Guests must play unimpeded, and the game must run standalone if the SDK never loads.
- **Do not chase Full-Launch features (ads, IAP, progress save, leaderboards) in v1.** Mission is Basic Launch + online friends. Full-Launch items get documented, not implemented now.

## Expected output

When this plan is executed, the delivered state is:

- A **CrazyGames-ready production build** (`dist/`) that a lone visitor loads into a bot-filled match, and that verified passes the tech checks (size, relative paths, sitelock, mobile/DPR readability).
- **Online host-authoritative synchronized multiplayer:** a room creator + the CrazyGames invite/join flow bring 2–4 players from different machines into the same synchronized match; a static-host-compatible transport relay runs on Railway; desync-recovery mechanisms are fully functional; `npm run check` and sim integrity stay green.
- **Basic CrazyGames SDK integration** that frees you to submit: `gameplayStart/stop`, `muteAudio`, `updateRoom`/`inviteLink`/`addJoinRoomListener`, and CG username/avatar tags for online players.
- **The local couch game intact and playable**, plus a documented Full-Launch roadmap (ads, progress save, WebRTC) as the next phase.

Phase 2 (Full Launch, monetization, WebRTC, leaderboards, solo campaign) is tracked separately in `CrazyGames-v2-plan.md`. It is **out of scope** for this v1 work.
