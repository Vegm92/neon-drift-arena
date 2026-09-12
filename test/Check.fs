module Check

open Vec
open Domain
open Domain.Cfg
open Maps
open Sim
open State
open Spawn

let dt = physicsDt
let present = { noInput with Present = true }
let all i = Array.init 4 (fun _ -> i)
let run n (inputs: Input[]) w = Seq.fold (fun w _ -> step dt inputs w) w (seq { 1..n })

let place i p a (w: World) =
    { w with
        Ships =
            w.Ships
            |> Array.map (fun s -> if s.Id = i then { s with Pos = p; Angle = a; Vel = zero; Invuln = 0. } else s) }

let edit i f (w: World) = { w with Ships = w.Ships |> Array.map (fun s -> if s.Id = i then f s else s) }

let damage' amt (s: Ship) =
    let soaked = min s.Shield amt
    { s with Shield = s.Shield - soaked; Hp = s.Hp - (amt - soaked) }

let check name cond =
    if not cond then failwithf "FAIL: %s" name else printfn "ok  %s" name


[<EntryPoint>]
let main _ =
    let traverse = 2. * arenaHalf / maxSpeed
    check "end-to-end at max speed takes 8-10s" (traverse >= 8. && traverse <= 10.)

    let w0 = step dt (all present) initial
    check "all four join" (w0.Ships |> Array.forall (fun s -> s.Active && s.Alive))
    let aimed = w0 |> place 0 (spawnPos 0) 0. |> step dt (all { present with Aim = Some 3.; Steer = true })
    check "aim turns toward the stick at turn rate" (abs (aimed.Ships.[0].Angle - turnRate * dt) < 1e-6)
    let snapped = w0 |> place 0 (spawnPos 0) 0. |> step dt (all { present with Aim = Some 3. })
    check "gamepad aim snaps" (snapped.Ships.[0].Angle = 3.)

    let w1 = step dt (all { present with Fire = true }) w0
    let s0 = w1.Ships.[0]
    check "recoil pushes shooter backwards" (dot s0.Vel (ofAngle s0.Angle) < 0.)
    check "one bullet per ship" (w1.Bullets.Length = 4)

    let w2 = w0 |> place 0 zero 0. |> place 1 (v 200. 0.) 0.
    let shooter = Array.init 4 (fun i -> if i = 0 then { present with Fire = true } else present)
    let w3 = run 60 shooter w2
    check "bullet damages target" (w3.Ships.[1].Hp < hpMax)
    check "hit knocks target along bullet path" (w3.Ships.[1].Vel.X > 0.)

    let w4 = w0 |> edit 0 (fun s -> { s with Pos = zero }) |> step dt (all present)
    check "core grants a shield" (w4.Ships.[0].Shield = shieldAmount)
    check "shield is a fifth of a hull" (shieldAmount = hpMax * 0.2)
    check "core respawns in 30s" (w4.Pads.[0].RespawnIn > padRespawn)
    let w4b = w4 |> step dt (all present)
    check "core ignores an already shielded ship" (w4b.Ships.[0].Shield = shieldAmount)
    let shielded = w4 |> edit 0 (fun s -> { s with Invuln = 0. })
    let soaked = shielded |> edit 0 (damage' 12.) 
    check "shield soaks damage before the hull" (soaked.Ships.[0].Shield = shieldAmount - 12. && soaked.Ships.[0].Hp = hpMax)
    let through = shielded |> edit 0 (damage' (shieldAmount + 15.))
    check "damage past the shield hits the hull" (through.Ships.[0].Shield = 0. && through.Ships.[0].Hp = hpMax - 15.)

    let w5 = w0 |> place 0 (v (arenaHalf + killMargin + 1.) 0.) 0. |> step dt (all present)
    check "out of bounds kills and costs a stock" (not w5.Ships.[0].Alive && w5.Ships.[0].Stocks = stocks - 1)
    let w5b =
        w0 |> edit 0 (fun s -> { s with LastHit = 1; LastWeapon = Blaster })
        |> place 0 (v (arenaHalf + killMargin + 1.) 0.) 0. |> step dt (all present)
    check "knocking a ship out of the arena awards the void medal"
        (w5b.Ships.[1].VoidMedals = 1
         && w5b.Events |> List.exists (function Medal(1, "voidkill") -> true | _ -> false))
    check "explosion event emitted" (w5.Events |> List.exists (function Explode _ -> true | _ -> false))
    let w6 = run (int (respawnDelay / dt) + 2) (all present) w5
    check "respawns at a spawn point" (w6.Ships.[0].Alive && [ 0..3 ] |> List.exists (fun i -> w6.Ships.[0].Pos = spawnPos i))

    let w7 =
        w0 |> place 0 zero 0. |> place 1 (v 30. 0.) 0. |> edit 1 (fun s -> { s with Vel = v (-150.) 0. })
        |> step dt (all present)
    check "ram exchanges momentum" (w7.Ships.[0].Vel.X < -50. && w7.Ships.[1].Vel.X > -50.)

    let w7b =
        w0 |> place 0 zero (System.Math.PI / 2.) |> place 1 (v 30. 0.) 0.
        |> edit 0 (fun s -> { s with Invuln = 0.; Hp = 1. })
        |> edit 1 (fun s -> { s with Invuln = 0.; Vel = v (-150.) 0. })
        |> step dt (all present)
    check "a ram kill awards the rammer medal"
        (w7b.Ships.[0].Hp <= 0. && w7b.Ships.[1].RamKillMedals = 1
         && w7b.Events |> List.exists (function Medal(1, "ramkill") -> true | _ -> false))

    let w7d =
        w0 |> place 0 zero (System.Math.PI / 2.) |> place 1 (v 30. 0.) 0.
        |> edit 0 (fun s -> { s with Invuln = 0. })
        |> edit 1 (fun s -> { s with Invuln = 0.; Vel = v (-maxSpeed) 0. })
        |> step dt (all present)
    check "a full-speed ram one-shots a stationary, unguarded target"
        (w7d.Ships.[0].Hp <= 0.)
    check "the attacker takes no damage ramming a stationary target"
        (w7d.Ships.[1].Hp = hpMax)
    check "an attacker's nose pointing at its target is not a brace: no parry on the victim"
        (w7d.Ships.[0].Stun = 0. && w7d.Ships.[1].Stun = 0.)

    let w7d2 =
        w0 |> place 0 zero (System.Math.PI / 2.) |> place 1 (v 30. 0.) System.Math.PI
        |> edit 0 (fun s -> { s with Invuln = 0. })
        |> edit 1 (fun s -> { s with Invuln = 0.; Vel = v (-maxSpeed) 0. })
        |> step dt (all present)
    check "an attacker facing its own direction of travel still doesn't parry an unguarded hit"
        (w7d2.Ships.[0].Hp <= 0. && w7d2.Ships.[0].Stun = 0. && w7d2.Ships.[1].Stun = 0.)

    let w7e =
        w0 |> place 0 zero (System.Math.PI / 3.) |> place 1 (v 30. 0.) 0.
        |> edit 0 (fun s -> { s with Invuln = 0. })
        |> edit 1 (fun s -> { s with Invuln = 0.; Vel = v (-maxSpeed) 0. })
        |> step dt (all present)
    check "facing the incoming ram cuts the damage you take"
        (w7e.Ships.[0].Hp > w7d.Ships.[0].Hp && w7e.Ships.[0].Hp < hpMax)

    let w7f =
        w0 |> place 0 zero 0. |> place 1 (v 30. 0.) 0.
        |> edit 0 (fun s -> { s with Invuln = 0. })
        |> edit 1 (fun s -> { s with Invuln = 0.; Vel = v (-maxSpeed) 0. })
        |> step dt (all present)
    check "squarely facing a hard enough ram parries it: both ships clash, spark and stun"
        (w7f.Ships.[0].Stun > 0. && w7f.Ships.[1].Stun > 0.
         && w7f.Events |> List.exists (function Bump _ -> true | _ -> false))
    check "a parry announces both ships by id, for the HUD shout"
        (w7f.Events |> List.exists (function Parried(_, a, b) -> (a = 0 && b = 1) || (a = 1 && b = 0) | _ -> false))
    check "a stunned ship cannot steer out of it (existing stun contract)"
        (let stepped = w7f |> step dt (all { present with Thrust = true })
         stepped.Ships.[0].Thrusting = 0. && stepped.Ships.[1].Thrusting = 0.)

    let mashing =
        Array.init 4 (fun i ->
            if i = 0 then { present with Thrust = true }
            elif i = 1 then { present with Thrust = true }
            else noInput)
    let w7c =
        w0 |> place 0 zero 0. |> place 1 (v 20. 0.) System.Math.PI
        |> edit 0 (fun s -> { s with Invuln = 0. })
        |> edit 1 (fun s -> { s with Invuln = 0. })
        |> run 60 mashing
    check "two ships thrusting into each other still push apart"
        (len (w7c.Ships.[1].Pos - w7c.Ships.[0].Pos) > 2. * shipRadius + 5.)
    let rams =
        Seq.fold
            (fun (w, n) _ ->
                let w = step dt mashing w
                w, n + (w.Events |> List.sumBy (function Ram _ -> 1 | _ -> 0)))
            (w0 |> place 0 zero 0. |> place 1 (v 20. 0.) System.Math.PI
                |> edit 0 (fun s -> { s with Invuln = 0. })
                |> edit 1 (fun s -> { s with Invuln = 0. }), 0)
            (seq { 1..60 })
        |> snd
    check "a shoving match does not spam ram sparks" (rams <= 3)

    let two = Array.init 4 (fun i -> if i < 2 then present else noInput)
    let w8 = step dt two initial |> edit 1 (fun s -> { s with Hp = 0.; Stocks = 1 }) |> step dt two
    check "last ship standing wins" (w8.Phase = Over(Some 0))
    let w9 = step dt (Array.init 4 (fun i -> if i = 0 then { present with Start = true } else noInput)) w8
    check "rematch resets stocks" (w9.Phase = Playing && w9.Ships.[1].Stocks = stocks)

    let three = Array.init 4 (fun i -> if i < 3 then present else noInput)
    let teamed = initial |> withTeams [| 1; 1; 2; 0 |] |> step dt three
    let w11 = teamed |> edit 2 (fun s -> { s with Hp = 0.; Stocks = 1 }) |> step dt three
    check "team wins when the other side is out" (match w11.Phase with Over(Some i) -> w11.Ships.[i].Team = 1 | _ -> false)
    let w12 = teamed |> edit 0 (fun s -> { s with Hp = 0.; Stocks = 1 }) |> step dt three
    check "match continues while a teammate lives" (w12.Phase = Playing)
    let w13 =
        teamed |> place 0 zero 0. |> place 1 (v 30. 0.) 0. |> edit 1 (fun s -> { s with Vel = v (-150.) 0. })
        |> step dt three
    check "teammates ram without damage" (w13.Ships.[0].Hp = hpMax && w13.Ships.[1].Hp = hpMax)
    check "teammates keep their team on respawn" (w13.Ships.[0].Team = 1 && (reset (fun _ -> true) w13).Ships.[1].Team = 1)
    check "reset drops players who left" (not (reset (fun i -> i <> 1) w13).Ships.[1].Active)

    let clear (p: V2) margin = asteroids |> Array.forall (fun a -> len (a.Pos - p) > a.Radius + margin)
    let ray (dir: V2) = [ 180. .. 40. .. 900. ] |> List.forall (fun t -> clear (dir * t) shipRadius)
    for i in 0 .. layouts.Length - 1 do
        setLayout i
        let tag name = sprintf "arena %d: %s" i name
        check (tag "pads sit clear of rock") (initial.Pads |> Array.forall (fun p -> clear p.Pos (padRadius + shipRadius)))
        check (tag "spawns are clear") ([ 0..3 ] |> List.forall (fun k -> clear (spawnPos k) (3. * shipRadius)))
        check (tag "crate spots are clear") (cratePositions |> Array.forall (fun p -> clear p (crateRadius + shipRadius)))
        check (tag "rocks do not overlap") (
            asteroids
            |> Array.forall (fun a ->
                asteroids |> Array.forall (fun b -> System.Object.ReferenceEquals(a, b) || len (a.Pos - b.Pos) > a.Radius + b.Radius)))
        check (tag "every rock is inside the arena") (
            asteroids
            |> Array.forall (fun a ->
                abs a.Pos.X + a.Radius < arenaHalf
                && abs a.Pos.Y + a.Radius < arenaHalf
                && abs a.Pos.X + abs a.Pos.Y + a.Radius < diagLimit ()))
        if isTrack i then
            check (tag "every gate is on the road") (road |> Array.forall (fun g -> abs g.X + trackWidth / 2. < arenaHalf && abs g.Y + trackWidth / 2. < arenaHalf && abs g.X + abs g.Y + trackWidth / 2. < diagLimit ()))
            check (tag "checkpoints sit on road points") (gates |> Array.forall (fun g -> Array.contains g road) && gates.[0] = road.[0])
            check (tag "the road never doubles back on itself") (
                corners
                |> Array.mapi (fun i g -> i, g)
                |> Array.forall (fun (i, g) ->
                    corners |> Array.mapi (fun j h -> j, h) |> Array.forall (fun (j, h) -> abs (i - j) <= 1 || abs (i - j) >= corners.Length - 1 || len (g - h) > trackWidth * 0.9)))
            check (tag "the road is smoothed through every corner") (road.Length = corners.Length * Track.smoothSamples && corners |> Array.forall (fun c -> Array.contains c road))
            check (tag "a race track is a big arena") (arenaHalf = raceHalf)
        else
            check (tag "the core has an open approach") (
                [ 0. .. 45. .. 315. ]
                |> List.exists (fun d ->
                    let a = d * System.Math.PI / 180.
                    ray (v (cos a) (sin a))))
            check (tag "one shield core") ((initial.Pads |> Array.filter (fun p -> p.Kind = 2)).Length = 1)
            check (tag "four heal pads") ((initial.Pads |> Array.filter (fun p -> p.Kind = 1)).Length = 4)
            check (tag "eight crate spots") (cratePositions.Length = 8)
    setLayout 0

    let strafer = Array.init 4 (fun i -> if i = 0 then { present with Strafe = 1. } else present)
    let w10 = w0 |> place 0 zero 0. |> run 30 strafer
    check "E strafes to the ship's right" (w10.Ships.[0].Vel.Y > 1. && abs w10.Ships.[0].Vel.X < 1e-6 && w10.Ships.[0].Angle = 0.)

    let reverser = Array.init 4 (fun i -> if i = 0 then { present with Reverse = true } else present)
    let thruster = Array.init 4 (fun i -> if i = 0 then { present with Thrust = true } else present)
    let rev = (w0 |> place 0 zero 0. |> run 30 reverser).Ships.[0]
    let back = rev.Vel.X
    check "reversing flag drives the retro flames" rev.Reversing
    let fwd = (w0 |> place 0 zero 0. |> run 30 thruster).Ships.[0].Vel.X
    check "S reverses at a quarter of thrust" (back < 0. && abs (abs back / fwd - reverseFactor) < 0.02)

    let rock = asteroids.[0]
    let w11 =
        w0 |> place 0 (rock.Pos + v (-(rock.Radius + shipRadius + 2.)) 0.) 0.
        |> edit 0 (fun s -> { s with Vel = v 200. 0. }) |> run 3 (all present)
    let s11 = w11.Ships.[0]
    check "asteroid bounces ship back" (s11.Vel.X < 0.)
    check "asteroid stuns on impact" (s11.Stun > 0.)
    // Spin is torque now, not a canned flourish: a square hit has no lever arm
    // to turn, an off-centre one scrapes and tumbles, and the harder scrape
    // must tumble harder.
    check "a square asteroid hit does not spin the ship" (abs s11.Spin < 1e-6)
    let graze off =
        (w0 |> place 0 (rock.Pos + v (-(rock.Radius + shipRadius + 2.)) off) 0.
         |> edit 0 (fun s -> { s with Vel = v 200. 0. }) |> run 3 (all present)).Ships.[0].Spin
    check "an off-centre asteroid hit spins the ship" (abs (graze 14.) > 1.)
    check "a deeper scrape spins harder" (abs (graze 14.) > abs (graze 6.))
    check "opposite sides spin opposite ways" (graze 14. * graze -14. < 0.)
    let w12 = run 10 (Array.init 4 (fun i -> if i = 0 then { present with Turn = 1.; Thrust = true } else present)) w11
    check "stunned ship ignores input" (w12.Ships.[0].Thrusting = 0.)
    let w13 = run (int (asteroidStun / dt) + 2) (all present) w12
    check "stun wears off" (w13.Ships.[0].Stun = 0.)
    let spun = w0 |> place 0 zero 0. |> edit 0 (fun s -> { s with Spin = 6. }) |> run 400 (all present)
    check "a tumble damps down to a clean stop" (spun.Ships.[0].Spin = 0.)

    let w14 =
        w0 |> place 0 (rock.Pos + v (-(rock.Radius + 60.)) 0.) 0.
        |> run 30 (Array.init 4 (fun i -> if i = 0 then { present with Fire = true } else present))
    check "asteroids block bullets" (w14.Bullets |> List.forall (fun b -> b.Pos.X < rock.Pos.X))

    let firing = Array.init 4 (fun i -> if i = 0 then { present with Fire = true } else present)
    let using = Array.init 4 (fun i -> if i = 0 then { present with Special = true } else present)

    let healPad = initial.Pads |> Array.find (fun p -> p.Kind = 1)
    check "four heal pads" ((initial.Pads |> Array.filter (fun p -> p.Kind = 1)).Length = 4)
    let wh = w0 |> place 0 healPad.Pos 0. |> edit 0 (fun s -> { s with Hp = 30. }) |> step dt (all present)
    check "heal pad restores hp" (wh.Ships.[0].Hp = 30. + healAmount)
    check "heal pad respawns in 30s" (wh.Pads |> Array.exists (fun p -> p.Kind = 1 && p.RespawnIn > padRespawn))
    let whole = w0 |> place 0 healPad.Pos 0. |> step dt (all present)
    check "heal pad ignores a healthy ship" (whole.Pads |> Array.forall (fun p -> p.Kind = 0 || p.RespawnIn = 0.))

    let sick = w0 |> place 0 zero 0. |> edit 0 (fun s -> { s with Hp = 10. }) |> run 30 thruster
    let well = w0 |> place 0 zero 0. |> run 30 thruster
    check "damaged ships fly a quarter slower" (abs (sick.Ships.[0].Vel.X / well.Ships.[0].Vel.X - hurtFactor) < 0.02)
    check "hurting reports the smoke threshold" (hurting sick.Ships.[0] && not (hurting well.Ships.[0]))

    let cook = w0 |> place 0 zero 0. |> edit 0 (fun s -> { s with Heat = heatMax - 1. }) |> step dt firing
    check "overheat locks the blaster" (cook.Ships.[0].Locked > 0.)
    check "overheat emits a cue" (cook.Events |> List.exists (function Cooked _ -> true | _ -> false))
    check "locked blaster does not fire" ((run 5 firing cook).Bullets.Length = cook.Bullets.Length)
    let thawing = run (int (heatMax / heatCool / dt) - 2) firing cook
    check "the lock outlives the heat bar" (thawing.Ships.[0].Locked > 0. && thawing.Ships.[0].Heat > 0.)
    check "the blaster returns only once fully cool" ((run 4 firing thawing).Ships.[0].Heat < heatPerShot * 1.01)

    check "crate spots are clear" (cratePositions |> Array.forall (fun p -> clear p (crateRadius + shipRadius)))
    check "four crates at all times" (initial.Crates.Length = 4)
    check "crates start apart" ((initial.Crates |> Array.distinctBy (fun c -> c.Pos)).Length = 4)
    let grab = w0 |> place 0 initial.Crates.[0].Pos 0. |> step dt (all present)
    check "crate grants a loaded weapon" (grab.Ships.[0].Weapon <> Blaster && grab.Ships.[0].Ammo > 0)
    check "crate respawns in 10s on a free spot"
        (grab.Crates.[0].RespawnIn > 0.
         && cratePositions |> Array.contains grab.Crates.[0].Pos
         && (grab.Crates |> Array.distinctBy (fun c -> c.Pos)).Length = 4)

    let charging =
        w0 |> place 0 zero 0. |> place 1 (v 1000. 0.) 0.
        |> edit 0 (fun s -> { s with Weapon = Rail; Ammo = 2 })
        |> run (int (railCharge / dt) + 2) using
    check "rail holds at full charge instead of firing itself"
        (charging.Ships.[0].Charge = railCharge && charging.Ships.[0].Ammo = 2 && charging.Ships.[1].Hp = hpMax)
    let holdingOn = charging |> run (int (railCharge / dt)) using
    check "a held charge never overflows" (holdingOn.Ships.[0].Charge = railCharge && holdingOn.Ships.[1].Hp = hpMax)
    let railed = charging |> run 2 (all present)
    check "rail is a one hit kill" (not railed.Ships.[1].Alive && railed.Ships.[1].Stocks = stocks - 1)
    check "rail spends a charge" (railed.Ships.[0].Ammo = 1)
    check "rail kicks the shooter back" (railed.Ships.[0].Vel.X < -railRecoil * 0.5)
    let died =
        charging |> edit 0 (fun s -> { s with Alive = false; RespawnIn = respawnDelay })
        |> run 2 (all present)
    check "dying at full charge fires nothing" (died.Ships.[0].Charge = 0. && died.Ships.[0].Ammo = 2)
    let early =
        w0 |> place 0 zero 0. |> place 1 (v 1000. 0.) 0.
        |> edit 0 (fun s -> { s with Weapon = Rail; Ammo = 2 })
        |> run 30 using |> run 2 (all present)
    check "releasing early drops the charge" (early.Ships.[0].Charge = 0. && early.Ships.[1].Hp = hpMax)

    let mined =
        w0 |> place 0 zero 0. |> place 1 (v 400. 0.) 0.
        |> edit 0 (fun s -> { s with Weapon = Mines; Ammo = 4 })
        |> step dt using
    check "mine drops dormant behind the ship" (mined.Mines.Length = 1 && mined.Mines.Head.Fuse < 0.)
    let idle = run 10 (all present) mined
    check "mine stays dormant out of range" (idle.Mines |> List.forall (fun m -> m.Fuse < 0.))
    let lured = idle |> place 1 (v 60. 0.) 0. |> step dt (all present)
    check "mine arms when an enemy closes in" (lured.Mines |> List.forall (fun m -> m.Fuse > 0.))
    let boom = run (int (mineFuse / dt) + 2) (all present) lured
    check "mine blows 1.5s after arming" (boom.Mines.IsEmpty && boom.Ships.[1].Hp < hpMax)

    let swarmed =
        w0 |> place 0 zero 0. |> edit 0 (fun s -> { s with Weapon = Swarm; Ammo = 3 })
        |> run 30 using
    check "swarm fires one seeker per press" (swarmed.Bullets.Length = 1 && swarmed.Ships.[0].Ammo = 2)
    let both =
        w0 |> place 0 zero 0. |> edit 0 (fun s -> { s with Weapon = Swarm; Ammo = 3 })
        |> step dt (Array.init 4 (fun i -> if i = 0 then { present with Fire = true; Special = true } else present))
    check "blaster and special fire together" (both.Bullets.Length = 2 && both.Ships.[0].Ammo = 2)
    check "seekers run at seeker speed" (swarmed.Bullets |> List.forall (fun b -> abs (len b.Vel - seekerSpeed) < 1.))

    let pulsed =
        w0 |> place 0 zero 0. |> place 1 (v 200. 0.) 0. |> place 2 (v (-200.) 0.) 0.
        |> edit 0 (fun s -> { s with Weapon = Pulse; Ammo = 3 })
        |> step dt using
    check "pulse shoves the ship in front" (pulsed.Ships.[1].Vel.X > 100.)
    check "pulse spares the ship behind" (abs pulsed.Ships.[2].Vel.X < 1.)
    check "pulse deals no damage" (pulsed.Ships.[1].Hp = hpMax)
    check "pulse kicks the user back" (pulsed.Ships.[0].Vel.X < 0.)

    let zapped =
        w0 |> place 0 zero 0. |> place 1 (v 100. 0.) 0. |> place 2 (v (-100.) 0.) 0. |> place 3 (v 600. 0.) 0.
        |> edit 0 (fun s -> { s with Weapon = Scatter; Ammo = 2 })
        |> step dt using
    check "scatter stuns the ship in front for a second" (zapped.Ships.[1].Stun >= scatterStun - dt)
    check "scatter hurts a little" (zapped.Ships.[1].Hp = hpMax - scatterDamage)
    check "scatter spares ships behind or far" (zapped.Ships.[2].Stun = 0. && zapped.Ships.[3].Stun = 0.)
    check "scatter has two shots" (zapped.Ships.[0].Ammo = 1 && zapped.Ships.[0].Weapon = Scatter)

    let towed =
        w0 |> place 0 zero 0. |> place 1 (v 300. 0.) 0.
        |> edit 0 (fun s -> { s with Weapon = Tractor; Ammo = 2 })
        |> run (int (railCharge / dt) + 20) using
    check "tractor latches the enemy ahead after charging" (towed.Ships.[0].Tow = TowShip 1 && towed.Ships.[0].Ammo = 1)
    let behind =
        w0 |> place 0 zero 0. |> place 1 (v (-300.) 0.) 0.
        |> edit 0 (fun s -> { s with Weapon = Tractor; Ammo = 2 })
        |> run (int (railCharge / dt) + 2) using
    check "tractor ignores what is behind" (behind.Ships.[0].Tow <> TowShip 1)
    let early =
        w0 |> place 0 zero 0. |> place 1 (v 300. 0.) 0.
        |> edit 0 (fun s -> { s with Weapon = Tractor; Ammo = 2 })
        |> run 30 using |> run 2 (all present)
    check "tractor drops its charge when released" (early.Ships.[0].Charge = 0. && early.Ships.[0].Ammo = 2)
    check "tractor reaches half a blaster shot" (tractorRange = bulletSpeed * bulletLife * 0.5)
    check "tractor pulls the enemy toward us" (towed.Ships.[1].Vel.X < -50. && abs towed.Ships.[0].Vel.X < 1e-6)
    let letGo = run (int (tractorTime / dt) + 2) (all present) towed
    check "tractor lets go after its time" (letGo.Ships.[0].Tow = NoTether)
    let hooked =
        w0 |> place 0 (rock.Pos + v (-(rock.Radius + 300.)) 0.) 0. |> place 1 (v 1200. 1200.) 0.
        |> place 2 (v (-1200.) 1200.) 0. |> place 3 (v 1200. (-1200.)) 0.
        |> edit 0 (fun s -> { s with Weapon = Tractor; Ammo = 2 })
        |> run (int (railCharge / dt) + 20) using
    check "tractor pulls us to a rock when no ship is near"
        ((match hooked.Ships.[0].Tow with TowRock _ -> true | _ -> false) && hooked.Ships.[0].Vel.X > 50.)
    let ringed = w0 |> place 0 (v (arenaHalf + killMargin + 1.) 0.) 0. |> step dt (all present)
    check "ring outs are flagged" (ringed.Events |> List.exists (function Explode(_, _, r) -> r | _ -> false))
    check "ring outs are counted" (ringed.Ships.[0].Rings = 1)
    let shotDown =
        w0 |> place 0 zero 0. |> place 1 (v 200. 0.) 0.
        |> edit 1 (fun s -> { s with Hp = 1. }) |> run 60 firing
    check "kills are credited to the shooter" (shotDown.Ships.[0].Kills = 1 && shotDown.Ships.[0].Streak = 1)
    check "hits are counted for accuracy" (shotDown.Ships.[0].Hits > 0 && shotDown.Ships.[0].Shots > 0)
    let downed =
        Seq.fold (fun (w, seen) _ -> let w = step dt firing w in w, seen @ w.Events) (w0 |> place 0 zero 0. |> place 1 (v 200. 0.) 0. |> edit 1 (fun s -> { s with Hp = 1. }), []) (seq { 1..60 }) |> snd
    check "kill feed names the shooter and the blaster" (downed |> List.exists (function Downed(1, 0, Blaster, false) -> true | _ -> false))

    let camping = w0 |> place 1 (spawnPos 0 + v 50. 0.) 0. |> place 2 zero 0. |> place 3 (v 100. 100.) 0.
    let backAgain = camping |> edit 0 (fun s -> { s with Alive = false; RespawnIn = dt / 2. }) |> step dt (all present)
    check "respawn avoids the camped spawn point" (backAgain.Ships.[0].Alive && len (backAgain.Ships.[0].Pos - spawnPos 0) > 100.)
    let negativeRng =
        { camping with Rng = -1234567 } |> edit 0 (fun s -> { s with Alive = false; RespawnIn = dt / 2. }) |> step dt (all present)
    check "respawn survives a negative rng state" (negativeRng.Ships.[0].Alive && [ 0..3 ] |> List.exists (fun i -> negativeRng.Ships.[0].Pos = spawnPos i))


    // Two bots left alone used to open every match by flying straight down each
    // other's throat, trading both hulls away and burning all three stocks each
    // inside a minute. The opening has to survive on its own.
    let feud =
        setLayout 0
        let joined = step dt (Array.init 4 (fun i -> if i < 2 then present else noInput)) initial
        Seq.fold
            (fun (w, worst) _ ->
                let w = step dt (Array.init 4 (fun i -> if i < 2 then bot w i else noInput)) w
                w, min worst (if w.Ships.[0].Alive || w.Ships.[1].Alive then 2 else 0))
            (joined, 2)
            (seq { 1 .. 120 * 45 })
    let feudW, bothDown = feud
    check "two bots never trade themselves out in the same breath" (bothDown = 2)
    check "two bots duelling for 45 s do not burn through their stocks"
        (feudW.Ships.[0].Stocks > 0 && feudW.Ships.[1].Stocks > 0)




    let duel = w0 |> place 0 zero 0. |> place 1 (v 300. 0.) 0. |> place 2 (v (-1200.) 1200.) 0. |> place 3 (v 1200. (-1200.)) 0.
    let b = bot duel 0
    check "bot fires at the ship ahead" (b.Fire && b.Aim = Some 0. && not b.Thrust)
    let turned = bot (duel |> place 1 (v 0. 300.) 0.) 0
    check "bot steers toward its target and holds fire" (turned.Aim = Some (System.Math.PI / 2.) && not turned.Fire && turned.Thrust)
    let crossing = bot (duel |> edit 1 (fun s -> { s with Vel = v 0. 200. })) 0
    check "bot leads a moving target" (match crossing.Aim with Some a -> a > 0.15 && a < 0.6 | None -> false)
    let rock = asteroids.[0]
    let rolling = duel |> place 0 (rock.Pos - v (rock.Radius + 150.) 0.) 0. |> edit 0 (fun s -> { s with Vel = v 200. 0. }) |> place 1 (rock.Pos + v (rock.Radius + 300.) 0.) 0.
    let swerve = bot rolling 0
    check "bot swerves around a rock in its path" (match swerve.Aim with Some a -> abs a > 0.5 | None -> false)
    check "bot holds fire when a rock blocks the shot" (not swerve.Fire && swerve.Thrust)
    let headOn hp0 =
        duel |> place 1 (v 200. 0.) System.Math.PI
        |> edit 0 (fun s -> { s with Vel = v 250. 0.; Hp = hp0; Shield = 0. })
        |> edit 1 (fun s -> { s with Vel = v (-250.) 0.; Hp = 20.; Shield = 0. })
    let veering = bot (headOn 20.) 0
    check "bot veers from a head-on that would kill it" (match veering.Aim with Some a -> abs a > 0.5 && veering.Thrust && veering.Boost | None -> false)
    check "bot keeps a ram that only kills the target" ((bot (headOn 100.) 0).Aim = Some 0.)
    // A trade that guts both hulls kills both on the next touch, so an even
    // head-on has to break off even though neither ship dies to this one hit.
    let mutual =
        duel |> place 1 (v 200. 0.) System.Math.PI
        |> edit 0 (fun s -> { s with Vel = v 250. 0.; Angle = 0.3; Hp = hpMax; Shield = 0. })
        |> edit 1 (fun s -> { s with Vel = v (-250.) 0.; Angle = System.Math.PI - 0.3; Hp = hpMax; Shield = 0. })
    check "bot breaks off an even head-on instead of trading both hulls away"
        (match (bot mutual 0).Aim with Some a -> abs a > 0.5 | None -> false)
    // Turning for the centre used to replace the whole priority chain, so a bot
    // that drifted out to the rim parked there, nose inward, firing and never
    // thrusting clear of the band.
    let rim = duel |> place 0 (v (arenaHalf - 100.) 0.) 0. |> place 1 (v 300. 0.) 0.
    let back = bot rim 0
    check "a bot on the rim turns inward and thrusts off it"
        (match back.Aim with Some a -> abs (abs a - System.Math.PI) < 0.2 && back.Thrust | None -> false)
    let cornered = duel |> place 0 (v (arenaHalf - 100.) 0.) 0. |> place 1 (v 2000. 0.) 0. |> edit 0 (fun s -> { s with Hp = 5. })
    check "fleeing the rim outranks hunting a pad" (bot cornered 0).Thrust
    // With the enemy respawning there is no target, and a bot used to hand back
    // no input at all: it coasted on its last heading and sailed off the edge.
    let alone =
        w0 |> place 0 (v (arenaHalf - 300.) 0.) 0.
        |> edit 0 (fun s -> { s with Vel = v 240. 0. })
        |> edit 1 (fun s -> { s with Alive = false; Active = true; RespawnIn = 2.5 })
        |> edit 2 (fun s -> { s with Alive = false; Active = true; RespawnIn = 2.5 })
        |> edit 3 (fun s -> { s with Alive = false; Active = true; RespawnIn = 2.5 })
    let lonely = bot alone 0
    check "a bot with nobody to chase still flies itself off the rim"
        (match lonely.Aim with Some a -> abs (abs a - System.Math.PI) < 0.2 && lonely.Steer | None -> false)
    let survived =
        Seq.fold (fun w _ -> step dt (Array.init 4 (fun i -> if i = 0 then bot w i else noInput)) w) alone (seq { 1 .. 120 * 4 })
    check "a bot left alone does not drift out of the arena and die" survived.Ships.[0].Alive
    // A lull is dead time, so the bar for a detour drops to "not already full".
    let topping =
        bot (w0 |> place 0 zero 0. |> edit 0 (fun s -> { s with Boost = boostMax * 0.5 })
             |> edit 1 (fun s -> { s with Alive = false; Active = true; RespawnIn = 2.5 })
             |> edit 2 (fun s -> { s with Alive = false; Active = true; RespawnIn = 2.5 })
             |> edit 3 (fun s -> { s with Alive = false; Active = true; RespawnIn = 2.5 })) 0
    check "a bot tops up its boost while there is nothing to fight" topping.Thrust
    // The class of fault behind all of this: a branch that hands back no input
    // at all, leaving a live ship coasting like scenery. A live bot always has
    // somewhere it means to be, from anywhere in the arena, with or without a
    // fight on.
    let empty =
        w0 |> edit 1 (fun s -> { s with Alive = false; Active = true; RespawnIn = 2.5 })
        |> edit 2 (fun s -> { s with Alive = false; Active = true; RespawnIn = 2.5 })
        |> edit 3 (fun s -> { s with Alive = false; Active = true; RespawnIn = 2.5 })
    let inert =
        [ for gx in -4 .. 4 do
            for gy in -4 .. 4 do
                let p = v (float gx * arenaHalf / 4.2) (float gy * arenaHalf / 4.2)
                let b = bot (empty |> place 0 p 0. |> edit 0 (fun s -> { s with Boost = boostMax })) 0
                if b.Aim.IsNone || not b.Steer then yield p ]
    check "a live bot is never handed a dead stick, anywhere in the arena" inert.IsEmpty
    let roaming =
        Seq.fold (fun w _ -> step dt (Array.init 4 (fun i -> if i = 0 then bot w i else noInput)) w) (empty |> place 0 zero 0.) (seq { 1 .. 120 * 20 })
    check "a bot with no fight keeps flying instead of parking" (len roaming.Ships.[0].Vel > 40. && roaming.Ships.[0].Alive)
    let close = duel |> place 1 (v 220. 0.) 0.
    let weaveA = bot close 0
    let weaveB = bot { close with Time = close.Time + 0.7 } 0
    check "bots weave across the line while duelling up close"
        (weaveA.Strafe <> 0. && weaveB.Strafe <> 0. && weaveA.Strafe <> weaveB.Strafe)
    check "bots hold a straight line at range" ((bot (duel |> place 1 (v 900. 0.) 0.) 0).Strafe = 0.)
    // The lead was three rounds of a fixed point that contracts by |v| / speed:
    // wobbly for the blaster and divergent for the swarm. Fire along the aim it
    // gives and the shot has to actually arrive with the target.
    let interceptMiss wpn (tv: V2) =
        let w = duel |> place 1 (v 400. 0.) 0. |> edit 0 (fun s -> { s with Weapon = wpn }) |> edit 1 (fun s -> { s with Vel = tv })
        let a = (bot w 0).Aim |> Option.defaultValue 0.
        let speed = if wpn = Swarm then seekerSpeed else bulletSpeed
        [ for k in 1 .. 240 -> float k / 120. ]
        |> List.map (fun tm -> len (ofAngle a * speed * tm - (v 400. 0. + tv * tm)))
        |> List.min
    check "the blaster lead actually intercepts a crossing target" (interceptMiss Blaster (v 0. 200.) < shipRadius)
    // Seekers home, so they do not need the lead to land - what matters is that
    // asking for one against a target the seeker cannot outrun no longer throws
    // the aim somewhere arbitrary, which is what the divergent fixed point did.
    let swarmAim (tv: V2) =
        let w = duel |> place 1 (v 400. 0.) 0. |> edit 0 (fun s -> { s with Weapon = Swarm }) |> edit 1 (fun s -> { s with Vel = tv })
        (bot w 0).Aim |> Option.defaultValue nan
    // When no intercept exists the only sane aim is the target itself. The old
    // fixed point had nothing to converge on and simply ran away: three rounds
    // of a 1.27x expansion put the nose 0.65 rad off a target sitting dead
    // ahead, and the further it ran the further it pointed.
    check "an unreachable swarm lead aims at the target rather than running away"
        ([ v 260. 260.; v 300. 0.; v 200. 240. ]
         |> List.forall (fun tv -> let a = swarmAim tv in not (System.Double.IsNaN a) && abs (atan2 (sin a) (cos a)) < 0.1))
    // A solver that wobbles turns a smooth world into a twitching one, so nudge
    // the target's speed in tiny steps: the aim must follow smoothly. The old
    // fixed point jumped, which is the twitch seen from the cockpit.
    let sweep =
        [ for k in 0 .. 200 -> float k * 0.5 ]
        |> List.map (fun sp ->
            let w = duel |> place 1 (v 420. 0.) 0. |> edit 1 (fun s -> { s with Vel = v 0. sp })
            (bot w 0).Aim |> Option.defaultValue nan)
    let roughest =
        sweep |> List.pairwise |> List.map (fun (a, b) -> abs (atan2 (sin (b - a)) (cos (b - a)))) |> List.max
    check "the aim tracks a target's speed smoothly instead of jumping" (roughest < 0.02)
    // Measured across all eight arenas, four bots, sixty seconds: ram accounted
    // for 1% of the damage and guns 99%, so the opening slaughter was never a
    // ramming problem. It was 249 nose-to-nose charges where both bots flew
    // down each other's throat trading fire. Declining the trade halved them.
    // Far enough out that veer is not in play yet - this is about how the bot
    // chooses to approach, not how it bails out of a contact already on top of
    // it.
    let charge =
        duel |> place 0 zero 0. |> place 1 (v 650. 0.) System.Math.PI
        |> edit 0 (fun s -> { s with Vel = v 240. 0.; Hp = hpMax })
        |> edit 1 (fun s -> { s with Vel = v (-240.) 0.; Hp = hpMax })
    let offLine (b: Input) = match b.Aim with Some a -> abs (atan2 (sin a) (cos a)) > 0.3 | None -> false
    check "an even head-on charge is declined by both sides" (offLine (bot charge 0) && offLine (bot charge 1))
    // and they must slide past rather than both turning into the same gap
    let aimOf (b: Input) = match b.Aim with Some a -> a | None -> 0.
    check "the two sides of a declined charge pass on opposite sides"
        (sin (aimOf (bot charge 0)) * sin (aimOf (bot charge 1)) < 0.)
    let ahead = charge |> edit 1 (fun s -> { s with Hp = 20. })
    check "a bot well ahead on health presses the charge home" (not (offLine (bot ahead 0)))
    check "a distant approach is not treated as a charge"
        (not (offLine (bot (charge |> place 1 (v 1100. 0.) System.Math.PI) 0)))
    check "the declined charge is a pass, not a panic: veer is not what fired"
        (match (bot charge 0).Aim with Some a -> abs (abs a - passArc) < 0.05 | None -> false)
    let sucked = bot { duel with Hole = Some { Pos = v 150. 0.; Life = 5. } } 0
    check "bot flees a gravity well" (match sucked.Aim with Some a -> abs a > 2.5 && sucked.Thrust && sucked.Boost | None -> false)
    let padAim kind (b: Input) =
        let p = duel.Pads |> Array.filter (fun p -> p.Kind = kind && p.RespawnIn <= 0.) |> Array.minBy (fun p -> len p.Pos)
        match b.Aim with Some a -> abs (atan2 (sin (a - atan2 p.Pos.Y p.Pos.X)) (cos (a - atan2 p.Pos.Y p.Pos.X))) < 0.2 && b.Thrust | None -> false
    check "bot heads for a boost pad when dry" (bot (duel |> place 1 (v 0. 1200.) 0. |> edit 0 (fun s -> { s with Boost = 5. })) 0 |> padAim 0)
    check "bot heads for a heal pad when hurting" (bot (duel |> place 1 (v 0. 1200.) 0. |> edit 0 (fun s -> { s with Hp = 10. })) 0 |> padAim 1)

    let out = w0 |> place 0 zero 0. |> edit 0 (fun s -> { s with Alive = false; Stocks = 0; LaunchAngle = 0. })
    let turning = run 12 (Array.init 4 (fun i -> if i = 0 then { present with Turn = 1. } else present)) out
    check "a launcher sits on the rim and turns with the stick" (launcher turning.Ships.[0] && turning.Ships.[0].LaunchAngle > 0.3 && abs (turning.Ships.[0].Pos.X - arenaHalf) < 1. && turning.Ships.[0].Pos.Y > 0.)
    let hurled = out |> step dt firing
    let rock = hurled.Rocks.Head
    check "a launcher hurls a rock from the rim toward the centre" (hurled.Rocks.Length = 1 && rock.Pos.X > arenaHalf - 10. && rock.Vel.X < 0. && hurled.Ships.[0].LaunchCd > 0.)
    check "the launcher reloads slowly" ((run 10 firing hurled).Rocks.Length = 1)
    let struck = hurled |> place 1 zero 0. |> run 600 (all present)
    check "a rolling rock stuns, hurts and credits the launcher" (struck.Ships.[1].Hp < hpMax && struck.Ships.[1].LastHit = 0 && struck.Ships.[1].LastWeapon = Rock)
    let walled = hurled |> place 1 (v (arenaHalf - 400.) 0.) 0. |> step dt (Array.init 4 (fun i -> if i = 1 then { present with Fire = true } else present))
    let shielded = run 60 (all present) walled
    check "rocks stop bullets" (walled.Bullets.Length = 1 && shielded.Bullets.IsEmpty && shielded.Rocks.Length = 1)
    let gate = { A = v -500. 0.; B = v 500. 0.; Life = 5. }
    let gated = { w0 with Portals = [ gate ] } |> place 0 (v -500. 0.) 0. |> edit 0 (fun s -> { s with Vel = v 200. 0. })
    let warped = step dt (all present) gated
    check "a wormhole moves a ship to the other end with its velocity" (warped.Ships.[0].Pos.X > 500. && warped.Ships.[0].Pos.X < 600. && warped.Ships.[0].Vel.X > 150.)
    check "a ship cannot bounce straight back through" ((run 30 (all present) (warped |> place 0 (v 500. 0.) 0. |> edit 0 (fun s -> { s with Vel = v -200. 0. }))).Ships.[0].Pos.X > 400.)
    let shotThrough = gated |> place 0 (v -700. 0.) 0. |> edit 0 (fun s -> { s with Vel = zero }) |> step dt firing |> run 45 (all present)
    check "bullets ride wormholes" (shotThrough.Bullets |> List.exists (fun b -> b.Pos.X > 500.))
    let opened = run (int (portalEvery / dt) + 2) (all present) w0
    check "wormholes open on schedule, apart and inside the arena" (opened.Portals.Length = 1 && len (opened.Portals.Head.A - opened.Portals.Head.B) > 500. && abs opened.Portals.Head.A.X < arenaHalf && abs opened.Portals.Head.B.Y < arenaHalf)
    let twinned = { opened with PortalIn = 0. } |> step dt (all present)
    check "a second wormhole pair never opens while one is live" (twinned.Portals.Length = 1)
    let well = { w0 with Hole = Some { Pos = v 300. 0.; Life = 5. } }
    let drawn = well |> place 0 zero 0. |> run 120 (all present)
    check "a black hole pulls a resting ship toward it" (drawn.Ships.[0].Vel.X > 20. && drawn.Ships.[0].Pos.X > 0.)
    let swallowed = well |> place 0 (v 300. 0.) 0. |> step dt (all present)
    check "the core of a black hole downs a ship" (not swallowed.Ships.[0].Alive && swallowed.Ships.[0].LastWeapon = Singularity)
    let bent = well |> place 0 (v -200. -200.) 0. |> step dt firing |> run 30 (all present)
    check "bullets bend around a black hole" (bent.Bullets |> List.exists (fun b -> b.Vel.Y > 5.))
    let collapsed = run (int (holeEvery / dt) + 2) (all present) w0
    check "a black hole opens on schedule inside the arena" (match collapsed.Hole with Some h -> abs h.Pos.X < arenaHalf && abs h.Pos.Y < arenaHalf | None -> false)
    let fixedHole = { w0 with Hole = Some { Pos = v 300. 0.; Life = infinity }; HoleIn = 0. }
    let held = run (int (holeEvery / dt) + 60) (all present) fixedHole
    check "a map's black hole never expires" (match held.Hole with Some h -> System.Double.IsInfinity h.Life && h.Pos = v 300. 0. | None -> false)
    check "a map's black hole blocks the wandering one" (held.Events |> List.forall (function HoleOpen _ -> false | _ -> true))
    mapHole <- Some(v 300. 0., 90., 2e7)
    check "a map's black hole uses its own core and gravity" (holeCoreNow () = 90. && holeGNow () = 2e7)
    let heavier = fixedHole |> place 0 (v 700. 0.) 0. |> run 30 (all present)
    mapHole <- None
    let lighter = fixedHole |> place 0 (v 700. 0.) 0. |> run 30 (all present)
    check "a heavier map hole pulls harder" (heavier.Ships.[0].Vel.X < lighter.Ships.[0].Vel.X)
    check "layouts without a hole leave the wandering one alone" (layouts |> Array.forall (fun l -> l.Hole.IsNone))

    let ghosting = out |> edit 0 (fun s -> { s with Ghosting = true; Angle = 0. })
    let ghostInputs onOff = Array.init 4 (fun i -> if i = 0 then { present with Thrust = true; Boost = onOff } else present)
    let drifting = run 90 (ghostInputs false) ghosting
    check "a ghost accelerates and turns like a live ship, not an instant dart"
        (launcher drifting.Ships.[0] && drifting.Ships.[0].Pos.X > 20. && drifting.Ships.[0].Pos.X < arenaHalf)
    let boosting = run 90 (ghostInputs true) ghosting
    check "a ghost's boost never drains" (boosting.Ships.[0].Boost = boostMax)
    let dropped = ghosting |> step dt firing
    check "a ghost drops a live mine instead of a rock" (dropped.Rocks.IsEmpty && dropped.Mines.Length = 1 && dropped.Mines.Head.Fuse > 0. && dropped.Ships.[0].LaunchCd > 0.)
    check "ghost/launcher swap defaults to F, sharing Special's key since the dead have no weapon"
        (Binds.defaults Binds.Swap = [| "KeyF" |] && Binds.defaults Binds.Swap = Binds.defaults Binds.Special)

    check "launchers never win" ((step dt (all present) { out with Ships = out.Ships |> Array.mapi (fun i s -> if i = 1 then s else { s with Alive = false; Stocks = 0 }) }).Phase = Over(Some 1))

    mode <- Practice
    target <- 3
    let staged = stage w0
    check "practice lines shooters up against the target"
        (staged.Ships.[0..2] |> Array.forall (fun s -> s.Pos.X < 0.) && staged.Ships.[3].Pos.X > 0. && staged.Mines.Length = 1 && staged.Crates.[0].RespawnIn = 0.)
    check "practice arms any weapon" ((arm 0 Rail staged).Ships.[0].Ammo = railAmmo)
    let range = staged |> edit 3 (fun s -> { s with Hp = 0. }) |> step dt (all present)
    check "practice never costs a stock" (range.Ships.[3].Stocks = stocks && range.Phase = Playing)
    let grabbed = staged |> place 0 staged.Crates.[0].Pos 0. |> step dt (all present)
    check "practice crate cycles weapons and comes right back" (grabbed.Ships.[0].Weapon = Rail && grabbed.Crates.[0].RespawnIn = 1. && grabbed.Crates.[0].Pos = staged.Crates.[0].Pos)
    mode <- Arena
    target <- -1

    let behindOne = w0 |> edit 0 (fun s -> { s with Alive = false; Stocks = 1; RespawnIn = dt / 2. }) |> step dt (all present)
    check "the underdog respawns with full boost and a shield" (behindOne.Ships.[0].Boost = boostMax && behindOne.Ships.[0].Shield = shieldAmount)
    catchUp <- false
    let noMercy = w0 |> edit 0 (fun s -> { s with Alive = false; Stocks = 1; RespawnIn = dt / 2. }) |> step dt (all present)
    check "catch-up can be switched off" (noMercy.Ships.[0].Boost = boostStart && noMercy.Ships.[0].Shield = 0.)
    catchUp <- true
    let even = w0 |> edit 0 (fun s -> { s with Alive = false; RespawnIn = dt / 2. }) |> step dt (all present)
    check "an even ship gets no catch-up" (even.Ships.[0].Boost = boostStart)

    let crateAt = w0.Crates.[0].Pos
    mutator <- 1
    let railsOnly = w0 |> place 0 crateAt 0. |> step dt (all present)
    check "RAILS ONLY hands out railguns" (railsOnly.Ships.[0].Weapon = Rail)
    mutator <- 2
    let fast = (w0 |> place 0 zero 0. |> run 30 thruster).Ships.[0].Vel.X
    mutator <- 3
    let coasting = w0 |> place 0 zero 0. |> edit 0 (fun s -> { s with Vel = v 200. 0. }) |> run 120 (all present)
    mutator <- 0
    let braking = w0 |> place 0 zero 0. |> edit 0 (fun s -> { s with Vel = v 200. 0. }) |> run 120 (all present)
    check "TURBO thrusts harder" (fast > fwd * 1.3)
    check "ICE keeps ships sliding" (coasting.Ships.[0].Vel.X > braking.Ships.[0].Vel.X + 10.)

    let midway = run 240 three (step dt three initial) |> edit 0 (fun s -> { s with Stocks = 1 })
    let late = step dt (all present) midway
    check "a drop-in ship arrives alive with full stocks" (late.Ships.[3].Active && late.Ships.[3].Alive && late.Ships.[3].Stocks = stocks && late.Ships.[0].Stocks = 1)

    check "border is whole until the clock runs out" (bounds matchTime = 1. && bounds (matchTime + shrinkTime) = shrinkMin)
    let late = { w0 with Time = matchTime + shrinkTime + 1. } |> place 0 (v (arenaHalf * 0.9) 0.) 0.
    let closed = step dt (all present) late
    check "sudden death border kills a ship at the old edge" (not closed.Ships.[0].Alive && closed.Ships.[0].Rings = 1)
    let healPad = w0.Pads |> Array.findIndex (fun p -> p.Kind = 1)
    let starving =
        { w0 with Time = matchTime + 1. } |> place 0 w0.Pads.[healPad].Pos 0. |> edit 0 (fun s -> { s with Hp = 10. })
        |> step dt (all present)
    check "heal pads sleep in sudden death" (starving.Ships.[0].Hp = 10.)

    let special = Array.init 4 (fun i -> if i = 0 then { present with Special = true } else present)

    let walled = w0 |> place 0 zero 0. |> arm 0 Barrier |> step dt special
    check "a barrier drops in front of its owner" (walled.Deploys |> List.exists (fun d -> d.Kind = wallKind))
    let incoming = { Owner = 1; Pos = v 300. 0.; Vel = v -600. 0.; Life = 2.; Kind = 0; Damage = bulletDamage }
    let stopped = { walled with Bullets = [ incoming ] } |> edit 0 (fun s -> { s with Invuln = 0. }) |> run 60 (all present)
    check "a barrier eats a bullet before it reaches the ship behind" (stopped.Bullets.IsEmpty && stopped.Ships.[0].Hp = hpMax)

    let turret = w0 |> place 0 zero 0. |> place 1 (v 200. 0.) 0. |> arm 0 Sentry |> step dt special
    check "a sentry lands at its owner's tail" (turret.Deploys |> List.exists (fun d -> d.Kind = turretKind))
    let firing = turret |> run 30 (all present)
    check "a sentry shoots the nearest enemy and never its owner" (not firing.Bullets.IsEmpty && firing.Bullets |> List.forall (fun b -> b.Owner = 0))
    let nest = (turret.Deploys |> List.find (fun d -> d.Kind = turretKind)).Pos
    let shells = [ for i in 0..1 -> { Owner = 1; Pos = nest + v 0. (float i * 2.); Vel = zero; Life = 1.; Kind = 0; Damage = sentryHp / 2. } ]
    let wrecked = { turret with Bullets = shells } |> run 2 (all present)
    check "enemy fire wrecks a sentry" (wrecked.Deploys |> List.forall (fun d -> d.Kind <> turretKind))

    let glide (w: World) =
        let b = { Owner = 1; Pos = v -150. -500.; Vel = v 600. 0.; Life = 3.; Kind = 0; Damage = 0. }
        match ({ w with Bullets = [ b ] } |> run 40 (all present)).Bullets with
        | [ x ] -> x.Pos.X + 150.
        | _ -> 0.
    let bubbled = glide (w0 |> place 0 (v 0. -600.) 0. |> arm 0 Bubble |> step dt special)
    let plain = glide (w0 |> place 0 (v 0. -600.) 0. |> step dt (all present))
    check "a time bubble drags an enemy bullet down to bubbleFactor speed" (plain > 100. && abs (bubbled - plain * bubbleFactor) < 4.)

    mode <- Race
    setLayout (layouts |> Array.findIndex (fun l -> l.Track.IsSome))
    let grid = step dt (all present) initial
    check "race grid sits behind the start gate" (grid.Ships |> Array.forall (fun s -> s.Active && len (s.Pos - gates.[0]) < 400.))
    let skip = grid |> place 0 gates.[2] 0. |> step dt (all present)
    check "a gate out of order does not count" (skip.Ships.[0].Next = 1)
    let lapOnce w = Seq.fold (fun w g -> w |> place 0 gates.[g % gates.Length] 0. |> step dt (all present)) w (seq { 1 .. gates.Length })
    let lap1 = lapOnce grid
    check "passing every gate in order counts a lap" (lap1.Ships.[0].Laps = 1 && lap1.Ships.[0].Next = 1)
    let crashed = lap1 |> edit 0 (fun s -> { s with Hp = 0. }) |> run (int (respawnDelay / dt) + 2) (all present)
    check "a race crash keeps stocks, laps and respawns at the last gate" (crashed.Ships.[0].Alive && crashed.Ships.[0].Stocks = stocks && crashed.Ships.[0].Laps = 1 && len (crashed.Ships.[0].Pos - gates.[0]) < 1.)
    let won = Seq.fold (fun w _ -> lapOnce w) crashed (seq { 2 .. int laps })
    let first = won.Ships.[0]
    check "finishing every lap parks the ship and marks the time" (first.Finish > 0. && not first.Alive && won.Phase = Playing)
    check "the finish is announced with a place" (won.Events |> List.exists (function Finished(0, 1) -> true | _ -> false))
    let flag = run (int (raceGrace / dt) + 2) (all present) won
    check "the race ends for everyone after the grace period" (flag.Phase = Over(Some 0))
    let sprint (p: V2) = (grid |> place 0 p 0. |> run 360 thruster).Ships.[0].Vel |> len
    check "off the road a ship is slower than on it" (sprint (v 200. -600.) < sprint gates.[0] * 0.7)
    let ahead = lap1 |> edit 1 (fun s -> { s with Pos = gates.[3]; Next = 4 })
    check "a lap ahead ranks first, then the ship with more gates behind it" ((rank ahead.Ships).[0..1] = [| 0; 1 |])
    check "a finished ship ranks above everyone still racing" (Sim.rank won.Ships |> Array.head = 0 && Sim.place won.Ships 0 = 1)
    for t in 0 .. tracks.Length - 1 do
        setLayout (layouts.Length - tracks.Length + t)
        let solo = { initial with Ships = initial.Ships |> Array.mapi (fun i s -> if i = 1 then { freshShip 1 with Invuln = 0. } else s) }
        let lapped = Seq.fold (fun w _ -> step dt (Array.init 4 (fun i -> if i = 1 then bot w 1 else noInput)) w) solo (seq { 1 .. 120 * 90 })
        check (sprintf "track %d: a lone bot laps the circuit within 90 s" t) (lapped.Ships.[1].Laps >= 1)
        let boosts = layouts.[layouts.Length - tracks.Length + t].Pads |> List.filter (fun (_, _, k) -> k = 0)
        check (sprintf "track %d: boost pads come in clusters and every one sits on the road" t) (boosts.Length >= 14 && boosts |> List.forall (fun (p, _, _) -> Track.onTrack p))
    setLayout (layouts |> Array.findIndex (fun l -> l.Track.IsSome))
    let quiet = grid |> place 0 gates.[0] 0. |> step dt shooter
    check "the blaster stays silent in a race" (quiet.Bullets.IsEmpty)
    let crated = grid |> place 0 grid.Crates.[0].Pos 0. |> step dt (all present)
    // The crate RNG used to be an LCG truncated with `% 1000003`, which collapsed
    // it to a 124-long cycle and skewed the crateTiers weights badly.
    let seenRng = System.Collections.Generic.HashSet<int>()
    let mutable rr = 7
    while seenRng.Add rr && seenRng.Count < 200000 do
        rr <- nextRng rr
    check "the crate rng does not repeat itself quickly" (seenRng.Count >= 200000)
    let drawn n (pick: int -> Weapon) =
        let c = System.Collections.Generic.Dictionary<Weapon, int>()
        let mutable r = 7
        for _ in 1..n do
            r <- nextRng r
            let w = pick r
            c.[w] <- (if c.ContainsKey w then c.[w] else 0) + 1
        c
    let evenly (tiers: (Weapon * int)[]) (pick: int -> Weapon) =
        let n = 120000
        let total = tiers |> Array.sumBy snd
        drawn n pick
        |> Seq.forall (fun (KeyValue(w, k)) ->
            let want = float n * float (tiers |> Array.find (fun (x, _) -> x = w) |> snd) / float total
            abs (float k - want) / want < 0.05)
    check "crates follow the tier weights in an arena match"
        (evenly crateTiers (fun r -> crateWeapons.[rngIndex crateWeapons.Length r]))
    check "crates are evenly spread across the race arsenal"
        (evenly (raceArsenal |> Array.map (fun w -> w, 1)) (fun r -> raceArsenal.[rngIndex raceArsenal.Length r]))
    check "race crates only hand out stun weapons" (Array.contains crated.Ships.[0].Weapon raceArsenal
         && ((crated.Ships.[0].Weapon <> Swarm && crated.Ships.[0].Weapon <> Scatter) || crated.Ships.[0].Ammo = 1))
    // Walk enough crates to see every entry in the race arsenal at least once,
    // so the one-shot rule is checked against a real Swarm and Scatter grab.
    let grabs =
        Seq.fold
            (fun (w, seen) _ ->
                let w = run 60 (all present) w
                let w = w |> place 0 w.Crates.[0].Pos 0. |> step dt (all present)
                w, (w.Ships.[0].Weapon, w.Ships.[0].Ammo) :: seen)
            (grid, [])
            (seq { 1..60 })
        |> snd
    check "a race crate hands the zapper a single shot"
        (grabs |> List.exists (fun (w, _) -> w = Scatter)
         && grabs |> List.forall (fun (w, a) -> (w <> Swarm && w <> Scatter) || a = 1))
    let seeker = { Owner = 1; Pos = gates.[0] - v 30. 0.; Vel = v 400. 0.; Life = 1.; Kind = 2; Damage = seekerDamage }
    let stung = { grid with Bullets = [ seeker ] } |> place 0 gates.[0] 0. |> edit 0 (fun s -> { s with Invuln = 0. }) |> run 12 (all present)
    check "a race hit stuns instead of hurting" (stung.Ships.[0].Stun > 0. && stung.Ships.[0].Hp = hpMax)
    check "a race hit stuns for about a second and spins the ship"
        (stung.Ships.[0].Stun > scatterStun - 0.2 && stung.Ships.[0].Spin <> 0.)
    check "the race arsenal only holds weapons that work in a race"
        (raceArsenal |> Array.forall (fun w -> w <> Sentry && w <> Blaster))
    check "the race repulsor has a shorter reach than the arena one" (racePulseRange < pulseRange)
    check "a race time bubble fades sooner than an arena one" (raceBubbleLife < bubbleLife)
    // The tractor sorts by reach in a race and by aim angle otherwise, so a
    // near target off to the side must win over a distant one dead ahead.
    let towed =
        grid |> place 0 gates.[0] 0.
        // Ship 1 is near but off to the side, ship 2 is dead ahead but further,
        // ship 3 is parked square abeam so it falls outside the tractor cone.
        |> place 1 (gates.[0] + v 60. 40.) 0.
        |> place 2 (gates.[0] + v 260. 0.) 0.
        |> place 3 (gates.[0] + v 0. 900.) 0.
        |> edit 0 (fun s -> { s with Weapon = Tractor; Ammo = 2; Tow = NoTether })
        |> run (int (railCharge / dt) + 2) (all { present with Special = true })
    check "a race tractor grabs the closest target, not the straightest"
        (towed.Ships.[0].Tow = TowShip 1)
    let minePos = gates.[0] + v 0. 200.
    let laid = { grid with Mines = [ { Owner = 1; Pos = minePos; Vel = zero; Fuse = -1. } ] } |> edit 0 (fun s -> { s with Invuln = 0. })
    let waiting = laid |> place 0 (minePos + v 80. 0.) 0. |> run 30 (all present)
    check "a race mine stays put and dormant beside a ship" (waiting.Mines.Length = 1 && waiting.Mines.Head.Fuse < 0. && waiting.Mines.Head.Pos = minePos)
    let touched = laid |> place 0 (minePos + v 20. 0.) 0. |> step dt (all present)
    check "a race mine blasts on contact" (touched.Mines.IsEmpty && touched.Ships.[0].Stun > 0.)
    let missile = { Owner = 1; Pos = gates.[0] + v -300. 0.; Vel = v 400. 0.; Life = 2.; Kind = 2; Damage = seekerDamage }
    let straight = { grid with Bullets = [ missile ] } |> place 0 (gates.[0] + v 0. 250.) 0. |> run 30 (all present)
    check "a race missile flies straight past a target off its line" (straight.Bullets |> List.forall (fun b -> b.Vel.Y = 0.))
    mode <- Arena
    setLayout 0

    check "pilot levels start at 1 and climb on the XP curve" (Progress.level 0 = 1 && Progress.level 49 = 1 && Progress.level 50 = 2 && Progress.level 200 = 3)
    check "XP to the next level closes to zero exactly at the boundary" (Progress.toNext 0 = 50 && Progress.toNext 49 = 1 && Progress.toNext 50 = 150)
    let today = "2026-09-07"
    check "the daily picks are stable for one date and distinct in kind" (Progress.daily today = Progress.daily today && (Progress.daily today |> Array.map Progress.task |> Array.distinct).Length = 3)
    check "a different date picks a different set" (Seq.init 14 (fun d -> Progress.daily (sprintf "2026-09-%02d" (d + 1)) |> Array.toList) |> Seq.distinct |> Seq.length > 1)
    let won = { Progress.Won = true; Progress.Kills = 4; Progress.Deaths = 0; Progress.RaceTime = 0.; Progress.Clean = true }
    let winId = Progress.pool |> Array.findIndex (fun (t, g, _) -> t = Progress.WinMatches && g = 3)
    check "a win advances a win-matches task and completes it on the third" (Progress.advance winId 0 won = 1 && Progress.complete winId (Progress.advance winId 2 won))
    let bestId = Progress.pool |> Array.findIndex (fun (t, _, _) -> t = Progress.KillsInMatch)
    check "kills-in-one-match keeps the best, never the sum" (Progress.advance bestId 7 won = 7 && Progress.advance bestId 1 won = 4)
    let raceId = Progress.pool |> Array.findIndex (fun (t, _, _) -> t = Progress.RaceUnder)
    check "a race task ignores a match that was never raced" (Progress.advance raceId 0 won = 0 && Progress.advance raceId 0 { won with Progress.RaceTime = 60. } = 1)
    check "match XP pays the base, the win and every kill" (Progress.matchXp won = 23)
    check "a task pays its reward once, on the crossing" (Progress.earned [| winId |] [| 2 |] [| 3 |] = Progress.reward winId && Progress.earned [| winId |] [| 3 |] [| 3 |] = 0)
    let ids = Progress.daily today
    let date', ids', prog' = Progress.decode today (Progress.encode today ids [| 1; 2; 3 |])
    check "a saved daily round-trips" (date' = today && ids' = ids && prog' = [| 1; 2; 3 |])
    let _, _, rolled = Progress.decode "2026-09-08" (Progress.encode today ids [| 1; 2; 3 |])
    check "yesterday's daily resets when the date rolls over" (rolled = [| 0; 0; 0 |])
    let _, junkIds, junkProg = Progress.decode today "not|a|record"
    check "a corrupt daily falls back to a fresh set" (junkIds = Progress.daily today && junkProg = [| 0; 0; 0 |])

    let goldenHash = Determinism.golden
    let settled = Determinism.goldenRun ()
    let actual = Determinism.worldHash settled
    let moved = settled.Ships |> Array.filter (fun s -> len s.Pos > 1.) |> Array.length
    if actual <> goldenHash then
        printfn "  sim golden hash=0x%08X time=%.3f phase=%A alive=%d moved=%d" actual settled.Time settled.Phase (settled.Ships |> Array.filter (fun s -> s.Alive) |> Array.length) moved
    check "the golden run is still a live match, not a degenerate state" (settled.Phase = Playing && moved = 4)
    check "the scripted run hashes to the golden value" (actual = goldenHash)
    check "the same script replays to the same hash" (Determinism.worldHash (Determinism.goldenRun ()) = goldenHash)

    0
