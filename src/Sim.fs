module Sim

open System
open Vec
open Domain
open Domain.Cfg

let spawnPos i =
    let a = (diagLimit - 320.) / 2.
    match i with
    | 0 -> v (-a) (-a)
    | 1 -> v a (-a)
    | 2 -> v (-a) a
    | _ -> v a a

let freshShip i =
    let p = spawnPos i
    { Id = i
      Team = 0
      Pos = p
      Vel = zero
      Angle = atan2 (-p.Y) (-p.X)
      Hp = hpMax
      Shield = 0.
      Boost = boostStart
      Stocks = stocks
      Alive = true
      Active = true
      Thrusting = 0.
      Reversing = false
      RespawnIn = 0.
      Invuln = invulnTime
      Cooldown = 0.
      Stun = 0.
      Spin = 0.
      Heat = 0.
      Locked = 0.
      Weapon = Blaster
      Ammo = 0
      Charge = 0.
      Held = false
      Tow = NoTether
      TowLeft = 0.
      LastHit = -1
      Streak = 0
      Shots = 0
      Hits = 0
      Grabs = 0
      Rings = 0
      Kills = 0 }

let private respawn (s: Ship) =
    { freshShip s.Id with
        Team = s.Team
        Shots = s.Shots
        Hits = s.Hits
        Grabs = s.Grabs
        Rings = s.Rings
        Kills = s.Kills }

let private side (s: Ship) = if s.Team > 0 then -s.Team else s.Id

let private nearest (ships: Ship[]) owner (p: V2) =
    ships
    |> Array.filter (fun s -> s.Alive && side s <> side ships.[owner])
    |> Array.fold
        (fun best s ->
            match best with
            | Some (b: Ship) when len (b.Pos - p) <= len (s.Pos - p) -> best
            | _ -> Some s)
        None

let withTeams (teams: int[]) (w: World) =
    { w with Ships = w.Ships |> Array.map (fun s -> { s with Team = teams.[s.Id] }) }

type Layout =
    { Rocks: (V2 * float) list
      Pads: (V2 * float * int) list
      Crates: V2 list }

let private polar r deg =
    let a = deg * Math.PI / 180.
    v (cos a * r) (sin a * r)

let private turn (p: V2) deg =
    let a = deg * Math.PI / 180.
    v (p.X * cos a - p.Y * sin a) (p.X * sin a + p.Y * cos a)

let private quad f =
    [ for sx in [ 1.; -1. ] do
          for sy in [ 1.; -1. ] do
              yield! f sx sy ]

let private spin f = [ for q in 0..3 -> f (float q * 90.) ] |> List.concat

let private nextRng r = abs (r * 1664525 + 1013904223) % 1000003

let private jitter i =
    let x = sin (float i * 12.9898) * 43758.5453
    x - floor x

let private core = v 0. 0., shieldAmount, 2

let private axisPads m =
    [ core
      v m 0., padRefill, 0
      v (-m) 0., padRefill, 0
      v 0. m, padRefill, 0
      v 0. (-m), padRefill, 0
      v (m / 2.) 0., padRefill, 0
      v (-m / 2.) 0., padRefill, 0
      v 0. (m / 2.), padRefill, 0
      v 0. (-m / 2.), padRefill, 0 ]

let private heals h =
    [ v h 0., healAmount, 1
      v (-h) 0., healAmount, 1
      v 0. h, healAmount, 1
      v 0. (-h), healAmount, 1 ]

