module Render

open System
open Browser
open Browser.Types
open Fable.Core.JsInterop
open Vec
open Domain
open Domain.Cfg
open Three

let private colors = [| 0x00f6ff; 0xff2bd6; 0x7cff3a; 0xffb300 |]
let private teamColors = [| 0; 0x3b7bff; 0xff3b5c |]
let private shipColor (s: Ship) = if s.Team > 0 then teamColors.[s.Team] else colors.[playerColor.[s.Id]]
let private trailLen = 48
let private bulletPool = 96
let private minePool = 24
let private maxCamH = (arenaHalf + 100.) / tan (20. * Math.PI / 180.)
let private minCamH = maxCamH * 0.75
let private rnd = Random()
let private sheet = 1536., 1024.
let private cell = 530., 450.
let private cells = [| 217., 40.; 790., 46.; 12., 452.; 502., 481.; 984., 474. |]
let private spriteOf (s: Ship) = if s.Team > 0 then s.Team - 1 else [| 0; 1; 3; 4 |].[playerColor.[s.Id]]

type ShipView =
    { Shield: Mesh
      Root: Object3D
      Body: Mesh
      Flame: Mesh
      Retro: Object3D
      Coil: Mesh
      Laser: Mesh
      Tether: Mesh
      Bubble: Mesh
      Trail: Mesh
      History: ResizeArray<V2> }

type Burst =
    { Points: Mesh
      Vel: float[]
      mutable Life: float }

type Flash =
    { Obj: Mesh
      Grow: float
      Span: float
      mutable Life: float }

type View =
    { Scene: Object3D
      Camera: Camera
      Renderer: Renderer
      Composer: Composer
      Bloom: Bloom
      Ships: ShipView[]
      mutable Pads: Mesh[]
      mutable Rocks: Object3D[]
      mutable Layout: int
      Bullets: Mesh[]
      Mines: Object3D[]
      Crates: Object3D[]
      Panels: HTMLElement[]
      Banner: HTMLElement
      Vignette: HTMLElement
      mutable Bursts: Burst list
      mutable Flashes: Flash list
      Hp: float[]
      Shake: float[]
      Smoke: float[]
      mutable Puff: float
      mutable Spike: float
      mutable Tint: float
      mutable TintHex: string
      mutable Cam: V2
      mutable CamH: float }

let private flat (g: BufferGeometry) = g.rotateX (-Math.PI / 2.)

let private glowMat hex opacity =
    three.MeshBasicMaterial(
        box {| color = hex; transparent = true; opacity = opacity; blending = three.AdditiveBlending |}
    )

let private lineMat hex opacity =
    three.LineBasicMaterial(
        box {| color = hex; transparent = true; opacity = opacity; blending = three.AdditiveBlending |}
    )

let private shipSheet =
    lazy
        (let t = three.loadTexture "/ships.png"
         t.colorSpace <- three.SRGBColorSpace
         t.repeat.set (fst cell / fst sheet, snd cell / snd sheet)
         t)

let private shipGeometry = lazy (three.PlaneGeometry(64., 54.).rotateZ (-Math.PI / 2.) |> flat)

let private rgb hex =
    float ((hex >>> 16) &&& 0xff) / 255., float ((hex >>> 8) &&& 0xff) / 255., float (hex &&& 0xff) / 255.

let private mkTrail hex =
    let g = three.BufferGeometry()
    g.setAttribute ("position", three.Float32BufferAttribute(Array.zeroCreate (trailLen * 3), 3))
    let r, gr, b = rgb hex
    let cols =
        Array.init (trailLen * 3) (fun i ->
            let f = 1. - float (i / 3) / float (trailLen - 1)
            (match i % 3 with
             | 0 -> r
             | 1 -> gr
             | _ -> b)
            * f
            * f)
    g.setAttribute ("color", three.Float32BufferAttribute(cols, 3))
    g.setDrawRange (0, 0)
    three.Line(
        g,
        three.LineBasicMaterial(
            box {| vertexColors = true; transparent = true; opacity = 0.9; blending = three.AdditiveBlending |}
        )
    )

