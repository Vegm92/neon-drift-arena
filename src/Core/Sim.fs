module Sim

open System
open Vec
open Domain
open Domain.Cfg

let mutable race = false
let mutable gates: V2[] = [||]
let mutable trackWidth = 0.

let private gateAhead i = gates.[(i + 1) % gates.Length]

let private segDist (a: V2) (b: V2) (p: V2) =
    let ab = b - a
    let l2 = dot ab ab
    if l2 < 1e-9 then
        len (p - a)
    else
        let t = max 0. (min 1. (dot (p - a) ab / l2))
        len (p - (a + ab * t))

let onTrack (p: V2) =
    not race
    || gates |> Array.mapi (fun i g -> segDist g (gateAhead i) p) |> Array.min < trackWidth / 2.

let spawnPos i =
    if race && gates.Length > 1 then
        let dir = norm (gateAhead 0 - gates.[0])
        let side = v -dir.Y dir.X
        gates.[0] - dir * (90. + 100. * float (i / 2)) + side * (if i % 2 = 0 then -70. else 70.)
    else
        let a = (diagLimit - 320.) / 2.
        match i with
        | 0 -> v (-a) (-a)
        | 1 -> v a (-a)
        | 2 -> v (-a) a
        | _ -> v a a

let spawnAngle (p: V2) =
    if race && gates.Length > 1 then
        let d = gateAhead 0 - gates.[0]
        atan2 d.Y d.X
    else
        atan2 (-p.Y) (-p.X)

let freshShip i =
    let p = spawnPos i
    { Id = i
      Team = 0
      Pos = p
      Vel = zero
      Angle = spawnAngle p
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
      LastWeapon = Blaster
      LaunchCd = 0.
      LaunchAngle = atan2 p.Y p.X
      WarpCd = 0.
      Next = 1
      Laps = 0
      Finish = 0.
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

let side (s: Ship) = if s.Team > 0 then -s.Team else s.Id

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
      Crates: V2 list
      Track: (V2 list * float) option }

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

let private arenas =
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
                 v (sx * arenaHalf * 0.75) (sy * arenaHalf * 0.75) ])
         Track = None }

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
         Crates = spin (fun a -> [ turn (v (arenaHalf * 0.32) 0.) a; turn (v (arenaHalf * 0.85) 0.) a ])
         Track = None }

       { Rocks =
           spin (fun a ->
               [ for i in 0..4 ->
                     turn (polar (320. + float i * 160.) (26. + float i * 13.)) a, 50. - float i * 3. ])
         Pads =
           core
           :: spin (fun a -> [ turn (polar 560. -24.) a, padRefill, 0; turn (polar 900. -30.) a, padRefill, 0 ])
           @ spin (fun a -> [ turn (polar (arenaHalf * 0.86) -8.) a, healAmount, 1 ])
         Crates = spin (fun a -> [ turn (polar 700. 68.) a; turn (polar 1080. 50.) a ])
         Track = None }

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
         Crates = quad (fun sx sy -> [ v (sx * 640.) (sy * 640.); v (sx * 1060.) (sy * 260.) ])
         Track = None }

       { Rocks =
           [ for k in 0..19 do
                 if k % 5 <> 0 then
                     yield polar 780. (float k * 18.), 52. + jitter k * 16. ]
         Pads =
           core
           :: quad (fun sx sy -> [ v (sx * 300.) (sy * 300.), padRefill, 0; v (sx * 520.) (sy * 520.), padRefill, 0 ])
           @ heals (arenaHalf * 0.9)
         Crates = spin (fun a -> [ turn (v 1080. 0.) a; turn (v 560. 0.) a ])
         Track = None } |]

let private oval =
    let rx, ry, r = arenaHalf * 0.7, arenaHalf * 0.45, 320.
    let corner cx cy a0 =
        [ for k in 0..2 ->
              let a = (a0 + float k * 30.) * Math.PI / 180.
              v (cx + cos a * r) (cy + sin a * r) ]
    [ v 0. -ry; v (rx * 0.5) -ry ]
    @ corner (rx - r) (-ry + r) -90.
    @ [ v rx 0. ]
    @ corner (rx - r) (ry - r) 0.
    @ [ v (rx * 0.5) ry; v 0. ry; v (-rx * 0.5) ry ]
    @ corner (-rx + r) (ry - r) 90.
    @ [ v -rx 0. ]
    @ corner (-rx + r) (-ry + r) 180.
    @ [ v (-rx * 0.5) -ry ]

