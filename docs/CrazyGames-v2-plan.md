# Neon Drift Arena — CrazyGames v2 Plan (Phase 2)

## Relationship to v1

This is the **next phase** after `CrazyGames-v1-plan.md`. v1 delivers: single-player on-ramp to a bot match, online host-authoritative lockstep over a Railway WebSocket relay, and Basic CrazyGames SDK integration (`gameplayStart/stop`, `muteAudio`, `updateRoom`/`inviteLink`/`addJoinRoomListener`, CG username/avatar tags). Nothing here is part of the v1 work — do not implement these items until v1 has shipped.

## Vision for v2

Turn the v1 online friends-brawler into a monetized, account-integrated game eligible for **Full Launch** on CrazyGames: rewarded/banner ads, cross-device progress, CG-account identity, lower-latency WebRTC transport, and platform leaderboards. Full Launch progression is gated on real-world Basic-Launch metrics (average playtime, conversion to gameplay, retention vs. platform benchmarks).

## Goals

1. **Monetize without hurting the match.** Rewarded ads and banners integrated through the CrazyGames SDK, respecting `muteAudio` and `gameplayStart/stop` (which v1 already emits), and disabled-equivalent behavior outside eligible placements.
2. **Account-linked, cloud-synced progress.** Player progression (wins/series tally, collected loadouts, unlockables) persists via the CrazyGames **Data module** (or APS) and syncs across devices when a guest logs in.
3. **CG identity as the online persona.** Use the `user` module for username/avatar in online rooms (extending the v1 tag seed), with clean guest fallback and no blocking login CTA.
4. **Transport latency pass (optional, post-metrics).** Swap the relay hop for a direct WebRTC data channel where it pays off, without breaking the v1 wire-format.
5. **Platform leaderboards.** Per-mode leaderboards (win streaks / high scores) keyed by CG account, surfaced in the lobby.

## Correctness — what "done" means

| Goal | Correct (accept) | Incorrect (reject) |
|---|---|---|
| Monetization | Rewarded ad triggers `gameplayStop` during the break and `gameplayStart` on resume; banner shows only in non-gameplay screens; syllabic — no ad interrupts mid-match without a natural break; `muteAudio` silences the whole game | Ads fire during active play; `gameplay` events misfire around ad breaks; banner over gameplay |
| Progress save | Progress written via CG Data; a guest's local progress syncs to cloud on login; reload restores state across devices | Progress lost on reload; fabricated sync that drops data; reliance on unguarded local storage |
| CG identity | Online players tagged with CG username/avatar; guests get a non-blocking fallback; auth listener re-tags when a guest logs in mid-game | In-game rename overriding CG identity in online rooms; login shown as a blocking CTA |
| WebRTC drop-in | Same protocol bytes as v1 relay; lower RTT when it works; graceful fallback to relay through TURN/NAT when it doesn't | New wire format; feature-flagged divergence between relay and WebRTC paths that desyncs rooms |
| Leaderboards | Per-mode board backed by CG leaderboard API/SDK; scores tied to CG account; lobby surfacing is optional-driven and clean on guests | Boards misattributed across accounts; guest scores breaking account-keyed integrity |

## Safe rails (do)

- **Keep the v1 architecture intact.** Host-authoritative lockstep, the transport-agnostic wire format, the sacred pure sim (`Sim.step(dt, inputs, world)`), and `npm run check` as the canary all carry forward unchanged.
- **Gate monetization on job events, not game logic.** Ads live only where v1 already emits `gameplayStop/Start`; rewarded placements use natural breaks (respawn, rematch, boost refill).
- **Use the SDK's own modules over homegrown.** Data module / APS for progress, User module for identity, CG Leaderboards API for boards. Respect CG's own consent and terms of service.
- **Implement in the same layering as v1:** CG-specific code stays in Platform/UI; `Core/` remains CG-agnostic.
- **Validate against the CrazyGames QA tool / preview** before every Full-Launch progression gate, same as v1.

## Guardrails (avoid)

- **Do not implement v2 items during v1.** This file is reference for the next phase, not v1 scope.
- **Do not regress the v1 ships.** Monetization and account work must not break local couch play, phone pads, bots, or the bot-match on-ramp.
- **Do not make login or ads mandatory to play.** Guests play unimpeded; ads appear at natural breaks, never as a soft wall.
- **Do not store or trust the client-side `__dangerousUserId`; do not decrypt `getUserToken()` on the client.** Identity flows that need server truth go through verified token exchange.
- **Do not hand-roll a TURN/ICE stack in-game.** If WebRTC happens, the Railway relay hosts signalling + TURN fallback; the game just swaps the socket object.
- **Do not let leaderboard or progress features ship half-integrated** (e.g. score posting that silently drops for guests) — that's a correct-to-reject purity break.
- **Do not over-monetize between friends.** The couch-brawler spirit and its "local first" identity stay the product north star.

## Expected output

When this plan is executed, the game is **Full-Launch eligible** on CrazyGames: monetized through natural breaks, syncable progress across devices, CG-account identity in online rooms, optional lower-latency transport, and per-mode leaderboards — all layered on the unchanged v1 deterministic core, with local couch play, bots, phone pads and `npm run check` still green.

The executing model is free to choose engines, layouts and details within these rails; the correctness table is the gate.