let private mkShip (scene: Object3D) i =
    let hex = colors.[i]
    let root = three.Group()
    let body =
        three.Mesh(
            shipGeometry.Value,
            three.MeshBasicMaterial(box {| map = shipSheet.Value.clone (); transparent = true; depthWrite = false |})
        )
    body.position.y <- 1.
    let flame = three.Mesh((three.CircleGeometry(7., 12) |> flat).translate (-16., 3., 0.), glowMat hex 0.9)
    root.add body
    root.add flame
    let retro = three.Group()
    for z in [ -7.; 7. ] do
        retro.add (three.Mesh((three.CircleGeometry(2.5, 8) |> flat).translate (15., 3., z), glowMat hex 0.9))
    root.add retro
    let coil = three.Mesh((three.RingGeometry(9., 13., 24) |> flat).translate (30., 3., 0.), glowMat 0xffffff 1.)
    coil.visible <- false
    root.add coil
    let laser = three.Mesh((three.PlaneGeometry(400., 1.2) |> flat).translate (218., 2., 0.), glowMat hex 0.3)
    laser.visible <- false
    root.add laser
    let bubble = three.Mesh(three.RingGeometry(0.985, 1., 64) |> flat, glowMat hex 0.2)
    bubble.position.y <- 2.
    bubble.visible <- false
    root.add bubble
    let shield = three.Mesh(three.RingGeometry(shipRadius + 5., shipRadius + 9., 6) |> flat, glowMat 0x3b8cff 0.9)
    shield.visible <- false
    root.add shield
    let trail = mkTrail hex
    let tether = three.Mesh(three.PlaneGeometry(1., 4.) |> flat, glowMat hex 0.7)
    tether.visible <- false
    scene.add tether
    scene.add root
    scene.add trail
    { Shield = shield
      Root = root
      Body = body
      Flame = flame
      Retro = retro
      Coil = coil
      Laser = laser
      Tether = tether
      Bubble = bubble
      Trail = trail
      History = ResizeArray() }

let private padHex (p: Pad) =
    match p.Kind with
    | 1 -> 0x3bff9e
    | 2 -> 0x3b8cff
    | _ -> 0xfff45c

let private padSpan (p: Pad) =
    match p.Kind with
    | 1 -> healRespawn
    | 2 -> shieldRespawn
    | _ -> padRespawn

let private mkPad (scene: Object3D) (p: Pad) =
    let inner, seg =
        match p.Kind with
        | 1 -> padRadius - 12., 40
        | 2 -> padRadius * 0.42, 6
        | _ -> padRadius - 5., 40
    let m = three.Mesh(three.RingGeometry(inner, padRadius, seg) |> flat, glowMat (padHex p) 1.)
    m.position.set (p.Pos.X, 0.5, p.Pos.Y)
    scene.add m
    m

let private mkMine (scene: Object3D) =
    let g = three.IcosahedronGeometry(mineRadius, 0)
    let root = three.Group()
    root.add (three.Mesh(g, three.MeshBasicMaterial(box {| color = 0x120308 |})))
    root.add (three.LineSegments(three.EdgesGeometry g, lineMat 0xff2b4d 1.))
    root.visible <- false
    scene.add root
    root

let private mkCrate (scene: Object3D) =
    let side = crateRadius * 1.35
    let g = three.BoxGeometry(side, side, side)
    let root = three.Group()
    root.add (three.Mesh(g, three.MeshBasicMaterial(box {| color = 0x0a0a1e |})))
    root.add (three.LineSegments(three.EdgesGeometry g, lineMat 0xfff45c 1.))
    for axis in 0..2 do
        let strap = three.LineSegments(three.EdgesGeometry(three.BoxGeometry(side * 1.02, side * 0.26, side * 1.02)), lineMat 0xfff45c 0.6)
        strap.rotation.set ((if axis = 1 then Math.PI / 2. else 0.), 0., (if axis = 2 then Math.PI / 2. else 0.))
        root.add strap
    root.add (three.Mesh(three.RingGeometry(crateRadius + 8., crateRadius + 11., 32) |> flat, glowMat 0xfff45c 0.7))
    scene.add root
    root