let tracks =
    [| { Rocks = [ v 0. 0., 60.; v 500. 0., 40.; v -500. 0., 40. ]
         Pads =
           [ v (arenaHalf * 0.25) (-(arenaHalf * 0.45)), padRefill, 0
             v (-(arenaHalf * 0.25)) (arenaHalf * 0.45), padRefill, 0
             v (arenaHalf * 0.7) 0., healAmount, 1
             v (-(arenaHalf * 0.7)) 0., healAmount, 1 ]
         Crates =
           [ v (arenaHalf * 0.5) (-(arenaHalf * 0.45))
             v (-(arenaHalf * 0.5)) (arenaHalf * 0.45)
             v (arenaHalf * 0.7) (arenaHalf * 0.2)
             v (-(arenaHalf * 0.7)) (-(arenaHalf * 0.2)) ]
         Track = Some(oval, 280.) } |]

let layouts = Array.append arenas tracks
let isTrack i = layouts.[i].Track.IsSome

let mutable layout = 0
let mutable practice = false
let mutable target = -1
let arsenal = [| Rail; Mines; Swarm; Pulse; Scatter; Tractor |]
let raceArsenal = [| Scatter; Mines; Swarm |]
let mutable mutator = 0
let mutators = 4
let private turbo () = if mutator = 2 then 1.5 else 1.
let private slick () = if mutator = 3 then 0.25 else 1.
let mutable asteroids: Asteroid[] = [||]
let mutable cratePositions: V2[] = [||]

let private build i =
    let l = layouts.[i]
    asteroids <- l.Rocks |> List.map (fun (p, r) -> { Pos = p; Radius = r }) |> List.toArray
    cratePositions <- l.Crates |> List.toArray
    gates <- l.Track |> Option.map (fst >> List.toArray) |> Option.defaultValue [||]
    trackWidth <- l.Track |> Option.map snd |> Option.defaultValue 0.
    { Ships = Array.init 4 (fun i -> { freshShip i with Active = false; Alive = false; Stocks = 0 })
      Bullets = []
      Mines = []
      Rocks = []
      Portals = []
      PortalIn = portalEvery
      Hole = None
      HoleIn = holeEvery
      RaceEnd = 0.
      Pads = l.Pads |> List.map (fun (p, a, k) -> { Pos = p; Amount = a; RespawnIn = 0.; Kind = k }) |> List.toArray
      Crates = [| 0; 2; 4; 6 |] |> Array.map (fun i -> { Pos = cratePositions.[i % cratePositions.Length]; RespawnIn = 0. })
      Rng = 7
      Phase = Playing
      Time = 0.
      Events = [] }

let mutable initial = build 0

let setLayout i =
    layout <- (i + layouts.Length) % layouts.Length
    initial <- build layout

let bounds t =
    if race || t <= matchTime then 1. else max shrinkMin (1. - (t - matchTime) / shrinkTime)

let sudden (w: World) = not race && w.Time > matchTime

let private outOfBoundsAt k (p: V2) =
    abs p.X > arenaHalf * k + killMargin
    || abs p.Y > arenaHalf * k + killMargin
    || abs p.X + abs p.Y > diagLimit * k + killMargin

let private inside k margin (p: V2) =
    abs p.X < arenaHalf * k - margin && abs p.Y < arenaHalf * k - margin && abs p.X + abs p.Y < diagLimit * k - margin

let private freeSpot k (ships: Ship[]) (avoid: V2 list) rng =
    let mutable r = rng
    let mutable pick = zero
    let mutable tries = 0
    let clear (p: V2) =
        inside k 120. p
        && asteroids |> Array.forall (fun a -> len (a.Pos - p) > a.Radius + 90.)
        && ships |> Array.forall (fun s -> not s.Alive || len (s.Pos - p) > 250.)
        && avoid |> List.forall (fun q -> len (q - p) > 500.)
    while tries < 40 && (tries = 0 || not (clear pick)) do
        r <- nextRng r
        let a = float (r % 360) * Math.PI / 180.
        let d = 250. + float ((r / 360) % 900)
        pick <- ofAngle a * d
        tries <- tries + 1
    pick, r

