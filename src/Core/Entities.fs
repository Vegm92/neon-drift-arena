module Entities

open System
open Vec
open Domain
open Domain.Cfg
open State
open Track
open Arena
open Combat
open Weapons

let private blocked (rocks: Rock list) (p: V2) =
    asteroids |> Array.exists (fun a -> len (a.Pos - p) < a.Radius)
    || rocks |> List.exists (fun r -> len (r.Pos - p) < r.Radius)

let steer dt (ships: Ship[]) (b: Bullet) =
    if b.Kind <> 2 then
        b
    else
        match nearest ships b.Owner b.Pos with
        | Some _ when race -> b
        | Some t ->
            let a0 = atan2 b.Vel.Y b.Vel.X
            let a1 = atan2 (t.Pos.Y - b.Pos.Y) (t.Pos.X - b.Pos.X)
            let d = atan2 (sin (a1 - a0)) (cos (a1 - a0))
            let lim = seekerTurn * dt
            { b with Vel = ofAngle (a0 + max -lim (min lim d)) * seekerSpeed }
        | None -> b

let stepBullet k rocks dt (b: Bullet) =
    let b = { b with Pos = b.Pos + b.Vel * dt; Life = b.Life - dt }
    if b.Life <= 0. || outOfBoundsAt k b.Pos || blocked rocks b.Pos then None else Some b

let stepPortals k dt sudden (ships: Ship[]) rng (w: World) =
    let live = w.Portals |> List.choose (fun p -> if p.Life <= dt then None else Some { p with Life = p.Life - dt })
    // One pair at a time: a new gate waits for the live one to close, whatever portalEvery is tuned to.
    if w.PortalIn > dt || sudden || not live.IsEmpty then
        live, max 0. (w.PortalIn - dt), rng, []
    else
        let a, r1 = freeSpot k ships [] rng
        let b, r2 = freeSpot k ships [ a ] r1
        { A = a; B = b; Life = portalLife } :: live, portalEvery, r2, [ PortalOpen(a, b) ]

let stepHole k dt sudden (ships: Ship[]) rng (w: World) =
    let live = w.Hole |> Option.filter (fun h -> h.Life > dt) |> Option.map (fun h -> { h with Life = h.Life - dt })
    if w.HoleIn > dt || sudden || live.IsSome then
        live, max 0. (w.HoleIn - dt), rng, []
    else
        let p, r = freeSpot k ships (w.Portals |> List.collect (fun g -> [ g.A; g.B ])) rng
        Some { Pos = p; Life = holeLife }, holeEvery, r, [ HoleOpen p ]

let pull (hole: Hole option) dt (p: V2) (vel: V2) =
    match hole with
    | Some h ->
        let d = h.Pos - p
        let r = max (holeCoreNow ()) (len d)
        vel + norm d * (holeGNow () / (r * r) * dt)
    | None -> vel

let resolveHole (hole: Hole option) dt (ships: Ship[]) =
    ships
    |> Array.map (fun s ->
        match hole with
        | Some h when s.Alive && len (h.Pos - s.Pos) < holeCoreNow () -> { s with Hp = 0.; LastWeapon = Singularity }
        | Some _ when s.Alive -> { s with Vel = pull hole dt s.Pos s.Vel }
        | _ -> s)

let warpAt (portals: Portal list) (p: V2) (vel: V2) =
    portals
    |> List.tryPick (fun g ->
        let jump (src: V2) (dst: V2) =
            let dir = if len vel > 1e-6 then norm vel else norm (dst - src)
            Some(dst + dir * (portalRadius + 4.))
        if len (p - g.A) < portalRadius then jump g.A g.B
        elif len (p - g.B) < portalRadius then jump g.B g.A
        else None)

let resolveWarps dt (portals: Portal list) (ships: Ship[]) =
    let events = ResizeArray()
    let s =
        ships
        |> Array.map (fun sh ->
            let sh = { sh with WarpCd = max 0. (sh.WarpCd - dt) }
            if sh.Alive && sh.WarpCd <= 0. then
                match warpAt portals sh.Pos sh.Vel with
                | Some p ->
                    events.Add(Warp sh.Pos)
                    events.Add(Warp p)
                    { sh with Pos = p; WarpCd = 0.5 }
                | None -> sh
            else sh)
    s, List.ofSeq events

let stepRocks k dt (rocks: Rock list) =
    let events = ResizeArray()
    let live =
        rocks
        |> List.choose (fun r ->
            let r = { r with Pos = r.Pos + r.Vel * dt; Life = r.Life - dt }
            let hitRock = asteroids |> Array.exists (fun a -> len (a.Pos - r.Pos) < a.Radius + r.Radius)
            if hitRock then events.Add(Bump r.Pos)
            if r.Life <= 0. || hitRock || outOfBoundsAt k r.Pos then None else Some r)
    live, List.ofSeq events

let resolveBeams (ships: Ship[]) beams =
    let s = Array.copy ships
    let events = ResizeArray()
    for (a, b, owner) in beams do
        let dir = norm (b - a)
        for i in 0 .. s.Length - 1 do
            if s.[i].Alive && side s.[i] <> side s.[owner] && segDist a b s.[i].Pos < shipRadius + 6. then
                events.Add(Hit(s.[i].Pos, "Rail", railDamage))
                if s.[i].Invuln <= 0. then
                    s.[owner] <- { s.[owner] with Hits = s.[owner].Hits + 1 }
                s.[i] <- { s.[i] with Vel = s.[i].Vel + dir * bulletKnockback } |> tag owner Rail |> damage railDamage
    s, List.ofSeq events

