module Collisions

open Vec
open Domain
open Domain.Cfg
open State
open Track
open Combat
open Entities

let private bump (events: ResizeArray<Event>) (sh: Ship) (a: Asteroid) =
    let d = sh.Pos - a.Pos
    let dist = len d
    let minDist = a.Radius + shipRadius
    if dist < minDist && dist > 1e-6 then
        let n = d * (1. / dist)
        let vn = dot sh.Vel n
        let turn = if sh.Vel.X * n.Y - sh.Vel.Y * n.X > 0. then 1. else -1.
        events.Add(Bump(a.Pos + n * a.Radius))
        { sh with
            Pos = a.Pos + n * minDist
            Vel = if vn < 0. then sh.Vel - n * ((1. + restitution) * vn) else sh.Vel
            Stun = asteroidStun
            Spin = turn * asteroidSpin
            Thrusting = 0. }
    else
        sh

let resolveAsteroids (ships: Ship[]) =
    let events = ResizeArray()
    let s = ships |> Array.map (fun sh -> if sh.Alive then Array.fold (bump events) sh asteroids else sh)
    s, List.ofSeq events

let resolveRocks (ships: Ship[]) (rocks: Rock list) =
    let events = ResizeArray()
    let hit (sh: Ship) (r: Rock) =
        let d = sh.Pos - r.Pos
        if len d < r.Radius + shipRadius && len d > 1e-6 then
            let n = norm d
            let closing = min 0. (dot (sh.Vel - r.Vel) n)
            let mark s = if side s <> side ships.[r.Owner] then tag r.Owner Rock s else s
            let bumped = bump events sh { Pos = r.Pos; Radius = r.Radius }
            { bumped with Vel = bumped.Vel + r.Vel * 0.5 } |> mark |> damage (abs closing * rockDamage)
        else
            sh
    let s = ships |> Array.map (fun sh -> if sh.Alive then List.fold hit sh rocks else sh)
    s, List.ofSeq events

let resolveDeploys (ships: Ship[]) (deploys: Deployable list) (bullets: Bullet list) =
    let events = ResizeArray()
    let ds = List.toArray deploys
    let s = Array.copy ships
    for i in 0 .. ds.Length - 1 do
        if ds.[i].Kind = wallKind then
            let a, b = wallEnds ds.[i]
            for j in 0 .. s.Length - 1 do
                let sh = s.[j]
                if sh.Alive then
                    let cp = segClosest a b sh.Pos
                    let d = sh.Pos - cp
                    let dist = len d
                    let minDist = wallThick + shipRadius
                    if dist < minDist && dist > 1e-6 then
                        let n = d * (1. / dist)
                        let vn = dot sh.Vel n
                        s.[j] <-
                            { sh with
                                Pos = cp + n * minDist
                                Vel = if vn < 0. then sh.Vel - n * ((1. + restitution) * vn) else sh.Vel }
    let stopper (b: Bullet) =
        ds
        |> Array.tryFindIndex (fun d ->
            if d.Kind = wallKind then
                let a, e = wallEnds d
                segDist a e b.Pos < wallThick + 4.
            elif d.Kind = turretKind then
                sideOf ships d.Owner <> sideOf ships b.Owner && len (d.Pos - b.Pos) < sentryRadius + 4.
            else
                false)
    let remaining =
        bullets
        |> List.filter (fun b ->
            match stopper b with
            | Some i ->
                if ds.[i].Kind = turretKind then
                    ds.[i] <- { ds.[i] with Hp = ds.[i].Hp - b.Damage }
                events.Add(Bump b.Pos)
                false
            | None -> true)
    s, List.ofArray ds, remaining, List.ofSeq events

let resolveBullets (ships: Ship[]) (bullets: Bullet list) =
    let s = Array.copy ships
    let events = ResizeArray()
    let remaining =
        bullets
        |> List.filter (fun b ->
            let target =
                s |> Array.tryFindIndex (fun sh -> sh.Alive && side sh <> side s.[b.Owner] && len (sh.Pos - b.Pos) < shipRadius + 5.)
            match target with
            | Some i ->
                if s.[i].Invuln <= 0. then
                    s.[b.Owner] <- { s.[b.Owner] with Hits = s.[b.Owner].Hits + 1 }
                s.[i] <- { s.[i] with Vel = s.[i].Vel + norm b.Vel * bulletKnockback } |> tag b.Owner (if b.Kind = 2 then Swarm else Blaster) |> damage b.Damage
                events.Add(Hit(b.Pos, (if b.Kind = 2 then "Swarm" else "Blaster"), b.Damage))
                false
            | None -> true)
    s, remaining, List.ofSeq events

