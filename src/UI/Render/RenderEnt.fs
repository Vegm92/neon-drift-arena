module RenderEnt

open System
open Browser
open Browser.Types
open Fable.Core.JsInterop
open Vec
open Domain
open Domain.Cfg
open Three
open RenderTypes
open RenderFx

let drawTether t (w: World) (sv: ShipView) (s: Ship) =
    let target =
        match s.Tow with
        | TowShip j when s.Alive -> Some w.Ships.[j].Pos
        | TowRock k when s.Alive -> Some State.asteroids.[k].Pos
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

let drawShip t (vw: View) (sv: ShipView) (s: Ship) =
    let launcher = Sim.launcher s
    sv.Root.visible <- s.Alive
    sv.Mark.visible <- launcher
    sv.Vent.visible <- s.Alive && s.Heat > 0.001
    if sv.Vent.visible then
        let h = min 1. (s.Heat / heatMax)
        let c = 43 + int (212. * h)
        sv.Vent.position.set (s.Pos.X, 3., s.Pos.Y)
        sv.Vent.geometry.setDrawRange (0, int (float ventSegments * h) * 6)
        sv.Vent.material.color.setHex (0xff0000 ||| (c <<< 8) ||| c)
        sv.Vent.material.opacity <- (if s.Locked > 0. then 0.7 + 0.3 * sin (t * 16.) else 0.3)
        if s.Locked > 0. && rnd.NextDouble() < 0.35 then
            let n = ofAngle (s.Angle + (if rnd.NextDouble() < 0.5 then 1. else -1.) * Math.PI / 2.)
            spawnCone vw (s.Pos + n * (shipRadius + 2.)) 0xffc0a0 1 70. (atan2 n.Y n.X) 0.35 6.
    if launcher then
        let k = 2.5 * vw.CamH / maxCamH ()
        sv.Mark.position.set (s.Pos.X, 3., s.Pos.Y)
        sv.Mark.scale.set (k, 1., k)
        sv.Mark.rotation.y <- -s.Angle
        sv.Mark.material.color.setHex (shipColor s)
        sv.Mark.material.opacity <- if s.LaunchCd <= 0. then 0.6 + 0.4 * sin (t * 6.) else 0.25
        sv.History.Clear()
    elif s.Alive then
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

let private threat (w: World) (me: Ship) =
    let foe (owner: int) = Sim.side w.Ships.[owner] <> Sim.side me
    let toward (p: V2) f = let d = p - me.Pos in atan2 d.Y d.X, f
    let bullets =
        w.Bullets
        |> List.choose (fun b ->
            let rel = me.Pos - b.Pos
            let dir = norm b.Vel
            let along = dot rel dir
            if foe b.Owner && along > 0. && along < 620. && len (rel - dir * along) < 60. then
                Some(toward b.Pos (1. - along / 620.))
            else None)
    let rocks =
        w.Rocks
        |> List.choose (fun r ->
            let rel = me.Pos - r.Pos
            let dir = norm r.Vel
            let along = dot rel dir
            if along > 0. && along < 700. && len (rel - dir * along) < r.Radius + 2. * shipRadius then
                Some(toward r.Pos (1. - along / 700.))
            else None)
    let mines =
        w.Mines
        |> List.choose (fun m ->
            let d = len (m.Pos - me.Pos)
            if foe m.Owner && m.Fuse >= 0. && d < mineMagnet * 2.5 then Some(toward m.Pos (1. - d / (mineMagnet * 2.5))) else None)
    let charging =
        w.Ships
        |> Array.toList
        |> List.choose (fun s ->
            let d = me.Pos - s.Pos
            let dist = len d
            let reach = if s.Weapon = Rail then 4. * arenaHalf else tractorRange * 1.2
            let rel = atan2 d.Y d.X - s.Angle
            if s.Alive && s.Charge > 0. && foe s.Id && dist < reach && abs (atan2 (sin rel) (cos rel)) < 0.35 then
                Some(toward s.Pos (0.5 + 0.5 * min 1. (s.Charge / railCharge)))
            else None)
    let hole =
        match w.Hole with
        | Some h when len (h.Pos - me.Pos) < State.holeCoreNow () * 12. -> [ toward h.Pos (1. - len (h.Pos - me.Pos) / (State.holeCoreNow () * 12.)) ]
        | _ -> []
    match bullets @ rocks @ mines @ charging @ hole with
    | [] when State.race -> Some(toward State.gates.[me.Next] 0.5, true)
    | [] -> None
    | ts -> Some(List.maxBy snd ts, false)

let drawWarn t (w: World) (sv: ShipView) (s: Ship) =
    let hit = if s.Alive then threat w s else None
    sv.Warn.visible <- hit.IsSome
    match hit with
    | Some((a, f), guide) ->
        sv.Warn.position.set (s.Pos.X, 3., s.Pos.Y)
        sv.Warn.rotation.y <- -a
        sv.Warn.material.color.setHex (if guide then portalHex else 0xff3b5c)
        sv.Warn.material.opacity <- if guide then 0.7 else (0.25 + 0.75 * f) * (0.7 + 0.3 * sin (t * 18.))
    | None -> ()

