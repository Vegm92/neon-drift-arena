module Render

open System
open Browser
open Browser.Types
open Fable.Core.JsInterop
open Vec
open Domain
open Domain.Cfg
open Three
open RenderTypes
open RenderMeshes
open RenderFx
open RenderEnt
open RenderHud
open RenderCam

let create () =
    let scene = three.Scene()
    let w, h = window.innerWidth, window.innerHeight
    let camera = three.PerspectiveCamera(40., aspect (), 1., 10000.)
    let renderer = three.WebGLRenderer()
    renderer.setPixelRatio (min window.devicePixelRatio 2.)
    renderer.setSize (w, h)
    document.body.appendChild renderer.domElement |> ignore
    let composer = effectComposer renderer
    let bloom = bloomPass (three.Vector2(w, h), 1.3, 0.5, 0.12)
    composer.addPass (renderPass (scene, camera))
    composer.addPass (box bloom)
    let border, spawns, frame = mkArena scene
    let hole, horizon, halo = mkHole scene
    let hud = document.getElementById "hud"
    let vw =
        { Scene = scene
          Camera = camera
          Renderer = renderer
          Composer = composer
          Bloom = bloom
          Ships = Array.init 4 (mkShip scene)
          Pads = [||]
          Rocks = [||]
          Road = None
          Marks = [||]
          Layout = -1
          Bullets = Array.init bulletPool (fun _ -> mkBullet scene)
          Mines = Array.init minePool (fun _ -> mkMine scene)
          Boulders = Array.init rockPool (fun _ -> mkAsteroid scene 0xff7a1e 0xffe0b0 { Pos = zero; Radius = rockRadius })
          Gates = Array.init (portalPool * 4) (mkGate scene)
          Hole = hole
          Horizon = horizon
          Halo = halo
          Crates = Array.init 4 (fun _ -> mkCrate scene)
          Panels = Array.init 4 (mkPanel hud)
          Tags = Array.init 4 (mkTag hud)
          Names = Array.init 4 Strings.t.Player
          Spawns = spawns
          Intro = 0.
          Border = border
          Frame = frame
          Size = arenaHalf
          Clock = document.getElementById "clock"
          Feed = document.getElementById "feed"
          Banner = document.getElementById "banner"
          Vignette = document.getElementById "vignette"
          Bursts = []
          Shards = []
          Flashes = []
          Hp = Array.create 4 hpMax
          Shake = Array.zeroCreate 4
          Smoke = Array.zeroCreate 4
          Puff = 0.
          Spike = 0.
          Jolt = 0.
          Tint = 0.
          TintHex = "#ffffff"
          KillCd = 0.
          Cam = zero
          CamH = maxCamH () }
    syncArena vw
    window.addEventListener ("resize", fun _ -> resize vw)
    vw

let syncArena = RenderMeshes.syncArena

