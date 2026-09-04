module RenderFx

open System
open Vec
open Domain
open Domain.Cfg
open Three
open RenderTypes

let spawnCone (vw: View) (p: V2) hex n speed dir spread size =
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
