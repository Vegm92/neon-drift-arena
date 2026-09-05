# Steam deployment checklist

What the repo has today versus what Steam needs. Gaps are ordered by how much they block a release.
Sources: Summer Engine's Steam guide and the Steamworks docs (release process, graphical assets, trailers, Steam Direct onboarding).

## 1. Blocker: no desktop executable

Steam only ships native builds. Today the game is a Vite site served by `deploy/server.mjs` on Railway. Nothing in the repo produces a `.exe`, `.app` or Linux binary.

| Missing | Notes |
|---------|-------|
| Desktop wrapper project (Electron or Tauri) | Not in `package.json`, no `main` process, no packaging script. Windows x64 is the minimum; macOS and Linux are optional depots. |
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

- Steam account, then Steamworks partner sign-up at https://partner.steamgames.com/steamdirect. $100 per app, credited back after $1,000 revenue.
- Legal name or company, identity verification, tax interview (W-8BEN for non-US individuals), bank details.
- 30-day wait after paying the fee before the app can be released.
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
| Screenshots | 5 or more, 1920x1080, 16:9 | Real gameplay, no UI mockups. The 1200x675 stills are too small. |
| Trailer | MP4, H.264 + AAC, up to 1920x1080, 30 or 60 fps, 5,000+ kbps | `public/trailer.mp4` is 1280x720, 27 s, about 1.5 Mbps. Re-capture at 1080p, 30 to 60 s with a title card, upload directly (no YouTube link). |

Text and settings:

- Short description (300 chars), long description, developer and publisher names, support email or URL, legal line.
- System requirements, minimum and recommended: OS, CPU, GPU with WebGL 2, RAM, disk.
- Supported languages: `Strings.fs` has only `en`. Declare English (interface, audio, subtitles) and nothing else.
- Genre, up to 20 tags, feature checkboxes: Local Multi-Player, Shared/Split Screen, Full Controller Support, Remote Play Together (the last one matters most for a couch game).
- Content survey and age rating questionnaire (cartoon ship combat, no blood).
- Price or Free. The landing page sells "FREE · NO INSTALL, PLAY IN BROWSER"; decide whether the Steam build is paid and keep the web copy consistent.
- Timing: Coming Soon page live at least 2 weeks before release; store page review takes 3 to 5 business days, plan 7.

## 4. Steamworks app configuration

- App ID, one depot per OS, install folder name, launch options with the executable path per OS.
- Controller support level and default Steam Input configuration.
- Steam Cloud (optional), achievements (none), Remote Play Together enabled.
- Build review takes 3 to 5 business days, plan 7. Valve runs the build on a clean machine.
- Both checklists (store page and build) must be complete before "Mark as ready for review".