let private mkAsteroid (scene: Object3D) (a: Asteroid) =
    let g = three.IcosahedronGeometry(a.Radius, 1)
    let root = three.Group()
    root.add (three.Mesh(g, three.MeshBasicMaterial(box {| color = 0x05060f |})))
    root.add (three.LineSegments(three.EdgesGeometry g, lineMat 0x6a7cff 0.8))
    root.position.set (a.Pos.X, 0., a.Pos.Y)
    root.rotation.set (a.Pos.X * 0.01, a.Pos.Y * 0.01, 0.)
    scene.add root
    root

let private mkBullet (scene: Object3D) =
    let m = three.Mesh(three.PlaneGeometry(16., 3.) |> flat, glowMat 0xffffff 1.)
    m.visible <- false
    scene.add m
    m

let private octagon (scale: float) =
    let a = arenaHalf * scale
    let c = (diagLimit - arenaHalf) * scale
    [| for x, y in [ a, c; c, a; -c, a; -a, c; -a, -c; -c, -a; c, -a; a, -c ] do
           yield! [ x; 0.; y ] |]

let private mkLoop (scene: Object3D) scale hex opacity y =
    let g = three.BufferGeometry()
    g.setAttribute ("position", three.Float32BufferAttribute(octagon scale, 3))
    let l = three.LineLoop(g, lineMat hex opacity)
    l.position.y <- y
    scene.add l

let private mkArena (scene: Object3D) =
    mkLoop scene 1. 0x00f6ff 1. 0.
    mkLoop scene 0.985 0xff2bd6 0.35 0.
    mkLoop scene 0.62 0x15294d 0.7 -0.5
    let grid = three.GridHelper(arenaHalf * 2., 27, 0x101a38, 0x0a0f22)
    grid.position.y <- -1.
    scene.add grid
    let cut = diagLimit - arenaHalf
    for sx in [ 1.; -1. ] do
        for sy in [ 1.; -1. ] do
            let g = three.BufferGeometry()
            g.setAttribute (
                "position",
                three.Float32BufferAttribute(
                    [| sx * arenaHalf; 0.; sy * cut
                       sx * arenaHalf; 0.; sy * arenaHalf
                       sx * cut; 0.; sy * arenaHalf |],
                    3
                )
            )
            let m = three.Mesh(g, three.MeshBasicMaterial(box {| color = 0x000000 |}))
            m.position.y <- -0.9
            scene.add m
    let core = three.Group()
    core.add (three.Mesh(three.RingGeometry(96., 100., 6) |> flat, glowMat 0xfff45c 0.8))
    core.add (three.Mesh(three.RingGeometry(150., 152., 48) |> flat, glowMat 0x00f6ff 0.35))
    core.position.y <- -0.5
    scene.add core
    for i in 0..3 do
        let p = Sim.spawnPos i
        let m = three.Mesh(three.RingGeometry(120., 124., 8) |> flat, glowMat colors.[i] 0.5)
        m.position.set (p.X, -0.5, p.Y)
        m.rotation.z <- Math.PI / 8.
        scene.add m
    let n = 1500
    let stars = three.BufferGeometry()
    stars.setAttribute (
        "position",
        three.Float32BufferAttribute(
            Array.init (n * 3) (fun i ->
                match i % 3 with
                | 1 -> -80. - rnd.NextDouble() * 400.
                | _ -> (rnd.NextDouble() - 0.5) * 7000.),
            3
        )
    )
    scene.add (
        three.Points(stars, three.PointsMaterial(box {| color = 0x9fb3ff; size = 3.; transparent = true; opacity = 0.7 |}))
    )

let syncArena (vw: View) =
    if vw.Layout <> Sim.layout then
        vw.Layout <- Sim.layout
        for o in vw.Rocks do
            vw.Scene.remove o
        for m in vw.Pads do
            vw.Scene.remove m
        vw.Rocks <- Sim.asteroids |> Array.map (mkAsteroid vw.Scene)
        vw.Pads <- Sim.initial.Pads |> Array.map (mkPad vw.Scene)

