module RenderMeshes

open System
open Browser
open Browser.Types
open Fable.Core.JsInterop
open Vec
open Domain
open Domain.Cfg
open Three
open RenderTypes

let private hulls = [| "phantom", Math.PI / 2.; "shadow", Math.PI; "star-a", Math.PI; "star-b", Math.PI |]
let private shipLength = 64.

let mkTrail () =
    let g = three.BufferGeometry()
    g.setAttribute ("position", three.Float32BufferAttribute(Array.zeroCreate (trailLen * 3), 3))
    let cols =
        Array.init (trailLen * 3) (fun i ->
            let f = 1. - float (i / 3) / float (trailLen - 1)
            f * f)
    g.setAttribute ("color", three.Float32BufferAttribute(cols, 3))
    g.setDrawRange (0, 0)
    three.Line(
        g,
        three.LineBasicMaterial(
            box {| vertexColors = true; transparent = true; opacity = 0.9; blending = three.AdditiveBlending |}
        )
    )

let mkShip (scene: Object3D) i =
    let hex = colors.[i]
    let root = three.Group()
    let body = three.Group()
    body.position.y <- 1.
    let name, yaw = hulls.[i % hulls.Length]
    loadGltf ("/models/" + name + ".glb") (fun hull ->
        hull.rotation.y <- yaw
        let size = three.sizeOf hull
        let mid = three.centerOf hull
        let k = shipLength / size.x
        hull.scale.set (k, k, k)
        hull.position.set (-mid.x * k, -mid.y * k, -mid.z * k)
        hull.traverse (fun o -> if o?isMesh then o?material?transparent <- true)
        body.add hull)
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
    let vent = three.Mesh(three.RingGeometry(shipRadius + 3., shipRadius + 6., ventSegments) |> flat, glowMat 0xffffff 0.9)
    vent.position.y <- 3.
    vent.visible <- false
    scene.add vent
    let warn = three.Mesh(three.Arc(shipRadius + 15., shipRadius + 19., 20, -0.65, 1.3) |> flat, glowMat 0xff3b5c 0.8)
    warn.position.y <- 3.
    warn.visible <- false
    scene.add warn
    let mark = three.Mesh(three.Arc(30., 38., 20, -0.9, 1.8) |> flat, glowMat hex 0.9)
    mark.position.y <- 3.
    mark.visible <- false
    scene.add mark
    let trail = mkTrail ()
    let tether = three.Mesh(three.PlaneGeometry(1., 4.) |> flat, glowMat hex 0.7)
    tether.visible <- false
    scene.add tether
    scene.add root
    scene.add trail
    { Shield = shield
      Vent = vent
      Root = root
      Body = body
      Flame = flame
      Retro = retro
      Coil = coil
      Laser = laser
      Tether = tether
      Bubble = bubble
      Warn = warn
      Mark = mark
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

let mkPad (scene: Object3D) (p: Pad) =
    let inner, seg =
        match p.Kind with
        | 1 -> padRadius - 12., 40
        | 2 -> padRadius * 0.42, 6
        | _ -> padRadius - 5., 40
    let m = three.Mesh(three.RingGeometry(inner, padRadius, seg) |> flat, glowMat (padHex p) 1.)
    m.position.set (p.Pos.X, 0.5, p.Pos.Y)
    scene.add m
    m

let mkMine (scene: Object3D) =
    let g = three.IcosahedronGeometry(mineRadius, 0)
    let root = three.Group()
    root.add (three.Mesh(g, three.MeshBasicMaterial(box {| color = 0x120308 |})))
    root.add (three.LineSegments(three.EdgesGeometry g, lineMat 0xff2b4d 1.))
    root.visible <- false
    scene.add root
    root

let mkCrate (scene: Object3D) =
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

let mkAsteroid (scene: Object3D) (fill: int) (edge: int) (a: Asteroid) =
    let g = three.IcosahedronGeometry(a.Radius, 1)
    let root = three.Group()
    root.add (three.Mesh(g, three.MeshBasicMaterial(box {| color = fill |})))
    root.add (three.LineSegments(three.EdgesGeometry g, lineMat edge 0.8))
    root.position.set (a.Pos.X, 0., a.Pos.Y)
    root.rotation.set (a.Pos.X * 0.01, a.Pos.Y * 0.01, 0.)
    scene.add root
    root

let mkGate (scene: Object3D) i =
    let inner = i % 2 = 1
    let r = if inner then portalRadius * 0.55 else portalRadius
    let m = three.Mesh(three.RingGeometry(r - 4., r, (if inner then 6 else 40)) |> flat, glowMat portalHex 0.9)
    m.position.y <- 2.
    m.visible <- false
    scene.add m
    m

let mkHole (scene: Object3D) =
    let root = three.Group()
    let disc = three.Mesh(three.CircleGeometry(holeCore * 3., 48) |> flat, three.MeshBasicMaterial(box {| color = 0x000000 |}))
    disc.position.y <- 1.
    root.add disc
    let horizon = three.Mesh(three.RingGeometry(holeCore * 3., holeCore * 3. + 6., 48) |> flat, glowMat holeHex 1.)
    horizon.position.y <- 2.
    root.add horizon
    let halo = three.Mesh(three.RingGeometry(holeCore * 9., holeCore * 9. + 3., 6) |> flat, glowMat holeHex 0.2)
    halo.position.y <- 2.
    root.add halo
    root.visible <- false
    scene.add root
    root, horizon, halo

let mkBullet (scene: Object3D) =
    let m = three.Mesh(three.PlaneGeometry(16., 3.) |> flat, glowMat 0xffffff 1.)
    m.visible <- false
    scene.add m
    m

let private octagon (scale: float) =
    let a = arenaHalf * scale
    let c = (diagLimit () - arenaHalf) * scale
    [| for x, y in [ a, c; c, a; -c, a; -a, c; -a, -c; -c, -a; c, -a; a, -c ] do
           yield! [ x; 0.; y ] |]

let private mkLoop (scene: Object3D) scale hex opacity y =
    let g = three.BufferGeometry()
    g.setAttribute ("position", three.Float32BufferAttribute(octagon scale, 3))
    let l = three.LineLoop(g, lineMat hex opacity)
    l.position.y <- y
    scene.add l
    l

let mkFrame (scene: Object3D) =
    let frame = three.Group()
    let border = three.Group()
    mkLoop border 1. 0x00f6ff 1. 0. |> ignore
    mkLoop border 0.985 0xff2bd6 0.35 0. |> ignore
    frame.add border
    mkLoop frame 0.62 0x15294d 0.7 -0.5 |> ignore
    let cut = diagLimit () - arenaHalf
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
            frame.add m
    let core = three.Group()
    core.add (three.Mesh(three.RingGeometry(96., 100., 6) |> flat, glowMat 0xfff45c 0.8))
    core.add (three.Mesh(three.RingGeometry(150., 152., 48) |> flat, glowMat 0x00f6ff 0.35))
    core.position.y <- -0.5
    frame.add core
    let spawns =
        Array.init 4 (fun i ->
            let p = Sim.spawnPos i
            let m = three.Mesh(three.RingGeometry(120., 124., 8) |> flat, glowMat colors.[playerColor.[i]] 0.5)
            m.position.set (p.X, -0.5, p.Y)
            m.rotation.z <- Math.PI / 8.
            frame.add m
            m)
    scene.add frame
    border, spawns, frame

let mkArena (scene: Object3D) =
    let border, spawns, frame = mkFrame scene
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
    border, spawns, frame

let private mkRoad (scene: Object3D) =
    let g = State.road
    let n = g.Length
    let half = State.trackWidth / 2.
    let edge (side: float) =
        [| for i in 0 .. n - 1 do
               let d = norm (g.[(i + 1) % n] - g.[(i + n - 1) % n])
               let p = g.[i] + v -d.Y d.X * (side * half)
               yield! [ p.X; -0.5; p.Y ] |]
    let road = three.Group()
    for side, hex, alpha in [ 1., 0x00f6ff, 0.55; -1., 0xff2bd6, 0.55 ] do
        let geo = three.BufferGeometry()
        geo.setAttribute ("position", three.Float32BufferAttribute(edge side, 3))
        road.add (three.LineLoop(geo, lineMat hex alpha))
    let d = norm (g.[1] - g.[0])
    let a, b = g.[0] + v -d.Y d.X * half, g.[0] - v -d.Y d.X * half
    let start = three.BufferGeometry()
    start.setAttribute ("position", three.Float32BufferAttribute([| a.X; -0.4; a.Y; b.X; -0.4; b.Y |], 3))
    road.add (three.Line(start, lineMat 0xfff45c 1.))
    for i in 1 .. n - 1 do
        let d = norm (g.[(i + 1) % n] - g.[i - 1])
        let a, b = g.[i] + v -d.Y d.X * (half * 0.25), g.[i] - v -d.Y d.X * (half * 0.25)
        let tick = three.BufferGeometry()
        tick.setAttribute ("position", three.Float32BufferAttribute([| a.X; -0.5; a.Y; b.X; -0.5; b.Y |], 3))
        road.add (three.Line(tick, lineMat 0x9fb3ff 0.25))
    scene.add road
    road

let private mkMark (scene: Object3D) i (p: V2) =
    let m = three.Mesh(three.RingGeometry(gateRadius - 6., gateRadius, 48) |> flat, glowMat (if i = 0 then 0xfff45c else portalHex) 0.2)
    m.position.set (p.X, 1.5, p.Y)
    scene.add m
    m

let syncArena (vw: View) =
    if vw.Layout <> State.layout then
        vw.Layout <- State.layout
        if vw.Size <> arenaHalf then
            vw.Size <- arenaHalf
            vw.Scene.remove vw.Frame
            let border, spawns, frame = mkFrame vw.Scene
            vw.Border <- border
            vw.Spawns <- spawns
            vw.Frame <- frame
        vw.Road |> Option.iter vw.Scene.remove
        vw.Road <- if State.road.Length > 1 then Some(mkRoad vw.Scene) else None
        for m in vw.Marks do
            vw.Scene.remove m
        vw.Marks <- State.gates |> Array.mapi (mkMark vw.Scene)
        for o in vw.Rocks do
            vw.Scene.remove o
        for m in vw.Pads do
            vw.Scene.remove m
        vw.Rocks <- State.asteroids |> Array.map (mkAsteroid vw.Scene 0x05060f 0x6a7cff)
        vw.Pads <- Sim.initial.Pads |> Array.map (mkPad vw.Scene)

let mkPanel (hud: HTMLElement) i =
    let el = document.createElement "div"
    el.className <- sprintf "panel p%d off" i
    el.innerHTML <-
        sprintf
            "<div class=\"p-header\"><span class=\"name\" style=\"color:#%06x\">P%d</span><div class=\"stocks\"></div></div><div class=\"p-body\"><div class=\"bar-label\">HP</div><div class=\"bar hp\"><i></i></div><div class=\"bar-label\">SHIELD</div><div class=\"bar shield\"><i></i></div><div class=\"bar-label\">HEAT</div><div class=\"bar heat\"><i></i></div><div class=\"bar-label\">BOOST</div><div class=\"bar boost\"><i></i></div></div><div class=\"p-footer\"><div class=\"wep\"></div><div class=\"medals\"></div></div>"
            colors.[playerColor.[i]]
            (i + 1)
    hud.appendChild el |> ignore
    el

let mkTag (hud: HTMLElement) i =
    let el = document.createElement "div"
    el.className <- "tag"
    el.textContent <- Strings.t.Player i
    el.hidden <- true
    hud.appendChild el |> ignore
    el
