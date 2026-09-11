# CrazyGames QA / publisher gap review

A pass over the current codebase against CrazyGames' published requirements
and known QA behavior, done before submission. Complements
`docs/CrazyGames-v1-plan.md` and `docs/CrazyGames-v2-plan.md` (what's
planned) — this is what's actually true today.

## Summary

No finding here would cause an outright rejection. There is one real
functional gap (missing SDK loading signal) and a cluster of stale claims in
`DEVELOPING.md` that misdescribe the CrazyGames integration — worth fixing
before using that file to write submission notes. Everything else is either
already compliant or a measurement to take right before packing the build.

## Confirmed gaps

1. **No `loadingStart()`/`loadingStop()` SDK calls.** `CrazyGames.js` exposes
   no such export, and nothing in `src/Main.fs` calls it. The `#loader`
   spinner (`play/index.html:11-15`) is hidden by plain app code
   (`src/Main.fs:49`) with zero signal sent to the platform. CrazyGames'
   docs treat this pair as mandatory around asset loading (v3 naming;
   `sdkGameLoadingStart`/`Stop` in v2). Add the pair in `CrazyGames.js`/`.fs`
   and call `loadingStart()` at the top of boot, `loadingStop()` right
   before `Main.fs:49` hides the spinner.

2. **`DEVELOPING.md` misdescribes the integration** — four separate claims
   don't match the code, and `GAMEPLAY.md` already has it right in one case:
   - `DEVELOPING.md:44` — banners in `#cg-banner-1`/`#cg-banner-2`, refreshed
     "at most once per 31 s." **No banner code exists anywhere**: no
     `sdk.banner.*` call in `CrazyGames.js`, no such elements in
     `play/index.html` (confirmed by grep). `GAMEPLAY.md:40` correctly says
     "There are no banner ads."
   - `DEVELOPING.md:98` — claims the leaderboard is wired up. **No
     `sdk.leaderboard.*` call exists**; it's only in the forward-looking
     `docs/CrazyGames-v2-plan.md` spec.
   - `DEVELOPING.md:42,98` — claims `CrazyGames.createRelaySocket` exists
     with a WebRTC/WebSocket fallback. **It doesn't exist** (confirmed by
     grep on `CrazyGames.js`/`.fs`); the actual relay is a plain WebSocket in
     `src/Platform/Input.fs`, unrelated to the CrazyGames SDK wrapper.
   - `DEVELOPING.md:37` — claims the SDK script loads in both `index.html`
     and `play/index.html`. It only loads in `play/index.html:7` (confirmed
     by grep) — not a submission problem, since only `play/index.html`'s
     output ships to CrazyGames via `scripts/pack-crazygames.mjs`, but the
     doc line is wrong.

3. **"SDK v2" labeling vs. the actual `v3` script.** `play/index.html:7`
   loads `crazygames-sdk-v3.js`, but `DEVELOPING.md:37,98`, `CLAUDE.md`, and
   `docs/CrazyGames-v2-plan.md` all call it "SDK v2." The calls in use
   (`gameplayStart`/`gameplayStop`/ads/user/data) work under either version,
   but the loading-signal gap above is exactly the kind of thing the v2→v3
   rename (`sdkGameLoadingStart` → `loadingStart`) causes — worth fixing the
   labeling at the same time as gap 1.

## Verify before submitting (needs a build, not just reading code)

- **Packed size/file count.** `npm run pack` → measure `dist-game/` against
  CrazyGames' caps: ≤50MB initial download (≤20MB if you want mobile-homepage
  eligibility), ≤250MB total, ≤1,500 files. The 47MB `models/` dev sandbox
  dir is already pruned (`DEVELOPING.md:33`), but no one has measured the
  real packed output.
- **Time to first gameplay.** CrazyGames QA targets ≤20s from load start to
  the `gameplayStart` call, for externally hosted files.
- **First-session ad timing.** CrazyGames' guidance is to avoid ads very
  early in a new player's first session (~3-5 min in, or after the tutorial).
  Nothing in the code currently special-cases the very first match a player
  ever completes — the midgame ad (`src/Main.fs:53-60`) can fire after
  match 1. Worth a deliberate decision (skip the ad on the player's first
  completed match, or accept it), not an accident.