let private damage amt (s: Ship) =
    if s.Invuln > 0. then
        s
    elif race then
        if amt >= bulletDamage then { s with Stun = max s.Stun scatterStun; Thrusting = 0. } else s
    else
        let soaked = min s.Shield amt
        { s with Shield = s.Shield - soaked; Hp = s.Hp - (amt - soaked) }

let private tag by wpn (s: Ship) = if s.Invuln > 0. then s else { s with LastHit = by; LastWeapon = wpn }

let hurting (s: Ship) = s.Alive && s.Hp < hurtBelow

let launcher (s: Ship) = s.Active && not s.Alive && s.Stocks <= 0

let rim k angle =
    let c, s = abs (cos angle), abs (sin angle)
    let t = min (min (arenaHalf / max c 1e-9) (arenaHalf / max s 1e-9)) (diagLimit / (c + s))
    ofAngle angle * (t * k)

let private stepLauncher k dt (inp: Input) (s: Ship) =
    let angle =
        match inp.Aim with
        | Some a -> a
        | None -> s.LaunchAngle + inp.Turn * turnRate * dt
    { s with
        LaunchAngle = angle
        Angle = angle + Math.PI
        Vel = zero
        Pos = rim k angle
        LaunchCd = max 0. (s.LaunchCd - dt) }

let private slow (s: Ship) =
    (if hurting s then hurtFactor else 1.) * (if onTrack s.Pos then 1. else offroadFactor)

let private join (inp: Input) (s: Ship) =
    if not s.Active && inp.Present then respawn s else s

let private stepShip k dt (inp: Input) (s: Ship) =
    if launcher s then
        stepLauncher k dt inp s
    elif not s.Alive then
        s
    else
        let inp = if s.Stun > 0. then noInput else inp
        let k = slow s * turbo ()
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
        let vel = (s.Vel + push * dt) * (1. - (if race then raceDrag else drag) * slick () * dt) |> clampLen (maxSpeed * k)
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
    if s.Alive && inp.Fire && not race && s.Cooldown <= 0. && s.Locked <= 0. then
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
        | Blaster
        | Collision
        | Rock
        | Singularity -> s, [], [], []
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
    if launcher s then
        if inp.Fire && s.LaunchCd <= 0. then
            let r = { Owner = s.Id; Pos = s.Pos; Vel = norm (zero - s.Pos) * rockSpeed; Radius = rockRadius; Life = rockLife }
            { s with LaunchCd = rockCooldown }, [], [], [ r ], [ Launch s.Pos ]
        else
            s, [], [], [], []
    else
    let s, shots, e1 = blaster inp s
    let s, more, mines, e2 = special dt inp s
    s, shots @ more, mines, [], e1 @ e2

let private blocked (rocks: Rock list) (p: V2) =
    asteroids |> Array.exists (fun a -> len (a.Pos - p) < a.Radius)
    || rocks |> List.exists (fun r -> len (r.Pos - p) < r.Radius)

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

let private stepBullet k rocks dt (b: Bullet) =
    let b = { b with Pos = b.Pos + b.Vel * dt; Life = b.Life - dt }
    if b.Life <= 0. || outOfBoundsAt k b.Pos || blocked rocks b.Pos then None else Some b

let private stepPortals k dt sudden (ships: Ship[]) rng (w: World) =
    let live = w.Portals |> List.choose (fun p -> if p.Life <= dt then None else Some { p with Life = p.Life - dt })
    if w.PortalIn > dt || sudden then
        live, max 0. (w.PortalIn - dt), rng, []
    else
        let a, r1 = freeSpot k ships [] rng
        let b, r2 = freeSpot k ships [ a ] r1
        { A = a; B = b; Life = portalLife } :: live, portalEvery, r2, [ PortalOpen(a, b) ]

let private stepHole k dt sudden (ships: Ship[]) rng (w: World) =
    let live = w.Hole |> Option.filter (fun h -> h.Life > dt) |> Option.map (fun h -> { h with Life = h.Life - dt })
    if w.HoleIn > dt || sudden || live.IsSome then
        live, max 0. (w.HoleIn - dt), rng, []
    else
        let p, r = freeSpot k ships (w.Portals |> List.collect (fun g -> [ g.A; g.B ])) rng
        Some { Pos = p; Life = holeLife }, holeEvery, r, [ HoleOpen p ]