let layouts =
    [| { Rocks =
           [ for k in 0..11 do
                 if k % 3 <> 0 then
                     yield polar 430. (float k * 30.), 44. + jitter k * 10.
             yield! quad (fun sx sy ->
                 let c = arenaHalf * 0.52
                 [ v (sx * c) (sy * c), 58.
                   v (sx * (c + 230.)) (sy * (c - 180.)), 40.
                   v (sx * (c - 180.)) (sy * (c + 230.)), 40. ])
             yield! quad (fun sx sy ->
                 let e = arenaHalf * 0.86
                 [ v (sx * e) (sy * 250.), 42.
                   v (sx * 250.) (sy * e), 42. ]) ]
         Pads = axisPads (arenaHalf * 0.55) @ heals (arenaHalf * 0.874)
         Crates =
           quad (fun sx sy ->
               [ v (sx * arenaHalf * 0.35) (sy * arenaHalf * 0.35)
                 v (sx * arenaHalf * 0.75) (sy * arenaHalf * 0.75) ]) }

       { Rocks =
           [ yield! spin (fun a ->
                 [ turn (v 620. 0.) a, 58.
                   turn (polar 640. 15.) a, 42.
                   turn (polar 640. -15.) a, 42.
                   turn (v 790. 0.) a, 36. ])
             yield! quad (fun sx sy ->
                 let c = arenaHalf * 0.62
                 [ v (sx * c) (sy * c), 48.
                   v (sx * (c + 210.)) (sy * (c - 260.)), 38. ]) ]
         Pads =
           core
           :: quad (fun sx sy ->
               let d = arenaHalf * 0.78 / sqrt 2.
               [ v (sx * d) (sy * d), padRefill, 0
                 v (sx * d * 0.45) (sy * d * 0.45), padRefill, 0 ])
           @ heals (arenaHalf * 0.86)
         Crates = spin (fun a -> [ turn (v (arenaHalf * 0.32) 0.) a; turn (v (arenaHalf * 0.85) 0.) a ]) }

       { Rocks =
           spin (fun a ->
               [ for i in 0..4 ->
                     turn (polar (320. + float i * 160.) (26. + float i * 13.)) a, 50. - float i * 3. ])
         Pads =
           core
           :: spin (fun a -> [ turn (polar 560. -24.) a, padRefill, 0; turn (polar 900. -30.) a, padRefill, 0 ])
           @ spin (fun a -> [ turn (polar (arenaHalf * 0.86) -8.) a, healAmount, 1 ])
         Crates = spin (fun a -> [ turn (polar 700. 68.) a; turn (polar 1080. 50.) a ]) }

       { Rocks =
           [ for sy in [ 1.; -1. ] do
                 for i in 0..6 do
                     let x = -900. + float i * 300.
                     if abs x > 200. then yield v x (sy * 340.), 46.
             yield! quad (fun sx sy ->
                 [ v (sx * arenaHalf * 0.82) (sy * arenaHalf * 0.62), 50.
                   v (sx * arenaHalf * 0.5) (sy * arenaHalf * 0.86), 40. ])
             for sy in [ 1.; -1. ] -> v 0. (sy * 760.), 54. ]
         Pads =
           [ core
             v 420. 0., padRefill, 0
             v (-420.) 0., padRefill, 0
             v 760. 0., padRefill, 0
             v (-760.) 0., padRefill, 0
             v 0. 560., padRefill, 0
             v 0. (-560.), padRefill, 0
             v 420. 560., padRefill, 0
             v (-420.) (-560.), padRefill, 0
             v (arenaHalf * 0.9) 0., healAmount, 1
             v (-(arenaHalf * 0.9)) 0., healAmount, 1
             v 0. (arenaHalf * 0.88), healAmount, 1
             v 0. (-(arenaHalf * 0.88)), healAmount, 1 ]
         Crates = quad (fun sx sy -> [ v (sx * 640.) (sy * 640.); v (sx * 1060.) (sy * 260.) ]) }

       { Rocks =
           [ for k in 0..19 do
                 if k % 5 <> 0 then
                     yield polar 780. (float k * 18.), 52. + jitter k * 16. ]
         Pads =
           core
           :: quad (fun sx sy -> [ v (sx * 300.) (sy * 300.), padRefill, 0; v (sx * 520.) (sy * 520.), padRefill, 0 ])
           @ heals (arenaHalf * 0.9)
         Crates = spin (fun a -> [ turn (v 1080. 0.) a; turn (v 560. 0.) a ]) } |]

let mutable layout = 0
let mutable asteroids: Asteroid[] = [||]
let mutable cratePositions: V2[] = [||]

let private build i =
    let l = layouts.[i]
    asteroids <- l.Rocks |> List.map (fun (p, r) -> { Pos = p; Radius = r }) |> List.toArray
    cratePositions <- l.Crates |> List.toArray
    { Ships = Array.init 4 (fun i -> { freshShip i with Active = false; Alive = false; Stocks = 0 })
      Bullets = []
      Mines = []
      Pads = l.Pads |> List.map (fun (p, a, k) -> { Pos = p; Amount = a; RespawnIn = 0.; Kind = k }) |> List.toArray
      Crates = [| 0; 2; 4; 6 |] |> Array.map (fun i -> { Pos = cratePositions.[i]; RespawnIn = 0. })
      Rng = 7
      Phase = Playing
      Time = 0.
      Events = [] }

