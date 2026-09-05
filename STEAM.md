# Steam deployment checklist

What the repo has today versus what Steam needs. Gaps are ordered by how much they block a release.
Sources: Summer Engine's Steam guide, the Steam Direct page and the Steamworks docs (release process, graphical assets, trailers).

## 1. Blocker: no desktop executable

Steam only ships native builds. Today the game is a Vite site served by `deploy/server.mjs` on Railway. Nothing in the repo produces a `.exe`, `.app` or Linux binary.

| Missing | Notes |
|---------|-------|
| Desktop wrapper project (Electron or Tauri) | Not in `package.json`, no `main` process, no packaging script. Windows x64 is the minimum; macOS and Linux are optional depots. |
| No extra installs for the player | Steam rejects builds that need software installed by hand. Electron bundles Chromium. Tauri relies on WebView2 on Windows, so its installer must embed the WebView2 bootstrapper or offline installer. |
| Relative asset base | `vite.config.js` has no `base`; `play/index.html`, `pad.html` and `Sfx.fs` load `/build/Main.js`, `/backdrop.jpg`, `/ships.png`, `/music/*.mp3` from the site root. Either set `base: "./"` or have the wrapper serve `dist/` over a local HTTP server. |
| Offline-safe dev endpoints | `Menu.fs` fetches `/__pad-url`, `Settings.fs` posts to `/__tweaks`. Both are Vite-only; they fail silently but the lobby QR stays empty. |
| Phone-pad relay for a local build | Pads ride Vite HMR in dev and `/relay` on the public host. A Steam build has neither. Either embed a LAN WebSocket relay plus `pad.html` in the wrapper and print the LAN URL in the QR, or drop the feature and its store copy. LAN mirror clients (`nda:state`) are HMR-only and go away entirely. |
| QUIT TO DESKTOP | `Strings.fs` only has `QUIT TO LOBBY`. A gamepad-only player has no way to exit the process. |
| Fullscreen and window | Only `Pad.fs` calls `requestFullscreen`. The wrapper must open borderless/fullscreen; the existing resize listener in `Render.fs` handles size changes. |
| Audio without a keypress | WebAudio stays suspended until a key or click (`Sfx.fs`), so a gamepad-only session is silent. Launch Chromium with `--autoplay-policy=no-user-gesture-required` (Electron) or resume the context from the wrapper. |
| Keyboard layout detection | `Input.fs` uses `navigator.keyboard.getLayoutMap`, Chromium only. Fine in Electron, missing in Tauri's WebKit on macOS (AZERTY hints break). |
| Steam overlay hooks | Electron needs `--in-process-gpu` and `--disable-direct-composition`, Steamworks init (`steamworks.js`) and a `steam_appid.txt` next to the binary in dev. |
| Steam Input | Gamepad API sees XInput pads, so set the default Steam Input config to the Gamepad template and test with Steam Input on (keyboard emulation causes double input). Declare controller support level in Steamworks. |
| Save data location | `localStorage` keys (`nda-tweaks`, `nda-pads`, `nda-tut`, audio, arena, catch-up) land in the wrapper's user-data folder. Point Steam Auto-Cloud at that folder if cloud saves are wanted; otherwise nothing to do. |
| Build pipeline | No `.github`, no `steam`/`package` script. Need: wrapper build (`electron-builder` or `tauri build`), `app_build.vdf` + one `depot_build.vdf` per OS, `steamcmd` upload, and a clean-machine test before submitting. |
| macOS signing | A Mac depot needs a notarized, signed app. Windows does not need signing for Steam. |
| Depot contents | Exclude the landing page (`index.html`, Google Fonts link, `og.jpg`, trailer) and the Railway `Dockerfile`/server from the depot. The game itself uses system fonts, so it runs offline. |

## 2. Steamworks account and legal (outside the repo)