let private mkPanel (hud: HTMLElement) i =
    let el = document.createElement "div"
    el.className <- sprintf "panel p%d off" i
    el.innerHTML <-
        sprintf
            "<div class=\"name\" style=\"color:#%06x\">%s</div><div class=\"bar hp\"><i></i></div><div class=\"bar shield\"><i></i></div><div class=\"bar boost\"><i></i></div><div class=\"bar heat\"><i></i></div><div class=\"stocks\"></div><div class=\"wep\"></div>"
            colors.[i]
            (Strings.t.Player i)
    hud.appendChild el |> ignore
    el

let private aspect () =
    let w, h = window.innerWidth, window.innerHeight
    if h > 0. then w / h else 16. / 9.

let private resize (vw: View) =
    let w, h = window.innerWidth, window.innerHeight
    vw.Camera.aspect <- aspect ()
    vw.Camera.updateProjectionMatrix ()
    vw.Renderer.setSize (w, h)
    vw.Composer.setSize (w, h)

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
    mkArena scene
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
          Layout = -1
          Bullets = Array.init bulletPool (fun _ -> mkBullet scene)
          Mines = Array.init minePool (fun _ -> mkMine scene)
          Crates = Array.init 4 (fun _ -> mkCrate scene)
          Panels = Array.init 4 (mkPanel hud)
          Banner = document.getElementById "banner"
          Vignette = document.getElementById "vignette"
          Bursts = []
          Flashes = []
          Hp = Array.create 4 hpMax
          Shake = Array.zeroCreate 4
          Smoke = Array.zeroCreate 4
          Puff = 0.
          Spike = 0.
          Tint = 0.
          TintHex = "#ffffff"
          Cam = zero
          CamH = maxCamH }
    syncArena vw
    window.addEventListener ("resize", fun _ -> resize vw)
    vw

let private spawnCone (vw: View) (p: V2) hex n speed dir spread size =
    let g = three.BufferGeometry()
    g.setAttribute (
        "position",
        three.Float32BufferAttribute(
            Array.init (n * 3) (fun i ->
                match i % 3 with
                | 0 -> p.X
                | 1 -> 4.
                | _ -> p.Y),
            3
        )
    )
    let vel = Array.zeroCreate (n * 2)
    for k in 0 .. n - 1 do
        let a = dir + (rnd.NextDouble() - 0.5) * 2. * spread
        let s = speed * (0.3 + rnd.NextDouble())
        vel.[k * 2] <- cos a * s
        vel.[k * 2 + 1] <- sin a * s
    let pts =
        three.Points(
            g,
            three.PointsMaterial(
                box {| color = hex; size = size; transparent = true; opacity = 1.; blending = three.AdditiveBlending |}
            )
        )
    vw.Scene.add pts
    vw.Bursts <- { Points = pts; Vel = vel; Life = 1. } :: vw.Bursts

let private spawnBurst (vw: View) (p: V2) hex n speed =
    spawnCone vw p hex n speed 0. Math.PI 7.

let private addFlash (vw: View) (m: Mesh) grow span =
    vw.Scene.add m
    vw.Flashes <- { Obj = m; Grow = grow; Span = span; Life = span } :: vw.Flashes

let private spawnBolts (vw: View) (p: V2) hex dir spread range =
    for _ in 1..6 do
        let a = dir + (rnd.NextDouble() - 0.5) * 2. * spread
        let reach = range * (0.45 + 0.55 * rnd.NextDouble())
        let axis = ofAngle a
        let across = ofAngle (a + Math.PI / 2.)
        let steps = 6
        let mutable last = p
        for k in 1..steps do
            let t = float k / float steps
            let sway = (rnd.NextDouble() - 0.5) * reach * (if k = steps then 0.12 else 0.25)
            let next = p + axis * (reach * t) + across * sway
            let d = next - last
            let m = three.Mesh(three.PlaneGeometry(len d, 2.5) |> flat, glowMat hex 1.)
            let mid = (last + next) * 0.5
            m.position.set (mid.X, 5., mid.Y)
            m.rotation.y <- -(atan2 d.Y d.X)
            addFlash vw m 0. (0.1 + rnd.NextDouble() * 0.12)
            last <- next