let mutable initial = build 0

let setLayout i =
    layout <- (i + layouts.Length) % layouts.Length
    initial <- build layout

let bounds t =
    if t <= matchTime then 1. else max shrinkMin (1. - (t - matchTime) / shrinkTime)

let sudden (w: World) = w.Time > matchTime

let private outOfBoundsAt k (p: V2) =
    abs p.X > arenaHalf * k + killMargin
    || abs p.Y > arenaHalf * k + killMargin
    || abs p.X + abs p.Y > diagLimit * k + killMargin

let private damage amt (s: Ship) =
    if s.Invuln > 0. then
        s
    else
        let soaked = min s.Shield amt
        { s with Shield = s.Shield - soaked; Hp = s.Hp - (amt - soaked) }

let private tag by (s: Ship) = if s.Invuln > 0. then s else { s with LastHit = by }

let hurting (s: Ship) = s.Alive && s.Hp < hurtBelow

let private slow (s: Ship) = if hurting s then hurtFactor else 1.

let private join (inp: Input) (s: Ship) =
    if not s.Active && inp.Present then respawn s else s

let private stepShip dt (inp: Input) (s: Ship) =
    if not s.Alive then
        s
    else
        let inp = if s.Stun > 0. then noInput else inp
        let k = slow s
        let angle =
            match inp.Aim with
            | Some a when not inp.Steer -> a
            | Some a ->
                let d = atan2 (sin (a - s.Angle)) (cos (a - s.Angle))
                let lim = turnRate * k * dt
                s.Angle + max -lim (min lim d) + s.Spin * dt
            | None -> s.Angle + (inp.Turn * turnRate * k + s.Spin) * dt
        let boosting = inp.Boost && s.Boost > 0.
        let thrusting = if boosting then 2. elif inp.Thrust || inp.Boost then 1. else 0.
        let accel =
            if boosting then boostAccel * k
            elif thrusting > 0. then thrustAccel * k
            elif inp.Reverse then -thrustAccel * reverseFactor * k
            else 0.
        let push = ofAngle angle * accel + ofAngle (angle + System.Math.PI / 2.) * (inp.Strafe * strafeAccel * k)
        let vel = (s.Vel + push * dt) * (1. - drag * dt) |> clampLen (maxSpeed * k)
        { s with
            Angle = angle
            Vel = vel
            Pos = s.Pos + vel * dt
            Boost = if boosting then max 0. (s.Boost - boostDrain * dt) else s.Boost
            Thrusting = thrusting
            Reversing = accel < 0.
            Invuln = max 0. (s.Invuln - dt)
            Cooldown = max 0. (s.Cooldown - dt)
            Heat = max 0. (s.Heat - heatCool * dt)
            Locked = max 0. (s.Locked - dt)
            Stun = max 0. (s.Stun - dt)
            Spin = if s.Stun > dt then s.Spin else 0. }

let private spend (s: Ship) =
    let a = s.Ammo - 1
    if a <= 0 then { s with Weapon = Blaster; Ammo = 0; Charge = 0. } else { s with Ammo = a }

let private blaster (inp: Input) (s: Ship) =
    if s.Alive && inp.Fire && s.Cooldown <= 0. && s.Locked <= 0. then
        let dir = ofAngle s.Angle
        let nose = s.Pos + dir * (shipRadius + 6.)
        let heat = s.Heat + heatPerShot
        let cooked = heat >= heatMax
        let b =
            { Owner = s.Id
              Pos = nose
              Vel = dir * bulletSpeed + s.Vel * 0.5
              Life = bulletLife
              Kind = 0
              Damage = bulletDamage }
        { s with
            Cooldown = fireCooldown
            Heat = (if cooked then heatMax else heat)
            Locked = (if cooked then overheatLock else 0.)
            Vel = s.Vel - dir * recoil
            Shots = s.Shots + 1 },
        [ b ],
        (Shot nose :: (if cooked then [ Cooked s.Pos ] else []))
    else
        s, [], []

