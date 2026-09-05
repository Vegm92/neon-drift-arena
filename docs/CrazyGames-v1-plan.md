# Neon Drift Arena — CrazyGames v1 Publication Plan

## Vision

Take Neon Drift Arena from a local couch brawler (4 ships on one machine) to a **publishable CrazyGames web game** where players on different machines can fight in the same 4-ship arena, invite friends through CrazyGames' own UI, and a lone player can land directly into a bot-filled match. The core 120 Hz deterministic simulation becomes the single source of truth; the network layer is a thin transport around it, not a rewiring of the game.

The target is **Basic Launch** (sole player + bots works out of the box, online friends-battles work, CrazyGames QA can play it end-to-end), with the architecture leaving a clean path to **Full Launch** (monetization, account/progress integration, WebRTC).

## Goals

1. **A single player is immediately playable.** Landing on the page must lead to a bot-match with zero setup friction — ADD BOT becomes the on-ramp, not an obscure option.
2. **Online friends can play together (host-authoritative lockstep).** A room creator steps the sim; joined players send inputs; the CrazyGames invite/join flow routes players into a room.
3. **The CrazyGames SDK is integrated enough for Basic Launch and unblocks Full Launch:** `gameplayStart/stop`, `muteAudio` settings listener, `updateRoom`/`inviteLink`/`addJoinRoomListener` (CG friend invites), and `user` module for CG username/avatar as the online player tag.
4. **Production static build is verified** against CrazyGames' tech requirements (bundle size, relative paths, sitelock whitelist, mobile/DPR readability, touch/gesture CSS).
5. **The sim stays a pure, deterministic core.** Networked and local play share the same `Sim.step`. Nothing about the network touches the physics.

## Correctness — what "done" means

Each goal has an explicit test:

| Goal | Correct (accept) | Incorrect (reject) |
|---|---|---|
| Solo play | A fresh load goes from first frame → lobby → bot match in ≤1 visible action; ✓ `npm run check` still passes; single player can reach gameplay without a second human | Player stalls in lobby with no clear path to a match; bots required to fill more slots than asked |
| Online lockstep | 2–4 players on **different machines** share a room; identical sim state over N seconds for same inputs+seed (verified by a state-hash check in an internal test); a slowpoke client does not desync or freeze the match | Any divergence of sim state between clients (positions, HP, spawns) that isn't purely cosmetic/interpolated; a client that drops and rejoins breaks the room |
| SDK Basic | `gameplayStart` fires at match launch, `gameplayStop` on pause/end; `muteAudio` setting actually silences the game and cannot be overridden by the in-game mute; CG invite link routes a joiner into the host's room with correct `inviteParams`/`roomId` | Events misfire/never fire (Basic QA flags "no Gameplay start" → download-size mis-measure); audio plays when CG says mute; invite link lands a player in the wrong room or the lobby |
| CG username/avatar | Online players are tagged with their CG profile (fallback to guest name); works when user is logged in and when guest | In-game rename overrides CG identity for online rooms; login becomes a blocking CTA |
| Static prod build | `dist/` loads over HTTP (no dev server); all paths relative; ≤50 MB total / ≤20 MB initial; no console errors; pad/second-player paths either work or are cleanly gated | Any absolute path, ≥limit bundle, crash in a static host, phone-pad path silently broken without explanation |

## Safe rails (what to do)

- **Treat the sim as sacred.** `Sim.step(dt, inputs, world)` stays pure and browser-free. All network logic lives in a new layer (Platform/Net) that feeds `Input[]` in and consumes `World` out. `npm run check` must keep passing — it's the canary.
- **Reuse the phone-pad as the template.** The existing `nda:pad`→`Input` path already proves remote `Input` delivery. Generalize it; don't reinvent the input contract.
- **Send the seed and layout, not state.** Lockstep works because same seed + same `Input[]` = same world. The host broadcasts seed/arena/settings once; clients only exchange inputs + a periodic state hash. This keeps bandwidth ~zero and is the crux of the whole design.
- **Host steps, relay forwards, clients render.** The room creator is authoritative. The Railway relay (Node/WS or Colyseus) is a dumb pipe for inputs and the state-hash heartbeat.
- **Feed inputs per-step, not per-frame.** The current `masked()` samples once per frame and reuses across steps; network lockstep needs the input array at the physics cadence (120 Hz). This is the one real refactor inside Main — do it cleanly behind the Net layer.
- **Integrate the SDK incrementally and behind a guard.** Load the CG SDK script, and the whole CG layer only when `window.CrazyGames` exists (it will only on CG; locally it must be inert). Never let CG calls break local dev or offline play.
- **Verify the production build on a real static host** (e.g. Railway static deploy or a local `dist` served over HTTP) before submitting — this catches sitelock, relative-path and bundle-size failures the dev server hides.
- **Use your Railway MCP** to stand up the realtime service and set env vars; keep credentials out of the repo.

## Guardrails (what to avoid)

- **Do not put gameplay logic in the server.** The relay must not compute the sim; that's a server-authoritative side-road (option ii) we explicitly set aside. Keep the server dumb to keep it reliable and cheap.
- **Do not rewrite the renderer or Domain/Cfg.** They already consume the host's world correctly. Networking is additive, not invasive.
- **Do not hand-roll WebRTC now.** NAT/TURN/ICE is a rabbit hole that stalls v1. Keep the relay transport; document WebRTC as a post-v1 drop-in (the message wire-format is transport-agnostic). If it lands in the plan, it's a labeled later phase, never scope creep on v1.
- **Do not remove or replace the local couch experience.** Local multiplayer, phone pads, and the existing lobby stay fully functional — the game is a local brawler first, online as an extra layer. Regression here = rejection.
- **Do not let CG-specific code leak into `Core/`.** User-facing strings, locale, and the sim all stay CG-agnostic; CG awareness lives only in Platform/UI layers, matching the existing layering.
- **Do not gate the whole game behind CG login or SDK presence.** Guests must play unimpeded (CrazyGames requirement), and the game must run standalone if the SDK never loads.
- **Do not chase Full-Launch features (ads, IAP, progress save, leaderboards) in v1.** Mission is Basic Launch + online friends. Full-Launch items get documented, not implemented now.

## Expected output

When this plan is executed, the delivered state is:

- A **CrazyGames-ready production build** (`dist/`) that a lone visitor loads into a bot-filled match, and that verified passes the tech checks (size, relative paths, sitelock, mobile/DPR readability).
- **Online host-authoritative lockstep:** a room creator + the CrazyGames invite/join flow bring 2–4 players from different machines into the same deterministic match; a static-host-compatible transport relay runs on Railway; `npm run check` and sim determinism stay green.
- **Basic CrazyGames SDK integration** that frees you to submit: `gameplayStart/stop`, `muteAudio`, `updateRoom`/`inviteLink`/`addJoinRoomListener`, and CG username/avatar tags for online players.
- **The local couch game intact and playable**, plus a documented Full-Launch roadmap (ads, progress save, WebRTC) as the next phase.

The executing model is free to choose engines, file layout, and implementation details within these rails — the correctness tests above are the gate it must pass.

Phase 2 (Full Launch, monetization, WebRTC, leaderboards, solo campaign) is tracked separately in `CrazyGames-v2-plan.md`. It is **out of scope** for this v1 work.