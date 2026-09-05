module RenderFx

open System
open Vec
open Domain
open Domain.Cfg
open Three
open RenderTypes

let private emit (vw: View) (p: V2) (mat: Material) n speed dir spread life decay =
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
    let pts = three.Points(g, mat)
    vw.Scene.add pts
    vw.Bursts <- { Points = pts; Vel = vel; Decay = decay; Life = life } :: vw.Bursts

let spawnCone (vw: View) (p: V2) hex n speed dir spread size =
    emit
        vw
        p
        (three.PointsMaterial(
            box {| color = hex; size = size; transparent = true; opacity = 1.; blending = three.AdditiveBlending |}
        ))
        n
        speed
        dir
        spread
        1.
        1.4

let private puffTex =
    lazy
        three.loadTexture
            "data:image/svg+xml,<svg xmlns='http://www.w3.org/2000/svg' width='64' height='64'><radialGradient id='g'><stop offset='0' stop-color='white'/><stop offset='0.55' stop-color='white' stop-opacity='0.45'/><stop offset='1' stop-color='white' stop-opacity='0'/></radialGradient><circle cx='32' cy='32' r='32' fill='url(%23g)'/></svg>"

let spawnSmoke (vw: View) (p: V2) n speed size =
    let mat =
        three.PointsMaterial(
            box
                {| color = 0x585c66
                   size = size
                   map = puffTex.Value
                   transparent = true
                   opacity = 0.45
                   depthWrite = false |}
        )
    for _ in 1..n do
        let off = ofAngle (rnd.NextDouble() * Math.PI * 2.) * (rnd.NextDouble() * size * 0.8)
        emit vw (p + off) mat 1 speed 0. Math.PI 0.45 0.24

let spawnBurst (vw: View) (p: V2) hex n speed =
    spawnCone vw p hex n speed 0. Math.PI 7.

let addFlash (vw: View) (m: Mesh) grow span =
    vw.Scene.add m
    vw.Flashes <- { Obj = m; Grow = grow; Span = span; Life = span } :: vw.Flashes

let spawnBolts (vw: View) (p: V2) hex dir spread range =
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

let spawnRing (vw: View) (p: V2) hex r grow span =
    let m = three.Mesh(three.RingGeometry(r * 0.86, r, 44) |> flat, glowMat hex 1.)
    m.position.set (p.X, 3., p.Y)
    addFlash vw m grow span

let spawnShards (vw: View) (p: V2) hex n =
    for k in 1..n do
        let span = 1.1 + rnd.NextDouble() * 0.9
        let m =
            three.Mesh(
                three.CircleGeometry(4. + rnd.NextDouble() * 5., 3) |> flat,
                glowMat (if k % 2 = 0 then hex else 0xff7a1e) 1.
            )
        m.position.set (p.X, 4., p.Y)
        vw.Scene.add m
        vw.Shards <-
            { Obj = m
              Spin = (rnd.NextDouble() - 0.5) * 16.
              Span = span
              Pos = p
              Vel = ofAngle (rnd.NextDouble() * Math.PI * 2.) * (160. + rnd.NextDouble() * 320.)
              Puff = 0.
              Life = span }
            :: vw.Shards

let spawnFlash (vw: View) (p: V2) hex r span =
    let m = three.Mesh(three.RingGeometry(0., r, 32) |> flat, glowMat hex 1.)
    m.position.set (p.X, 3., p.Y)
    addFlash vw m 1.5 span

let edgeHit (a: V2) (d: V2) =
    let axis o dd = if abs dd < 1e-6 then 1e9 else max ((arenaHalf - o) / dd) ((-arenaHalf - o) / dd)
    let chamfer =
        [ for sx in [ 1.; -1. ] do
              for sy in [ 1.; -1. ] do
                  let dd = sx * d.X + sy * d.Y
                  if dd > 1e-6 then yield (diagLimit () - (sx * a.X + sy * a.Y)) / dd ]
        |> function
            | [] -> 1e9
            | ts -> List.min ts
    min (min (axis a.X d.X) (axis a.Y d.Y)) chamfer |> max 0.

let spawnBeam (vw: View) (a: V2) (b: V2) hex =
    let d = norm (b - a)
    let far = a + d * edgeHit a d
    let mid = (a + far) * 0.5
    let m = three.Mesh(three.PlaneGeometry(len (far - a), 9.) |> flat, glowMat hex 1.)
    m.position.set (mid.X, 5., mid.Y)
    m.rotation.y <- -(atan2 d.Y d.X)
    addFlash vw m 0. 0.32

let updateBursts (vw: View) dt =
    vw.Bursts <-
        vw.Bursts
        |> List.filter (fun b ->
            b.Life <- b.Life - dt * b.Decay
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

let updateFlashes (vw: View) dt =
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

let updateShards (vw: View) dt =
    vw.Shards <-
        vw.Shards
        |> List.filter (fun s ->
            s.Life <- s.Life - dt
            if s.Life <= 0. then
                vw.Scene.remove s.Obj
                false
            else
                s.Vel <- s.Vel * max 0. (1. - 1.5 * dt)
                s.Pos <- s.Pos + s.Vel * dt
                s.Obj.position.set (s.Pos.X, 4., s.Pos.Y)
                s.Obj.rotation.y <- s.Obj.rotation.y + s.Spin * dt
                s.Obj.material.opacity <- min 1. (2. * s.Life / s.Span)
                s.Puff <- s.Puff - dt
                if s.Puff <= 0. then
                    s.Puff <- 0.1
                    spawnCone vw s.Pos 0xff8a2b 2 26. 0. Math.PI 6.
                    spawnSmoke vw s.Pos 1 16. 30.
                true)