let private inCone (s: Ship[]) p a range cone owner i =
    let d = s.[i].Pos - p
    let dist = len d
    if s.[i].Alive && side s.[i] <> side s.[owner] && dist < range && dist > 1e-6 then
        let rel = atan2 d.Y d.X - a
        let off = atan2 (sin rel) (cos rel)
        if abs off < cone then Some(norm d, 1. - dist / range) else None
    else
        None

let resolveWaves (ships: Ship[]) waves =
    let s = Array.copy ships
    for (p, a, owner) in waves do
        for i in 0 .. s.Length - 1 do
            match inCone s p a pulseRange pulseCone owner i with
            | Some(n, f) -> s.[i] <- { s.[i] with Vel = s.[i].Vel + n * (pulseForce * f) } |> tag owner Pulse
            | None -> ()
    s

let resolveZaps (ships: Ship[]) zaps =
    let s = Array.copy ships
    let events = ResizeArray()
    for (p, a, owner) in zaps do
        for i in 0 .. s.Length - 1 do
            match inCone s p a scatterRange scatterCone owner i with
            | Some _ ->
                events.Add(Hit(s.[i].Pos, "Scatter", scatterDamage))
                if s.[i].Invuln <= 0. then
                    s.[owner] <- { s.[owner] with Hits = s.[owner].Hits + 1 }
                s.[i] <- { s.[i] with Stun = max s.[i].Stun scatterStun; Thrusting = 0. } |> tag owner Scatter |> damage scatterDamage
            | None -> ()
    s, List.ofSeq events

let private acquire (s: Ship[]) owner =
    let me = s.[owner]
    let ahead (p: V2) radius tow =
        let d = p - me.Pos
        let rel = atan2 d.Y d.X - me.Angle
        let off = abs (atan2 (sin rel) (cos rel))
        if len d - radius < tractorRange && off < tractorCone then Some(off, tow) else None
    let ships = s |> Array.choose (fun t -> if t.Alive && side t <> side me then ahead t.Pos 0. (TowShip t.Id) else None)
    let rocks = asteroids |> Array.mapi (fun k a -> ahead a.Pos a.Radius (TowRock k)) |> Array.choose id
    match (if ships.Length > 0 then ships else rocks) with
    | [||] -> NoTether
    | found -> snd (Array.minBy fst found)

let resolveTows dt (ships: Ship[]) latches =
    let s = Array.copy ships
    for owner in latches do
        if s.[owner].Tow = NoTether then
            match acquire s owner with
            | NoTether -> ()
            | t -> s.[owner] <- { spend s.[owner] with Tow = t; TowLeft = tractorTime }
    for i in 0 .. s.Length - 1 do
        let me = s.[i]
        let drop () = s.[i] <- { s.[i] with Tow = NoTether; TowLeft = 0. }
        let pull (from: V2) (target: V2) = norm (target - from) * (tractorPull * dt)
        if me.Tow <> NoTether then
            if not me.Alive || me.TowLeft <= dt then
                drop ()
            else
                match me.Tow with
                | TowShip j when s.[j].Alive && len (s.[j].Pos - me.Pos) < tractorRange * 1.3 ->
                    s.[j] <- { s.[j] with Vel = s.[j].Vel + pull s.[j].Pos me.Pos } |> tag i Tractor
                    s.[i] <- { s.[i] with TowLeft = me.TowLeft - dt }
                | TowRock k when len (asteroids.[k].Pos - me.Pos) > asteroids.[k].Radius + 2. * shipRadius ->
                    s.[i] <- { me with Vel = me.Vel + pull me.Pos asteroids.[k].Pos; TowLeft = me.TowLeft - dt }
                | _ -> drop ()
    s

let stepMines k dt (ships: Ship[]) (mines: Mine list) =
    let events = ResizeArray()
    let blasts = ResizeArray()
    let kept =
        mines
        |> List.choose (fun m ->
            let near = nearest ships m.Owner m.Pos
            if race then
                match near with
                | Some t when len (t.Pos - m.Pos) < mineRadius + shipRadius ->
                    blasts.Add(m.Pos, m.Owner)
                    events.Add(Blast m.Pos)
                    None
                | _ -> Some m
            elif m.Fuse < 0. then
                match near with
                | Some t when len (t.Pos - m.Pos) < mineMagnet ->
                    events.Add(MineLive m.Pos)
                    Some { m with Fuse = mineFuse }
                | _ -> Some m
            elif m.Fuse <= dt || blocked [] m.Pos || outOfBoundsAt k m.Pos then
                blasts.Add(m.Pos, m.Owner)
                events.Add(Blast m.Pos)
                None
            else
                let pull =
                    match near with
                    | Some t -> norm (t.Pos - m.Pos) * (mineChase * dt)
                    | None -> zero
                let vel = (m.Vel + pull) * (1. - 0.9 * dt) |> clampLen 260.
                Some { m with Fuse = m.Fuse - dt; Vel = vel; Pos = m.Pos + vel * dt })
    kept, List.ofSeq blasts, List.ofSeq events

let resolveBlasts (ships: Ship[]) blasts =
    let s = Array.copy ships
    let events = ResizeArray()
    for (p, owner) in blasts do
        for i in 0 .. s.Length - 1 do
            let d = s.[i].Pos - p
            let dist = len d
            if s.[i].Alive && dist < mineBlast then
                let f = 1. - dist / mineBlast
                let dmg = mineDamage * f
                if dmg > 0. then
                    events.Add(Hit(s.[i].Pos, "Mines", dmg))
                s.[i] <- { s.[i] with Vel = s.[i].Vel + norm d * (pulseForce * f) } |> tag owner Mines |> damage dmg
    s, List.ofSeq events