let resolveRams (ships: Ship[]) =
    let s = Array.copy ships
    let events = ResizeArray()
    for i in 0 .. s.Length - 2 do
        for j in i + 1 .. s.Length - 1 do
            let a, b = s.[i], s.[j]
            let d = b.Pos - a.Pos
            let dist = len d
            if a.Alive && b.Alive && dist < 2. * shipRadius && dist > 1e-6 then
                let n = d * (1. / dist)
                let vn = dot (b.Vel - a.Vel) n
                let push = n * ((2. * shipRadius - dist) / 2.)
                let impulse = if vn < 0. then -(1. + restitution) * vn / 2. else 0.
                let enemy = side a <> side b
                let dmg = if enemy then abs vn * ramDamageFactor else 0.
                let mark by sh = if enemy then tag by Collision sh else sh
                s.[i] <- { a with Pos = a.Pos - push; Vel = a.Vel - n * impulse } |> mark b.Id |> damage dmg
                s.[j] <- { b with Pos = b.Pos + push; Vel = b.Vel + n * impulse } |> mark a.Id |> damage dmg
                if impulse > 0. then events.Add(Ram(a.Pos + d * 0.5))
    s, List.ofSeq events

let resolvePads sudden dt (ships: Ship[]) (pads: Pad[]) =
    let s = Array.copy ships
    let events = ResizeArray()
    let pads =
        pads
        |> Array.map (fun p ->
            if p.RespawnIn > 0. then
                { p with RespawnIn = p.RespawnIn - dt }
            else
                let wants (sh: Ship) =
                    sh.Alive
                    && len (sh.Pos - p.Pos) < padRadius + shipRadius
                    && (match p.Kind with
                        | 1 -> sh.Hp < hpMax && not sudden
                        | 2 -> sh.Shield <= 0.
                        | _ -> true)
                match s |> Array.tryFindIndex wants with
                | Some i when p.Kind = 2 ->
                    s.[i] <- { s.[i] with Shield = p.Amount }
                    events.Add(Pickup(p.Pos, true))
                    { p with RespawnIn = shieldRespawn }
                | Some i when p.Kind = 1 ->
                    s.[i] <- { s.[i] with Hp = min hpMax (s.[i].Hp + p.Amount) }
                    events.Add(Mend p.Pos)
                    { p with RespawnIn = healRespawn }
                | Some i ->
                    s.[i] <- { s.[i] with Boost = min boostMax (s.[i].Boost + p.Amount) }
                    events.Add(Pickup(p.Pos, p.Amount >= boostMax))
                    { p with RespawnIn = padRespawn }
                | None -> p)
    s, pads, List.ofSeq events

let stepCrates dt rng (ships: Ship[]) (crates: Crate[]) =
    let mutable r = rng
    let mutable sh = ships
    let events = ResizeArray()
    let taken = ResizeArray(crates |> Array.map (fun c -> c.Pos))
    let out =
        crates
        |> Array.map (fun c ->
            if c.RespawnIn > 0. then
                { c with RespawnIn = c.RespawnIn - dt }
            else
                match sh |> Array.tryFindIndex (fun s -> s.Alive && len (s.Pos - c.Pos) < crateRadius + shipRadius) with
                | Some i ->
                    r <- nextRng r
                    let w =
                        if practice then arsenal.[sh.[i].Grabs % arsenal.Length]
                        elif race then raceArsenal.[r % raceArsenal.Length]
                        elif mutator = 1 then Rail
                        else crateWeapons.[r % crateWeapons.Length]
                    let a = Array.copy sh
                    a.[i] <- { a.[i] with Weapon = w; Ammo = (if race && w = Swarm then 1 else weaponAmmo w); Charge = 0.; Grabs = a.[i].Grabs + 1 }
                    sh <- a
                    events.Add(Grab c.Pos)
                    r <- nextRng r
                    if practice then { c with RespawnIn = 1. } else
                    let mutable k = r % cratePositions.Length
                    let mutable tries = 0
                    while tries < cratePositions.Length && taken.Contains cratePositions.[k] do
                        k <- (k + 1) % cratePositions.Length
                        tries <- tries + 1
                    taken.Remove c.Pos |> ignore
                    taken.Add cratePositions.[k]
                    { Pos = cratePositions.[k]; RespawnIn = crateRespawn }
                | None -> c)
    sh, out, r, List.ofSeq events