## Confirmed compliant (verified directly, not just asserted)

- **Progress save**: `nda-xp`/`nda-daily`/`nda-high-score`/
  `nda-matches-played` go through the Data module (`src/UI/Menu.fs:41-44,
  61-64`) — satisfies "at least one save method."
- **No custom fullscreen button** on the main game view (CrazyGames
  prohibits these — it manages fullscreen itself). The only
  `requestFullscreen()` call is on the separate phone-pad page
  (`src/UI/Pad.fs:43-47`), which isn't part of the CrazyGames-hosted build.
- **Ad flow is correct**: the one midgame ad call (`src/Main.fs:53-60`) is
  wrapped in `gameplayStop()`/`gameplayStart()`, mutes, and freezes the sim
  to a render-only frame while it plays — matches the mute+pause+block-input
  requirement.
- **Physics is framerate-independent**: `src/Main.fs:438-457` is a properly
  capped fixed-timestep accumulator (`Cfg.physicsDt`, capped at 8 steps per
  frame, backlog dropped and logged rather than spiraling). This directly
  addresses "physics that breaks on high-refresh-rate monitors," one of
  CrazyGames' cited top rejection causes.
- **Keyboard input is layout-independent**: `src/Platform/Input.fs` reads
  `KeyboardEvent.code` (`"KeyW"`, `"ArrowUp"`, …), not `.key`/`.which`.
- **Onboarding is skippable and short**: the tutorial (`src/Main.fs:17-39`)
  is a static legend card dismissed on the first keydown/pointerdown, not a
  forced multi-step flow — matches "front-load the fun." It's skipped
  entirely on an instant-multiplayer join via
  `CrazyGames.isInstantMultiplayer()`.
- **The lobby is solo-playable on load** (per `CLAUDE.md`: keyboard claims
  slot 0, a bot claims slot 1) — a QA reviewer testing alone in one browser
  tab can press Start immediately, no second device required.

## Not implemented, not required

Banner ads, rewarded ads, a leaderboard, and account-linked cloud-save
migration are all optional under CrazyGames' requirements and are not built.
`docs/CrazyGames-v2-plan.md` already scopes these as future work — this
report doesn't duplicate that plan, just confirms none of it exists yet in
the shipped code.

## Known QA-coverage gap (not code, process)

`qa/report.md:68` already self-discloses its UX/UI sweep never exercised the
CrazyGames ad/invite flows, or Firefox/Safari. Worth a manual pass through
the invite-room and midgame-ad paths specifically before submission, since
that's exactly the surface CrazyGames QA will exercise first.

## Sources

- [Introduction – Requirements](https://docs.crazygames.com/requirements/intro/)
- [Technical – Requirements](https://docs.crazygames.com/requirements/technical/)
- [Gameplay – Requirements](https://docs.crazygames.com/requirements/gameplay/)
- [Quality guidelines – Requirements](https://docs.crazygames.com/requirements/quality/)
- [Advertisement – Requirements](https://docs.crazygames.com/requirements/ads/)
- [Account integration – Requirements](https://docs.crazygames.com/requirements/account-integration/)
- [Game covers – Requirements](https://docs.crazygames.com/requirements/game-covers/)
- [Game – HTML5 v2 SDK](https://docs.crazygames.com/sdk/html5-v2/game/)
- [Video ads – SDK](https://docs.crazygames.com/sdk/video-ads/)
- [Data – SDK](https://docs.crazygames.com/sdk/data/)
- [Automatic progress save](https://docs.crazygames.com/other/aps/)
- [Sitelock – HTML5 resources](https://docs.crazygames.com/resources/html5/sitelock/)
- [Getting to the first frame – Game loading tips](https://docs.crazygames.com/resources/getting-to-the-first-frame/)
- Developer rejection/experience reports: itch.io forum threads on CrazyGames
  submissions (inconsistent rejection feedback reported by multiple
  developers; no single documented rejection checklist beyond the official
  requirements pages above)