let private special dt (inp: Input) (s: Ship) =
    let press = inp.Special && not s.Held
    let s = { s with Held = inp.Special }
    if not s.Alive then
        { s with Charge = 0. }, [], [], []
    else
        let dir = ofAngle s.Angle
        let nose = s.Pos + dir * (shipRadius + 6.)
        match s.Weapon with
        | Blaster -> s, [], [], []
        | Rail ->
            if inp.Special then
                let c = s.Charge + dt
                if c >= railCharge then
                    let far = s.Pos + dir * (4. * arenaHalf)
                    { spend s with
                        Charge = 0.
                        Vel = s.Vel - dir * railRecoil
                        Shots = s.Shots + 1 },
                    [],
                    [],
                    [ Beam(nose, far, s.Id) ]
                else
                    { s with Charge = c }, [], [], (if press then [ Charging s.Pos ] else [])
            else
                { s with Charge = 0. }, [], [], []
        | Mines ->
            if press then
                let m =
                    { Owner = s.Id
                      Pos = s.Pos - dir * (shipRadius + 10.)
                      Vel = s.Vel * 0.4
                      Fuse = -1. }
                spend s, [], [ m ], [ MineSet m.Pos ]
            else
                s, [], [], []
        | Swarm ->
            if press then
                let b =
                    { Owner = s.Id
                      Pos = nose
                      Vel = dir * seekerSpeed + s.Vel * 0.5
                      Life = seekerLife
                      Kind = 2
                      Damage = seekerDamage }
                { spend s with
                    Vel = s.Vel - dir * (recoil * 0.6)
                    Shots = s.Shots + 1 },
                [ b ],
                [],
                [ Shot nose ]
            else
                s, [], [], []
        | Pulse ->
            if press then
                { spend s with Vel = s.Vel - dir * (pulseForce * 0.08) }, [], [], [ Wave(s.Pos, s.Angle, s.Id) ]
            else
                s, [], [], []
        | Scatter ->
            if press then
                { spend s with Vel = s.Vel - dir * recoil; Shots = s.Shots + 1 }, [], [], [ Zap(s.Pos, s.Angle, s.Id) ]
            else
                s, [], [], []
        | Tractor ->
            if inp.Special && s.Tow = NoTether then
                let c = s.Charge + dt
                if c >= railCharge then
                    { s with Charge = 0. }, [], [], [ Latch(s.Pos, s.Id) ]
                else
                    { s with Charge = c }, [], [], (if press then [ Charging s.Pos ] else [])
            else
                { s with Charge = 0. }, [], [], []

let private fire dt (inp: Input) (s: Ship) =
    let s, shots, e1 = blaster inp s
    let s, more, mines, e2 = special dt inp s
    s, shots @ more, mines, e1 @ e2

let private blocked (p: V2) =
    asteroids |> Array.exists (fun a -> len (a.Pos - p) < a.Radius)

let private steer dt (ships: Ship[]) (b: Bullet) =
    if b.Kind <> 2 then
        b
    else
        match nearest ships b.Owner b.Pos with
        | Some t ->
            let a0 = atan2 b.Vel.Y b.Vel.X
            let a1 = atan2 (t.Pos.Y - b.Pos.Y) (t.Pos.X - b.Pos.X)
            let d = atan2 (sin (a1 - a0)) (cos (a1 - a0))
            let lim = seekerTurn * dt
            { b with Vel = ofAngle (a0 + max -lim (min lim d)) * seekerSpeed }
        | None -> b

let private stepBullet k dt (b: Bullet) =
    let b = { b with Pos = b.Pos + b.Vel * dt; Life = b.Life - dt }
    if b.Life <= 0. || outOfBoundsAt k b.Pos || blocked b.Pos then None else Some b

let private segDist (a: V2) (b: V2) (p: V2) =
    let ab = b - a
    let l2 = dot ab ab
    if l2 < 1e-9 then
        len (p - a)
    else
        let t = max 0. (min 1. (dot (p - a) ab / l2))
        len (p - (a + ab * t))

let private resolveBeams (ships: Ship[]) beams =
    let s = Array.copy ships
    let events = ResizeArray()
    for (a, b, owner) in beams do
        let dir = norm (b - a)
        for i in 0 .. s.Length - 1 do
            if s.[i].Alive && side s.[i] <> side s.[owner] && segDist a b s.[i].Pos < shipRadius + 6. then
                events.Add(Hit s.[i].Pos)
                if s.[i].Invuln <= 0. then
                    s.[owner] <- { s.[owner] with Hits = s.[owner].Hits + 1 }
                s.[i] <- { s.[i] with Vel = s.[i].Vel + dir * bulletKnockback } |> tag owner |> damage railDamage
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