- Steam account, then Steamworks partner sign-up at https://partner.steamgames.com/steamdirect. $100 per app, non-refundable, credited back once the app reaches $1,000 adjusted gross revenue.
- Sign the digital agreements, then identity verification, tax questionnaire (W-9 for US, W-8BEN for treaty countries) and bank details. The bank account holder must match the legal identity; a company needs a business account.
- 30-day wait after paying the fee before the app can be released.
- Content rules: nothing you do not own the rights to, no ad-based business model, adult content labelled. The first one is the only risk here, see asset rights below.
- Asset rights: no `LICENSE` file in the repo. `public/music/battle1..3.mp3` and `menu.mp3` were committed without any license or attribution record; `ships.png` and `backdrop.jpg` likewise. Valve's review checks ownership of copyrighted material. Bebas Neue (OFL) is only on the landing page.

## 3. Store page (all missing)

Existing images (`og.jpg` 1200x630, `trailer.jpg` 1280x720, stills 1200x675, `backdrop.jpg` 1376x768) match none of the required sizes.

| Asset | Size | Rule |
|-------|------|------|
| Header capsule | 920x430 | Artwork plus readable title/logo only. No "FREE · NO INSTALL" or other marketing text. |
| Small capsule | 462x174 | Logo must read at 120x45. |
| Main capsule | 1232x706 | |
| Vertical capsule | 748x896 | |
| Library capsule | 600x900 | |
| Library hero | 3840x1240 | Artwork only, no text. |
| Library logo | 1280 wide and/or 720 tall | Transparent PNG. |
| Community icon | 184x184 JPG | |
| Client icon | 32x32 ICO (Windows), ICNS (macOS) | |
| Page background | 1438x810 | Optional. |
| Screenshots | 5 or more, 1920x1080, 16:9 | Real gameplay, no UI mockups. The 1200x675 stills are too small. |
| Trailer | MP4, H.264 + AAC, up to 1920x1080, 30 or 60 fps, 5,000+ kbps | `public/trailer.mp4` is 1280x720, 27 s, about 1.5 Mbps. Re-capture at 1080p, 30 to 60 s with a title card, upload directly (no YouTube link). |

The Summer Engine article lists the pre-August-2024 sizes (460x215, 231x87, 616x353). Steamworks now asks for the doubled sizes above; the old ones get rejected or upscaled.

Text and settings:

- Short description (300 chars), long description, developer and publisher names, support email or URL, legal line. Lead with what the player does and use searchable words (local multiplayer, couch, arena shooter, party game); the text is indexed.
- System requirements, minimum and recommended: OS, CPU, GPU with WebGL 2, RAM, disk.
- Supported languages: `Strings.fs` has only `en`. Declare English (interface, audio, subtitles) and nothing else.
- Genre, up to 20 tags, feature checkboxes: Local Multi-Player, Shared/Split Screen, Full Controller Support, Remote Play Together (the last one matters most for a couch game).
- Content survey and age rating questionnaire (cartoon ship combat, no blood).
- Pricing: Free-to-Play skips the price step. Paid: base price in USD, regional prices auto-generated. Valve keeps 30% (25% past $10M, 20% past $50M). Launch discount up to 40% in week one, then no discount for 28 days. A first game with no audience usually sits at $5 to $15. The landing page sells "FREE · NO INSTALL, PLAY IN BROWSER"; decide before the page goes live and keep the web copy consistent.
- Timing: the store page can go to review before the build. Review takes 1 to 5 business days, plan 7. Coming Soon page live at least 2 weeks before release (Valve minimum); 2 to 6 months is the recommended wishlist window.
- Optional: a demo build for Steam Next Fest is a separate app with its own depot.

## 4. Steamworks app configuration

- App ID, one depot per OS, install folder name, launch options with the executable path per OS.
- Upload with `steamcmd` from the Steamworks SDK (or the dashboard uploader), then set the build live on the default branch so reviewers get it.
- Controller support level and default Steam Input configuration.
- Steam Cloud (optional), achievements (none), Remote Play Together enabled.
- Build review takes 1 to 5 business days, plan 7. Valve runs the build on a clean machine, so test on one without the .NET SDK, Node or dev tools first.
- Both checklists (store page and build) must be complete before "Mark as ready for review".