let private pull (hole: Hole option) dt (p: V2) (vel: V2) =
    match hole with
    | Some h ->
        let d = h.Pos - p
        let r = max holeCore (len d)
        vel + norm d * (holeG / (r * r) * dt)
    | None -> vel

let private resolveHole (hole: Hole option) dt (ships: Ship[]) =
    ships
    |> Array.map (fun s ->
        match hole with
        | Some h when s.Alive && len (h.Pos - s.Pos) < holeCore -> { s with Hp = 0.; LastWeapon = Singularity }
        | Some _ when s.Alive -> { s with Vel = pull hole dt s.Pos s.Vel }
        | _ -> s)

let private warpAt (portals: Portal list) (p: V2) (vel: V2) =
    portals
    |> List.tryPick (fun g ->
        let jump (src: V2) (dst: V2) =
            let dir = if len vel > 1e-6 then norm vel else norm (dst - src)
            Some(dst + dir * (portalRadius + 4.))
        if len (p - g.A) < portalRadius then jump g.A g.B
        elif len (p - g.B) < portalRadius then jump g.B g.A
        else None)

let private resolveWarps dt (portals: Portal list) (ships: Ship[]) =
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

let private stepRocks k dt (rocks: Rock list) =
    let events = ResizeArray()
    let live =
        rocks
        |> List.choose (fun r ->
            let r = { r with Pos = r.Pos + r.Vel * dt; Life = r.Life - dt }
            let hitRock = asteroids |> Array.exists (fun a -> len (a.Pos - r.Pos) < a.Radius + r.Radius)
            if hitRock then events.Add(Bump r.Pos)
            if r.Life <= 0. || hitRock || outOfBoundsAt k r.Pos then None else Some r)
    live, List.ofSeq events

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

let private resolveWaves (ships: Ship[]) waves =
    let s = Array.copy ships
    for (p, a, owner) in waves do
        for i in 0 .. s.Length - 1 do
            match inCone s p a pulseRange pulseCone owner i with
            | Some(n, f) -> s.[i] <- { s.[i] with Vel = s.[i].Vel + n * (pulseForce * f) } |> tag owner Pulse
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
                    s.[j] <- { s.[j] with Vel = s.[j].Vel + pull s.[j].Pos me.Pos } |> tag i Tractor
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

let private resolveBlasts (ships: Ship[]) blasts =
    let s = Array.copy ships
    for (p, owner) in blasts do
        for i in 0 .. s.Length - 1 do
            let d = s.[i].Pos - p
            let dist = len d
            if s.[i].Alive && dist < mineBlast then
                let f = 1. - dist / mineBlast
                s.[i] <- { s.[i] with Vel = s.[i].Vel + norm d * (pulseForce * f) } |> tag owner Mines |> damage (mineDamage * f)
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

let private resolveRocks (ships: Ship[]) (rocks: Rock list) =
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
                s.[i] <- { s.[i] with Vel = s.[i].Vel + norm b.Vel * bulletKnockback } |> tag b.Owner (if b.Kind = 2 then Swarm else Blaster) |> damage b.Damage
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
                let mark by sh = if enemy then tag by Collision sh else sh
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

let private pickSpawn (ships: Ship[]) rng (s: Ship) =
    let foes = ships |> Array.filter (fun t -> t.Alive && side t <> side s)
    let safety p =
        if foes.Length = 0 then 0. else foes |> Array.map (fun t -> len (t.Pos - p)) |> Array.min
    let ranked = [| 0..3 |] |> Array.sortByDescending (fun i -> safety (spawnPos i))
    ranked.[(rng + s.Id * 7919) % 2]

let mutable catchUp = true

let private underdog (ships: Ship[]) (s: Ship) =
    let rivals = ships |> Array.filter (fun t -> t.Active && t.Stocks > 0 && side t <> side s)
    catchUp && rivals.Length > 0 && s.Stocks < (rivals |> Array.map (fun t -> t.Stocks) |> Array.min)