let private resolveWaves (ships: Ship[]) waves =
    let s = Array.copy ships
    for (p, a, owner) in waves do
        for i in 0 .. s.Length - 1 do
            match inCone s p a pulseRange pulseCone owner i with
            | Some(n, f) -> s.[i] <- { s.[i] with Vel = s.[i].Vel + n * (pulseForce * f) } |> tag owner
            | None -> ()
    s

let private resolveZaps (ships: Ship[]) zaps =
    let s = Array.copy ships
    let events = ResizeArray()
    for (p, a, owner) in zaps do
        for i in 0 .. s.Length - 1 do
            match inCone s p a scatterRange scatterCone owner i with
            | Some _ ->
                events.Add(Hit s.[i].Pos)
                if s.[i].Invuln <= 0. then
                    s.[owner] <- { s.[owner] with Hits = s.[owner].Hits + 1 }
                s.[i] <- { s.[i] with Stun = max s.[i].Stun scatterStun; Thrusting = 0. } |> tag owner |> damage scatterDamage
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

let private resolveTows dt (ships: Ship[]) latches =
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
                    s.[j] <- { s.[j] with Vel = s.[j].Vel + pull s.[j].Pos me.Pos } |> tag i
                    s.[i] <- { s.[i] with TowLeft = me.TowLeft - dt }
                | TowRock k when len (asteroids.[k].Pos - me.Pos) > asteroids.[k].Radius + 2. * shipRadius ->
                    s.[i] <- { me with Vel = me.Vel + pull me.Pos asteroids.[k].Pos; TowLeft = me.TowLeft - dt }
                | _ -> drop ()
    s

let private stepMines k dt (ships: Ship[]) (mines: Mine list) =
    let events = ResizeArray()
    let blasts = ResizeArray()
    let kept =
        mines
        |> List.choose (fun m ->
            let near = nearest ships m.Owner m.Pos
            if m.Fuse < 0. then
                match near with
                | Some t when len (t.Pos - m.Pos) < mineMagnet ->
                    events.Add(MineLive m.Pos)
                    Some { m with Fuse = mineFuse }
                | _ -> Some m
            elif m.Fuse <= dt || blocked m.Pos || outOfBoundsAt k m.Pos then
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

let private resolveBlasts (ships: Ship[]) blasts =
    let s = Array.copy ships
    for (p, owner) in blasts do
        for i in 0 .. s.Length - 1 do
            let d = s.[i].Pos - p
            let dist = len d
            if s.[i].Alive && dist < mineBlast then
                let f = 1. - dist / mineBlast
                s.[i] <- { s.[i] with Vel = s.[i].Vel + norm d * (pulseForce * f) } |> tag owner |> damage (mineDamage * f)
    s

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

let private resolveAsteroids (ships: Ship[]) =
    let events = ResizeArray()
    let s = ships |> Array.map (fun sh -> if sh.Alive then Array.fold (bump events) sh asteroids else sh)
    s, List.ofSeq events

let private resolveBullets (ships: Ship[]) (bullets: Bullet list) =
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
                s.[i] <- { s.[i] with Vel = s.[i].Vel + norm b.Vel * bulletKnockback } |> tag b.Owner |> damage b.Damage
                events.Add(Hit b.Pos)
                false
            | None -> true)
    s, remaining, List.ofSeq events

let private resolveRams (ships: Ship[]) =
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
                let mark by sh = if enemy then tag by sh else sh
                s.[i] <- { a with Pos = a.Pos - push; Vel = a.Vel - n * impulse } |> mark b.Id |> damage dmg
                s.[j] <- { b with Pos = b.Pos + push; Vel = b.Vel + n * impulse } |> mark a.Id |> damage dmg
                if impulse > 0. then events.Add(Ram(a.Pos + d * 0.5))
    s, List.ofSeq events

let private resolvePads sudden dt (ships: Ship[]) (pads: Pad[]) =
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

let private stepCrates dt rng (ships: Ship[]) (crates: Crate[]) =
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
                    let w = crateWeapons.[r % crateWeapons.Length]
                    let a = Array.copy sh
                    a.[i] <- { a.[i] with Weapon = w; Ammo = weaponAmmo w; Charge = 0.; Grabs = a.[i].Grabs + 1 }
                    sh <- a
                    events.Add(Grab c.Pos)
                    r <- nextRng r
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