let drawSmoke (vw: View) (w: World) dt =
    w.Ships
    |> Array.iteri (fun i s ->
        if Sim.hurting s then
            vw.Smoke.[i] <- vw.Smoke.[i] + dt
            if vw.Smoke.[i] > 0.07 then
                vw.Smoke.[i] <- 0.
                spawnSmoke vw s.Pos 2 26. 22.
        else
            vw.Smoke.[i] <- 0.)

let drawBorder (vw: View) (w: World) =
    let k = Sim.bounds w.Time
    vw.Border.scale.set (k, 1., k)
    let closing = Sim.sudden w && k > shrinkMin
    (vw.Border.children.[0] :?> Mesh).material.color.setHex (if closing then 0xff3b5c else 0x00f6ff)
    let left = if State.race then w.Time else max 0. (matchTime - w.Time)
    vw.Clock.className <- if Sim.sudden w then "sudden" else ""
    vw.Clock.textContent <-
        if Sim.sudden w then Strings.t.SuddenDeath
        else sprintf "%d:%02d" (int left / 60) (int left % 60)

let drawPads (vw: View) (w: World) =
    let sudden = Sim.sudden w
    Array.iter2
        (fun (m: Mesh) (p: Pad) ->
            let ready = p.RespawnIn <= 0. && not (sudden && p.Kind = 1)
            m.material.opacity <- if ready then 0.75 + 0.25 * sin (w.Time * 4.) else 0.12
            let span =
                match p.Kind with
                | 1 -> healRespawn
                | 2 -> shieldRespawn
                | _ -> padRespawn
            let s = if ready then 1. else 1. - p.RespawnIn / span
            m.scale.set (s, 1., s))
        vw.Pads
        w.Pads

let drawCrates (vw: View) (w: World) =
    Array.iter2
        (fun (o: Object3D) (c: Crate) ->
            let ready = c.RespawnIn <= 0.
            o.visible <- ready
            if ready then
                o.position.set (c.Pos.X, 0., c.Pos.Y)
                o.rotation.set (w.Time * 0.7, w.Time * 1.3, 0.))
        vw.Crates
        w.Crates

let drawRocks (vw: View) (w: World) dt =
    let puff = vw.Puff + dt > 0.05
    let mutable k = 0
    for r in w.Rocks do
        if puff then spawnCone vw r.Pos 0xff9955 4 70. (atan2 -r.Vel.Y -r.Vel.X) 0.7 18.
        if k < vw.Boulders.Length then
            let o = vw.Boulders.[k]
            o.visible <- true
            o.position.set (r.Pos.X, 0., r.Pos.Y)
            o.rotation.set (r.Life * 1.7, r.Life * 1.1, 0.)
            k <- k + 1
    for i in k .. vw.Boulders.Length - 1 do
        vw.Boulders.[i].visible <- false

let drawPortals (vw: View) (w: World) =
    let mutable k = 0
    for g in w.Portals do
        for e in [ g.A; g.B ] do
            for inner in [ false; true ] do
                if k < vw.Gates.Length then
                    let m = vw.Gates.[k]
                    m.visible <- true
                    m.position.set (e.X, 2., e.Y)
                    m.rotation.z <- (if inner then -2.4 else 0.8) * w.Time
                    let fade = min 1. (g.Life / 1.5) * min 1. ((portalLife - g.Life) * 3.)
                    m.material.opacity <- fade * (if inner then 0.9 else 0.55 + 0.25 * sin (w.Time * 5.))
                    k <- k + 1
    for i in k .. vw.Gates.Length - 1 do
        vw.Gates.[i].visible <- false

let drawHole (vw: View) (w: World) dt =
    match w.Hole with
    | Some h ->
        let core = State.holeCoreNow ()
        vw.Hole.visible <- true
        vw.Hole.position.set (h.Pos.X, 0., h.Pos.Y)
        vw.Hole.scale.set (core / holeCore, 1., core / holeCore)
        let fade =
            if Double.IsInfinity h.Life then 1.
            else min 1. (h.Life / 1.5) * min 1. ((holeLife - h.Life) * 2.)
        vw.Horizon.material.opacity <- fade * (0.85 + 0.15 * sin (w.Time * 7.))
        vw.Horizon.rotation.y <- w.Time * 1.5
        vw.Halo.material.opacity <- fade * 0.2
        vw.Halo.rotation.y <- -w.Time * 0.4
        if vw.Puff + dt > 0.05 then
            let a = rnd.NextDouble() * Math.PI * 2.
            spawnCone vw (h.Pos + ofAngle a * (State.holeCoreNow () * 8.)) holeHex 3 260. (a + Math.PI + 0.5) 0.25 14.
    | None -> vw.Hole.visible <- false

let drawMines (vw: View) (w: World) =
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

let drawBullets (vw: View) (w: World) dt =
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
