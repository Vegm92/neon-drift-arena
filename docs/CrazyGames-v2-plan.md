# Neon Drift Arena — CrazyGames v2 Publication Plan (Corrected & Completed)

## Relationship to v1

This is the **next phase** after `CrazyGames-v1-plan.md`. v1 delivers: single-player on-ramp to a bot match, online host-authoritative lockstep over a Railway WebSocket relay, and Basic CrazyGames SDK integration (`gameplayStart/stop`, `muteAudio` settings listener, `updateRoom`/`inviteLink`/`addJoinRoomListener`, CG username/avatar tags). Nothing here is part of the v1 work — do not implement these items until v1 has shipped and Basic Launch is live and verified.

## Vision for v2

Turn the v1 online friends-brawler into a fully monetized, account-integrated game eligible for **Full Launch** on CrazyGames: rewarded/banner ads, cross-device progress, CG-account identity, lower-latency WebRTC transport, and a secure platform leaderboard. Full Launch progression is gated on real-world Basic-Launch metrics (average playtime, conversion to gameplay, retention vs. platform benchmarks).

## Goals

1. **Monetize without hurting the match.** Video ads (midgame/rewarded) are requested via the `SDK.ad.requestAd` module with robust callback handling (`adStarted`, `adFinished`, `adError`) that pauses game simulations and silences audio. Banner ads are shown in non-gameplay screens and **explicitly cleared** via `SDK.banner.clearAllBanners()` before active gameplay begins to prevent layout overlays.
2. **Account-linked, cloud-synced progress.** Player progression (wins/series tally, collected loadouts, unlockables) persists via the CrazyGames SDK v2 **Data module** (`window.CrazyGames.SDK.data`). A guest's local progress is seamlessly migrated and synchronized with their CrazyGames cloud profile when they log in.
3. **CG identity as the online persona.** Use the `user` module (`isUserAccountAvailable`, `getUser`) for username/avatar in online rooms, with clean guest fallback and no blocking login CTA. Real-time auth listeners dynamically update names and profile details mid-game if a user signs in.
4. **Transport latency pass (optional, post-metrics).** Swap the WebSocket relay hop for a direct WebRTC data channel where it pays off, without breaking the v1 wire-format, and with graceful, automatic fallback to the WebSocket relay via TURN.
5. **Secure Platform Leaderboard.** Integrate a single, highly secure global leaderboard (e.g., Win Streaks or High Scores) using the CrazyGames SDK v2 `leaderboard` module. Scores are encrypted on the client using AES-GCM via the SDK's encryption helpers (`encryptScore`) to prevent tampering and ensure platform compliance.

## Correctness — what "done" means

| Goal | Correct (accept) | Incorrect (reject) |
|---|---|---|
| **Monetization** | Rewarded/Midgame ads hook into `adStarted` to mute audio, pause the physics sim, and call `gameplayStop()`; hooks into `adFinished`/`adError` to unmute, resume sim, and call `gameplayStart()`; banners are displayed in menus and **cleared completely** via `clearAllBanners()` when entering gameplay; banners respect the 30-second refresh and max 2-per-screen limits | Ads fire during active play; `gameplay` events misfire around ad breaks; banner remains on-screen or overlaps gameplay; game freezes or soft-locks if an ad fails to load or is blocked (`adError` is unhandled) |
| **Progress save** | Progress written/read via `SDK.data.setItem`/`getItem`; local guest progress is merged/uploaded to the cloud when the auth listener triggers login; state is restored across devices upon reload | Progress is lost on page reload; fabricated sync drops local guest progress on account link; reliance on raw local storage for authenticated users |
| **CG identity** | Online players are tagged with CG username/avatar; fallback is handled gracefully for guests; `addAuthListener` instantly updates the local player profile and broadcasts the update to the host when a guest logs in mid-room | In-game custom name input overrides CG identity in online rooms; login is shown as a blocking modal that gates gameplay |
| **WebRTC drop-in** | Direct WebRTC data channel uses the exact same protocol bytes and routing logic as the v1 WebSocket relay; automatically falls back to WebSocket relay through a Railway-hosted TURN/NAT gateway if WebRTC handshake fails | WebRTC introduces a divergent wire format; feature-flagged divergence between paths causes room desynchronization; WebRTC failures leave players disconnected with no fallback |
| **Leaderboard** | A single global platform leaderboard is configured in the CG Developer Portal; scores submitted via `SDK.leaderboard.submitScore` are **fully encrypted** using AES-GCM and the portal-provided encryption key; guests are cleanly excluded or handled without breaking account-keyed integrity | Attempting to submit raw, unencrypted scores (causes API rejection); attempting to create multiple platform leaderboards (violates SDK v2 limits); guest scores cause application crashes |

