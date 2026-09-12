module Collisions

open Vec
open Domain
open Domain.Cfg
open State
open Track
open Combat
open Entities

let private bump (events: ResizeArray<Event>) (pos: V2) (radius: float) (vel: V2) (sh: Ship) =
    let d = sh.Pos - pos
    let dist = len d
    if dist < radius + shipRadius && dist > 1e-6 then
        let n = d * (1. / dist)
        let cp = pos + n * radius
        events.Add(Bump cp)
        let hit, jn = Impact.apply cp n vel 0. sh
        hit |> Impact.separate cp n shipRadius |> Impact.stun jn, jn
    else
        sh, 0.

let resolveAsteroids (ships: Ship[]) =
    let events = ResizeArray()
    let one sh (a: Asteroid) = fst (bump events a.Pos a.Radius zero sh)
    let s = ships |> Array.map (fun sh -> if sh.Alive then Array.fold one sh asteroids else sh)
    s, List.ofSeq events

let resolveRocks (ships: Ship[]) (rocks: Rock list) =
    let events = ResizeArray()
    let hit (sh: Ship) (r: Rock) =
        let bumped, jn = bump events r.Pos r.Radius r.Vel sh
        if jn > 0. then
            let mark s = if side s <> side ships.[r.Owner] then tag r.Owner Rock s else s
            bumped |> mark |> damage (jn * rockDamage)
        else
            bumped
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
                        let surface = cp + n * wallThick
                        let hit, _ = Impact.apply surface n zero 0. sh
                        s.[j] <- hit |> Impact.separate surface n shipRadius
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
                events.Add(Hit(b.Pos, (if b.Kind = 2 then "Swarm" else "Blaster"), b.Damage, i))
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
                let overlap = 2. * shipRadius - dist
                let push = n * (overlap / 2.)
                let a', b', _ = Impact.pair (a.Pos + n * shipRadius) n a b
                let sep = n * (overlap * ramSeparate / 2.)
                let enemy = side a <> side b
                // damage is one-sided by default: each ship's own closing speed hurts the other,
                // so a stationary ship dies to a full-speed hit while the attacker takes nothing back
                let speedIntoB = max 0. (dot a.Vel n)
                let speedIntoA = max 0. (-(dot b.Vel n))
                // facing the incoming ship (bracing nose-first) cuts the damage you take
                let faceA = max 0. (dot (ofAngle a.Angle) n)
                let faceB = max 0. (dot (ofAngle b.Angle) (n * -1.))
                let dmgToA = speedIntoA * ramDamageFactor * (1. - ramFaceGuard * faceA)
                let dmgToB = speedIntoB * ramDamageFactor * (1. - ramFaceGuard * faceB)
                // a hard, square brace parries the ram: only the ship actually under threat can
                // parry with its own facing — an attacker's nose is always toward its target
                // just from thrusting there, so that alone must never count as a brace
                let parried =
                    enemy
                    && ((speedIntoA > ramEventSpeed && faceA > ramParryFace)
                        || (speedIntoB > ramEventSpeed && faceB > ramParryFace))
                let mark by sh = if enemy then tag by Collision sh else sh
                let brace sh = if parried then { sh with Stun = max sh.Stun ramParryStun; Spin = asteroidSpin } else sh
                s.[i] <- { a' with Pos = a.Pos - push; Vel = a'.Vel - sep } |> mark b.Id |> damage (if enemy then dmgToA else 0.) |> brace
                s.[j] <- { b' with Pos = b.Pos + push; Vel = b'.Vel + sep } |> mark a.Id |> damage (if enemy then dmgToB else 0.) |> brace
                if parried then
                    events.Add(Bump(a.Pos + d * 0.5))
                    events.Add(Parried(a.Pos + d * 0.5, a.Id, b.Id))
                if vn < -ramEventSpeed then events.Add(Ram(a.Pos + d * 0.5))
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
                        match mode with
                        | Practice -> arsenal.[sh.[i].Grabs % arsenal.Length]
                        | Race -> raceArsenal.[rngIndex raceArsenal.Length r]
                        | Arena when mutator = 1 -> Rail
                        | Arena -> crateWeapons.[rngIndex crateWeapons.Length r]
                    let a = Array.copy sh
                    a.[i] <- { a.[i] with Weapon = w; Ammo = (if mode = Race && (w = Swarm || w = Scatter) then 1 else weaponAmmo w); Charge = 0.; Grabs = a.[i].Grabs + 1 }
                    sh <- a
                    events.Add(Grab c.Pos)
                    r <- nextRng r
                    if mode = Practice then { c with RespawnIn = 1. } else
                    let mutable k = rngIndex cratePositions.Length r
                    let mutable tries = 0
                    while tries < cratePositions.Length && taken.Contains cratePositions.[k] do
                        k <- (k + 1) % cratePositions.Length
                        tries <- tries + 1
                    taken.Remove c.Pos |> ignore
                    taken.Add cratePositions.[k]
                    { Pos = cratePositions.[k]; RespawnIn = crateRespawn }
                | None -> c)
    sh, out, r, List.ofSeq events