let private spawnRing (vw: View) (p: V2) hex r grow span =
    let m = three.Mesh(three.RingGeometry(r * 0.86, r, 44) |> flat, glowMat hex 1.)
    m.position.set (p.X, 3., p.Y)
    addFlash vw m grow span

let private edgeHit (a: V2) (d: V2) =
    let axis o dd = if abs dd < 1e-6 then 1e9 else max ((arenaHalf - o) / dd) ((-arenaHalf - o) / dd)
    let chamfer =
        [ for sx in [ 1.; -1. ] do
              for sy in [ 1.; -1. ] do
                  let dd = sx * d.X + sy * d.Y
                  if dd > 1e-6 then yield (diagLimit - (sx * a.X + sy * a.Y)) / dd ]
        |> function
            | [] -> 1e9
            | ts -> List.min ts
    min (min (axis a.X d.X) (axis a.Y d.Y)) chamfer |> max 0.

let private spawnBeam (vw: View) (a: V2) (b: V2) hex =
    let d = norm (b - a)
    let far = a + d * edgeHit a d
    let mid = (a + far) * 0.5
    let m = three.Mesh(three.PlaneGeometry(len (far - a), 9.) |> flat, glowMat hex 1.)
    m.position.set (mid.X, 5., mid.Y)
    m.rotation.y <- -(atan2 d.Y d.X)
    addFlash vw m 0. 0.32

let private updateBursts (vw: View) dt =
    vw.Bursts <-
        vw.Bursts
        |> List.filter (fun b ->
            b.Life <- b.Life - dt * 1.4
            if b.Life <= 0. then
                vw.Scene.remove b.Points
                false
            else
                let attr = b.Points.geometry.getAttribute "position"
                let a = attr.array
                for k in 0 .. b.Vel.Length / 2 - 1 do
                    a.[k * 3] <- a.[k * 3] + b.Vel.[k * 2] * dt
                    a.[k * 3 + 2] <- a.[k * 3 + 2] + b.Vel.[k * 2 + 1] * dt
                attr.needsUpdate <- true
                b.Points.material.opacity <- b.Life
                true)

let private updateFlashes (vw: View) dt =
    vw.Flashes <-
        vw.Flashes
        |> List.filter (fun f ->
            f.Life <- f.Life - dt
            if f.Life <= 0. then
                vw.Scene.remove f.Obj
                false
            else
                let k = 1. - f.Life / f.Span
                let s = 1. + f.Grow * k
                f.Obj.scale.set (s, 1., s)
                f.Obj.material.opacity <- 1. - k
                true)

let private drawTether t (w: World) (sv: ShipView) (s: Ship) =
    let target =
        match s.Tow with
        | TowShip j when s.Alive -> Some w.Ships.[j].Pos
        | TowRock k when s.Alive -> Some Sim.asteroids.[k].Pos
        | _ -> None
    sv.Tether.visible <- target.IsSome
    match target with
    | Some p ->
        let d = p - s.Pos
        let mid = (s.Pos + p) * 0.5
        sv.Tether.position.set (mid.X, 4., mid.Y)
        sv.Tether.rotation.y <- -(atan2 d.Y d.X)
        sv.Tether.scale.set (len d, 1., 1.)
        sv.Tether.material.opacity <- 0.5 + 0.3 * sin (t * 25.)
    | None -> ()