let draw (vw: View) (w: World) (events: Event list) dt =
    for e in events do
        match e with
        | Hit(p, _, _, _) -> spawnBurst vw p 0xffffff 10 160.
        | Ram p -> spawnBurst vw p 0xffffff 16 200.
        | Bump p -> spawnBurst vw p 0xff9955 8 130.
        | Pickup(p, big) -> spawnBurst vw p 0x33ffcc (if big then 26 else 14) 150.
        | Mend p ->
            spawnBurst vw p 0xff4d9d 20 140.
            spawnRing vw p 0xff4d9d 30. 1.8 0.5
        | Grab p ->
            spawnBurst vw p 0xfff45c 26 190.
            spawnRing vw p 0xfff45c 34. 2.4 0.45
        | Charging p -> spawnRing vw p 0xffffff 40. -0.7 (min 0.9 railCharge)
        | Beam(a, b, i) ->
            spawnBeam vw a b (shipColor w.Ships.[i])
            spawnBurst vw a 0xffffff 22 260.
            vw.Spike <- max vw.Spike 0.7
        | MineSet p -> spawnBurst vw p 0xff2b4d 8 90.
        | MineLive p -> spawnRing vw p 0xff2b4d mineMagnet 0.2 0.35
        | Blast p ->
            spawnBurst vw p 0xff6a2b 40 300.
            spawnSmoke vw p 10 50. 30.
            spawnRing vw p 0xff6a2b mineBlast 1.3 0.45
            vw.Spike <- max vw.Spike 0.8
        | Wave(p, a, i) ->
            spawnCone vw p (shipColor w.Ships.[i]) 34 pulseRange a pulseCone 7.
            spawnRing vw p 0xbfe6ff 120. 2.2 0.4
        | Cooked p ->
            spawnSmoke vw p 6 34. 16.
            spawnRing vw p 0xffffff (shipRadius + 6.) 1.5 0.35
        | Zap(p, a, _) ->
            spawnBolts vw p 0x9df3ff a (scatterCone * 1.3) (scatterRange * 1.3)
            spawnCone vw p 0xbff6ff 48 (scatterRange * 2.6) a (scatterCone * 1.2) 11.
            vw.Spike <- max vw.Spike 0.4
        | Latch(p, i) -> spawnRing vw p (shipColor w.Ships.[i]) 30. 3. 0.3
        | Launch p ->
            spawnBurst vw p 0xff9955 18 200.
            spawnRing vw p 0xff9955 rockRadius 2.5 0.4
        | PortalOpen(a, b) ->
            for p in [ a; b ] do
                spawnRing vw p portalHex portalRadius 3. 0.6
                spawnBurst vw p portalHex 20 160.
        | Warp p -> spawnBurst vw p portalHex 12 140.
        | HoleOpen p ->
            spawnRing vw p holeHex (holeCore * 9.) -0.8 0.9
            spawnBurst vw p holeHex 40 240.
            vw.Spike <- max vw.Spike 0.9
        | Explode(p, i, ring) ->
            let hex = shipColor w.Ships.[i]
            let sv = vw.Ships.[i]
            let dir =
                if sv.History.Count > 1 then
                    let d = sv.History.[0] - sv.History.[1]
                    atan2 d.Y d.X
                else
                    rnd.NextDouble() * Math.PI * 2.
            spawnFlash vw p 0xffa04d 16. 0.1
            spawnShards vw p hex 9
            spawnCone vw p 0xff6a14 52 320. dir Math.PI 10.
            spawnCone vw p 0xffc23d 22 150. dir Math.PI 12.
            spawnCone vw p hex 40 380. dir 0.8 7.
            spawnSmoke vw p 12 60. 42.
            spawnRing vw p 0xff8a2b 44. 2.6 0.34
            spawnRing vw p hex 34. 2.4 0.46
            // A takedown inside `killGap` of the last one flashes at a fraction of the
            // strength, so a double kill never lands two full flashes in a second.
            let damp = if vw.KillCd > 0. then killRepeat else 1.
            vw.KillCd <- killGap
            vw.Spike <- max vw.Spike (killSpike * damp)
            vw.Jolt <- 1.
            vw.Tint <- max vw.Tint (killTint * damp)
            vw.TintHex <- sprintf "#%06x" (if ring then 0x3d5cff else hex)
        | Downed(victim, by, wpn, ring) -> feedLine vw w victim by wpn ring
        | Finished(i, _) -> spawnBurst vw w.Ships.[i].Pos (shipColor w.Ships.[i]) 40 260.
        | Shot _ -> ()
    drawSmoke vw w dt
    Array.iter2 (drawShip w.Time vw) vw.Ships w.Ships
    Array.iter2 (drawTether w.Time w) vw.Ships w.Ships
    Array.iter2 (drawWarn w.Time w) vw.Ships w.Ships
    drawBorder vw w
    drawPads vw w
    drawCrates vw w
    drawMines vw w
    drawRocks vw w dt
    drawPortals vw w
    drawHole vw w dt
    drawBullets vw w dt
    updateBursts vw dt
    updateShards vw dt
    updateFlashes vw dt
    frameCamera vw w dt
    drawTags vw w
    drawSpawns vw w
    drawMarks vw w
    drawHud vw w dt
    drawTint vw dt
    drawPost vw dt
    vw.Composer.render ()