let private reborn (ships: Ship[]) rng (s: Ship) =
    let p, a =
        if race then
            let p = gates.[(s.Next + gates.Length - 1) % gates.Length]
            let d = gates.[s.Next] - p
            p, atan2 d.Y d.X
        else
            let p = spawnPos (pickSpawn ships rng s)
            p, atan2 (-p.Y) (-p.X)
    let back = { respawn s with Stocks = s.Stocks; Pos = p; Angle = a; Next = s.Next; Laps = s.Laps }
    if underdog ships s then { back with Boost = boostMax; Shield = shieldAmount } else back

let private settle (ships: Ship[]) rng k dt (s: Ship) =
    if s.Alive && (s.Hp <= 0. || outOfBoundsAt k s.Pos) then
        let ring = outOfBoundsAt k s.Pos
        { s with
            Alive = false
            Stocks = (if practice || race then s.Stocks else s.Stocks - 1)
            RespawnIn = respawnDelay
            Vel = zero
            Thrusting = 0.
            Streak = 0
            Rings = s.Rings + (if ring then 1 else 0) },
        Some(s.Pos, s.Id, ring, s.LastHit, s.LastWeapon)
    elif not s.Alive && s.Active && s.Stocks > 0 && s.Finish = 0. then
        (if s.RespawnIn <= dt then reborn ships rng s else { s with RespawnIn = s.RespawnIn - dt }),
        None
    else
        s, None

let private progress (s: Ship) =
    let n = gates.Length
    float (s.Laps * n + (s.Next + n - 1) % n) - len (s.Pos - gates.[s.Next]) / (4. * arenaHalf)

let rank (ships: Ship[]) =
    ships
    |> Array.filter (fun s -> s.Active)
    |> Array.sortBy (fun s -> (if s.Finish > 0. then s.Finish else infinity), -progress s)
    |> Array.map (fun s -> s.Id)

let place (ships: Ship[]) i =
    rank ships |> Array.tryFindIndex ((=) i) |> Option.defaultValue 0 |> (+) 1

let private stepGates time (ships: Ship[]) =
    let mutable place = ships |> Array.filter (fun s -> s.Finish > 0.) |> Array.length
    let events = ResizeArray()
    let ships =
        ships
        |> Array.map (fun s ->
            if not (race && s.Alive) || len (s.Pos - gates.[s.Next]) > gateRadius then
                s
            else
                let next = (s.Next + 1) % gates.Length
                let n = if next = 1 then s.Laps + 1 else s.Laps
                if float n >= laps then
                    place <- place + 1
                    events.Add(Finished(s.Id, place))
                    { s with Next = next; Laps = n; Finish = time; Alive = false; Vel = zero; Thrusting = 0. }
                else
                    { s with Next = next; Laps = n })
    ships, List.ofSeq events

let private phase (ships: Ship[]) raceEnd =
    let active = ships |> Array.filter (fun s -> s.Active)
    let contenders = active |> Array.filter (fun s -> s.Stocks > 0)
    if race then
        let finished = active |> Array.filter (fun s -> s.Finish > 0.)
        if active.Length >= 2 && finished.Length > 0 && (finished.Length = active.Length || raceEnd >= raceGrace) then
            Over(Some (finished |> Array.minBy (fun s -> s.Finish)).Id)
        else
            Playing
    elif active.Length >= 2 && (contenders |> Array.distinctBy side).Length <= 1 then
        Over(contenders |> Array.tryHead |> Option.map (fun s -> s.Id))
    else
        Playing

let private lead (me: Ship) (t: Ship) =
    if me.Weapon = Rail then
        t.Pos
    else
        let rel = t.Vel - me.Vel * 0.5
        let speed = if me.Weapon = Swarm then seekerSpeed else bulletSpeed
        Seq.fold (fun (p: V2) _ -> t.Pos + rel * (len (p - me.Pos) / speed)) t.Pos (seq { 1..3 })

let private rockBetween (from: V2) (target: V2) =
    let d = target - from
    let l = len d
    let dir = norm d
    asteroids
    |> Array.exists (fun a ->
        let rel = a.Pos - from
        let along = max 0. (min l (dot rel dir))
        len (rel - dir * along) < a.Radius)