let private drawShip t (vw: View) (sv: ShipView) (s: Ship) =
    sv.Root.visible <- s.Alive
    if s.Alive then
        sv.Root.position.set (s.Pos.X, 0., s.Pos.Y)
        sv.Root.rotation.y <- -s.Angle
        sv.Flame.visible <- s.Thrusting > 0.
        let f = s.Thrusting * (0.8 + 0.3 * sin (t * 40.))
        sv.Flame.scale.set (f, 1., f * 0.7)
        sv.Retro.visible <- s.Reversing
        let r = 0.8 + 0.3 * sin (t * 50.)
        sv.Retro.scale.set (r, 1., r)
        let railing = s.Weapon = Rail && s.Charge > 0.
        sv.Coil.visible <- railing
        sv.Laser.visible <- railing
        sv.Bubble.visible <- s.Weapon = Tractor && s.Charge > 0.
        if sv.Bubble.visible then
            sv.Bubble.scale.set (tractorRange, 1., tractorRange)
            sv.Bubble.material.opacity <- 0.06 + 0.14 * min 1. (s.Charge / railCharge)
        if railing then
            let c = min 1. (s.Charge / railCharge)
            sv.Laser.material.color.setHex (shipColor s)
            sv.Laser.material.opacity <- 0.15 + 0.35 * c
            sv.Coil.scale.set (0.3 + c, 1., 0.3 + c)
            sv.Coil.material.opacity <- 0.4 + 0.6 * c
        sv.Shield.visible <- s.Shield > 0.
        if s.Shield > 0. then
            sv.Shield.material.opacity <- 0.35 + 0.45 * (s.Shield / shieldAmount) + 0.2 * sin (t * 8.)
            sv.Shield.rotation.z <- t * 0.9
        sv.Body.material.opacity <- if s.Invuln > 0. then 0.4 + 0.4 * sin (t * 30.) else 1.
        sv.Body.material.color.setHex (if s.Team > 0 then teamColors.[s.Team] else 0xffffff)
        let k = spriteOf s
        let cx, cy = cells.[k]
        sv.Body.rotation.y <- if k = 3 then Math.PI else 0.
        sv.Body.material?map?offset?set (cx / fst sheet, 1. - (cy + snd cell) / snd sheet)
        sv.Flame.material.color.setHex (shipColor s)
        sv.Trail.material.opacity <- min 1. (0.55 + 0.18 * float s.Streak)
        sv.History.Insert(0, s.Pos)
        if sv.History.Count > trailLen then sv.History.RemoveAt(sv.History.Count - 1)
    else
        sv.History.Clear()
    let attr = sv.Trail.geometry.getAttribute "position"
    let a = attr.array
    for i in 0 .. sv.History.Count - 1 do
        let p = sv.History.[i]
        a.[i * 3] <- p.X
        a.[i * 3 + 1] <- 2.
        a.[i * 3 + 2] <- p.Y
    sv.Trail.geometry.setDrawRange (0, sv.History.Count)
    attr.needsUpdate <- true

let private drawSmoke (vw: View) (w: World) dt =
    w.Ships
    |> Array.iteri (fun i s ->
        if Sim.hurting s then
            vw.Smoke.[i] <- vw.Smoke.[i] + dt
            if vw.Smoke.[i] > 0.07 then
                vw.Smoke.[i] <- 0.
                spawnCone vw s.Pos 0x5a3020 3 40. (atan2 -s.Vel.Y -s.Vel.X) 0.9 7.
        else
            vw.Smoke.[i] <- 0.)

let private drawPads (vw: View) (w: World) =
    Array.iter2
        (fun (m: Mesh) (p: Pad) ->
            let ready = p.RespawnIn <= 0.
            m.material.opacity <- if ready then 0.75 + 0.25 * sin (w.Time * 4.) else 0.12
            let span = padSpan p
            let s = if ready then 1. else 1. - p.RespawnIn / span
            m.scale.set (s, 1., s))
        vw.Pads
        w.Pads

let private drawCrates (vw: View) (w: World) =
    Array.iter2
        (fun (o: Object3D) (c: Crate) ->
            let ready = c.RespawnIn <= 0.
            o.visible <- ready
            if ready then
                o.position.set (c.Pos.X, 0., c.Pos.Y)
                o.rotation.set (w.Time * 0.7, w.Time * 1.3, 0.))
        vw.Crates
        w.Crates

let private drawMines (vw: View) (w: World) =
    let mutable k = 0
    for m in w.Mines do
        if k < vw.Mines.Length then
            let o = vw.Mines.[k]
            o.visible <- true
            o.position.set (m.Pos.X, 3., m.Pos.Y)
            let live = m.Fuse >= 0.
            let s = if live then 1.2 + 0.4 * sin (w.Time * 34.) else 1.
            o.scale.set (s, s, s)
            o.rotation.set (w.Time * 1.1, w.Time * 0.8, 0.)
            k <- k + 1
    for i in k .. vw.Mines.Length - 1 do
        vw.Mines.[i].visible <- false