let private settle k dt (s: Ship) =
    if s.Alive && (s.Hp <= 0. || outOfBoundsAt k s.Pos) then
        let ring = outOfBoundsAt k s.Pos
        { s with
            Alive = false
            Stocks = s.Stocks - 1
            RespawnIn = respawnDelay
            Vel = zero
            Thrusting = 0.
            Streak = 0
            Rings = s.Rings + (if ring then 1 else 0) },
        Some(s.Pos, s.Id, ring, s.LastHit)
    elif not s.Alive && s.Active && s.Stocks > 0 then
        (if s.RespawnIn <= dt then { respawn s with Stocks = s.Stocks } else { s with RespawnIn = s.RespawnIn - dt }),
        None
    else
        s, None

let private phase (ships: Ship[]) =
    let active = ships |> Array.filter (fun s -> s.Active)
    let contenders = active |> Array.filter (fun s -> s.Stocks > 0)
    if active.Length >= 2 && (contenders |> Array.distinctBy side).Length <= 1 then
        Over(contenders |> Array.tryHead |> Option.map (fun s -> s.Id))
    else
        Playing

let reset (w: World) =
    { initial with Ships = w.Ships |> Array.map (fun s -> if s.Active then { freshShip s.Id with Team = s.Team } else s); Rng = w.Rng }

let step dt (inputs: Input[]) (w: World) =
    match w.Phase with
    | Over _ when inputs |> Array.exists (fun i -> i.Start) -> reset w
    | _ ->
        let k = bounds w.Time
        let fired =
            w.Ships
            |> Array.map (fun s -> join inputs.[s.Id] s |> stepShip dt inputs.[s.Id] |> fire dt inputs.[s.Id])
        let ships = fired |> Array.map (fun (s, _, _, _) -> s)
        let newBullets = fired |> Array.toList |> List.collect (fun (_, b, _, _) -> b)
        let newMines = fired |> Array.toList |> List.collect (fun (_, _, m, _) -> m)
        let shotEvents = fired |> Array.toList |> List.collect (fun (_, _, _, e) -> e)
        let beams = shotEvents |> List.choose (function Beam(a, b, o) -> Some(a, b, o) | _ -> None)
        let waves = shotEvents |> List.choose (function Wave(p, a, o) -> Some(p, a, o) | _ -> None)
        let zaps = shotEvents |> List.choose (function Zap(p, a, o) -> Some(p, a, o) | _ -> None)
        let latches = shotEvents |> List.choose (function Latch(_, o) -> Some o | _ -> None)
        let ships = resolveWaves ships waves
        let ships, zapHits = resolveZaps ships zaps
        let ships = resolveTows dt ships latches
        let ships, beamHits = resolveBeams ships beams
        let bullets =
            newBullets @ w.Bullets
            |> List.map (steer dt ships)
            |> List.choose (stepBullet k dt)
        let ships, bullets, hits = resolveBullets ships bullets
        let mines, blasts, mineEvents = stepMines k dt ships (newMines @ w.Mines)
        let ships = resolveBlasts ships blasts
        let ships, rams = resolveRams ships
        let ships, bumps = resolveAsteroids ships
        let ships, pads, picks = resolvePads (sudden w) dt ships w.Pads
        let ships, crates, rng, grabs = stepCrates dt w.Rng ships w.Crates
        let settled, deaths = ships |> Array.map (settle k dt) |> Array.unzip
        let ships = Array.copy settled
        let kills = deaths |> Array.toList |> List.choose id
        for (_, victim, _, by) in kills do
            if by >= 0 && by <> victim then
                ships.[by] <- { ships.[by] with Kills = ships.[by].Kills + 1; Streak = ships.[by].Streak + 1 }
        { Ships = ships
          Bullets = bullets
          Mines = mines
          Pads = pads
          Crates = crates
          Rng = rng
          Phase = phase ships
          Time = w.Time + dt
          Events =
            shotEvents
            @ beamHits
            @ zapHits
            @ hits
            @ mineEvents
            @ rams
            @ bumps
            @ picks
            @ grabs
            @ (kills |> List.map (fun (p, i, ring, _) -> Explode(p, i, ring))) }
