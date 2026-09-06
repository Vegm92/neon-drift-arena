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
    check "explosion event emitted" (w5.Events |> List.exists (function Explode _ -> true | _ -> false))
    let w6 = run (int (respawnDelay / dt) + 2) (all present) w5
    check "respawns at a spawn point" (w6.Ships.[0].Alive && [ 0..3 ] |> List.exists (fun i -> w6.Ships.[0].Pos = spawnPos i))

    let w7 =
        w0 |> place 0 zero 0. |> place 1 (v 30. 0.) 0. |> edit 1 (fun s -> { s with Vel = v (-150.) 0. })
        |> step dt (all present)
    check "ram exchanges momentum" (w7.Ships.[0].Vel.X < -50. && w7.Ships.[1].Vel.X > -50.)

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
            check (tag "the road is smoothed through every corner") (road.Length = corners.Length * 4 && corners |> Array.forall (fun c -> Array.contains c road))
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
    check "asteroid stuns and spins" (s11.Stun > 0. && s11.Spin <> 0.)
    let w12 = run 10 (Array.init 4 (fun i -> if i = 0 then { present with Turn = 1.; Thrust = true } else present)) w11
    check "stunned ship ignores input" (w12.Ships.[0].Thrusting = 0.)
    let w13 = run (int (asteroidStun / dt) + 2) (all present) w12
    check "stun wears off" (w13.Ships.[0].Stun = 0. && w13.Ships.[0].Spin = 0.)

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

    check "launchers never win" ((step dt (all present) { out with Ships = out.Ships |> Array.mapi (fun i s -> if i = 1 then s else { s with Alive = false; Stocks = 0 }) }).Phase = Over(Some 1))

    practice <- true
    target <- 3
    let staged = stage w0
    check "practice lines shooters up against the target"
        (staged.Ships.[0..2] |> Array.forall (fun s -> s.Pos.X < 0.) && staged.Ships.[3].Pos.X > 0. && staged.Mines.Length = 1 && staged.Crates.[0].RespawnIn = 0.)
    check "practice arms any weapon" ((arm 0 Rail staged).Ships.[0].Ammo = railAmmo)
    let range = staged |> edit 3 (fun s -> { s with Hp = 0. }) |> step dt (all present)
    check "practice never costs a stock" (range.Ships.[3].Stocks = stocks && range.Phase = Playing)
    let grabbed = staged |> place 0 staged.Crates.[0].Pos 0. |> step dt (all present)
    check "practice crate cycles weapons and comes right back" (grabbed.Ships.[0].Weapon = Rail && grabbed.Crates.[0].RespawnIn = 1. && grabbed.Crates.[0].Pos = staged.Crates.[0].Pos)
    practice <- false
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

    race <- true
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
    setLayout (layouts |> Array.findIndex (fun l -> l.Track.IsSome))
    let quiet = grid |> place 0 gates.[0] 0. |> step dt shooter
    check "the blaster stays silent in a race" (quiet.Bullets.IsEmpty)
    let crated = grid |> place 0 grid.Crates.[0].Pos 0. |> step dt (all present)
    check "race crates only hand out stun weapons" (Array.contains crated.Ships.[0].Weapon raceArsenal && (crated.Ships.[0].Weapon <> Swarm || crated.Ships.[0].Ammo = 1))
    let seeker = { Owner = 1; Pos = gates.[0] - v 30. 0.; Vel = v 400. 0.; Life = 1.; Kind = 2; Damage = seekerDamage }
    let stung = { grid with Bullets = [ seeker ] } |> place 0 gates.[0] 0. |> edit 0 (fun s -> { s with Invuln = 0. }) |> run 12 (all present)
    check "a race hit stuns instead of hurting" (stung.Ships.[0].Stun > 0. && stung.Ships.[0].Hp = hpMax)
    let minePos = gates.[0] + v 0. 200.
    let laid = { grid with Mines = [ { Owner = 1; Pos = minePos; Vel = zero; Fuse = -1. } ] } |> edit 0 (fun s -> { s with Invuln = 0. })
    let waiting = laid |> place 0 (minePos + v 80. 0.) 0. |> run 30 (all present)
    check "a race mine stays put and dormant beside a ship" (waiting.Mines.Length = 1 && waiting.Mines.Head.Fuse < 0. && waiting.Mines.Head.Pos = minePos)
    let touched = laid |> place 0 (minePos + v 20. 0.) 0. |> step dt (all present)
    check "a race mine blasts on contact" (touched.Mines.IsEmpty && touched.Ships.[0].Stun > 0.)
    let missile = { Owner = 1; Pos = gates.[0] + v -300. 0.; Vel = v 400. 0.; Life = 2.; Kind = 2; Damage = seekerDamage }
    let straight = { grid with Bullets = [ missile ] } |> place 0 (gates.[0] + v 0. 250.) 0. |> run 30 (all present)
    check "a race missile flies straight past a target off its line" (straight.Bullets |> List.forall (fun b -> b.Vel.Y = 0.))
    race <- false
    setLayout 0

    0