let private drawBullets (vw: View) (w: World) dt =
    vw.Puff <- vw.Puff + dt
    let puff = vw.Puff > 0.05
    if puff then vw.Puff <- 0.
    let mutable k = 0
    for b in w.Bullets do
        if puff && b.Kind = 2 then
            spawnCone vw b.Pos 0xd8d8d8 4 70. (atan2 -b.Vel.Y -b.Vel.X) 0.7 18.
        if k < vw.Bullets.Length then
            let m = vw.Bullets.[k]
            m.visible <- true
            m.position.set (b.Pos.X, 4., b.Pos.Y)
            m.rotation.y <- -(atan2 b.Vel.Y b.Vel.X)
            let stretch = len b.Vel / bulletSpeed
            m.scale.set (max 0.6 (stretch * 2.2), 1., if b.Kind = 2 then 1.6 else 1.)
            m.material.color.setHex (if b.Kind = 2 then 0xff8a3d else shipColor w.Ships.[b.Owner])
            k <- k + 1
    for i in k .. vw.Bullets.Length - 1 do
        vw.Bullets.[i].visible <- false

let private frameCamera (vw: View) (w: World) dt =
    let alive = w.Ships |> Array.filter (fun s -> s.Alive)
    let lo, hi =
        if alive.Length = 0 then
            v (-arenaHalf) (-arenaHalf), v arenaHalf arenaHalf
        else
            let xs = alive |> Array.map (fun s -> s.Pos.X)
            let ys = alive |> Array.map (fun s -> s.Pos.Y)
            v (Array.min xs) (Array.min ys), v (Array.max xs) (Array.max ys)
    let center = (lo + hi) * 0.5
    let pad = 220.
    let t = tan (20. * Math.PI / 180.)
    let hY = ((hi.Y - lo.Y) / 2. + pad) / t
    let hX = ((hi.X - lo.X) / 2. + pad) / (t * vw.Camera.aspect)
    let h = max hX hY |> max minCamH |> min maxCamH
    let zoomed = (maxCamH - h) / (maxCamH - minCamH)
    let center = center * zoomed
    let k = 1. - exp (-4. * dt)
    vw.Cam <- vw.Cam + (center - vw.Cam) * k
    vw.CamH <- vw.CamH + (h - vw.CamH) * k
    vw.Camera.position.set (vw.Cam.X, vw.CamH, vw.Cam.Y + vw.CamH * 0.3)
    vw.Camera.lookAt (vw.Cam.X, 0., vw.Cam.Y)

let private weaponLabel (s: Ship) =
    match s.Weapon with
    | Blaster -> Strings.t.WBlaster
    | Rail -> Strings.t.Loaded Strings.t.WRail s.Ammo
    | Mines -> Strings.t.Loaded Strings.t.WMines s.Ammo
    | Swarm -> Strings.t.Loaded Strings.t.WSwarm s.Ammo
    | Pulse -> Strings.t.Loaded Strings.t.WPulse s.Ammo
    | Scatter -> Strings.t.Loaded Strings.t.WScatter s.Ammo
    | Tractor -> Strings.t.Loaded Strings.t.WTractor s.Ammo