## Safe rails (do)

- **Keep the v1 architecture intact.** The host-authoritative lockstep state-reconciliation, transport-agnostic wire format, the sacred pure physics simulation (`Sim.step`), and `npm run check` as the canary must remain unchanged and functional.
- **Implement proper banner ad lifecycle cleanup.** Always call `window.CrazyGames.SDK.banner.clearAllBanners()` or `clearBanner(containerId)` on any transition out of the lobby/menus and into active gameplay.
- **Handle all video ad callbacks carefully.** You must implement `adStarted`, `adFinished`, and `adError` for every ad request. If an ad fails or is blocked, the `adError` callback must gracefully resume the game and ensure the player is not blocked from playing.
- **Use the SDK's own modules over homegrown.** Standardize on the SDK v2 `data` module for player progress, the `user` module for authentication, and the `leaderboard` module for high scores. Discard legacy SDK v1 concepts like "APS" (Automatic Progress Saving).
- **Encrypt leaderboard scores.** Use the SDK's AES-GCM score encryption utilities (`encryptScore`) with the game's unique encryption key before calling `submitScore`. 
- **Migrate Guest State on Login.** Register a listener via `window.CrazyGames.SDK.user.addAuthListener`. When a user transitions from Guest to Logged-in:
  1. Retrieve local-only progress from `localStorage` or `SDK.data` (guest partition).
  2. Write/merge it to their CrazyGames authenticated cloud profile using the `data` module.
  3. Reload/reinitialize progress state to reflect cloud truth.
- **Implement WebRTC transport as an interchangeable socket wrapper.** Design the WebRTC connection to match the existing WebSocket interface. The socket object is swapped at runtime, keeping the game serialization and input distribution logic unaware of the underlying transport layer.

## Guardrails (avoid)

- **Do not use "Multiple Platform Leaderboards".** CrazyGames SDK v2 supports **only one primary leaderboard** per game. Do not attempt to submit to different leaderboard IDs. Choose one primary metric (e.g., Total Series Wins or High Streak) for the platform board.
- **Do not let ads interrupt active matches.** Request midgame ads only during natural breaks (between matches, in lobby screens, or on respawn gates) where v1 already emits `gameplayStop/Start`.
- **Do not make login or ads mandatory to play.** Guests must be able to play local couch games, bot matches, and online friends-rooms unimpeded. Ads must never act as a hard soft-wall for core content.
- **Do not store or trust the client-side `__dangerousUserId`; do not decrypt `getUserToken()` on the client.** If secure verification is required (such as leaderboard validation or server-side progress saving), handle token exchange securely via a Railway-hosted backend using the official CrazyGames API key.
- **Do not hardcode sensitive keys.** Keep your CrazyGames API key and Leaderboard Encryption Key secure. For client-side encryption, follow CrazyGames SDK rules using their runtime encryption helper, but ensure production environment variables/keys are injected during the build step and never committed in plaintext to the repository.
- **Do not hand-roll a WebRTC NAT-traversal solution.** Use a lightweight signaling channel over the existing WebSocket relay and fallback immediately to the WebSocket relay if peer-to-peer data channels cannot be negotiated in a timely manner (e.g., 5 seconds).

## Expected output

When this plan is executed, the game is **Full-Launch eligible** on CrazyGames:
- **Fully monetized** through non-intrusive rewarded/midgame video ads and clean menu banner placements that are properly destroyed upon gameplay entry.
- **Cloud-synced progression** using the SDK v2 `data` module, with automatic guest-to-account profile migration.
- **Seamless CG-account identity** in online multiplayer rooms.
- **Low-latency, peer-to-peer WebRTC data channels** with a robust and transparent WebSocket relay fallback.
- **A secure, encrypted single platform leaderboard** compliant with CrazyGames SDK v2 standards.
- All of this is integrated with zero regressions to the F# deterministic core, local couch play, bots, mobile/phone pads, and with `npm run check` fully passing.