let private dodge (me: Ship) =
    let dir = if len me.Vel > 40. then norm me.Vel else ofAngle me.Angle
    let look = 180. + len me.Vel * 0.9
    asteroids
    |> Array.choose (fun a ->
        let rel = a.Pos - me.Pos
        let along = dot rel dir
        let side = dir.X * rel.Y - dir.Y * rel.X
        if along > 0. && along < look + a.Radius && abs side < a.Radius + 2.5 * shipRadius then Some(along, side) else None)
    |> Array.sortBy fst
    |> Array.tryHead
    |> Option.map (fun (_, side) -> atan2 dir.Y dir.X - (if side >= 0. then 1. else -1.) * 0.9)

let bot (w: World) i =
    let me = w.Ships.[i]
    let idle = { noInput with Present = true }
    if launcher me then
        match nearest w.Ships i me.Pos with
        | Some t -> { idle with Aim = Some(atan2 t.Pos.Y t.Pos.X); Fire = me.LaunchCd <= 0. }
        | None -> idle
    elif not me.Alive then
        idle
    elif race then
        let near = len (me.Pos - gates.[me.Next]) < gateRadius * 1.5
        let goal = if near then gateAhead me.Next else gates.[me.Next]
        let d = goal - me.Pos
        let aim = defaultArg (dodge me) (atan2 d.Y d.X)
        let off = abs (atan2 (sin (aim - me.Angle)) (cos (aim - me.Angle)))
        let ahead =
            nearest w.Ships i me.Pos
            |> Option.filter (fun t ->
                let r = t.Pos - me.Pos
                len r < 520. && abs (atan2 (sin (atan2 r.Y r.X - me.Angle)) (cos (atan2 r.Y r.X - me.Angle))) < 0.25)
        { idle with
            Aim = Some aim
            Steer = true
            Thrust = true
            Boost = off < 0.15 && me.Boost > 30.
            Special = ahead.IsSome && me.Weapon <> Blaster && int (w.Time * 2.) % 2 = 0 }
    else
        match nearest w.Ships i me.Pos with
        | None -> idle
        | Some t ->
            let dist = len (t.Pos - me.Pos)
            let k = bounds w.Time
            let shot = lead me t
            let d = shot - me.Pos
            let dodging = dodge me
            let aim =
                if abs me.Pos.X > arenaHalf * k - 220. || abs me.Pos.Y > arenaHalf * k - 220. then
                    atan2 -me.Pos.Y -me.Pos.X
                else
                    defaultArg dodging (atan2 d.Y d.X)
            let off = abs (atan2 (sin (aim - me.Angle)) (cos (aim - me.Angle)))
            let facing = off < 0.25 && dodging.IsNone && not (rockBetween me.Pos shot)
            let hold = me.Weapon = Rail || me.Weapon = Tractor
            { idle with
                Aim = Some aim
                Steer = true
                Thrust = dodging.IsSome || dist > 320. || off > 0.6
                Boost = dist > 900. && me.Boost > 40.
                Fire = facing && dist < 650.
                Special = facing && dist < 520. && me.Weapon <> Blaster && (hold || int (w.Time * 2.) % 2 = 0) }

let stage (w: World) =
    let shooters = w.Ships |> Array.filter (fun s -> s.Active && s.Id <> target) |> Array.map (fun s -> s.Id)
    let ships =
        w.Ships
        |> Array.map (fun s ->
            if s.Id = target then
                { freshShip s.Id with Team = s.Team; Pos = v 300. 0.; Angle = Math.PI; Invuln = 0. }
            elif s.Active then
                let k = Array.findIndex ((=) s.Id) shooters
                let y = (float k - float (shooters.Length - 1) / 2.) * 120.
                { freshShip s.Id with Team = s.Team; Pos = v -300. y; Angle = 0.; Invuln = 0. }
            else s)
    let crates = w.Crates |> Array.mapi (fun i c -> if i = 0 then { c with Pos = v 0. -220.; RespawnIn = 0. } else c)
    let mines = if target >= 0 then [ { Owner = target; Pos = v 0. 220.; Vel = zero; Fuse = -1. } ] else []
    { w with Ships = ships; Crates = crates; Mines = mines; Bullets = [] }

let arm i wpn (w: World) =
    { w with Ships = w.Ships |> Array.map (fun s -> if s.Id = i then { s with Weapon = wpn; Ammo = weaponAmmo wpn; Charge = 0. } else s) }