let private drawHud (vw: View) (w: World) dt =
    w.Ships
    |> Array.iteri (fun i s ->
        let el = vw.Panels.[i]
        if s.Alive && s.Hp < vw.Hp.[i] then
            vw.Shake.[i] <- min 1. (vw.Shake.[i] + (vw.Hp.[i] - s.Hp) / 40.)
        vw.Hp.[i] <- if s.Alive then s.Hp else hpMax
        vw.Shake.[i] <- max 0. (vw.Shake.[i] - dt * 3.4)
        let k = vw.Shake.[i]
        let jolt = k * 9.
        el?style?transform <-
            sprintf
                "translate(%.1fpx,%.1fpx) scale(%.3f)"
                ((rnd.NextDouble() - 0.5) * jolt)
                ((rnd.NextDouble() - 0.5) * jolt)
                (1. + k * 0.09)
        el.className <-
            sprintf
                "panel p%d%s%s%s"
                i
                (if s.Active then "" else " off")
                (if Sim.hurting s then " hurt" else "")
                (if s.Locked > 0. then " cooked" else "")
        el.querySelector(".name")?style?color <- sprintf "#%06x" (shipColor s)
        el.querySelector(".hp i")?style?width <- sprintf "%.0f%%" (max 0. s.Hp / hpMax * 100.)
        el.querySelector(".shield i")?style?width <- sprintf "%.0f%%" (s.Shield / shieldAmount * 100.)
        el.querySelector(".boost i")?style?width <- sprintf "%.0f%%" (s.Boost / boostMax * 100.)
        el.querySelector(".heat i")?style?width <- sprintf "%.0f%%" (s.Heat / heatMax * 100.)
        (el.querySelector ".stocks" :?> HTMLElement).textContent <- String.replicate (max 0 s.Stocks) "◆"
        (el.querySelector ".wep" :?> HTMLElement).textContent <- weaponLabel s)

let private drawTint (vw: View) dt =
    vw.Tint <- max 0. (vw.Tint - dt * 1.6)
    vw.Vignette?style?opacity <- string vw.Tint
    if vw.Tint > 0. then
        vw.Vignette?style?boxShadow <- sprintf "inset 0 0 220px 60px %s" vw.TintHex

let private drawPost (vw: View) dt =
    vw.Spike <- max 0. (vw.Spike - dt * 2.6)
    vw.Bloom.strength <- 1.3 + vw.Spike * 1.7

let draw (vw: View) (w: World) (events: Event list) dt =
    for e in events do
        match e with
        | Hit p -> spawnBurst vw p 0xffffff 10 160.
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
            spawnRing vw p 0xff6a2b mineBlast 1.3 0.45
            vw.Spike <- max vw.Spike 0.8
        | Wave(p, a, i) ->
            spawnCone vw p (shipColor w.Ships.[i]) 34 pulseRange a pulseCone 7.
            spawnRing vw p 0xbfe6ff 120. 2.2 0.4
        | Cooked p ->
            spawnBurst vw p 0xff7b2b 18 120.
            spawnRing vw p 0xff7b2b 26. 2.2 0.5
        | Zap(p, a, _) ->
            spawnBolts vw p 0x9df3ff a (scatterCone * 1.3) (scatterRange * 1.3)
            spawnCone vw p 0xbff6ff 48 (scatterRange * 2.6) a (scatterCone * 1.2) 11.
            vw.Spike <- max vw.Spike 0.4
        | Latch(p, i) -> spawnRing vw p (shipColor w.Ships.[i]) 30. 3. 0.3
        | Explode(p, i, ring) ->
            let hex = shipColor w.Ships.[i]
            let sv = vw.Ships.[i]
            let dir =
                if sv.History.Count > 1 then
                    let d = sv.History.[0] - sv.History.[1]
                    atan2 d.Y d.X
                else
                    rnd.NextDouble() * Math.PI * 2.
            spawnCone vw p hex 46 340. dir 0.8 7.
            spawnCone vw p 0xffffff 22 180. dir Math.PI 7.
            spawnRing vw p hex 40. 3.2 0.6
            vw.Spike <- max vw.Spike 1.
            vw.Tint <- 1.
            vw.TintHex <- sprintf "#%06x" (if ring then 0x3d5cff else hex)
        | Shot _ -> ()
    drawSmoke vw w dt
    Array.iter2 (drawShip w.Time vw) vw.Ships w.Ships
    Array.iter2 (drawTether w.Time w) vw.Ships w.Ships
    Array.iter2 (drawTether w.Time w) vw.Ships w.Ships
    drawPads vw w
    drawCrates vw w
    drawMines vw w
    drawBullets vw w dt
    updateBursts vw dt
    updateFlashes vw dt
    frameCamera vw w dt
    drawHud vw w dt
    drawTint vw dt
    drawPost vw dt
    vw.Composer.render ()