let reset (active: int -> bool) (w: World) =
    { initial with
        Ships =
            w.Ships
            |> Array.map (fun s ->
                if active s.Id then { freshShip s.Id with Team = s.Team }
                else { freshShip s.Id with Team = s.Team; Active = false; Alive = false; Stocks = 0 })
        Rng = w.Rng }

let step dt (inputs: Input[]) (w: World) =
    match w.Phase with
    | Over _ when inputs |> Array.exists (fun i -> i.Start) -> reset (fun i -> w.Ships.[i].Active) w
    | _ ->
        let k = bounds w.Time
        let fired =
            w.Ships
            |> Array.map (fun s -> join inputs.[s.Id] s |> stepShip k dt inputs.[s.Id] |> fire dt inputs.[s.Id])
        let ships = fired |> Array.map (fun (s, _, _, _, _) -> s)
        let newBullets = fired |> Array.toList |> List.collect (fun (_, b, _, _, _) -> b)
        let newMines = fired |> Array.toList |> List.collect (fun (_, _, m, _, _) -> m)
        let newRocks = fired |> Array.toList |> List.collect (fun (_, _, _, r, _) -> r)
        let shotEvents = fired |> Array.toList |> List.collect (fun (_, _, _, _, e) -> e)
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
            |> List.map (fun b -> { b with Vel = pull w.Hole dt b.Pos b.Vel })
            |> List.choose (stepBullet k w.Rocks dt)
            |> List.map (fun b -> match warpAt w.Portals b.Pos b.Vel with Some p -> { b with Pos = p } | None -> b)
        let ships, bullets, hits = resolveBullets ships bullets
        let mines, blasts, mineEvents = stepMines k dt ships (newMines @ w.Mines)
        let mines = mines |> List.map (fun m -> match warpAt w.Portals m.Pos m.Vel with Some p -> { m with Pos = p } | None -> { m with Vel = pull w.Hole dt m.Pos m.Vel })
        let ships = resolveBlasts ships blasts
        let ships, rams = resolveRams ships
        let ships, bumps = resolveAsteroids ships
        let rocks, rockEvents = stepRocks k dt (newRocks @ w.Rocks)
        let rocks = rocks |> List.map (fun r -> match warpAt w.Portals r.Pos r.Vel with Some p -> { r with Pos = p } | None -> { r with Vel = pull w.Hole dt r.Pos r.Vel })
        let ships, rockHits = resolveRocks ships rocks
        let ships, warps = resolveWarps dt w.Portals ships
        let ships = resolveHole w.Hole dt ships
        let ships, pads, picks = resolvePads (sudden w) dt ships w.Pads
        let ships, crates, rng, grabs = stepCrates dt w.Rng ships w.Crates
        let portals, portalIn, rng, portalEvents = stepPortals k dt (sudden w || race) ships rng w
        let hole, holeIn, rng, holeEvents = stepHole k dt (sudden w || race) ships rng w
        let ships, gateEvents = stepGates (w.Time + dt) ships
        let raceEnd = if ships |> Array.exists (fun s -> s.Finish > 0.) then w.RaceEnd + dt else 0.
        let settled, deaths = ships |> Array.map (settle ships rng k dt) |> Array.unzip
        let ships = Array.copy settled
        let kills = deaths |> Array.toList |> List.choose id
        for (_, victim, _, by, _) in kills do
            if by >= 0 && by <> victim then
                ships.[by] <- { ships.[by] with Kills = ships.[by].Kills + 1; Streak = ships.[by].Streak + 1 }
        { Ships = ships
          Bullets = bullets
          Mines = mines
          Rocks = rocks
          Portals = portals
          PortalIn = portalIn
          Hole = hole
          HoleIn = holeIn
          RaceEnd = raceEnd
          Pads = pads
          Crates = crates
          Rng = rng
          Phase = phase ships raceEnd
          Time = w.Time + dt
          Events =
            shotEvents
            @ beamHits
            @ zapHits
            @ hits
            @ mineEvents
            @ rams
            @ bumps
            @ rockEvents
            @ rockHits
            @ warps
            @ portalEvents
            @ holeEvents
            @ gateEvents
            @ picks
            @ grabs
            @ (kills |> List.map (fun (p, i, ring, _, _) -> Explode(p, i, ring)))
            @ (kills |> List.map (fun (_, i, ring, by, wpn) -> Downed(i, by, wpn, ring))) }
